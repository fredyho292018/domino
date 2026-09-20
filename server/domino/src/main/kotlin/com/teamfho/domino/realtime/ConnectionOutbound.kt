package com.teamfho.domino.realtime

import org.springframework.web.socket.WebSocketSession
import tools.jackson.databind.json.JsonMapper
import java.time.Instant
import java.util.ArrayDeque
import java.util.concurrent.CountDownLatch
import java.util.concurrent.ScheduledFuture
import java.util.concurrent.ScheduledThreadPoolExecutor
import java.util.concurrent.TimeUnit
import java.util.concurrent.locks.ReentrantLock
import kotlin.concurrent.withLock

enum class OutboundClass { CRITICAL, CONTROL, SOCIAL_EPHEMERAL }
enum class OfferResult { ENQUEUED, COALESCED, REJECTED_STALE, UNAUTHENTICATED, CLOSED }
data class OutboundLimits(val criticalMessages:Int=64, val criticalBytes:Int=256*1024,
    val controlMessages:Int=16, val controlBytes:Int=32*1024,
    val socialTargets:Int=50, val socialBytes:Int=16*1024, val socialFrameBytes:Int=2*1024,
    val frameBytes:Int=128*1024, val criticalBurst:Int=8, val sendMillis:Long=1000,
    val criticalAgeMillis:Long=2000, val controlWaitMillis:Long=20) {
    init { require(listOf(criticalMessages,criticalBytes,controlMessages,controlBytes,socialTargets,socialBytes,socialFrameBytes,frameBytes,criticalBurst).all{it>0})
        require(sendMillis>0 && criticalAgeMillis>0 && controlWaitMillis>0) }
}
data class OutboundSnapshot(val lifecycle:String,val counts:List<Int>,val bytes:List<Long>,
    val inFlight:Boolean,val sent:Long,val coalesced:Long,val rejected:Long,val stale:Boolean,
    val recoveryReady:Boolean,val waits:Long,val oldestCriticalMillis:Long)

/** One lazy virtual writer per admitted connection. Producers only encode and enqueue.
 * Queue accounting uses the exact UTF-8 JSON payload, excluding the fixed envelope.
 * The lock protects admission/selection; it is NEVER held across socket I/O.
 * Selection under this lock is the transmission commit point. Invalidation before
 * that point removes a candidate; after that point an in-flight frame cannot be recalled. */
class ConnectionOutbound(private val session:WebSocketSession,
    val limits:OutboundLimits=OutboundLimits(), private val cleaned:()->Unit={},
    private val metrics:OutboundMetrics?=null) {
    private data class Pending(val type:String,val json:String,val bytes:Int,val at:Long)
    private enum class Life { OPEN, DRAINING, CLOSING, CLOSED }
    private val lock=ReentrantLock()
    private val changed=lock.newCondition()
    private val critical=ArrayDeque<Pending>()
    private val control=ArrayDeque<Pending>()
    private val social=linkedMapOf<String,Pending>()
    private val bytes=LongArray(3)
    private val transport=OutboundTransport(session,limits.sendMillis)
    private var life=Life.OPEN
    private var authorized=false
    private var authSent=false
    private var inFlight=false
    private var sent=0L
    private var sendGeneration=0L
    private var burst=0
    private var coalesced=0L
    private var rejected=0L
    private var stale=false
    private var waits=0L
    private var drainCode=1000
    private var sendDeadline:ScheduledFuture<*>?=null
    private var ageDeadline:ScheduledFuture<*>?=null
    private val transportClosed=CountDownLatch(1)
    private val ended=CountDownLatch(1)
    private val writer=Thread.ofVirtual().name("domino-outbound").unstarted(::run)
    init { writer.start() }

    fun offerCritical(type:String,payload:Map<String,Any>)=offer(OutboundClass.CRITICAL,type,payload)
    fun offerControl(type:String,payload:Map<String,Any>)=offer(OutboundClass.CONTROL,type,payload)
    fun offerEphemeral(key:String,type:String,payload:Map<String,Any>):OfferResult {
        require(key.isNotEmpty() && key.length<=128)
        return offer(OutboundClass.SOCIAL_EPHEMERAL,type,payload,key)
    }
    private fun offer(kind:OutboundClass,type:String,payload:Map<String,Any>,key:String=""):OfferResult {
        require(type.matches(Regex("[A-Z_]{1,64}")))
        // Immutable encoded payload: later producer mutations cannot change size/content.
        val encoded=json.writeValueAsString(payload)
        val size=encoded.toByteArray(Charsets.UTF_8).size
        var fatal=false
        val result=lock.withLock {
            if(life!=Life.OPEN)return@withLock OfferResult.CLOSED
            if(kind!=OutboundClass.CONTROL && !authorized)return@withLock OfferResult.UNAUTHENTICATED
            val i=kind.ordinal
            val old=if(kind==OutboundClass.SOCIAL_EPHEMERAL)social[key] else null
            val count=when(kind){OutboundClass.CRITICAL->critical.size;OutboundClass.CONTROL->control.size;else->social.size}
            val maxCount=when(kind){OutboundClass.CRITICAL->limits.criticalMessages;OutboundClass.CONTROL->limits.controlMessages;else->limits.socialTargets}
            val maxBytes=when(kind){OutboundClass.CRITICAL->limits.criticalBytes;OutboundClass.CONTROL->limits.controlBytes;else->limits.socialBytes}
            val frameLimit=if(kind==OutboundClass.SOCIAL_EPHEMERAL)limits.socialFrameBytes else limits.frameBytes
            if(size>frameLimit || (old==null && count>=maxCount) || bytes[i]-(old?.bytes?:0)+size>maxBytes) {
                if(kind==OutboundClass.SOCIAL_EPHEMERAL) {
                    // Do not leave an older value queued after rejecting its replacement.
                    if(old!=null){social.remove(key);bytes[i]-=old.bytes;metrics?.queue(kind,-1,-old.bytes.toLong())}
                    stale=true;rejected++;metrics?.event("social_rejected")
                    changed.signalAll();return@withLock OfferResult.REJECTED_STALE
                }
                fatal=true;return@withLock OfferResult.CLOSED
            }
            val p=Pending(type,encoded,size,System.nanoTime())
            when(kind){OutboundClass.CRITICAL->critical.addLast(p);OutboundClass.CONTROL->control.addLast(p);else->social[key]=p}
            bytes[i]+=size-(old?.bytes?:0)
            metrics?.queue(kind,if(old==null)1 else 0,(size-(old?.bytes?:0)).toLong())
            if(type=="AUTHENTICATED" && kind==OutboundClass.CONTROL)authorized=true
            if(old!=null){coalesced++;metrics?.event("social_coalesced")}
            armAge();changed.signalAll()
            if(old==null)OfferResult.ENQUEUED else OfferResult.COALESCED
        }
        if(fatal)close(1013,"queue_pressure")
        return result
    }
    /** No Social protocol is implemented here; future callers supply their own Control cleanup. */
    fun invalidate(key:String)=lock.withLock {
        social.remove(key)?.let {bytes[2]-=it.bytes;metrics?.queue(OutboundClass.SOCIAL_EPHEMERAL,-1,-it.bytes.toLong())}
        changed.signalAll()
    }
    fun acknowledgeEphemeralRecovery():Boolean=lock.withLock {
        if(!stale || social.isNotEmpty() || inFlight || life!=Life.OPEN)false else {stale=false;true}
    }
    /** Existing protocol error is sent before close. Drain already-admitted non-ephemeral work. */
    fun finish(code:Int)=lock.withLock {
        if(life==Life.OPEN){life=Life.DRAINING;drainCode=code;clearSocial();changed.signalAll()}
    }
    fun close(code:Int=1000,reason:String="closed",remote:Boolean=false) = closeWhen(code,reason,remote){true}
    private fun closeWhen(code:Int,reason:String,remote:Boolean=false,eligible:()->Boolean) {
        val owned=lock.withLock {
            if(life==Life.CLOSING || life==Life.CLOSED || !eligible())false else {
                life=Life.CLOSING;clearQueues();sendDeadline?.cancel(false);ageDeadline?.cancel(false)
                sendDeadline=null;ageDeadline=null;changed.signalAll();true
            }
        }
        if(!owned)return
        metrics?.event(reason)
        if(reason!="closed")org.slf4j.LoggerFactory.getLogger(javaClass).warn("OUTBOUND_CLOSED category={}",reason)
        // At most one close task per admitted connection. No socket close under producer monitors.
        Thread.ofVirtual().name("domino-outbound-close").start {
            try {if(!remote)transport.close(code)}catch(_:Exception){}finally{transportClosed.countDown()}
        }
    }
    private fun armAge() {
        ageDeadline?.cancel(false);ageDeadline=null
        val p=critical.peekFirst()?:return
        val left=limits.criticalAgeMillis*1_000_000-(System.nanoTime()-p.at)
        ageDeadline=timers.schedule({
            closeWhen(1013,"critical_age") {critical.peekFirst()?.let{System.nanoTime()-it.at>=limits.criticalAgeMillis*1_000_000}==true}
        },left.coerceAtLeast(0),TimeUnit.NANOSECONDS)
    }
    private fun clearSocial(){metrics?.queue(OutboundClass.SOCIAL_EPHEMERAL,-social.size,-bytes[2]);social.clear();bytes[2]=0}
    private fun clearQueues(){metrics?.queue(OutboundClass.CRITICAL,-critical.size,-bytes[0]);metrics?.queue(OutboundClass.CONTROL,-control.size,-bytes[1]);critical.clear();control.clear();bytes[0]=0;bytes[1]=0;clearSocial()}
    private fun run() {
        try {
            while(true) {
                val pending=lock.withLock {
                    while(life==Life.OPEN && critical.isEmpty() && control.isEmpty() && social.isEmpty()) {waits++;changed.await()}
                    if(life==Life.CLOSING || life==Life.CLOSED)return@withLock null
                    if(life==Life.DRAINING && critical.isEmpty() && control.isEmpty())return@withLock null
                    val chooseControl=control.isNotEmpty() && (!authSent || critical.isEmpty() || burst>=limits.criticalBurst || System.nanoTime()-control.first.at>=limits.controlWaitMillis*1_000_000)
                    val kind=if(chooseControl)OutboundClass.CONTROL else if(critical.isNotEmpty() && authSent)OutboundClass.CRITICAL else if(control.isNotEmpty())OutboundClass.CONTROL else OutboundClass.SOCIAL_EPHEMERAL
                    val p=when(kind){OutboundClass.CRITICAL->{burst++;critical.removeFirst()};OutboundClass.CONTROL->{burst=0;control.removeFirst()};else->{val e=social.entries.first();social.remove(e.key)!!}}
                    bytes[kind.ordinal]-=p.bytes;metrics?.queue(kind,-1,-p.bytes.toLong());armAge()
                    inFlight=true
                    val generation=++sendGeneration
                    sendDeadline=timers.schedule({closeWhen(1013,"send_deadline"){inFlight && sendGeneration==generation}},limits.sendMillis,TimeUnit.MILLISECONDS)
                    p
                }?:break
                // Sequence is allocated only for selected frames; unsent coalesced states consume none.
                val frame="{\"type\":\"${pending.type}\",\"version\":1,\"sequence\":${sent+1},\"timestamp\":\"${Instant.now()}\",\"payload\":${pending.json}}"
                transport.send(frame)
                lock.withLock {
                    sent++;if(pending.type=="AUTHENTICATED")authSent=true
                    inFlight=false;sendDeadline?.cancel(false);sendDeadline=null;changed.signalAll()
                }
            }
        } catch(_:Exception) {close(1011,"send_failure")}
        finally {
            close(drainCode)
            transportClosed.await()
            try {cleaned()}catch(_:Exception){metrics?.event("cleanup_failure")}finally {
                lock.withLock {life=Life.CLOSED;inFlight=false;clearQueues();changed.signalAll()}
                ended.countDown()
            }
        }
    }
    fun isAccepting()=lock.withLock {life==Life.OPEN}
    fun snapshot()=lock.withLock {OutboundSnapshot(life.name,listOf(critical.size,control.size,social.size),bytes.toList(),inFlight,sent,coalesced,rejected,stale,stale&&social.isEmpty()&&!inFlight,waits,critical.peekFirst()?.let{(System.nanoTime()-it.at)/1_000_000}?:0)}
    internal fun awaitIdle(millis:Long=5000):Boolean=lock.withLock {
        var left=TimeUnit.MILLISECONDS.toNanos(millis)
        while((critical.isNotEmpty() || control.isNotEmpty() || social.isNotEmpty() || inFlight || life==Life.DRAINING || life==Life.CLOSING)&&left>0)left=changed.awaitNanos(left)
        left>0
    }
    internal fun awaitClosed(millis:Long=5000)=ended.await(millis,TimeUnit.MILLISECONDS)
    companion object {
        private val json=JsonMapper.builder().build()
        private val timers=ScheduledThreadPoolExecutor(2,Thread.ofPlatform().daemon().name("outbound-deadline-",0).factory()).apply{removeOnCancelPolicy=true}
        internal fun retainedDeadlines()=timers.queue.size
    }
}
