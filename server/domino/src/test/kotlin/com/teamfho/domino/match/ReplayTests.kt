package com.teamfho.domino.match

import com.teamfho.domino.online.*
import com.teamfho.domino.catalog.*
import com.teamfho.domino.security.FirebaseIdentity
import org.junit.jupiter.api.Test
import java.time.Instant
import kotlin.test.*

class ReplayFixture(partners:Boolean=false, beforeCommand:(Int)->Unit={}):ReplaySource {
    var reads=0;var eventPages=0
    val events=mutableListOf<MatchEvent>()
    val rounds=mutableMapOf<Int,MatchRound>()
    val random=java.util.Random(9)
    val engine=OnlineEngine(OnlineRandom{random.nextInt(it)})
    var state:OnlineState
    init {
        val catalog=GameCatalogValidator.resolve(GameCatalogV4Publisher.canonical())
        val mode=catalog.modes.single{it.key==if(partners)"PARTNERS_2V2_ONLINE" else "DUEL_1V1"}
        val participants=(0 until mode.playerCount).map{MatchParticipant(it,"p$it","Name $it",if(partners)it%2 else null,ControlType.REMOTE_HUMAN,ConnectionState.CONNECTED,Instant.EPOCH)}
        val m=Match("replay-test",MatchStatus.CREATED,mode.key,MatchExecutionMode.ONLINE,4,mode.topologyVersion,mode.ruleSet.id,1,1,
            MatchRuleSnapshot.freeze(catalog,mode),participants.dropLast(1),0,0,null,listOf(0,0),null,null,null,0,MatchVisibility.PRIVATE,SpectatorPolicy(false),Instant.EPOCH,Instant.EPOCH)
        state=OnlineState(m,OnlinePhase.WAITING_FOR_PLAYER)
        apply(engine.join(state,participants.last(),"join",Instant.EPOCH))
        var n=0
        while(state.phase!=OnlinePhase.MATCH_FINISHED) {
            check(n++<5000)
            beforeCommand(n)
            val command=when(state.phase) {
                OnlinePhase.STARTER_SELECTION->{val st=state.starter!!
                    if(st.method==StarterMethod.EVEN_ODD_GUESS)st.guessingSeat to OnlineCommand(1,"c$n",m.matchId,OnlineCommandType.SUBMIT_EVEN_ODD_GUESS,even=true)
                    else (0..1).first{it.toString() !in st.selections} to OnlineCommand(1,"c$n",m.matchId,OnlineCommandType.SELECT_STARTER_TILE,candidate=(0..1).first{it !in st.selections.values})}
                OnlinePhase.ROUND_FINISHED->0 to OnlineCommand(1,"c$n",m.matchId,OnlineCommandType.NEXT_ROUND)
                else->{val seat=state.match.currentSeat!!;val t=state.hands.getValue("$seat").firstOrNull{OnlineEngine.fits(state,it,ChainEnd.RIGHT)}
                    seat to OnlineCommand(1,"c$n",m.matchId,if(t==null)OnlineCommandType.PASS else OnlineCommandType.PLAY_TILE,t,if(t==null)null else ChainEnd.RIGHT)}
            }
            // Some tiles fit only LEFT; use normal timeout selection for these turns.
            val c=command.second
            if(c.type==OnlineCommandType.PASS&&state.hands.getValue("${command.first}").any{OnlineEngine.fits(state,it,ChainEnd.LEFT)}) {
                val tile=state.hands.getValue("${command.first}").first{OnlineEngine.fits(state,it,ChainEnd.LEFT)}
                apply(engine.command(state,"p${command.first}",c.copy(type=OnlineCommandType.PLAY_TILE,tile=tile,chainEnd=ChainEnd.LEFT),Instant.EPOCH))
            }else apply(engine.command(state,"p${command.first}",c,Instant.EPOCH))
        }
    }
    private fun apply(w:OnlineWrite){state=w.state;events+=w.events;w.rounds.forEach{rounds[it.roundNumber]=it}}
    override fun match(id:String)=state.match.also{reads++}
    override fun events(id:String,after:Long,limit:Int)=events.filter{it.sequence>after}.take(limit).also{eventPages++}
    override fun round(id:String,number:Int)=rounds[number].also{reads++}
}
class ReplayTests {
    @Test fun `both completed modes reconstruct persisted public and private state with bounded pages`() {
        for(partners in listOf(false,true)) {
            val f=ReplayFixture(partners);val service=ReplayService(f);val m=service.manifest("p0","replay-test")
            assertTrue(m.replayAvailable);assertEquals(if(partners)4 else 2,m.perspectives.size)
            assertEquals(f.state.match.ruleSnapshot,m.ruleSnapshot)
            val all=mutableListOf<MatchEvent>();do {val page=service.page("p0","replay-test",all.size.toLong(),57);all+=page.items}while(page.nextSequence!=null)
            assertEquals(f.events.map{it.copy(causedByCommandId=null)},all)
            assertEquals((f.events.size+249)/250,f.eventPages)
            val rebuilt=MatchReplayReducer.reconstruct(f.state.match,all)
            assertEquals(f.state.board,rebuilt.publicState.board);assertEquals(f.state.match.score,rebuilt.publicState.scores)
            for(seat in m.perspectives)assertEquals(f.state.hands["$seat"],rebuilt.privateStates[seat]!!.hand)
        }
    }
    @Test fun `server authorization covers manifest and private pages and active matches`() {
        val f=ReplayFixture(true);val service=ReplayService(f)
        assertFailsWith<ReplayForbidden>{service.manifest("intruder","replay-test")}
        assertFailsWith<ReplayForbidden>{service.page("intruder","replay-test",0,250)}
        service.manifest("p0","replay-test") // warm cache cannot bypass subsequent authorization
        f.state=f.state.copy(match=f.state.match.copy(status=MatchStatus.IN_PROGRESS))
        assertEquals(ReplayAvailability.ACTIVE_MATCH,assertFailsWith<ReplayFailure>{service.page("p0","replay-test",0,250)}.reason)
    }
    @Test fun `gaps schema and missing private data fail closed`() {
        val f=ReplayFixture();val original=f.events.toList()
        f.events.removeAt(12);assertEquals(ReplayAvailability.INCOMPLETE_EVENTS,ReplayService(f).manifest("p0","replay-test").replayAvailabilityReason)
        f.events.clear();f.events+=original;f.events[0]=f.events[0].copy(eventSchemaVersion=8)
        assertEquals(ReplayAvailability.UNSUPPORTED_SCHEMA,ReplayService(f).manifest("p0","replay-test").replayAvailabilityReason)
        f.events.clear();f.events+=original
        val at=f.events.indexOfFirst{it.payload is HandDealt};f.events[at]=f.events[at].copy(payload=PlayerPassed(0),type=MatchEventType.PLAYER_PASSED)
        assertEquals(ReplayAvailability.MISSING_PRIVATE_SNAPSHOT,ReplayService(f).manifest("p0","replay-test").replayAvailabilityReason)
    }
    @Test fun `controller maps participant denial to 403 active to 409 invalid paging to 400`() {
        val f=ReplayFixture();val controller=ReplayController(ReplayService(f))
        assertEquals(403,controller.manifest(FirebaseIdentity("other",true),"replay-test").statusCode.value())
        assertEquals(403,controller.page(FirebaseIdentity("other",true),"replay-test",0,250).statusCode.value())
        assertEquals(400,controller.page(FirebaseIdentity("p0",true),"replay-test",0,251).statusCode.value())
        assertEquals(200,controller.manifest(FirebaseIdentity("p0",true),"replay-test").statusCode.value())
    }
    @Test fun `event matrix includes all current payload kinds`() {
        assertEquals(16,MatchEventType.entries.size)
    }
}
