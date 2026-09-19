package com.teamfho.domino.online

import com.teamfho.domino.realtime.PresenceStore
import org.springframework.stereotype.Component
import org.springframework.scheduling.annotation.Scheduled
import org.slf4j.LoggerFactory
import java.time.Clock
import java.util.concurrent.ConcurrentHashMap

/** Durable due index is discovery, not authority. Every terminal action checks the current
 * Firestore revision/deadline in one transaction. Restart and competing workers use the same path. */
@Component
class OnlineTurnWorker(private val repository: OnlineRepository, private val service: OnlineMatchService,
    private val presence: PresenceStore,private val index:TurnDueIndex,private val bridge:TurnIndexBridge) {
    private val dirty=ConcurrentHashMap.newKeySet<String>()
    private val log=LoggerFactory.getLogger(javaClass)
    fun connectionChanged(uid: String){dirty.add(uid)} // Never do Firestore/fan-out under a socket lock.
    @Scheduled(fixedDelayString="\${domino.online.worker-delay-ms:5000}", scheduler="onlineTurnScheduler")
    fun tick() {bridge.tick();processDue(Clock.systemUTC().instant())}
    fun processDue(now: java.time.Instant) {
        try {
            val ids=index.claim(now).toMutableSet()
            dirty.toList().take(100).forEach {uid->
                dirty.remove(uid)
                try {ids+=repository.activeFor(uid)}catch(e:Exception){dirty.add(uid);throw e}
            }
            for(id in ids)try {
                val state=service.state(id)
                if(state.match.status in setOf(com.teamfho.domino.match.MatchStatus.FINISHED,com.teamfho.domino.match.MatchStatus.CANCELLED)){index.forget(id);continue}
                state.match.participants.mapNotNull {it.playerUid}.forEach {uid->
                    service.connection(id,uid){try {presence.connectionCount(uid)?.let {it>0}}catch(_:Exception){null}}
                }
                service.timeout(id)
                repository.refreshDiscovery(id,now)
            } catch(e:OnlineFailure) {
                if(e.code==OnlineError.MATCH_NOT_FOUND)index.forget(id)
                if(e.code !in setOf(OnlineError.STALE_COMMAND,OnlineError.TIMEOUT_NOT_DUE,OnlineError.MATCH_NOT_FOUND))log.warn("ONLINE_WORK_UNAVAILABLE category={}",e.code)
            } catch(_:Exception){log.warn("ONLINE_WORK_UNAVAILABLE category=STORAGE")}
        } catch(_:Exception){log.warn("ONLINE_WORK_UNAVAILABLE category=DISCOVERY")}
    }
}
