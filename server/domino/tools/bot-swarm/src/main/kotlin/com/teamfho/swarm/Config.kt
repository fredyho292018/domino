package com.teamfho.swarm

import java.net.URI
import java.nio.file.Files
import java.nio.file.Path
import org.yaml.snakeyaml.LoaderOptions
import org.yaml.snakeyaml.Yaml
import org.yaml.snakeyaml.constructor.SafeConstructor

data class Config(val clients:Int=10,val mode:String="DUEL_1V1",val environment:String="LOCAL",
    val baseUrl:String="http://127.0.0.1:8080",val seed:Long=20260915,
    val thinkMinMs:Long=800,val thinkMaxMs:Long=2500,val requeue:Boolean=true,
    val requeueMinMs:Long=1000,val requeueMaxMs:Long=5000,val startupMaxMs:Long=1500,
    val durationSeconds:Long=0,val maxFailures:Int=8,val summarySeconds:Long=10,
    val slotOffset:Int=0,val targetMatches:Int=0) {
    fun validate(testUrl:String?=null):Config {
        require(environment in setOf("LOCAL","TEST")){"PROD_AND_UNKNOWN_ENVIRONMENT_BLOCKED"}
        require(clients in 1..20 && slotOffset in 0..19 && clients+slotOffset<=20){"CLIENT_CAP_20"}
        require(targetMatches in 0..10000)
        require(mode in setOf("DUEL_1V1","PARTNERS_2V2_ONLINE")){"UNSUPPORTED_MODE"}
        val u=URI(baseUrl)
        require(u.isAbsolute&&u.rawUserInfo==null&&u.rawQuery==null&&u.rawFragment==null&&u.path.orEmpty() in setOf("","/")){"INVALID_ENDPOINT"}
        if(environment=="LOCAL")require(u.scheme in setOf("http","https")&&u.host in setOf("localhost","127.0.0.1")){"LOCAL_REQUIRES_LOOPBACK"}
        else require(u.scheme=="https"&&!testUrl.isNullOrBlank()&&baseUrl==testUrl){"TEST_REQUIRES_EXPLICIT_TARGET"}
        require(thinkMinMs>=100&&thinkMaxMs>=thinkMinMs&&thinkMaxMs<=30000){"INVALID_THINK_TIME"}
        require(requeueMinMs>=1000&&requeueMaxMs>=requeueMinMs&&requeueMaxMs<=60000){"INVALID_REQUEUE_TIME"}
        require(startupMaxMs in 0..10000&&durationSeconds>=0&&maxFailures in 1..100&&summarySeconds in 2..300){"INVALID_TIMING"}
        return this
    }
    companion object {
        fun load(args:Array<String>,env:Map<String,String> = System.getenv()):Config {
            val cli=args.associate { require(it.startsWith("--")&&it.contains('=')){"USE_KEY_EQUALS_VALUE"};it.substring(2).split('=',limit=2).let{p->p[0] to p[1]} }
            val path=Path.of(cli["config"]?:"bot-swarm.yml")
            val options=LoaderOptions().apply{isAllowDuplicateKeys=false;maxAliasesForCollections=0;codePointLimit=32768}
            val values=if(Files.exists(path)) Files.newBufferedReader(path).use {Yaml(SafeConstructor(options)).load<Map<String,Any>>(it).orEmpty().mapValues{v->v.value.toString()}.toMutableMap()} else mutableMapOf()
            mapOf("DOMINO_SWARM_CLIENTS" to "clients","DOMINO_SWARM_MODE" to "mode","DOMINO_SWARM_ENVIRONMENT" to "environment","DOMINO_SWARM_SEED" to "seed").forEach{(e,k)->env[e]?.let{values[k]=it}}
            values.putAll(cli.filterKeys{it!="config"})
            val known=setOf("clients","mode","environment","baseUrl","seed","thinkMinMs","thinkMaxMs","requeue","requeueMinMs","requeueMaxMs","startupMaxMs","durationSeconds","maxFailures","summarySeconds","slotOffset","targetMatches")
            require(values.keys.all{it in known}){"UNKNOWN_CONFIGURATION_KEY"}
            fun s(k:String,d:String)=values[k]?:d
            return Config(s("clients","10").toInt(),s("mode","DUEL_1V1"),s("environment","LOCAL"),s("baseUrl","http://127.0.0.1:8080"),s("seed","20260915").toLong(),
                s("thinkMinMs","800").toLong(),s("thinkMaxMs","2500").toLong(),s("requeue","true").toBooleanStrict(),s("requeueMinMs","1000").toLong(),s("requeueMaxMs","5000").toLong(),
                s("startupMaxMs","1500").toLong(),s("durationSeconds","0").toLong(),s("maxFailures","8").toInt(),s("summarySeconds","10").toLong(),s("slotOffset","0").toInt(),s("targetMatches","0").toInt()).validate(env["DOMINO_SWARM_TEST_BASE_URL"])
        }
    }
}

object Aliases {
    private val names="Mateo Sofia Lucas Camila Daniel Valeria Adrian Elena Marco Isabella Gabriel Sara Nicolas Andrea Diego Lucia Samuel Emma Alejandro Natalia Carlos Laura David Mia Leo Paula Hector Julia Martin Clara Rafael Eva Bruno Daniela Victor Alicia Oscar Marina Tomas Carla".split(' ')
    fun forSeed(seed:Long)=names.shuffled(kotlin.random.Random(seed)).take(20)
}
