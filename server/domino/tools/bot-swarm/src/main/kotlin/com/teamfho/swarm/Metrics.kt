package com.teamfho.swarm

import java.util.concurrent.ConcurrentHashMap
import java.util.concurrent.atomic.AtomicLong

class Metrics {
    private val ackLatency=LatencyHistogram()
    private val matchmakingLatency=LatencyHistogram()
    private val eventHandlerLatency=LatencyHistogram()
    private val restLatency=LatencyHistogram()
    fun recordRest(ms:Long)=restLatency.record(ms)
    fun recordAck(ms:Long)=ackLatency.record(ms)
    private val timingEnabled=System.getenv("DOMINO_ACK_PHASE_TIMING")=="true"
    private val timings=java.util.ArrayDeque<Map<String,Any>>()
    private var timingDropped=0L
    @Synchronized fun recordAckCorrelation(id:String,type:String,ms:Long) {
        if(!timingEnabled)return
        if(timings.size>=2048){timingDropped++;return}
        val hash=java.security.MessageDigest.getInstance("SHA-256").digest(id.toByteArray()).joinToString(""){"%02x".format(it)}
        val kind=if(type in setOf("PLAY_TILE","PASS","SELECT_STARTER_TILE","SUBMIT_EVEN_ODD_GUESS","NEXT_ROUND"))type else "UNKNOWN"
        timings.addLast(mapOf("correlation" to hash,"commandType" to kind,"clientAckMs" to ms,"completedEpochMillis" to System.currentTimeMillis()))
    }
    @Synchronized private fun timingSnapshot():Map<String,Any> {
        if(!timingEnabled)return emptyMap()
        val result=mapOf("ackPhaseCorrelations" to timings.toList(),"ackPhaseCorrelationDropped" to timingDropped)
        timings.clear();return result
    }
    fun recordEventHandler(ms:Long)=eventHandlerLatency.record(ms)
    private val counts=ConcurrentHashMap<String,AtomicLong>()
    private val completed=ConcurrentHashMap.newKeySet<String>()
    private val cancelled=ConcurrentHashMap.newKeySet<String>()
    fun cancelled(id:String){if(cancelled.add(id)){add("matchesCancelled");active.remove(id)}}
    private val rounds=ConcurrentHashMap.newKeySet<String>()
    private val starts=ConcurrentHashMap.newKeySet<String>()
    private val active=ConcurrentHashMap.newKeySet<String>()
    private val peak=AtomicLong()
    fun completedCount()=completed.size
    private val waits=java.util.concurrent.CopyOnWriteArrayList<Long>()
    fun add(name:String,n:Long=1){counts.computeIfAbsent(name){AtomicLong()}.addAndGet(n)}
    fun started(id:String){if(starts.add(id)){add("matchesStarted");active.add(id);peak.accumulateAndGet(active.size.toLong(),::maxOf)}}
    fun finished(id:String){if(completed.add(id)){add("matchesFinished");active.remove(id)}}
    fun round(id:String,round:Int){if(rounds.add("$id:$round"))add("roundsFinished")}
    fun recordWait(ms:Long){waits.add(ms.coerceAtLeast(0));matchmakingLatency.record(ms.coerceAtLeast(0))}
    fun snapshot():Map<String,Any> = counts.mapValues{it.value.get()}.toSortedMap()+timingSnapshot()+mapOf("commandAckLatency" to ackLatency.snapshot(),"matchmakingLatency" to matchmakingLatency.snapshot(),"wsEventDispatchLatency" to eventHandlerLatency.snapshot(),"restLatency" to restLatency.snapshot(),"maxSimultaneousMatches" to peak.get(),"averageMatchmakingWaitMs" to if(waits.isEmpty())0 else waits.average().toLong(),"maxMatchmakingWaitMs" to (waits.maxOrNull()?:0),"monetizationActivity" to 0)
}
