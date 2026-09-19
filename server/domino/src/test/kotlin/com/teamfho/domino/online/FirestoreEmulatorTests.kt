package com.teamfho.domino.online

import com.google.cloud.firestore.*
import com.teamfho.domino.catalog.*
import com.teamfho.domino.match.*
import org.junit.jupiter.api.Tag
import org.junit.jupiter.api.Test
import org.mockito.Mockito
import org.mockito.stubbing.Answer
import java.lang.reflect.InvocationTargetException
import java.time.Instant
import java.util.concurrent.CountDownLatch
import java.util.concurrent.TimeUnit
import kotlin.test.*

/** Counts SDK document operations attempted by the real repository. Not a billing estimate:
 * query minimums, watch reads and SDK retries are reported separately. No production instrumentation. */
private class CountingFirestore(val real:Firestore) {
    var reads=0;var writes=0;var events=0;var participants=0;var histories=0;var unchangedParticipants=0
    private val participantValues=mutableMapOf<String,Any?>()
    fun wrap(value:Any?):Any? {
        val type=when(value) {
            is Firestore->Firestore::class.java
            is Transaction->Transaction::class.java
            is WriteBatch->WriteBatch::class.java
            else->return value
        }
        return Mockito.mock(type,Answer {call->
            val args=call.arguments.copyOf()
            if(call.method.name=="get" && (value is DocumentReference || value is Transaction && args.firstOrNull() is DocumentReference))reads++
            if(call.method.name in setOf("create","set","delete","update") && (value is Transaction || value is WriteBatch)) {
                val path=(args[0] as DocumentReference).path;writes++
                if("/events/" in path)events++
                if(path.startsWith("matches/")&&"/players/" in path){participants++;if(participantValues[path]==args.getOrNull(1))unchangedParticipants++;participantValues[path]=args.getOrNull(1)}
                if("/matchHistory/" in path)histories++
            }
            if(call.method.name=="runTransaction") {
                @Suppress("UNCHECKED_CAST") val callback=args[0] as Transaction.Function<Any?>
                args[0]=Transaction.Function<Any?> {tx->callback.updateCallback(wrap(tx) as Transaction)}
            }
            try {call.method.trySetAccessible();wrap(call.method.invoke(value,*args))}catch(e:InvocationTargetException){throw e.targetException}
        })
    }
    val db get()=wrap(real) as Firestore
}

@Tag("EMULATOR")
class FirestoreEmulatorTests {
    private fun connect():Firestore {
        check(System.getenv("FIRESTORE_EMULATOR_HOST")=="127.0.0.1:18085") {"LOCAL_EMULATOR_REQUIRED"}
        // Explicit demo project + loopback + no credentials: never falls back to ADC/production.
        return FirestoreOptions.newBuilder().setProjectId("demo-domino-f0").setHost("127.0.0.1:18085").setEmulatorHost("127.0.0.1:18085")
            .setChannelProvider(FirestoreOptions.getDefaultTransportChannelProviderBuilder()
                .setEndpoint("127.0.0.1:18085").setChannelConfigurator{it.usePlaintext().proxyDetector{null}}.build())
            .setCredentials(FirestoreOptions.EmulatorCredentials()).build().service
    }
    @Test fun `controlled full duel counts real adapter operations locally`() {
        connect().use {real->
            val meter=CountingFirestore(real);val adapter=FirestoreOnlineRepository(meter.db)
            val repo=object:OnlineRepository by adapter {
                override fun read(matchId:String):OnlineState?{meter.reads++;return adapter.read(matchId)}
            }
            val catalog=GameCatalogService(GameCatalogRepository {GameCatalogV2Publisher.canonical()})
            val clock=OnlineTurnTests.Time(Instant.parse("2026-01-01T00:00:00Z"))
            val random=java.util.Random(2);val engine=OnlineEngine(OnlineRandom{random.nextInt(it)})
            val service=OnlineMatchService(catalog,repo,engine,clock)
            val id=service.create("f0-a","DUEL_1V1",true).publicState.matchId
            var state=service.join("f0-b",id,"join").write!!.state;var commands=0
            while(state.match.status!=MatchStatus.FINISHED && commands<3000) {
                clock.value=clock.value.plusSeconds(6)
                var seat=state.match.currentSeat?:0
                var type=OnlineCommandType.PASS;var tile:DominoPips?=null;var end:ChainEnd?=null;var candidate:Int?=null;var even:Boolean?=null
                if(state.starter!=null && state.phase!=OnlinePhase.PLAYING && state.phase!=OnlinePhase.ROUND_FINISHED) {
                    val starter=state.starter!!
                    if(starter.method==StarterMethod.EVEN_ODD_GUESS){seat=starter.guessingSeat;type=OnlineCommandType.SUBMIT_EVEN_ODD_GUESS;even=true}
                    else {seat=(0..1).first{it.toString() !in starter.selections};type=OnlineCommandType.SELECT_STARTER_TILE;candidate=(0..1).first{it !in starter.selections.values}}
                } else if(state.phase==OnlinePhase.ROUND_FINISHED){seat=0;type=OnlineCommandType.NEXT_ROUND}
                else {
                    val move=state.hands.getValue("$seat").flatMap{t->ChainEnd.entries.filter{OnlineEngine.fits(state,t,it)}.map{t to it}}.firstOrNull()
                    if(move!=null){type=OnlineCommandType.PLAY_TILE;tile=move.first;end=move.second}
                }
                state=service.command(if(seat==0)"f0-a" else "f0-b",OnlineCommand(1,"c${commands++}",id,type,tile,end,candidate,even)).write!!.state
            }
            assertEquals(MatchStatus.FINISHED,state.match.status)
            assertEquals(2,meter.histories)
            assertEquals(state.match.lastSequence.toInt(),meter.events)
            println("F0_CONTROLLED_DUEL commands=$commands reads=${meter.reads} writes=${meter.writes} events=${meter.events} participantWrites=${meter.participants} unchangedParticipantWrites=${meter.unchangedParticipants} historyWrites=${meter.histories} rounds=${state.match.currentRoundNumber}")
        }
    }
    @Test fun `durable watch recovers persisted work then receives committed changes`() {
        connect().use {db->
            val id="f0-feed-${java.util.UUID.randomUUID()}";val ref=db.document("onlineTurnWork/$id")
            ref.set(mapOf("dueAt" to com.google.cloud.Timestamp.now())).get()
            val initial=CountDownLatch(1);val removed=CountDownLatch(1)
            try {FirestoreTurnWorkFeed(db).watch({match,due->if(match==id){if(due==null)removed.countDown()else initial.countDown()}},{error("feed failed")}).use {
                assertTrue(initial.await(10,TimeUnit.SECONDS));ref.delete().get();assertTrue(removed.await(10,TimeUnit.SECONDS))
            }}finally{ref.delete().get()}
        }
    }
    @Test fun `persisted expired turn recovers across service restart and concurrent workers`() {
        connect().use {db->
            val f=OnlineTurnTests.Fixture();f.at(65)
            val repo=FirestoreOnlineRepository(db);repo.create(f.s())
            val index=MemoryTurnDueIndex();val recovered=CountDownLatch(1)
            val feed=TurnWorkFeed {change,fail->FirestoreTurnWorkFeed(db).watch({id,due->change(id,due);if(id==f.id)recovered.countDown()},fail)}
            TurnIndexBridge(index,feed).use {bridge->
                bridge.tick();assertTrue(recovered.await(10,TimeUnit.SECONDS))
                val presence=object:com.teamfho.domino.realtime.PresenceStore {
                    override fun touch(uid:String,connectionId:String,serverId:String){}
                    override fun remove(uid:String,connectionId:String){}
                    override fun onlinePlayers()=2L
                    override fun connectionCount(uid:String)=1L
                }
                val restarted=OnlineMatchService(f.f.catalog,repo,f.f.engine,f.clock)
                val worker=OnlineTurnWorker(repo,restarted,presence,index,bridge)
                val pool=java.util.concurrent.Executors.newFixedThreadPool(2)
                try {pool.invokeAll((0..1).map{java.util.concurrent.Callable{OnlineMatchService(f.f.catalog,FirestoreOnlineRepository(db),f.f.engine,f.clock).timeout(f.id)}}).forEach{it.get()}}
                finally{pool.shutdownNow()}
                worker.processDue(f.clock.instant())
                val events=repo.events(f.id,0)
                assertEquals(1,events.count{it.payload is TurnTimeout});assertEquals(1,events.count{it.payload is AutoPlayed})
                assertEquals(1,repo.read(f.id)!!.board.size)
            }
            db.document("onlineTurnWork/${f.id}").delete().get()
        }
    }
    @Test fun `empty durable feed remains idle for sixty seconds without reopening`() {
        connect().use {db->
            var starts=0;val changes=java.util.concurrent.atomic.AtomicInteger()
            val feed=TurnWorkFeed {change,fail->starts++;FirestoreTurnWorkFeed(db).watch({id,due->changes.incrementAndGet();change(id,due)},fail)}
            val index=MemoryTurnDueIndex()
            val f=OnlineFixture();var polls=0
            val repository=object:OnlineRepository by f.repo {
                override fun due(now:Instant):List<String>{polls++;error("Idle Firestore polling forbidden")}
            }
            val presence=object:com.teamfho.domino.realtime.PresenceStore {
                override fun touch(uid:String,connectionId:String,serverId:String){}
                override fun remove(uid:String,connectionId:String){}
                override fun onlinePlayers()=0L
                override fun connectionCount(uid:String):Long?=error("No active matches")
            }
            TurnIndexBridge(index,feed).use {bridge->
                val worker=OnlineTurnWorker(repository,f.service,presence,index,bridge)
                repeat(12){worker.tick();assertTrue(index.claim(Instant.now()).isEmpty());Thread.sleep(5000)}
            }
            assertEquals(1,starts);assertEquals(0,changes.get())
            assertEquals(0,polls)
            println("F0_IDLE_SECONDS=60 WATCH_STARTS=1 TURN_POLL_QUERIES=0 WORK_CHANGES=0")
        }
    }
}
