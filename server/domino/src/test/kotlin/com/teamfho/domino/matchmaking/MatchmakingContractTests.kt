package com.teamfho.domino.matchmaking

import com.teamfho.domino.catalog.*
import com.teamfho.domino.online.*
import com.teamfho.domino.realtime.*
import com.teamfho.domino.security.*
import org.junit.jupiter.api.Test
import org.mockito.Mockito.*
import org.springframework.web.socket.*
import java.time.Instant
import java.util.UUID
import kotlin.test.*

class MatchmakingContractTests {
    private val catalog=GameCatalogService(GameCatalogRepository{GameCatalogV3Publisher.canonical()})
    @Test fun `catalog derives compatibility and unavailable modes cannot join`() {
        val c=catalog.resolve()!!
        assertEquals("DUEL_1V1:double-nine-duel:1:ONLINE",MatchmakingKey.resolve(c,"DUEL_1V1").value)
        assertFailsWith<MatchmakingFailure>{MatchmakingKey.resolve(c,"PARTNERS_2V2")}
        assertFailsWith<MatchmakingFailure>{MatchmakingKey.resolve(c.copy(modes=c.modes.map{it.copy(active=false)}),"DUEL_1V1")}
        assertFailsWith<MatchmakingFailure>{MatchmakingKey.resolve(GameCatalogValidator.resolve(GameCatalogV2Publisher.canonical()),"DUEL_1V1")}
    }
    @Test fun `controller uses authenticated principal rejects extra authority fields`() {
        val store=mock(MatchmakingStore::class.java)
        val key=MatchmakingKey.resolve(catalog.resolve()!!,"DUEL_1V1")
        `when`(store.join("verified-user",key)).thenReturn(QueueStatus(QueueState.QUEUED))
        `when`(store.leave("verified-user")).thenReturn(QueueStatus(QueueState.NOT_QUEUED))
        val controller=MatchmakingController(MatchmakingService(store,catalog,OnlineMatchService(catalog,MemoryOnlineRepository())))
        val principal=FirebaseIdentity("verified-user",true)
        for(field in listOf("uid","seat","opponentUid","ruleSetId","ruleSetVersion","targetScore","reservationId"))
            assertFailsWith<MatchmakingFailure>{controller.join(principal,mapOf("modeKey" to "DUEL_1V1",field to "untrusted"))}
        assertEquals(QueueState.QUEUED,controller.join(principal,mapOf("modeKey" to "DUEL_1V1")).state)
        assertEquals(QueueState.NOT_QUEUED,controller.leave(principal).state)
        verify(store).join("verified-user",key);verify(store).leave("verified-user")
        assertNull(controller.active(FirebaseIdentity("another-user",true)).match)
    }
    @Test fun `MATCH_FOUND goes only to all authenticated connections for assigned UID`() {
        val store=mock(MatchmakingStore::class.java)
        val service=MatchmakingService(store,catalog,OnlineMatchService(catalog,MemoryOnlineRepository()))
        val presence=RealtimeHandlerTests.MemoryPresence()
        val handler=RealtimeHandler(FirebaseTokenVerifier{FirebaseIdentity(it,true)},presence,RealtimeProperties(),matchmaking=service)
        fun peer(uid:String):MutableList<String> {
            val socket=mock(WebSocketSession::class.java);val out=java.util.concurrent.CopyOnWriteArrayList<String>()
            `when`(socket.id).thenReturn(UUID.randomUUID().toString());`when`(socket.isOpen).thenReturn(true)
            doAnswer{out.add((it.arguments[0] as TextMessage).payload);null}.`when`(socket).sendMessage(any())
            handler.afterConnectionEstablished(socket)
            handler.handleMessage(socket,TextMessage(GameCatalogCodec.mapper.writeValueAsString(mapOf("type" to "AUTH","version" to 1,"sequence" to 1,"timestamp" to Instant.now().toString(),"payload" to mapOf("idToken" to uid)))))
            assertTrue(handler.awaitOutboundIdle());out.clear();return out
        }
        val first=peer("a");val second=peer("a");val other=peer("b")
        service.notify("a","MATCH_FOUND",QueueStatus(QueueState.MATCHED,MatchFound(UUID.randomUUID().toString(),0,"Opponent","DUEL_1V1","double-nine-duel",1)))
        assertTrue(handler.awaitOutboundIdle())
        assertEquals(1,first.size);assertEquals(1,second.size);assertTrue(other.isEmpty())
        val payload=GameCatalogCodec.mapper.readTree(first.single())["payload"]
        assertEquals("MATCHED",payload["state"].asText())
        assertFalse(first.single().contains("uid"));assertFalse(first.single().contains("token"));assertFalse(first.single().contains("hand"))
        handler.shutdown();assertTrue(handler.awaitOutboundIdle())
    }
}
