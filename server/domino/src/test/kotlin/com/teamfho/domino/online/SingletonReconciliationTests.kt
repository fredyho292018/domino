package com.teamfho.domino.online

import com.teamfho.domino.catalog.*
import com.teamfho.domino.match.*
import org.junit.jupiter.api.Test
import org.junit.jupiter.api.io.TempDir
import java.nio.file.*
import java.time.Instant
import java.util.UUID
import kotlin.test.*

class SingletonReconciliationTests {
    @TempDir lateinit var dir:Path
    private val id=UUID(0,51).toString();private val other=UUID(0,52).toString();private val run=UUID(1,51).toString()
    private val now=Instant.EPOCH.plusSeconds(180)
    private fun gate()=FileAbandonedMatchReconciliationGate(dir.resolve("gate.json"),run,FileAbandonedMatchReconciliationGate.registryHash(listOf(id)),"SINGLE_MATCH",id)
    private fun configure(enabled:Boolean=true)=Files.writeString(dir.resolve("gate.json"),GameCatalogCodec.json(LegacyReconciliationFile(1,run,enabled,listOf(id),"SINGLE_MATCH")))
    private fun state(matchId:String=id,version:Int=0)=PartnersOnlineTests.started().let{s->s.copy(abandonmentLifecycleVersion=version,match=s.match.copy(matchId=matchId,participants=s.match.participants.map{it.copy(connectionState=ConnectionState.ABANDONED)}))}
    private fun service(repo:OnlineRepository,g:AbandonedMatchReconciliationGate=gate())=OnlineMatchService(GameCatalogService(GameCatalogRepository{GameCatalogV4Publisher.canonical()}),repo,OnlineEngine(reconciliationGate=g),OnlineTurnTests.Time(now))
    @Test fun `singleton transaction allows exactly one id and commits once`() {
        configure();val repo=MemoryOnlineRepository();repo.create(state());val foreign=state(other);repo.create(foreign);val s=service(repo)
        assertNotNull(s.reconcileLegacySingleton(id));assertNull(s.reconcileLegacySingleton(id));assertNull(s.reconcileLegacySingleton(other))
        assertEquals(MatchStatus.CANCELLED,repo.read(id)!!.match.status);assertEquals(0,repo.read(id)!!.abandonmentLifecycleVersion)
        assertEquals(1,repo.events(id,0).count{it.payload is MatchFinished});assertTrue(repo.histories.isEmpty());assertEquals(foreign,repo.read(other))
    }
    @Test fun `current lifecycle cannot be migrated but normal abandonment bypasses gate`() {
        configure();val repo=MemoryOnlineRepository();repo.create(state(version=1));val s=service(repo)
        assertNull(s.reconcileLegacySingleton(id));assertTrue(repo.events(id,0).isEmpty());configure(false)
        assertNotNull(s.abandon(id));assertEquals(MatchStatus.CANCELLED,repo.read(id)!!.match.status)
    }
    @Test fun `singleton rejects wrong state unknown version connected and disconnected seats`() {
        configure();val engine=OnlineEngine(reconciliationGate=gate())
        for(s in listOf(state(version=-1),state().let{it.copy(match=it.match.copy(status=MatchStatus.CREATED))},state().let{it.copy(match=it.match.copy(status=MatchStatus.STARTING))}) )assertNull(engine.reconcileSingleton(s,"test",now))
        for(c in listOf(ConnectionState.CONNECTED,ConnectionState.DISCONNECTED)) {
            val s=state().let{it.copy(match=it.match.copy(participants=it.match.participants.map{p->if(p.seatIndex==0)p.copy(connectionState=c) else p}))}
            assertNull(engine.reconcileSingleton(s,"test",now))
        }
    }
    @Test fun `singleton malformed empty wrong mode wrong pin wrong operation fail closed`() {
        val g=gate();assertFalse(g.allowsLegacy(id))
        for(text in listOf("","{",GameCatalogCodec.json(LegacyReconciliationFile(1,run,true,emptyList(),"SINGLE_MATCH")),GameCatalogCodec.json(LegacyReconciliationFile(1,run,true,listOf(id))),GameCatalogCodec.json(LegacyReconciliationFile(1,run,true,listOf(other),"SINGLE_MATCH")),GameCatalogCodec.json(LegacyReconciliationFile(1,UUID(5,5).toString(),true,listOf(id),"SINGLE_MATCH")))) {
            Files.writeString(dir.resolve("gate.json"),text);assertFalse(g.allowsLegacy(id));assertNotNull(OnlineEngine(reconciliationGate=g).abandon(state(version=1),"normal",now))
        }
        configure();assertFalse(FileAbandonedMatchReconciliationGate(dir.resolve("gate.json"),run,"0".repeat(64),"SINGLE_MATCH",id).allowsLegacy(id))
    }
    @Test fun `shared file and centralized connection path obey same singleton policy`() {
        val a=gate();val b=gate();configure();assertTrue(a.allowsLegacy(id));assertTrue(b.allowsLegacy(id))
        val pending=state().let{it.copy(match=it.match.copy(participants=it.match.participants.map{p->if(p.seatIndex==3)p.copy(connectionState=ConnectionState.DISCONNECTED,reconnectDeadlineAt=now) else p}))}
        assertEquals(MatchStatus.CANCELLED,OnlineEngine(reconciliationGate=a).connection(pending,3,false,"connection",now)!!.state.match.status)
        assertNull(OnlineEngine(reconciliationGate=b).connection(pending.copy(match=pending.match.copy(matchId=other)),3,false,"wrong",now))
        configure(false);assertFalse(a.allowsLegacy(id));assertFalse(b.allowsLegacy(id));assertFalse(gate().allowsLegacy(id))
    }
    @Test fun `close between precheck and commit prevents singleton transaction`() {
        configure();val memory=MemoryOnlineRepository();val before=state();memory.create(before)
        val repo=object:OnlineRepository by memory {
            override fun transact(matchId:String,expectedSequence:Long,commandId:String,fingerprint:String,transition:(OnlineState)->OnlineWrite):OnlineCommit {
                configure(false);return memory.transact(matchId,expectedSequence,commandId,fingerprint,transition)
            }
        }
        assertFailsWith<OnlineFailure>{service(repo).reconcileLegacySingleton(id)};assertEquals(before,memory.read(id));assertTrue(memory.events(id,0).isEmpty())
    }
    @Test fun `audit correlation stable and no raw id`() {assertEquals(run,gate().operationId());val hash=LegacyReconciliationAudit.fingerprint(id);assertEquals(64,hash.length);assertFalse(hash.contains(id));assertEquals(hash,LegacyReconciliationAudit.fingerprint(id));assertNotEquals(hash,LegacyReconciliationAudit.fingerprint(other))}
    @Test fun `historical 22 config without mode remains supported while singleton rejects two ids`() {
        val ids=(1L..22L).map{UUID(0,it).toString()}
        Files.writeString(dir.resolve("gate.json"),GameCatalogCodec.json(LegacyReconciliationFile(1,run,true,ids)).replace(",\"mode\":\"EXACT_22\"",""))
        assertTrue(FileAbandonedMatchReconciliationGate(dir.resolve("gate.json"),run,FileAbandonedMatchReconciliationGate.registryHash(ids)).allowsLegacy(ids.first()))
        Files.writeString(dir.resolve("gate.json"),GameCatalogCodec.json(LegacyReconciliationFile(1,run,true,listOf(id,other),"SINGLE_MATCH")))
        assertFalse(gate().allowsLegacy(id))
    }
    @Test fun `audit messages correlate committed singleton without participant or raw match ids`() {
        val logger=org.slf4j.LoggerFactory.getLogger(LegacyReconciliationAudit::class.java) as ch.qos.logback.classic.Logger
        val appender=ch.qos.logback.core.read.ListAppender<ch.qos.logback.classic.spi.ILoggingEvent>();appender.start();logger.addAppender(appender)
        try {
            configure();val repo=MemoryOnlineRepository();repo.create(state());service(repo).reconcileLegacySingleton(id)
            val messages=appender.list.map{it.formattedMessage}
            assertEquals(1,messages.count{it.startsWith("LEGACY_RECONCILIATION_COMPLETED")})
            assertTrue(messages.all{it.contains("operation=$run")&&it.contains("matchFingerprint=${LegacyReconciliationAudit.fingerprint(id)}")&&!it.contains(id)&&!it.contains("m5-p")})
        }finally{logger.detachAppender(appender);appender.stop()}
    }
}
