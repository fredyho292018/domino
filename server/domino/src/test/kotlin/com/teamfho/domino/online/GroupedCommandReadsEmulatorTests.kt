package com.teamfho.domino.online

import com.google.api.core.ApiFuture
import com.google.api.core.ApiFutures
import com.google.api.gax.grpc.GrpcStatusCode
import com.google.api.gax.rpc.AbortedException
import com.google.cloud.firestore.*
import com.google.firestore.v1.BatchGetDocumentsRequest
import com.teamfho.domino.match.*
import com.teamfho.domino.realtime.AckPhaseTiming
import com.teamfho.domino.realtime.AckTrace
import io.grpc.*
import org.junit.jupiter.api.Tag
import org.junit.jupiter.api.Test
import org.mockito.Mockito
import org.mockito.stubbing.Answer
import java.lang.reflect.InvocationTargetException
import java.time.Instant
import java.util.concurrent.*
import java.util.concurrent.atomic.AtomicInteger
import kotlin.test.*

/** All persistence uses demo project, loopback, explicit emulator credentials, never ADC. */
@Tag("EMULATOR")
class GroupedCommandReadsEmulatorTests {
    private class Reads:ClientInterceptor {
        val requests=CopyOnWriteArrayList<BatchGetDocumentsRequest>()
        override fun <Q:Any?,S:Any?> interceptCall(method:MethodDescriptor<Q,S>,options:CallOptions,next:Channel):ClientCall<Q,S> =
            object:ForwardingClientCall.SimpleForwardingClientCall<Q,S>(next.newCall(method,options)) {
                override fun sendMessage(message:Q) {
                    if(message is BatchGetDocumentsRequest)requests.add(message)
                    super.sendMessage(message)
                }
            }
    }
    private fun connect(meter:Reads=Reads()):Firestore {
        check(System.getenv("FIRESTORE_EMULATOR_HOST")=="127.0.0.1:18085")
        val options=FirestoreOptions.newBuilder().setProjectId("demo-domino-f0").setHost("127.0.0.1:18085").setEmulatorHost("127.0.0.1:18085")
            .setCredentials(FirestoreOptions.EmulatorCredentials()).build()
        // Emulator build replaces the channel provider. Observe the final provider, retaining
        // the real SDK, demo project and emulator credentials; never disable the emulator guard.
        val observed=Mockito.spy(options)
        val provider=FirestoreOptions.getDefaultTransportChannelProviderBuilder().setEndpoint("127.0.0.1:18085")
            .setChannelConfigurator{it.usePlaintext().proxyDetector{null}.intercept(meter)}.build()
        Mockito.doReturn(provider).`when`(observed).transportChannelProvider
        return FirestoreOptions.DefaultFirestoreFactory().create(observed)
    }
    /** Test-only faults/control retain the real SDK transaction and emulator commit. */
    private fun decorate(real:Firestore,
        batch:((Transaction,Array<DocumentReference>)->ApiFuture<List<DocumentSnapshot>>)?=null,
        beforeCallback:()->Unit={},
        afterCallback:()->Unit={}):Firestore {
        fun transaction(tx:Transaction)=Mockito.mock(Transaction::class.java,Answer {call->
            @Suppress("UNCHECKED_CAST")
            if(call.method.name=="getAll" && batch!=null)batch(tx,call.rawArguments[0] as Array<DocumentReference>)
            else try {call.method.trySetAccessible();call.method.invoke(tx,*call.rawArguments)}catch(e:InvocationTargetException){throw e.targetException}
        })
        return Mockito.mock(Firestore::class.java,Answer {call->
            val args=call.rawArguments.copyOf()
            if(call.method.name=="runTransaction") {
                assertEquals(8,(args[1] as TransactionOptions).numberOfAttempts)
                @Suppress("UNCHECKED_CAST") val callback=args[0] as Transaction.Function<Any?>
                args[0]=Transaction.Function<Any?> {tx->beforeCallback();val result=callback.updateCallback(transaction(tx));afterCallback();result}
            }
            try {call.method.trySetAccessible();call.method.invoke(real,*args)}catch(e:InvocationTargetException){throw e.targetException}
        })
    }
    private fun command(s:OnlineState,id:String="command"):OnlineCommand {
        val seat=s.match.currentSeat!!
        val move=s.hands.getValue("$seat").flatMap{tile->ChainEnd.entries.filter{OnlineEngine.fits(s,tile,it)}.map{tile to it}}.first()
        return OnlineCommand(1,id,s.match.matchId,OnlineCommandType.PLAY_TILE,move.first,move.second)
    }
    private fun uid(s:OnlineState)=s.match.participants[s.match.currentSeat!!].playerUid!!

    @Test fun `wire requests prove four rounds before three after and four documents with receipt first`() {
        for(sequential in listOf(true,false)) {
            val meter=Reads();connect(meter).use {db->
                val used=if(sequential)decorate(db,batch={tx,refs->ApiFutures.immediateFuture(refs.map{tx.get(it).get()})}) else db
                val f=AbandonFixture(FirestoreOnlineRepository(used));val s=f.initial;meter.requests.clear()
                val trace=AckTrace(System.nanoTime(),sink={});AckPhaseTiming.current.set(trace)
                val result=try{f.service.command(uid(s),command(s))}finally{AckPhaseTiming.current.remove()}
                assertNotNull(result.write)
                assertEquals(if(sequential)listOf(1,1,1,1) else listOf(1,1,2),meter.requests.map{it.documentsCount})
                assertTrue(meter.requests[0].transaction.isEmpty);assertTrue(meter.requests.drop(1).all{!it.transaction.isEmpty})
                assertTrue(meter.requests[1].documentsList.single().endsWith("/commands/command"))
                assertEquals(4,meter.requests.sumOf{it.documentsCount})
                val timing=trace.firestore.snapshot();assertEquals(true,timing["complete"]);assertEquals(3,timing["transactionReadCount"])
                if(!sequential)assertEquals(3,(timing["preReadCount"] as Int)+(timing["transactionReadRoundCount"] as Int))
                println("S710_WIRE sequential=$sequential rounds=${meter.requests.size} documents=${meter.requests.sumOf{it.documentsCount}}")
                if(!sequential) {
                    meter.requests.clear()
                    assertNull(f.service.command(uid(s),command(s)).write)
                    assertEquals(listOf(1,1),meter.requests.map{it.documentsCount})
                    meter.requests.clear()
                    assertEquals(OnlineError.COMMAND_ID_CONFLICT,assertFailsWith<OnlineFailure>{f.service.command(uid(s),command(s).copy(type=OnlineCommandType.PASS,tile=null,chainEnd=null))}.code)
                    assertEquals(listOf(1,1),meter.requests.map{it.documentsCount})
                }
            }
        }
    }
    @Test fun `reversed snapshot order is mapped by reference and presence schedule is preserved`() {
        connect().use {db->
            val f=AbandonFixture(FirestoreOnlineRepository(decorate(db,batch={tx,refs->ApiFutures.immediateFuture(tx.getAll(*refs).get().reversed())})))
            val work=db.document("onlineTurnWork/${f.id}");val checkAt=com.google.cloud.Timestamp.ofTimeSecondsAndNanos(7,0)
            work.update("presenceCheckAt",checkAt).get()
            f.service.command(uid(f.initial),command(f.initial))
            val saved=work.get().get();assertEquals(checkAt,saved.getTimestamp("presenceCheckAt"));assertEquals(checkAt,saved.getTimestamp("dueAt"))
        }
    }
    @Test fun `missing work keeps existing fallback and missing runtime keeps not found`() {
        connect().use {db->
            val repo=FirestoreOnlineRepository(db);val f=AbandonFixture(repo)
            val work=db.document("onlineTurnWork/${f.id}");work.delete().get()
            f.service.command(uid(f.initial),command(f.initial))
            assertEquals(com.google.cloud.Timestamp.ofTimeSecondsAndNanos(30,0),work.get().get().getTimestamp("presenceCheckAt"))
            val before=repo.read(f.id)!!;db.document("matches/${f.id}/runtime/authoritative").delete().get()
            assertEquals(OnlineError.MATCH_NOT_FOUND,assertFailsWith<OnlineFailure>{repo.transact(f.id,before.match.lastSequence,"missing","fingerprint"){error("No transition")}}.code)
            assertFalse(db.document("matches/${f.id}/commands/missing").get().get().exists())
        }
    }
    @Test fun `either grouped read failure and incomplete batch cannot transition or persist partial state`() {
        connect().use {db->
            for(survivor in listOf(0,1,2)) {
                val repo=FirestoreOnlineRepository(decorate(db,batch={tx,refs->
                    if(survivor==2)ApiFutures.immediateFuture(listOf(tx.get(refs[0]).get()))
                    else {tx.get(refs[survivor]).get();ApiFutures.immediateFailedFuture(IllegalStateException("INJECTED_READ_FAILURE"))}
                }))
                val f=AbandonFixture(repo);val work=db.document("onlineTurnWork/${f.id}").get().get().data
                var transitions=0
                assertFails{repo.transact(f.id,f.initial.match.lastSequence,"failure","fingerprint"){transitions++;error("Must not execute")}}
                assertEquals(0,transitions);assertEquals(f.initial,repo.read(f.id))
                assertEquals(work,db.document("onlineTurnWork/${f.id}").get().get().data)
                assertEquals(f.initial.match,MatchCodec.read(db.document("matches/${f.id}").get().get().data!!,Match::class.java))
                assertFalse(db.document("matches/${f.id}/commands/failure").get().get().exists())
                assertTrue(repo.events(f.id,0).isEmpty())
            }
        }
    }
    @Test fun `retry rereads both documents preserves eight attempt policy and commits once`() {
        val meter=Reads();connect(meter).use {db->
            val attempts=AtomicInteger()
            val used=decorate(db,afterCallback={if(attempts.incrementAndGet()==1)throw AbortedException("TEST_ABORT",null,GrpcStatusCode.of(Status.Code.ABORTED),true)})
            val f=AbandonFixture(FirestoreOnlineRepository(used));meter.requests.clear()
            val result=f.service.command(uid(f.initial),command(f.initial))
            assertEquals(2,attempts.get());assertEquals(listOf(1,1,2,1,2),meter.requests.map{it.documentsCount})
            assertEquals(result.write!!.events,f.repo.events(f.id,0));assertEquals(result.write!!.state,f.repo.read(f.id))
        }
    }
    @Test fun `prior revision change is rejected before domain`() {
        connect().use {db->
            val normal=FirestoreOnlineRepository(db);val f=AbandonFixture(normal);var transitions=0
            f.service.command(uid(f.initial),command(f.initial,"winner"))
            assertEquals(OnlineError.STALE_COMMAND,assertFailsWith<OnlineFailure>{normal.transact(f.id,f.initial.match.lastSequence,"stale","fp"){transitions++;error("Must not execute")}}.code)
            assertEquals(0,transitions);assertFalse(db.document("matches/${f.id}/commands/stale").get().get().exists())
        }
    }
    @Test fun `revision remains frozen when another instance advances state between retry callbacks`() {
        connect().use {db->connect().use {other->
            val attempts=AtomicInteger();lateinit var f:AbandonFixture
            val used=decorate(db,beforeCallback={
                if(attempts.incrementAndGet()==2) {
                    OnlineMatchService(f.catalog,FirestoreOnlineRepository(other),OnlineEngine(),f.clock)
                        .command(uid(f.initial),command(f.initial,"other-winner"))
                }
            },afterCallback={if(attempts.get()==1)throw AbortedException("TEST_ABORT",null,GrpcStatusCode.of(Status.Code.ABORTED),true)})
            f=AbandonFixture(FirestoreOnlineRepository(used))
            assertEquals(OnlineError.STALE_COMMAND,assertFailsWith<OnlineFailure>{f.service.command(uid(f.initial),command(f.initial,"retried"))}.code)
            assertEquals(2,attempts.get())
            assertFalse(db.document("matches/${f.id}/commands/retried").get().get().exists())
            assertTrue(db.document("matches/${f.id}/commands/other-winner").get().get().exists())
            assertEquals(1,f.repo.read(f.id)!!.board.size)
        }}
    }
    @Test fun `separate clients concurrent duplicate and revision race commit only one transition`() {
        for(duplicate in listOf(true,false))connect().use {a->connect().use {b->
            val ra=FirestoreOnlineRepository(a);val rb=FirestoreOnlineRepository(b);val f=AbandonFixture(ra)
            val barrier=CyclicBarrier(2)
            fun service(repo:OnlineRepository)=OnlineMatchService(f.catalog,object:OnlineRepository by repo {
                override fun transact(matchId:String,expectedSequence:Long,commandId:String,fingerprint:String,transition:(OnlineState)->OnlineWrite):OnlineCommit {
                    barrier.await(10,TimeUnit.SECONDS);return repo.transact(matchId,expectedSequence,commandId,fingerprint,transition)
                }
            },OnlineEngine(),f.clock)
            val pool=Executors.newFixedThreadPool(2)
            try {
                val results=pool.invokeAll(listOf(service(ra),service(rb)).mapIndexed {i,s->Callable{runCatching{s.command(uid(f.initial),command(f.initial,if(duplicate)"same" else "race$i"))}}}).map{it.get(30,TimeUnit.SECONDS)}
                assertEquals(1,results.count{it.getOrNull()?.write!=null})
                if(duplicate){assertTrue(results.all{it.isSuccess});assertEquals(results[0].getOrThrow().receipt,results[1].getOrThrow().receipt)}
                else {assertEquals(1,results.count{it.isFailure});assertEquals(OnlineError.STALE_COMMAND,(results.single{it.isFailure}.exceptionOrNull() as OnlineFailure).code)}
                val winner=results.first{it.getOrNull()?.write!=null}.getOrThrow().write!!
                assertEquals(winner.events,ra.events(f.id,0));assertEquals(winner.state,rb.read(f.id))
            } finally {pool.shutdownNow()}
        }}
    }
}
