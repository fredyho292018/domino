package com.teamfho.domino.social

import org.junit.jupiter.api.Test
import org.junit.jupiter.api.condition.EnabledIfEnvironmentVariable
import org.springframework.data.redis.connection.lettuce.LettuceConnectionFactory
import org.springframework.data.redis.core.StringRedisTemplate
import java.util.UUID
import java.util.concurrent.atomic.AtomicInteger
import kotlin.test.*

/** Explicit opt-in; only the disposable A.2 loopback container, never the normal Redis instance. */
@EnabledIfEnvironmentVariable(named="DOMINO_A2_REDIS_TESTS",matches="true")
class SocialRateGateRedisTests {
    @Test fun `real refused socket remains bounded and falls back`() {
        // Reserve then release an ephemeral loopback port; no real service is contacted.
        val port=java.net.ServerSocket(0,1,java.net.InetAddress.getLoopbackAddress()).use{it.localPort}
        val config=org.springframework.data.redis.connection.RedisStandaloneConfiguration("127.0.0.1",port)
        val client=org.springframework.data.redis.connection.lettuce.LettuceClientConfiguration.builder()
            .commandTimeout(java.time.Duration.ofSeconds(1))
            .clientOptions(io.lettuce.core.ClientOptions.builder().socketOptions(
                io.lettuce.core.SocketOptions.builder().connectTimeout(java.time.Duration.ofSeconds(1)).build()).build()).build()
        val factory=LettuceConnectionFactory(config,client);factory.afterPropertiesSet();factory.start()
        try {
            BoundedSocialRedis(RedisSocialRateLimiter{StringRedisTemplate(factory)}).use {bounded->
                val gate=ResilientSocialRateGate(bounded)
                repeat(3) {
                    val start=System.nanoTime()
                    assertEquals(RateDecision.ALLOW,gate.decide("refused-test",SocialOperation.PROFILE))
                    val elapsed=(System.nanoTime()-start)/1000000
                    println("A2_REFUSED_SOCKET elapsedMillis=$elapsed")
                    assertTrue(elapsed<750,"200ms caller budget plus CI scheduling tolerance")
                }
                assertEquals(SocialRedisCircuit.State.OPEN,gate.circuit.state)
                assertTrue(bounded.activeWorkers()<=2);assertEquals(0,bounded.queuedTasks())
            }
        } finally {factory.destroy()}
    }
    @Test fun `real Redis distributed limits and controlled outage recovery`() {
        val factory=LettuceConnectionFactory("127.0.0.1",16379)
        factory.afterPropertiesSet();factory.start()
        try {
            val redis=StringRedisTemplate(factory)
            val actual=RedisSocialRateLimiter{redis}
            val clock=SocialTestClock();val calls=AtomicInteger();var unavailable=false
            val delegate=DistributedSocialGate {u,o->calls.incrementAndGet();if(unavailable)throw java.net.ConnectException("controlled outage");actual.allowed(u,o)}
            BoundedSocialRedis(delegate).use {bounded->
                val a=ResilientSocialRateGate(bounded,LocalSocialLimiter(clock),SocialRedisCircuit(clock))
                val b=ResilientSocialRateGate(bounded,LocalSocialLimiter(clock),SocialRedisCircuit(clock))
                // Establish connection outside the timed policy decision so cold startup is explicit.
                assertEquals("PONG",redis.connectionFactory!!.connection.use{it.ping()})
                val uid="a2-${UUID.randomUUID()}"
                val start=System.nanoTime()
                repeat(30){assertEquals(RateDecision.ALLOW,(if(it%2==0)a else b).decide(uid,SocialOperation.FOLLOW))}
                println("A2_REAL_REDIS healthyAverageMicros=${(System.nanoTime()-start)/30000}")
                repeat(4){assertEquals(RateDecision.DENY_429,a.decide(uid,SocialOperation.FOLLOW))}
                assertEquals(SocialRedisCircuit.State.CLOSED,a.circuit.state)
                assertEquals(RateDecision.ALLOW,a.decide(uid,SocialOperation.UNFOLLOW))
                unavailable=true
                repeat(3){assertEquals(RateDecision.UNAVAILABLE_503,a.decide(uid,SocialOperation.SEARCH))}
                val before=calls.get()
                repeat(100){assertEquals(RateDecision.UNAVAILABLE_503,a.decide(uid,SocialOperation.SEARCH))}
                assertEquals(before,calls.get())
                assertEquals(RateDecision.ALLOW,a.decide(uid,SocialOperation.PROFILE))
                assertEquals(RateDecision.ALLOW,a.decide(uid,SocialOperation.BLOCK))
                unavailable=false;clock.seconds(5)
                assertEquals(RateDecision.ALLOW,a.decide(uid,SocialOperation.SEARCH))
                assertEquals(SocialRedisCircuit.State.CLOSED,a.circuit.state)
            }
        } finally {factory.destroy()}
    }
}
