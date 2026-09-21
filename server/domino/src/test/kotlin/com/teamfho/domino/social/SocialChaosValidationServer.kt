package com.teamfho.domino.social

import com.google.cloud.firestore.*
import com.teamfho.domino.DominoApplication
import com.teamfho.domino.security.*
import com.teamfho.domino.common.ApiErrorCode
import com.teamfho.domino.player.FirestorePlayerFoundationRepository
import io.micrometer.core.instrument.MeterRegistry
import org.springframework.boot.builder.SpringApplicationBuilder
import org.springframework.boot.test.context.TestConfiguration
import org.springframework.context.annotation.*
import org.springframework.web.bind.annotation.*
import java.time.Clock
import java.util.concurrent.ConcurrentHashMap
import java.util.concurrent.TimeUnit
import java.util.concurrent.CountDownLatch
import com.teamfho.domino.realtime.*
import org.springframework.web.socket.*
import org.springframework.web.socket.handler.TextWebSocketHandler
import org.springframework.web.socket.handler.WebSocketSessionDecorator
import org.springframework.web.socket.config.annotation.WebSocketConfigurer

/** Test classpath only. No production Firebase credentials or production endpoints accepted. */
object SocialChaosValidationServer {
    @Volatile private var storageFailure=false
    @JvmStatic fun main(args:Array<String>) {
        check(System.getenv("DOMINO_A4_LOCAL_ONLY")=="true")
        check(System.getenv("FIRESTORE_EMULATOR_HOST")=="127.0.0.1:18085")
        check(System.getenv("DOMINO_REAL_FIRESTORE_TESTS")!="true")
        val port=args.single().toInt();require(port in setOf(18141,18142))
        SpringApplicationBuilder(DominoApplication::class.java,Beans::class.java).run(
            "--spring.profiles.active=a4-local","--firebase.enabled=false","--server.address=127.0.0.1","--server.port=$port",
            "--spring.data.redis.host=127.0.0.1","--spring.data.redis.port=16379",
            "--spring.data.redis.timeout=300ms","--spring.data.redis.connect-timeout=300ms",
            "--logging.level.org.springframework.boot.security.autoconfigure.UserDetailsServiceAutoConfiguration=OFF")
    }
    @TestConfiguration(proxyBeanMethods=false)
    class Beans {
        @Bean fun localPlayers(db:Firestore):com.teamfho.domino.player.PlayerFoundationRepository=FirestorePlayerFoundationRepository(db,Clock.systemUTC())
        @Bean(destroyMethod="close") fun localFirestore():Firestore {
            val raw=FirestoreOptions.newBuilder().setProjectId("demo-domino-f0").setEmulatorHost("127.0.0.1:18085")
                .setCredentials(FirestoreOptions.EmulatorCredentials()).build().service
            return org.mockito.Mockito.mock(Firestore::class.java,org.mockito.stubbing.Answer {call->
                check(!storageFailure || call.method.name=="close"){"local storage fault"}
                try{call.method.invoke(raw,*call.rawArguments)}catch(e:java.lang.reflect.InvocationTargetException){throw e.targetException}
            })
        }
        @Bean @Primary fun localVerifier()=FirebaseTokenVerifier {token->
            if(!Regex("a4-[a-zA-Z0-9-]{1,100}").matches(token))throw AuthFailure(ApiErrorCode.AUTH_TOKEN_INVALID)
            FirebaseIdentity(token,true)
        }
        @Bean fun probes()=Probes()
        @Bean fun probeRoutes(probes:Probes)=WebSocketConfigurer{it.addHandler(probes,"/api/a4/socket")}
    }
    class Probes:TextWebSocketHandler() {
        class Probe(session:WebSocketSession) {
            @Volatile var barrier=CountDownLatch(0)
            @Volatile var selected=CountDownLatch(0)
            val frames=java.util.concurrent.CopyOnWriteArrayList<String>()
            val writer=ConnectionOutbound(object:WebSocketSessionDecorator(session) {
                override fun sendMessage(message:WebSocketMessage<*>) {super.sendMessage(message);frames.add(message.payload.toString())}
            },OutboundLimits(sendMillis=60000),beforeEphemeralCommit={selected.countDown();check(barrier.await(50,TimeUnit.SECONDS))})
        }
        val entries=ConcurrentHashMap<String,Probe>()
        override fun afterConnectionEstablished(session:WebSocketSession) {
            val id=session.uri!!.query;require(Regex("[a-zA-Z0-9-]{1,60}").matches(id))
            val p=Probe(session);check(entries.putIfAbsent(id,p)==null);session.attributes["a4Id"]=id
            p.writer.offerControl("AUTHENTICATED",emptyMap())
        }
        override fun afterConnectionClosed(session:WebSocketSession,status:CloseStatus) {
            entries.remove(session.attributes["a4Id"] as String)?.let{it.barrier.countDown();it.writer.close(remote=true)}
        }
    }
    @RestController
    @Profile("a4-local")
    class Diagnostics(private val db:Firestore,private val runtime:SocialInvalidationRuntime,private val metrics:MeterRegistry,private val probes:Probes) {
        private val handles=ConcurrentHashMap<String,LocalSocialAuthorizationIndex.Handle>()
        private var savedFeed:FirestoreSocialInvalidationFeed?=null
        @Volatile private var readFailure=false
        private val reader=SocialAuthorizationReader {viewer,target->
            check(!readFailure)
            db.runTransaction {tx->
                val pair=tx.get(db.document("socialPairs/${SocialPairIdentity.id(viewer,target)}")).get()
                val privacy=tx.get(db.document("players/$target/socialSettings/current")).get()
                val ab=tx.get(db.document("players/$viewer/blocks/$target")).get().exists()
                val ba=tx.get(db.document("players/$target/blocks/$viewer")).get().exists()
                val friend=tx.get(db.document("friendships/${SocialPairIdentity.id(viewer,target)}")).get().exists()
                SocialAuthorizationSnapshot(!ab && !ba && (privacy.getString("presenceVisibility")=="EVERYONE" || privacy.getString("presenceVisibility")=="FRIENDS" && friend),pair.getLong("authorizationRevision")?:0,privacy.getLong("revision")?:1)
            }.get(5,TimeUnit.SECONDS)
        }
        @PostMapping("/api/a4/seed") fun seed(@RequestBody body:Map<String,String>):PublicPlayerIdentity {
            val uid=body.getValue("uid");require(uid.startsWith("a4-"))
            FirestorePlayerFoundationRepository(db,Clock.systemUTC()).ensure(FirebaseIdentity(uid,true),"en","Guest-ABCDEFGH")
            return PublicPlayerIdentityService(FirestoreSocialRepository(db)).ensure(uid)
        }
        @GetMapping("/api/a4/audit") fun audit():Map<String,Any> {
            val events=db.collection(SocialInvalidation.COLLECTION).get().get().documents
            val pairs=db.collection("socialPairs").get().get().documents
            val ids=events.map{it.id}.toSet()
            val missing=pairs.sumOf{p->(1L..(p.getLong("authorizationRevision")?:0)).count{SocialInvalidation.pair(p.id,it).eventId !in ids}}
            return mapOf("events" to events.size,"pending" to events.count{it.getBoolean("published")!=true},"missing" to missing,
                "eventData" to events.map{SocialInvalidation.decode(it.data)})
        }
        @PostMapping("/api/a4/backlog") fun backlog(@RequestBody body:Map<String,String>):Map<String,Int> {
            val count=body.getValue("count").toInt();require(count in 1..100)
            val uid=body.getValue("actor");val target=body.getValue("target");require(uid.startsWith("a4-") && target.startsWith("a4-"))
            val identity=PublicPlayerIdentityService(FirestoreSocialRepository(db)).ensure(target)
            val candidate=SocialCandidate(target,PublicPlayerProfile(identity.publicPlayerId,identity.friendCode,"Guest"),"")
            val repo=FirestoreSocialRepository(db,runtime)
            repeat(count){repo.block(uid,candidate,it%2==0)}
            return mapOf("mutations" to count)
        }
        @PostMapping("/api/a4/feed") fun feed(@RequestBody body:Map<String,String>):Map<String,Any> {
            val f=FirestoreSocialInvalidationFeed(db)
            val cursor=body["seconds"]?.let{SocialFeedCursor(com.google.cloud.Timestamp.ofTimeSecondsAndNanos(it.toLong(),body.getValue("nanos").toInt()),body["id"]?.takeIf{it.isNotBlank()})}
            val page=cursor?.let{f.recover(it)}
            val next=page?.cursor?:f.start()
            return mapOf("seconds" to next.time.seconds.toString(),"nanos" to next.time.nanos.toString(),"id" to (next.id?:""),
                "eventIds" to (page?.events?.map{it.eventId}?:emptyList<String>()),"caughtUp" to (page?.caughtUp?:false))
        }
        @PostMapping("/api/a4/handle") fun handle(@RequestBody body:Map<String,String>):Map<String,String> {
            val h=runtime.index.register(body.getValue("connection"),body.getValue("viewer"),body.getValue("target"),reader)
            handles[h.id]=h;return mapOf("id" to h.id)
        }
        @GetMapping("/api/a4/state") fun state():Map<String,Any> = mapOf(
            "pid" to ProcessHandle.current().pid(),"size" to runtime.index.size(),
            "handles" to handles.mapValues{(_,h)->mapOf("allowed" to h.canDeliver(),"pair" to h.pairRevision,"privacy" to h.privacyRevision)},
            "metrics" to metrics.meters.filter{it.id.name.startsWith("social")}.associate{it.id.toString() to it.measure().map{v->v.value}},
            "threads" to Thread.getAllStackTraces().keys.filter{it.isAlive && it.name.startsWith("social-")}.groupingBy{it.name}.eachCount(),
            "probes" to probes.entries.mapValues{(_,p)->mapOf("selected" to (p.selected.count==0L),"frames" to p.frames.toList(),"snapshot" to p.writer.snapshot())})
        @PostMapping("/api/a4/probe") fun probe(@RequestBody body:Map<String,String>):Map<String,Any> {
            val p=probes.entries.getValue(body.getValue("id"))
            when(body.getValue("action")) {
                "hold"->{p.barrier=CountDownLatch(1);p.selected=CountDownLatch(1)}
                "offer"->{val h=handles.getValue(body.getValue("handle"));return mapOf("result" to p.writer.offerEphemeral(body["key"]?:"target","TEST_STATE",mapOf("value" to (body["value"]?:"old")),h.capability()).name)}
                "release"->p.barrier.countDown()
                "critical"->p.writer.offerCritical("MATCH_UPDATE",mapOf("synthetic" to true))
                "close"->{p.barrier.countDown();p.writer.close();check(p.writer.awaitClosed());probes.entries.remove(body.getValue("id"))}
            };return mapOf("ok" to true)
        }
        @PostMapping("/api/a4/close") fun close(@RequestBody body:Map<String,String>):Map<String,Int> {
            runtime.index.closeConnection(body.getValue("connection"));handles.entries.removeIf{it.value.connection==body.getValue("connection")};return mapOf("size" to runtime.index.size())
        }
        @PostMapping("/api/a4/fault") fun fault(@RequestBody body:Map<String,String>):Map<String,Boolean> {
            when(body.getValue("kind")) {
                "storage"->storageFailure=body.getValue("enabled").toBoolean()
                "reader"-> {readFailure=body.getValue("enabled").toBoolean();if(!readFailure)runtime.index.refreshExpired()}
                "pubsub"->{
                    // Fault injection is confined to this test-only host; production API has no switches.
                    val f=SocialInvalidationRuntime::class.java.getDeclaredField("bus").also{it.isAccessible=true}
                    (f.get(runtime) as? SocialInvalidationPubSub)?.close()
                    if(!body.getValue("enabled").toBoolean())f.set(runtime,null)
                }
                "dispatcher"->SocialInvalidationRuntime::class.java.getDeclaredField("nextDispatch").also{it.isAccessible=true}.setLong(runtime,if(body.getValue("enabled").toBoolean())Long.MAX_VALUE else 0)
                "dispatchFailure"->{
                    val f=SocialInvalidationRuntime::class.java.getDeclaredField("feed").also{it.isAccessible=true}
                    if(body.getValue("enabled").toBoolean()) {
                        savedFeed=f.get(runtime) as FirestoreSocialInvalidationFeed
                        val spy=org.mockito.Mockito.spy(savedFeed!!)
                        org.mockito.Mockito.doThrow(IllegalStateException("local injected dispatcher failure")).`when`(spy).pending()
                        f.set(runtime,spy)
                    } else {f.set(runtime,savedFeed);savedFeed=null}
                }
                else->error("unknown local fault")
            };return mapOf("ok" to true)
        }
    }
}
