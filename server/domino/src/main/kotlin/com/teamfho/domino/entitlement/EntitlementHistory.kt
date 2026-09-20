package com.teamfho.domino.entitlement

import com.teamfho.domino.match.*
import org.springframework.stereotype.Service

@Service
class EntitlementHistory(private val entitlements:EntitlementService,private val history:PlayerMatchHistoryRepository,
    private val source:ReplaySource) {
    fun historyLimited(uid:String)=!entitlements.limit(uid,EntitlementLimit.HISTORY_MAX).unlimited
    fun page(uid:String,limit:Int,cursor:String?):HistoryPage {
        val cap=entitlements.limit(uid,EntitlementLimit.HISTORY_MAX)
        if(cap.unlimited)return history.history(uid,limit,cursor)
        val window=history.history(uid,cap.maximum!!,null).items
        val start=if(cursor==null)0 else window.indexOfFirst{it.matchId==cursor}.also {
            if(it<0) throw EntitlementFailure("FEATURE_NOT_ENTITLED",EntitlementFeature.FULL_HISTORY,Plan.FREE)
        }+1
        val rows=window.drop(start).take(limit)
        return HistoryPage(rows,rows.lastOrNull()?.matchId?.takeIf {start+rows.size<window.size})
    }
    fun allowed(uid:String,matchId:String):Boolean = replayWindow(uid)?.contains(matchId) ?: true
    fun replayWindow(uid:String):Set<String>? {
        val cap=entitlements.limit(uid,EntitlementLimit.REPLAY_MAX)
        if(cap.unlimited)return null
        // Metadata only. No event archive is fetched to determine rank. Bounded work, never full-history loading.
        var cursor:String?=null;var scanned=0;val eligible=linkedSetOf<String>()
        do {
            val page=history.history(uid,20,cursor)
            for(h in page.items) {
                scanned++
                val m=source.match(h.matchId)
                if(m!=null && m.status==MatchStatus.FINISHED && m.executionMode==MatchExecutionMode.ONLINE &&
                    m.modeKey in setOf("DUEL_1V1","PARTNERS_2V2_ONLINE") && m.lastSequence>0 && m.participants.any{it.playerUid==uid}) {
                    eligible.add(h.matchId)
                    if(eligible.size>=cap.maximum!!)return eligible
                }
            }
            cursor=page.nextCursor
            if(cursor!=null && scanned>=200)throw EntitlementFailure("ENTITLEMENTS_UNAVAILABLE")
        }while(cursor!=null)
        return eligible
    }
    fun requireReplay(uid:String,id:String) {
        if(!allowed(uid,id))throw EntitlementFailure("FEATURE_NOT_ENTITLED",EntitlementFeature.FULL_REPLAY,Plan.FREE)
    }
}
