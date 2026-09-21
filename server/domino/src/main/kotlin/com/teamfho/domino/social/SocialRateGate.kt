package com.teamfho.domino.social

import io.micrometer.core.instrument.MeterRegistry
import java.util.concurrent.*
import java.util.concurrent.atomic.AtomicLong

enum class SocialOperationClass { READ, ABUSE_SENSITIVE_CREATION, DURABLE_LIMITED_MUTATION, SAFETY_MUTATION, PRIVACY_TIGHTENING, PRIVACY_LOOSENING, EPHEMERAL }
enum class FallbackBucket(val perMinute:Int,val burst:Int) { READ(12,4), SEND_ACCEPT(6,2), SAFETY(30,5) }
enum class SocialOperation(val kind:SocialOperationClass,val redisKey:String,val fallback:FallbackBucket?) {
    SEARCH(SocialOperationClass.ABUSE_SENSITIVE_CREATION,"NAME",null),
    CODE_LOOKUP(SocialOperationClass.ABUSE_SENSITIVE_CREATION,"FRIEND_CODE",null),
    PROFILE(SocialOperationClass.READ,"profile",FallbackBucket.READ),
    SETTINGS_READ(SocialOperationClass.READ,"settings",FallbackBucket.READ),
    SUMMARY(SocialOperationClass.READ,"summary",FallbackBucket.READ),
    BLOCK_LIST(SocialOperationClass.READ,"blocks",FallbackBucket.READ),
    REQUESTS(SocialOperationClass.READ,"requests",FallbackBucket.READ),
    FRIENDS(SocialOperationClass.READ,"friends",FallbackBucket.READ),
    FOLLOW_LIST(SocialOperationClass.READ,"followList",FallbackBucket.READ),
    SEND(SocialOperationClass.DURABLE_LIMITED_MUTATION,"friendSend",FallbackBucket.SEND_ACCEPT),
    ACCEPT(SocialOperationClass.DURABLE_LIMITED_MUTATION,"accept",FallbackBucket.SEND_ACCEPT),
    DECLINE(SocialOperationClass.SAFETY_MUTATION,"decline",FallbackBucket.SAFETY),
    CANCEL(SocialOperationClass.SAFETY_MUTATION,"cancel",FallbackBucket.SAFETY),
    UNFRIEND(SocialOperationClass.SAFETY_MUTATION,"unfriend",FallbackBucket.SAFETY),
    BLOCK(SocialOperationClass.SAFETY_MUTATION,"block",FallbackBucket.SAFETY),
    UNBLOCK(SocialOperationClass.SAFETY_MUTATION,"unblock",FallbackBucket.SAFETY),
    FOLLOW(SocialOperationClass.ABUSE_SENSITIVE_CREATION,"follow",null),
    UNFOLLOW(SocialOperationClass.SAFETY_MUTATION,"unfollow",FallbackBucket.SAFETY),
    PRIVACY_TIGHTEN(SocialOperationClass.PRIVACY_TIGHTENING,"settings",FallbackBucket.SAFETY),
    PRIVACY_LOOSEN(SocialOperationClass.PRIVACY_LOOSENING,"settings",null),
    PRIVACY_NOOP(SocialOperationClass.PRIVACY_TIGHTENING,"settings",FallbackBucket.SAFETY),
    // Local preflight protects the durable read needed to classify an untrusted patch.
    PRIVACY_INSPECT(SocialOperationClass.PRIVACY_TIGHTENING,"settings",FallbackBucket.SAFETY),
    EPHEMERAL(SocialOperationClass.EPHEMERAL,"ephemeral",null)
}
enum class RateDecision { ALLOW, DENY_429, UNAVAILABLE_503 }
fun interface SocialRateGate:AutoCloseable {
    fun decide(uid:String,operation:SocialOperation):RateDecision
    fun check(uid:String,operation:SocialOperation) {when(decide(uid,operation)) {
        RateDecision.ALLOW -> Unit
        RateDecision.DENY_429 -> throw SocialFailure("SOCIAL_ACTION_RATE_LIMITED",429)
        RateDecision.UNAVAILABLE_503 -> throw SocialFailure("SOCIAL_SERVICE_UNAVAILABLE",503)
    }}
    override fun close() {}
}
fun interface DistributedSocialGate { fun allowed(uid:String,operation:SocialOperation):Boolean }
fun interface SocialNanoClock {fun now():Long}

/** No eviction of active actors. Last-access order makes expiry incremental, without timers. */
class LocalSocialLimiter(private val clock:SocialNanoClock=SocialNanoClock(System::nanoTime),
    val capacity:Int=10000,val safetyReserve:Int=1000,val idleNanos:Long=TimeUnit.MINUTES.toNanos(5)) {
    private class Entry(var touched:Long) {
        val tokens=doubleArrayOf(4.0,2.0,5.0)
        val updated=LongArray(3){touched}
    }
    private val entries=LinkedHashMap<String,Entry>(16,0.75f,true)
    var capacityRejections=0L;private set
    init {require(capacity>0 && safetyReserve in 0..capacity && idleNanos>0)}
    @Synchronized fun decide(uid:String,bucket:FallbackBucket):RateDecision {
        val now=clock.now()
        val iterator=entries.entries.iterator()
        while(iterator.hasNext()) {val e=iterator.next();if(now-e.value.touched<idleNanos)break;iterator.remove()}
        // Fixed-size digest bounds retained key memory even for maximum-length Unicode UIDs.
        val key=java.util.Base64.getUrlEncoder().withoutPadding().encodeToString(
            java.security.MessageDigest.getInstance("SHA-256").digest(uid.toByteArray(Charsets.UTF_8)))
        val entry=entries[key]?:run {
            val bound=if(bucket==FallbackBucket.SAFETY)capacity else capacity-safetyReserve
            if(entries.size>=bound){capacityRejections++;return RateDecision.UNAVAILABLE_503}
            Entry(now).also{entries[key]=it}
        }
        entry.touched=now
        val i=bucket.ordinal
        val elapsed=(now-entry.updated[i]).coerceAtLeast(0).toDouble()/TimeUnit.MINUTES.toNanos(1)
        entry.tokens[i]=minOf(bucket.burst.toDouble(),entry.tokens[i]+elapsed*bucket.perMinute)
        entry.updated[i]=now
        if(entry.tokens[i]<1.0)return RateDecision.DENY_429
        entry.tokens[i]--;return RateDecision.ALLOW
    }
    @Synchronized fun size()=entries.size
}

/** One shared infrastructure circuit, never per UID. Completion tickets discard obsolete results. */
class SocialRedisCircuit(private val clock:SocialNanoClock=SocialNanoClock(System::nanoTime)) {
    enum class State { CLOSED, OPEN, HALF_OPEN }
    data class Ticket(val generation:Long)
    @Volatile var state=State.CLOSED;private set
    private var generation=0L;private var failures=0;private var until=0L
    var openSeconds=5L;private set
    private val log=org.slf4j.LoggerFactory.getLogger(javaClass)
    @Synchronized fun enter():Ticket? {
        if(state==State.HALF_OPEN)return null
        if(state==State.OPEN) {
            if(clock.now()<until)return null
            state=State.HALF_OPEN;log.info("SOCIAL_REDIS_CIRCUIT state=HALF_OPEN")
        }
        return Ticket(generation)
    }
    @Synchronized fun complete(ticket:Ticket,healthy:Boolean) {
        if(ticket.generation!=generation)return
        if(healthy) {
            failures=0
            if(state==State.HALF_OPEN){generation++;state=State.CLOSED;openSeconds=5;log.info("SOCIAL_REDIS_CIRCUIT state=CLOSED")}
        } else {
            if(state==State.HALF_OPEN)openSeconds=minOf(30,openSeconds*2)
            if(state==State.HALF_OPEN || ++failures>=3) {
                state=State.OPEN;generation++;until=clock.now()+TimeUnit.SECONDS.toNanos(openSeconds)
                log.warn("SOCIAL_REDIS_CIRCUIT state=OPEN durationSeconds={}",openSeconds)
            }
        }
    }
}

/** 200 ms CALLER budget, two workers, zero waiting-task queue. Timeout does not
 * pretend to cancel Redis work: occupied workers stay occupied until the native
 * command ends (existing Redis 1 s timeout). No replacement/unbounded tasks. */
class BoundedSocialRedis(private val delegate:DistributedSocialGate,val budgetMillis:Long=200):DistributedSocialGate,AutoCloseable {
    private val pool=ThreadPoolExecutor(0,2,30,TimeUnit.SECONDS,SynchronousQueue(),
        Thread.ofPlatform().daemon().name("social-redis-",0).factory(),ThreadPoolExecutor.AbortPolicy())
    override fun allowed(uid:String,operation:SocialOperation):Boolean {
        val pending=pool.submit<Boolean>{delegate.allowed(uid,operation)}
        try {return pending.get(budgetMillis,TimeUnit.MILLISECONDS)}
        catch(e:InterruptedException){Thread.currentThread().interrupt();throw e}
        finally {if(!pending.isDone)pending.cancel(false)} // bounded occupied worker; no new attempt
    }
    fun activeWorkers()=pool.activeCount
    fun queuedTasks()=pool.queue.size
    override fun close(){pool.shutdownNow()}
}

class ResilientSocialRateGate(private val distributed:DistributedSocialGate,
    val local:LocalSocialLimiter=LocalSocialLimiter(),val circuit:SocialRedisCircuit=SocialRedisCircuit(),
    private val metrics:MeterRegistry?=null):SocialRateGate {
    private val failures=AtomicLong()
    private val fallbackLogged=java.util.concurrent.atomic.AtomicBoolean()
    private val capacityLogged=java.util.concurrent.atomic.AtomicBoolean()
    private val log=org.slf4j.LoggerFactory.getLogger(javaClass)
    init {
        metrics?.gauge("social.local.limiter.entries",local){it.size().toDouble()}
        metrics?.gauge("social.redis.circuit.state",circuit){it.state.ordinal.toDouble()}
        metrics?.gauge("social.redis.failures",failures)
    }
    override fun decide(uid:String,operation:SocialOperation):RateDecision {
        require(uid.isNotBlank() && uid.length<=128)
        // Preserve unconditional safety Block; even full emergency storage cannot block it.
        if(operation==SocialOperation.BLOCK)return record(operation,RateDecision.ALLOW,"safety_bypass")
        if(operation==SocialOperation.PRIVACY_NOOP)return record(operation,RateDecision.ALLOW,"noop")
        if(operation==SocialOperation.PRIVACY_INSPECT)return record(operation,local.decide(uid,FallbackBucket.SAFETY),"privacy_preflight")
        val ticket=circuit.enter()
        if(ticket!=null)try {
            val allowed=distributed.allowed(uid,operation)
            circuit.complete(ticket,true) // a valid quota denial proves infrastructure is healthy
            if(circuit.state==SocialRedisCircuit.State.CLOSED)fallbackLogged.set(false)
            return record(operation,if(allowed)RateDecision.ALLOW else RateDecision.DENY_429,"distributed")
        } catch(_:Exception) {failures.incrementAndGet();circuit.complete(ticket,false)}
        if(fallbackLogged.compareAndSet(false,true))log.warn("SOCIAL_RATE_GATE fallback=ACTIVE")
        val bucket=operation.fallback?:return record(operation,RateDecision.UNAVAILABLE_503,"unavailable")
        // Privacy preflight already consumed the safety token; never double-charge one patch.
        val result=if(operation==SocialOperation.PRIVACY_TIGHTEN)RateDecision.ALLOW else local.decide(uid,bucket)
        return record(operation,result,"fallback")
    }
    private fun record(operation:SocialOperation,result:RateDecision,origin:String):RateDecision {
        metrics?.counter("social.rate.gate.decisions","result",result.name,"origin",origin,"operation_class",operation.kind.name)?.increment()
        if(origin=="fallback")metrics?.counter("social.rate.limit.fallback")?.increment()
        if(result==RateDecision.UNAVAILABLE_503 && (origin=="fallback" || origin=="privacy_preflight")) {
            metrics?.counter("social.local.limiter.capacity.rejections")?.increment()
            if(capacityLogged.compareAndSet(false,true))log.warn("SOCIAL_RATE_GATE localCapacity=EXHAUSTED")
        } else if(origin=="fallback" && result==RateDecision.ALLOW)capacityLogged.set(false)
        return result
    }
    override fun close(){(distributed as? AutoCloseable)?.close()}
}

object PrivacyDirection {
    fun classify(current:SocialPrivacySettings,patch:PrivacyPatch):SocialOperation {
        val next=patch.apply(current)
        if(next==current)return SocialOperation.PRIVACY_NOOP
        val loosens=(!current.discoverableByName && next.discoverableByName) ||
            next.friendRequests.ordinal<current.friendRequests.ordinal || next.follow.ordinal<current.follow.ordinal ||
            next.presenceVisibility.ordinal<current.presenceVisibility.ordinal || next.matchActivityVisibility.ordinal<current.matchActivityVisibility.ordinal
        return if(loosens)SocialOperation.PRIVACY_LOOSEN else SocialOperation.PRIVACY_TIGHTEN
    }
}
