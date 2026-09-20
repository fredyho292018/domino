package com.teamfho.domino.match

import com.teamfho.domino.security.FirebaseIdentity
import org.springframework.http.ResponseEntity
import org.springframework.security.core.annotation.AuthenticationPrincipal
import org.springframework.web.bind.annotation.*

data class ReplayHistoryItem(val history: PlayerMatchHistory, val participants: List<PublicParticipant>,
    val selfSeat: Int?, val teams: List<List<Int>>, val replayAvailable: Boolean, val replayAvailabilityReason: ReplayAvailability,
    val premiumLocked:Boolean=false)
data class ReplayHistoryPage(val items: List<ReplayHistoryItem>, val nextCursor: String?,val historyLimited:Boolean=false)

/** Bounded metadata reads only; full timeline integrity is checked when opening the manifest. */
@RestController
class ReplayHistoryController(private val history:PlayerMatchHistoryRepository,private val source:ReplaySource,
    private val access:com.teamfho.domino.entitlement.EntitlementHistory) {
    @GetMapping("/api/v1/players/me/history")
    fun history(@AuthenticationPrincipal identity:FirebaseIdentity,@RequestParam(defaultValue="20") limit:Int,
        @RequestParam(required=false) cursor:String?):ResponseEntity<*> {
        if(limit !in 1..20 || cursor!=null&&runCatching{MatchIds.document(cursor)}.isFailure)
            return ResponseEntity.badRequest().body(mapOf("code" to "HISTORY_PAGE_INVALID"))
        return try {
            val page=access.page(identity.uid,limit,cursor)
            val replays=if(page.items.isEmpty()) emptySet() else access.replayWindow(identity.uid)
            val items=page.items.map {h->
                val m=source.match(h.matchId)?.takeIf{it.participants.any{p->p.playerUid==identity.uid}}
                val reason=when {
                    m==null->ReplayAvailability.LEGACY_DATA
                    m.status!=MatchStatus.FINISHED->ReplayAvailability.ACTIVE_MATCH
                    m.modeKey !in setOf("DUEL_1V1","PARTNERS_2V2_ONLINE")||m.executionMode!=MatchExecutionMode.ONLINE->ReplayAvailability.LEGACY_DATA
                    m.lastSequence==0L->ReplayAvailability.INCOMPLETE_EVENTS
                    else->ReplayAvailability.AVAILABLE
                }
                val locked=reason==ReplayAvailability.AVAILABLE && replays!=null && h.matchId !in replays
                ReplayHistoryItem(h,m?.participants?.map{PublicParticipant(it.seatIndex,it.displayNameSnapshot,it.teamId,it.controlType)}.orEmpty(),
                    m?.participants?.singleOrNull{it.playerUid==identity.uid}?.seatIndex,m?.ruleSnapshot?.mode()?.seatTeams.orEmpty(),reason==ReplayAvailability.AVAILABLE && !locked,reason,locked)
            }
            ResponseEntity.ok(ReplayHistoryPage(items,page.nextCursor,access.historyLimited(identity.uid)))
        }catch(e:com.teamfho.domino.entitlement.EntitlementFailure){throw e}
        catch(_:Exception){ResponseEntity.status(503).body(mapOf("code" to "HISTORY_UNAVAILABLE"))}
    }
}
