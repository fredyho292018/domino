package com.teamfho.domino.online

import com.teamfho.domino.catalog.*
import com.teamfho.domino.match.*
import org.junit.jupiter.api.Test
import java.time.Instant
import java.util.UUID
import kotlin.test.*

class AbandonFixture(val repo:OnlineRepository=MemoryOnlineRepository()) {
    val clock=OnlineTurnTests.Time(Instant.EPOCH)
    val catalog=GameCatalogService(GameCatalogRepository{GameCatalogV4Publisher.canonical()})
    val engine=OnlineEngine()
    val service=OnlineMatchService(catalog,repo,engine,clock)
    val initial=PartnersOnlineTests.started().let{it.copy(abandonmentLifecycleVersion=1,match=it.match.copy(matchId="s701r-${UUID.randomUUID()}"))}
    val id=initial.match.matchId
    init{repo.create(initial)}
    fun at(s:Long){clock.value=Instant.EPOCH.plusSeconds(s)}
    fun state()=repo.read(id)!!
    fun presence(seat:Int,connected:Boolean)=service.connection(id,"m5-p$seat"){connected}
    fun disconnectAll(){(0..3).forEach{presence(it,false)}}
    fun expireAll(){(0..3).forEach{presence(it,false)}}
}

class AllAbandonedTests {
    @Test fun `single disconnect reconnect and single abandonment preserve match`() {
        val f=AbandonFixture();f.presence(0,false);f.at(179);f.presence(0,true)
        assertEquals(ConnectionState.CONNECTED,f.state().match.participants[0].connectionState)
        f.at(200);f.presence(0,false);f.at(380);f.presence(0,false)
        assertEquals(ConnectionState.ABANDONED,f.state().match.participants[0].connectionState)
        assertEquals(MatchStatus.IN_PROGRESS,f.state().match.status);assertNull(f.service.abandon(f.id))
    }
    @Test fun `four reconnect within grace and boundary ordering`() {
        val f=AbandonFixture();f.disconnectAll();f.at(179);(0..3).forEach{f.presence(it,true)}
        val seat=f.state().match.currentSeat!!
        f.service.command("m5-p$seat",OnlineCommand(1,"resumed",f.id,OnlineCommandType.PLAY_TILE,f.state().hands.getValue("$seat").first(),ChainEnd.RIGHT))
        assertEquals(1,f.state().board.size)
        f.at(180);f.expireAll() // New disconnect grace, not expired previous grace.
        assertEquals(MatchStatus.IN_PROGRESS,f.state().match.status)
        assertTrue(f.state().match.participants.all{it.connectionState==ConnectionState.DISCONNECTED})
        val g=AbandonFixture();g.disconnectAll();g.at(180);(0..3).forEach{g.presence(it,true)}
        assertEquals(MatchStatus.CANCELLED,g.state().match.status)
        assertTrue(g.state().match.participants.all{it.connectionState==ConnectionState.ABANDONED})
    }
    @Test fun `staggered grace cancels only on last seat and terminal evaluation is idempotent`() {
        val f=AbandonFixture()
        for(i in 0..3){f.at(i*10L);f.presence(i,false)}
        for(i in 0..2){f.at(180+i*10L);f.presence(i,false);assertEquals(MatchStatus.IN_PROGRESS,f.state().match.status)}
        f.at(210);val terminal=f.presence(3,false)!!.write!!
        assertEquals(MatchStatus.CANCELLED,terminal.state.match.status)
        assertEquals(MatchFinishReason.CANCELLED,terminal.state.match.result!!.finishReason)
        assertNull(terminal.state.match.result!!.winner);assertEquals(f.initial.match.score,terminal.state.match.score)
        assertNull(terminal.state.turnDeadlineAt);assertNull(terminal.state.turnStartedAt);assertNull(terminal.state.match.currentSeat)
        assertTrue(terminal.histories.isEmpty());assertEquals(1,terminal.events.count{it.payload is MatchFinished})
        val s=f.state();repeat(5){assertNull(f.service.abandon(f.id));assertNull(f.presence(3,true))}
        assertEquals(s,f.state())
    }
    @Test fun `legacy all abandoned states cancel in playing and between rounds preserving partial audit`() {
        for(phase in listOf(OnlinePhase.PLAYING,OnlinePhase.ROUND_FINISHED)) {
            val f=AbandonFixture();f.disconnectAll();f.at(180)
            val old=f.state().copy(phase=phase,turnStartedAt=f.clock.instant(),turnDeadlineAt=f.clock.instant().plusSeconds(60),match=f.state().match.copy(participants=f.state().match.participants.map{it.copy(connectionState=ConnectionState.ABANDONED,abandonedAt=f.clock.instant())}))
            val write=f.engine.abandon(old,"legacy",f.clock.instant())!!
            OnlineWrites.validate(old,write,"legacy")
            assertEquals(old.hands,write.state.hands);assertEquals(old.board,write.state.board);assertEquals(old.round,write.state.round)
            assertTrue(write.histories.isEmpty());assertTrue(write.rounds.isEmpty())
            assertNull(write.state.turnDeadlineAt);assertNull(write.state.turnStartedAt)
            assertEquals(listOf(MatchEventType.MATCH_FINISHED),write.events.map{it.type})
            val source=object:ReplaySource {
                override fun match(id:String)=write.state.match
                override fun events(id:String,after:Long,limit:Int):List<MatchEvent> = error("Cancelled replay must not read events")
                override fun round(id:String,number:Int):MatchRound?=error("Cancelled replay must not read rounds")
            }
            assertEquals(ReplayAvailability.ACTIVE_MATCH,assertFailsWith<ReplayFailure>{ReplayService(source).manifest("m5-p0",f.id)}.reason)
        }
    }
}
