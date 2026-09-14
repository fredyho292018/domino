package com.teamfho.domino.online

import com.google.cloud.firestore.Firestore
import com.teamfho.domino.catalog.GameCatalogService
import com.teamfho.domino.security.FirebaseIdentity
import org.springframework.beans.factory.ObjectProvider
import org.springframework.context.annotation.Bean
import org.springframework.context.annotation.Configuration
import org.springframework.http.ResponseEntity
import org.springframework.security.core.annotation.AuthenticationPrincipal
import org.springframework.web.bind.annotation.*

@Configuration(proxyBeanMethods=false)
class OnlineConfiguration {
    @Bean fun onlineRepository(db: ObjectProvider<Firestore>): OnlineRepository {
        val delegate=db.ifAvailable?.let(::FirestoreOnlineRepository)
        return object: OnlineRepository {
            fun ready()=delegate?:throw OnlineFailure(OnlineError.STORAGE_UNAVAILABLE)
            override fun create(state: OnlineState)=ready().create(state)
            override fun read(matchId: String)=ready().read(matchId)
            override fun events(matchId: String,after: Long)=ready().events(matchId,after)
            override fun transact(matchId: String,expectedSequence: Long,commandId: String,fingerprint: String,transition:(OnlineState)->OnlineWrite)=ready().transact(matchId,expectedSequence,commandId,fingerprint,transition)
        }
    }
    @Bean fun onlineMatchService(catalog: GameCatalogService,repository: OnlineRepository)=OnlineMatchService(catalog,repository)
}
data class CreateOnlineMatch(val modeKey: String)
data class JoinOnlineMatch(val commandId: String)
@RestController
class OnlineMatchController(private val service: OnlineMatchService) {
    @PostMapping("/api/v1/matches")
    fun create(@AuthenticationPrincipal identity: FirebaseIdentity,@RequestBody request: CreateOnlineMatch)=service.create(identity.uid,request.modeKey)
    @PostMapping("/api/v1/matches/{id}/join")
    fun join(@AuthenticationPrincipal identity: FirebaseIdentity,@PathVariable id: String,@RequestBody request: JoinOnlineMatch): OnlineSnapshot {
        service.join(identity.uid,id,request.commandId);return service.snapshot(identity.uid,id)
    }
    @GetMapping("/api/v1/matches/{id}/snapshot")
    fun snapshot(@AuthenticationPrincipal identity: FirebaseIdentity,@PathVariable id: String)=service.snapshot(identity.uid,id)
    @GetMapping("/api/v1/matches/{id}/events")
    fun events(@AuthenticationPrincipal identity: FirebaseIdentity,@PathVariable id: String,@RequestParam(defaultValue="0") afterSequence: Long)=service.events(identity.uid,id,afterSequence)
    @ExceptionHandler(OnlineFailure::class)
    fun failure(e: OnlineFailure): ResponseEntity<*> = ResponseEntity.status(when(e.code) {
        OnlineError.NOT_PARTICIPANT->403;OnlineError.MATCH_NOT_FOUND->404;OnlineError.STORAGE_UNAVAILABLE->503;else->409
    }).body(mapOf("code" to e.code.name))
}
