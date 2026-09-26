package com.teamfho.swarm

import kotlin.test.*

class LatencyHistogramTest {
    @Test fun boundedPercentilesAndOverflow() {
        val h=LatencyHistogram()
        assertEquals("NOT_MEASURED",h.snapshot()["p50Ms"])
        (1..100).forEach { h.record(it.toLong()) }
        assertEquals(50,h.snapshot()["p50Ms"])
        assertEquals(95,h.snapshot()["p95Ms"])
        assertEquals(99,h.snapshot()["p99Ms"])
        repeat(100){h.record(60001)}
        assertEquals("OVER_60000_MS",h.snapshot()["p95Ms"])
        assertEquals(100L,h.snapshot()["overflow"])
        assertFailsWith<IllegalArgumentException>{h.record(-1)}
    }
}
