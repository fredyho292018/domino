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
private class CountingFirestore(val real:Firestore, val legacyParticipantWrites:Boolean=false) {
    var reads=0;var writes=0;var events=0;var participants=0;var histories=0;var unchangedParticipants=0
    private val participantValues=mutableMapOf<String,Any?>()
    private val pending=ThreadLocal<MutableMap<String,Map<String,Any?>>>()
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
                if(legacyParticipantWrites && value is Transaction && path.endsWith("/runtime/authoritative")) {
                    val state=GameCatalogCodec.mapper.readValue((args[1] as Map<*,*>)["stateJson"] as String,OnlineState::class.java)
                    state.match.participants.forEach {pending.get()["matches/${state.match.matchId}/players/${it.seatIndex}"]=MatchCodec.map(it)}
                }
                if("/players/" in path)pending.get()?.remove(path)
                if("/events/" in path)events++
                if(path.startsWith("matches/")&&"/players/" in path){participants++;if(participantValues[path]==args.getOrNull(1))unchangedParticipants++;participantValues[path]=args.getOrNull(1)}
                if("/matchHistory/" in path)histories++
            }
            if(call.method.name=="runTransaction") {
                @Suppress("UNCHECKED_CAST") val callback=args[0] as Transaction.Function<Any?>
                args[0]=Transaction.Function<Any?> {tx->
                    pending.set(mutableMapOf())
                    try {
                        val wrapped=wrap(tx) as Transaction
                        val result=callback.updateCallback(wrapped)
                        // Test-only reconstruction of F0's unconditional participant writes.
                        pending.get().toMap().forEach {(path,data)->wrapped.set(real.document(path),data)}
                        result
                    } finally {pending.remove()}
                }
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
    private fun controlledDuel(legacy:Boolean):List<String> {
        connect().use {real->
            val meter=CountingFirestore(real,legacy);val adapter=FirestoreOnlineRepository(meter.db)
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
            assertEquals(500,meter.reads);assertEquals(378,meter.events);assertEquals(7,state.match.currentRoundNumber)
            assertEquals(if(legacy)1148 else 899,meter.writes)
            assertEquals(if(legacy)251 else 2,meter.participants)
            assertEquals(if(legacy)249 else 0,meter.unchangedParticipants)
            println("F01_CONTROLLED_DUEL legacy=$legacy commands=$commands reads=${meter.reads} writes=${meter.writes} events=${meter.events} participantWrites=${meter.participants} unchangedParticipantWrites=${meter.unchangedParticipants} historyWrites=${meter.histories} rounds=${state.match.currentRoundNumber}")
            val histories=listOf("f0-a","f0-b").map{real.document("players/$it/matchHistory/$id").get().get().data}
            val persisted=real.collection("matches/$id/players").get().get().documents.sortedBy{it.id}.map{MatchCodec.read(it.data,MatchParticipant::class.java)}
            assertEquals(state.match.participants,persisted)
            assertEquals(state,FirestoreOnlineRepository(real).read(id))
            return listOf(state,adapter.events(id,0),histories).map{GameCatalogCodec.mapper.writeValueAsString(it).replace(id,"MATCH_ID")}
        }
    }
    @Test fun `controlled duel preserves complete state events history and eliminates unchanged writes`() {
        assertEquals(controlledDuel(true),controlledDuel(false))
    }
    @Test fun `connection changes persist only affected participant and survive repository recreation`() {
        connect().use {db->
            val f=OnlineTurnTests.Fixture();val meter=CountingFirestore(db);val repo=FirestoreOnlineRepository(meter.db)
            repo.create(f.s());val initial=meter.participants
            val service=OnlineMatchService(f.f.catalog,repo,f.f.engine,f.clock)
            fun verify(expected:Int) {
                assertEquals(initial+expected,meter.participants)
                val state=FirestoreOnlineRepository(db).read(f.id)!!
                state.match.participants.forEach {p->assertEquals(p,MatchCodec.read(db.document("matches/${f.id}/players/${p.seatIndex}").get().get().data!!,MatchParticipant::class.java))}
            }
            service.connection(f.id,"p0"){false};verify(1)
            service.connection(f.id,"p0"){false};verify(1)
            service.connection(f.id,"p0"){true};verify(2)
            f.at(1);service.connection(f.id,"p0"){false};verify(3)
            f.at(182);service.connection(f.id,"p0"){false};verify(4)
            assertEquals(ConnectionState.ABANDONED,repo.read(f.id)!!.match.participants[0].connectionState)
            assertEquals(0,meter.unchangedParticipants)
            db.document("onlineTurnWork/${f.id}").delete().get()
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
    @Test fun `participant optimization retains firestore race receipts and command conflicts`() {
        connect().use {db->
            for(crossPlayer in listOf(false,true)) {
                val f=OnlineTurnTests.Fixture();val repo=FirestoreOnlineRepository(db);val before=f.s();repo.create(before)
                val seat=before.match.currentSeat!!;val pool=java.util.concurrent.Executors.newFixedThreadPool(2)
                val barrier=java.util.concurrent.CyclicBarrier(2)
                val commands=(0..1).map {n->
                    val actor=if(crossPlayer && n==1)1-seat else seat
                    "p$actor" to OnlineCommand(1,"race$n",f.id,OnlineCommandType.PLAY_TILE,before.hands.getValue("$actor")[n],ChainEnd.RIGHT)
                }
                try {
                    val results=pool.invokeAll(commands.map {(uid,c)->java.util.concurrent.Callable {
                        barrier.await(10,TimeUnit.SECONDS)
                        runCatching {repo.transact(f.id,before.match.lastSequence,c.commandId,c.toString()) {
                            f.f.engine.command(it,uid,c,f.clock.instant())
                        }}
                    }}).map{it.get()}
                    assertEquals(1,results.count{it.isSuccess});assertEquals(1,repo.read(f.id)!!.board.size)
                    val winner=results.indexOfFirst{it.isSuccess};val c=commands[winner].second
                    val after=repo.read(f.id)
                    val repeated=repo.transact(f.id,before.match.lastSequence,c.commandId,c.toString()){error("Receipt must prevent transition")}
                    assertNull(repeated.write);assertEquals(results[winner].getOrThrow().receipt,repeated.receipt)
                    assertEquals(OnlineError.COMMAND_ID_CONFLICT,assertFailsWith<OnlineFailure> {
                        repo.transact(f.id,before.match.lastSequence,c.commandId,"different"){error("Conflict must prevent transition")}
                    }.code)
                    assertEquals(after,FirestoreOnlineRepository(db).read(f.id))
                    after!!.match.participants.forEach {p->assertEquals(p,MatchCodec.read(db.document("matches/${f.id}/players/${p.seatIndex}").get().get().data!!,MatchParticipant::class.java))}
                } finally {pool.shutdownNow();db.document("onlineTurnWork/${f.id}").delete().get()}
            }
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
