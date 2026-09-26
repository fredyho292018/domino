package com.teamfho.domino.online

import com.teamfho.domino.catalog.*
import com.teamfho.domino.match.*
import com.teamfho.domino.realtime.PresenceStore
import org.junit.jupiter.api.Test
import org.junit.jupiter.api.io.TempDir
import java.nio.file.*
import java.time.Instant
import java.util.UUID
import java.util.concurrent.*
import kotlin.test.*

class AbandonedReconciliationGateTests {
    @TempDir lateinit var directory:Path
    private val ids=(1L..22L).map{UUID(0,it).toString()}
    private val denied=(23L..44L).map{UUID(0,it).toString()}
    private val run=UUID(1,1).toString()
    private val now=Instant.EPOCH.plusSeconds(180)
    private fun file()=directory.resolve("gate.json")
    private fun gate()=FileAbandonedMatchReconciliationGate(file(),run,FileAbandonedMatchReconciliationGate.registryHash(ids))
    private fun configure(enabled:Boolean=true, list:List<String> = ids) {
        val temp=directory.resolve("next.json")
        Files.writeString(temp,GameCatalogCodec.json(LegacyReconciliationFile(1,run,enabled,list)))
        Files.move(temp,file(),StandardCopyOption.ATOMIC_MOVE,StandardCopyOption.REPLACE_EXISTING)
    }
    private fun state(id:String,lastPending:Boolean=false,version:Int=0):OnlineState {
        val s=PartnersOnlineTests.started()
        return s.copy(abandonmentLifecycleVersion=version,match=s.match.copy(matchId=id,participants=s.match.participants.map {
            it.copy(connectionState=if(lastPending&&it.seatIndex==3)ConnectionState.DISCONNECTED else ConnectionState.ABANDONED,
                disconnectedAt=Instant.EPOCH,reconnectDeadlineAt=now,abandonedAt=if(lastPending&&it.seatIndex==3)null else now)
        }))
    }
    private fun service(repo:OnlineRepository,g:AbandonedMatchReconciliationGate)=OnlineMatchService(
        GameCatalogService(GameCatalogRepository{GameCatalogV4Publisher.canonical()}),repo,OnlineEngine(reconciliationGate=g),OnlineTurnTests.Time(now))
    @Test fun `exact 22 allowed and 22 denied through domain and connection paths`() {
        configure();val engine=OnlineEngine(reconciliationGate=gate())
        for(id in ids+denied) {
            val legacy=state(id);val terminal=engine.abandon(legacy,"evaluate",now)
            val pending=state(id,true);val connection=engine.connection(pending,3,false,"connection",now)
            if(id in ids) {
                for(write in listOf(terminal,connection)) {
                    assertEquals(MatchStatus.CANCELLED,write!!.state.match.status)
                    assertTrue(write.histories.isEmpty());assertNull(write.state.match.result!!.winner)
                    assertEquals(1,write.events.count{it.payload is MatchFinished})
                    assertNull(engine.abandon(write.state,"again",now))
                }
            } else {assertNull(terminal);assertNull(connection)}
        }
    }
    @Test fun `worker cancels only authorized legacy and leaves unauthorized authoritative state unchanged`() {
        configure();val repo=MemoryOnlineRepository();val before=(ids+denied).associateWith{state(it)}
        before.values.forEach(repo::create)
        val index=MemoryTurnDueIndex();before.keys.forEach{index.entries[it]=now}
        val presence=object:PresenceStore {
            override fun touch(uid:String,connectionId:String,serverId:String){};override fun remove(uid:String,connectionId:String){}
            override fun onlinePlayers()=0L;override fun connectionCount(uid:String)=0L
        }
        val worker=OnlineTurnWorker(repo,service(repo,gate()),presence,index,TurnIndexBridge(index,TurnWorkFeed{_,_->AutoCloseable{}}))
        repeat(2){worker.processDue(now)}
        ids.forEach{assertEquals(MatchStatus.CANCELLED,repo.read(it)!!.match.status);assertFalse(index.entries.containsKey(it))}
        denied.forEach{assertEquals(before[it],repo.read(it));assertTrue(repo.events(it,0).isEmpty())}
        assertTrue(repo.histories.isEmpty())
    }
    @Test fun `absent empty disabled malformed wrong hash and unknown fields close legacy only`() {
        val g=gate()
        val variants=listOf<String?>(null,"", "{", GameCatalogCodec.json(LegacyReconciliationFile(1,run,false,ids)),
            GameCatalogCodec.json(LegacyReconciliationFile(1,run,true,emptyList())),
            GameCatalogCodec.json(LegacyReconciliationFile(1,run,true,denied)),
            GameCatalogCodec.json(LegacyReconciliationFile(1,run,true,ids.dropLast(1)+ids.first())),
            GameCatalogCodec.json(LegacyReconciliationFile(1,run,true,ids)).dropLast(1)+",\"unexpected\":true}")
        for(text in variants) {
            if(text==null)Files.deleteIfExists(file()) else Files.writeString(file(),text)
            val engine=OnlineEngine(reconciliationGate=g)
            assertNull(engine.abandon(state(ids.first()),"legacy",now))
            assertEquals(MatchStatus.CANCELLED,engine.abandon(state(denied.first(),version=1),"normal",now)!!.state.match.status)
        }
        Files.delete(file());Files.createDirectory(file());assertFalse(g.allowsLegacy(ids.first()))
    }
    @Test fun `new service-created match cancels at grace and reconnect inside grace survives`() {
        val memory=MemoryOnlineRepository()
        val repo=object:OnlineRepository by memory {
            override fun createPaired(write:OnlineWrite):OnlineState {memory.create(write.state);return write.state}
        }
        val time=OnlineTurnTests.Time(Instant.EPOCH)
        val catalog=GameCatalogService(GameCatalogRepository{GameCatalogV4Publisher.canonical()})
        val service=OnlineMatchService(catalog,repo,OnlineEngine(reconciliationGate=gate()),time)
        val rules=PartnersOnlineTests.before().match.ruleSnapshot
        for(reconnect in listOf(false,true)) {
            time.value=Instant.EPOCH
            val id=UUID.randomUUID().toString();service.createPaired(id,(0..3).map{"fixture-$it"},rules)
            assertEquals(1,repo.read(id)!!.abandonmentLifecycleVersion)
            time.value=Instant.EPOCH
            (0..3).forEach{service.connection(id,"fixture-$it"){false}}
            time.value=Instant.EPOCH.plusSeconds(if(reconnect)179 else 180)
            (0..3).forEach{service.connection(id,"fixture-$it"){reconnect}}
            assertEquals(if(reconnect)MatchStatus.IN_PROGRESS else MatchStatus.CANCELLED,repo.read(id)!!.match.status)
        }
    }
    @Test fun `old JSON defaults legacy and transitions cannot upgrade marker`() {
        val old=GameCatalogCodec.json(state(ids.first())).replace(",\"abandonmentLifecycleVersion\":0","")
        assertEquals(0,GameCatalogCodec.mapper.readValue(old,OnlineState::class.java).abandonmentLifecycleVersion)
        assertNull(OnlineEngine().abandon(GameCatalogCodec.mapper.readValue(old,OnlineState::class.java),"closed",now))
        val before=state(ids.first(),version=1);val write=OnlineEngine().abandon(before,"normal",now)!!
        assertFailsWith<IllegalArgumentException>{OnlineWrites.validate(before,write.copy(state=write.state.copy(abandonmentLifecycleVersion=0)),"normal")}
        assertNull(OnlineEngine(reconciliationGate=AbandonedMatchReconciliationGate{true}).abandon(before.copy(abandonmentLifecycleVersion=2),"unknown",now))
    }
    @Test fun `two instances reload common file closing survives reconstruction and concurrent calls commit once`() {
        val first=gate();val second=gate();assertFalse(first.allowsLegacy(ids[0]));configure()
        assertTrue(first.allowsLegacy(ids[0]));assertTrue(second.allowsLegacy(ids[0]))
        val repo=MemoryOnlineRepository();repo.create(state(ids[0]));val pool=Executors.newFixedThreadPool(2)
        try {
            pool.invokeAll(listOf(first,second).map{g->Callable{service(repo,g).abandon(ids[0])}}).forEach{it.get()}
            assertEquals(1,repo.events(ids[0],0).count{it.payload is MatchFinished})
        } finally {pool.shutdownNow()}
        configure(false)
        for(g in listOf(first,second,gate()))assertFalse(g.allowsLegacy(ids[1]))
        assertNull(service(repo,first).abandon(ids[0]))
    }
    @Test fun `gate closes between preliminary read and transaction and prevents commit`() {
        configure();val memory=MemoryOnlineRepository();val before=state(ids[0]);memory.create(before)
        val repository=object:OnlineRepository by memory {
            override fun transact(matchId:String,expectedSequence:Long,commandId:String,fingerprint:String,transition:(OnlineState)->OnlineWrite):OnlineCommit {
                configure(false)
                return memory.transact(matchId,expectedSequence,commandId,fingerprint,transition)
            }
        }
        assertEquals(OnlineError.STALE_COMMAND,assertFailsWith<OnlineFailure>{service(repository,gate()).abandon(ids[0])}.code)
        assertEquals(before,memory.read(ids[0]));assertTrue(memory.events(ids[0],0).isEmpty())
    }
}
