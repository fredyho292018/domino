package com.teamfho.domino.social

import com.google.cloud.firestore.Firestore
import com.teamfho.domino.security.FirebaseIdentity
import org.springframework.beans.factory.ObjectProvider
import org.springframework.context.annotation.Bean
import org.springframework.context.annotation.Configuration
import org.springframework.security.core.annotation.AuthenticationPrincipal
import org.springframework.web.bind.annotation.*

open class FollowServices(private val factory:()->FollowService) { open fun ready()=factory() }
@Configuration(proxyBeanMethods=false)
class FollowConfiguration {
    @Bean fun followServices(db:ObjectProvider<Firestore>,cursor:SocialCursor)=FollowServices {
        FollowService(FirestoreFriendships(db.ifAvailable?:throw SocialFailure("SOCIAL_SERVICE_UNAVAILABLE")),cursor)
    }
}
@RestController
class FollowController(private val services:FollowServices,private val social:SocialServices,private val rate:SocialRateGate) {
    private fun ready(i:FirebaseIdentity,action:SocialOperation):FollowService {
        rate.check(i.uid,action);social.ready().identity.ensure(i.uid);return services.ready()
    }
    @PostMapping("/api/v1/players/{id}/follow")
    fun follow(@AuthenticationPrincipal i:FirebaseIdentity,@PathVariable id:String):Map<String,Boolean> {ready(i,SocialOperation.FOLLOW).set(i.uid,id,true);return mapOf("success" to true)}
    @DeleteMapping("/api/v1/players/{id}/follow")
    fun unfollow(@AuthenticationPrincipal i:FirebaseIdentity,@PathVariable id:String):Map<String,Boolean> {ready(i,SocialOperation.UNFOLLOW).set(i.uid,id,false);return mapOf("success" to true)}
    @GetMapping("/api/v1/player/followers")
    fun followers(@AuthenticationPrincipal i:FirebaseIdentity,@RequestParam(required=false) cursor:String?,@RequestParam(defaultValue="20") limit:Int)=ready(i,SocialOperation.FOLLOW_LIST).list(i.uid,false,cursor,limit)
    @GetMapping("/api/v1/player/following")
    fun following(@AuthenticationPrincipal i:FirebaseIdentity,@RequestParam(required=false) cursor:String?,@RequestParam(defaultValue="20") limit:Int)=ready(i,SocialOperation.FOLLOW_LIST).list(i.uid,true,cursor,limit)
}
