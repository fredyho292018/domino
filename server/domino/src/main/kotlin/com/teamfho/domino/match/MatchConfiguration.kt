package com.teamfho.domino.match

import com.google.cloud.firestore.Firestore
import com.teamfho.domino.catalog.GameCatalogService
import com.teamfho.domino.security.FirebaseIdentity
import org.springframework.beans.factory.ObjectProvider
import org.springframework.beans.factory.annotation.Value
import org.springframework.context.annotation.Bean
import org.springframework.context.annotation.Configuration
import org.springframework.http.ResponseEntity
import org.springframework.security.core.annotation.AuthenticationPrincipal
import org.springframework.web.bind.annotation.GetMapping
import org.springframework.web.bind.annotation.RequestParam
import org.springframework.web.bind.annotation.RestController

@Configuration(proxyBeanMethods=false)
class MatchConfiguration {
    @Bean fun matchStore(db: ObjectProvider<Firestore>): MatchStore = MatchStore(db.ifAvailable?.let(::FirestoreMatchRepository))
    @Bean fun spectatorPolicyRules(@Value("\${domino.match.min-hand-spectator-delay-seconds:\${MIN_HAND_SPECTATOR_DELAY_SECONDS:60}}") minimum: Long)=SpectatorPolicyRules(minimum)
    @Bean fun matchPersistenceService(catalog: GameCatalogService,store: MatchStore,policies: SpectatorPolicyRules)=MatchPersistenceService(catalog,store,spectators=policies)
}
// Availability boundary: absent Firestore disables Match persistence, never offline Unity gameplay.
class MatchStore(private val delegate: FirestoreMatchRepository?): MatchRepository,MatchEventRepository,PlayerMatchHistoryRepository {
    private fun ready()=delegate?:error("MATCH_STORAGE_UNAVAILABLE")
    override fun create(match: Match)=ready().create(match)
    override fun read(matchId: String)=ready().read(matchId)
    override fun round(matchId: String,number: Int)=ready().round(matchId,number)
    override fun transact(matchId: String,expectedSequence: Long,commandId: String,transition:(MatchAggregate)->MatchWrite)=ready().transact(matchId,expectedSequence,commandId,transition)
    override fun readTrustedEvents(matchId: String,afterSequence: Long,limit: Int)=ready().readTrustedEvents(matchId,afterSequence,limit)
    override fun history(uid: String,limit: Int,afterMatchId: String?)=ready().history(uid,limit,afterMatchId)
}
@RestController
class MatchHistoryController(private val history: PlayerMatchHistoryRepository,
    private val access:com.teamfho.domino.entitlement.EntitlementHistory) {
    @GetMapping("/api/v1/players/me/matches")
    fun history(@AuthenticationPrincipal identity: FirebaseIdentity,@RequestParam(defaultValue="20") limit: Int,
        @RequestParam(required=false) cursor: String?): ResponseEntity<*> {
        if(limit !in 1..100)return ResponseEntity.badRequest().body(mapOf("code" to "HISTORY_PAGE_INVALID"))
        if(cursor!=null && runCatching {MatchIds.document(cursor)}.isFailure)return ResponseEntity.badRequest().body(mapOf("code" to "HISTORY_CURSOR_INVALID"))
        return try {ResponseEntity.ok(access.page(identity.uid,limit,cursor))}
        catch(e:com.teamfho.domino.entitlement.EntitlementFailure){throw e}
        catch (_: Exception) {ResponseEntity.status(503).body(mapOf("code" to "MATCH_HISTORY_UNAVAILABLE"))}
    }
}
