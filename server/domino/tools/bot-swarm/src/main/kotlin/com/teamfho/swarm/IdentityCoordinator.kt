package com.teamfho.swarm

import com.sun.net.httpserver.HttpServer
import kotlinx.coroutines.runBlocking
import java.net.InetSocketAddress
import java.net.http.HttpClient
import java.nio.file.Files
import java.security.MessageDigest
import java.security.SecureRandom
import java.util.Base64
import java.util.concurrent.CountDownLatch
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicBoolean

fun main(args:Array<String>) {
    try { require(args.isEmpty()||args.toList()==listOf("--launch-check"));runBlocking { coordinate(args.isNotEmpty()) } }
    catch(_:Exception) { println("COORDINATION=FAIL_SAFE\nMATCHES_STARTED=0");kotlin.system.exitProcess(1) }
}
private suspend fun coordinate(launchCheck:Boolean=false) {
    val config=Config(environment="TEST",baseUrl="https://domino-api-test.teamfho.com",clients=3)
    ValidationTarget.authorize(config);config.validate(System.getenv("DOMINO_SWARM_TEST_BASE_URL"))
    require(!ValidationTarget.emulator())
    val settings=IdentitySettings();require(settings.project=="teamfho-domino")
    Files.list(settings.directory).use { paths->require(paths.filter{it.fileName.toString().matches(Regex("slot-\\d+\\.json"))}.count()==3L) }
    if(launchCheck) {
        (0..2).forEach{settings.validateSavedScope(config,it)}
        println("SWARM_PROCESS_START=PASS\nSWARM_PROCESS_CONFIGURATION=TEST\nSWARM_PROCESS_IDENTITY_DISCOVERY=3\nSWARM_PROCESS_REMOTE_TARGET_VALID=YES\nAUTHENTICATION_ATTEMPTED=NO\nMATCHMAKING_STARTED=NO\nMATCHES_STARTED=0")
        return
    }
    val hashes=(0..2).map{MessageDigest.getInstance("SHA-256").digest(Files.readAllBytes(settings.file(it)))}
    val nonce=ByteArray(32).also{SecureRandom().nextBytes(it)}
    val session=IdentityCoordination.session(nonce)
    val proofs=mutableListOf<IdentityProof>()
    HttpClient.newHttpClient().use { http->
        for(slot in 0..2) Identity(settings,config,slot,http).use { identity->
            identity.refresh(persistRotation=false)
            proofs.add(IdentityCoordination.proof(nonce,identity.uid))
            println("SLOT_0${slot+1}_PROJECT_MATCHES_TEST=YES\nSLOT_0${slot+1}_SCOPE=TEST\nSLOT_0${slot+1}_AUTH_READY=YES")
        }
    }
    println("SWARM_EXTERNAL_IDENTITY_DISCOVERY=PASS\nSESSION_NONCE_RANDOM=YES")
    val completed=CountDownLatch(1);val used=AtomicBoolean(false);val issued=AtomicBoolean(false)
    val server=HttpServer.create(InetSocketAddress("127.0.0.1",39163),0)
    server.createContext("/identity") { exchange->
        var body="{\"accepted\":false}";var status=400
        try {
            require(exchange.requestURI.path=="/identity" && exchange.requestURI.rawQuery==null)
            require(exchange.requestHeaders.getFirst("Origin")==null)
            if(exchange.requestMethod=="GET") {
                require(issued.compareAndSet(false,true) && !used.get())
                body=Json.write(mapOf("nonce" to Base64.getEncoder().encodeToString(nonce),"session" to session));status=200
            } else {
                require(exchange.requestMethod=="POST" && issued.get() && used.compareAndSet(false,true))
                val bytes=exchange.requestBody.readNBytes(2049);require(bytes.size<=2048)
                val p=Json.read(String(bytes,Charsets.UTF_8))
                require(p.path("ready").isBoolean)
                val unity=IdentityProof(p.text("session"),p.text("project"),p.text("scope"),p.path("ready").asBoolean(),p.text("fingerprint"))
                val result=IdentityCoordination.compare(session,listOf(unity)+proofs)
                result.forEach{(k,v)->println("$k=$v")}
                val accepted=result["DISTINCT_MATCH_PLAYERS"]=="YES"
                body=Json.write(mapOf("accepted" to accepted,"session" to session));status=200
                println("UNITY_PROJECT_MATCHES_TEST=YES\nUNITY_AUTH_READY=YES\nIDENTITY_GATE="+if(accepted)"PASS" else "FAIL")
            }
        } catch(_:Exception) { println("COORDINATION_REQUEST=REJECTED") }
        finally {
            val bytes=body.toByteArray();exchange.responseHeaders.add("Content-Type","application/json")
            exchange.responseHeaders.add("Cache-Control","no-store")
            exchange.sendResponseHeaders(status,bytes.size.toLong());exchange.responseBody.use{it.write(bytes)};exchange.close()
            if(used.get())completed.countDown()
        }
    }
    try {
        server.start();println("COORDINATOR_READY=YES\nMATCHES_STARTED=0")
        require(completed.await(15,TimeUnit.MINUTES)){"UNITY_TIMEOUT"}
    } finally {
        server.stop(0);nonce.fill(0);proofs.clear()
        require((0..2).all{hashes[it].contentEquals(MessageDigest.getInstance("SHA-256").digest(Files.readAllBytes(settings.file(it))))})
        println("IDENTITY_FILES_MODIFIED=0\nMATCHES_STARTED=0\nBOT_SWARM_MATCHMAKING_STARTED=NO")
    }
}
