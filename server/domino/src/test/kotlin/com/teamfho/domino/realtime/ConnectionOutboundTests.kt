package com.teamfho.domino.realtime

import org.junit.jupiter.api.Test
import org.junit.jupiter.api.AfterEach
import org.mockito.Mockito.*
import org.springframework.web.socket.*
import tools.jackson.databind.json.JsonMapper
import java.util.concurrent.*
import java.util.concurrent.atomic.AtomicInteger
import kotlin.test.*

class ConnectionOutboundTests {
    private val fixtures=mutableListOf<Fixture>()
    private val json=JsonMapper.builder().build()
    inner class Fixture(limits:OutboundLimits) {
        val socket=mock(WebSocketSession::class.java)
        val frames=CopyOnWriteArrayList<String>()
        val calls=AtomicInteger();val active=AtomicInteger();val peak=AtomicInteger()
        val closes=AtomicInteger();val cleanups=AtomicInteger()
        @Volatile var gate:CountDownLatch?=null
        @Volatile var entered=CountDownLatch(1)
        @Volatile var broken=false
        val writer:ConnectionOutbound
        init {
            `when`(socket.isOpen).thenReturn(true)
            doAnswer {
                peak.accumulateAndGet(active.incrementAndGet(),Math::max)
                try {calls.incrementAndGet();entered.countDown();gate?.await(5,TimeUnit.SECONDS)
                    if(broken)throw IllegalStateException("test failure")
                    frames.add((it.arguments[0] as TextMessage).payload)
                } finally {active.decrementAndGet()}
                null
            }.`when`(socket).sendMessage(any())
            doAnswer {closes.incrementAndGet();gate?.countDown();null}.`when`(socket).close(any())
            writer=ConnectionOutbound(socket,limits,{cleanups.incrementAndGet()})
        }
        fun auth(){writer.offerControl("AUTHENTICATED",emptyMap());assertTrue(writer.awaitIdle())}
        fun block(){gate=CountDownLatch(1);entered=CountDownLatch(1);writer.offerControl("PONG",emptyMap());assertTrue(entered.await(2,TimeUnit.SECONDS))}
        fun release(){gate?.countDown();assertTrue(writer.awaitIdle())}
        fun types()=frames.map{json.readTree(it)["type"].asString()}
    }
    private fun fixture(limits:OutboundLimits=OutboundLimits())=Fixture(limits).also{fixtures+=it}
    @AfterEach fun cleanup(){fixtures.forEach{it.writer.close();assertTrue(it.writer.awaitClosed());assertEquals(1,it.cleanups.get())}}

    @Test fun `unauthenticated traffic denied control auth preserves wire and contiguous sequence`() {
        val f=fixture()
        assertEquals(OfferResult.UNAUTHENTICATED,f.writer.offerCritical("MATCH_UPDATE",emptyMap()))
        assertEquals(OfferResult.UNAUTHENTICATED,f.writer.offerEphemeral("a","TEST_STATE",emptyMap()))
        f.auth();f.writer.offerCritical("MATCH_UPDATE",mapOf("sequence" to 123,"text" to "ñá"));assertTrue(f.writer.awaitIdle())
        f.frames.forEachIndexed {i,frame->val n=json.readTree(frame);assertEquals(5,n.size());assertEquals(i+1L,n["sequence"].asLong());assertEquals(1,n["version"].asInt());java.time.Instant.parse(n["timestamp"].asString())}
        assertEquals(123,json.readTree(f.frames.last())["payload"]["sequence"].asInt())
        assertEquals(1,f.peak.get())
    }
    @Test fun `critical fifo and command response causal order plus control fairness`() {
        val f=fixture(OutboundLimits(controlWaitMillis=5000));f.auth();f.block()
        repeat(20){f.writer.offerCritical(if(it%2==0)"MATCH_UPDATE" else "COMMAND_ACCEPTED",mapOf("index" to it))}
        f.writer.offerControl("PONG",mapOf("late" to true));f.release()
        val critical=f.frames.map{json.readTree(it)}.filter{it["payload"].has("index")}
        assertEquals((0..19).toList(),critical.map{it["payload"]["index"].asInt()})
        val late=f.frames.indexOfFirst{it.contains("late")};assertTrue(late<=10,"At most eight Critical before Control")
        assertEquals(1,f.peak.get())
    }
    @Test fun `control wait budget can precede critical burst`() {
        val f=fixture(OutboundLimits(controlWaitMillis=1));f.auth();f.block()
        repeat(8){f.writer.offerCritical("MATCH_UPDATE",mapOf("n" to it))}
        f.writer.offerControl("PONG",mapOf("late" to true));Thread.sleep(10);f.release()
        assertTrue(f.frames[2].contains("late"))
    }
    @Test fun `latest ephemeral only invalidate before selection and reliable cleanup control`() {
        val f=fixture();f.auth();f.block()
        repeat(100){f.writer.offerEphemeral("a","TEST_STATE",mapOf("n" to it))}
        f.writer.offerEphemeral("b","TEST_STATE",mapOf("remove" to true));f.writer.invalidate("b")
        f.writer.offerControl("TEST_CLEANUP",emptyMap());f.writer.offerCritical("MATCH_UPDATE",emptyMap())
        assertEquals(99L,f.writer.snapshot().coalesced)
        f.release()
        assertFalse(f.frames.any{it.contains("remove")});assertTrue(f.frames.last().contains("99"))
        assertEquals(1,f.types().count{it=="TEST_STATE"})
        f.frames.forEachIndexed{i,s->assertEquals(i+1L,json.readTree(s)["sequence"].asLong())}
    }
    @Test fun `invalidation after transmission commit cannot recall in flight frame`() {
        val f=fixture();f.auth();f.gate=CountDownLatch(1);f.entered=CountDownLatch(1)
        f.writer.offerEphemeral("a","TEST_STATE",mapOf("n" to 1));assertTrue(f.entered.await(2,TimeUnit.SECONDS))
        f.writer.invalidate("a");f.release();assertEquals(1,f.types().count{it=="TEST_STATE"})
    }
    @Test fun `social target exhaustion reports stale and explicit recovery without harming critical`() {
        val f=fixture(OutboundLimits(socialTargets=2));f.auth();f.block()
        for(k in listOf("a","b"))assertEquals(OfferResult.ENQUEUED,f.writer.offerEphemeral(k,"TEST_STATE",emptyMap()))
        assertEquals(OfferResult.REJECTED_STALE,f.writer.offerEphemeral("c","TEST_STATE",emptyMap()))
        assertFalse(f.writer.acknowledgeEphemeralRecovery())
        assertEquals(OfferResult.ENQUEUED,f.writer.offerCritical("MATCH_UPDATE",emptyMap()))
        assertEquals(OfferResult.ENQUEUED,f.writer.offerControl("PONG",emptyMap()))
        f.release();assertTrue(f.writer.snapshot().recoveryReady);assertTrue(f.writer.acknowledgeEphemeralRecovery());assertFalse(f.writer.snapshot().stale)
    }
    @Test fun `social UTF8 byte and per frame limits remove rejected replacement`() {
        val f=fixture(OutboundLimits(socialBytes=20,socialFrameBytes=16));f.auth();f.block()
        val exact=mapOf<String,Any>("s" to "é".repeat(4)) // 16 UTF-8 bytes, not 12 chars
        assertEquals(OfferResult.ENQUEUED,f.writer.offerEphemeral("a","TEST_STATE",exact))
        assertEquals(16L,f.writer.snapshot().bytes[2]);assertEquals(OfferResult.REJECTED_STALE,f.writer.offerEphemeral("b","TEST_STATE",mapOf("s" to "x")))
        assertEquals(OfferResult.REJECTED_STALE,f.writer.offerEphemeral("a","TEST_STATE",mapOf("s" to "é".repeat(5))))
        assertEquals(0L,f.writer.snapshot().bytes[2]);f.release();assertFalse(f.types().contains("TEST_STATE"))
    }
    @Test fun `critical count exhaustion closes exactly once rather than silently dropping`() {
        val f=fixture(OutboundLimits(criticalMessages=2));f.auth();f.block()
        repeat(2){assertEquals(OfferResult.ENQUEUED,f.writer.offerCritical("MATCH_UPDATE",mapOf("n" to it)))}
        assertEquals(OfferResult.CLOSED,f.writer.offerCritical("MATCH_UPDATE",emptyMap()))
        assertTrue(f.writer.awaitClosed());assertEquals(1,f.closes.get());assertEquals(listOf(0,0,0),f.writer.snapshot().counts)
    }
    @Test fun `critical byte exhaustion checked independently from count`() {
        val f=fixture(OutboundLimits(criticalBytes=16));f.auth();f.block()
        assertEquals(OfferResult.ENQUEUED,f.writer.offerCritical("MATCH_UPDATE",mapOf("s" to "é".repeat(4))))
        assertEquals(OfferResult.CLOSED,f.writer.offerCritical("MATCH_UPDATE",emptyMap()));assertTrue(f.writer.awaitClosed())
    }
    @Test fun `control count and byte exhaustion explicitly close`() {
        for(limits in listOf(OutboundLimits(controlMessages=1),OutboundLimits(controlBytes=2))) {
            val f=fixture(limits);f.auth();f.block()
            assertEquals(OfferResult.ENQUEUED,f.writer.offerControl("PONG",emptyMap()))
            assertEquals(OfferResult.CLOSED,f.writer.offerControl("PONG",emptyMap()));assertTrue(f.writer.awaitClosed())
        }
    }
    @Test fun `oversized critical frame is rejected by explicit close`() {
        val f=fixture(OutboundLimits(frameBytes=16));f.auth()
        assertEquals(OfferResult.CLOSED,f.writer.offerCritical("MATCH_UPDATE",mapOf("s" to "é".repeat(5))));assertTrue(f.writer.awaitClosed())
    }
    @Test fun `slow writer does not block producers and send deadline cleans once`() {
        val f=fixture(OutboundLimits(sendMillis=100));f.auth();f.block()
        val started=System.nanoTime();f.writer.offerCritical("MATCH_UPDATE",emptyMap())
        assertTrue((System.nanoTime()-started)/1_000_000<100)
        assertTrue(f.writer.awaitClosed());assertEquals(1,f.closes.get());assertEquals(1,f.cleanups.get())
    }
    @Test fun `oldest critical age closes even when send deadline is longer`() {
        val f=fixture(OutboundLimits(sendMillis=2000,criticalAgeMillis=50));f.auth();f.block()
        f.writer.offerCritical("MATCH_UPDATE",emptyMap());assertTrue(f.writer.awaitClosed(1000));assertEquals(1,f.closes.get())
    }
    @Test fun `writer exception and concurrent closes share one lifecycle`() {
        val f=fixture();f.auth();f.broken=true
        f.writer.offerCritical("MATCH_UPDATE",emptyMap())
        val workers=(1..20).map{Thread.ofVirtual().start{f.writer.close()}};workers.forEach{it.join()}
        assertTrue(f.writer.awaitClosed());assertEquals(1,f.closes.get());assertEquals(1,f.cleanups.get())
    }
    @Test fun `protocol error drains accepted terminal critical messages before ending`() {
        val f=fixture();f.auth();f.block()
        f.writer.offerCritical("MATCH_UPDATE",mapOf("phase" to "MATCH_FINISHED"));f.writer.offerCritical("COMMAND_ACCEPTED",emptyMap())
        f.writer.offerControl("SYSTEM_ERROR",mapOf("code" to "PROTOCOL"));f.writer.finish(1008);f.release()
        assertTrue(f.writer.awaitClosed());assertTrue(f.frames.any{it.contains("MATCH_FINISHED")});assertTrue(f.types().contains("SYSTEM_ERROR"));assertEquals(1,f.closes.get())
    }
    @Test fun `auth failure is delivered before close without authorizing critical`() {
        val f=fixture();f.writer.offerControl("AUTH_FAILED",mapOf("code" to "AUTH_TOKEN_INVALID"));f.writer.finish(1008)
        assertTrue(f.writer.awaitClosed());assertEquals(listOf("AUTH_FAILED"),f.types())
    }
    @Test fun `idle writer parks and old connection cannot leak into new generation`() {
        val old=fixture();old.auth();assertTrue(old.writer.awaitIdle());Thread.sleep(20)
        val waits=old.writer.snapshot().waits;Thread.sleep(50);assertEquals(waits,old.writer.snapshot().waits)
        old.writer.close();assertTrue(old.writer.awaitClosed());val fresh=fixture();fresh.auth()
        assertEquals(OfferResult.CLOSED,old.writer.offerCritical("MATCH_UPDATE",mapOf("old" to true)))
        assertEquals(1,json.readTree(fresh.frames.single())["sequence"].asInt());assertFalse(fresh.frames.any{it.contains("old")})
    }
    @Test fun `concurrent mixed admission remains bounded and has one serialized writer`() {
        val f=fixture();f.auth();f.block()
        val failures=CopyOnWriteArrayList<Throwable>()
        val workers=(0..7).map {n->Thread.ofVirtual().start { try {
            repeat(4){i->assertEquals(OfferResult.ENQUEUED,f.writer.offerCritical("MATCH_UPDATE",mapOf("id" to "$n-$i")))}
            f.writer.offerControl("PONG",mapOf("n" to n))
            repeat(100){f.writer.offerEphemeral("$n","TEST_STATE",mapOf("n" to it))}
        } catch(t:Throwable){failures.add(t)} };};workers.forEach{it.join()}
        assertTrue(failures.isEmpty(),failures.toString())
        val s=f.writer.snapshot();assertEquals(listOf(32,8,8),s.counts);assertTrue(s.bytes[0]<=256*1024);assertTrue(s.bytes[1]<=32*1024);assertTrue(s.bytes[2]<=16*1024)
        f.release();assertEquals(1,f.peak.get());assertEquals(32,f.frames.filter{it.contains("MATCH_UPDATE")}.map{json.readTree(it)["payload"]["id"].asString()}.toSet().size)
        assertEquals(listOf(0L,0L,0L),f.writer.snapshot().bytes)
        f.frames.forEachIndexed{i,s->assertEquals(i+1L,json.readTree(s)["sequence"].asLong())}
    }
    @Test fun `cancelled deadlines and queues are released on repeated close`() {
        val before=ConnectionOutbound.retainedDeadlines()
        repeat(30){val f=fixture();f.auth();f.block();f.writer.offerCritical("MATCH_UPDATE",emptyMap());f.writer.close();assertTrue(f.writer.awaitClosed())}
        assertTrue(ConnectionOutbound.retainedDeadlines()<=before)
    }
    @Test fun `local performance samples compare prior synchronous envelope with bounded writer`() {
        val f=fixture();f.auth()
        val payload=mapOf<String,Any>("text" to "representative", "revision" to 42)
        fun measured(action:()->Unit):Long {val start=System.nanoTime();action();return (System.nanoTime()-start)/1000}
        repeat(30){f.writer.offerCritical("MATCH_UPDATE",payload);assertTrue(f.writer.awaitIdle())}
        val baseline=measured {repeat(100){i->f.socket.sendMessage(TextMessage(json.writeValueAsString(mapOf("type" to "MATCH_UPDATE","version" to 1,"sequence" to i+1,"timestamp" to java.time.Instant.now().toString(),"payload" to payload))))}}
        val single=measured {f.writer.offerCritical("MATCH_UPDATE",payload);assertTrue(f.writer.awaitIdle())}
        val critical=measured {repeat(4){repeat(25){f.writer.offerCritical("MATCH_UPDATE",payload)};assertTrue(f.writer.awaitIdle())}}
        val mixed=measured {repeat(10){repeat(8){f.writer.offerCritical("MATCH_UPDATE",payload)};f.writer.offerControl("PONG",emptyMap());assertTrue(f.writer.awaitIdle())}}
        f.block()
        val coalescing=measured {repeat(10){n->repeat(50){f.writer.offerEphemeral("$it","TEST_STATE",mapOf("n" to n))}}}
        assertEquals(50,f.writer.snapshot().counts[2]);assertEquals(450L,f.writer.snapshot().coalesced)
        val drain=measured {f.release()}
        println("LOCAL_PERF_MICROS baseline_sync_100=$baseline single_enqueue_send=$single critical_100=$critical mixed_90=$mixed coalescing_500_to_50=$coalescing drain_50=$drain")
    }
}
