package com.teamfho.domino.realtime

import org.junit.jupiter.params.ParameterizedTest
import org.junit.jupiter.params.provider.ValueSource
import org.springframework.web.socket.TextMessage
import kotlin.test.assertFalse

class RealtimeProtocolTests {
    @ParameterizedTest
    @ValueSource(strings = [
        "{\"type\":\"AUTH\",\"version\":2,\"sequence\":1,\"timestamp\":\"2026-09-12T00:00:00Z\",\"payload\":{\"idToken\":\"one\"}}",
        "{\"type\":\"AUTH\",\"version\":1,\"version\":1,\"sequence\":1,\"timestamp\":\"2026-09-12T00:00:00Z\",\"payload\":{\"idToken\":\"one\"}}",
        "{\"type\":\"AUTH\",\"version\":1,\"sequence\":0,\"timestamp\":\"2026-09-12T00:00:00Z\",\"payload\":{\"idToken\":\"one\"}}",
        "{\"type\":\"AUTH\",\"version\":1,\"sequence\":1,\"timestamp\":\"invalid\",\"payload\":{\"idToken\":\"one\"}}"
    ])
    fun `invalid envelope closes`(message: String) {
        val fixture=RealtimeHandlerTests();val peer=fixture.peer()
        fixture.handler.handleMessage(peer.socket,TextMessage(message))
        kotlin.test.assertTrue(fixture.handler.awaitOutboundIdle())
        assertFalse(peer.socket.isOpen)
        fixture.shutdown()
    }
}
