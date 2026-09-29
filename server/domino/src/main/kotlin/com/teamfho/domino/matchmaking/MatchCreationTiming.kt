package com.teamfho.domino.matchmaking

import java.lang.management.ManagementFactory
import java.security.MessageDigest
import javax.management.ObjectName
import org.springframework.stereotype.Component
import tools.jackson.databind.json.JsonMapper

interface MatchCreationTimingMBean {
    fun start(seconds:Int)
    fun stop()
    fun snapshot():String
}

/** Explicit local diagnostic session. No endpoint, credentials, state writes or SDK configuration. */
@Component
class MatchCreationTiming(private val context:org.springframework.context.ApplicationContext):MatchCreationTimingMBean {
    private var credentials:com.google.auth.oauth2.OAuth2Credentials?=null
    private val listener=com.google.auth.oauth2.OAuth2Credentials.CredentialsChangedListener { CreationRecorder.credentialChanged() }
    init {val server=ManagementFactory.getPlatformMBeanServer();val name=ObjectName("com.teamfho.domino:type=MatchCreationTiming");if(!server.isRegistered(name))server.registerMBean(this,name)}
    override fun start(seconds:Int) {
        CreationRecorder.start(seconds)
        credentials=context.getBeanProvider(com.google.cloud.firestore.Firestore::class.java).ifAvailable?.options?.credentials as? com.google.auth.oauth2.OAuth2Credentials
        credentials?.addChangeListener(listener)
    }
    override fun stop(){credentials?.removeChangeListener(listener);credentials=null;CreationRecorder.stop()}
    override fun snapshot():String=JsonMapper.builder().build().writeValueAsString(CreationRecorder.snapshot())
}

object CreationRecorder {
    val current=ThreadLocal<CreationTrace?>()
    private var deadline=0L
    private val joins=linkedMapOf<String,Long>()
    private val traces=linkedMapOf<String,CreationTrace>()
    private var dropped=0
    private var fsCount=0L
    private var lastFs=0L
    private var credentialCount=0L
    private var lastCredential=0L
    fun hash(value:String)=MessageDigest.getInstance("SHA-256").digest(value.toByteArray()).joinToString(""){"%02x".format(it)}
    @Synchronized fun enabled()=deadline!=0L&&System.nanoTime()<deadline
    @Synchronized fun start(seconds:Int){require(seconds in 1..1800);check(!enabled());joins.clear();traces.clear();dropped=0;fsCount=0;lastFs=0;credentialCount=0;lastCredential=0;deadline=System.nanoTime()+seconds*1_000_000_000L}
    @Synchronized fun stop(){deadline=0;joins.clear()}
    @Synchronized fun joined(uid:String){if(!enabled())return;val key=hash(uid);if(joins.size>=256 && key !in joins){dropped++;return};joins.putIfAbsent(key,System.nanoTime())}
    @Synchronized fun firestoreActivity(){if(enabled()){fsCount++;lastFs=System.nanoTime()}}
    @Synchronized fun credentialChanged(){if(enabled()){credentialCount++;lastCredential=System.nanoTime()}}
    @Synchronized fun context()=mapOf("fsActivityCount" to fsCount,"lastFsMonoNanos" to lastFs,"credentialChangeCount" to credentialCount,"lastCredentialMonoNanos" to lastCredential)
    @Synchronized fun begin(id:String,uids:List<String>):CreationTrace? {
        if(!enabled())return null
        val h=hash(id);if(h in traces)return traces[h]
        if(traces.size>=16){dropped++;return null}
        val times=uids.mapNotNull{joins.remove(hash(it))}
        return CreationTrace(h,times,uids.size,context()).also{traces[h]=it}
    }
    @Synchronized fun delivery(id:String,seat:Int):CreationDelivery?=if(enabled())traces[hash(id)]?.let{CreationDelivery(it,seat)}else null
    @Synchronized fun snapshot()=mapOf("enabled" to enabled(),"dropped" to dropped,"context" to context(),"records" to traces.values.map{it.snapshot()})
    fun <T> phase(name:String,block:()->T):T {val trace=current.get();return if(trace==null)block()else trace.phase(name,block)}
}

/** Scalar/hash-only bounded trace; clocks are injected in tests. */
class CreationTrace(val correlation:String,ready:List<Long>,private val required:Int,
    private val beforeContext:Map<String,Long>,private val clock:()->Long=System::nanoTime) {
    private val points=linkedMapOf<String,Long>()
    private val phases=linkedMapOf<String,Long>()
    private val counts=linkedMapOf<String,Int>()
    private val deliveries=linkedMapOf<Int,Map<String,Long>>()
    private var invalid=ready.size!=required
    private var afterContext=beforeContext
    private var classDelta=0L
    private var jitDelta=0L
    private val classBefore=ManagementFactory.getClassLoadingMXBean().totalLoadedClassCount
    private val jitBefore=ManagementFactory.getCompilationMXBean()?.totalCompilationTime?:-1
    init {if(ready.isNotEmpty()){points["M0_JOIN_RESPONSE_OBSERVED"]=ready.min();points["M1_LAST_JOIN_RESPONSE_OBSERVED"]=ready.max()};mark("M2_RESERVATION_OBSERVED")}
    fun mark(name:String){
        val at=clock();val ctx=if(name=="M5_PERSISTENCE_COMPLETE")CreationRecorder.context()else null
        synchronized(this){if(points.size>=40 && name !in points){invalid=true;return};points[name]=at
            if(ctx!=null){afterContext=ctx;classDelta=ManagementFactory.getClassLoadingMXBean().totalLoadedClassCount-classBefore;jitDelta=(ManagementFactory.getCompilationMXBean()?.totalCompilationTime?:-1)-jitBefore}}
    }
    @Synchronized fun markFirst(name:String){if(points.size>=40 && name !in points){invalid=true;return};points.putIfAbsent(name,clock())}
    @Synchronized fun measured(name:String,nanos:Long){if(nanos<0 || phases.size>=32 && name !in phases){invalid=true;return};phases[name]=(phases[name]?:0)+nanos;counts[name]=(counts[name]?:0)+1}
    fun <T> phase(name:String,block:()->T):T {val start=clock();try{return block()}finally{val end=clock();synchronized(this){if(end<start||phases.size>=32&&name !in phases)invalid=true else{phases[name]=(phases[name]?:0)+(end-start).coerceAtLeast(0);counts[name]=(counts[name]?:0)+1}}}}
    @Synchronized fun sent(seat:Int,start:Long,end:Long,epoch:Long,handedEpoch:Long=epoch){if(seat !in 0 until required || seat in deliveries || end<start){invalid=true;return};deliveries[seat]=mapOf("handedMonoNanos" to start,"sendReturnedMonoNanos" to end,"sendReturnedEpochMillis" to epoch,"handedEpochMillis" to handedEpoch)}
    @Synchronized fun snapshot():Map<String,Any> {
        val ordered=listOf("M0_JOIN_RESPONSE_OBSERVED","M1_LAST_JOIN_RESPONSE_OBSERVED","M2_RESERVATION_OBSERVED","M3_CREATION_START","M4_PERSISTENCE_START","M5_PERSISTENCE_COMPLETE","M8_PUBLICATION_START")
        val p=ordered.mapNotNull{points[it]};val readyComplete=ordered.all{it in points} && p.zipWithNext().all{(a,b)->b>=a}
        return mapOf("correlation" to correlation,"complete" to (!invalid&&readyComplete&&deliveries.size==required),"requiredPlayers" to required,
            "pointsNanos" to points.toMap(),"phasesNanos" to phases.toMap(),"phaseCounts" to counts.toMap(),"deliveries" to deliveries.toMap(),"contextBefore" to beforeContext,
            "contextAfter" to afterContext,"loadedClassesDelta" to classDelta,"compilationMillisDelta" to jitDelta)
    }
}

class CreationDelivery(private val trace:CreationTrace,private val seat:Int) {
    private var handed=0L
    private var handedEpoch=0L
    fun handed(){handed=System.nanoTime();handedEpoch=System.currentTimeMillis()}
    fun sent(){trace.sent(seat,handed,System.nanoTime(),System.currentTimeMillis(),handedEpoch)}
}
