package com.teamfho.domino.online

import com.teamfho.domino.realtime.*
import com.teamfho.domino.security.*
import com.teamfho.domino.catalog.GameCatalogCodec
import com.teamfho.domino.match.*
import org.junit.jupiter.api.Test
import org.mockito.Mockito.*
import org.springframework.web.socket.*
import java.time.Instant
import java.util.UUID
import kotlin.test.*

class OnlineTransportTests {
    class Peer(val socket: WebSocketSession,val uid: String) {val output=java.util.concurrent.CopyOnWriteArrayList<String>();var sequence=0L}
    @Test fun `authenticated commands fan out only to participant UID connections with private cursors`() {
        val f=OnlineFixture();val presence=RealtimeHandlerTests.MemoryPresence()
        val handler=RealtimeHandler(FirebaseTokenVerifier {FirebaseIdentity(it,true)},presence,RealtimeProperties(),f.service)
        fun send(p: Peer,type: String,payload: Any) {
            handler.handleMessage(p.socket,TextMessage(GameCatalogCodec.mapper.writeValueAsString(mapOf("type" to type,"version" to 1,"sequence" to ++p.sequence,"timestamp" to Instant.now().toString(),"payload" to payload))))
            assertTrue(handler.awaitOutboundIdle())
            if(type=="MATCH_COMMAND" && p.output.last().contains("COMMAND_ACCEPTED")) {
                val nodes=p.output.map{GameCatalogCodec.mapper.readTree(it)}
                val ack=nodes.last()["payload"]
                val update=nodes.last{it["type"].asString()=="MATCH_UPDATE"}["payload"]
                assertEquals(ack["resultingSequence"].asLong(),update["snapshot"]["publicState"]["lastSequence"].asLong())
            }
        }
        fun peer(uid: String): Peer {
            val p=Peer(mock(WebSocketSession::class.java),uid)
            `when`(p.socket.id).thenReturn(UUID.randomUUID().toString());`when`(p.socket.isOpen).thenReturn(true)
            doAnswer {p.output+=(it.arguments[0] as TextMessage).payload;null}.`when`(p.socket).sendMessage(any())
            handler.afterConnectionEstablished(p.socket);send(p,"AUTH",mapOf("idToken" to uid));return p
        }
        val a=peer("p0");val a2=peer("p0");val b=peer("p1");val other=peer("unrelated")
        f.join();assertTrue(handler.awaitOutboundIdle())
        repeat(30) {
            val s=f.state();if(s.phase==OnlinePhase.PLAYING)return@repeat
            val st=s.starter!!
            val seat=if(st.method==com.teamfho.domino.catalog.StarterMethod.EVEN_ODD_GUESS)st.guessingSeat else (0..1).first {it.toString() !in st.selections}
            val c=if(st.method==com.teamfho.domino.catalog.StarterMethod.EVEN_ODD_GUESS)
                OnlineCommand(1,UUID.randomUUID().toString(),f.id,OnlineCommandType.SUBMIT_EVEN_ODD_GUESS,even=true)
                else OnlineCommand(1,UUID.randomUUID().toString(),f.id,OnlineCommandType.SELECT_STARTER_TILE,candidate=(0..1).first {it !in st.selections.values})
            send(if(seat==0)a else b,"MATCH_COMMAND",c)
        }
        assertEquals(OnlinePhase.PLAYING,f.state().phase)
        for(p in listOf(a,a2,b)) {
            val update=p.output.map {GameCatalogCodec.mapper.readTree(it)}.last {it.path("type").asString()=="MATCH_UPDATE"}.path("payload")
            val snapshot=GameCatalogCodec.mapper.readValue(update.path("snapshot").toString(),OnlineSnapshot::class.java)
            val seat=if(p.uid=="p0")0 else 1
            assertEquals(seat,snapshot.privateState.seat);assertEquals(f.state().hands[seat.toString()],snapshot.privateState.hand)
            val events=(0 until update.path("events").size()).map {GameCatalogCodec.mapper.readValue(update.path("events").get(it).toString(),OnlineEventView::class.java)}
            events.mapNotNull {it.event?.payload as? HandDealt}.forEach {assertEquals(seat,it.seat)}
            assertTrue(events.any {it.type=="SEQUENCE_ADVANCED"&&it.event==null})
            assertTrue(p.output.none {it.contains("SYSTEM_ERROR")})
        }
        assertTrue(other.output.none {it.contains("MATCH_UPDATE")})
        assertEquals(3,presence.onlinePlayers())
        val before=f.state()
        send(other,"MATCH_COMMAND",OnlineCommand(1,"attack",f.id,OnlineCommandType.PASS))
        assertTrue(other.output.last().contains("NOT_PARTICIPANT"));assertEquals(before,f.state())
        handler.afterConnectionClosed(a.socket,CloseStatus.NORMAL);assertTrue(handler.awaitOutboundIdle())
        val reconnect=peer("p0")
        assertEquals(1,GameCatalogCodec.mapper.readTree(reconnect.output.first())["sequence"].asInt())
        val restored=f.service.snapshot("p0",f.id)
        assertEquals(OnlineMatchService.snapshot(before,"p0").copy(serverNow=restored.serverNow),restored)
        f.move();assertTrue(handler.awaitOutboundIdle())
        assertTrue(reconnect.output.any{it.contains("MATCH_UPDATE")})
        val updated=f.service.snapshot("p0",f.id)
        assertEquals(OnlineMatchService.snapshot(f.state(),"p0").copy(serverNow=updated.serverNow),updated)
        handler.shutdown();assertTrue(handler.awaitOutboundIdle())
    }
}
