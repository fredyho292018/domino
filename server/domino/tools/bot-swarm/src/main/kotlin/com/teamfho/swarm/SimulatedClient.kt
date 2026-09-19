package com.teamfho.swarm

import kotlinx.coroutines.*
import java.net.http.HttpClient
import tools.jackson.databind.JsonNode
import kotlin.random.Random

class SimulatedClient(private val config:Config,private val identity:Identity,val alias:String,private val http:HttpClient,private val metrics:Metrics,private val shouldRequeue:()->Boolean={true}) {
    @Volatile var state=ClientState.STARTING;private set
    @Volatile var matchId:String?=null;private set
    @Volatile var seat:Int?=null;private set
    private val random=Random(config.seed+identity.slot*7919)
    private val api=Api(http,config.baseUrl){identity.currentToken()}
    private val tracker=SequenceTracker()
    private var socket:Socket?=null
    private var queuedAt=0L
    private var pending:LogicalCommand?=null
    private var pendingAt=0L
    private var commandRetries=0
    private var planned:Map<String,Any>?=null
    private var plannedAt=Long.MAX_VALUE
    private var requeueAt=Long.MAX_VALUE
    private var lastProgress=now()
    private var lastStuckLog=now()
    private var failures=0
    private var authenticatedOnce=false
    private var sentCommands=0
    private var emulatorDropDone=false
    private val authRetry=ExpiredAuthRetry()
    private fun now()=System.nanoTime()/1_000_000
    private fun jitter(min:Long,max:Long)=if(min==max)min else random.nextLong(min,max+1)
    private fun status(next:ClientState){state=next}
    suspend fun run() {
        metrics.add("clientsStarted")
        try {
            delay(jitter(0,config.startupMaxMs))
            while(currentCoroutineContext().isActive) {
                try {
                    status(ClientState.AUTHENTICATING);identity.refresh()
                    api.call("POST","player/bootstrap",mapOf("language" to "es"))
                    api.call("PUT","player/display-name",mapOf("displayName" to alias))
                    Mode.catalog(api.call("GET","game-modes"),config.mode)
                    status(if(authenticatedOnce)ClientState.RECONNECTING else ClientState.CONNECTING)
                    val wire=Socket(http,config.baseUrl);socket=wire;wire.connect();wire.send("AUTH",mapOf("idToken" to identity.token))
                    val auth=withTimeout(15000){wire.messages.receive()}
                    if(auth.text("type")!="AUTHENTICATED")throw authFailure(auth)
                    metrics.add(if(authenticatedOnce)"reconnects" else "clientsConnected");authenticatedOnce=true
                    println("SWARM_CLIENT_CONNECTED alias=$alias")
                    val heartbeat=auth.path("payload").path("heartbeatIntervalSeconds").asLong()*1000
                    require(heartbeat in 5000..60000){"INVALID_HEARTBEAT"}
                    // Recovery precedes queue entry, including after backend/Redis loss.
                    recover()
                    loop(wire,heartbeat)
                    break
                }catch(e:CancellationException){throw e}
                catch(e:Exception) {
                    metrics.add("errors");failures++
                    val fatal=(e as? SafeFailure)?.fatal==true || e is IllegalArgumentException
                    println("SWARM_CLIENT_FAILED alias=$alias category="+(if(e is SafeFailure)e.category else "CLIENT_FAILURE"))
                    if(e is SafeFailure&&authRetry.allow(e.category)) {
                        closeSocket();metrics.add("expiredTokenRefreshes");continue
                    }
                    if(fatal||failures>=config.maxFailures){status(ClientState.FAILED);break}
                    metrics.add("disconnects");status(ClientState.RECONNECTING)
                    closeSocket();delay(jitter(500,minOf(15000L,1000L shl minOf(failures-1,4))))
                }
            }
        }finally {
            withContext(NonCancellable){
                withTimeoutOrNull(5000){try{if(matchId==null)api.call("DELETE","matchmaking/queue")}catch(_:Exception){}}
                withTimeoutOrNull(2000){closeSocket()}
            }
            identity.close();if(state!=ClientState.FAILED)status(ClientState.STOPPED)
        }
    }
    private suspend fun closeSocket(){try{socket?.close()}catch(_:Exception){};socket=null}
    private suspend fun recover(){applyQueue(api.call("GET","matchmaking/queue"));if(state==ClientState.IDLE)join()}
    private suspend fun join(){status(ClientState.JOINING_QUEUE);queuedAt=now();metrics.add("queueJoins");applyQueue(api.call("POST","matchmaking/queue",mapOf("modeKey" to config.mode)))}
    private suspend fun applyQueue(n:JsonNode) {
        when(n.text("state")) {
            "MATCHED" -> enter(n.path("match").text("matchId"))
            "QUEUED" -> status(ClientState.SEARCHING)
            "RESERVED" -> status(ClientState.JOINING_QUEUE)
            "NOT_QUEUED" -> status(ClientState.IDLE)
            else -> throw SafeFailure("QUEUE_REJECTED")
        }
    }
    private suspend fun enter(id:String) {
        if(matchId!=null&&matchId!=id)throw SafeFailure("ACTIVE_MATCH_CHANGED",true)
        if(matchId==null){metrics.add("matchesFound");if(queuedAt>0)metrics.recordWait(now()-queuedAt);status(ClientState.MATCH_FOUND);println("SWARM_MATCH_FOUND alias=$alias matchId=$id")}
        matchId=id;status(ClientState.ENTERING_MATCH);resync();metrics.started(id)
    }
    private suspend fun resync() {
        val id=matchId?:return
        val next=Projection(api.call("GET","matches/$id/snapshot"))
        require(next.mode.key==config.mode){"CROSS_MODE_ASSIGNMENT"}
        tracker.snapshot(next);seat=next.seat;metrics.add("resyncs");changed()
    }
    private fun changed() {
        val s=tracker.current?:return
        lastProgress=now();planned=s.intent();plannedAt=if(planned==null)Long.MAX_VALUE else now()+jitter(config.thinkMinMs,config.thinkMaxMs)
        if(s.phase=="MATCH_FINISHED") {
            if(state!=ClientState.MATCH_FINISHED&&state!=ClientState.REQUEUE_DELAY){metrics.finished(s.id);println("SWARM_MATCH_FINISHED alias=$alias matchId=${s.id}");requeueAt=now()+jitter(config.requeueMinMs,config.requeueMaxMs)}
            if(state!=ClientState.REQUEUE_DELAY)status(ClientState.MATCH_FINISHED)
            planned=null
        } else {status(ClientState.PLAYING);if(s.phase=="ROUND_FINISHED")metrics.round(s.id,s.public.number("currentRound"))}
    }
    private suspend fun message(n:JsonNode) {
        val p=n.path("payload")
        when(n.text("type")) {
            "MATCH_FOUND","MATCHMAKING_STATUS" -> applyQueue(p)
            "MATCH_UPDATE" -> if(matchId==p.text("matchId")) {
                val gaps=tracker.gaps
                if(tracker.update(p)){changed();for(e in p.path("events"))if(e.text("type")=="TURN_TIMEOUT")metrics.add("timeoutsObserved")}
                else if(tracker.gaps>gaps){metrics.add("sequenceGaps");resync()}
            }
            "COMMAND_ACCEPTED","COMMAND_REJECTED" -> if(p.text("commandId")==pending?.id) {
                val rejected=n.text("type")=="COMMAND_REJECTED"
                pending=null;commandRetries=0
                if(rejected){metrics.add("commandsRejected");resync()}
                else if(p.path("resultingSequence").asLong()>(tracker.current?.sequence?:0))resync()
            }
            "AUTH_FAILED" -> throw authFailure(n)
            "SYSTEM_ERROR" -> throw SafeFailure("WS_SYSTEM_ERROR")
        }
    }
    private suspend fun loop(wire:Socket,heartbeat:Long) {
        var pingAt=now()+heartbeat;var lastReceived=now()
        while(currentCoroutineContext().isActive) {
            val current=now()
            if(!emulatorDropDone&&identity.slot==0&&sentCommands>=10&&pending==null&&
                System.getenv("DOMINO_SWARM_EMULATOR_DROP_SOCKET_ONCE")=="true"&&ValidationTarget.emulator()) {
                emulatorDropDone=true
                throw SafeFailure("EMULATOR_SOCKET_DROP")
            }
            if(matchId==null&&!shouldRequeue())return
            if(current-lastReceived>45000)throw SafeFailure("HEARTBEAT_TIMEOUT")
            if(current>=pingAt){wire.send("PING");pingAt=current+heartbeat}
            if(current-lastProgress>90000&&current-lastStuckLog>60000&&state==ClientState.PLAYING){println("STUCK_MATCH_SUSPECTED matchId=$matchId sequence=${tracker.current?.sequence}");lastStuckLog=current}
            if(state==ClientState.MATCH_FINISHED) {
                // Authorized normal history endpoint; no Firestore scan.
                val history=api.call("GET","players/me/matches")
                if(history.path("items").any{it.text("matchId")==matchId})metrics.add("historyConfirmed")else throw SafeFailure("HISTORY_NOT_FOUND")
                status(ClientState.REQUEUE_DELAY)
            }
            if(state==ClientState.REQUEUE_DELAY&&current>=requeueAt) {
                if(!config.requeue||!shouldRequeue())return
                matchId=null;seat=null;tracker.clear();pending=null;planned=null;requeueAt=Long.MAX_VALUE;status(ClientState.IDLE);recover()
            }
            if(pending!=null&&current-pendingAt>=15000) {
                if(commandRetries++>=2)throw SafeFailure("COMMAND_ACK_TIMEOUT")
                // Retry the original payload and commandId, even if its response was lost.
                wire.send("MATCH_COMMAND",pending!!.body);pendingAt=current;metrics.add("commandRetries")
            }
            if(pending==null&&planned!=null&&current>=plannedAt) {
                pending=LogicalCommand.create(tracker.current!!,planned!!);planned=null;plannedAt=Long.MAX_VALUE
                wire.send("MATCH_COMMAND",pending!!.body);pendingAt=current;metrics.add("commandsSent");sentCommands++
            }
            val wake=minOf(pingAt,if(pending!=null)pendingAt+15000 else plannedAt,requeueAt)
            val next=withTimeoutOrNull((wake-now()).coerceIn(1,heartbeat)){wire.messages.receive()}
            if(next!=null){lastReceived=now();message(next)}
        }
    }
}
