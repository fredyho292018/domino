package com.teamfho.domino.realtime

import java.lang.management.ManagementFactory
import java.security.MessageDigest
import java.util.ArrayDeque
import javax.management.ObjectName
import org.springframework.stereotype.Component
import tools.jackson.databind.json.JsonMapper

/** Local JVM management only: no HTTP endpoint, protocol fields or metric labels. */
interface AckPhaseTimingMBean {
    fun start(seconds: Int)
    fun stop()
    fun drain(): String
    fun configuration():String
}

@Component
class AckPhaseTiming(private val context:org.springframework.context.ApplicationContext) : AckPhaseTimingMBean {
    init {
        val server=ManagementFactory.getPlatformMBeanServer()
        val name=ObjectName("com.teamfho.domino:type=AckPhaseTiming")
        if(!server.isRegistered(name))server.registerMBean(this,name)
    }
    override fun start(seconds:Int)=Recorder.start(seconds)
    override fun stop()=Recorder.stop()
    override fun drain():String {
        val pools=context.getBeansOfType(org.springframework.scheduling.concurrent.ThreadPoolTaskScheduler::class.java).mapValues {(_,s)->
            val e=s.scheduledThreadPoolExecutor
            mapOf("active" to e.activeCount,"queued" to e.queue.size,"poolSize" to e.poolSize,"completed" to e.completedTaskCount)
        }
        return JsonMapper.builder().build().writeValueAsString(mapOf("batch" to JsonMapper.builder().build().readTree(Recorder.drain()),"scheduledExecutors" to pools))
    }
    override fun configuration():String {
        val db=context.getBeanProvider(com.google.cloud.firestore.Firestore::class.java).ifAvailable
        val options=db?.options
        val channel=options?.transportChannelProvider
        val pool=if(channel is com.google.api.gax.grpc.InstantiatingGrpcChannelProvider)channel.channelPoolSettings.toString() else "NOT_AVAILABLE"
        return JsonMapper.builder().build().writeValueAsString(mapOf("channelProvider" to (channel?.javaClass?.name?:"NOT_AVAILABLE"),
            "channelPoolSettings" to pool,"serviceRetrySettings" to (options?.retrySettings?.toString()?:"NOT_AVAILABLE"),
            "transactionAttempts" to 8,"preReadTimeoutSeconds" to 15,"transactionFutureTimeout" to "SDK_MANAGED_NO_EXPLICIT_FUTURE_TIMEOUT",
            "availableProcessors" to Runtime.getRuntime().availableProcessors(),
            "project" to (options?.projectId?:"NOT_AVAILABLE"),"database" to (options?.databaseId?:"NOT_AVAILABLE"),
            "clientImplementation" to (db?.javaClass?.name?:"NOT_AVAILABLE"),
            "credentialsMechanism" to (options?.credentials?.javaClass?.name?:"NOT_AVAILABLE"),
            "executorProvider" to "NOT_EXPOSED_BY_FIRESTORE_OPTIONS"))
    }

    companion object {
        val current=ThreadLocal<AckTrace?>()
        fun begin(at:Long):AckTrace?=if(Recorder.enabled())AckTrace(at)else null
    }
}

/** A bounded opt-in session. Expiration disables new traces without changing application config. */
internal object Recorder {
    private const val CAPACITY=20000
    private var deadline=0L
    private var generation=0L
    private val rows=ArrayDeque<Map<String,Any>>()
    private var dropped=0L
    private val json=JsonMapper.builder().build()
    @Synchronized fun start(seconds:Int) {
        require(seconds in 1..3600)
        check(deadline==0L || System.nanoTime()>=deadline) {"TIMING_SESSION_ACTIVE"}
        check(rows.isEmpty()) {"DRAIN_PREVIOUS_SESSION"}
        generation++;dropped=0;deadline=System.nanoTime()+seconds*1_000_000_000L
    }
    @Synchronized fun stop(){deadline=0}
    @Synchronized fun enabled()=deadline!=0L && System.nanoTime()<deadline
    @Synchronized fun generation()=generation
    @Synchronized fun record(session:Long,row:Map<String,Any>) {
        if(session!=generation)return
        if(rows.size>=CAPACITY){dropped++;return}
        rows.addLast(row)
    }
    @Synchronized fun drain():String {
        val result=json.writeValueAsString(mapOf("records" to rows.toList(),"dropped" to dropped,"active" to enabled()))
        rows.clear();return result
    }
}

/** Sequential outer phases partition total. Domain callbacks run on Firestore threads;
 * their summed duration is subtracted from inclusive transaction time, never counted twice. */
class AckTrace(private val start:Long,private val clock:()->Long=System::nanoTime,
    private val sink:(Map<String,Any>)->Unit={row->Recorder.record(Recorder.generation(),row)}) {
    private val session=Recorder.generation()
    val firestore=FirestorePhaseTiming(clock)
    private var phase="authorization"
    private var at=start
    private val times=linkedMapOf<String,Long>()
    private var domain=0L
    private var attempts=0
    private var invalid=false
    private var ended=false
    private var queuedAt:Long?=null
    private var selectedAt:Long?=null
    private var command=""
    private var type="UNKNOWN"
    @Synchronized fun identify(id:String,kind:String) {
        command=MessageDigest.getInstance("SHA-256").digest(id.toByteArray()).joinToString(""){"%02x".format(it)}
        type=if(kind in TYPES)kind else "UNKNOWN"
    }
    @Synchronized fun mark(next:String) {
        if(ended)return
        val now=clock();val elapsed=now-at
        val allowed=when(phase){"authorization"->setOf("lookup","firestore");"lookup"->setOf("authorization");"firestore"->setOf("postCommit");"postCommit"->setOf("ackEmission");else->emptySet()}
        if(elapsed<0 || next !in allowed)invalid=true
        times[phase]=(times[phase]?:0)+elapsed.coerceAtLeast(0)
        phase=next;at=now
    }
    @Synchronized fun attempt(){attempts++}
    @Synchronized fun enqueued(){queuedAt=clock()}
    @Synchronized fun selected(){selectedAt=clock()}
    fun <T> domain(block:()->T):T {
        val began=clock()
        try{return firestore.domain(block)}finally{synchronized(this){val d=clock()-began;if(d<0)invalid=true else domain+=d}}
    }
    @Synchronized fun finish() {
        if(ended)return
        val now=clock();val d=now-at
        if(d<0)invalid=true
        times[phase]=(times[phase]?:0)+d.coerceAtLeast(0);ended=true
        val total=now-start
        val transaction=times["firestore"]?:0
        val complete=!invalid && command.isNotEmpty() && PHASES.all{times.containsKey(it)} && domain<=transaction && times.values.sum()==total
        val phases=times.toMutableMap();phases["firestore"]=(transaction-domain).coerceAtLeast(0);phases["domain"]=domain
        val row=mapOf("correlation" to command,"commandType" to type,"complete" to complete,
            "firestoreDetail" to firestore.snapshot(),
            "phasesNanos" to phases,"serverTotalNanos" to total.coerceAtLeast(0),
            "transactionInclusiveNanos" to transaction,"attemptCount" to attempts,"retryCount" to (attempts-1).coerceAtLeast(0),
            "outboundQueueNanos" to if(queuedAt!=null&&selectedAt!=null)(selectedAt!!-queuedAt!!).coerceAtLeast(0)else -1L,
            "completedEpochMillis" to System.currentTimeMillis())
        if(session==Recorder.generation())sink(row)
    }
    companion object {
        val PHASES=setOf("authorization","lookup","firestore","postCommit","ackEmission")
        val TYPES=setOf("PLAY_TILE","PASS","SELECT_STARTER_TILE","SUBMIT_EVEN_ODD_GUESS","NEXT_ROUND")
    }
}
