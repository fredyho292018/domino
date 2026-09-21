package com.teamfho.domino.realtime

import org.springframework.data.redis.core.StringRedisTemplate
import org.springframework.data.redis.core.script.DefaultRedisScript
import org.springframework.stereotype.Component
import java.security.MessageDigest

interface PresenceStore {
    fun touch(uid: String, connectionId: String, serverId: String)
    fun remove(uid: String, connectionId: String)
    fun onlinePlayers(): Long
    fun connectionCount(uid: String): Long? = null // Unknown, never interpreted as zero.
    fun states(uids:List<String>):Map<String,com.teamfho.domino.social.SocialPresenceState> =
        uids.associateWith { com.teamfho.domino.social.SocialPresenceState.UNKNOWN }
    fun matchActivity(uid:String,version:String,active:Boolean) {}
}

// Atomic leases, not increment/decrement counters. Same UID on two devices counts once.
// Redis TIME is authoritative across server instances. Sorted-set scores provide expiry
// semantics even before Redis lazy key expiration runs; no keyspace notifications needed.
@Component
class RedisPresenceStore(private val redis: StringRedisTemplate, private val properties: RealtimeProperties) : PresenceStore {
    private val prefix = "domino:v1:{presence}:"
    private fun player(uid: String) = MessageDigest.getInstance("SHA-256").digest(uid.toByteArray())
        .joinToString("") { "%02x".format(it) }
    private val mutate = DefaultRedisScript<Long>("""
        local t = redis.call('TIME')
        local now = tonumber(t[1]) * 1000 + math.floor(tonumber(t[2]) / 1000)
        local ttl = tonumber(ARGV[3])
        local before = redis.call('ZCARD',KEYS[2]) > 0
        redis.call('ZREMRANGEBYSCORE', KEYS[2], '-inf', now)
        if ARGV[4] == 'touch' then
          redis.call('ZADD', KEYS[2], now + ttl, ARGV[2])
          redis.call('SET', KEYS[3], ARGV[5], 'PX', ttl)
        else
          redis.call('ZREM', KEYS[2], ARGV[2])
          redis.call('DEL', KEYS[3])
        end
        local latest = redis.call('ZREVRANGE', KEYS[2], 0, 0, 'WITHSCORES')
        if #latest > 0 then
          redis.call('ZADD', KEYS[1], latest[2], ARGV[1])
          redis.call('PEXPIRE', KEYS[2], math.max(1, tonumber(latest[2]) - now))
        else
          redis.call('ZREM', KEYS[1], ARGV[1])
          redis.call('DEL', KEYS[2])
        end
        local after = #latest > 0
        if before ~= after then
          redis.call('PUBLISH','domino:v1:social:presence-changed',cjson.encode({version=1,uid=ARGV[6]}))
        end
        return 1
    """.trimIndent(), Long::class.java)
    private val count = DefaultRedisScript<Long>("""
        local t = redis.call('TIME')
        local now = tonumber(t[1]) * 1000 + math.floor(tonumber(t[2]) / 1000)
        redis.call('ZREMRANGEBYSCORE', KEYS[1], '-inf', now)
        return redis.call('ZCARD', KEYS[1])
    """.trimIndent(), Long::class.java)
    override fun touch(uid: String, connectionId: String, serverId: String) = change(uid, connectionId, "touch", serverId)
    override fun remove(uid: String, connectionId: String) = change(uid, connectionId, "remove", "")
    private fun change(uid: String, id: String, operation: String, serverId: String) {
        val player = player(uid)
        redis.execute(mutate, listOf(prefix + "players", prefix + "player:" + player, prefix + "connection:" + id),
            player, id, (properties.presenceTtlSeconds * 1000).toString(), operation, serverId,uid)
    }
    override fun onlinePlayers(): Long = redis.execute(count, listOf(prefix + "players")) ?: 0
    private val playerCount=DefaultRedisScript<Long>("""
        local t=redis.call('TIME')
        local now=tonumber(t[1])*1000+math.floor(tonumber(t[2])/1000)
        redis.call('ZREMRANGEBYSCORE',KEYS[1],'-inf',now)
        return redis.call('ZCARD',KEYS[1])
    """.trimIndent(),Long::class.java)
    override fun connectionCount(uid: String): Long? = redis.execute(playerCount,listOf(prefix+"player:"+player(uid)))
    private val snapshot=DefaultRedisScript<List<*>>("""
        local t=redis.call('TIME')
        local now=tonumber(t[1])*1000+math.floor(tonumber(t[2])/1000)
        local states={}
        for i=1,#KEYS,2 do
          redis.call('ZREMRANGEBYSCORE',KEYS[i],'-inf',now)
          local state='OFFLINE'
          if redis.call('ZCARD',KEYS[i])>0 then
            state='ONLINE'
            if redis.call('HGET',KEYS[i+1],'active')=='true' then state='IN_MATCH' end
          end
          table.insert(states,state)
        end
        return states
    """.trimIndent(),List::class.java)
    override fun states(uids:List<String>):Map<String,com.teamfho.domino.social.SocialPresenceState> {
        require(uids.size<=50)
        if(uids.isEmpty())return emptyMap()
        val result=redis.execute(snapshot,uids.flatMap{listOf(prefix+"player:"+player(it),prefix+"activity:"+player(it))}) ?: error("PRESENCE_UNAVAILABLE")
        check(result.size==uids.size)
        return uids.zip(result.map{com.teamfho.domino.social.SocialPresenceState.valueOf(it as String)}).toMap()
    }
    private val activity=DefaultRedisScript<Long>("""
        local prior=redis.call('HGET',KEYS[1],'version')
        if prior and prior>ARGV[1] then return 0 end
        local previous=redis.call('HGET',KEYS[1],'active')
        redis.call('HSET',KEYS[1],'version',ARGV[1],'active',ARGV[2])
        redis.call('PEXPIRE',KEYS[1],120000)
        if previous~=ARGV[2] then
          redis.call('PUBLISH','domino:v1:social:presence-changed',cjson.encode({version=1,uid=ARGV[3]}))
        end
        return 1
    """.trimIndent(),Long::class.java)
    override fun matchActivity(uid:String,version:String,active:Boolean) {
        redis.execute(activity,listOf(prefix+"activity:"+player(uid)),version,active.toString(),uid)
    }
}
