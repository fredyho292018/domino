package com.teamfho.domino.realtime

import kotlin.test.*
import org.junit.jupiter.api.Test

class FirestoreReadRoundTimingTest {
    @Test fun `miss partitions named reads start wait domain and finalization`() {
        var n=0L;val t=FirestorePhaseTiming{n}
        t.preRead{n+=10};t.transaction{n+=3;t.attempt{t.receiptRead<String>{n+=5;null};t.groupedRead{n+=7};t.domain{n+=2};n+=1};n+=11}
        val s=t.snapshot()
        assertEquals(true,s["readBreakdownComplete"])
        assertEquals(5L,s["receiptReadNanos"]);assertEquals(7L,s["getAllReadNanos"]);assertEquals(12L,s["transactionReadNanos"])
        assertEquals(3L,s["acquisitionToFirstCallbackNanos"]);assertEquals(11L,s["completionTailNanos"])
        assertEquals(1,s["receiptMissCount"]);assertEquals(0,s["receiptHitCount"]);assertEquals(1,s["getAllCount"])
        assertEquals(3,s["transactionReadCount"]);assertEquals(2,s["transactionReadRoundCount"])
    }
    @Test fun `hit has no grouped read or secret payload`() {
        val t=FirestorePhaseTiming();t.preRead{1};t.transaction{t.attempt{t.receiptRead{"uid token private data"}}}
        val s=t.snapshot();assertEquals(true,s["readBreakdownComplete"]);assertEquals(1,s["receiptHitCount"])
        assertEquals(0,s["getAllCount"]);assertEquals(0L,s["getAllReadNanos"])
        assertTrue(s.values.all{it is Number || it is Boolean});assertFalse(s.toString().contains("private data"))
    }
    @Test fun `missing or misordered named reads fail closed`() {
        for(mode in 0..3){val t=FirestorePhaseTiming();t.preRead{1};t.transaction{t.attempt{
            when(mode){0->t.receiptRead<String>{null};1->t.groupedRead{1};2->{t.receiptRead{"hit"};t.groupedRead{1}};else->{t.receiptRead<String>{null};t.domain{1};t.groupedRead{1}}}
        }};assertEquals(false,t.snapshot()["readBreakdownComplete"])}
    }
    @Test fun `missing callback and clock regression cannot masquerade as timings`() {
        val empty=FirestorePhaseTiming();empty.preRead{1};empty.transaction{1};assertEquals(false,empty.snapshot()["readBreakdownComplete"])
        var n=10L;val t=FirestorePhaseTiming{n};t.preRead{1};t.transaction{t.attempt{t.receiptRead<String>{n=0;null};t.groupedRead{1}}}
        assertEquals(false,t.snapshot()["complete"]);assertTrue(t.snapshot().values.filterIsInstance<Number>().all{it.toLong()>=0})
    }
    @Test fun `failed read preserves exception and invalidates classification`() {
        val expected=IllegalStateException("secret");val t=FirestorePhaseTiming();t.preRead{1}
        val actual=assertFailsWith<IllegalStateException>{t.transaction{t.attempt{t.receiptRead<String>{null};t.groupedRead{throw expected}}}}
        assertSame(expected,actual);assertEquals(false,t.snapshot()["readBreakdownComplete"]);assertFalse(t.snapshot().toString().contains("secret"))
    }
    @Test fun `retry that ends in receipt hit remains separately classifiable`() {
        val t=FirestorePhaseTiming();t.preRead{1};t.transaction{
            t.attempt{t.receiptRead<String>{null};t.groupedRead{1};t.domain{1}}
            t.attempt{t.receiptRead{"receipt"}}
        };val s=t.snapshot();assertEquals(true,s["readBreakdownComplete"])
        assertEquals(1,s["receiptHitCount"]);assertEquals(1,s["receiptMissCount"]);assertEquals(1,s["retryCount"])
    }
    @Test fun `recorder bounds rows and rejects prior generation`() {
        Recorder.stop();Recorder.drain();Recorder.start(1)
        try{val g=Recorder.generation();repeat(20001){Recorder.record(g,mapOf("safe" to it))};Recorder.record(g-1,mapOf("unsafe" to "secret"))
            val result=Recorder.drain();assertTrue(result.contains("\"dropped\":1"));assertFalse(result.contains("secret"))
        }finally{Recorder.stop();Recorder.drain()}
    }
    @Test fun `named trace overhead is bounded without IO`() {
        fun one(){val t=FirestorePhaseTiming();t.preRead{1};t.transaction{t.attempt{t.receiptRead<String>{null};t.groupedRead{1};t.domain{1}}};t.snapshot()}
        repeat(2000){one()};val start=System.nanoTime();repeat(10000){one()};val mean=(System.nanoTime()-start)/10000
        println("S715_NAMED_TRACE_MEAN_NANOS=$mean");assertTrue(mean<1_000_000,"Local instrumentation mean must be below 1ms")
    }
}
