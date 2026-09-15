package com.teamfho.domino.matchmaking

import com.teamfho.domino.catalog.GameCatalogService
import com.teamfho.domino.match.MatchRuleSnapshot
import com.teamfho.domino.online.*
import org.slf4j.LoggerFactory

class MatchmakingService(private val store:MatchmakingStore,private val catalog:GameCatalogService,private val online:OnlineMatchService,
    private val metrics:MatchmakingMetrics?=null) {
    private val log=LoggerFactory.getLogger(javaClass)
    var notify:(String,String,QueueStatus)->Unit={_,_,_->}
    private fun current():Pair<MatchmakingKey,MatchRuleSnapshot> {
        val c=catalog.resolve()?:throw MatchmakingFailure("MODE_UNAVAILABLE")
        val key=MatchmakingKey.resolve(c,"DUEL_1V1")
        return key to MatchRuleSnapshot.freeze(c,c.modes.single{it.key==key.modeKey})
    }
    private fun found(s:OnlineSnapshot):QueueStatus {
        val seat=s.privateState.seat
        val opponent=s.publicState.participants.single{it.seat!=seat}
        return QueueStatus(QueueState.MATCHED,MatchFound(s.publicState.matchId,seat,opponent.displayNameSnapshot,s.publicState.modeKey,
            s.ruleSnapshot.ruleSetId,s.ruleSnapshot.ruleSetVersion))
    }
    fun active(uid:String)=online.active(uid)?.let(::found)?:QueueStatus(QueueState.NOT_QUEUED)
    fun status(uid:String):QueueStatus {
        val active=active(uid);if(active.match!=null)return active
        return store.status(uid).let{if(it.state==QueueState.MATCHED)QueueStatus(QueueState.NOT_QUEUED)else it}
    }
    fun join(uid:String,mode:String):QueueStatus {
        if(mode!="DUEL_1V1")throw MatchmakingFailure("MODE_UNAVAILABLE")
        val active=active(uid);if(active.match!=null)return active.copy(reason="ACTIVE_MATCH_EXISTS")
        val (key,_)=current();val result=store.join(uid,key)
        metrics?.joined()
        log.info("MATCHMAKING_JOIN queue={}",key.value)
        return result
    }
    fun leave(uid:String):QueueStatus {
        // Redis decides cancel vs reserve atomically. A RESERVED response is never cancellation success.
        val result=store.leave(uid)
        // Redis may have restarted after a Match committed. A missing queue key is not proof of no assignment.
        val assigned=active(uid);if(assigned.match!=null)return assigned
        metrics?.cancelled();if(result.state==QueueState.NOT_QUEUED)log.info("MATCHMAKING_CANCEL");return result
    }
    private fun emit(uid:String,type:String,status:QueueStatus) {
        try{notify(uid,type,status)}catch(_:Exception){log.warn("MATCHMAKING_DELIVERY_UNAVAILABLE")}
    }
    private fun complete(r:PairReservation,state:OnlineState) {
        if(!store.complete(r))return // Expired owner cannot finalize or count another worker's reservation.
        metrics?.matched(r.waitMillis)
        for(uid in listOf(r.uidA,r.uidB))emit(uid,"MATCH_FOUND",found(OnlineMatchService.snapshot(state,uid)))
        log.info("MATCHMAKING_MATCH_FOUND reservation={} matchId={}",r.id,state.match.matchId)
    }
    private fun process(r:PairReservation) {
        log.info("MATCHMAKING_RESERVATION reservation={} queue={}",r.id,r.key)
        log.info("MATCHMAKING_PAIR reservation={}",r.id)
        try {
            val state=online.createPaired(r.id,r.uidA,r.uidB,r.rules)
            log.info("MATCHMAKING_MATCH_CREATED matchId={}",state.match.matchId)
            complete(r,state)
        } catch(_:Exception) {
            // A timed-out commit may have succeeded. Atomically discover it or fence all late retries.
            try {
                val committed=online.settleFailedCreation(r.id)
                if(committed!=null){complete(r,committed);return}
                store.failed(r)
                metrics?.failed()
                for(uid in listOf(r.uidA,r.uidB))emit(uid,"MATCHMAKING_STATUS",QueueStatus(QueueState.FAILED,reason="MATCH_CREATION_FAILED"))
                log.warn("MATCHMAKING_FAILURE reservation={}",r.id)
            } catch(_:Exception) {log.warn("MATCHMAKING_RECOVERY_PENDING reservation={}",r.id)}
        }
    }
    fun tick() {
        try {
            store.cleanup().forEach{emit(it,"MATCHMAKING_STATUS",QueueStatus(QueueState.NOT_QUEUED,reason="DISCONNECTED"));log.info("MATCHMAKING_CLEANUP")}
            store.recover()?.let(::process)
            val (key,rules)=current()
            repeat(8){val r=store.reserve(key,rules)?:return;process(r)}
        } catch(_:Exception){log.warn("MATCHMAKING_UNAVAILABLE")}
    }
    fun connectionLost(uid:String) {
        // Presence cleanup checks all sockets; losing one device alone does not cancel a user's queue entry.
        try{store.cleanup().forEach{emit(it,"MATCHMAKING_STATUS",QueueStatus(QueueState.NOT_QUEUED,reason="DISCONNECTED"))}}catch(_:Exception){ }
    }
    fun waiting()=try{store.waiting()}catch(_:Exception){0L}
}
