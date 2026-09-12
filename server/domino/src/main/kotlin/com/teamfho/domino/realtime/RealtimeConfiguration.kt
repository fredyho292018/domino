package com.teamfho.domino.realtime

import org.springframework.boot.context.properties.ConfigurationProperties
import org.springframework.boot.context.properties.EnableConfigurationProperties
import org.springframework.context.annotation.Configuration
import org.springframework.scheduling.annotation.EnableScheduling
import org.springframework.web.socket.config.annotation.EnableWebSocket
import org.springframework.web.socket.config.annotation.WebSocketConfigurer
import org.springframework.web.socket.config.annotation.WebSocketHandlerRegistry
import org.springframework.web.socket.server.support.DefaultHandshakeHandler

@ConfigurationProperties("realtime")
data class RealtimeProperties(
    val authTimeoutSeconds: Long = 5,
    val heartbeatIntervalSeconds: Long = 20,
    val heartbeatTimeoutSeconds: Long = 45,
    val presenceTtlSeconds: Long = 60,
    val messagesPerSecond: Int = 10,
) {
    init {
        require(authTimeoutSeconds in 1..30)
        require(heartbeatIntervalSeconds in 5..60)
        require(heartbeatTimeoutSeconds > heartbeatIntervalSeconds)
        require(presenceTtlSeconds > heartbeatTimeoutSeconds)
        require(messagesPerSecond in 2..100)
    }
}

@Configuration(proxyBeanMethods = false)
@EnableWebSocket
@EnableScheduling
@EnableConfigurationProperties(RealtimeProperties::class)
class RealtimeConfiguration(private val handler: RealtimeHandler) : WebSocketConfigurer {
    override fun registerWebSocketHandlers(registry: WebSocketHandlerRegistry) {
        // Native clients omit Origin; browsers retain Spring's same-origin protection.
        registry.addHandler(handler, "/ws/v1/realtime")
            .setHandshakeHandler(DefaultHandshakeHandler())
    }
}
