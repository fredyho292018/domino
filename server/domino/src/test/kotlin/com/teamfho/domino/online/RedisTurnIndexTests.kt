package com.teamfho.domino.online

import org.junit.jupiter.api.Test
import org.junit.jupiter.api.Tag
import org.junit.jupiter.api.condition.EnabledIfEnvironmentVariable
import org.springframework.data.redis.connection.lettuce.LettuceConnectionFactory
import org.springframework.data.redis.core.StringRedisTemplate
import java.time.Instant
import java.util.UUID
import java.util.concurrent.*
import kotlin.test.*

@Tag("LOCAL_REDIS")
@EnabledIfEnvironmentVariable(named="DOMINO_REDIS_TESTS",matches="true")
class RedisTurnIndexTests {
    @Test fun `atomic claims fencing restart rebuild and worker crash retry`() {
        val factory=LettuceConnectionFactory("127.0.0.1",6379);factory.afterPropertiesSet();factory.start()
        val redis=StringRedisTemplate(factory);val prefix="domino:test:{f0-${UUID.randomUUID()}}:"
        val index=RedisTurnDueIndex(redis,prefix);val now=Instant.now();val pool=Executors.newFixedThreadPool(2)
        try {
            assertEquals(2,index.lead("a"));assertEquals(0,index.lead("b"))
            assertFalse(index.offer("b","match",now));assertTrue(index.offer("a","match",now))
            val barrier=CyclicBarrier(2)
            val claims=pool.invokeAll((0..1).map{Callable{barrier.await();index.claim(now)}}).flatMap{it.get()}
            assertEquals(listOf("match"),claims)
            assertTrue(index.claim(now.plusSeconds(29)).isEmpty());assertEquals(listOf("match"),index.claim(now.plusSeconds(30)))
            index.release("a")
            var snapshots=0
            val feed=TurnWorkFeed {changed,_->snapshots++;changed("match",now);AutoCloseable{}}
            TurnIndexBridge(index,feed).use {bridge->
                bridge.tick();assertEquals(listOf("match"),index.claim(now))
                // Simulate Redis loss only in this test's unique namespace; never FLUSHDB.
                redis.delete(listOf(prefix+"leader",prefix+"due"));bridge.tick();bridge.tick()
                assertEquals(listOf("match"),index.claim(now));assertEquals(2,snapshots)
            }
        }finally{pool.shutdownNow();redis.delete(listOf(prefix+"leader",prefix+"due"));factory.destroy()}
    }
}
