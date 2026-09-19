package com.teamfho.swarm

import kotlinx.coroutines.*
import java.net.http.HttpClient
import java.time.Duration

fun main(args:Array<String>) {
    try { runBlocking { runSwarm(Config.load(args)) } }
    catch(_:Exception){System.err.println("SWARM_START_FAILED (check configuration, identity provisioning and local services)");kotlin.system.exitProcess(1)}
}
suspend fun runSwarm(config:Config)=supervisorScope {
    ValidationTarget.authorize(config)
    config.validate(System.getenv("DOMINO_SWARM_TEST_BASE_URL"))
    // Validation runs before reading credentials, authentication or opening a socket.
    println("SWARM_STARTED environment=${config.environment} backend=${config.baseUrl} mode=${config.mode} clients=${config.clients}")
    val settings=IdentitySettings();val http=HttpClient.newBuilder().connectTimeout(Duration.ofSeconds(15)).followRedirects(HttpClient.Redirect.NEVER).build()
    val identities=mutableListOf<Identity>()
    try {
    val slots=config.slotOffset until config.slotOffset+config.clients
    val uids=slots.map{Json.read(java.nio.file.Files.readString(settings.file(it))).text("uid")}
    require(uids.none{it.isBlank()}&&uids.distinct().size==config.clients){"DISTINCT_IDENTITIES_REQUIRED"}
    val metrics=Metrics();val aliases=Aliases.forSeed(config.seed)
    val clients=(config.slotOffset until config.slotOffset+config.clients).map{slot->
        val identity=Identity(settings,config,slot,http);identities.add(identity)
        SimulatedClient(config,identity,aliases[slot],http,metrics){config.targetMatches==0||metrics.completedCount()<config.targetMatches}
    }
    val jobs=clients.map{launch {it.run()}}
    // Local validation control only: never a network command or a game-engine delay.
    val stopPath=System.getenv("DOMINO_SWARM_STOP_FILE")?.let{java.nio.file.Path.of(it)}
    val stopWatcher=launch {
        while(isActive) {
            if(clients.any{it.state==ClientState.FAILED}||(stopPath!=null&&java.nio.file.Files.exists(stopPath))) {
                println("SWARM_STOP_REQUESTED reason="+if(clients.any{it.state==ClientState.FAILED})"CLIENT_FAILED"else"LOCAL_SIGNAL")
                jobs.forEach{it.cancel()};break
            }
            delay(250)
        }
    }
    val stop=Thread { runBlocking {jobs.forEach{it.cancel()};withTimeoutOrNull(8000){jobs.joinAll()}} }
    Runtime.getRuntime().addShutdownHook(stop)
    val dashboard=launch {while(isActive){delay(config.summarySeconds*1000);println("DOMINO BOT SWARM mode=${config.mode} states="+clients.groupingBy{it.state}.eachCount()+" activeMatches="+clients.filter{it.state==ClientState.PLAYING}.mapNotNull{it.matchId}.distinct().size+" metrics="+Json.write(metrics.snapshot()))}}
    try {if(config.durationSeconds>0){withTimeoutOrNull(config.durationSeconds*1000){jobs.joinAll()};jobs.forEach{it.cancel()}};jobs.joinAll()}
    finally{stopWatcher.cancelAndJoin();dashboard.cancelAndJoin();try{Runtime.getRuntime().removeShutdownHook(stop)}catch(_:IllegalStateException){};println("SWARM_STOPPED "+Json.write(metrics.snapshot()))}
    if(clients.any{it.state==ClientState.FAILED})throw SafeFailure("SWARM_CLIENTS_FAILED",true)
    } finally {identities.forEach{it.close()};http.close()}
}
