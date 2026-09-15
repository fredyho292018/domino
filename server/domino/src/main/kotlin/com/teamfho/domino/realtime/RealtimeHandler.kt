package com.teamfho.domino.realtime

import com.teamfho.domino.online.*
import com.teamfho.domino.catalog.GameCatalogCodec
import com.teamfho.domino.match.MatchCodec
import com.teamfho.domino.security.AuthFailure
import com.teamfho.domino.security.FirebaseTokenVerifier
import org.springframework.scheduling.annotation.Scheduled
import org.springframework.stereotype.Component
import org.springframework.web.socket.*
import org.springframework.web.socket.handler.TextWebSocketHandler
import tools.jackson.databind.json.JsonMapper
import tools.jackson.core.StreamReadFeature
import tools.jackson.core.StreamReadConstraints
import tools.jackson.core.json.JsonFactory
import java.time.Instant
import java.util.UUID
import java.util.concurrent.ConcurrentHashMap

@Component
class RealtimeHandler(
    private val verifier: FirebaseTokenVerifier,
    private val presence: PresenceStore,
    private val properties: RealtimeProperties,
    private val online: OnlineMatchService? = null,
    private val turnWorker: OnlineTurnWorker? = null,
    private val matchmaking: com.teamfho.domino.matchmaking.MatchmakingService? = null,
) : TextWebSocketHandler() {
    private class Connection(val socket: WebSocketSession) {
        val id = UUID.randomUUID().toString()
        val opened = System.nanoTime()
        var lastHeartbeat = opened
        var lastLease = opened
        @Volatile var uid: String? = null
        var subscribed = false
        var sequence = 0L
        var incoming = 0L
        var rateWindow = opened
        var messages = 0
    }
    private val connections = ConcurrentHashMap<String, Connection>()
    private val serverId = UUID.randomUUID().toString()
    private val json = JsonMapper.builder(JsonFactory.builder()
        .enable(StreamReadFeature.STRICT_DUPLICATE_DETECTION)
        .streamReadConstraints(StreamReadConstraints.builder().maxNestingDepth(8).maxStringLength(MAX_BYTES).build()).build()).build()
    private var lastCount = -1L
    private var lastWaiting = -1L
    companion object { const val MAX_BYTES = 32768 }

    init {
        matchmaking?.notify={uid,type,state ->
            connections.values.filter{it.uid==uid}.forEach{c -> synchronized(c) {
                if(c.socket.isOpen)try{send(c,type,MatchCodec.map(state))}catch(_:Exception){close(c,1011)}
            }}
        }
        online?.committed = { write ->
            connections.values.forEach { c ->
                val uid=c.uid
                val participant=write.state.match.participants.singleOrNull {it.playerUid==uid}
                if(uid!=null && participant!=null) synchronized(c) {
                    if(c.socket.isOpen) try {
                        send(c,"MATCH_UPDATE",mapOf("matchId" to write.state.match.matchId,
                            "firstSequence" to write.events.first().sequence,
                            "events" to write.events.map {MatchCodec.map(OnlineMatchService.authorized(it,participant.seatIndex))},
                            "snapshot" to MatchCodec.map(OnlineMatchService.snapshot(write.state,uid))))
                    } catch(_: Exception) { close(c,1011) }
                }
            }
        }
    }
    override fun afterConnectionEstablished(session: WebSocketSession) {
        if (connections.size >= 1000) { session.close(CloseStatus.SERVICE_OVERLOAD); return }
        session.textMessageSizeLimit = MAX_BYTES
        session.binaryMessageSizeLimit = 1
        connections[session.id] = Connection(session)
    }
    override fun handleTextMessage(session: WebSocketSession, message: TextMessage) {
        val c = connections[session.id] ?: return
        var onlineCommand: OnlineCommand? = null
        try {
            synchronized(c) {
                if (!session.isOpen) return
                val now = System.nanoTime()
                if (now - c.rateWindow >= 1_000_000_000) { c.rateWindow = now; c.messages = 0 }
                if (++c.messages > properties.messagesPerSecond) { fail(c, "RATE_LIMIT"); return }
                if (message.payload.toByteArray().size > MAX_BYTES) { fail(c, "MESSAGE_SIZE"); return }
                val root = json.readTree(message.payload)
                if (!root.isObject || root.size() != 5 || !root.path("version").isIntegralNumber || root.path("version").asInt() != 1 ||
                    !root.path("sequence").isIntegralNumber || root.path("sequence").asLong() <= c.incoming ||
                    !root.path("timestamp").isString || !root.path("payload").isObject) { fail(c, "PROTOCOL"); return }
                Instant.parse(root.path("timestamp").asString())
                c.incoming = root.path("sequence").asLong()
                val payload = root.path("payload")
                when (root.path("type").asString()) {
                    "AUTH" -> {
                        if (c.uid != null || payload.size() != 1 || !payload.path("idToken").isString || expired(c.opened, properties.authTimeoutSeconds)) {
                            fail(c, "PROTOCOL"); return
                        }
                        val token = payload.path("idToken").asString()
                        if (token.isBlank() || token.length > 8192) { fail(c, "AUTH_TOKEN_INVALID", true); return }
                        val identity = verifier.verify(token)
                        // Timeout task may have closed the socket during Firebase verification.
                        if (!session.isOpen || expired(c.opened, properties.authTimeoutSeconds)) { close(c, 1008); return }
                        presence.touch(identity.uid, c.id, serverId)
                        if (!session.isOpen) { presence.remove(identity.uid, c.id); cleanup(c); return }
                        c.uid = identity.uid
                        c.lastHeartbeat = System.nanoTime(); c.lastLease = c.lastHeartbeat
                        send(c, "AUTHENTICATED", mapOf("heartbeatIntervalSeconds" to properties.heartbeatIntervalSeconds,
                            "heartbeatTimeoutSeconds" to properties.heartbeatTimeoutSeconds))
                        send(c, "PRESENCE_READY", mapOf("state" to "ONLINE"))
                        turnWorker?.connectionChanged(identity.uid)
                    }
                    "PING", "GLOBAL_ACTIVITY_SUBSCRIBE", "GLOBAL_ACTIVITY_UNSUBSCRIBE" -> {
                        if (c.uid == null || payload.size() != 0) { fail(c, "PROTOCOL"); return }
                        when (root.path("type").asString()) {
                            "PING" -> {
                                c.lastHeartbeat = now
                                if (expired(c.lastLease, properties.heartbeatIntervalSeconds)) {
                                    presence.touch(c.uid!!, c.id, serverId); c.lastLease = now
                                }
                                send(c, "PONG", emptyMap())
                            }
                            "GLOBAL_ACTIVITY_SUBSCRIBE" -> {
                                c.subscribed = true; send(c, "GLOBAL_ACTIVITY_SNAPSHOT", activity(presence.onlinePlayers()))
                            }
                            else -> c.subscribed = false
                        }
                    }
                    "MATCH_COMMAND" -> {
                        if(c.uid==null || online==null) { fail(c,"PROTOCOL");return }
                        try { onlineCommand=GameCatalogCodec.mapper.readValue(payload.toString(),OnlineCommand::class.java) }
                        catch(_: Exception) {
                            val id=payload.path("commandId").asString("").take(128)
                            send(c,"COMMAND_REJECTED",mapOf("commandId" to id,"code" to "INVALID_COMMAND"));return
                        }
                    }
                    else -> fail(c, "PROTOCOL")
                }
            }
            onlineCommand?.let {command ->
                try {
                    val result=online!!.command(c.uid!!,command)
                    synchronized(c) {send(c,"COMMAND_ACCEPTED",mapOf("commandId" to command.commandId,"matchId" to command.matchId,"resultingSequence" to result.receipt.resultingSequence))}
                } catch(e: OnlineFailure) {
                    org.slf4j.LoggerFactory.getLogger(javaClass).info("ONLINE_COMMAND_REJECTED code={}",e.code.name)
                    synchronized(c) {send(c,"COMMAND_REJECTED",mapOf("commandId" to command.commandId,"code" to e.code.name))}
                } catch(_: Exception) {
                    synchronized(c) {send(c,"COMMAND_REJECTED",mapOf("commandId" to command.commandId,"code" to "STORAGE_UNAVAILABLE"))}
                }
            }
        } catch (failure: AuthFailure) {
            synchronized(c) { fail(c, failure.code.name, true) }
        } catch (_: org.springframework.dao.DataAccessException) {
            synchronized(c) { fail(c, "UNAVAILABLE", retryable = true) }
        } catch (_: Exception) {
            synchronized(c) { fail(c, "PROTOCOL") }
        }
    }
    private fun activity(count: Long) = mapOf("onlinePlayers" to count, "activeMatches" to 0,
        "waitingPlayers" to (matchmaking?.waiting()?:0), "openRooms" to 0, "generatedAt" to Instant.now().toString())
    private fun expired(since: Long, seconds: Long) = System.nanoTime() - since >= seconds * 1_000_000_000
    private fun send(c: Connection, type: String, payload: Map<String, Any>) {
        c.socket.sendMessage(TextMessage(json.writeValueAsString(mapOf("type" to type, "version" to 1,
            "sequence" to ++c.sequence, "timestamp" to Instant.now().toString(), "payload" to payload))))
    }
    private fun fail(c: Connection, code: String, auth: Boolean = false, retryable: Boolean = false) {
        try { send(c, if (auth) "AUTH_FAILED" else "SYSTEM_ERROR", mapOf("code" to code)) }
        finally { close(c, if (retryable) 1013 else 1008) }
    }
    private fun close(c: Connection, code: Int) {
        try { c.socket.close(CloseStatus(code, "Realtime closed")) } catch (_: Exception) { }
        cleanup(c)
    }
    private fun cleanup(c: Connection) {
        if (!connections.remove(c.socket.id, c)) return
        c.uid?.let { try { presence.remove(it, c.id) } catch (_: Exception) { /* lease expires */ };turnWorker?.connectionChanged(it);matchmaking?.connectionLost(it) }
    }
    override fun afterConnectionClosed(session: WebSocketSession, status: CloseStatus) {
        connections[session.id]?.let { synchronized(it) { cleanup(it) } }
    }
    override fun handleTransportError(session: WebSocketSession, exception: Throwable) {
        connections[session.id]?.let { synchronized(it) { close(it, 1011) } }
    }
    @Scheduled(fixedDelay = 1000)
    fun expireConnections() {
        connections.values.forEach { c ->
            // Closing an unauthenticated transport must not wait for token verification.
            if (c.uid == null && expired(c.opened, properties.authTimeoutSeconds)) {
                try { c.socket.close(CloseStatus.POLICY_VIOLATION) } catch (_: Exception) { }
                cleanup(c)
                return@forEach // verification thread performs cleanup; do not block the deadline scheduler
            }
            synchronized(c) {
                if (!c.socket.isOpen || (c.uid != null && expired(c.lastHeartbeat, properties.heartbeatTimeoutSeconds))) close(c, 1001)
            }
        }
    }
    @Scheduled(fixedDelay = 2000)
    fun publishActivity() {
        try {
            val count = presence.onlinePlayers()
            val waiting = matchmaking?.waiting()?:0L
            if (count == lastCount && waiting == lastWaiting) return
            lastCount = count
            lastWaiting = waiting
            connections.values.forEach { c -> synchronized(c) {
                if (c.uid != null && c.subscribed) try { send(c, "GLOBAL_ACTIVITY_UPDATED", activity(count)) } catch (_: Exception) { close(c, 1011) }
            } }
        } catch (_: Exception) {
            lastCount = -1
            connections.values.forEach { c -> synchronized(c) { try { fail(c, "UNAVAILABLE", retryable = true) } catch (_: Exception) { close(c, 1013) } } }
        }
    }
}
