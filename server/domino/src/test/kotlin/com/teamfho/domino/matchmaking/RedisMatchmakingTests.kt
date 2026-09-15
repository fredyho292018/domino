package com.teamfho.domino.matchmaking

import com.teamfho.domino.catalog.*
import com.teamfho.domino.match.*
import com.teamfho.domino.online.*
import com.teamfho.domino.realtime.*
import org.junit.jupiter.api.Test
import org.junit.jupiter.api.condition.EnabledIfEnvironmentVariable
import org.springframework.data.redis.connection.lettuce.LettuceConnectionFactory
import org.springframework.data.redis.core.StringRedisTemplate
import java.util.UUID
import java.util.concurrent.Executors
import kotlin.test.*

@EnabledIfEnvironmentVariable(named="DOMINO_REDIS_TESTS",matches="true")
class RedisMatchmakingTests {
    class Fixture:AutoCloseable {
        val factory=LettuceConnectionFactory("127.0.0.1",6379).apply{afterPropertiesSet();start()}
        val redis=StringRedisTemplate(factory)
        val prefix="domino:v1:{presence}:mm:test-${UUID.randomUUID()}:"
        val presence=RedisPresenceStore(redis,RealtimeProperties())
        val users=mutableListOf<String>()
        val catalog=GameCatalogService(GameCatalogRepository{GameCatalogV3Publisher.canonical()})
        val key=MatchmakingKey.resolve(catalog.resolve()!!,"DUEL_1V1")
        val rules=MatchRuleSnapshot.freeze(catalog.resolve()!!,catalog.resolve()!!.modes.single{it.key=="DUEL_1V1"})
        fun worker(lease:Long=30000)=RedisMatchmakingStore(redis,prefix,lease)
        val store=worker()
        fun user():String=UUID.randomUUID().toString().also{users.add(it);presence.touch(it,it,"i3-test")}
        override fun close(){users.forEach{presence.remove(it,it)};val keys=redis.keys("$prefix*");if(keys.isNotEmpty())redis.delete(keys);factory.destroy()}
    }
    @Test fun `FIFO unique UID multiple devices incompatible keys and presence`()=Fixture().use {f->
        val a=f.user();val b=f.user();val c=f.user()
        assertEquals(QueueState.QUEUED,f.store.join(a,f.key).state)
        f.presence.touch(a,"second","i3-test")
        try {
            repeat(3){f.store.join(a,f.key)}
            assertEquals(1,f.store.waiting())
            assertNull(f.store.reserve(f.key,f.rules))
            val incompatible=f.key.copy(ruleSetVersion=2)
            f.store.join(b,incompatible);assertNull(f.store.reserve(f.key,f.rules))
            f.store.join(c,f.key)
            val pair=f.store.reserve(f.key,f.rules)!!
            assertEquals(listOf(a,c),listOf(pair.uidA,pair.uidB));assertNotEquals(pair.uidA,pair.uidB)
            assertEquals(QueueState.RESERVED,f.store.leave(a).state)
            f.store.complete(pair);assertEquals(QueueState.MATCHED,f.store.status(a).state)
            assertEquals(QueueState.QUEUED,f.store.status(b).state)
        }finally{f.presence.remove(a,"second")}
        val gone=f.user();f.store.join(gone,f.key);f.presence.remove(gone,gone)
        assertTrue(gone in f.store.cleanup());assertEquals(QueueState.NOT_QUEUED,f.store.status(gone).state)
        assertFailsWith<MatchmakingFailure>{f.store.join("not-present",f.key)}
    }
    @Test fun `two Redis connections concurrent workers reserve each UID only once`()=Fixture().use {f->
        val users=(1..100).map{f.user()};users.forEach{f.store.join(it,f.key)}
        val otherFactory=LettuceConnectionFactory("127.0.0.1",6379).apply{afterPropertiesSet();start()}
        val pool=Executors.newFixedThreadPool(8)
        try {
            val other=RedisMatchmakingStore(StringRedisTemplate(otherFactory),f.prefix)
            val pairs=pool.invokeAll((1..100).map{i->java.util.concurrent.Callable{(if(i%2==0)f.store else other).reserve(f.key,f.rules)}}).mapNotNull{it.get()}
            assertEquals(50,pairs.size);assertEquals(50,pairs.map{it.id}.distinct().size)
            assertEquals(users.toSet(),pairs.flatMap{listOf(it.uidA,it.uidB)}.toSet())
            assertEquals(100,pairs.flatMap{listOf(it.uidA,it.uidB)}.distinct().size)
            assertEquals(0,f.store.waiting());pairs.forEach{f.store.complete(it)}
        }finally{pool.shutdownNow();otherFactory.destroy()}
    }
    @Test fun `three concurrent joins produce one pair and one waiting player`()=Fixture().use {f->
        val users=(1..3).map{f.user()};val pool=Executors.newFixedThreadPool(3)
        try {
            val start=java.util.concurrent.CountDownLatch(1)
            val joins=users.map{uid->pool.submit<QueueStatus>{start.await();f.worker().join(uid,f.key)}}
            start.countDown();joins.forEach{assertEquals(QueueState.QUEUED,it.get().state)}
            val pairs=pool.invokeAll((1..3).map{java.util.concurrent.Callable{f.worker().reserve(f.key,f.rules)}}).mapNotNull{it.get()}
            assertEquals(1,pairs.size);val pair=pairs.single();assertNotEquals(pair.uidA,pair.uidB)
            val remaining=users.single{it!=pair.uidA&&it!=pair.uidB}
            assertEquals(QueueState.QUEUED,f.store.status(remaining).state);assertEquals(1,f.store.waiting())
            f.store.complete(pair);assertNull(f.store.reserve(f.key,f.rules))
        }finally{pool.shutdownNow()}
    }
    @Test fun `cancel racing reservation has exactly one winner`()=Fixture().use{f->
        val pool=Executors.newFixedThreadPool(2)
        try {repeat(25){
            val a=f.user();val b=f.user();f.store.join(a,f.key);f.store.join(b,f.key)
            val start=java.util.concurrent.CountDownLatch(1)
            val cancelled=pool.submit<QueueStatus>{start.await();f.store.leave(a)}
            val reserved=pool.submit<PairReservation?>{start.await();f.worker().reserve(f.key,f.rules)}
            start.countDown();val status=cancelled.get();val pair=reserved.get()
            if(status.state==QueueState.NOT_QUEUED){assertNull(pair);f.store.leave(b)}
            else {assertEquals(QueueState.RESERVED,status.state);assertNotNull(pair);assertEquals(setOf(a,b),setOf(pair.uidA,pair.uidB));f.store.complete(pair)}
        }}finally{pool.shutdownNow()}
    }
    @Test fun `expired owner lease retains same creation identity and fences stale completion`()=Fixture().use{f->
        val store=f.worker(80);val a=f.user();val b=f.user();store.join(a,f.key);store.join(b,f.key)
        val original=store.reserve(f.key,f.rules)!!;Thread.sleep(120)
        val recovered=store.recover()!!
        assertEquals(original.id,recovered.id);assertNotEquals(original.owner,recovered.owner)
        store.failed(original);assertEquals(QueueState.RESERVED,store.status(a).state)
        store.complete(recovered);assertEquals(QueueState.MATCHED,store.status(a).state)
        assertNull(store.recover());assertNull(store.reserve(f.key,f.rules))
    }
    @Test fun `creation failure notifies failure releases reservation and allows explicit retry`()=Fixture().use{f->
        val a=f.user();val b=f.user();val memory=MemoryOnlineRepository()
        val repo=object:OnlineRepository by memory {
            override fun createPaired(write:OnlineWrite):OnlineState=throw IllegalStateException("test failure")
            override fun settleFailedCreation(id:String):OnlineState?=null
        }
        val service=MatchmakingService(f.store,f.catalog,OnlineMatchService(f.catalog,repo))
        val events=mutableListOf<String>();service.notify={_,type,_->events.add(type)}
        service.join(a,"DUEL_1V1");service.join(b,"DUEL_1V1");service.tick()
        assertEquals(listOf("MATCHMAKING_STATUS","MATCHMAKING_STATUS"),events)
        assertEquals(QueueState.FAILED,f.store.status(a).state);assertEquals(0,f.store.waiting());assertTrue(memory.states.isEmpty())
        assertEquals(QueueState.QUEUED,service.join(a,"DUEL_1V1").state)
    }
    @Test fun `service uses existing engine and recovers lost MATCH_FOUND without another match`()=Fixture().use{f->
        val a=f.user();val b=f.user();val memory=MemoryOnlineRepository()
        val repo=object:OnlineRepository by memory {
            @Synchronized override fun createPaired(write:OnlineWrite):OnlineState {
                memory.read(write.state.match.matchId)?.let{return it};memory.create(write.state);return write.state
            }
            override fun settleFailedCreation(id:String)=memory.read(id)
            override fun activeFor(uid:String)=memory.states.values.filter{s->s.match.participants.any{it.playerUid==uid}}.map{it.match.matchId}
        }
        val service=MatchmakingService(f.store,f.catalog,OnlineMatchService(f.catalog,repo))
        val delivered=mutableMapOf<String,QueueStatus>();service.notify={uid,_,s->delivered[uid]=s}
        service.join(a,"DUEL_1V1");service.join(b,"DUEL_1V1");service.tick()
        assertEquals(1,memory.states.size);assertEquals(setOf(a,b),delivered.keys)
        assertEquals(delivered[a]!!.match!!.matchId,delivered[b]!!.match!!.matchId)
        assertNotEquals(delivered[a]!!.match!!.seat,delivered[b]!!.match!!.seat)
        val created=memory.states.values.single();assertEquals(OnlinePhase.STARTER_SELECTION,created.phase)
        assertEquals(f.rules,created.match.ruleSnapshot);assertTrue(created.hands.isEmpty())
        assertEquals(delivered[a],service.active(a));assertEquals(delivered[a]!!.match,service.join(a,"DUEL_1V1").match)
        assertEquals("ACTIVE_MATCH_EXISTS",service.join(a,"DUEL_1V1").reason)
        assertEquals(1,memory.states.size);assertEquals(0,f.store.waiting())
        // Only this fixture's queue namespace is lost; committed Match remains authoritative.
        val keys=f.redis.keys("${f.prefix}*");if(keys.isNotEmpty())f.redis.delete(keys)
        assertEquals(delivered[a],service.leave(a));assertEquals(delivered[b],service.status(b))
    }
    @Test fun `rate limit never duplicates position`()=Fixture().use{f->
        val a=f.user();repeat(20){f.store.join(a,f.key)}
        assertEquals("RATE_LIMIT",assertFailsWith<MatchmakingFailure>{f.store.join(a,f.key)}.code)
        assertEquals(1,f.store.waiting())
    }
    @Test fun `independent OS workers execute production Lua without a JVM lock`()=Fixture().use{f->
        val a=f.user();val b=f.user();val c=f.user();listOf(a,b,c).forEach{f.store.join(it,f.key)}
        val script=RedisMatchmakingStore::class.java.getDeclaredField("script").apply{isAccessible=true}.get(f.store) as org.springframework.data.redis.core.script.RedisScript<*>
        // Hex transports JSON intact through the Windows Docker CLI argument boundary.
        val rulesHex=GameCatalogCodec.mapper.writeValueAsString(f.rules).toByteArray().joinToString(""){"%02x".format(it)}
        val transport="ARGV[5]=string.gsub(ARGV[5],'..',function(cc) return string.char(tonumber(cc,16)) end)\n"
        val workers=(1..2).map {
            ProcessBuilder("docker","exec","domino-redis","redis-cli","--raw","EVAL",transport+script.scriptAsString,"1",f.prefix,
                "reserve",f.key.value,UUID.randomUUID().toString(),UUID.randomUUID().toString(),rulesHex,"30000")
                .redirectErrorStream(true).start()
        }
        val results=workers.map {p->val out=p.inputStream.bufferedReader().readText().trim();assertEquals(0,p.waitFor(),out);out}.filter{it.isNotEmpty()}
        assertEquals(1,results.size,results.joinToString("\n"))
        val job=GameCatalogCodec.mapper.readTree(results.single())
        assertEquals(a,job["uidA"].asText());assertEquals(b,job["uidB"].asText())
        assertEquals(QueueState.RESERVED,f.store.status(a).state);assertEquals(QueueState.QUEUED,f.store.status(c).state)
        assertEquals(1,f.store.waiting())
    }
}
