package com.teamfho.domino.social

import com.teamfho.domino.realtime.*
import org.junit.jupiter.api.Test
import org.mockito.Mockito.*
import org.springframework.web.socket.*
import java.util.concurrent.*
import kotlin.test.*

/** Synthetic payload only: no Social Presence protocol. Tests the A.1/A.3 delivery boundary. */
class SocialOutboundAuthorizationTests {
    @Test fun `queued ephemeral is suppressed after authorization revocation`() {
        val tasks=ArrayDeque<Runnable>()
        val index=LocalSocialAuthorizationIndex(java.util.concurrent.Executor{tasks.add(it)})
        val h=index.register("connection","viewer","target"){_,_->SocialAuthorizationSnapshot(true,0,1)}
        index.recovered();while(tasks.isNotEmpty())tasks.removeFirst().run()
        assertTrue(h.canDeliver())
        val socket=mock(WebSocketSession::class.java)
        val gate=CountDownLatch(1);val entered=CountDownLatch(1);val frames=CopyOnWriteArrayList<String>()
        `when`(socket.isOpen).thenReturn(true)
        doAnswer {val frame=(it.arguments[0] as TextMessage).payload
            if(frame.contains("HOLD")){entered.countDown();gate.await(3,TimeUnit.SECONDS)}
            frames.add(frame);null
        }.`when`(socket).sendMessage(any())
        doAnswer {gate.countDown();null}.`when`(socket).close(any())
        val writer=ConnectionOutbound(socket,OutboundLimits(sendMillis=5000))
        try {
            writer.offerControl("AUTHENTICATED",emptyMap());assertTrue(writer.awaitIdle())
            writer.offerControl("HOLD",emptyMap());assertTrue(entered.await(2,TimeUnit.SECONDS))
            // The current API only authorizes admission, not subsequent transmission.
            if(h.canDeliver())writer.offerEphemeral("target","TEST_STATE",mapOf("private" to true))
            index.invalidate(SocialInvalidation.pair(h.pair,1));assertFalse(h.canDeliver())
            gate.countDown();assertTrue(writer.awaitIdle())
            assertFalse(frames.any{it.contains("TEST_STATE")},"Queued data transmitted after A.3 authorization was invalidated")
        } finally {gate.countDown();writer.close();assertTrue(writer.awaitClosed());h.close()}
    }
}
