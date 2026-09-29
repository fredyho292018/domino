package com.teamfho.swarm

import kotlinx.coroutines.runBlocking
import kotlinx.coroutines.withTimeout
import java.net.http.HttpClient

// Explicit one-user validation. No SimulatedClient, queue call, or gameplay command.
fun main(args:Array<String>) {
    try { runBlocking {
        val config=Config.load(args)
        ValidationTarget.authorize(config)
        val settings=IdentitySettings()
        require(config.clients==1 && config.environment=="TEST" && settings.project=="teamfho-domino")
        require(config.baseUrl=="https://domino-api-test.teamfho.com" && !ValidationTarget.emulator())
        require(settings.directory.parent.fileName.toString()=="LOAD")
        HttpClient.newHttpClient().use { http ->
            Identity(settings,config,config.slotOffset,http).use { identity ->
                identity.refresh()
                val bootstrap=Api(http,config.baseUrl){identity.currentToken()}.call("POST","player/bootstrap",mapOf("language" to "es"))
                require(bootstrap.path("player").text("uid")==identity.uid)
                val socket=Socket(http,config.baseUrl)
                try {
                    socket.connect();socket.send("AUTH",mapOf("idToken" to identity.token))
                    val reply=withTimeout(15000){socket.messages.receive()}
                    require(reply.text("type")=="AUTHENTICATED")
                    println("LOAD_SAMPLE_REST=PASS\nLOAD_SAMPLE_WSS=PASS\nMATCHMAKING_STARTED=NO")
                } finally {withTimeout(5000){socket.close()}}
            }
        }
    }} catch(_:Exception) {
        System.err.println("LOAD_SAMPLE=FAIL_SAFE");kotlin.system.exitProcess(1)
    }
}
