package com.teamfho.domino.online

import com.teamfho.domino.match.*
import com.teamfho.domino.realtime.PresenceStore
import org.junit.jupiter.api.Test
import java.time.*
import java.util.concurrent.*
import kotlin.test.*

class OnlineTurnTests {
    @Test fun `new worker discovers persisted expired turn and refreshes bounded index`() {
        val f=Fixture();f.at(65);var refreshed=0
        val repo=object:OnlineRepository by f.f.repo {
            override fun due(now:Instant)=if(f.s().turnDeadlineAt!!<=now)listOf(f.id)else emptyList()
            override fun refreshDiscovery(matchId:String,now:Instant){refreshed++}
        }
        val presence=object:PresenceStore {
            override fun touch(uid:String,connectionId:String,serverId:String){};override fun remove(uid:String,connectionId:String){}
            override fun onlinePlayers()=2L;override fun connectionCount(uid:String)=1L
        }
        OnlineTurnWorker(repo,f.service,presence).processDue(f.clock.instant())
        OnlineTurnWorker(repo,f.service,presence).processDue(f.clock.instant())
        assertEquals(1,refreshed);assertEquals(1,f.events().count {it.payload is TurnTimeout})
    }
    @Test fun `lifecycle notification counts two one zero sockets without duplicate participant events`() {
        val f=Fixture();var sockets=2L
        val repo=object:OnlineRepository by f.f.repo {override fun activeFor(uid:String)=listOf(f.id)}
        val presence=object:PresenceStore {
            override fun touch(uid:String,connectionId:String,serverId:String){};override fun remove(uid:String,connectionId:String){}
            override fun onlinePlayers()=2L;override fun connectionCount(uid:String)=if(uid=="p0")sockets else 1L
        }
        val worker=OnlineTurnWorker(repo,f.service,presence)
        for(n in listOf(2L,1L,0L,0L,1L,2L)){sockets=n;worker.connectionChanged("p0");worker.processDue(f.clock.instant())}
        assertEquals(1,f.events().count {it.payload is PlayerDisconnected});assertEquals(1,f.events().count {it.payload is PlayerReconnected})
    }
    @Test fun `late command rejected at abandonment boundary before worker runs`() {
        val f=Fixture();val seat=f.s().match.currentSeat!!;f.presence(seat,false);f.at(180)
        assertEquals(OnlineError.PLAYER_ABANDONED,assertFailsWith<OnlineFailure>{f.service.command("p$seat",OnlineCommand(1,"late-before-worker",f.id,OnlineCommandType.PASS))}.code)
    }
    class Time(var value: Instant):Clock(){override fun instant()=value;override fun getZone()=ZoneOffset.UTC;override fun withZone(zone:ZoneId)=this}
    class Fixture {
        val f=OnlineFixture().apply {start()};val clock=Time(f.state().turnStartedAt!!)
        val service=OnlineMatchService(f.catalog,f.repo,f.engine,clock)
        val id=f.id
        fun s()=f.state()
        fun at(seconds:Long){clock.value=f.repo.stream.getValue(id).first {it.payload is TurnStarted}.createdAt.plusSeconds(seconds)}
        fun presence(seat:Int,online:Boolean)=service.connection(id,"p$seat"){online}
        fun events()=f.repo.stream.getValue(id)
    }
    @Test fun `deadline frozen from policy persisted and starter has no deadline`() {
        val f=Fixture();assertEquals(f.s().turnStartedAt!!.plusSeconds(60),f.s().turnDeadlineAt)
        val event=f.events().last().payload as TurnStarted
        assertEquals(1,event.turnNumber);assertEquals(f.s().turnDeadlineAt,event.turnDeadlineAt)
        assertEquals(f.s(),MatchCodec.copy(f.s(),OnlineState::class.java))
        val notStarted=OnlineFixture();notStarted.join();assertNull(notStarted.state().turnDeadlineAt)
    }
    @Test fun `59 seconds accepted old timeout does nothing`() {
        val f=Fixture();f.at(59);val s=f.s();val seat=s.match.currentSeat!!
        f.service.command("p$seat",OnlineCommand(1,"before",f.id,OnlineCommandType.PLAY_TILE,s.hands.getValue("$seat").first(),ChainEnd.RIGHT))
        f.at(60);assertNull(f.service.timeout(f.id));assertEquals(1,f.s().board.size)
        assertEquals(f.clock.instant().plusSeconds(59),f.s().turnDeadlineAt)
    }
    @Test fun `exact deadline rejects client with typed error before timeout`() {
        val f=Fixture();val before=f.s();f.at(60)
        assertEquals(OnlineError.TURN_EXPIRED,assertFailsWith<OnlineFailure>{f.service.command("p${before.match.currentSeat}",OnlineCommand(1,"late",f.id,OnlineCommandType.PASS))}.code)
        assertEquals(before,f.s())
    }
    @Test fun `idle timeout deterministic hand order then LEFT and audit final sequence`() {
        val f=Fixture();val before=f.s();f.at(59);assertNull(f.service.timeout(f.id));f.at(60)
        f.service.timeout(f.id);val after=f.s();val chosen=before.hands.getValue("${before.match.currentSeat}").first()
        assertEquals(chosen,after.board.single().tile);assertEquals(ChainEnd.LEFT,after.board.single().chainEnd)
        assertEquals(1,f.events().count {it.payload is TurnTimeout});val auto=f.events().single {it.payload is AutoPlayed}.payload as AutoPlayed
        assertEquals(after.match.lastSequence,auto.resultingSequence);assertEquals(AutoPlayReason.TURN_TIMEOUT,auto.reason)
        assertEquals(1,after.board.size);assertEquals(2,after.match.currentTurnNumber)
    }
    @Test fun `two workers race claims exactly once`() {
        val f=Fixture();f.at(60);val pool=Executors.newFixedThreadPool(2);val start=CyclicBarrier(2)
        try {pool.invokeAll((0..1).map {Callable {start.await();runCatching {OnlineMatchService(f.f.catalog,f.f.repo,f.f.engine,f.clock).timeout(f.id)}}}).forEach {it.get()}}
        finally {pool.shutdownNow()}
        assertEquals(1,f.events().count {it.payload is TurnTimeout});assertEquals(1,f.events().count {it.payload is AutoPlayed});assertEquals(1,f.s().board.size)
    }
    @Test fun `player vs timeout at boundary one terminal action`() {
        val f=Fixture();val before=f.s();f.at(60);val pool=Executors.newFixedThreadPool(2)
        val c=OnlineCommand(1,"race-player",f.id,OnlineCommandType.PLAY_TILE,before.hands.getValue("${before.match.currentSeat}").first(),ChainEnd.LEFT)
        try {pool.invokeAll(listOf(Callable {runCatching {f.service.command("p${before.match.currentSeat}",c)}},Callable {runCatching {f.service.timeout(f.id)}})).forEach {it.get()}}
        finally {pool.shutdownNow()}
        assertEquals(1,f.s().board.size);assertEquals(1,f.events().count {it.payload is TurnTimeout})
    }
    @Test fun `disconnect and reconnect keep same deadline with 15 seconds remaining`() {
        val f=Fixture();val seat=f.s().match.currentSeat!!;val deadline=f.s().turnDeadlineAt
        f.at(20);f.presence(seat,false);val p=f.s().match.participants[seat]
        assertEquals(f.clock.instant(),p.disconnectedAt);assertEquals(f.clock.instant().plusSeconds(180),p.reconnectDeadlineAt)
        f.at(45);f.presence(seat,true);assertEquals(deadline,f.s().turnDeadlineAt)
        assertEquals(15,Duration.between(f.clock.instant(),deadline).seconds)
        assertEquals(ConnectionState.CONNECTED,f.s().match.participants[seat].connectionState)
    }
    @Test fun `last socket semantics duplicate disconnect reconnect no duplicate events`() {
        val f=Fixture();val seq=f.s().match.lastSequence
        assertNull(f.service.connection(f.id,"p0"){2L>0});assertEquals(seq,f.s().match.lastSequence)
        assertNull(f.service.connection(f.id,"p0"){1L>0});f.service.connection(f.id,"p0"){0L>0}
        f.service.connection(f.id,"p0"){false};assertEquals(1,f.events().count {it.payload is PlayerDisconnected})
        f.presence(0,true);f.presence(0,true);assertEquals(1,f.events().count {it.payload is PlayerReconnected})
    }
    @Test fun `unknown Redis presence cannot invent disconnect or corrupt deadline`() {
        val f=Fixture();val before=f.s();assertNull(f.service.connection(f.id,"p0"){null});assertEquals(before,f.s())
        f.at(60);f.service.timeout(f.id);assertEquals(1,f.s().board.size)
    }
    @Test fun `disconnect timeout and reconnect preserve already played tile`() {
        val f=Fixture();val seat=f.s().match.currentSeat!!;f.at(20);f.presence(seat,false);f.at(60);f.service.timeout(f.id)
        val board=f.s().board;val deadline=f.s().turnDeadlineAt
        f.at(75);f.presence(seat,true);assertEquals(board,f.s().board);assertEquals(deadline,f.s().turnDeadlineAt)
        assertEquals(1-seat,f.s().match.currentSeat);assertEquals(9,f.service.snapshot("p$seat",f.id).privateState.hand.size)
    }
    @Test fun `179 180 abandonment boundary no penalty and late reconnect does not revive seat`() {
        val f=Fixture();f.presence(0,false);f.at(179);assertNull(f.presence(0,false));assertEquals(ConnectionState.DISCONNECTED,f.s().match.participants[0].connectionState)
        f.at(180);f.presence(0,true);assertEquals(ConnectionState.ABANDONED,f.s().match.participants[0].connectionState)
        assertEquals(f.clock.instant(),f.s().match.participants[0].abandonedAt);assertNull(f.presence(0,true))
        assertEquals(listOf(0,0),f.s().match.score);assertEquals(MatchStatus.IN_PROGRESS,f.s().match.status)
        assertEquals(1,f.events().count {it.payload is PlayerAbandoned})
        assertEquals(OnlineError.PLAYER_ABANDONED,assertFailsWith<OnlineFailure>{f.service.command("p0",OnlineCommand(1,"late",f.id,OnlineCommandType.PASS))}.code)
    }
    @Test fun `abandoned human still follows timeout scoring without bot relabel`() {
        val f=Fixture();val seat=f.s().match.currentSeat!!;f.presence(seat,false);f.at(180);f.presence(seat,false);f.service.timeout(f.id)
        assertEquals(1,f.s().board.size);assertEquals(ControlType.REMOTE_HUMAN,f.s().match.participants[seat].controlType)
    }
    @Test fun `restarted service recovers overdue persisted deadline exactly once`() {
        val f=Fixture();val deadline=f.s().turnDeadlineAt;f.at(75)
        val restarted=OnlineMatchService(f.f.catalog,f.f.repo,OnlineEngine(),f.clock)
        assertEquals(deadline,restarted.state(f.id).turnDeadlineAt);restarted.timeout(f.id);assertNull(restarted.timeout(f.id))
        assertEquals(1,f.events().count {it.payload is TurnTimeout})
    }
    @Test fun `autoplay no legal move passes and can finish blocked`() {
        val f=Fixture();val base=f.s().copy(match=f.s().match.copy(currentSeat=0),hands=mapOf("0" to listOf(DominoPips(1,1)),"1" to listOf(DominoPips(2,2))),
            board=listOf(BoardPlacement(DominoPips(0,0),ChainEnd.RIGHT,1)),consecutivePasses=1)
        val write=f.f.engine.timeout(base,"sys_pass",base.turnDeadlineAt!!)
        assertEquals(FinishType.BLOCKED,write.state.round!!.finishType);assertEquals(4,write.state.round!!.scoreAwarded)
        assertNull((write.events.single {it.payload is AutoPlayed}.payload as AutoPlayed).tile)
        assertEquals(1,write.events.count {it.payload is PlayerPassed});assertNull(write.state.turnDeadlineAt)
    }
    @Test fun `autoplay capicua can finish round match history with standard score`() {
        val f=Fixture();val base=f.s().copy(match=f.s().match.copy(currentSeat=0,score=listOf(140,0)),hands=mapOf("0" to listOf(DominoPips(1,2)),"1" to listOf(DominoPips(3,3))),
            board=listOf(BoardPlacement(DominoPips(1,2),ChainEnd.RIGHT,1)))
        val write=f.f.engine.timeout(base,"sys_win",base.turnDeadlineAt!!)
        assertEquals(FinishType.CAPICUA,write.state.round!!.finishType);assertEquals(162,write.state.match.score[0]);assertEquals(MatchStatus.FINISHED,write.state.match.status)
        assertEquals(2,write.histories.size);assertEquals(write.state.match.lastSequence,(write.events[1].payload as AutoPlayed).resultingSequence)
    }
    @Test fun `replay applies timeout action only once and connection metadata is public without hands`() {
        val f=Fixture();f.presence(0,false);f.presence(0,true);f.at(60);f.service.timeout(f.id)
        val replay=MatchReplayReducer.reconstruct(f.s().match,f.events())
        assertEquals(f.s().board,replay.publicState.board);assertEquals(f.s().turnDeadlineAt,replay.publicState.turnDeadline)
        val public=f.service.snapshot("p0",f.id).publicState
        assertFalse(MatchCodec.map(public).containsKey("hands"));assertEquals(ConnectionState.CONNECTED,public.participants[0].connectionState)
    }
    @Test fun `client cannot reserve timer id or alter deadline`() {
        val f=Fixture();assertEquals(OnlineError.INVALID_COMMAND,assertFailsWith<OnlineFailure>{f.service.command("p0",OnlineCommand(1,"sys_timeout_1_1",f.id,OnlineCommandType.PASS))}.code)
        assertFails {com.teamfho.domino.catalog.GameCatalogCodec.mapper.readValue("""{"protocolVersion":1,"commandId":"x","matchId":"x","type":"PASS","turnDeadlineAt":"2099-01-01T00:00:00Z"}""",OnlineCommand::class.java)}
    }
}
