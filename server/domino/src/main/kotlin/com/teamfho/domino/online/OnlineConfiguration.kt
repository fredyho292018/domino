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
    @Bean fun turnDueIndex(redis:org.springframework.data.redis.core.StringRedisTemplate,db:ObjectProvider<Firestore>):TurnDueIndex =
        if(db.ifAvailable!=null)RedisTurnDueIndex(redis) else object:TurnDueIndex {
            override fun lead(owner:String)=0
            override fun release(owner:String){}
            override fun offer(owner:String,id:String,due:java.time.Instant?)=false
            override fun claim(now:java.time.Instant)=emptyList<String>()
            override fun forget(id:String){}
        }
    @Bean fun turnWorkFeed(db:ObjectProvider<Firestore>):TurnWorkFeed = db.ifAvailable?.let(::FirestoreTurnWorkFeed)
        ?:TurnWorkFeed {_,_->AutoCloseable {}}
    @Bean(destroyMethod="close") fun turnIndexBridge(index:TurnDueIndex,feed:TurnWorkFeed)=TurnIndexBridge(index,feed)
    // Keep existing lifecycle jobs separate from potentially slow Firestore discovery.
    @Bean fun taskScheduler()=org.springframework.scheduling.concurrent.ThreadPoolTaskScheduler().apply {
        poolSize=1;setThreadNamePrefix("realtime-lifecycle-")
    }
    @Bean fun onlineTurnScheduler()=org.springframework.scheduling.concurrent.ThreadPoolTaskScheduler().apply {
        poolSize=1;setThreadNamePrefix("online-turn-")
    }
    @Bean fun onlineRepository(db: ObjectProvider<Firestore>): OnlineRepository {
        val delegate=db.ifAvailable?.let(::FirestoreOnlineRepository)
        return object: OnlineRepository {
            fun ready()=delegate?:throw OnlineFailure(OnlineError.STORAGE_UNAVAILABLE)
            override fun create(state: OnlineState)=ready().create(state)
            override fun createPaired(write: OnlineWrite)=ready().createPaired(write)
            override fun settleFailedCreation(id:String)=ready().settleFailedCreation(id)
            override fun read(matchId: String)=ready().read(matchId)
            override fun events(matchId: String,after: Long)=ready().events(matchId,after)
            override fun due(now: java.time.Instant)=ready().due(now)
            override fun activeFor(uid: String)=ready().activeFor(uid)
            override fun refreshDiscovery(matchId: String,now: java.time.Instant)=ready().refreshDiscovery(matchId,now)
            override fun transact(matchId: String,expectedSequence: Long,commandId: String,fingerprint: String,transition:(OnlineState)->OnlineWrite)=ready().transact(matchId,expectedSequence,commandId,fingerprint,transition)
        }
    }
    @Bean fun abandonedMatchReconciliationGate(
        @org.springframework.beans.factory.annotation.Value("\${domino.online.legacy-reconciliation.file:}") file:String,
        @org.springframework.beans.factory.annotation.Value("\${domino.online.legacy-reconciliation.run-id:}") runId:String,
        @org.springframework.beans.factory.annotation.Value("\${domino.online.legacy-reconciliation.registry-sha256:}") hash:String,
        @org.springframework.beans.factory.annotation.Value("\${domino.online.legacy-reconciliation.mode:EXACT_22}") mode:String,
        @org.springframework.beans.factory.annotation.Value("\${domino.online.legacy-reconciliation.match-id:}") matchId:String
    ):AbandonedMatchReconciliationGate=FileAbandonedMatchReconciliationGate(file.takeIf{it.isNotBlank()}?.let{runCatching{java.nio.file.Path.of(it)}.getOrNull()},runId,hash,mode,matchId)
    @Bean fun onlineMatchService(catalog: GameCatalogService,repository: OnlineRepository,profiles:OnlineParticipantProfiles,
        gate:AbandonedMatchReconciliationGate)=
        OnlineMatchService(catalog,repository,engine=OnlineEngine(reconciliationGate=gate),profiles=profiles::get)
    @Bean fun onlineParticipantProfiles(db:ObjectProvider<Firestore>)=OnlineParticipantProfiles {uid ->
            try {
                val database=db.ifAvailable?:throw OnlineFailure(OnlineError.STORAGE_UNAVAILABLE)
                val doc=com.teamfho.domino.matchmaking.CreationRecorder.phase("profileRead"){database.document("players/${com.teamfho.domino.match.MatchIds.document(uid)}").get().get(15,java.util.concurrent.TimeUnit.SECONDS)}
                com.teamfho.domino.matchmaking.CreationRecorder.firestoreActivity()
                checkOnline(doc.exists(),OnlineError.STORAGE_UNAVAILABLE)
                val name=doc.getString("displayName")
                checkOnline(!name.isNullOrBlank(),OnlineError.STORAGE_UNAVAILABLE)
                val marker=com.teamfho.domino.matchmaking.CreationRecorder.phase("testMarkerRead"){database.document("developmentTestAccounts/$uid").get().get(15,java.util.concurrent.TimeUnit.SECONDS)}
                com.teamfho.domino.matchmaking.CreationRecorder.firestoreActivity()
                OnlineParticipantProfile(name,marker.getBoolean("isTestAccount")==true&&marker.getString("testSource")=="BOT_SWARM")
            } catch(e:OnlineFailure){throw e}
            catch(_:Exception){throw OnlineFailure(OnlineError.STORAGE_UNAVAILABLE)}
        }
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
