package com.teamfho.domino

import org.junit.jupiter.api.Test
import org.junit.jupiter.api.Tag
import org.springframework.boot.builder.SpringApplicationBuilder
import org.springframework.boot.autoconfigure.EnableAutoConfiguration
import org.springframework.boot.test.context.TestConfiguration
import org.springframework.boot.web.server.context.WebServerApplicationContext
import java.net.Socket
import kotlin.test.*

@TestConfiguration(proxyBeanMethods=false)
@EnableAutoConfiguration
class F0TemporaryServerConfiguration

@Tag("IN_MEMORY")
class ValidationServerCleanupTests {
    @Test fun `temporary spring server closes port even on validation failure`() {
        var port=0
        assertFailsWith<IllegalStateException> {
            SpringApplicationBuilder(F0TemporaryServerConfiguration::class.java).logStartupInfo(false)
                .run("--server.port=0","--spring.main.banner-mode=off").use {context->
                    port=(context as WebServerApplicationContext).webServer!!.port
                    Socket("127.0.0.1",port).use{assertTrue(it.isConnected)}
                    error("simulated validation failure")
                }
        }
        assertTrue(port>0)
        assertFails {Socket("127.0.0.1",port).use{}}
    }
}
