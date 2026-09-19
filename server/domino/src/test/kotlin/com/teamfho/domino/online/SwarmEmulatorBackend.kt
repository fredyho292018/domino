package com.teamfho.domino.online

import com.google.cloud.firestore.*
import com.google.firebase.*
import com.google.firebase.auth.FirebaseAuth
import com.google.auth.oauth2.*
import com.teamfho.domino.DominoApplication
import com.teamfho.domino.catalog.*
import com.teamfho.domino.player.*
import com.teamfho.domino.security.*
import org.springframework.boot.builder.SpringApplicationBuilder
import org.springframework.boot.test.context.TestConfiguration
import org.springframework.context.annotation.Bean
import org.mockito.Mockito
import org.mockito.stubbing.Answer
import java.nio.file.*
import java.time.Clock
import java.util.concurrent.ConcurrentHashMap
import java.util.concurrent.atomic.AtomicLong

/** Test classpath only. Never packaged into bootJar; all dependencies are exact loopback. */
object SwarmEmulatorBackend {
    const val project="demo-domino-swarm"
    val counts=ConcurrentHashMap<String,AtomicLong>()
    private val originals=java.util.Collections.synchronizedMap(java.util.IdentityHashMap<Any,Any>())
    private val documents=ConcurrentHashMap<String,Any>()
    fun count(key:String,n:Long=1){counts.computeIfAbsent(key){AtomicLong()}.addAndGet(n)}
    fun measured(value:Any?):Any? {
        val type=when(value){is Firestore->Firestore::class.java;is Transaction->Transaction::class.java;is WriteBatch->WriteBatch::class.java
            is DocumentReference->DocumentReference::class.java;is CollectionReference->CollectionReference::class.java;is Query->Query::class.java;else->return value}
        val proxy=Mockito.mock(type,Answer {call->
            val args=call.arguments.map{originals[it]?:it}.toTypedArray();val name=call.method.name
            if(name=="get"&&(value is DocumentReference||value is Transaction&&args.firstOrNull() is DocumentReference))count("reads")
            if(name=="get"&&value is Query)count("queries")
            if(name=="whereLessThanOrEqualTo"&&args.firstOrNull()=="dueAt")count("idleTurnQueries")
            if(name in setOf("set","create","update","delete")&&(value is Transaction||value is WriteBatch||value is DocumentReference)) {
                val ref=if(value is DocumentReference)value else args[0] as DocumentReference
                val data=args.getOrNull(if(value is DocumentReference)0 else 1)
                count("writes")
                val path=ref.path
                if(path.startsWith("matches/")&&"/players/" in path){count("participantWrites");if(data!=null&&documents.put(path,data)==data)count("unchangedParticipantWrites")}
                if("/events/" in path)count("eventWrites")
                if("/matchHistory/" in path)count("historyWrites")
                if(path.startsWith("rewardIntents/"))count("rewardIntentWrites")
            }
            if(name=="runTransaction") {
                @Suppress("UNCHECKED_CAST") val callback=args[0] as Transaction.Function<Any?>
                args[0]=Transaction.Function<Any?>{tx->callback.updateCallback(measured(tx) as Transaction)}
            }
            try{call.method.trySetAccessible();measured(call.method.invoke(value,*args))}
            catch(e:java.lang.reflect.InvocationTargetException){throw e.targetException}
        })
        originals[proxy]=value!!;return proxy
    }
    @JvmStatic fun main(args:Array<String>) {
        check(System.getenv("DOMINO_SWARM_EMULATOR")=="true")
        check(System.getenv("FIRESTORE_EMULATOR_HOST")=="127.0.0.1:18085")
        check(System.getenv("FIREBASE_AUTH_EMULATOR_HOST")=="127.0.0.1:19099")
        val dir=Path.of("build/swarm-emulator");Files.createDirectories(dir)
        SpringApplicationBuilder(DominoApplication::class.java,EmulatorBeans::class.java).run(
            "--firebase.enabled=false","--firebase.project-id=$project","--server.address=127.0.0.1","--server.port=18086",
            "--spring.data.redis.port=16379","--logging.level.com.teamfho.domino=WARN").use {context->
            Files.writeString(dir.resolve("ready"),"READY")
            try {while(!Files.exists(dir.resolve("stop"))) {
                Files.writeString(dir.resolve("metrics.json"),GameCatalogCodec.mapper.writeValueAsString(counts.mapValues{it.value.get()}))
                Thread.sleep(1000)
            }}finally{Files.deleteIfExists(dir.resolve("ready"))}
        }
    }
    @TestConfiguration(proxyBeanMethods=false)
    class EmulatorBeans {
        @Bean(destroyMethod="delete") fun emulatorApp():FirebaseApp=FirebaseApp.initializeApp(FirebaseOptions.builder()
            .setProjectId(project).setCredentials(GoogleCredentials.create(AccessToken("emulator-only",java.util.Date(Long.MAX_VALUE)))).build(),"swarm-emulator")
        @Bean fun emulatorVerifier(app:FirebaseApp):FirebaseTokenVerifier=FirebaseAdminTokenVerifier(FirebaseAuth.getInstance(app))
        @Bean(destroyMethod="close") fun emulatorFirestore():Firestore {
            val db=FirestoreOptions.newBuilder().setProjectId(project).setHost("127.0.0.1:18085").setEmulatorHost("127.0.0.1:18085")
                .setCredentials(FirestoreOptions.EmulatorCredentials()).build().service
            val wrapped=measured(db) as Firestore
            GameCatalogSeed.run(FirestoreCatalogDocumentStore(wrapped),GameCatalogV4Publisher.canonical())
            return wrapped
        }
        @Bean fun emulatorPlayers(db:Firestore):PlayerFoundationRepository=FirestorePlayerFoundationRepository(db,Clock.systemUTC())
    }
}
