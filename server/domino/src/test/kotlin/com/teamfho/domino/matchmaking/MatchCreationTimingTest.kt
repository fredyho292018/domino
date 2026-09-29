package com.teamfho.domino.matchmaking

import kotlin.test.*
import org.junit.jupiter.api.Test
import org.junit.jupiter.api.AfterEach

class MatchCreationTimingTest {
    @AfterEach fun cleanup(){CreationRecorder.current.remove();CreationRecorder.stop()}
    private fun complete(t:CreationTrace) {
        listOf("M3_CREATION_START","M4_PERSISTENCE_START","M6_ASSIGNMENT_BUFFER_START","M7_ASSIGNMENT_BUFFER_COMPLETE","M5_PERSISTENCE_COMPLETE","M8_PUBLICATION_START").forEach(t::mark)
        repeat(4){t.sent(it,1000,1001,2000,1999)}
    }
    @Test fun `four ready players and all deliveries are required`() {
        var n=10L;val t=CreationTrace("hash",listOf(1,2,3,4),4,emptyMap()){n++}
        assertEquals(false,t.snapshot()["complete"]);complete(t);assertEquals(true,t.snapshot()["complete"])
        val missing=CreationTrace("hash",listOf(1,2,3),4,emptyMap()){n++};complete(missing);assertEquals(false,missing.snapshot()["complete"])
    }
    @Test fun `null return runs once and exception identity is preserved`() {
        val t=CreationTrace("hash",emptyList(),4,emptyMap());CreationRecorder.current.set(t)
        var calls=0;assertNull(CreationRecorder.phase<String?>("read"){calls++;null});assertEquals(1,calls)
        val error=IllegalStateException("private payload");assertSame(error,assertFailsWith<IllegalStateException>{CreationRecorder.phase("failed"){throw error}})
        assertFalse(t.snapshot().toString().contains("private payload"))
    }
    @Test fun `disabled trace preserves return and executes once`() {
        var calls=0;assertEquals(7,CreationRecorder.phase("test"){calls++;7});assertEquals(1,calls)
        assertNull(CreationRecorder.begin("secret",listOf("uid")))
    }
    @Test fun `negative durations and duplicate delivery invalidate trace`() {
        var n=10L;val t=CreationTrace("hash",listOf(1,2,3,4),4,emptyMap()){n}
        t.phase("read"){n=9};n=20;complete(t);assertEquals(false,t.snapshot()["complete"])
        val d=CreationTrace("hash",listOf(1,2,3,4),4,emptyMap());complete(d);d.sent(0,1,2,3);assertEquals(false,d.snapshot()["complete"])
    }
    @Test fun `retry durations accumulate and nested writes are not relabelled commit`() {
        var n=10L;val t=CreationTrace("hash",listOf(1,2,3,4),4,emptyMap()){n}
        repeat(2){t.phase("transactionCallback"){t.phase("assignmentRead"){n+=5};t.phase("allBufferedWrites"){n+=2}}}
        val s=t.snapshot();assertEquals(10L,(s["phasesNanos"] as Map<*,*>)["assignmentRead"])
        assertEquals(2,(s["phaseCounts"] as Map<*,*>)["transactionCallback"])
        assertEquals(14L,(s["phasesNanos"] as Map<*,*>)["transactionCallback"])
    }
    @Test fun `bounded recorder emits no raw identifiers`() {
        CreationRecorder.start(10)
        repeat(17){i->val u=(0..3).map{"private-user-$i-$it"};u.forEach(CreationRecorder::joined);CreationRecorder.begin("private-match-$i",u)}
        val s=CreationRecorder.snapshot();assertEquals(16,(s["records"] as List<*>).size);assertEquals(1,s["dropped"])
        assertFalse(s.toString().contains("private-user"));assertFalse(s.toString().contains("private-match"))
    }
    @Test fun `missing marker order cannot pass`() {
        var n=0L;val t=CreationTrace("hash",listOf(1,2,3,4),4,emptyMap()){n++};complete(t);assertEquals(false,t.snapshot()["complete"])
    }
    @Test fun `points and phases remain bounded`() {
        val t=CreationTrace("hash",listOf(1,2,3,4),4,emptyMap())
        repeat(200){t.markFirst("m$it");t.measured("p$it",1)}
        assertTrue((t.snapshot()["pointsNanos"] as Map<*,*>).size<=40)
        assertTrue((t.snapshot()["phasesNanos"] as Map<*,*>).size<=32)
        assertEquals(false,t.snapshot()["complete"])
    }
}
