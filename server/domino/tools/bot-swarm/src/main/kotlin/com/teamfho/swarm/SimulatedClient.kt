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
    private val api=Api(http,config.baseUrl,metrics){identity.currentToken()}
    private val tracker=SequenceTracker()
    private val correctness=CapacityCorrectness()
    private var correctnessCounts=correctness.counters()
    private fun reportCorrectness() {
        val next=correctness.counters()
        next.forEach{(key,value)->metrics.add(key,value-(correctnessCounts[key]?:0))}
        correctnessCounts=next
    }
    private var socket:Socket?=null
    private var queuedAt=0L
    private var pending:LogicalCommand?=null
    private var pendingAt=0L
    private var firstPendingAt=0L
    private var commandRetries=0
    private var planned:Map<String,Any>?=null
    private var plannedAt=Long.MAX_VALUE
    private var requeueAt=Long.MAX_VALUE
    private var lastProgress=now()
    private var lastStuckLog=now()
    private var failures=0
    private var authenticatedOnce=false
    private var countedSocket=false
    private val holdFile=System.getenv("DOMINO_SWARM_HOLD_UNTIL_FILE")?.let{java.nio.file.Path.of(it)}
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
                    wire.receivedAt(auth)
                    if(auth.text("type")!="AUTHENTICATED")throw authFailure(auth)
                    metrics.add(if(authenticatedOnce)"reconnects" else "clientsConnected");authenticatedOnce=true
                    metrics.add("activeAuthenticatedSockets");countedSocket=true
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
    private suspend fun closeSocket(){try{socket?.close()}catch(_:Exception){};socket=null;if(countedSocket){metrics.add("activeAuthenticatedSockets",-1);countedSocket=false}}
    private suspend fun recover(){applyQueue(api.call("GET","matchmaking/queue"));if(state==ClientState.IDLE)join()}
    private suspend fun join(){status(ClientState.JOINING_QUEUE);queuedAt=0;metrics.add("queueJoins");val accepted=api.call("POST","matchmaking/queue",mapOf("modeKey" to config.mode));queuedAt=now();applyQueue(accepted)}
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
            if(state!=ClientState.MATCH_FINISHED&&state!=ClientState.REQUEUE_DELAY){
                val cancelled=s.public.text("status")=="CANCELLED"
                require(cancelled||s.public.text("status")=="FINISHED")
                if(cancelled)metrics.cancelled(s.id) else metrics.finished(s.id)
                println((if(cancelled)"SWARM_MATCH_CANCELLED" else "SWARM_MATCH_FINISHED")+" alias=$alias matchId=${s.id}")
                requeueAt=now()+jitter(config.requeueMinMs,config.requeueMaxMs)
            }
            if(state!=ClientState.REQUEUE_DELAY)status(ClientState.MATCH_FINISHED)
            planned=null
        } else {status(ClientState.PLAYING);if(s.phase=="ROUND_FINISHED")metrics.round(s.id,s.public.number("currentRound"))}
    }
    private suspend fun message(n:JsonNode) {
        val p=n.path("payload")
        if(n.text("type")=="MATCH_UPDATE"&&matchId!=p.text("matchId")) {
            metrics.add("unauthorizedDeliveries");throw SafeFailure("CROSS_MATCH_DELIVERY",true)
        }
        if(n.text("type")=="MATCH_UPDATE") {
            for(e in p.path("events")) {
                val body=e.path("event")
                if(!body.isMissingNode&&!body.isNull)correctness.authorize(matchId?:"",body.text("matchId"),seat?:-1,body.text("visibility"),if(body.path("targetSeat").isNumber)body.number("targetSeat")else null)
            }
            val before=tracker.current?.sequence
            val after=p.path("snapshot").path("lastSequence").asLong()
            if(before!=null&&after<before)metrics.add("sequenceRegressions")
            if(before!=null&&after==before)metrics.add("duplicateEvents")
            reportCorrectness()
            if((correctnessCounts["unauthorizedDeliveries"]?:0)>0)throw SafeFailure("UNAUTHORIZED_DELIVERY",true)
        }
        if(n.text("type") in setOf("COMMAND_ACCEPTED","COMMAND_REJECTED")) {
            correctness.receipt(p.text("commandId"),p.path("resultingSequence").asLong(),n.text("type")=="COMMAND_ACCEPTED")
            reportCorrectness()
        }
        when(n.text("type")) {
            "MATCH_FOUND","MATCHMAKING_STATUS" -> applyQueue(p)
            "MATCH_UPDATE" -> if(matchId==p.text("matchId")) {
                val gaps=tracker.gaps
                if(tracker.update(p)){changed();for(e in p.path("events"))if(e.text("type")=="TURN_TIMEOUT")metrics.add("timeoutsObserved")}
                else if(tracker.gaps>gaps){metrics.add("sequenceGaps");resync()}
            }
            "COMMAND_ACCEPTED","COMMAND_REJECTED" -> if(p.text("commandId")==pending?.id) {
                val rejected=n.text("type")=="COMMAND_REJECTED"
                if(!rejected)metrics.recordAck(now()-firstPendingAt)
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
                if(tracker.current?.public?.text("status")=="FINISHED") {
                    val history=api.call("GET","players/me/matches")
                    if(history.path("items").any{it.text("matchId")==matchId})metrics.add("historyConfirmed")else throw SafeFailure("HISTORY_NOT_FOUND")
                }
                status(ClientState.REQUEUE_DELAY)
            }
            if(state==ClientState.REQUEUE_DELAY&&current>=requeueAt) {
                if(!config.requeue||!shouldRequeue()) {
                    // Capacity validation only: retain normal heartbeat after completion,
                    // without requeueing or issuing commands. Coordinator writes epoch deadline.
                    if(holdFile==null || (java.nio.file.Files.exists(holdFile) && System.currentTimeMillis()>=java.nio.file.Files.readString(holdFile).trim().toLong()))return
                    requeueAt=current+1000
                    continue
                }
                matchId=null;seat=null;tracker.clear();pending=null;planned=null;requeueAt=Long.MAX_VALUE;status(ClientState.IDLE);recover()
            }
            if(pending!=null&&current-pendingAt>=15000) {
                if(commandRetries++>=2)throw SafeFailure("COMMAND_ACK_TIMEOUT")
                // Retry the original payload and commandId, even if its response was lost.
                wire.send("MATCH_COMMAND",pending!!.body);pendingAt=current;metrics.add("commandRetries")
            }
            if(pending==null&&planned!=null&&current>=plannedAt) {
                pending=LogicalCommand.create(tracker.current!!,planned!!);planned=null;plannedAt=Long.MAX_VALUE
                firstPendingAt=now();wire.send("MATCH_COMMAND",pending!!.body);pendingAt=current;metrics.add("commandsSent");sentCommands++
            }
            val wake=minOf(pingAt,if(pending!=null)pendingAt+15000 else plannedAt,requeueAt)
            val next=withTimeoutOrNull((wake-now()).coerceIn(1,heartbeat)){wire.messages.receive()}
            if(next!=null){lastReceived=now();val began=wire.receivedAt(next);try{message(next)}finally{if(next.text("type")=="MATCH_UPDATE"&&began!=null)metrics.recordEventHandler((System.nanoTime()-began)/1_000_000)}}
        }
    }
}
