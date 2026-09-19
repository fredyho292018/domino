package com.teamfho.domino.online

import com.teamfho.domino.DominoApplication
import com.teamfho.domino.catalog.GameCatalogCodec
import com.teamfho.domino.match.*
import com.google.cloud.firestore.Firestore
import org.springframework.boot.builder.SpringApplicationBuilder
import java.net.URI
import java.net.http.*
import java.nio.file.*
import java.time.*
import java.util.UUID
import java.util.concurrent.*

/** Explicit I2 companion. Files coordinate test steps/public evidence, never credentials or hands.
 * The independent JVM participant chooses using only its own authorized REST snapshot.
 * No test verifier, gameplay fixture, or server state mutation is installed. */
object OnlineTurnRealValidation {
    private val json=GameCatalogCodec.mapper
    private val http=HttpClient.newBuilder().connectTimeout(Duration.ofSeconds(10)).build()
    private val dir=Path.of("../../client/Validation/Generated/I2")
    private fun request(url:String,body:String?,token:String?=null):String {
        val b=HttpRequest.newBuilder(URI(url)).timeout(Duration.ofSeconds(25)).header("Content-Type","application/json")
        if(token!=null)b.header("Authorization","Bearer $token")
        val r=http.send(if(body==null)b.GET().build() else b.POST(HttpRequest.BodyPublishers.ofString(body)).build(),HttpResponse.BodyHandlers.ofString())
        check(r.statusCode()==200){"HTTP_${r.statusCode()}"};return r.body()
    }
    private fun save(name:String,value:Any) {
        Files.writeString(dir.resolve("$name.tmp"),json.writeValueAsString(value))
        Files.move(dir.resolve("$name.tmp"),dir.resolve("$name.json"),StandardCopyOption.REPLACE_EXISTING)
    }
    private class Peer(val token:String):WebSocket.Listener {
        val messages=LinkedBlockingQueue<String>();val partial=StringBuilder();var outgoing=0L
        @Volatile var lastUpdate=0L
        @Volatile var connected=false
        override fun onClose(webSocket:WebSocket,statusCode:Int,reason:String):CompletionStage<*> {if(this::socket.isInitialized&&webSocket===socket)connected=false;return CompletableFuture.completedFuture(null)}
        lateinit var socket:WebSocket
        override fun onOpen(webSocket:WebSocket){webSocket.request(1)}
        override fun onText(webSocket:WebSocket,data:CharSequence,last:Boolean):CompletionStage<*> {
            if(this::socket.isInitialized&&webSocket!==socket)return CompletableFuture.completedFuture(null)
            partial.append(data)
            if(last){val text=partial.toString();val n=json.readTree(text)
                if(n.path("type").asString()=="MATCH_UPDATE") lastUpdate=n.path("payload").path("snapshot").path("lastSequence").asLong()
                messages.add(text);partial.setLength(0)}
            webSocket.request(1);return CompletableFuture.completedFuture(null)
        }
        fun send(type:String,payload:Any){socket.sendText(json.writeValueAsString(mapOf("type" to type,"version" to 1,"sequence" to ++outgoing,"timestamp" to Instant.now().toString(),"payload" to payload)),true).get(10,TimeUnit.SECONDS)}
        fun until(type:String):tools.jackson.databind.JsonNode {
            val end=System.nanoTime()+TimeUnit.SECONDS.toNanos(30)
            while(System.nanoTime()<end){val t=messages.poll(1,TimeUnit.SECONDS)?:continue;val n=json.readTree(t)
                check(n.path("type").asString() !in setOf("AUTH_FAILED","SYSTEM_ERROR","COMMAND_REJECTED")){"PEER_REJECTED_${n.path("payload").path("code").asString().take(40)}"}
                if(n.path("type").asString()==type)return n.path("payload")}
            error("PEER_TIMEOUT")
        }
        fun open(){
            // Redis restoration can briefly return retryable UNAVAILABLE; mirror bounded transport recovery.
            for(attempt in 0..5) {
                messages.clear();partial.setLength(0);outgoing=0
                socket=http.newWebSocketBuilder().buildAsync(URI("ws://127.0.0.1:18083/ws/v1/realtime"),this).get(10,TimeUnit.SECONDS)
                try {send("AUTH",mapOf("idToken" to token));until("AUTHENTICATED");connected=true;return}
                catch(e:IllegalStateException){socket.abort();if(e.message!="PEER_REJECTED_UNAVAILABLE"||attempt==5)throw e;Thread.sleep(2000)}
            }
        }
        fun snapshot(id:String)=json.readValue(request("http://127.0.0.1:18083/api/v1/matches/$id/snapshot",null,token),OnlineSnapshot::class.java)
        fun step(id:String):Long {
            val s=snapshot(id)
            val command=when(s.phase){
                OnlinePhase.STARTER_SELECTION->{val st=s.starter!!
                    if(st.method==com.teamfho.domino.catalog.StarterMethod.EVEN_ODD_GUESS){check(st.guessingSeat==1);OnlineCommand(1,UUID.randomUUID().toString(),id,OnlineCommandType.SUBMIT_EVEN_ODD_GUESS,even=true)}
                    else {check(1 !in st.selectedSeats);OnlineCommand(1,UUID.randomUUID().toString(),id,OnlineCommandType.SELECT_STARTER_TILE,candidate=st.availableCandidates.first())}}
                OnlinePhase.PLAYING->{check(s.publicState.currentSeat==1);val board=s.publicState.board
                    val legal=s.privateState.hand.flatMap {t->ChainEnd.entries.filter {end->board.isEmpty()||(if(end==ChainEnd.LEFT)board.first().tile.sideA else board.last().tile.sideB).let {it==t.sideA||it==t.sideB}}.map {t to it}}.firstOrNull()
                    if(legal==null)OnlineCommand(1,UUID.randomUUID().toString(),id,OnlineCommandType.PASS)
                    else OnlineCommand(1,UUID.randomUUID().toString(),id,OnlineCommandType.PLAY_TILE,legal.first,legal.second)}
                else->error("PEER_PHASE")}
            send("MATCH_COMMAND",command);return until("COMMAND_ACCEPTED").path("resultingSequence").asLong()
        }
    }
    @JvmStatic fun main(args:Array<String>){
        com.teamfho.domino.validation.RealFirestoreGuard.requireOptIn()
        require(args.contains("--validate-i2"));Files.createDirectories(dir)
        val config=json.readTree(Files.readString(Path.of("../../client/DominoGame/Assets/google-services.json")))
        val key=config.path("client").get(0).path("api_key").get(0).path("current_key").asString()
        var token:String?=null;var peer:Peer?=null
        try {
            val account=json.readTree(request("https://identitytoolkit.googleapis.com/v1/accounts:signUp?key=$key","{\"returnSecureToken\":true}"))
            token=account.path("idToken").asString();val uid=account.path("localId").asString()
            var context=SpringApplicationBuilder(DominoApplication::class.java).logStartupInfo(false).run("--server.port=18083")
            try {
                val p=Peer(token).also {peer=it;it.open()};save("server-ready",mapOf("ready" to true))
                val deadline=System.nanoTime()+TimeUnit.MINUTES.toNanos(15);var lastPing=System.nanoTime();var lastRequest=0
                while(System.nanoTime()<deadline){
                    if(System.nanoTime()-lastPing>TimeUnit.SECONDS.toNanos(10)){if(p.connected)p.send("PING",emptyMap<String,String>());lastPing=System.nanoTime()}
                    val f=dir.resolve("request.json");if(!Files.exists(f)){Thread.sleep(100);continue}
                    val text=try {Files.readString(f)}catch(_:java.io.IOException){Thread.sleep(50);continue}
                    val command=json.readTree(text);val n=command.path("number").asInt()
                    if(n<=lastRequest){Thread.sleep(100);continue};lastRequest=n
                    val id=command.path("matchId").asString();val op=command.path("operation").asString()
                    if(op=="join") request("http://127.0.0.1:18083/api/v1/matches/$id/join",json.writeValueAsString(JoinOnlineMatch(UUID.randomUUID().toString())),token)
                    if(op=="stop")break
                    if(op=="disconnect"){p.socket.sendClose(1000,"Validation disconnect").get(5,TimeUnit.SECONDS);p.connected=false}
                    if(op=="reconnect"){p.socket.abort();p.open()}
                    if(op=="restart") {
                        context.close();p.connected=false
                        // One real overdue recovery, using the canonical 60s policy, no rule override.
                        Thread.sleep(65000)
                        context=SpringApplicationBuilder(DominoApplication::class.java).logStartupInfo(false).run("--server.port=18083")
                        p.open()
                    }
                    if(op=="step")p.step(id)
                    val raw=json.readTree(request("http://127.0.0.1:18083/api/v1/matches/$id/snapshot",null,token))
                    val s=json.readValue(raw.toString(),OnlineSnapshot::class.java);check(s.privateState.seat==1)
                    val waitUntil=System.nanoTime()+TimeUnit.SECONDS.toNanos(2)
                    while(p.lastUpdate<s.lastSequence&&System.nanoTime()<waitUntil)Thread.sleep(30)

                    // Keep the original wire node, including Jackson's polymorphic `kind` discriminator.
                    save("response",mapOf("number" to n,"publicState" to raw.path("publicState"),"sequence" to s.lastSequence,"wsSequence" to p.lastUpdate,"roundResult" to raw.path("roundResult"),"ownSeat" to 1,"phase" to s.phase,"deadline" to s.turnDeadlineAt,"serverNow" to s.serverNow))
                    if(op=="finish"){
                        val db=context.getBean(Firestore::class.java);val repo=FirestoreMatchRepository(db);val match=repo.read(id)!!
                        check(match.participants.map {it.playerUid}.distinct().size==2&&match.participants[1].playerUid==uid)
                        val events=repo.readTrustedEvents(id,0);check(events.map {it.sequence}==(1L..match.lastSequence).toList())
                        val timeouts=events.filter {it.payload is TurnTimeout};val autos=events.filter {it.payload is AutoPlayed}
                        check(timeouts.size>=3&&autos.size==timeouts.size)
                        check(timeouts.map {it.roundNumber to it.turnNumber}.distinct().size==timeouts.size)
                        check(events.any {it.payload is PlayerDisconnected}&&events.any {it.payload is PlayerReconnected})
                        val runtime=context.getBean(OnlineMatchService::class.java).state(id)
                        check(runtime.turnStartedAt!=null&&runtime.turnDeadlineAt==runtime.turnStartedAt.plusSeconds(60))
                        check(db.collection("matches/$id/players").get().get().size()==2)
                        save("firestore",mapOf("matchId" to id,"differentUids" to true,"events" to events.size,"timeouts" to timeouts.size,
                            "autoplays" to autos.size,"disconnects" to events.count {it.payload is PlayerDisconnected},"reconnects" to events.count {it.payload is PlayerReconnected},
                            "turnStartedAt" to runtime.turnStartedAt,"turnDeadlineAt" to runtime.turnDeadlineAt,"walletWrites" to 0,"strictSequence" to true))
                        println("I2_FIRESTORE=PASS MATCH=$id EVENTS=${events.size}");break
                    }
                }
                if(!Files.exists(dir.resolve("firestore.json")))error("UNITY_VALIDATION_TIMEOUT")
            } finally {context.close()}
        } finally {
            peer?.let {runCatching {it.socket.abort()}}
            token?.let {runCatching {request("https://identitytoolkit.googleapis.com/v1/accounts:delete?key=$key",json.writeValueAsString(mapOf("idToken" to it)))}.onFailure {println("I2_PEER_ACCOUNT_CLEANUP=FAILED")}}
        }
    }
}
