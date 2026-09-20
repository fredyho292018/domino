package com.teamfho.domino.realtime

import jakarta.websocket.*
import jakarta.websocket.server.ServerContainer
import jakarta.websocket.server.ServerEndpointConfig
import org.apache.catalina.startup.Tomcat
import org.apache.tomcat.websocket.server.WsSci
import org.junit.jupiter.api.Test
import org.springframework.web.socket.adapter.standard.StandardWebSocketSession
import java.net.Socket
import java.nio.file.Files
import java.util.concurrent.*
import java.util.concurrent.atomic.AtomicInteger
import kotlin.test.*

/** Real embedded transport, raw loopback client that stops reading. No Spring/Firebase/Redis. */
class OutboundTransportTests {
    class Probe : Endpoint() {
        override fun onOpen(session: Session, config: EndpointConfig) { opened.complete(session) }
        companion object { var opened = CompletableFuture<Session>() }
    }
    @Test fun `real Tomcat forced close releases a blocked send without thread cancellation`() = probe(false)
    @Test fun `real Tomcat outbound writer deadline releases blocked send and cleanup`() = probe(true)
    private fun probe(useWriter:Boolean) {
        val root = Files.createTempDirectory("domino-outbound-")
        val server = Tomcat()
        Probe.opened = CompletableFuture()
        try {
            server.setBaseDir(root.toString()); server.setPort(0)
            server.connector.setProperty("address", "127.0.0.1")
            server.connector.setProperty("socket.txBufSize", "1024")
            val context = server.addContext("", root.toString())
            Tomcat.addServlet(context, "default", org.apache.catalina.servlets.DefaultServlet())
            context.addServletMappingDecoded("/", "default")
            context.addServletContainerInitializer(WsSci(), emptySet())
            server.start()
            (context.servletContext.getAttribute("jakarta.websocket.server.ServerContainer") as ServerContainer)
                .addEndpoint(ServerEndpointConfig.Builder.create(Probe::class.java, "/probe").build())
            Socket().use { socket ->
                socket.receiveBufferSize = 1024; socket.connect(java.net.InetSocketAddress("127.0.0.1", server.connector.localPort))
                socket.soTimeout = 3000
                socket.getOutputStream().write(("GET /probe HTTP/1.1\r\nHost: 127.0.0.1\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\nSec-WebSocket-Version: 13\r\n\r\n").toByteArray())
                val header = StringBuilder()
                while (!header.endsWith("\r\n\r\n")) header.append(socket.getInputStream().read().also { assertTrue(it >= 0) }.toChar())
                assertTrue(header.contains("101"), header.toString())
                val native = Probe.opened.get(3, TimeUnit.SECONDS)
                val spring = StandardWebSocketSession(org.springframework.http.HttpHeaders(), emptyMap<String, Any>(), null, null).apply { initializeNativeSession(native) }
                if(useWriter) {
                    val cleaned=AtomicInteger()
                    val outbound=ConnectionOutbound(spring,cleaned={cleaned.incrementAndGet()})
                    outbound.offerControl("AUTHENTICATED",emptyMap());assertTrue(outbound.awaitIdle())
                    val started=System.nanoTime()
                    outbound.offerCritical("MATCH_UPDATE",mapOf("large" to "x".repeat(120*1024)))
                    assertTrue(outbound.awaitClosed(4000))
                    assertEquals(1,cleaned.get());assertEquals("CLOSED",outbound.snapshot().lifecycle)
                    println("NATIVE_WRITER_DEADLINE_CLEANUP_MS=${(System.nanoTime()-started)/1_000_000}")
                } else {
                val transport = OutboundTransport(spring, 1000)
                val sent = AtomicInteger(); val entered = CountDownLatch(1)
                val writer = Thread.ofVirtual().start {
                    try { repeat(1000) { entered.countDown(); transport.send("x".repeat(128 * 1024)); sent.incrementAndGet() } }
                    catch (_: Exception) { }
                }
                assertTrue(entered.await(2, TimeUnit.SECONDS))
                Thread.sleep(100) // socket receive window is intentionally not drained
                assertTrue(writer.isAlive, "Must exercise an in-flight blocked write")
                val started = System.nanoTime()
                transport.close(1013)
                writer.join(4000)
                val elapsed = (System.nanoTime() - started) / 1_000_000
                assertFalse(writer.isAlive, "Native close must release the writer")
                assertTrue(sent.get() < 1000)
                assertTrue(elapsed < 4000)
                println("NATIVE_SEND_CLOSE_UNBLOCK_MS=$elapsed COMPLETED_FRAMES=${sent.get()}")
                }
            }
        } finally {
            server.stop(); server.destroy()
            Files.walk(root).use { it.sorted(Comparator.reverseOrder()).forEach(Files::deleteIfExists) }
        }
    }
}
