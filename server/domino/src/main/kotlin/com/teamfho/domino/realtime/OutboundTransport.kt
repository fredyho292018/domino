package com.teamfho.domino.realtime

import jakarta.websocket.CloseReason
import org.apache.tomcat.websocket.WsSession
import org.springframework.web.socket.CloseStatus
import org.springframework.web.socket.TextMessage
import org.springframework.web.socket.WebSocketSession
import org.springframework.web.socket.adapter.standard.StandardWebSocketSession

/** The only application data-write adapter. The production transport is embedded Tomcat.
 * Its native blocking timeout and forced socket close bound I/O; interrupting a Future does not. */
internal class OutboundTransport(private val session: WebSocketSession, sendMillis: Long) {
    private val native = (org.springframework.web.socket.handler.WebSocketSessionDecorator.unwrap(session) as? StandardWebSocketSession)?.nativeSession
    init {
        if (native != null) {
            require(native is WsSession) { "Unsupported outbound transport" }
            native.userProperties["org.apache.tomcat.websocket.BLOCKING_SEND_TIMEOUT"] = sendMillis
            native.userProperties["org.apache.tomcat.websocket.SESSION_CLOSE_TIMEOUT"] = sendMillis
        }
    }
    fun send(frame: String) = session.sendMessage(TextMessage(frame))
    fun close(code: Int) {
        if (native is WsSession) {
            val reason = CloseReason(CloseReason.CloseCodes.getCloseCode(code), "Realtime closed")
            native.doClose(reason, reason, true)
        } else session.close(CloseStatus(code, "Realtime closed")) // test/framework adapter
    }
}
