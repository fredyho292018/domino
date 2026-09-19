package com.teamfho.swarm

import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.future.await
import tools.jackson.databind.JsonNode
import java.net.URI
import java.net.http.*
import java.time.Duration
import java.time.Instant
import java.util.concurrent.CompletionStage

class SafeFailure(val category:String,val fatal:Boolean=false):RuntimeException(category)
class ExpiredAuthRetry {
    private var used=false
    fun allow(category:String):Boolean {
        if(used||category!="AUTH_TOKEN_EXPIRED")return false
        used=true;return true
    }
}
fun authFailure(message:JsonNode)=SafeFailure(
    if(message.path("payload").text("code")=="AUTH_TOKEN_EXPIRED")"AUTH_TOKEN_EXPIRED" else "WS_AUTH_REJECTED",true)
class Api(private val http:HttpClient,private val base:String,private val token:suspend ()->String) {
    suspend fun call(method:String,path:String,body:Any?=null):JsonNode {
        // Explicit allowlist excludes monetization and direct match creation by construction.
        require(path=="game-modes"||path=="player/bootstrap"||path=="player/display-name"||path=="matchmaking/queue"||path=="players/me/matches"||path.matches(Regex("matches/[A-Za-z0-9_-]{1,128}/(snapshot|events\\?afterSequence=[0-9]+)"))){"API_PATH_NOT_ALLOWED"}
        val request=HttpRequest.newBuilder(URI(base.trimEnd('/')+"/api/v1/"+path)).timeout(Duration.ofSeconds(30))
            .header("Authorization","Bearer "+token()).header("Content-Type","application/json")
            .method(method,if(body==null)HttpRequest.BodyPublishers.noBody()else HttpRequest.BodyPublishers.ofString(Json.write(body))).build()
        val response=http.sendAsync(request,HttpResponse.BodyHandlers.ofString()).await()
        if(response.statusCode()!=200) {
            val expired=response.statusCode()==401&&response.body().length<8192&&
                runCatching{Json.read(response.body()).text("code")=="AUTH_TOKEN_EXPIRED"}.getOrDefault(false)
            throw SafeFailure(if(expired)"AUTH_TOKEN_EXPIRED"else"HTTP_${response.statusCode()}",response.statusCode() in setOf(401,403))
        }
        require(response.body().length<=2_000_000){"HTTP_RESPONSE_SIZE"}
        return Json.read(response.body())
    }
}

class Socket(private val http:HttpClient,base:String):WebSocket.Listener {
    val messages=Channel<JsonNode>(128)
    private val uri=URI(base.replaceFirst("https://","wss://").replaceFirst("http://","ws://").trimEnd('/')+"/ws/v1/realtime")
    private var socket:WebSocket?=null
    private var outgoing=0L
    private val text=StringBuilder()
    suspend fun connect(){socket=http.newWebSocketBuilder().connectTimeout(Duration.ofSeconds(15)).buildAsync(uri,this).await()}
    override fun onOpen(webSocket:WebSocket){webSocket.request(1)}
    override fun onText(webSocket:WebSocket,data:CharSequence,last:Boolean):CompletionStage<*>? {
        if(text.length+data.length>262144){messages.close(SafeFailure("WS_SIZE",true));webSocket.abort();return null}
        text.append(data)
        if(last)try {
            val n=Json.read(text.toString());text.setLength(0)
            require(n.number("version")==1&&n.path("payload").isObject)
            if(!messages.trySend(n).isSuccess){messages.close(SafeFailure("WS_BACKPRESSURE"));webSocket.abort()}
        }catch(_:Exception){messages.close(SafeFailure("WS_PROTOCOL",true));webSocket.abort()}
        webSocket.request(1);return null
    }
    override fun onClose(webSocket:WebSocket,statusCode:Int,reason:String):CompletionStage<*>?{messages.close(SafeFailure("WS_CLOSED_$statusCode",statusCode==1008));return null}
    override fun onError(webSocket:WebSocket,error:Throwable){messages.close(SafeFailure("WS_TRANSPORT"))}
    suspend fun send(type:String,payload:Any=emptyMap<String,Any>()) {
        socket!!.sendText(Json.write(mapOf("type" to type,"version" to 1,"sequence" to ++outgoing,"timestamp" to Instant.now().toString(),"payload" to payload)),true).await()
    }
    suspend fun close(){try{socket?.sendClose(1000,"Swarm stopped")?.await()}finally{socket?.abort();messages.close()}}
}
