package com.teamfho.domino.social

import org.junit.jupiter.api.Test
import java.util.concurrent.*
import kotlin.test.*

class SocialInvalidationLifecycleTests {
    private fun runtime()=SocialInvalidationRuntime({null},{null},{null})
    private fun field(r:SocialInvalidationRuntime,name:String):Any? =
        r.javaClass.getDeclaredField(name).also{it.isAccessible=true}.get(r)

    @Test fun `start stop restart owns fresh resources and keeps index`() {
        val r=runtime();val index=r.index
        try {
            var previous:Any?=null
            repeat(4) {
                r.start()
                val scheduler=field(r,"scheduler") as ScheduledExecutorService
                val worker=field(r,"worker") as ExecutorService
                val future=field(r,"scheduled") as ScheduledFuture<*>
                assertNotSame(previous,scheduler);assertSame(index,r.index)
                r.start();assertSame(scheduler,field(r,"scheduler"));assertSame(future,field(r,"scheduled"))
                val done=CountDownLatch(1);worker.execute{done.countDown()};assertTrue(done.await(2,TimeUnit.SECONDS))
                r.stop();r.stop()
                assertFalse(r.isRunning);assertTrue(scheduler.isTerminated);assertTrue(worker.isTerminated)
                assertTrue(future.isCancelled);assertNull(field(r,"scheduled"))
                previous=scheduler
            }
        } finally {r.close()}
    }
    @Test fun `concurrent starts and stops are serialized`() {
        val r=runtime();val callers=Executors.newFixedThreadPool(4)
        try {
            val starts=(1..12).map{callers.submit{r.start()}}
            starts.forEach{it.get(5,TimeUnit.SECONDS)}
            val scheduler=field(r,"scheduler") as ScheduledExecutorService
            val worker=field(r,"worker") as ExecutorService
            val calls=(1..12).map { n->callers.submit{if(n%2==0)r.stop() else r.start()} }
            calls.forEach{it.get(10,TimeUnit.SECONDS)}
            r.stop();assertTrue(scheduler.isTerminated);assertTrue(worker.isTerminated)
            assertTrue((field(r,"worker") as ExecutorService).isTerminated)
            assertNull(field(r,"scheduler"))
        } finally {r.close();callers.shutdownNow()}
    }
    @Test fun `stop waits for cancelled callback before a new generation`() {
        val entered=CountDownLatch(1);val interrupted=CountDownLatch(1)
        val r=SocialInvalidationRuntime({entered.countDown();try{CountDownLatch(1).await()}catch(_:InterruptedException){interrupted.countDown()};null},{null},{null})
        try {
            r.start();assertTrue(entered.await(2,TimeUnit.SECONDS))
            val old=field(r,"scheduler") as ScheduledExecutorService
            r.stop();assertTrue(interrupted.await(1,TimeUnit.SECONDS));assertTrue(old.isTerminated)
            r.start();assertNotSame(old,field(r,"scheduler"));r.stop()
        } finally {r.close()}
    }
    @Test fun `stop before first start is safe`() {
        val r=runtime()
        try {r.stop();r.stop();r.start();assertTrue(r.isRunning)} finally {r.close()}
        assertTrue((field(r,"worker") as ExecutorService).isTerminated)
    }
}
