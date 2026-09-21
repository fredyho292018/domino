package com.teamfho.domino.social

import com.google.cloud.firestore.Firestore
import com.teamfho.domino.security.FirebaseIdentity
import org.springframework.beans.factory.ObjectProvider
import org.springframework.context.annotation.Bean
import org.springframework.context.annotation.Configuration
import org.springframework.data.redis.core.StringRedisTemplate
import org.springframework.http.ResponseEntity
import org.springframework.security.core.annotation.AuthenticationPrincipal
import org.springframework.web.bind.annotation.*
import java.util.Base64

@Configuration(proxyBeanMethods=false)
class SocialConfiguration {
    @Bean fun socialCursor():SocialCursor = System.getenv("DOMINO_SOCIAL_CURSOR_KEY")?.let {SocialCursor(Base64.getDecoder().decode(it))} ?: SocialCursor()
    @Bean(destroyMethod="close") fun socialRateGate(redis:ObjectProvider<StringRedisTemplate>,metrics:io.micrometer.core.instrument.MeterRegistry):SocialRateGate =
        ResilientSocialRateGate(BoundedSocialRedis(RedisSocialRateLimiter {redis.ifAvailable}),metrics=metrics)
    @Bean fun socialControllerServices(db:ObjectProvider<Firestore>,cursor:SocialCursor):SocialServices = SocialServices({
        FirestoreSocialRepository(db.ifAvailable?:throw SocialFailure("SOCIAL_SERVICE_UNAVAILABLE"))
    },cursor)
}
open class SocialServices(private val repository:()->FirestoreSocialRepository,private val cursor:SocialCursor) {
    open fun ready():Bundle {
        val r=repository();val identity=PublicPlayerIdentityService(r);val access=SocialAccess(r,r);val privacy=SocialPrivacyService(identity,r)
        return Bundle(identity,PublicPlayerProfileService(identity,r,privacy,access),PlayerDiscoveryService(r,r,access,cursor),privacy,BlockService(r,access,cursor))
    }
    data class Bundle(val identity:PublicPlayerIdentityService,val profiles:PublicPlayerProfileService,val discovery:PlayerDiscoveryService,
        val privacy:SocialPrivacyService,val blocks:BlockService)
}
@RestController
class SocialController(private val services:SocialServices,private val rate:SocialRateGate,private val friends:ObjectProvider<FriendshipServices>,private val follows:FollowServices) {
    private fun ready(identity:FirebaseIdentity,action:SocialOperation):SocialServices.Bundle {
        rate.check(identity.uid,action)
        return services.ready().also {it.identity.ensure(identity.uid)}
    }
    @GetMapping("/api/v1/player/social-summary")
    fun summary(@AuthenticationPrincipal i:FirebaseIdentity):Map<String,Any> {
        val s=ready(i,SocialOperation.SUMMARY).profiles.summary(i.uid)
        val result=mutableMapOf<String,Any>("profile" to s.profile,"privacy" to s.privacy)
        friends.ifAvailable?.let { provider->
            result["friends"]=try{provider.ready().summary(i.uid)}catch(_:Exception){FriendCapacity(0,null,false,0,"UNAVAILABLE")}
        }
        result["follows"]=follows.ready().counts(i.uid)
        return result
    }
    @GetMapping("/api/v1/players/{id}/profile")
    fun profile(@AuthenticationPrincipal i:FirebaseIdentity,@PathVariable id:String):Map<String,Any?> {
        val p=ready(i,SocialOperation.PROFILE).profiles.profile(i.uid,id)
        return mapOf("publicPlayerId" to p.publicPlayerId,"friendCode" to p.friendCode,"displayName" to p.displayName,"avatarKey" to p.avatarKey,
            "relationship" to friends.ifAvailable?.ready()?.relationship(i.uid,id))
    }
    @GetMapping("/api/v1/players/search")
    fun search(@AuthenticationPrincipal i:FirebaseIdentity,@RequestParam mode:String,@RequestParam(required=false) q:String?,
        @RequestParam(required=false) friendCode:String?,@RequestParam(required=false) cursor:String?,@RequestParam(defaultValue="20") limit:Int):SocialPage<PublicPlayerProfile> {
        socialCheck(mode in setOf("NAME","FRIEND_CODE"),"INVALID_SEARCH_QUERY")
        return ready(i,if(mode=="NAME")SocialOperation.SEARCH else SocialOperation.CODE_LOOKUP).discovery.search(i.uid,mode,q,friendCode,cursor,limit)
    }
    @GetMapping("/api/v1/player/social-settings")
    fun settings(@AuthenticationPrincipal i:FirebaseIdentity)=ready(i,SocialOperation.SETTINGS_READ).privacy.getEffectiveSocialPrivacy(i.uid)
    @PatchMapping("/api/v1/player/social-settings")
    fun settings(@AuthenticationPrincipal i:FirebaseIdentity,@RequestBody fields:Map<String,Any?>):SocialPrivacySettings {
        val allowed=setOf("discoverableByName","revision","friendRequests","follow","presenceVisibility","matchActivityVisibility")
        socialCheck(fields.keys.all{it in allowed} && fields.size>=2 && fields["revision"] is Number && Regex("[1-9][0-9]{0,15}").matches(fields["revision"].toString()),"INVALID_SEARCH_QUERY")
        socialCheck("discoverableByName" !in fields || fields["discoverableByName"] is Boolean,"INVALID_SEARCH_QUERY")
        fun contact(key:String):ContactPermission? {if(key !in fields)return null;return ContactPermission.entries.find{it.name==fields[key]}?:throw SocialFailure("INVALID_SEARCH_QUERY",400)}
        fun visibility(key:String):SocialVisibility? {if(key !in fields)return null;return SocialVisibility.entries.find{it.name==fields[key]}?:throw SocialFailure("INVALID_SEARCH_QUERY",400)}
        val patch=PrivacyPatch(fields["discoverableByName"] as Boolean?,(fields["revision"] as Number).toLong(),contact("friendRequests"),contact("follow"),visibility("presenceVisibility"),visibility("matchActivityVisibility"))
        return services.ready().privacy.updateGated(i.uid,patch,rate)
    }
    @GetMapping("/api/v1/player/blocks")
    fun blocks(@AuthenticationPrincipal i:FirebaseIdentity,@RequestParam(required=false) cursor:String?,@RequestParam(defaultValue="20") limit:Int)=ready(i,SocialOperation.BLOCK_LIST).blocks.list(i.uid,cursor,limit)
    @PostMapping("/api/v1/players/{id}/block")
    fun block(@AuthenticationPrincipal i:FirebaseIdentity,@PathVariable id:String):Map<String,Boolean> {ready(i,SocialOperation.BLOCK).blocks.set(i.uid,id,true);return mapOf("success" to true)}
    @DeleteMapping("/api/v1/players/{id}/block")
    fun unblock(@AuthenticationPrincipal i:FirebaseIdentity,@PathVariable id:String):Map<String,Boolean> {ready(i,SocialOperation.UNBLOCK).blocks.set(i.uid,id,false);return mapOf("success" to true)}
}
@RestControllerAdvice(assignableTypes=[SocialController::class,FriendshipController::class,FollowController::class])
@org.springframework.core.annotation.Order(org.springframework.core.Ordered.HIGHEST_PRECEDENCE)
class SocialErrors {
    @ExceptionHandler(Exception::class)
    fun error(e:Exception):ResponseEntity<Map<String,String>> {
        val known=generateSequence<Throwable>(e){it.cause}.take(20).filterIsInstance<SocialFailure>().firstOrNull()
        if(known!=null)return ResponseEntity.status(known.status).body(mapOf("code" to known.code))
        val invalid=e is org.springframework.web.bind.MissingServletRequestParameterException || e is org.springframework.http.converter.HttpMessageNotReadableException || e is org.springframework.web.method.annotation.MethodArgumentTypeMismatchException
        return ResponseEntity.status(if(invalid)400 else 503).body(mapOf("code" to if(invalid)"INVALID_SEARCH_QUERY" else "SOCIAL_SERVICE_UNAVAILABLE"))
    }
}
