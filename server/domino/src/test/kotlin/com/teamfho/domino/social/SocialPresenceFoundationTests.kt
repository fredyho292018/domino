package com.teamfho.domino.social

import com.teamfho.domino.realtime.*
import org.junit.jupiter.api.Test
import org.mockito.Mockito.*
import org.springframework.web.socket.*
import java.util.concurrent.CopyOnWriteArrayList
import java.util.concurrent.Executor
import kotlin.test.*

class SocialPresenceFoundationTests {
    @Test fun `queue recovery resnapshots unchanged targets under a fresh stream fence`() {
        val frames=CopyOnWriteArrayList<String>();val gate=java.util.concurrent.CountDownLatch(1);val entered=java.util.concurrent.CountDownLatch(1)
        val socket=mock(WebSocketSession::class.java);`when`(socket.isOpen).thenReturn(true)
        doAnswer{val text=(it.arguments[0] as TextMessage).payload
            if(text.contains("BLOCK_WRITER")){entered.countDown();check(gate.await(5,java.util.concurrent.TimeUnit.SECONDS))}
            frames.add(text);null}.`when`(socket).sendMessage(any())
        val writer=ConnectionOutbound(socket,OutboundLimits(sendMillis=6000))
        val index=LocalSocialAuthorizationIndex(Executor{it.run()});index.recovered()
        val ids=(0..2).map{it.toString().padStart(22,'x')};var state=SocialPresenceState.ONLINE
        val store=object:PresenceStore {
            override fun touch(uid:String,connectionId:String,serverId:String){}
            override fun remove(uid:String,connectionId:String){}
            override fun onlinePlayers()=0L
            override fun states(uids:List<String>)=uids.associateWith{state}
        }
        val sub=SocialPresenceSubscriptions("c","v",writer,index,SocialAuthorizationReader{_,_->SocialAuthorizationSnapshot(true,0,1)}, {it},store)
        try {
            writer.offerControl("AUTHENTICATED",emptyMap());assertTrue(writer.awaitIdle())
            sub.replace(ids,1);sub.refresh();assertTrue(writer.awaitIdle());sub.refresh();assertTrue(writer.awaitIdle());frames.clear()
            writer.offerCritical("BLOCK_WRITER",emptyMap());assertTrue(entered.await(2,java.util.concurrent.TimeUnit.SECONDS))
            val capability=index.forTarget(ids[0]).single().capability()!!
            repeat(50){writer.offerEphemeral("other:$it","TEST_STATE",emptyMap(),capability)}
            state=SocialPresenceState.OFFLINE;sub.stateChanged();sub.refresh();assertTrue(writer.snapshot().stale)
            gate.countDown();assertTrue(writer.awaitIdle());sub.refresh();assertTrue(writer.awaitIdle())
            val snapshots=frames.filter{it.contains("SOCIAL_PRESENCE_SNAPSHOT")}
            assertEquals(3,snapshots.size);assertTrue(snapshots.all{it.contains("OFFLINE")})
            assertFalse(writer.snapshot().stale)
        }finally{gate.countDown();sub.close();writer.close();assertTrue(writer.awaitClosed())}
    }
    @Test fun `durable caught up feed renews permissions without social graph polling`() {
        var now=0L;var reads=0
        val index=LocalSocialAuthorizationIndex(Executor{it.run()},{now})
        val h=index.register("c","v","t"){_,_->reads++;SocialAuthorizationSnapshot(true,0,1)}
        index.recovered();val initial=reads
        repeat(100){now+=5_000_000_000L;index.recovered();assertTrue(h.canDeliver())}
        assertEquals(initial,reads)
        now+=31_000_000_000L;assertFalse(h.canDeliver())
        index.recoveryFailed();assertFalse(h.canDeliver())
        h.close()
    }
    @Test fun `selected presence frame fails final barrier after block and does not revive`() {
        val selected=java.util.concurrent.CountDownLatch(1);val release=java.util.concurrent.CountDownLatch(1)
        val frames=CopyOnWriteArrayList<String>()
        val socket=mock(WebSocketSession::class.java);`when`(socket.isOpen).thenReturn(true)
        doAnswer{frames.add((it.arguments[0] as TextMessage).payload);null}.`when`(socket).sendMessage(any())
        val writer=ConnectionOutbound(socket,beforeEphemeralCommit={selected.countDown();check(release.await(5,java.util.concurrent.TimeUnit.SECONDS))})
        val index=LocalSocialAuthorizationIndex(Executor{it.run()});var revision=0L
        val h=index.register("c","v","t"){_,_->SocialAuthorizationSnapshot(true,revision,1,true)};index.recovered()
        try {
            writer.offerControl("AUTHENTICATED",emptyMap());assertTrue(writer.awaitIdle())
            writer.offerEphemeral("presence:t","SOCIAL_PRESENCE_UPDATED",mapOf("state" to "IN_MATCH"),h.capability())
            assertTrue(selected.await(3,java.util.concurrent.TimeUnit.SECONDS))
            revision=1;index.invalidate(SocialInvalidation.pair(h.pair,revision))
            assertTrue(h.canDeliver()) // Reauthorized, but the selected old generation is dead.
            release.countDown();assertTrue(writer.awaitIdle())
            assertFalse(frames.any{it.contains("IN_MATCH")})
        }finally{release.countDown();h.close();writer.close();assertTrue(writer.awaitClosed())}
    }
    @Test fun `match activity is projected independently and unknown is never offline`() {
        for(state in SocialPresenceState.entries) {
            assertEquals(SocialPresenceState.UNKNOWN,projectPresence(state,false,true))
            assertEquals(state,projectPresence(state,true,true))
        }
        assertEquals(SocialPresenceState.ONLINE,projectPresence(SocialPresenceState.IN_MATCH,true,false))
        assertEquals(SocialPresenceState.UNKNOWN,projectPresence(SocialPresenceState.UNKNOWN,true,false))
    }
    @Test fun `permission projection and capability are from same authorization generation`() {
        val index=LocalSocialAuthorizationIndex(Executor{it.run()})
        var snapshot=SocialAuthorizationSnapshot(true,0,1,true)
        val handle=index.register("c","viewer","target",SocialAuthorizationReader{_,_->snapshot})
        index.recovered()
        val first=handle.delivery()!!
        assertTrue(first.second)
        snapshot=SocialAuthorizationSnapshot(true,0,2,false)
        index.invalidate(SocialInvalidation.privacy("target",2))
        assertFalse(first.first.tryCommit())
        assertFalse(handle.delivery()!!.second)
        handle.close();assertEquals(0,index.size())
    }
    @org.junit.jupiter.params.ParameterizedTest
    @org.junit.jupiter.params.provider.ValueSource(ints=[1,20,50])
    fun `bounded targets use individually revocable frames without internal identities`(count:Int) {
        val frames=CopyOnWriteArrayList<String>()
        val socket=mock(WebSocketSession::class.java)
        `when`(socket.isOpen).thenReturn(true)
        doAnswer{frames.add((it.arguments[0] as TextMessage).payload);null}.`when`(socket).sendMessage(any())
        val writer=ConnectionOutbound(socket)
        val index=LocalSocialAuthorizationIndex(Executor{it.run()})
        index.recovered()
        val ids=(0 until count).map{it.toString().padStart(22,'x')}
        val store=object:PresenceStore {
            override fun touch(uid:String,connectionId:String,serverId:String){}
            override fun remove(uid:String,connectionId:String){}
            override fun onlinePlayers()=0L
            override fun states(uids:List<String>)=uids.associateWith{SocialPresenceState.IN_MATCH}
        }
        val sub=SocialPresenceSubscriptions("c","viewer",writer,index,
            SocialAuthorizationReader{_,_->SocialAuthorizationSnapshot(true,0,1,false)},
            {"internal-"+it},store)
        try {
            writer.offerControl("AUTHENTICATED",emptyMap());assertTrue(writer.awaitIdle())
            sub.replace(ids,1);sub.refresh();assertTrue(writer.awaitIdle())
            assertEquals(count,index.size())
            val states=frames.filter{it.contains("SOCIAL_PRESENCE_SNAPSHOT")}
            assertEquals(count,states.size)
            assertTrue(states.all{it.contains("ONLINE") && !it.contains("internal-") && it.toByteArray().size<2048})
            assertFailsWith<IllegalArgumentException>{sub.replace((0..50).map{it.toString().padStart(22,'x')},2)}
            assertEquals(count,index.size())
        } finally {sub.close();writer.close();assertTrue(writer.awaitClosed());assertEquals(0,index.size())}
    }
}
