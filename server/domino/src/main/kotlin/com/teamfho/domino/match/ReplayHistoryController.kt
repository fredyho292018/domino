package com.teamfho.domino.match

import com.teamfho.domino.security.FirebaseIdentity
import org.springframework.http.ResponseEntity
import org.springframework.security.core.annotation.AuthenticationPrincipal
import org.springframework.web.bind.annotation.*

data class ReplayHistoryItem(val history: PlayerMatchHistory, val participants: List<PublicParticipant>,
    val selfSeat: Int?, val teams: List<List<Int>>, val replayAvailable: Boolean, val replayAvailabilityReason: ReplayAvailability)
data class ReplayHistoryPage(val items: List<ReplayHistoryItem>, val nextCursor: String?)

/** Bounded metadata reads only; full timeline integrity is checked when opening the manifest. */
@RestController
class ReplayHistoryController(private val history:PlayerMatchHistoryRepository,private val source:ReplaySource) {
    @GetMapping("/api/v1/players/me/history")
    fun history(@AuthenticationPrincipal identity:FirebaseIdentity,@RequestParam(defaultValue="20") limit:Int,
        @RequestParam(required=false) cursor:String?):ResponseEntity<*> {
        if(limit !in 1..20 || cursor!=null&&runCatching{MatchIds.document(cursor)}.isFailure)
            return ResponseEntity.badRequest().body(mapOf("code" to "HISTORY_PAGE_INVALID"))
        return try {
            val page=history.history(identity.uid,limit,cursor)
            val items=page.items.map {h->
                val m=source.match(h.matchId)?.takeIf{it.participants.any{p->p.playerUid==identity.uid}}
                val reason=when {
                    m==null->ReplayAvailability.LEGACY_DATA
                    m.status!=MatchStatus.FINISHED->ReplayAvailability.ACTIVE_MATCH
                    m.modeKey !in setOf("DUEL_1V1","PARTNERS_2V2_ONLINE")||m.executionMode!=MatchExecutionMode.ONLINE->ReplayAvailability.LEGACY_DATA
                    m.lastSequence==0L->ReplayAvailability.INCOMPLETE_EVENTS
                    else->ReplayAvailability.AVAILABLE
                }
                ReplayHistoryItem(h,m?.participants?.map{PublicParticipant(it.seatIndex,it.displayNameSnapshot,it.teamId,it.controlType)}.orEmpty(),
                    m?.participants?.singleOrNull{it.playerUid==identity.uid}?.seatIndex,m?.ruleSnapshot?.mode()?.seatTeams.orEmpty(),reason==ReplayAvailability.AVAILABLE,reason)
            }
            ResponseEntity.ok(ReplayHistoryPage(items,page.nextCursor))
        }catch(_:Exception){ResponseEntity.status(503).body(mapOf("code" to "HISTORY_UNAVAILABLE"))}
    }
}
