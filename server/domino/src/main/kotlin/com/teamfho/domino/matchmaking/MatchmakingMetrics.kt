package com.teamfho.domino.matchmaking

import io.micrometer.core.instrument.MeterRegistry
import java.util.concurrent.TimeUnit

/** Operational process counters only; never an authority for queue membership or game state. */
class MatchmakingMetrics(store:MatchmakingStore,registry:MeterRegistry) {
    private val joins=registry.counter("domino.matchmaking.join.requests")
    private val cancellations=registry.counter("domino.matchmaking.cancel.requests")
    private val matches=registry.counter("domino.matchmaking.matches")
    private val failures=registry.counter("domino.matchmaking.reservation.failures")
    private val wait=registry.timer("domino.matchmaking.wait")
    init {registry.gauge("domino.matchmaking.queue.size",store){try{it.waiting().toDouble()}catch(_:Exception){0.0}}}
    fun joined()=joins.increment()
    fun cancelled()=cancellations.increment()
    fun failed()=failures.increment()
    fun matched(waitMillis:Long){matches.increment();wait.record(waitMillis,TimeUnit.MILLISECONDS)}
}
