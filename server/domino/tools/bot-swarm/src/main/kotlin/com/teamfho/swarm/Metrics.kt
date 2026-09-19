package com.teamfho.swarm

import java.util.concurrent.ConcurrentHashMap
import java.util.concurrent.atomic.AtomicLong

class Metrics {
    private val counts=ConcurrentHashMap<String,AtomicLong>()
    private val completed=ConcurrentHashMap.newKeySet<String>()
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
    fun recordWait(ms:Long){waits.add(ms.coerceAtLeast(0))}
    fun snapshot():Map<String,Any> = counts.mapValues{it.value.get()}.toSortedMap()+mapOf("maxSimultaneousMatches" to peak.get(),"averageMatchmakingWaitMs" to if(waits.isEmpty())0 else waits.average().toLong(),"maxMatchmakingWaitMs" to (waits.maxOrNull()?:0),"monetizationActivity" to 0)
}
