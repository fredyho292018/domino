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

/** Explicit I1.1 companion. Files coordinate test steps/public evidence, never credentials or hands.
 * The independent JVM participant chooses using only its own authorized REST snapshot.
 * No test verifier, gameplay fixture, or server state mutation is installed. */
object OnlineUnityValidation {
    private val json=GameCatalogCodec.mapper
    private val http=HttpClient.newBuilder().connectTimeout(Duration.ofSeconds(10)).build()
    private val dir=Path.of("../../client/Validation/Generated/I11")
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
        lateinit var socket:WebSocket
        override fun onOpen(webSocket:WebSocket){webSocket.request(1)}
        override fun onText(webSocket:WebSocket,data:CharSequence,last:Boolean):CompletionStage<*> {
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
                check(n.path("type").asString() !in setOf("AUTH_FAILED","SYSTEM_ERROR","COMMAND_REJECTED")){"PEER_REJECTED"}
                if(n.path("type").asString()==type)return n.path("payload")}
            error("PEER_TIMEOUT")
        }
        fun open(){socket=http.newWebSocketBuilder().buildAsync(URI("ws://127.0.0.1:18083/ws/v1/realtime"),this).get(10,TimeUnit.SECONDS);send("AUTH",mapOf("idToken" to token));until("AUTHENTICATED")}
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
        require(args.contains("--validate-i11"));Files.createDirectories(dir)
        val config=json.readTree(Files.readString(Path.of("../../client/DominoGame/Assets/google-services.json")))
        val key=config.path("client").get(0).path("api_key").get(0).path("current_key").asString()
        var token:String?=null;var peer:Peer?=null
        try {
            val account=json.readTree(request("https://identitytoolkit.googleapis.com/v1/accounts:signUp?key=$key","{\"returnSecureToken\":true}"))
            token=account.path("idToken").asString();val uid=account.path("localId").asString()
            SpringApplicationBuilder(DominoApplication::class.java).logStartupInfo(false).run("--server.port=18083").use {context->
                val p=Peer(token).also {peer=it;it.open()};save("server-ready",mapOf("ready" to true))
                val deadline=System.nanoTime()+TimeUnit.MINUTES.toNanos(15);var lastPing=System.nanoTime();var lastRequest=0
                while(System.nanoTime()<deadline){
                    if(System.nanoTime()-lastPing>TimeUnit.SECONDS.toNanos(10)){p.send("PING",emptyMap<String,String>());lastPing=System.nanoTime()}
                    val f=dir.resolve("request.json");if(!Files.exists(f)){Thread.sleep(100);continue}
                    val text=try {Files.readString(f)}catch(_:java.io.IOException){Thread.sleep(50);continue}
                    val command=json.readTree(text);val n=command.path("number").asInt()
                    if(n<=lastRequest){Thread.sleep(100);continue};lastRequest=n
                    val id=command.path("matchId").asString();val op=command.path("operation").asString()
                    if(op=="join") request("http://127.0.0.1:18083/api/v1/matches/$id/join",json.writeValueAsString(JoinOnlineMatch(UUID.randomUUID().toString())),token)
                    if(op=="step")p.step(id)
                    val raw=json.readTree(request("http://127.0.0.1:18083/api/v1/matches/$id/snapshot",null,token))
                    val s=json.readValue(raw.toString(),OnlineSnapshot::class.java);check(s.privateState.seat==1)
                    val waitUntil=System.nanoTime()+TimeUnit.SECONDS.toNanos(10)
                    while(p.lastUpdate<s.lastSequence&&System.nanoTime()<waitUntil)Thread.sleep(30)
                    check(p.lastUpdate==s.lastSequence){"PEER_WS_SEQUENCE"}
                    // Keep the original wire node, including Jackson's polymorphic `kind` discriminator.
                    save("response",mapOf("number" to n,"publicState" to raw.path("publicState"),"sequence" to s.lastSequence,"wsSequence" to p.lastUpdate,"roundResult" to raw.path("roundResult"),"ownSeat" to 1))
                    if(op=="finish"){
                        check(s.phase in setOf(OnlinePhase.ROUND_FINISHED,OnlinePhase.MATCH_FINISHED))
                        val db=context.getBean(Firestore::class.java);val repo=FirestoreMatchRepository(db);val match=repo.read(id)!!
                        check(match.participants.map {it.playerUid}.distinct().size==2&&match.participants[1].playerUid==uid)
                        val events=repo.readTrustedEvents(id,0);check(events.map {it.sequence}==(1L..match.lastSequence).toList())
                        check(db.collection("matches/$id/players").get().get().size()==2)
                        check(db.collection("matches/$id/rounds").get().get().size()>=1)
                        val page=json.readValue(request("http://127.0.0.1:18083/api/v1/matches/$id/events?afterSequence=0",null,token),OnlineEventPage::class.java)
                        page.events.mapNotNull {it.event?.payload as? HandDealt}.forEach {check(it.seat==1)}
                        save("firestore",mapOf("matchId" to id,"differentUids" to true,"events" to events.size,"players" to 2,"rounds" to 1,"walletWrites" to 0,"history" to "MATCH_HISTORY_NOT_COMPLETED","roundResult" to s.roundResult))
                        println("I11_FIRESTORE=PASS MATCH=$id EVENTS=${events.size}");return@use
                    }
                }
                if(!Files.exists(dir.resolve("firestore.json")))error("UNITY_VALIDATION_TIMEOUT")
            }
        } finally {
            peer?.let {runCatching {it.socket.abort()}}
            token?.let {runCatching {request("https://identitytoolkit.googleapis.com/v1/accounts:delete?key=$key",json.writeValueAsString(mapOf("idToken" to it)))}.onFailure {println("I11_PEER_ACCOUNT_CLEANUP=FAILED")}}
        }
    }
}
