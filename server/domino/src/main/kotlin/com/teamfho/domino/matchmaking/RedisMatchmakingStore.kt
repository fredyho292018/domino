package com.teamfho.domino.matchmaking

import com.teamfho.domino.catalog.GameCatalogCodec
import com.teamfho.domino.match.MatchRuleSnapshot
import org.springframework.data.redis.core.StringRedisTemplate
import org.springframework.data.redis.core.script.DefaultRedisScript
import java.security.MessageDigest
import java.util.UUID

/** The shared presence hash tag keeps presence checks and queue claims in one Redis slot.
 * Lease TTL is 30s. An expired owner is replaced, never a reservation ID: creation retries
 * must target the same Firestore transaction/idempotency receipt before releasing members. */
class RedisMatchmakingStore(private val redis: StringRedisTemplate,
    private val prefix:String="domino:v1:{presence}:mm:",private val leaseMillis:Long=30000): MatchmakingStore {
    private fun hash(uid: String)=MessageDigest.getInstance("SHA-256").digest(uid.toByteArray()).joinToString(""){"%02x".format(it)}
    private val script=DefaultRedisScript<String>("""
        local p=KEYS[1]
        local op=ARGV[1]
        local t=redis.call('TIME');local now=tonumber(t[1])*1000+math.floor(tonumber(t[2])/1000)
        local function read(h) local s=redis.call('GET',p..'u:'..h);if s then return cjson.decode(s) end end
        local function present(h) return tonumber(redis.call('ZSCORE','domino:v1:{presence}:players',h) or '0')>now end
        local function save(h,e,ttl) redis.call('SET',p..'u:'..h,cjson.encode(e));if ttl then redis.call('PEXPIRE',p..'u:'..h,ttl) end end
        local function remove(h,e) if e then redis.call('ZREM',p..'q:'..e.key,h) end;redis.call('DEL',p..'u:'..h);redis.call('SREM',p..'members',h) end
        local function state(e) return cjson.encode({state=e and e.state or 'NOT_QUEUED'}) end
        if op=='join' or op=='leave' or op=='status' then
          local h=ARGV[2];local e=read(h)
          if op=='status' then return state(e) end
          local n=redis.call('INCR',p..'rate:'..h);if n==1 then redis.call('PEXPIRE',p..'rate:'..h,10000) end
          if n>20 then return '{"error":"RATE_LIMIT"}' end
          if op=='leave' then if e and e.state=='QUEUED' then remove(h,e);return state(nil) end;return state(e) end
          if e and (e.state=='QUEUED' or e.state=='RESERVED') then return state(e) end
          if not present(h) then return '{"error":"PRESENCE_REQUIRED"}' end
          e={uid=ARGV[3],key=ARGV[4],state='QUEUED',joinedAt=now}
          save(h,e,120000);redis.call('SADD',p..'members',h);redis.call('SADD',p..'queues',e.key)
          redis.call('ZADD',p..'q:'..e.key,redis.call('INCR',p..'order'),h)
          return state(e)
        end
        if op=='reserve' then
          local required=tonumber(ARGV[7] or '2')
          local q=p..'q:'..ARGV[2];local list=redis.call('ZRANGE',q,0,999);local pair={}
          for _,h in ipairs(list) do
            local e=read(h)
            if not e or e.state~='QUEUED' then redis.call('ZREM',q,h)
            elseif not present(h) then remove(h,e)
            else table.insert(pair,h);if #pair==required then break end end
          end
          if #pair<required then return '' end
          local a=read(pair[1]);local b=read(pair[2]);local id=ARGV[3]
          local job={id=id,uidA=a.uid,uidB=b.uid,key=ARGV[2],rules=cjson.decode(ARGV[5]),owner=ARGV[4],a=pair[1],b=pair[2],waitMillis=math.max(0,now-(a.joinedAt+b.joinedAt)/2)}
          job.members=pair;job.additionalUids={};local joined=0
          for i,h in ipairs(pair) do local e=read(h);joined=joined+e.joinedAt;if i>2 then table.insert(job.additionalUids,e.uid) end end
          job.waitMillis=math.max(0,now-joined/required)
          for _,h in ipairs(pair) do local e=read(h);e.state='RESERVED';e.reservation=id;save(h,e);redis.call('ZREM',q,h) end
          redis.call('SET',p..'job:'..id,cjson.encode(job));redis.call('SET',p..'lease:'..id,ARGV[4],'PX',ARGV[6])
          redis.call('ZADD',p..'jobs',now+tonumber(ARGV[6]),id);return cjson.encode(job)
        end
        if op=='recover' then
          local ids=redis.call('ZRANGEBYSCORE',p..'jobs','-inf',now,'LIMIT',0,1)
          if #ids==0 then return '' end
          local id=ids[1];local raw=redis.call('GET',p..'job:'..id)
          if not raw then redis.call('ZREM',p..'jobs',id);return '' end
          local job=cjson.decode(raw);job.owner=ARGV[2]
          redis.call('SET',p..'job:'..id,cjson.encode(job));redis.call('SET',p..'lease:'..id,ARGV[2],'PX',ARGV[3])
          redis.call('ZADD',p..'jobs',now+tonumber(ARGV[3]),id);return cjson.encode(job)
        end
        if op=='complete' or op=='failed' then
          local id=ARGV[2];if redis.call('GET',p..'lease:'..id)~=ARGV[3] then return '' end
          local raw=redis.call('GET',p..'job:'..id);if not raw then return '' end
          local job=cjson.decode(raw)
          for _,h in ipairs(job.members or {job.a,job.b}) do local e=read(h)
            if e and e.reservation==id then e.state=op=='complete' and 'MATCHED' or 'FAILED';save(h,e,60000);redis.call('SREM',p..'members',h) end
          end
          redis.call('DEL',p..'job:'..id,p..'lease:'..id);redis.call('ZREM',p..'jobs',id);return 'ok'
        end
        if op=='cleanup' then
          local removed={}
          for _,h in ipairs(redis.call('SMEMBERS',p..'members')) do local e=read(h)
            if not e then redis.call('SREM',p..'members',h)
            elseif e.state=='QUEUED' then
              if not present(h) then table.insert(removed,e.uid);remove(h,e) else redis.call('PEXPIRE',p..'u:'..h,120000) end
            end
          end
          for _,key in ipairs(redis.call('SMEMBERS',p..'queues')) do
            for _,h in ipairs(redis.call('ZRANGE',p..'q:'..key,0,-1)) do if not read(h) then redis.call('ZREM',p..'q:'..key,h) end end
          end
          return cjson.encode(removed)
        end
        if op=='waiting' then local n=0;for _,key in ipairs(redis.call('SMEMBERS',p..'queues')) do n=n+redis.call('ZCARD',p..'q:'..key) end;return tostring(n) end
        return ''
    """.trimIndent(),String::class.java)
    private fun call(vararg args: String)=redis.execute(script,listOf(prefix),*args)?:""
    private fun parseStatus(raw: String):QueueStatus {
        val node=GameCatalogCodec.mapper.readTree(raw)
        if(node.has("error"))throw MatchmakingFailure(node["error"].asText())
        return QueueStatus(QueueState.valueOf(node["state"].asText()))
    }
    private fun reservation(raw:String):PairReservation? {
        if(raw.isEmpty())return null
        val n=GameCatalogCodec.mapper.readTree(raw)
        return PairReservation(n["id"].asText(),n["uidA"].asText(),n["uidB"].asText(),n["key"].asText(),
            GameCatalogCodec.mapper.treeToValue(n["rules"],MatchRuleSnapshot::class.java),n["owner"].asText(),n["waitMillis"]?.asLong()?:0,
            n["additionalUids"]?.takeIf{it.isArray}?.let{a->(0 until a.size()).map{a[it].asText()}}?:emptyList())
    }
    override fun join(uid:String,key:MatchmakingKey)=parseStatus(call("join",hash(uid),uid,key.value))
    override fun leave(uid:String)=parseStatus(call("leave",hash(uid)))
    override fun status(uid:String)=parseStatus(call("status",hash(uid)))
    override fun reserve(key:MatchmakingKey,rules:MatchRuleSnapshot)=reservation(call("reserve",key.value,UUID.randomUUID().toString(),UUID.randomUUID().toString(),GameCatalogCodec.mapper.writeValueAsString(rules),leaseMillis.toString(),rules.mode().playerCount.toString()))
    override fun recover()=reservation(call("recover",UUID.randomUUID().toString(),leaseMillis.toString()))
    override fun complete(reservation:PairReservation)=call("complete",reservation.id,reservation.owner)=="ok"
    override fun failed(reservation:PairReservation){call("failed",reservation.id,reservation.owner)}
    override fun cleanup():List<String> {val n=GameCatalogCodec.mapper.readTree(call("cleanup"));return (0 until n.size()).map{n[it].asText()}}
    override fun waiting()=call("waiting").toLong()
}
