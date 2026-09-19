package com.teamfho.domino.online

import com.teamfho.domino.DominoApplication
import com.teamfho.domino.catalog.GameCatalogCodec
import com.teamfho.domino.match.*
import com.google.cloud.firestore.Firestore
import org.springframework.boot.builder.SpringApplicationBuilder
import java.net.URI
import java.net.http.*
import java.nio.file.Files
import java.nio.file.Path
import java.time.Duration
import java.time.Instant
import java.util.UUID
import java.util.concurrent.*

/** Explicit integration runner: two temporary Firebase anonymous identities, real local G3,
 * real Firestore synthetic Match only. Tokens stay in process memory. No wallet/bootstrap calls. */
object OnlineRealValidation {
    private val json=GameCatalogCodec.mapper
    private val http=HttpClient.newBuilder().connectTimeout(Duration.ofSeconds(10)).build()
    private fun request(url: String,body: String?,token: String?=null): String {
        var b=HttpRequest.newBuilder(URI(url)).timeout(Duration.ofSeconds(25)).header("Content-Type","application/json")
        if(token!=null)b=b.header("Authorization","Bearer $token")
        val r=http.send(if(body==null)b.GET().build()else b.POST(HttpRequest.BodyPublishers.ofString(body)).build(),HttpResponse.BodyHandlers.ofString())
        check(r.statusCode()==200){"VALIDATION_HTTP_STATUS_${r.statusCode()}"};return r.body()
    }
    private class Peer(val token: String): WebSocket.Listener {
        val messages=LinkedBlockingQueue<String>();val partial=StringBuilder();var sequence=0L
        lateinit var socket: WebSocket
        override fun onOpen(webSocket: WebSocket) {webSocket.request(1)}
        override fun onText(webSocket: WebSocket,data: CharSequence,last: Boolean): CompletionStage<*> {
            partial.append(data);if(last){messages.add(partial.toString());partial.setLength(0)};webSocket.request(1);return CompletableFuture.completedFuture(null)
        }
        fun send(type: String,payload: Any) {socket.sendText(json.writeValueAsString(mapOf("type" to type,"version" to 1,"sequence" to ++sequence,"timestamp" to Instant.now().toString(),"payload" to payload)),true).get(10,TimeUnit.SECONDS)}
        fun until(type: String): tools.jackson.databind.JsonNode {
            val end=System.nanoTime()+TimeUnit.SECONDS.toNanos(30)
            while(System.nanoTime()<end) {val text=messages.poll(1,TimeUnit.SECONDS)?:continue;val node=json.readTree(text)
                check(node.path("type").asString() !in setOf("SYSTEM_ERROR","AUTH_FAILED","COMMAND_REJECTED")){"REALTIME_VALIDATION_REJECTED"}
                if(node.path("type").asString()==type)return node.path("payload")
            };error("REALTIME_VALIDATION_TIMEOUT")
        }
        fun open(){socket=http.newWebSocketBuilder().buildAsync(URI("ws://127.0.0.1:18083/ws/v1/realtime"),this).get(10,TimeUnit.SECONDS);send("AUTH",mapOf("idToken" to token));until("AUTHENTICATED")}
        fun snapshot(id: String)=json.readValue(request("http://127.0.0.1:18083/api/v1/matches/$id/snapshot",null,token),OnlineSnapshot::class.java)
    }
    @JvmStatic fun main(args: Array<String>) {
        com.teamfho.domino.validation.RealFirestoreGuard.requireOptIn()
        require(args.contains("--validate-i1"))
        val config=json.readTree(Files.readString(Path.of("../../client/DominoGame/Assets/google-services.json")))
        val key=config.path("client").get(0).path("api_key").get(0).path("current_key").asString()
        val accounts=mutableListOf<Pair<String,String>>()
        val peers=mutableListOf<Peer>()
        try {
            repeat(2) {
                val response=json.readTree(request("https://identitytoolkit.googleapis.com/v1/accounts:signUp?key=$key","{\"returnSecureToken\":true}"))
                accounts+=response.path("localId").asString() to response.path("idToken").asString()
            }
            check(accounts.map {it.first}.distinct().size==2)
            SpringApplicationBuilder(DominoApplication::class.java).logStartupInfo(false).run("--server.port=18083").use {context->
                accounts.forEach {peers+=Peer(it.second).also(Peer::open)}
                val service=context.getBean(OnlineMatchService::class.java)
                val initial=service.create(accounts[0].first,"DUEL_1V1",true);val id=initial.publicState.matchId
                println("I1_VALIDATION_MATCH_ID=$id VALIDATION_DATA=true")
                request("http://127.0.0.1:18083/api/v1/matches/$id/join",json.writeValueAsString(JoinOnlineMatch("validation-join")),accounts[1].second)
                var actions=0;var lastPing=System.nanoTime()
                while(actions++<700) {
                    if(System.nanoTime()-lastPing>TimeUnit.SECONDS.toNanos(10)){peers.forEach {it.send("PING",emptyMap<String,String>())};lastPing=System.nanoTime()}
                    val views=peers.map {it.snapshot(id)};check(views[0].privateState.seat==0&&views[1].privateState.seat==1)
                    val s=views[0]
                    if(s.phase==OnlinePhase.MATCH_FINISHED)break
                    var seat=0
                    val command=when(s.phase) {
                        OnlinePhase.STARTER_SELECTION->{val st=s.starter!!
                            if(st.method==com.teamfho.domino.catalog.StarterMethod.EVEN_ODD_GUESS){seat=st.guessingSeat;OnlineCommand(1,UUID.randomUUID().toString(),id,OnlineCommandType.SUBMIT_EVEN_ODD_GUESS,even=true)}
                            else {seat=(0..1).first {it !in st.selectedSeats};OnlineCommand(1,UUID.randomUUID().toString(),id,OnlineCommandType.SELECT_STARTER_TILE,candidate=st.availableCandidates.first())}}
                        OnlinePhase.ROUND_FINISHED->OnlineCommand(1,UUID.randomUUID().toString(),id,OnlineCommandType.NEXT_ROUND)
                        OnlinePhase.PLAYING->{seat=s.publicState.currentSeat!!;val board=s.publicState.board
                            val legal=views[seat].privateState.hand.flatMap {t->ChainEnd.entries.filter {end->board.isEmpty()||(if(end==ChainEnd.LEFT)board.first().tile.sideA else board.last().tile.sideB).let {it==t.sideA||it==t.sideB}}.map {t to it}}.firstOrNull()
                            if(legal==null)OnlineCommand(1,UUID.randomUUID().toString(),id,OnlineCommandType.PASS)
                            else OnlineCommand(1,UUID.randomUUID().toString(),id,OnlineCommandType.PLAY_TILE,legal.first,legal.second)}
                        else->error("UNEXPECTED_PHASE")
                    }
                    peers[seat].send("MATCH_COMMAND",command);val ack=peers[seat].until("COMMAND_ACCEPTED")
                    check(ack.path("commandId").asString()==command.commandId)
                    if(actions%20==0)println("I1_REAL_PROGRESS commands=$actions sequence=${ack.path("resultingSequence").asLong()}")
                    Thread.sleep(180)
                }
                val final=peers[0].snapshot(id);check(final.phase==OnlinePhase.MATCH_FINISHED)
                val db=context.getBean(Firestore::class.java);val repo=FirestoreMatchRepository(db);val match=repo.read(id)!!
                check(match.validationData&&match.score.max()>=150)
                val events=repo.readTrustedEvents(id,0);check(events.map {it.sequence}==(1L..match.lastSequence).toList())
                for(seat in 0..1){check(db.document("players/${accounts[seat].first}/matchHistory/$id").get().get().exists())
                    val eventPage=json.readValue(request("http://127.0.0.1:18083/api/v1/matches/$id/events?afterSequence=0",null,accounts[seat].second),OnlineEventPage::class.java)
                    eventPage.events.mapNotNull {it.event?.payload as? HandDealt}.forEach {check(it.seat==seat)}}
                val report="REAL_TWO_CLIENT_TEST=PASS\nREAL_FIRESTORE_ONLINE_MATCH=PASS\nREAL_WEBSOCKET=PASS\nMATCH_ID=$id\nEVENTS=${events.size}\nHISTORY_ENTRIES=2\nWALLET_WRITES=0\nVALIDATION_DATA=PRESERVED_AS_VALIDATION\n"
                Files.writeString(Path.of("../../client/Validation/Generated/i1-real-result.txt"),report);println(report)
                peers.forEach {it.socket.sendClose(1000,"Validation complete").get(5,TimeUnit.SECONDS)}
            }
        } finally {
            peers.forEach {runCatching {it.socket.abort()}}
            // Delete only the two anonymous accounts created by this invocation, using their own ID tokens.
            accounts.forEach {(_,token)->runCatching {request("https://identitytoolkit.googleapis.com/v1/accounts:delete?key=$key",json.writeValueAsString(mapOf("idToken" to token)))}.onFailure {println("I1_TEMP_IDENTITY_CLEANUP=FAILED")}}
        }
    }
}
