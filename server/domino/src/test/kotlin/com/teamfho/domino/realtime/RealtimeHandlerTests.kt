package com.teamfho.domino.realtime

import com.teamfho.domino.common.ApiErrorCode
import com.teamfho.domino.security.*
import org.junit.jupiter.api.Test
import org.junit.jupiter.params.ParameterizedTest
import org.junit.jupiter.params.provider.ValueSource
import org.mockito.Mockito.*
import org.springframework.web.socket.*
import tools.jackson.databind.json.JsonMapper
import java.time.Instant
import java.util.UUID
import kotlin.test.*

class RealtimeHandlerTests {
    class MemoryPresence : PresenceStore {
        val leases = java.util.concurrent.ConcurrentHashMap<String,String>()
        var writes = 0
        var unavailable = false
        override fun touch(uid: String, connectionId: String, serverId: String) { checkAvailable(); leases[connectionId] = uid; writes++ }
        override fun remove(uid: String, connectionId: String) { checkAvailable(); leases.remove(connectionId) }
        override fun onlinePlayers(): Long { checkAvailable(); return leases.values.toSet().size.toLong() }
        fun checkAvailable() { if (unavailable) throw org.springframework.dao.DataAccessResourceFailureException("unavailable") }
    }
    val store = MemoryPresence()
    val handler = RealtimeHandler(FirebaseTokenVerifier { token ->
        when (token) {
            "invalid" -> throw AuthFailure(ApiErrorCode.AUTH_TOKEN_INVALID)
            "expired" -> throw AuthFailure(ApiErrorCode.AUTH_TOKEN_EXPIRED)
            "revoked", "disabled", "wrong-project" -> throw AuthFailure(ApiErrorCode.AUTH_SESSION_INVALID)
            else -> FirebaseIdentity("verified-" + token, true)
        }
    }, store, RealtimeProperties())
    @org.junit.jupiter.api.AfterEach fun shutdown() { handler.shutdown(); assertTrue(handler.awaitOutboundIdle()) }
    val json = JsonMapper.builder().build()
    class Peer(val socket: WebSocketSession, val messages: MutableList<String>) { var sequence = 0L }
    fun peer(): Peer {
        val socket = mock(WebSocketSession::class.java)
        val out = java.util.concurrent.CopyOnWriteArrayList<String>()
        `when`(socket.id).thenReturn(UUID.randomUUID().toString())
        `when`(socket.isOpen).thenReturn(true)
        doAnswer { out.add((it.arguments[0] as TextMessage).payload); null }.`when`(socket).sendMessage(any())
        doAnswer { `when`(socket.isOpen).thenReturn(false); null }.`when`(socket).close(any())
        handler.afterConnectionEstablished(socket)
        return Peer(socket, out)
    }
    fun send(peer: Peer, type: String, payload: Map<String, Any> = emptyMap()) {
        handler.handleMessage(peer.socket, TextMessage(json.writeValueAsString(mapOf("type" to type, "version" to 1,
            "sequence" to ++peer.sequence, "timestamp" to Instant.now().toString(), "payload" to payload))))
        assertTrue(handler.awaitOutboundIdle())
    }
    @Test fun `verified identity only and aggregate contains no private information`() {
        val p = peer(); send(p, "AUTH", mapOf("idToken" to "player-secret")); send(p, "GLOBAL_ACTIVITY_SUBSCRIBE")
        assertEquals(setOf("verified-player-secret"), store.leases.values.toSet())
        assertTrue(p.messages.first().contains("AUTHENTICATED"))
        val snapshot = json.readTree(p.messages.last()).path("payload")
        assertEquals(1, snapshot.path("onlinePlayers").asInt())
        for (key in listOf("activeMatches", "waitingPlayers", "openRooms")) assertEquals(0, snapshot.path(key).asInt())
        assertEquals(5, snapshot.size())
        assertFalse(p.messages.any { it.contains("player-secret") || it.contains("uid") || it.contains("connectionId") })
    }
    @ParameterizedTest @ValueSource(strings = ["invalid", "expired", "revoked", "disabled", "wrong-project"])
    fun `auth errors close without presence`(token: String) {
        val p = peer(); send(p, "AUTH", mapOf("idToken" to token))
        assertFalse(p.socket.isOpen); assertEquals(0, store.onlinePlayers())
        assertTrue(p.messages.last().contains("AUTH_FAILED"))
    }
    @Test fun `client uid cannot grant authority`() {
        val p = peer(); send(p, "AUTH", mapOf("idToken" to "player", "uid" to "attacker"))
        assertFalse(p.socket.isOpen); assertEquals(0, store.onlinePlayers())
    }
    @Test fun `multiple devices are counted once and last close removes presence`() {
        val a = peer(); val b = peer(); val c = peer()
        send(a,"AUTH",mapOf("idToken" to "one")); send(b,"AUTH",mapOf("idToken" to "one"))
        assertEquals(1, store.onlinePlayers())
        send(c,"AUTH",mapOf("idToken" to "two")); assertEquals(2, store.onlinePlayers())
        handler.afterConnectionClosed(a.socket, CloseStatus.NORMAL); assertTrue(handler.awaitOutboundIdle()); assertEquals(2, store.onlinePlayers())
        handler.afterConnectionClosed(b.socket, CloseStatus.NORMAL); assertTrue(handler.awaitOutboundIdle()); assertEquals(1, store.onlinePlayers())
        handler.afterConnectionClosed(c.socket, CloseStatus.NORMAL); assertTrue(handler.awaitOutboundIdle()); assertEquals(0, store.onlinePlayers())
    }
    @ParameterizedTest @ValueSource(ints = [10,50,100])
    fun `logical connection simulation`(size: Int) {
        val peers = (0 until size).map { peer().also { p -> send(p,"AUTH",mapOf("idToken" to "user-${it / 2}")) } }
        assertEquals((size / 2).toLong(), store.onlinePlayers())
        peers.forEach { handler.afterConnectionClosed(it.socket, CloseStatus.NORMAL); assertTrue(handler.awaitOutboundIdle()) }
        assertEquals(0, store.onlinePlayers())
    }
    @Test fun `subscribe sends snapshot updates coalesce unsubscribe stops pushes`() {
        val p = peer(); send(p,"AUTH",mapOf("idToken" to "one")); send(p,"GLOBAL_ACTIVITY_SUBSCRIBE")
        handler.publishActivity(); assertTrue(handler.awaitOutboundIdle()); val count = p.messages.size; handler.publishActivity(); assertTrue(handler.awaitOutboundIdle()); assertEquals(count,p.messages.size)
        val second = peer(); send(second,"AUTH",mapOf("idToken" to "two")); handler.publishActivity(); assertTrue(handler.awaitOutboundIdle())
        assertTrue(p.messages.last().contains("GLOBAL_ACTIVITY_UPDATED"))
        send(p,"GLOBAL_ACTIVITY_UNSUBSCRIBE"); val after = p.messages.size
        handler.afterConnectionClosed(second.socket,CloseStatus.NORMAL); assertTrue(handler.awaitOutboundIdle()); handler.publishActivity(); assertTrue(handler.awaitOutboundIdle()); assertEquals(after,p.messages.size)
    }
    @Test fun `redis failure closes realtime and subsequent connections recover`() {
        store.unavailable = true
        val p = peer(); send(p,"AUTH",mapOf("idToken" to "one")); assertFalse(p.socket.isOpen)
        assertTrue(p.messages.last().contains("UNAVAILABLE"))
        store.unavailable = false
        val next = peer(); send(next,"AUTH",mapOf("idToken" to "one")); assertTrue(next.socket.isOpen)
    }
    @Test fun `flood closes and heartbeat does not rewrite every ping`() {
        val p = peer(); send(p,"AUTH",mapOf("idToken" to "one"))
        repeat(4) { send(p,"PING") }; assertEquals(1,store.writes)
        repeat(10) { send(p,"PING") }; assertFalse(p.socket.isOpen)
    }
    @Test fun `unknown invalid binary oversized and unauthenticated commands rejected`() {
        for (text in listOf("{broken", " ".repeat(32769))) {
            val p = peer(); handler.handleMessage(p.socket,TextMessage(text)); assertTrue(handler.awaitOutboundIdle()); assertFalse(p.socket.isOpen)
        }
        for(type in listOf("MATCH_MOVE","PING","GLOBAL_ACTIVITY_SUBSCRIBE")) {
            val p = peer(); send(p,type); assertFalse(p.socket.isOpen)
        }
    }
    private fun age(p: Peer, field: String, seconds: Long) {
        val registry = RealtimeHandler::class.java.getDeclaredField("connections").apply { isAccessible = true }.get(handler) as Map<*, *>
        val c = registry[p.socket.id]!!
        c.javaClass.getDeclaredField(field).apply { isAccessible = true }.setLong(c,System.nanoTime() - seconds * 1_000_000_000)
    }
    @Test fun `auth timeout closes unauthenticated socket`() {
        val p = peer(); age(p,"opened",6); handler.expireConnections(); assertTrue(handler.awaitOutboundIdle()); assertFalse(p.socket.isOpen)
    }
    @Test fun `heartbeat deadline expires authenticated connection and lease refresh is throttled`() {
        val p = peer(); send(p,"AUTH",mapOf("idToken" to "one"))
        age(p,"lastLease",21); send(p,"PING"); assertEquals(2,store.writes)
        age(p,"lastHeartbeat",46); handler.expireConnections(); assertTrue(handler.awaitOutboundIdle())
        assertFalse(p.socket.isOpen); assertEquals(0,store.onlinePlayers())
    }
    @Test fun `blocked writer does not hold producer monitor or remove second UID lease`() {
        val a=peer();val b=peer();send(a,"AUTH",mapOf("idToken" to "same"));send(b,"AUTH",mapOf("idToken" to "same"))
        val gate=java.util.concurrent.CountDownLatch(1);val entered=java.util.concurrent.CountDownLatch(1)
        doAnswer {entered.countDown();gate.await(3,java.util.concurrent.TimeUnit.SECONDS);null}.`when`(a.socket).sendMessage(any())
        doAnswer {gate.countDown();`when`(a.socket.isOpen).thenReturn(false);null}.`when`(a.socket).close(any())
        val ping=TextMessage(json.writeValueAsString(mapOf("type" to "PING","version" to 1,"sequence" to ++a.sequence,"timestamp" to Instant.now().toString(),"payload" to emptyMap<String,Any>())))
        val producer=Thread.ofVirtual().start {handler.handleMessage(a.socket,ping)}
        assertTrue(entered.await(2,java.util.concurrent.TimeUnit.SECONDS));producer.join(100)
        assertFalse(producer.isAlive,"Producer must return while socket remains blocked")
        handler.handleTransportError(a.socket,IllegalStateException("test failure"));assertTrue(handler.awaitOutboundIdle())
        assertEquals(1,store.onlinePlayers());assertEquals(1,store.leases.size);assertTrue(b.socket.isOpen)
        send(b,"PING");assertTrue(b.messages.last().contains("PONG"));verify(a.socket,times(1)).close(any())
    }
    @Test fun `close during token verification leaves no lease or delayed auth frame`() {
        val gate=java.util.concurrent.CountDownLatch(1);val entered=java.util.concurrent.CountDownLatch(1)
        val local=RealtimeHandler(FirebaseTokenVerifier {entered.countDown();gate.await();FirebaseIdentity("late",true)},store,RealtimeProperties())
        val socket=mock(WebSocketSession::class.java);`when`(socket.id).thenReturn("late-socket");`when`(socket.isOpen).thenReturn(true)
        local.afterConnectionEstablished(socket)
        val producer=Thread.ofVirtual().start {local.handleMessage(socket,TextMessage(json.writeValueAsString(mapOf("type" to "AUTH","version" to 1,"sequence" to 1,"timestamp" to Instant.now().toString(),"payload" to mapOf("idToken" to "test")))))}
        try {
            assertTrue(entered.await(2,java.util.concurrent.TimeUnit.SECONDS));local.handleTransportError(socket,IllegalStateException("test failure"));assertTrue(local.awaitOutboundIdle())
            gate.countDown();producer.join(2000);assertFalse(producer.isAlive);assertEquals(0,store.onlinePlayers());verify(socket,never()).sendMessage(any())
        } finally {gate.countDown();local.shutdown();assertTrue(local.awaitOutboundIdle())}
    }
}
