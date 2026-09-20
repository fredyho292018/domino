package com.teamfho.domino.realtime

import io.micrometer.core.instrument.MeterRegistry
import org.springframework.stereotype.Component
import java.util.concurrent.atomic.AtomicLong

@Component
class OutboundMetrics(private val registry:MeterRegistry) {
    private val counts=Array(3){AtomicLong()}
    private val bytes=Array(3){AtomicLong()}
    init {OutboundClass.entries.forEach {c->
        registry.gauge("websocket.outbound.queued.messages",listOf(io.micrometer.core.instrument.Tag.of("message_class",c.name)),counts[c.ordinal])
        registry.gauge("websocket.outbound.queued.bytes",listOf(io.micrometer.core.instrument.Tag.of("message_class",c.name)),bytes[c.ordinal])
    }}
    internal fun queue(c:OutboundClass,count:Int,size:Long){counts[c.ordinal].addAndGet(count.toLong());bytes[c.ordinal].addAndGet(size)}
    internal fun event(reason:String){registry.counter("websocket.outbound.events","reason",reason).increment()}
}
