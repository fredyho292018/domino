package com.teamfho.swarm

/** Bounded 1ms buckets up to 60 seconds; overflow is explicit, never silently clamped. */
class LatencyHistogram {
    private val buckets=LongArray(60001)
    private var count=0L
    private var overflow=0L
    private var max=0L
    @Synchronized fun record(ms:Long) { require(ms>=0);count++;max=maxOf(max,ms);if(ms>60000)overflow++ else buckets[ms.toInt()]++ }
    @Synchronized fun snapshot():Map<String,Any> {
        fun percentile(p:Double):Any {
            if(count==0L)return "NOT_MEASURED"
            val rank=kotlin.math.ceil(count*p).toLong();var n=0L
            for(i in buckets.indices){n+=buckets[i];if(n>=rank)return i}
            return "OVER_60000_MS"
        }
        return mapOf("count" to count,"p50Ms" to percentile(.5),"p95Ms" to percentile(.95),"p99Ms" to percentile(.99),"maxMs" to if(count==0L)"NOT_MEASURED" else max,"overflow" to overflow,
            "bucketsMs" to buckets.indices.filter{buckets[it]>0}.associate{it.toString() to buckets[it]})
    }
}
