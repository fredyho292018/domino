package com.teamfho.domino.realtime

import org.junit.jupiter.api.Test
import org.junit.jupiter.api.condition.EnabledIfEnvironmentVariable
import org.springframework.data.redis.connection.lettuce.LettuceConnectionFactory
import org.springframework.data.redis.core.StringRedisTemplate
import java.util.UUID
import kotlin.test.*

// Opt in only against the explicitly started local Docker instance. Never FLUSHDB.
@EnabledIfEnvironmentVariable(named = "DOMINO_REDIS_TESTS", matches = "true")
class RedisPresenceTests {
    @Test fun `real atomic leases unique users last device and unclean TTL expiry`() {
        val factory = LettuceConnectionFactory("127.0.0.1",6379)
        factory.afterPropertiesSet(); factory.start()
        try {
            val template = StringRedisTemplate(factory)
            val store = RedisPresenceStore(template,RealtimeProperties(1,5,6,7))
            val baseline = store.onlinePlayers()
            val a = UUID.randomUUID().toString(); val b = UUID.randomUUID().toString()
            val x = UUID.randomUUID().toString(); val y = UUID.randomUUID().toString(); val z = UUID.randomUUID().toString()
            try {
                store.touch(a,x,"test"); store.touch(a,y,"test")
                assertEquals(baseline+1,store.onlinePlayers())
                store.touch(b,z,"test"); assertEquals(baseline+2,store.onlinePlayers())
                store.remove(a,x); assertEquals(baseline+2,store.onlinePlayers())
                store.remove(a,y); assertEquals(baseline+1,store.onlinePlayers())
                Thread.sleep(4000); store.touch(b,z,"test"); Thread.sleep(4000)
                assertEquals(baseline+1,store.onlinePlayers())
                Thread.sleep(3200); assertEquals(baseline,store.onlinePlayers())
            } finally { store.remove(a,x); store.remove(a,y); store.remove(b,z) }
        } finally { factory.destroy() }
    }
}
