package com.teamfho.domino.online

import com.google.cloud.firestore.*
import com.teamfho.domino.match.*
import org.junit.jupiter.api.Tag
import org.junit.jupiter.api.Test
import java.time.Instant
import java.util.concurrent.*
import kotlin.test.*

@Tag("EMULATOR")
class AllAbandonedEmulatorTests {
    @Test fun `singleton shared gate concurrent transactions cancel once preserving marker`() {
        val directory=java.nio.file.Files.createTempDirectory("s705r-gate-");val file=directory.resolve("gate.json")
        try {connect().use{db->
            val id=java.util.UUID.randomUUID().toString();val run=java.util.UUID.randomUUID().toString()
            java.nio.file.Files.writeString(file,com.teamfho.domino.catalog.GameCatalogCodec.json(LegacyReconciliationFile(1,run,true,listOf(id),"SINGLE_MATCH")))
            fun gate()=FileAbandonedMatchReconciliationGate(file,run,FileAbandonedMatchReconciliationGate.registryHash(listOf(id)),"SINGLE_MATCH",id)
            val initial=PartnersOnlineTests.started().let{s->s.copy(abandonmentLifecycleVersion=0,match=s.match.copy(matchId=id,participants=s.match.participants.map{it.copy(connectionState=ConnectionState.ABANDONED)}))}
            val repo=FirestoreOnlineRepository(db);repo.create(initial)
            val clock=OnlineTurnTests.Time(Instant.EPOCH.plusSeconds(180));val catalog=com.teamfho.domino.catalog.GameCatalogService(com.teamfho.domino.catalog.GameCatalogRepository{com.teamfho.domino.catalog.GameCatalogV4Publisher.canonical()})
            val barrier=CyclicBarrier(2)
            val fenced=object:OnlineRepository by repo {override fun transact(matchId:String,expectedSequence:Long,commandId:String,fingerprint:String,transition:(OnlineState)->OnlineWrite):OnlineCommit {barrier.await(10,TimeUnit.SECONDS);return repo.transact(matchId,expectedSequence,commandId,fingerprint,transition)}}
            val pool=Executors.newFixedThreadPool(2)
            try {val results=pool.invokeAll((0..1).map{Callable{OnlineMatchService(catalog,fenced,OnlineEngine(reconciliationGate=gate()),clock).reconcileLegacySingleton(id)}}).map{it.get(30,TimeUnit.SECONDS)};assertEquals(1,results.count{it?.write!=null})}finally{pool.shutdownNow()}
            assertEquals(MatchStatus.CANCELLED,repo.read(id)!!.match.status);assertEquals(0,repo.read(id)!!.abandonmentLifecycleVersion)
            assertEquals(1,repo.events(id,0).count{it.payload is MatchFinished});assertFalse(db.document("onlineTurnWork/$id").get().get().exists())
            assertNull(OnlineMatchService(catalog,repo,OnlineEngine(reconciliationGate=gate()),clock).reconcileLegacySingleton(id))
            for(i in 0..3)assertFalse(db.document("players/m5-p$i/matchHistory/$id").get().get().exists())
        }}finally{java.nio.file.Files.deleteIfExists(file);java.nio.file.Files.deleteIfExists(directory)}
    }
    @Test fun `legacy gate persists marker and two authorized instances cancel once`() {
        val directory=java.nio.file.Files.createTempDirectory("s704a-gate-")
        val file=directory.resolve("gate.json")
        try {connect().use{db->
            val ids=(1..22).map{java.util.UUID.randomUUID().toString()};val run=java.util.UUID.randomUUID().toString()
            java.nio.file.Files.writeString(file,com.teamfho.domino.catalog.GameCatalogCodec.json(LegacyReconciliationFile(1,run,true,ids)))
            fun gate()=FileAbandonedMatchReconciliationGate(file,run,FileAbandonedMatchReconciliationGate.registryHash(ids))
            val initial=PartnersOnlineTests.started().let{s->s.copy(abandonmentLifecycleVersion=0,match=s.match.copy(matchId=ids[0],
                participants=s.match.participants.map{it.copy(connectionState=ConnectionState.ABANDONED,abandonedAt=Instant.EPOCH)}))}
            val repo=FirestoreOnlineRepository(db);repo.create(initial)
            val clock=OnlineTurnTests.Time(Instant.EPOCH.plusSeconds(180))
            val catalog=com.teamfho.domino.catalog.GameCatalogService(com.teamfho.domino.catalog.GameCatalogRepository{com.teamfho.domino.catalog.GameCatalogV4Publisher.canonical()})
            val barrier=CyclicBarrier(2)
            val fenced=object:OnlineRepository by repo {
                override fun transact(matchId:String,expectedSequence:Long,commandId:String,fingerprint:String,transition:(OnlineState)->OnlineWrite):OnlineCommit {
                    barrier.await(10,TimeUnit.SECONDS);return repo.transact(matchId,expectedSequence,commandId,fingerprint,transition)
                }
            }
            val pool=Executors.newFixedThreadPool(2)
            try {
                val outcomes=pool.invokeAll((0..1).map{Callable{OnlineMatchService(catalog,fenced,OnlineEngine(reconciliationGate=gate()),clock).abandon(ids[0])}}).map{it.get(30,TimeUnit.SECONDS)}
                assertEquals(1,outcomes.count{it?.write!=null})
            } finally {pool.shutdownNow()}
            val saved=FirestoreOnlineRepository(db).read(ids[0])!!
            assertEquals(0,saved.abandonmentLifecycleVersion);assertEquals(MatchStatus.CANCELLED,saved.match.status)
            val persisted=db.document("matches/${ids[0]}/runtime/authoritative").get().get()
            assertFalse(persisted.getString("stateJson")!!.contains("abandonmentLifecycleVersion"))
            assertEquals(0L,persisted.getLong("abandonmentLifecycleVersion"))
            assertEquals(1,repo.events(ids[0],0).count{it.payload is MatchFinished})
            assertFalse(db.document("onlineTurnWork/${ids[0]}").get().get().exists())
            for(i in 0..3)assertFalse(db.document("players/m5-p$i/matchHistory/${ids[0]}").get().get().exists())
            val fresh=initial.copy(abandonmentLifecycleVersion=1,match=initial.match.copy(matchId=ids[1]))
            repo.create(fresh)
            assertEquals(1,FirestoreOnlineRepository(db).read(ids[1])!!.abandonmentLifecycleVersion)
            assertNotNull(OnlineMatchService(catalog,repo,OnlineEngine(),clock).abandon(ids[1]))
        }} finally {java.nio.file.Files.deleteIfExists(file);java.nio.file.Files.deleteIfExists(directory)}
    }
    private fun connect():Firestore {
        check(System.getenv("FIRESTORE_EMULATOR_HOST")=="127.0.0.1:18085")
        return FirestoreOptions.newBuilder().setProjectId("demo-domino-f0").setHost("127.0.0.1:18085").setEmulatorHost("127.0.0.1:18085")
            .setChannelProvider(FirestoreOptions.getDefaultTransportChannelProviderBuilder().setEndpoint("127.0.0.1:18085").setChannelConfigurator{it.usePlaintext().proxyDetector{null}}.build())
            .setCredentials(FirestoreOptions.EmulatorCredentials()).build().service
    }
    @Test fun `two instances last abandonment commits one cancellation no history and deletes work`() {
        connect().use{db->
            val repo=FirestoreOnlineRepository(db);val f=AbandonFixture(repo);f.disconnectAll();f.at(180)
            (0..2).forEach{f.presence(it,false)}
            val oldEvents=repo.events(f.id,0)
            val barrier=CyclicBarrier(2)
            val concurrent=object:OnlineRepository by repo {
                override fun transact(matchId:String,expectedSequence:Long,commandId:String,fingerprint:String,transition:(OnlineState)->OnlineWrite):OnlineCommit {
                    barrier.await(10,TimeUnit.SECONDS)
                    return repo.transact(matchId,expectedSequence,commandId,fingerprint,transition)
                }
            }
            val pool=Executors.newFixedThreadPool(2)
            try {
                val results=pool.invokeAll((0..1).map{Callable{
                    OnlineMatchService(f.catalog,concurrent,OnlineEngine(),f.clock).connection(f.id,"m5-p3"){false}
                }}).map{it.get(30,TimeUnit.SECONDS)}
                assertEquals(1,results.count{it?.write!=null})
            }finally{pool.shutdownNow()}
            val state=FirestoreOnlineRepository(db).read(f.id)!!
            assertEquals(MatchStatus.CANCELLED,state.match.status);assertNull(state.turnDeadlineAt)
            val events=repo.events(f.id,0)
            assertEquals(oldEvents,events.take(oldEvents.size))
            assertEquals(1,events.count{it.payload is MatchFinished})
            assertEquals(MatchFinishReason.CANCELLED,(events.last().payload as MatchFinished).result.finishReason)
            repeat(3){assertNull(f.service.abandon(f.id));assertNull(f.presence(3,true));repo.refreshDiscovery(f.id,f.clock.instant())}
            assertEquals(events,repo.events(f.id,0))
            assertFalse(db.document("onlineTurnWork/${f.id}").get().get().exists())
            for(i in 0..3)assertFalse(db.document("players/m5-p$i/matchHistory/${f.id}").get().get().exists())
            assertEquals(MatchStatus.CANCELLED,MatchCodec.read(db.document("matches/${f.id}").get().get().data!!,Match::class.java).status)
        }
    }
    @Test fun `reconnect just before grace versus expiry transaction has coherent winner`() {
        connect().use{db->
            val repo=FirestoreOnlineRepository(db);val f=AbandonFixture(repo)
            // Three seats previously abandoned; fourth still has its original grace deadline.
            (0..2).forEach{f.presence(it,false)};f.at(10);f.presence(3,false)
            f.at(180);(0..2).forEach{f.presence(it,false)}
            val barrier=CyclicBarrier(2)
            val concurrent=object:OnlineRepository by repo {
                override fun transact(matchId:String,expectedSequence:Long,commandId:String,fingerprint:String,transition:(OnlineState)->OnlineWrite):OnlineCommit {
                    barrier.await(10,TimeUnit.SECONDS);return repo.transact(matchId,expectedSequence,commandId,fingerprint,transition)
                }
            }
            val pool=Executors.newFixedThreadPool(2)
            try {
                val results=pool.invokeAll(listOf(189L,190L).map{second->Callable{
                    runCatching{OnlineMatchService(f.catalog,concurrent,OnlineEngine(),OnlineTurnTests.Time(Instant.EPOCH.plusSeconds(second)))
                        .connection(f.id,"m5-p3"){true}}
                }}).map{it.get(30,TimeUnit.SECONDS)}
                assertEquals(1,results.count{it.getOrNull()?.write!=null})
                assertTrue(results.filter{it.isFailure}.all{(it.exceptionOrNull() as? OnlineFailure)?.code==OnlineError.STALE_COMMAND})
                val state=repo.read(f.id)!!
                if(state.match.participants[3].connectionState==ConnectionState.CONNECTED)assertEquals(MatchStatus.IN_PROGRESS,state.match.status)
                else {assertEquals(ConnectionState.ABANDONED,state.match.participants[3].connectionState);assertEquals(MatchStatus.CANCELLED,state.match.status)}
                assertTrue(repo.events(f.id,0).count{it.payload is MatchFinished}<=1)
            }finally{pool.shutdownNow();db.document("onlineTurnWork/${f.id}").delete().get()}
        }
    }
}
