package com.teamfho.domino.social

import com.google.cloud.firestore.Firestore
import com.teamfho.domino.entitlement.SubscriptionPolicy
import com.teamfho.domino.security.FirebaseIdentity
import org.springframework.beans.factory.ObjectProvider
import org.springframework.context.annotation.Bean
import org.springframework.context.annotation.Configuration
import org.springframework.security.core.annotation.AuthenticationPrincipal
import org.springframework.web.bind.annotation.*

open class FriendshipServices(private val factory:()->FriendshipService) { open fun ready()=factory() }
@Configuration(proxyBeanMethods=false)
class FriendshipConfiguration {
    @Bean fun friendshipServices(db:ObjectProvider<Firestore>,cursor:SocialCursor,policy:SubscriptionPolicy)=FriendshipServices {
        FriendshipService(FirestoreFriendships(db.ifAvailable?:throw SocialFailure("SOCIAL_SERVICE_UNAVAILABLE")),cursor,policy)
    }
}
@RestController
class FriendshipController(private val services:FriendshipServices,private val social:SocialServices,private val rate:SocialRateGate) {
    private fun ready(i:FirebaseIdentity,action:SocialOperation):FriendshipService {
        rate.check(i.uid,action);social.ready().identity.ensure(i.uid);return services.ready()
    }
    @PostMapping("/api/v1/players/{id}/friend-request")
    fun send(@AuthenticationPrincipal i:FirebaseIdentity,@PathVariable id:String)=ready(i,SocialOperation.SEND).send(i.uid,id)
    @GetMapping("/api/v1/player/friend-requests")
    fun requests(@AuthenticationPrincipal i:FirebaseIdentity,@RequestParam direction:String,@RequestParam(required=false) cursor:String?,@RequestParam(defaultValue="20") limit:Int)=ready(i,SocialOperation.REQUESTS).requests(i.uid,direction,cursor,limit)
    @PostMapping("/api/v1/friend-requests/{id}/accept")
    fun accept(@AuthenticationPrincipal i:FirebaseIdentity,@PathVariable id:String)=ready(i,SocialOperation.ACCEPT).resolve(i.uid,id,FriendRequestStatus.ACCEPTED)
    @PostMapping("/api/v1/friend-requests/{id}/decline")
    fun decline(@AuthenticationPrincipal i:FirebaseIdentity,@PathVariable id:String)=ready(i,SocialOperation.DECLINE).resolve(i.uid,id,FriendRequestStatus.DECLINED)
    @DeleteMapping("/api/v1/friend-requests/{id}")
    fun cancel(@AuthenticationPrincipal i:FirebaseIdentity,@PathVariable id:String)=ready(i,SocialOperation.CANCEL).resolve(i.uid,id,FriendRequestStatus.CANCELED)
    @GetMapping("/api/v1/player/friends")
    fun friends(@AuthenticationPrincipal i:FirebaseIdentity,@RequestParam(required=false) cursor:String?,@RequestParam(defaultValue="20") limit:Int)=ready(i,SocialOperation.FRIENDS).friends(i.uid,cursor,limit)
    @DeleteMapping("/api/v1/player/friends/{id}")
    fun remove(@AuthenticationPrincipal i:FirebaseIdentity,@PathVariable id:String):Map<String,Boolean> {ready(i,SocialOperation.UNFRIEND).remove(i.uid,id);return mapOf("success" to true)}
}
