package com.teamfho.domino.realtime

import kotlin.test.*
import org.junit.jupiter.api.Test

class FirestorePhaseTimingTest {
    @Test fun `batch counts two documents but measures its wait only once`() {
        var n=0L;val t=FirestorePhaseTiming{n}
        t.preRead{n+=1};t.transaction{t.attempt{t.read{n+=2};t.read(2){n+=5}}}
        val s=t.snapshot()
        assertEquals(true,s["complete"]);assertEquals(3,s["transactionReadCount"])
        assertEquals(2,s["transactionReadRoundCount"])
        assertEquals(7L,s["transactionReadNanos"]);assertEquals(8L,s["totalNanos"])
    }
    @Test fun `partitions transaction and excludes conversion from read`() {
        var n=0L; val t=FirestorePhaseTiming{n}
        t.preRead {n+=10}
        n+=7 // DTO conversion outside read
        t.transaction {
            n+=3
            t.attempt {
                repeat(3){t.read{n+=5};n+=2}
                t.domain{n+=4};n+=6
            }
            n+=20
        }
        val s=t.snapshot()
        assertEquals(true,s["complete"])
        assertEquals(10L,s["preReadNanos"])
        assertEquals(54L,s["transactionTotalNanos"])
        assertEquals(15L,s["transactionReadNanos"])
        assertEquals(4L,s["domainNanos"])
        assertEquals(12L,s["callbackLocalOtherNanos"])
        assertEquals(20L,s["completionTailNanos"])
        assertEquals(64L,s["totalNanos"])
        assertEquals(3,s["transactionReadCount"])
        assertEquals(0L,s["postCommitFirestoreNanos"])
        assertEquals(0,s["postCommitFirestoreOperationCount"])
    }
    @Test fun `retries retain failed attempt reads and waiting`() {
        var n=0L;val t=FirestorePhaseTiming{n}
        t.preRead{n+=1}
        t.transaction {
            n+=2
            runCatching{t.attempt{t.read{n+=3};throw IllegalStateException("private uid token")}}
            n+=11
            t.attempt{t.read{n+=5};t.domain{n+=7}}
            n+=13
        }
        val s=t.snapshot()
        assertEquals(true,s["complete"]);assertEquals(2,s["attemptCount"]);assertEquals(1,s["retryCount"])
        assertEquals(8L,s["transactionReadNanos"]);assertEquals(11L,s["betweenAttemptsNanos"])
        assertEquals(41L,s["transactionTotalNanos"])
        assertFalse(s.toString().contains("private"));assertFalse(s.toString().contains("token"))
    }
    @Test fun `missing and misordered phases cannot pass`() {
        assertEquals(false,FirestorePhaseTiming().snapshot()["complete"])
        val t=FirestorePhaseTiming();t.attempt{t.read{1}};t.preRead{1};t.transaction{1}
        assertEquals(false,t.snapshot()["complete"])
    }
    @Test fun `regressing clock is invalid and never produces negatives`() {
        var n=100L;val t=FirestorePhaseTiming{n}
        t.preRead{n=90};t.transaction{t.attempt{t.read{n=80}}}
        val s=t.snapshot();assertEquals(false,s["complete"])
        assertTrue(s.values.filterIsInstance<Number>().all{it.toLong()>=0})
    }
    @Test fun `nested read and domain overlap invalidates partition`() {
        var n=0L;val t=FirestorePhaseTiming{n}
        t.preRead{n++};t.transaction{t.attempt{t.read{t.domain{n+=10}}}}
        assertEquals(false,t.snapshot()["complete"])
    }
    @Test fun `bounded instrumentation overhead`() {
        fun one(){val t=FirestorePhaseTiming();t.preRead{1};t.transaction{t.attempt{repeat(3){t.read{1}};t.domain{1}}};t.snapshot()}
        repeat(2000){one()};val start=System.nanoTime();repeat(10000){one()}
        println("S708_FS_TRACE_MEAN_NANOS="+(System.nanoTime()-start)/10000)
    }
}
