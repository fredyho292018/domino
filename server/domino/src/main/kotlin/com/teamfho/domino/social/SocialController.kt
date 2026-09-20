package com.teamfho.domino.social

import com.google.cloud.firestore.Firestore
import com.teamfho.domino.security.FirebaseIdentity
import org.springframework.beans.factory.ObjectProvider
import org.springframework.context.annotation.Bean
import org.springframework.context.annotation.Configuration
import org.springframework.data.redis.core.StringRedisTemplate
import org.springframework.data.redis.core.script.DefaultRedisScript
import org.springframework.http.ResponseEntity
import org.springframework.security.core.annotation.AuthenticationPrincipal
import org.springframework.web.bind.annotation.*
import java.util.Base64

fun interface SocialRateLimiter { fun check(uid: String,action: String) }
class RedisSocialRateLimiter(private val redis: () -> StringRedisTemplate?) : SocialRateLimiter {
    private val script=DefaultRedisScript("""
        local t=redis.call('TIME'); local now=tonumber(t[1])+tonumber(t[2])/1000000
        local rate=tonumber(ARGV[1])/60; local capacity=tonumber(ARGV[2])
        local old=redis.call('HMGET',KEYS[1],'tokens','time')
        local tokens=math.min(capacity,(tonumber(old[1]) or capacity)+math.max(0,now-(tonumber(old[2]) or now))*rate)
        if tokens<1 then return 0 end
        redis.call('HSET',KEYS[1],'tokens',tokens-1,'time',now);redis.call('EXPIRE',KEYS[1],120);return 1
    """.trimIndent(),Long::class.java)
    override fun check(uid: String,action: String) {
        // A safety block must remain usable when Redis is unavailable.
        if(action=="block")return
        val limit=when(action){"NAME"->20;"FRIEND_CODE","unblock"->30;else->60}
        val burst=if(action=="NAME")5 else limit
        val hash=java.security.MessageDigest.getInstance("SHA-256").digest(uid.toByteArray()).joinToString(""){"%02x".format(it)}
        try {
            val result=redis()?.execute(script,listOf("domino:v1:social:rate:$hash:$action"),limit.toString(),burst.toString())
                ?:throw SocialFailure("SOCIAL_SERVICE_UNAVAILABLE")
            if(result!=1L)throw SocialFailure("SOCIAL_ACTION_RATE_LIMITED",429)
        } catch(e:SocialFailure){throw e}catch(_:Exception){throw SocialFailure("SOCIAL_SERVICE_UNAVAILABLE")}
    }
}
@Configuration(proxyBeanMethods=false)
class SocialConfiguration {
    @Bean fun socialCursor():SocialCursor = System.getenv("DOMINO_SOCIAL_CURSOR_KEY")?.let {SocialCursor(Base64.getDecoder().decode(it))} ?: SocialCursor()
    @Bean fun socialRateLimiter(redis:ObjectProvider<StringRedisTemplate>):SocialRateLimiter=RedisSocialRateLimiter {redis.ifAvailable}
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
class SocialController(private val services:SocialServices,private val rate:SocialRateLimiter) {
    private fun ready(identity:FirebaseIdentity,action:String):SocialServices.Bundle {
        rate.check(identity.uid,action)
        return services.ready().also {it.identity.ensure(identity.uid)}
    }
    @GetMapping("/api/v1/player/social-summary")
    fun summary(@AuthenticationPrincipal i:FirebaseIdentity)=ready(i,"summary").profiles.summary(i.uid)
    @GetMapping("/api/v1/players/{id}/profile")
    fun profile(@AuthenticationPrincipal i:FirebaseIdentity,@PathVariable id:String)=ready(i,"profile").profiles.profile(i.uid,id)
    @GetMapping("/api/v1/players/search")
    fun search(@AuthenticationPrincipal i:FirebaseIdentity,@RequestParam mode:String,@RequestParam(required=false) q:String?,
        @RequestParam(required=false) friendCode:String?,@RequestParam(required=false) cursor:String?,@RequestParam(defaultValue="20") limit:Int):SocialPage<PublicPlayerProfile> {
        socialCheck(mode in setOf("NAME","FRIEND_CODE"),"INVALID_SEARCH_QUERY")
        return ready(i,mode).discovery.search(i.uid,mode,q,friendCode,cursor,limit)
    }
    @GetMapping("/api/v1/player/social-settings")
    fun settings(@AuthenticationPrincipal i:FirebaseIdentity)=ready(i,"settings").privacy.getEffectiveSocialPrivacy(i.uid)
    @PatchMapping("/api/v1/player/social-settings")
    fun settings(@AuthenticationPrincipal i:FirebaseIdentity,@RequestBody fields:Map<String,Any?>):SocialPrivacySettings {
        socialCheck(fields.keys==setOf("discoverableByName","revision") && fields["discoverableByName"] is Boolean &&
            fields["revision"] is Number && Regex("[1-9][0-9]{0,15}").matches(fields["revision"].toString()),"INVALID_SEARCH_QUERY")
        return ready(i,"settings").privacy.update(i.uid,PrivacyPatch(fields["discoverableByName"] as Boolean,(fields["revision"] as Number).toLong()))
    }
    @GetMapping("/api/v1/player/blocks")
    fun blocks(@AuthenticationPrincipal i:FirebaseIdentity,@RequestParam(required=false) cursor:String?,@RequestParam(defaultValue="20") limit:Int)=ready(i,"blocks").blocks.list(i.uid,cursor,limit)
    @PostMapping("/api/v1/players/{id}/block")
    fun block(@AuthenticationPrincipal i:FirebaseIdentity,@PathVariable id:String):Map<String,Boolean> {ready(i,"block").blocks.set(i.uid,id,true);return mapOf("success" to true)}
    @DeleteMapping("/api/v1/players/{id}/block")
    fun unblock(@AuthenticationPrincipal i:FirebaseIdentity,@PathVariable id:String):Map<String,Boolean> {ready(i,"unblock").blocks.set(i.uid,id,false);return mapOf("success" to true)}
}
@RestControllerAdvice(assignableTypes=[SocialController::class])
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
