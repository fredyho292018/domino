package com.teamfho.domino.social

import com.teamfho.domino.realtime.*
import org.junit.jupiter.api.Test
import org.mockito.Mockito.*
import org.springframework.web.socket.*
import java.util.concurrent.*
import java.util.concurrent.atomic.AtomicInteger
import tools.jackson.databind.json.JsonMapper
import kotlin.test.*

class EphemeralBarrierTests {
    private class Fixture(val pauseSelected:Boolean=false):AutoCloseable {
        val work=java.util.concurrent.ConcurrentLinkedQueue<Runnable>()
        val index=LocalSocialAuthorizationIndex(Executor{work.add(it)})
        var snapshot=SocialAuthorizationSnapshot(true,0,1)
        var failing=false;var reads=0
        val reader=SocialAuthorizationReader{_,_->reads++;if(failing)error("unavailable");snapshot}
        val handle=index.register("connection","viewer","target",reader)
        val frames=CopyOnWriteArrayList<String>()
        val held=CountDownLatch(1);val release=CountDownLatch(1)
        val selected=CountDownLatch(1);val selectRelease=CountDownLatch(1)
        val active=AtomicInteger();val peak=AtomicInteger();val cleaned=AtomicInteger()
        val socket=mock(WebSocketSession::class.java)
        val writer:ConnectionOutbound
        init {
            index.recovered();runReads()
            `when`(socket.isOpen).thenReturn(true)
            doAnswer {
                peak.accumulateAndGet(active.incrementAndGet(),Math::max)
                try {val frame=(it.arguments[0] as TextMessage).payload
                    if(frame.contains("HOLD")){held.countDown();check(release.await(5,TimeUnit.SECONDS))}
                    frames.add(frame)
                }finally{active.decrementAndGet()};null
            }.`when`(socket).sendMessage(any())
            doAnswer{release.countDown();selectRelease.countDown();null}.`when`(socket).close(any())
            writer=ConnectionOutbound(socket,OutboundLimits(sendMillis=10000),{cleaned.incrementAndGet()},beforeEphemeralCommit={
                if(pauseSelected){selected.countDown();check(selectRelease.await(5,TimeUnit.SECONDS))}
            })
            writer.offerControl("AUTHENTICATED",emptyMap());assertTrue(writer.awaitIdle())
        }
        fun runReads(){while(true)(work.poll()?:break).run()}
        fun block(){writer.offerControl("HOLD",emptyMap());assertTrue(held.await(2,TimeUnit.SECONDS))}
        fun offer(n:Int,cap:EphemeralAuthorization?=handle.capability(),key:String="target")=writer.offerEphemeral(key,"TEST_STATE",mapOf("n" to n),cap)
        fun revoke(r:Long=1){index.invalidate(SocialInvalidation.pair(handle.pair,r))}
        fun resume(){release.countDown();selectRelease.countDown();assertTrue(writer.awaitIdle())}
        fun states()=frames.filter{it.contains("TEST_STATE")}
        fun sequences(){val m=JsonMapper.builder().build();assertEquals((1..frames.size).map{it.toLong()},frames.map{m.readTree(it)["sequence"].asLong()})}
        override fun close(){release.countDown();selectRelease.countDown();writer.close();assertTrue(writer.awaitClosed());handle.close();assertEquals(0,index.size());assertEquals(0,handle.capabilitySubscriptions());assertEquals(1,cleaned.get());assertEquals(1,peak.get())}
    }
    @Test fun `valid admission then queued revocation removes candidate without sequence gap`()=Fixture().use {f->
        f.block();assertEquals(OfferResult.ENQUEUED,f.offer(1));assertEquals(1,f.writer.snapshot().counts[2]);f.revoke()
        assertEquals(0,f.writer.snapshot().counts[2]);f.writer.offerCritical("MATCH_UPDATE",emptyMap());f.writer.offerControl("PONG",emptyMap())
        f.resume();assertTrue(f.states().isEmpty());assertTrue(f.frames.any{it.contains("MATCH_UPDATE")});f.sequences()
    }
    @Test fun `selected before commit candidate is rejected when revocation linearizes first`()=Fixture(true).use {f->
        assertEquals(OfferResult.ENQUEUED,f.offer(1));assertTrue(f.selected.await(2,TimeUnit.SECONDS));assertEquals(0,f.writer.snapshot().counts[2])
        f.revoke();f.resume();assertTrue(f.states().isEmpty());f.writer.offerCritical("MATCH_UPDATE",emptyMap());assertTrue(f.writer.awaitIdle());f.sequences()
    }
    @Test fun `commit before revocation permits only already committed frame without holding authority across IO`()=Fixture().use {f->
        f.writer.offerEphemeral("target","HOLD",mapOf("n" to 1),f.handle.capability());assertTrue(f.held.await(2,TimeUnit.SECONDS))
        val done=CompletableFuture.runAsync{f.revoke()};done.get(1,TimeUnit.SECONDS)
        assertNull(f.handle.capability());assertEquals(OfferResult.UNAUTHENTICATED,f.offer(2));f.resume();assertEquals(1,f.frames.count{it.contains("HOLD")});f.sequences()
    }
    @Test fun `reauthorization and coalescing cannot resurrect old capability`()=Fixture().use {f->
        f.block();val old=f.handle.capability()!!;repeat(3){f.offer(it,old)};assertEquals(2,f.writer.snapshot().coalesced)
        f.revoke();f.snapshot=SocialAuthorizationSnapshot(true,1,1);f.runReads();assertTrue(f.handle.canDeliver())
        f.offer(3,old);assertEquals(0,f.writer.snapshot().counts[2]);f.offer(4);f.resume()
        assertEquals(1,f.states().size);assertTrue(f.states().single().contains("\"n\":4"));f.sequences()
    }
    @Test fun `privacy and failed reauthorization deny old generation`()=Fixture().use {f->
        f.block();f.offer(1);f.failing=true;f.index.invalidate(SocialInvalidation.privacy("target",2));f.runReads();assertNull(f.handle.capability());f.resume();assertTrue(f.states().isEmpty())
    }
    @Test fun `duplicates and old events preserve current generation but do not revive previous`()=Fixture().use {f->
        val old=f.handle.capability()!!;f.revoke(2);f.snapshot=SocialAuthorizationSnapshot(true,2,1);f.runReads()
        f.index.invalidate(SocialInvalidation.pair(f.handle.pair,2));f.index.invalidate(SocialInvalidation.pair(f.handle.pair,1))
        assertFalse(old.tryCommit());assertTrue(f.handle.capability()!!.tryCommit());f.offer(4);assertTrue(f.writer.awaitIdle());assertEquals(1,f.states().size)
    }
    @Test fun `saturation remains isolated and unrelated target survives`()=Fixture().use {f->
        val other=f.index.register("connection","viewer","other",f.reader);f.runReads();f.block()
        repeat(50){f.offer(it,key="k$it")};assertEquals(OfferResult.REJECTED_STALE,f.offer(51,key="overflow"));f.revoke()
        assertEquals(0,f.writer.snapshot().counts[2]);f.writer.offerEphemeral("other","TEST_STATE",mapOf("n" to 99),other.capability())
        f.writer.offerCritical("MATCH_UPDATE",emptyMap());f.writer.offerControl("PONG",emptyMap());f.resume()
        assertEquals(1,f.states().size);assertTrue(f.states().single().contains("99"));other.close()
    }
    @Test fun `multiple viewers and connections revoked by privacy and pair scopes`()=Fixture().use {f->
        val second=f.index.register("second","viewer","target",f.reader);val stranger=f.index.register("third","stranger","target",f.reader);f.runReads()
        val a=f.handle.capability()!!;val b=second.capability()!!;val c=stranger.capability()!!
        f.revoke();assertFalse(a.tryCommit());assertFalse(b.tryCommit());assertTrue(c.tryCommit())
        f.index.invalidate(SocialInvalidation.privacy("target",2));assertFalse(c.tryCommit());second.close();stranger.close()
    }
    @Test fun `bounded 100 selection invalidation close races have no deadlock or stale delivery`() {
        repeat(100){Fixture(true).use {f->
            f.offer(it);assertTrue(f.selected.await(2,TimeUnit.SECONDS))
            val pool=Executors.newFixedThreadPool(2)
            try {val futures=pool.invokeAll(listOf(Callable{f.revoke()},Callable{f.index.closeConnection("connection")}));futures.forEach{it.get(2,TimeUnit.SECONDS)}}finally{pool.shutdownNow()}
            f.resume();assertTrue(f.states().isEmpty());f.sequences()
        }}
    }
    @Test fun `barrier overhead 100 1000 10000 decisions has no durable reader calls`()=Fixture().use {f->
        val cap=f.handle.capability()!!;val reads=f.reads
        for(n in listOf(100,1000,10000)){val start=System.nanoTime();repeat(n){assertTrue(cap.tryCommit())};println("A401_BARRIER decisions=$n avgNs=${(System.nanoTime()-start)/n}")}
        assertEquals(reads,f.reads)
    }
    @Test fun `three rapid generations retain only latest candidate and expose test commit counts`()=Fixture().use {f->
        f.block();val caps=mutableListOf<EphemeralAuthorization>()
        repeat(3){n->
            caps.add(f.handle.capability()!!);f.offer(n,caps.last())
            if(n<2){f.revoke(n+1L);f.snapshot=SocialAuthorizationSnapshot(true,n+1L,1);f.runReads()}
        }
        val decisions=caps.map{it.tryCommit()}
        assertEquals(listOf(false,false,true),decisions)
        f.resume();assertEquals(1,f.states().size);assertTrue(f.states().single().contains("\"n\":2"))
        assertEquals(0,f.handle.capabilitySubscriptions());f.sequences()
        println("A401_TEST_DECISIONS checks=3 rejected=2 commits=1 staleGenerationRejections=2")
    }
}
