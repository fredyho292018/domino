package com.teamfho.domino.social

import com.teamfho.domino.realtime.*
import org.junit.jupiter.api.Test
import org.junit.jupiter.api.condition.EnabledIfEnvironmentVariable
import org.springframework.data.redis.connection.lettuce.LettuceConnectionFactory
import org.springframework.data.redis.core.StringRedisTemplate
import java.util.UUID
import kotlin.test.*

/** Dedicated loopback instance only; never FLUSHDB or production credentials. */
@EnabledIfEnvironmentVariable(named="DOMINO_S14B_REDIS_TESTS",matches="true")
class SocialPresenceRedisTests {
    @Test fun `expired crashed connection cannot hide another renewed lease`() {
        val factory=LettuceConnectionFactory("127.0.0.1",16379);factory.afterPropertiesSet();factory.start()
        val store=RedisPresenceStore(StringRedisTemplate(factory),RealtimeProperties(1,5,6,7))
        val uid="s14b-ttl-${UUID.randomUUID()}";val x=UUID.randomUUID().toString();val y=UUID.randomUUID().toString()
        try {
            store.touch(uid,x,"a");store.touch(uid,y,"b")
            Thread.sleep(4000);store.touch(uid,y,"b");Thread.sleep(3300)
            assertEquals(SocialPresenceState.ONLINE,store.states(listOf(uid))[uid]);assertEquals(1,store.connectionCount(uid))
            Thread.sleep(4000)
            assertEquals(SocialPresenceState.OFFLINE,store.states(listOf(uid))[uid])
        }finally{store.remove(uid,x);store.remove(uid,y);factory.destroy()}
    }
    @org.junit.jupiter.params.ParameterizedTest
    @org.junit.jupiter.params.provider.ValueSource(ints=[1,20,50])
    fun `snapshot uses one bounded Redis script invocation`(count:Int) {
        val factory=LettuceConnectionFactory("127.0.0.1",16379);factory.afterPropertiesSet();factory.start()
        try {
            val template=org.mockito.Mockito.spy(StringRedisTemplate(factory))
            val store=RedisPresenceStore(template,RealtimeProperties())
            org.mockito.Mockito.clearInvocations(template)
            val result=store.states((1..count).map{"s14b-absent-${UUID.randomUUID()}"})
            assertEquals(count,result.size);assertTrue(result.values.all{it==SocialPresenceState.OFFLINE})
            val calls=org.mockito.Mockito.mockingDetails(template).invocations.count{it.method.name=="execute" && it.arguments.firstOrNull() is org.springframework.data.redis.core.script.RedisScript<*>}
            assertEquals(1,calls);println("S14B_REDIS_SNAPSHOT_$count SCRIPT_INVOCATIONS=$calls")
        }finally{factory.destroy()}
    }
    @Test fun `unique leases activity version ordering and last connection close`() {
        val factory=LettuceConnectionFactory("127.0.0.1",16379);factory.afterPropertiesSet();factory.start()
        val store=RedisPresenceStore(StringRedisTemplate(factory),RealtimeProperties())
        val uid="s14b-${UUID.randomUUID()}";val x=UUID.randomUUID().toString();val y=UUID.randomUUID().toString()
        try {
            fun state()=store.states(listOf(uid)).getValue(uid)
            assertEquals(SocialPresenceState.OFFLINE,state())
            store.touch(uid,x,"a");store.touch(uid,y,"b")
            assertEquals(SocialPresenceState.ONLINE,state());assertEquals(2,store.connectionCount(uid))
            store.matchActivity(uid,"0002",true);assertEquals(SocialPresenceState.IN_MATCH,state())
            store.matchActivity(uid,"0001",false);assertEquals(SocialPresenceState.IN_MATCH,state())
            store.matchActivity(uid,"0003",false);assertEquals(SocialPresenceState.ONLINE,state())
            store.remove(uid,x);assertEquals(SocialPresenceState.ONLINE,state())
            store.remove(uid,y);assertEquals(SocialPresenceState.OFFLINE,state())
        }finally{store.remove(uid,x);store.remove(uid,y);factory.destroy()}
    }
}
