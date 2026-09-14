package com.teamfho.domino.online

import com.teamfho.domino.catalog.*
import com.teamfho.domino.match.*
import org.junit.jupiter.api.Test
import java.time.Clock
import java.time.Instant
import java.time.ZoneOffset
import java.util.UUID
import java.util.concurrent.Executors
import kotlin.test.*

class MemoryOnlineRepository: OnlineRepository {
    val states=mutableMapOf<String,OnlineState>();val stream=mutableMapOf<String,MutableList<MatchEvent>>()
    val receipts=mutableMapOf<Pair<String,String>,OnlineReceipt>();val histories=mutableMapOf<String,PlayerMatchHistory>()
    @Synchronized override fun create(state: OnlineState) {require(state.match.matchId !in states);states[state.match.matchId]=copy(state)}
    @Synchronized override fun read(matchId: String)=states[matchId]?.let(::copy)
    @Synchronized override fun events(matchId: String,after: Long)=stream[matchId].orEmpty().filter {it.sequence>after}.take(500)
    @Synchronized override fun transact(matchId: String,expectedSequence: Long,commandId: String,fingerprint: String,transition:(OnlineState)->OnlineWrite): OnlineCommit {
        receipts[matchId to commandId]?.let {return OnlineWrites.prior(it,fingerprint)}
        val before=read(matchId)?:throw OnlineFailure(OnlineError.MATCH_NOT_FOUND)
        checkOnline(before.match.lastSequence==expectedSequence,OnlineError.STALE_COMMAND)
        val write=transition(before);OnlineWrites.validate(before,write,commandId)
        states[matchId]=copy(write.state);stream.getOrPut(matchId){mutableListOf()}.addAll(write.events);histories.putAll(write.histories)
        val r=OnlineReceipt(fingerprint,expectedSequence+1,write.state.match.lastSequence);receipts[matchId to commandId]=r
        return OnlineCommit(write,r)
    }
    private fun copy(s: OnlineState)=MatchCodec.copy(s,OnlineState::class.java)
}
class OnlineFixture(seed: Long=2) {
    val repo=MemoryOnlineRepository();var publication=GameCatalogV2Publisher.canonical()
    val catalog=GameCatalogService(GameCatalogRepository {publication})
    val random=java.util.Random(seed);val engine=OnlineEngine(OnlineRandom {random.nextInt(it)})
    val service=OnlineMatchService(catalog,repo,engine)
    val id=service.create("p0","DUEL_1V1").publicState.matchId
    fun state()=repo.read(id)!!
    fun join(){service.join("p1",id,"join")}
    fun command(seat: Int,type: OnlineCommandType,tile: DominoPips?=null,end: ChainEnd?=null,candidate: Int?=null,even: Boolean?=null,id: String=UUID.randomUUID().toString())=
        service.command("p$seat",OnlineCommand(1,id,this.id,type,tile,end,candidate,even))
    fun start() {
        join()
        repeat(50) {
            val s=state();if(s.phase==OnlinePhase.PLAYING)return
            val st=s.starter!!
            if(st.method==StarterMethod.EVEN_ODD_GUESS)command(st.guessingSeat,OnlineCommandType.SUBMIT_EVEN_ODD_GUESS,even=true)
            else {val seat=(0..1).first {it.toString() !in st.selections};command(seat,OnlineCommandType.SELECT_STARTER_TILE,candidate=(0..1).first {it !in st.selections.values})}
        }
        error("starter did not resolve")
    }
    fun move() {
        val s=state();if(s.phase==OnlinePhase.ROUND_FINISHED) {command(0,OnlineCommandType.NEXT_ROUND);return}
        val seat=s.match.currentSeat!!
        val move=s.hands.getValue(seat.toString()).flatMap {t->ChainEnd.entries.filter {OnlineEngine.fits(s,t,it)}.map {t to it}}.firstOrNull()
        if(move==null)command(seat,OnlineCommandType.PASS)else command(seat,OnlineCommandType.PLAY_TILE,move.first,move.second)
    }
}
class OnlineMatchTests {
    @Test fun `owned nonmatching tile rejected and successful move resets passes`() {
        val f=OnlineFixture();f.start();val base=f.state();val seat=base.match.currentSeat!!
        val t=base.hands.getValue(seat.toString()).first();val pip=(0..9).first {it!=t.sideA&&it!=t.sideB}
        val blocked=base.copy(board=listOf(BoardPlacement(DominoPips(pip,pip),ChainEnd.RIGHT,1-seat)))
        assertEquals(OnlineError.ILLEGAL_MOVE,assertFailsWith<OnlineFailure>{f.engine.command(blocked,"p$seat",OnlineCommand(1,"illegal",f.id,OnlineCommandType.PLAY_TILE,t,ChainEnd.RIGHT),Instant.now())}.code)
        val legal=base.copy(consecutivePasses=1)
        val result=f.engine.command(legal,"p$seat",OnlineCommand(1,"reset",f.id,OnlineCommandType.PLAY_TILE,t,ChainEnd.RIGHT),Instant.now())
        assertEquals(0,result.state.consecutivePasses)
    }
    @Test fun `blocked unequal pips examples have no finish bonus`() {
        val f=OnlineFixture();f.start();val base=f.state()
        for(lowSeat in 0..1) {
            val low=listOf(DominoPips(9,9));val high=listOf(DominoPips(8,9),DominoPips(5,5))
            val state=base.copy(match=base.match.copy(currentSeat=0),hands=mapOf("0" to if(lowSeat==0)low else high,"1" to if(lowSeat==1)low else high),
                board=listOf(BoardPlacement(DominoPips(0,0),ChainEnd.RIGHT,1)),consecutivePasses=1)
            val result=f.engine.command(state,"p0",OnlineCommand(1,"blocked",f.id,OnlineCommandType.PASS),Instant.now()).state
            assertEquals(lowSeat,result.round!!.winnerSeat);assertEquals(27,result.round!!.scoreAwarded)
        }
    }
    @Test fun `high tile tie repeats with virtual tiles and wrong even odd actor rejected`() {
        val f=OnlineFixture();f.join();val base=f.state()
        val tie=base.copy(starter=OnlineStarter(StarterMethod.HIGH_TILE_SELECTION,listOf(DominoPips(0,9),DominoPips(4,5)),mapOf("0" to 0),1))
        val result=f.engine.command(tie,"p1",OnlineCommand(1,"tie",f.id,OnlineCommandType.SELECT_STARTER_TILE,candidate=1),Instant.now()).state
        assertEquals(OnlinePhase.STARTER_SELECTION,result.phase);assertEquals(2,result.starter!!.attempt);assertTrue(result.hands.isEmpty())
        val guess=base.copy(starter=OnlineStarter(StarterMethod.EVEN_ODD_GUESS,listOf(DominoPips(4,5)),guessingSeat=1))
        assertEquals(OnlineError.INVALID_STARTER_ACTION,assertFailsWith<OnlineFailure>{f.engine.command(guess,"p0",OnlineCommand(1,"bad",f.id,OnlineCommandType.SUBMIT_EVEN_ODD_GUESS,even=true),Instant.now())}.code)
        val won=f.engine.command(guess,"p1",OnlineCommand(1,"good",f.id,OnlineCommandType.SUBMIT_EVEN_ODD_GUESS,even=false),Instant.now()).state
        assertEquals(1,won.round!!.starterSeat)
    }
    @Test fun `catalog changes do not alter active online rules and next starter is prior winner`() {
        val f=OnlineFixture();f.start();val frozen=f.state().match.ruleSnapshot
        f.publication=GameCatalogSeed.canonical()
        repeat(60){if(f.state().phase==OnlinePhase.PLAYING)f.move()}
        val before=f.state();assertEquals(OnlinePhase.ROUND_FINISHED,before.phase)
        val replacementCatalog=GameCatalogService(GameCatalogRepository {GameCatalogSeed.canonical()})
        assertTrue(replacementCatalog.resolve()!!.modes.none {it.key=="DUEL_1V1"})
        OnlineMatchService(replacementCatalog,f.repo,f.engine).command("p1",OnlineCommand(1,"next-frozen",f.id,OnlineCommandType.NEXT_ROUND))
        assertEquals(before.round!!.winnerSeat,f.state().round!!.starterSeat)
        assertEquals(frozen,f.state().match.ruleSnapshot)
    }
    @Test fun `cross player raced requests cannot consume two turns from one revision`() {
        val f=OnlineFixture();f.start();val before=f.state();val seat=before.match.currentSeat!!
        val pool=Executors.newFixedThreadPool(2);val barrier=java.util.concurrent.CyclicBarrier(2)
        try {
            val results=pool.invokeAll((0..1).map {actor->java.util.concurrent.Callable {
                barrier.await();runCatching {
                    val c=OnlineCommand(1,"cross-$actor",f.id,OnlineCommandType.PLAY_TILE,before.hands.getValue(actor.toString()).first(),ChainEnd.RIGHT)
                    f.repo.transact(f.id,before.match.lastSequence,c.commandId,"$actor"){f.engine.command(it,"p$actor",c,Instant.now())}
                }
            }}).map {it.get()}
            assertEquals(1,results.count {it.isSuccess});assertTrue(results[seat].isSuccess)
            assertEquals(1,f.state().board.size)
        } finally {pool.shutdownNow()}
    }
    @Test fun `create join reject duplicate and third seat`() {
        val f=OnlineFixture();assertEquals(MatchExecutionMode.ONLINE,f.state().match.executionMode)
        assertEquals(OnlineError.SAME_PLAYER,assertFailsWith<OnlineFailure>{f.service.join("p0",f.id,"bad")}.code)
        f.join();assertEquals(2,f.state().match.participants.size)
        assertEquals(OnlineError.MATCH_FULL,assertFailsWith<OnlineFailure>{f.service.join("p2",f.id,"third")}.code)
        assertEquals(2,f.state().match.participants.size)
    }
    @Test fun `both starter methods resolve without public hidden values`() {
        val methods=mutableSetOf<StarterMethod>()
        for(seed in listOf(0L,4096L,99999L,123456789L)) {
            val f=OnlineFixture(seed);f.join();methods+=f.state().starter!!.method
            val public=GameCatalogCodec.mapper.writeValueAsString(f.service.snapshot("p0",f.id))
            assertFalse(public.contains("sideA"));assertFalse(public.contains("sideB"))
            assertFalse(GameCatalogCodec.mapper.writeValueAsString(f.service.events("p0",f.id,0)).contains("sideA"))
        }
        assertEquals(StarterMethod.entries.toSet(),methods)
    }
    @Test fun `deal has 55 unique tiles 10 each and 35 reserve`() {
        val f=OnlineFixture();f.start();val s=f.state()
        assertEquals(listOf(10,10),s.hands.values.map {it.size});assertEquals(35,s.reserve.size)
        assertEquals(55,(s.hands.values.flatten()+s.reserve).toSet().size)
        assertEquals(OnlinePhase.PLAYING,s.phase);assertEquals(s.turnStartedAt!!.plusSeconds(60),f.service.snapshot("p0",f.id).publicState.turnDeadline)
    }
    @Test fun `snapshots and resync isolate caller hand with contiguous redacted sequences`() {
        val f=OnlineFixture();f.start()
        for(seat in 0..1) {
            val snap=f.service.snapshot("p$seat",f.id);assertEquals(f.state().hands[seat.toString()],snap.privateState.hand)
            assertFalse(GameCatalogCodec.mapper.writeValueAsString(snap.publicState).contains("sideA"))
            val page=f.service.events("p$seat",f.id,0)
            assertEquals((1L..f.state().match.lastSequence).toList(),page.events.map {it.sequence})
            page.events.mapNotNull {it.event}.forEach {if(it.payload is HandDealt)assertEquals(seat,it.payload.seat)}
            assertTrue(page.events.any {it.type=="SEQUENCE_ADVANCED"&&it.event==null})
        }
        assertEquals(OnlineError.NOT_PARTICIPANT,assertFailsWith<OnlineFailure>{f.service.snapshot("other",f.id)}.code)
        assertFailsWith<OnlineFailure>{f.service.events("other",f.id,0)}
    }
    @Test fun `invalid commands leave state and sequence unchanged`() {
        val f=OnlineFixture();f.start();val before=f.state();val seat=before.match.currentSeat!!
        fun reject(code: OnlineError, action:()->Unit) {assertEquals(code,assertFailsWith<OnlineFailure>(block=action).code);assertEquals(before,f.state())}
        reject(OnlineError.NOT_YOUR_TURN){f.command(1-seat,OnlineCommandType.PASS)}
        reject(OnlineError.PASS_NOT_ALLOWED){f.command(seat,OnlineCommandType.PASS)}
        reject(OnlineError.TILE_NOT_IN_HAND){f.command(seat,OnlineCommandType.PLAY_TILE,before.reserve.first(),ChainEnd.RIGHT)}
        reject(OnlineError.INVALID_COMMAND){f.command(seat,OnlineCommandType.PASS,even=true)}
    }
    @Test fun `duplicate command durable receipt conflict and UID binding`() {
        val f=OnlineFixture();f.start();val s=f.state();val seat=s.match.currentSeat!!;val tile=s.hands.getValue(seat.toString()).first()
        val first=f.command(seat,OnlineCommandType.PLAY_TILE,tile,ChainEnd.RIGHT,id="same")
        val after=f.state();val second=f.command(seat,OnlineCommandType.PLAY_TILE,tile,ChainEnd.RIGHT,id="same")
        assertEquals(first.receipt,second.receipt);assertNull(second.write);assertEquals(after,f.state())
        assertEquals(OnlineError.COMMAND_ID_CONFLICT,assertFailsWith<OnlineFailure>{f.command(seat,OnlineCommandType.PASS,id="same")}.code)
        assertEquals(OnlineError.COMMAND_ID_CONFLICT,assertFailsWith<OnlineFailure>{f.command(1-seat,OnlineCommandType.PLAY_TILE,tile,ChainEnd.RIGHT,id="same")}.code)
    }
    @Test fun `server frozen revision prevents two raced moves and out of turn actor`() {
        val f=OnlineFixture();f.start();val before=f.state();val seat=before.match.currentSeat!!
        val pool=Executors.newFixedThreadPool(12)
        try {
            val results=pool.invokeAll((0..11).map {n->java.util.concurrent.Callable {
                runCatching {val uid="p$seat";val c=OnlineCommand(1,"race$n",f.id,OnlineCommandType.PLAY_TILE,before.hands.getValue(seat.toString())[n%10],ChainEnd.RIGHT)
                    f.repo.transact(f.id,before.match.lastSequence,c.commandId,OnlineMatchService.fingerprint(uid,c.toString())){f.engine.command(it,uid,c,Instant.now())}}
            }}).map {it.get()}
            assertEquals(1,results.count {it.isSuccess});assertEquals(1,f.state().board.size)
        } finally {pool.shutdownNow()}
    }
    @Test fun `complete matches scoring history strict sequence and local frozen catalog`() {
        for(seed in 1L..12L) {
            val f=OnlineFixture(seed);f.start();val frozen=f.state().match.ruleSnapshot
            repeat(600) {if(f.state().phase!=OnlinePhase.MATCH_FINISHED)f.move()}
            val s=f.state();assertEquals(MatchStatus.FINISHED,s.match.status);assertTrue(s.match.score.max()>=150)
            assertEquals(frozen,s.match.ruleSnapshot);assertEquals(2,f.repo.histories.size)
            assertEquals(setOf(HistoryResult.WIN,HistoryResult.LOSS),f.repo.histories.values.map {it.result}.toSet())
            val events=f.repo.stream.getValue(f.id);assertEquals((1L..s.match.lastSequence).toList(),events.map {it.sequence})
            for(e in events)if(e.payload is RoundFinished) {
                val r=e.payload;val opponent=r.remainingPips[1-r.winnerSeat!!]
                assertEquals(when(r.finishType){FinishType.NORMAL->opponent+10;FinishType.CAPICUA->opponent*2+10;FinishType.BLOCKED->opponent},r.scoreAwarded)
            }
        }
    }
    @Test fun `M3 required normal capicua and blocked examples`() {
        val f=OnlineFixture();f.start();val base=f.state()
        fun fixture(a: List<DominoPips>,b: List<DominoPips>,starter: Int=0,pass: Int=0,board: List<BoardPlacement> = listOf(BoardPlacement(DominoPips(1,2),ChainEnd.RIGHT,1)))=
            base.copy(match=base.match.copy(currentSeat=0,score=listOf(0,0)),hands=mapOf("0" to a,"1" to b),board=board,consecutivePasses=pass,round=base.round!!.copy(starterSeat=starter))
        val cap=fixture(listOf(DominoPips(1,2)),listOf(DominoPips(9,9),DominoPips(7,7)))
        val c=OnlineCommand(1,"end",f.id,OnlineCommandType.PLAY_TILE,DominoPips(1,2),ChainEnd.RIGHT)
        val result=f.engine.command(cap,"p0",c,Instant.now());assertEquals(74,result.state.match.score[0]);assertEquals(FinishType.CAPICUA,result.state.round!!.finishType)
        val normal=cap.copy(board=listOf(BoardPlacement(DominoPips(0,2),ChainEnd.RIGHT,1)))
        assertEquals(42,f.engine.command(normal,"p0",c,Instant.now()).state.match.score[0])
        for(starter in 0..1) {
            val tied=fixture(listOf(DominoPips(9,9),DominoPips(3,3)),listOf(DominoPips(8,8),DominoPips(4,4)),starter,1)
            val end=f.engine.command(tied,"p0",OnlineCommand(1,"pass",f.id,OnlineCommandType.PASS),Instant.now()).state
            assertEquals(starter,end.round!!.winnerSeat);assertEquals(24,end.match.score[starter])
        }
    }
}
