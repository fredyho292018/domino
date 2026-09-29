package com.teamfho.swarm

import kotlinx.coroutines.*
import java.net.http.HttpClient
import java.nio.file.*
import java.time.Duration
import java.lang.management.ManagementFactory
import java.util.concurrent.atomic.AtomicReference

// Validation entry point only: no queue, recovery, display-name, match or gameplay calls.
interface S721Port {
    suspend fun auth()
    suspend fun bootstrap()
    suspend fun connect()
    suspend fun ping()
    suspend fun receive():String
    suspend fun close()
}
class S721Failure(val safe:String):RuntimeException(safe)
fun s721Category(e:Throwable,phase:String):String {
    val chain=generateSequence(e){it.cause}.take(8).toList()
    if(e is S721Failure)return e.safe
    val safe=chain.filterIsInstance<SafeFailure>().firstOrNull()?.category.orEmpty()
    return when {
        safe.startsWith("WS_CLOSED_")->"SERVER_CLOSE"
        safe=="AUTH_TOKEN_EXPIRED"->"TOKEN"
        safe=="WS_AUTH_REJECTED"->"WS_AUTH"
        safe=="FIREBASE_REFRESH_FAILED"->"AUTH"
        chain.any{it is java.net.UnknownHostException || it is java.nio.channels.UnresolvedAddressException}->"DNS"
        chain.any{it is javax.net.ssl.SSLException}->"TLS"
        chain.any{it is java.net.ConnectException}->"TCP"
        chain.any{it is java.net.http.WebSocketHandshakeException}->"WS_HANDSHAKE"
        (e is TimeoutCancellationException || chain.any{it is java.net.http.HttpTimeoutException}) && phase=="BOOTSTRAP"->"BOOTSTRAP_TIMEOUT"
        safe.startsWith("HTTP_") && phase=="BOOTSTRAP"->"BOOTSTRAP_HTTP"
        phase=="AUTH"->"AUTH"
        phase=="WS_AUTH"->"WS_AUTH"
        phase=="WS_CONNECT"->"WS_HANDSHAKE"
        else->"CLIENT_EXCEPTION"
    }
}
class S721Client(val slot:Int,private val port:S721Port,private val emit:(Map<String,Any>)->Unit,
    private val retryDelay:Long=5000,private val heartbeatMs:Long=10000,private val pongTimeoutMs:Long=30000) {
    @Volatile var state="NOT_STARTED";private set
    @Volatile var active=false;private set
    @Volatile var stable=false;private set
    @Volatile var attempts=0;private set
    @Volatile var terminal=false;private set
    private fun event(next:String,extra:Map<String,Any> = emptyMap()) {
        state=next;emit(mapOf("kind" to "client","slot" to slot,"state" to next,"attempt" to attempts)+extra)
    }
    suspend fun run() {
        try {
            for(attempt in 1..2) {
                attempts=attempt;var phase="AUTH";var connected=false
                try {
                    emit(mapOf("kind" to "attempt","slot" to slot,"attempt" to attempt,"phase" to phase))
                    port.auth();event("AUTH_SUCCESS")
                    phase="BOOTSTRAP";event("BOOTSTRAP_START");val began=System.nanoTime()
                    port.bootstrap();event("BOOTSTRAP_SUCCESS",mapOf("elapsedMs" to (System.nanoTime()-began)/1_000_000))
                    phase="WS_CONNECT";event("WS_CONNECT_START");port.connect()
                    phase="WS_AUTH";val first=withTimeout(15000){port.receive()}
                    if(first!="AUTHENTICATED")throw S721Failure("WS_AUTH")
                    connected=true;active=true;event("WS_CONNECTED")
                    phase="HEARTBEAT"
                    while(currentCoroutineContext().isActive) {
                        val ping=System.nanoTime();port.ping()
                        withTimeout(pongTimeoutMs) {
                            while(true) {
                                when(port.receive()) {
                                    "PONG"->break
                                    "PRESENCE_READY"->Unit
                                    "AUTH_FAILED"->throw S721Failure("WS_AUTH")
                                    "MATCH_FOUND","MATCH_EVENT"->throw S721Failure("AUTHORIZATION_VIOLATION")
                                    else->throw S721Failure("OTHER")
                                }
                            }
                        }
                        stable=true;event("WS_STABLE",mapOf("heartbeatMs" to (System.nanoTime()-ping)/1_000_000))
                        delay(heartbeatMs)
                    }
                } catch(e:Exception) {
                    if(e is CancellationException && e !is TimeoutCancellationException)throw e
                    val integrity=(e is S721Failure && e.safe=="AUTHORIZATION_VIOLATION") ||
                        (e is IllegalArgumentException && e.message in setOf("IDENTITY_CHANGED","FIREBASE_PROJECT_MISMATCH","IDENTITY_SCOPE_MISMATCH"))
                    val cat=if(integrity)"OTHER" else s721Category(e,phase)
                    event(if(connected)"DISCONNECTED" else when(phase){"AUTH"->"FAILED_AUTH";"BOOTSTRAP"->"FAILED_BOOTSTRAP";else->"FAILED_WS"},
                        mapOf("category" to cat,"phase" to phase,"heartbeatFailure" to (phase=="HEARTBEAT"),"closeCode" to ((e as? SafeFailure)?.category?.removePrefix("WS_CLOSED_")?.toIntOrNull() ?: 0)))
                    if(integrity)throw S721Failure("AUTHORIZATION_VIOLATION")
                    if((e is SafeFailure && e.fatal) || cat in setOf("TOKEN","WS_AUTH","SERVER_CLOSE","OTHER"))break
                } finally {
                    active=false;stable=false
                    withContext(NonCancellable){withTimeoutOrNull(2000){port.close()}}
                }
                if(attempt<2)delay(retryDelay)
            }
            terminal=true
        } catch(e:CancellationException) {
            event("STOPPED",mapOf("category" to "CANCELLED_BY_GLOBAL_STOP"));throw e
        }
    }
}
suspend fun s721Isolated(clients:List<suspend ()->Unit>,emergency:(String)->Unit)=supervisorScope {
    clients.map{ action->launch {
        try{action()}catch(e:CancellationException){throw e}
        catch(e:Exception){if(e is S721Failure && e.safe=="AUTHORIZATION_VIOLATION")emergency(e.safe)}
    }}.joinAll()
}
private class S721Real(val identity:Identity,val http:HttpClient,val base:String):S721Port {
    var socket:Socket?=null
    override suspend fun auth(){identity.refresh()}
    override suspend fun bootstrap(){Api(http,base){identity.token}.call("POST","player/bootstrap",mapOf("language" to "es"))}
    override suspend fun connect(){socket=Socket(http,base);socket!!.connect();socket!!.send("AUTH",mapOf("idToken" to identity.token))}
    override suspend fun ping(){socket!!.send("PING")}
    override suspend fun receive():String {val n=socket!!.messages.receive();socket!!.receivedAt(n);return n.text("type")}
    override suspend fun close(){socket?.close();socket=null}
}
fun main(args:Array<String>)=runBlocking {
    if(args.size!=1)throw IllegalArgumentException("PLAN_REQUIRED")
    val plan=Json.read(Files.readString(Path.of(args[0])))
    val base=plan.text("baseUrl");require(base=="https://domino-api-test.teamfho.com")
    val stop=Path.of(plan.text("stopFile"));require(!Files.exists(stop))
    val started=System.nanoTime();val emergency=AtomicReference<String?>(null)
    val emit:(Map<String,Any>)->Unit={data->synchronized(System.out){println(Json.write(data+mapOf("timestampMs" to System.currentTimeMillis(),"elapsedMs" to (System.nanoTime()-started)/1_000_000)))}}
    val http=HttpClient.newBuilder().connectTimeout(Duration.ofSeconds(15)).build()
    val identities=mutableListOf<Identity>();val clients=mutableListOf<S721Client>()
    try {
        for(g in 1..10) {
            val env=System.getenv().toMutableMap();env["DOMINO_SWARM_IDENTITIES_DIR"]=plan.path("groups")[g-1].text("directory")
            val settings=IdentitySettings(env);val config=Config(clients=20,environment="TEST",baseUrl=base).validate(base)
            for(s in 0..19){settings.validateSavedScope(config,s);val identity=Identity(settings,config,s,http);identities.add(identity);clients.add(S721Client((g-1)*20+s+1,S721Real(identity,http,base),emit))}
        }
        emit(mapOf("kind" to "ready","identities" to clients.size,"mode" to "CONNECT_ONLY"))
        val workers=launch {
            s721Isolated(clients.mapIndexed{i,c->{delay((i/20)*30000L+(i%20)*1500L);c.run()}},{emergency.set(it)})
        }
        var holdStart:Long?=null;var stableStart:Long?=null;var bestStableMs=0L
        while(true) {
            val elapsed=(System.nanoTime()-started)/1_000_000
            val active=clients.count{it.active};val stable=clients.count{it.stable}
            if(stable==200){if(stableStart==null)stableStart=elapsed;bestStableMs=maxOf(bestStableMs,elapsed-stableStart!!)}else stableStart=null
            if(holdStart==null && (stable==200 || (elapsed>=300000 && clients.all{it.active||it.terminal}) || elapsed>=490000))holdStart=elapsed
            val thread=ManagementFactory.getThreadMXBean();val gc=ManagementFactory.getGarbageCollectorMXBeans()
            emit(mapOf("kind" to "summary","active" to active,"stable" to stable,"attempted" to clients.count{it.attempts>0},"terminal" to clients.count{it.terminal},"stable200Ms" to bestStableMs,"threads" to thread.threadCount,"gcCount" to gc.sumOf{maxOf(0,it.collectionCount)},"gcMs" to gc.sumOf{maxOf(0,it.collectionTime)}))
            val reason=when {
                emergency.get()!=null->emergency.get()!!
                Files.exists(stop)->"GLOBAL_STOP"
                bestStableMs>=300000->"HOLD_200_COMPLETE"
                holdStart!=null && elapsed-holdStart!!>=300000 && stable!=200->"ACHIEVED_HOLD_COMPLETE"
                elapsed>=1100000->"BOUNDED_EXPERIMENT_END"
                else->null
            }
            if(reason!=null){emit(mapOf("kind" to "stop","reason" to reason));workers.cancelAndJoin();break}
            delay(1000)
        }
        emit(mapOf("kind" to "complete","active" to clients.count{it.active},"stable200Ms" to bestStableMs,"matchmaking" to false))
    } catch(e:Exception){emit(mapOf("kind" to "setupFailure","category" to "OTHER"));throw S721Failure("SETUP_FAILED")}
    finally{identities.forEach{runCatching{it.close()}};http.close()}
}
