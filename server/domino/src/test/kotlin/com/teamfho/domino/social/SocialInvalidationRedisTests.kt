package com.teamfho.domino.social

import org.junit.jupiter.api.Test
import org.junit.jupiter.api.condition.EnabledIfEnvironmentVariable
import org.springframework.data.redis.connection.lettuce.LettuceConnectionFactory
import org.springframework.data.redis.core.StringRedisTemplate
import java.util.UUID
import java.util.concurrent.Executor
import java.util.concurrent.CountDownLatch
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicInteger
import kotlin.test.*

@EnabledIfEnvironmentVariable(named="DOMINO_A3_REDIS_TESTS",matches="true")
class SocialInvalidationRedisTests {
    private fun withRedis(body:(LettuceConnectionFactory,StringRedisTemplate)->Unit) {
        val f=LettuceConnectionFactory("127.0.0.1",16379);f.afterPropertiesSet();f.start()
        try{body(f,StringRedisTemplate(f))}finally{f.destroy()}
    }
    private fun await(body:()->Boolean){val until=System.nanoTime()+TimeUnit.SECONDS.toNanos(5);while(!body()&&System.nanoTime()<until)Thread.sleep(10);assertTrue(body())}
    @Test fun `two local indexes cross instance block privacy unfriend duplicates and reconnect`()=withRedis {f,redis->
        val a=UUID.randomUUID().toString();val b=UUID.randomUUID().toString()
        val left=LocalSocialAuthorizationIndex(Executor{it.run()});val right=LocalSocialAuthorizationIndex(Executor{it.run()})
        val state=java.util.concurrent.atomic.AtomicReference(SocialAuthorizationSnapshot(true,0,1))
        val reader=SocialAuthorizationReader{_,_->state.get()}
        val ha=left.register("a",a,b,reader);val hb=right.register("b",a,b,reader)
        left.recovered();right.recovered();assertTrue(ha.canDeliver());assertTrue(hb.canDeliver())
        val joined=CountDownLatch(2);val count=AtomicInteger()
        SocialInvalidationPubSub(f,redis,left::invalidate,{joined.countDown()},{left.recoveryFailed()}).use {pub->
            SocialInvalidationPubSub(f,redis,{count.incrementAndGet();right.invalidate(it)},{joined.countDown()},{right.recoveryFailed()}).use {sub->
                pub.start();sub.start();assertTrue(joined.await(5,TimeUnit.SECONDS))
                state.set(SocialAuthorizationSnapshot(false,1,1));val block=SocialInvalidation.pair(ha.pair,1)
                left.invalidate(block);assertFalse(ha.canDeliver());pub.publish(block);await{!hb.canDeliver()}
                state.set(SocialAuthorizationSnapshot(true,2,1));pub.publish(SocialInvalidation.pair(ha.pair,2));await{hb.canDeliver()}
                // A later FRIENDS-only decision is denied after unfriend.
                state.set(SocialAuthorizationSnapshot(false,3,1));pub.publish(SocialInvalidation.pair(ha.pair,3));await{!hb.canDeliver()}
                pub.publish(block);pub.publish(block);Thread.sleep(80);assertFalse(hb.canDeliver())
                state.set(SocialAuthorizationSnapshot(true,3,2));pub.publish(SocialInvalidation.privacy(b,2));await{hb.canDeliver()}
                state.set(SocialAuthorizationSnapshot(false,3,3));pub.publish(SocialInvalidation.privacy(b,3));await{!hb.canDeliver()}
                val before=count.get();redis.convertAndSend(SocialInvalidationPubSub.CHANNEL,"{bad json}")
                redis.convertAndSend(SocialInvalidationPubSub.CHANNEL,"x".repeat(5000));Thread.sleep(100);assertEquals(before,count.get())
            }
            // A fresh subscription proves reconnect setup; recovery is explicitly required by callback.
            val recovered=CountDownLatch(1)
            SocialInvalidationPubSub(f,redis,right::invalidate,{right.recoveryFailed();recovered.countDown()},{right.recoveryFailed()}).use {sub->
                sub.start();assertTrue(recovered.await(5,TimeUnit.SECONDS));assertFalse(hb.canDeliver())
                right.recovered();assertFalse(hb.canDeliver())
            }
        }
    }
    @Test fun `publication failure cannot rollback a local invalidation`()=withRedis {f,redis->
        val index=LocalSocialAuthorizationIndex(Executor{it.run()})
        val state=java.util.concurrent.atomic.AtomicReference(SocialAuthorizationSnapshot(true,0,1))
        val h=index.register("c","a","b"){_,_->state.get()};index.recovered();assertTrue(h.canDeliver())
        val broken=org.mockito.Mockito.mock(StringRedisTemplate::class.java)
        org.mockito.Mockito.`when`(broken.convertAndSend(org.mockito.ArgumentMatchers.anyString(),org.mockito.ArgumentMatchers.any())).thenThrow(IllegalStateException("test transport down"))
        SocialInvalidationPubSub(f,broken,index::invalidate,{},{}).use {bus->
            state.set(SocialAuthorizationSnapshot(false,1,1));val e=SocialInvalidation.pair(h.pair,1)
            index.invalidate(e);assertFails{bus.publish(e)};assertFalse(h.canDeliver())
        }
    }
    @Test fun `real pubsub disconnect automatically reconnects and requests durable reconciliation`()=withRedis {f,redis->
        val subscriptions=AtomicInteger();val delivered=AtomicInteger()
        SocialInvalidationPubSub(f,redis,{delivered.incrementAndGet()},{subscriptions.incrementAndGet()},{}).use {bus->
            bus.start();await{subscriptions.get()>=1}
            // Only the dedicated loopback 16379 test broker; never the application's Redis.
            java.net.Socket("127.0.0.1",16379).use {socket->
                socket.soTimeout=2000
                val args=listOf("CLIENT","KILL","TYPE","pubsub")
                val command="*4\r\n"+args.joinToString(""){"\$${it.length}\r\n$it\r\n"}
                socket.getOutputStream().write(command.toByteArray());socket.getOutputStream().flush()
                assertTrue(socket.getInputStream().bufferedReader().readLine().startsWith(":"))
            }
            await{subscriptions.get()>=2}
            bus.publish(SocialInvalidation.privacy("reconnect-test",1));await{delivered.get()==1}
        }
    }
}
