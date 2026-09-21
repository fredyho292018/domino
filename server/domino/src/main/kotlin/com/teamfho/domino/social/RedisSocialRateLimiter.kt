package com.teamfho.domino.social

import org.springframework.data.redis.core.StringRedisTemplate
import org.springframework.data.redis.core.script.DefaultRedisScript

class RedisSocialRateLimiter(private val redis: () -> StringRedisTemplate?) : DistributedSocialGate {
    private val script=DefaultRedisScript("""
        local t=redis.call('TIME'); local now=tonumber(t[1])+tonumber(t[2])/1000000
        local rate=tonumber(ARGV[1])/60; local capacity=tonumber(ARGV[2])
        local old=redis.call('HMGET',KEYS[1],'tokens','time')
        local tokens=math.min(capacity,(tonumber(old[1]) or capacity)+math.max(0,now-(tonumber(old[2]) or now))*rate)
        if tokens<1 then return 0 end
        redis.call('HSET',KEYS[1],'tokens',tokens-1,'time',now);redis.call('EXPIRE',KEYS[1],120);return 1
    """.trimIndent(),Long::class.java)
    private val followScript=DefaultRedisScript("""
        local t=redis.call('TIME'); local now=tonumber(t[1])*1000+math.floor(tonumber(t[2])/1000)
        redis.call('ZREMRANGEBYSCORE',KEYS[1],'-inf',now-86400000)
        if redis.call('ZCARD',KEYS[1])>=200 or redis.call('ZCOUNT',KEYS[1],now-60000+1,'+inf')>=30 then return 0 end
        redis.call('ZADD',KEYS[1],now,ARGV[1]);redis.call('PEXPIRE',KEYS[1],86400000);return 1
    """.trimIndent(),Long::class.java)
    override fun allowed(uid: String,operation:SocialOperation):Boolean {
        val action=operation.redisKey
        val limit=when(action){"NAME"->20;"FRIEND_CODE","unblock"->30;else->60}
        val burst=if(action=="NAME")5 else limit
        val hash=java.security.MessageDigest.getInstance("SHA-256").digest(uid.toByteArray()).joinToString(""){"%02x".format(it)}
        try {
            val result=(if(action=="follow")redis()?.execute(followScript,listOf("domino:v1:social:rate:$hash:follow"),java.util.UUID.randomUUID().toString())
                else redis()?.execute(script,listOf("domino:v1:social:rate:$hash:$action"),limit.toString(),burst.toString()))
                ?:throw SocialFailure("SOCIAL_SERVICE_UNAVAILABLE")
            return result==1L
        } catch(e:SocialFailure){throw e}catch(_:Exception){throw SocialFailure("SOCIAL_SERVICE_UNAVAILABLE")}
    }
}
