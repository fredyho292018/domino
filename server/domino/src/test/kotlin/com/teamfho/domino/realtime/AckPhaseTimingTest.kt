package com.teamfho.domino.realtime

import kotlin.test.*
import org.junit.jupiter.api.Test

class AckPhaseTimingTest {
    @Test fun `out of order phases are not accepted`() {
        val rows=mutableListOf<Map<String,Any>>()
        AckTrace(0,{1},rows::add).apply{identify("a","PASS");mark("postCommit");mark("lookup");mark("authorization");mark("firestore");mark("ackEmission");finish()}
        assertEquals(false,rows.single()["complete"])
    }
    @Test fun `assess bounded trace overhead without remote operations`() {
        fun one() {
            val t=AckTrace(System.nanoTime(),sink={})
            t.identify("local-overhead-command","PLAY_TILE");t.mark("lookup");t.mark("authorization");t.mark("firestore")
            t.attempt();t.domain{1};t.mark("postCommit");t.mark("ackEmission");t.enqueued();t.selected();t.finish()
        }
        repeat(2000){one()}
        val begin=System.nanoTime();repeat(10000){one()}
        println("S707_TRACE_MEAN_NANOS="+(System.nanoTime()-begin)/10000)
    }
    @Test fun `phases partition total across retry domain callbacks`() {
        var now=0L
        val rows=mutableListOf<Map<String,Any>>()
        val t=AckTrace(0,{now},rows::add)
        t.identify("command-1","PLAY_TILE")
        now=10;t.mark("lookup");now=30;t.mark("authorization")
        now=35;t.mark("firestore")
        repeat(2){t.attempt();t.domain {now+=5}}
        now=100;t.mark("postCommit");now=120;t.mark("ackEmission");now=150;t.finish();t.finish()
        assertEquals(1,rows.size)
        val r=rows.single();assertEquals(true,r["complete"])
        val phases=r["phasesNanos"] as Map<*,*>
        assertEquals(150L,phases.values.sumOf{it as Long})
        assertEquals(55L,phases["firestore"]);assertEquals(10L,phases["domain"])
        assertEquals(2,r["attemptCount"]);assertEquals(1,r["retryCount"])
        assertEquals(150L,r["serverTotalNanos"])
        assertFalse(r.toString().contains("command-1"))
        assertEquals(64,(r["correlation"] as String).length)
    }
    @Test fun `missing phases remain invalid rather than fabricated zero samples`() {
        val rows=mutableListOf<Map<String,Any>>()
        AckTrace(0,{10},rows::add).apply{identify("a","PASS");finish()}
        assertEquals(false,rows.single()["complete"])
    }
    @Test fun `clock regression never creates negative duration`() {
        val rows=mutableListOf<Map<String,Any>>()
        AckTrace(10,{0},rows::add).apply{identify("a","TOKEN_SECRET");mark("lookup");finish()}
        val r=rows.single();assertEquals(false,r["complete"])
        assertTrue((r["phasesNanos"] as Map<*,*>).values.all{it as Long>=0})
        assertFalse(r.toString().contains("TOKEN_SECRET"))
    }
    @Test fun `hash correlation is stable and contains no raw identifier`() {
        val rows=mutableListOf<Map<String,Any>>()
        repeat(2){AckTrace(0,{1},rows::add).apply{identify("private-value","PASS");finish()}}
        assertEquals(rows[0]["correlation"],rows[1]["correlation"])
        assertFalse(rows.toString().contains("private-value"))
    }
}
