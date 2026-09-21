package com.teamfho.domino.social

import org.junit.jupiter.api.Test
import org.junit.jupiter.api.condition.EnabledIfEnvironmentVariable
import org.springframework.data.redis.connection.lettuce.LettuceConnectionFactory
import org.springframework.data.redis.core.StringRedisTemplate
import java.util.UUID
import kotlin.test.*

class FollowRateFailureTests {
    @Test fun `redis missing prevents follow but never prevents safety block`() {
        val limiter=ResilientSocialRateGate(RedisSocialRateLimiter{null})
        assertEquals("SOCIAL_SERVICE_UNAVAILABLE",assertFailsWith<SocialFailure>{limiter.check("a",SocialOperation.FOLLOW)}.code)
        limiter.check("a",SocialOperation.BLOCK)
    }
}
@EnabledIfEnvironmentVariable(named="DOMINO_S13_REDIS_TESTS",matches="true")
class FollowRedisRateTests {
    @Test fun `two instances share rolling minute and day limits without touching unrelated keys`() {
        val factory=LettuceConnectionFactory("127.0.0.1",6379);factory.afterPropertiesSet();factory.start()
        val uid="s13-rate-test-${UUID.randomUUID()}"
        val hash=java.security.MessageDigest.getInstance("SHA-256").digest(uid.toByteArray()).joinToString(""){"%02x".format(it)}
        val key="domino:v1:social:rate:$hash:follow"
        try {
            val redis=StringRedisTemplate(factory);val a=ResilientSocialRateGate(RedisSocialRateLimiter{redis});val b=ResilientSocialRateGate(RedisSocialRateLimiter{redis})
            try {
                repeat(30){(if(it%2==0)a else b).check(uid,SocialOperation.FOLLOW)}
                assertEquals("SOCIAL_ACTION_RATE_LIMITED",assertFailsWith<SocialFailure>{b.check(uid,SocialOperation.FOLLOW)}.code)
                assertEquals(30L,redis.opsForZSet().size(key))
                // Age only this test's own entries relative to Redis server time; no sleep or production keys.
                val timeScript=org.springframework.data.redis.core.script.DefaultRedisScript("local t=redis.call('TIME'); return tonumber(t[1])*1000+math.floor(tonumber(t[2])/1000)",Long::class.java)
                val now=redis.execute(timeScript,emptyList())!!
                redis.delete(key)
                repeat(199){redis.opsForZSet().add(key,"fixture-$it",(now-120000).toDouble())}
                a.check(uid,SocialOperation.FOLLOW)
                assertEquals("SOCIAL_ACTION_RATE_LIMITED",assertFailsWith<SocialFailure>{b.check(uid,SocialOperation.FOLLOW)}.code)
                assertEquals(200L,redis.opsForZSet().size(key))
                a.check(uid,SocialOperation.BLOCK);assertEquals(200L,redis.opsForZSet().size(key))
            } finally {redis.delete(key)}
        } finally {factory.destroy()}
    }
}
