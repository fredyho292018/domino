package com.teamfho.domino.online

import com.google.cloud.firestore.Firestore
import com.teamfho.domino.realtime.*
import com.teamfho.domino.social.*
import org.junit.jupiter.api.Test
import org.junit.jupiter.api.condition.EnabledIfEnvironmentVariable
import org.mockito.Mockito.*
import org.springframework.beans.factory.support.StaticListableBeanFactory
import org.springframework.data.redis.connection.RedisConnectionFactory
import org.springframework.data.redis.connection.lettuce.LettuceConnectionFactory
import org.springframework.data.redis.core.StringRedisTemplate
import java.security.MessageDigest
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicInteger
import kotlin.test.*

@EnabledIfEnvironmentVariable(named="DOMINO_S701R_REDIS_TESTS",matches="true")
class AllAbandonedRedisTests {
    @Test fun `actual worker and presence writer stop renewal and projections expire naturally`() {
        val factory=LettuceConnectionFactory("127.0.0.1",16381);factory.afterPropertiesSet();factory.start()
        val redis=StringRedisTemplate(factory);val store=RedisPresenceStore(redis,RealtimeProperties())
        val writes=AtomicInteger();var sockets=1L
        val measured=object:PresenceStore by store {
            override fun connectionCount(uid:String)=sockets
            override fun matchActivity(uid:String,version:String,active:Boolean){store.matchActivity(uid,version,active);writes.incrementAndGet()}
        }
        val beans=StaticListableBeanFactory()
        val invalidation=mock(SocialInvalidationRuntime::class.java)
        `when`(invalidation.index).thenReturn(LocalSocialAuthorizationIndex(java.util.concurrent.Executor{it.run()}))
        val runtime=SocialPresenceRuntime(beans.getBeanProvider(Firestore::class.java),invalidation,measured,beans.getBeanProvider(RedisConnectionFactory::class.java))
        val f=AbandonFixture();val prefix="s701r:${f.id}:"
        val index=RedisTurnDueIndex(redis,prefix)
        val bridge=TurnIndexBridge(index,TurnWorkFeed{_,_->AutoCloseable{}})
        val worker=OnlineTurnWorker(f.repo,f.service,measured,index,bridge,runtime)
        val keys=(0..3).map {i->"domino:v1:{presence}:activity:"+MessageDigest.getInstance("SHA-256").digest("m5-p$i".toByteArray()).joinToString(""){"%02x".format(it)}}
        fun drain(target:Int) {
            val until=System.nanoTime()+TimeUnit.SECONDS.toNanos(10)
            while(writes.get()<target&&System.nanoTime()<until){runtime.maintain();Thread.sleep(20)}
            assertEquals(target,writes.get())
        }
        fun tick(){index.lead("test");assertTrue(index.offer("test",f.id,f.clock.instant()));worker.processDue(f.clock.instant())}
        try {
            tick();drain(4);assertTrue(keys.all{redis.opsForHash<String,String>().get(it,"active")=="true"})
            f.at(1);tick();drain(8) // Active projections can renew.
            sockets=0;f.at(10);tick();drain(12)
            f.at(190);tick();drain(16)
            assertEquals(com.teamfho.domino.match.MatchStatus.CANCELLED,f.state().match.status)
            assertEquals(0L,redis.opsForZSet().size(prefix+"due"))
            assertTrue(keys.all{redis.opsForHash<String,String>().get(it,"active")=="false"})
            val deadline=System.nanoTime()+TimeUnit.SECONDS.toNanos(125)
            while(System.nanoTime()<deadline&&keys.any{redis.hasKey(it)}) {
                // Even repeated stale discovery hints must not refresh a terminal projection.
                tick();runtime.maintain();Thread.sleep(1000)
                assertEquals(16,writes.get())
            }
            assertTrue(keys.none{redis.hasKey(it)},"120-second TTL must converge without deletion")
            assertNull(f.state().turnDeadlineAt)
            println("S701R_REDIS_NATURAL_EXPIRY=PASS TTL_SECONDS=120 TERMINAL_RENEWALS=0")
        } finally {runtime.close();index.release("test");bridge.close();factory.destroy()}
    }
}
