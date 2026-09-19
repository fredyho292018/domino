package com.teamfho.domino.online

import org.springframework.data.redis.core.StringRedisTemplate
import org.springframework.data.redis.core.script.DefaultRedisScript
import java.time.Instant
import java.util.UUID

/** Ephemeral hints only. Firestore still checks turn, revision and deadline transactionally. */
interface TurnDueIndex {
    /** 0=not leader, 1=renewed lease, 2=new lease (requires a fresh durable snapshot). */
    fun lead(owner:String):Int
    fun release(owner:String)
    fun offer(owner:String,id:String,due:Instant?):Boolean
    fun claim(now:Instant):List<String>
    fun forget(id:String)
}

class RedisTurnDueIndex(private val redis:StringRedisTemplate,
    private val prefix:String="domino:v1:{turn-schedule}:"):TurnDueIndex {
    private val leader=prefix+"leader"
    private val due=prefix+"due"
    override fun lead(owner:String)=redis.execute(DefaultRedisScript(
        "if redis.call('GET',KEYS[1])==ARGV[1] then redis.call('PEXPIRE',KEYS[1],30000); return 1 end; if redis.call('SET',KEYS[1],ARGV[1],'NX','PX',30000) then return 2 end; return 0",Long::class.java),listOf(leader),owner)?.toInt()?:0
    override fun release(owner:String){redis.execute(DefaultRedisScript(
        "if redis.call('GET',KEYS[1])==ARGV[1] then return redis.call('DEL',KEYS[1]) end; return 0",Long::class.java),listOf(leader),owner)}
    override fun offer(owner:String,id:String,due:Instant?):Boolean=redis.execute(DefaultRedisScript(
        "if redis.call('GET',KEYS[1])~=ARGV[1] then return 0 end; if ARGV[3]=='REMOVE' then redis.call('ZREM',KEYS[2],ARGV[2]) else redis.call('ZADD',KEYS[2],ARGV[3],ARGV[2]) end; return 1",Long::class.java),
        listOf(leader,this.due),owner,id,due?.toEpochMilli()?.toString()?:"REMOVE")==1L
    @Suppress("UNCHECKED_CAST")
    override fun claim(now:Instant):List<String> = redis.execute(DefaultRedisScript(
        "local ids=redis.call('ZRANGEBYSCORE',KEYS[1],'-inf',ARGV[1],'LIMIT',0,100); for _,id in ipairs(ids) do redis.call('ZADD',KEYS[1],ARGV[2],id) end; return ids",List::class.java),
        listOf(due),now.toEpochMilli().toString(),now.plusSeconds(30).toEpochMilli().toString()) as? List<String> ?: emptyList()
    override fun forget(id:String){redis.opsForZSet().remove(due,id)}
}

fun interface TurnWorkFeed {
    /** Initial snapshot + committed changes of the durable work/outbox collection. */
    fun watch(changed:(String,Instant?)->Unit,failed:()->Unit):AutoCloseable
}

/** A single Redis-elected change-feed bridge closes the commit-to-Redis crash gap.
 * Redis loss closes/reopens the feed; the initial snapshot rebuilds all durable deadlines.
 * No periodic Firestore query is made while idle. */
class TurnIndexBridge(private val index:TurnDueIndex,private val feed:TurnWorkFeed,
    private val clock:java.time.Clock=java.time.Clock.systemUTC()):AutoCloseable {
    private var owner=UUID.randomUUID().toString()
    private var subscription:AutoCloseable?=null
    @Volatile private var failed=false
    private var retryAt=Instant.MIN
    private var failures=0
    @Synchronized fun tick() {
        if(clock.instant()<retryAt)return
        try {
            if(failed){backoff();return}
            val lease=index.lead(owner)
            if(lease==0||(lease==2&&subscription!=null)){reset();return}
            val sessionOwner=owner
            if(subscription==null)subscription=feed.watch({id,due->
                synchronized(this) {
                    if(owner==sessionOwner)try {if(!index.offer(sessionOwner,id,due))failed=true}catch(_:Exception){failed=true}
                }
            },{synchronized(this){if(owner==sessionOwner)failed=true}})
        }catch(_:Exception){backoff()}
    }
    private fun backoff(){reset();failed=false;failures=(failures+1).coerceAtMost(7);retryAt=clock.instant().plusSeconds((5L shl (failures-1)).coerceAtMost(300));org.slf4j.LoggerFactory.getLogger(javaClass).warn("ONLINE_TURN_INDEX_RECOVERY_DEFERRED")}
    private fun reset(){val oldOwner=owner;owner=UUID.randomUUID().toString();runCatching{subscription?.close()};subscription=null;runCatching{index.release(oldOwner)}}
    @Synchronized override fun close(){reset()}
}
