package com.teamfho.domino.online

import com.teamfho.domino.catalog.*
import com.teamfho.domino.match.*
import org.junit.jupiter.api.Test
import java.time.Instant
import java.nio.file.Files
import java.nio.file.Path
import kotlin.test.*

class PartnersOnlineTests {
    companion object {
        fun before():OnlineState {
            val c=GameCatalogValidator.resolve(GameCatalogV4Publisher.canonical())
            val mode=c.modes.single{it.key==GameCatalogV4Publisher.KEY};val now=Instant.EPOCH
            val m=Match("m5-test",MatchStatus.CREATED,mode.key,MatchExecutionMode.ONLINE,4,mode.topologyVersion,
                mode.ruleSet.id,1,mode.ruleSet.ruleSchemaVersion,MatchRuleSnapshot.freeze(c,mode),(0..2).map(::participant),0,0,null,listOf(0,0),null,null,null,0,MatchVisibility.PRIVATE,SpectatorPolicy(false),now,now)
            return OnlineState(m,OnlinePhase.WAITING_FOR_PLAYER)
        }
        fun participant(seat:Int)=MatchParticipant(seat,"m5-p$seat","Player $seat",seat%2,ControlType.REMOTE_HUMAN,ConnectionState.CONNECTED,Instant.EPOCH)
        fun started()=OnlineEngine().join(before(),participant(3),"start",Instant.EPOCH).state
    }
    @Test fun `v4 adds only online mode and binding preserving all prior rules and modes`() {
        val old=GameCatalogV3Publisher.canonical();val next=GameCatalogV4Publisher.canonical()
        assertEquals(old,next.copy(catalogVersion=3,modes=next.modes.dropLast(1),bindings=next.bindings.dropLast(1)))
        val c=GameCatalogValidator.resolve(next);val local=c.modes.single{it.key=="PARTNERS_2V2"};val online=c.modes.single{it.key==GameCatalogV4Publisher.KEY}
        assertEquals(local.ruleSet,online.ruleSet);assertEquals(local.seatTeams,online.seatTeams)
        assertEquals(listOf(0,3,2,1),online.ruleSet.turnOrder);assertEquals(4,online.minHumans);assertEquals(4,online.maxHumans);assertFalse(online.botsAllowed)
    }
    @Test fun `four private hands no starter mini game no bot no invented turn timeout`() {
        val write=OnlineEngine().join(before(),participant(3),"start",Instant.EPOCH);val s=write.state
        OnlineWrites.validate(before(),write,"start")
        assertEquals(OnlinePhase.PLAYING,s.phase);assertNull(s.starter);assertNull(s.turnDeadlineAt)
        assertEquals(0,s.match.currentSeat);assertEquals(listOf(10,10,10,10),s.hands.values.map{it.size});assertEquals(15,s.reserve.size)
        assertEquals(55,(s.hands.values.flatten()+s.reserve).toSet().size)
        for(seat in 0..3){val view=OnlineMatchService.snapshot(s,"m5-p$seat");assertEquals(s.hands["$seat"],view.privateState.hand)
            assertEquals(4,view.publicState.participants.size);assertEquals(listOf(10,10,10,10),view.publicState.tilesRemainingPerSeat)
            val visible=write.events.map{OnlineMatchService.authorized(it,seat)}
            assertEquals(1,visible.count{it.event?.payload is HandDealt})}
    }
    @Test
    @org.junit.jupiter.api.condition.EnabledIfEnvironmentVariable(named="DOMINO_M5_PARITY",matches="true")
    fun `existing local engine differential full matches maintain every deal move pass score tie and target`() {
        val path=Path.of("../../client/Validation/Generated/M5Parity/local-traces.json")
        assertTrue(Files.exists(path),"Run client/Validation/RunM5ParityFixtures.ps1 before this suite")
        val cases=GameCatalogCodec.mapper.readTree(Files.readString(path));assertEquals(100,cases.size())
        fun tiles(n:tools.jackson.databind.JsonNode)=(0 until n.size()).map{DominoPips(n[it][0].asInt(),n[it][1].asInt())}
        var total=0
        for(case in cases){val choices=(0 until case["randoms"].size()).map{case["randoms"][it].asInt()}.iterator();val engine=OnlineEngine(OnlineRandom{choices.next().also{n->require(n<it)}})
            var s=before();var first=true
            for(step in case["trace"]){total++;when(step["kind"].asText()) {
                "DEAL"->{s=if(first){first=false;engine.join(s,participant(3),"start",Instant.EPOCH).state}else engine.command(s,"m5-p0",OnlineCommand(1,"next-$total",s.match.matchId,OnlineCommandType.NEXT_ROUND),Instant.EPOCH).state
                    assertEquals(tiles(step["reserve"]),s.reserve);assertEquals(step["turn"].asInt(),s.match.currentSeat)
                    for(seat in 0..3)assertEquals(tiles(step["hands"][seat]),s.hands["$seat"])}
                "RESULT"->{assertEquals(step["winner"].asInt(),s.round!!.winnerSeat?:-1);assertEquals(step["team"].asInt(),s.round!!.winnerTeam?:-1)
                    assertEquals(step["award"].asInt(),s.round!!.scoreAwarded);assertEquals(step["type"].asText(),s.round!!.finishType!!.name)
                    assertEquals(listOf(step["scores"][0].asInt(),step["scores"][1].asInt()),s.match.score);assertEquals(step["multiplier"].asInt(),s.nextRoundMultiplier)
                    assertEquals(step["matchFinished"].asBoolean(),s.phase==OnlinePhase.MATCH_FINISHED)}
                else->{val play=step["kind"].asText()=="PLAY_TILE";val tile=step["tile"]
                    s=engine.command(s,"m5-p${step["seat"].asInt()}",OnlineCommand(1,"action-$total",s.match.matchId,if(play)OnlineCommandType.PLAY_TILE else OnlineCommandType.PASS,
                        if(play)DominoPips(tile[0].asInt(),tile[1].asInt())else null,if(play)ChainEnd.valueOf(step["end"].asText())else null),Instant.EPOCH).state
                    assertEquals(tiles(step["board"]),s.board.map{it.tile});for(seat in 0..3)assertEquals(tiles(step["hands"][seat]),s.hands["$seat"])
                    assertEquals(step["turn"].asInt(),s.match.currentSeat?:-1)}
            }}
            assertFalse(choices.hasNext());assertEquals(MatchStatus.FINISHED,s.match.status)
        }
        println("PARTNERS_LOCAL_SERVER_PARITY=100 MATCHES / $total STEPS")
    }
    @Test fun `opposing minimum tie retains double through repeated ties and resets after award`() {
        val engine=OnlineEngine();var s=started()
        fun blocked(points:List<Int>):OnlineState {
            val hands=points.mapIndexed {seat,p->seat.toString() to listOf(DominoPips(p/2,p-p/2))}.toMap()
            s=s.copy(hands=hands,board=listOf(BoardPlacement(DominoPips(0,0),ChainEnd.RIGHT,0)),consecutivePasses=3,match=s.match.copy(currentSeat=0))
            return engine.command(s,"m5-p0",OnlineCommand(1,"pass",s.match.matchId,OnlineCommandType.PASS),Instant.EPOCH).state
        }
        repeat(2){s=blocked(listOf(6,6,8,10));assertNull(s.round!!.winnerSeat);assertEquals(0,s.round!!.scoreAwarded);assertEquals(2,s.nextRoundMultiplier)
            s=engine.command(s,"m5-p0",OnlineCommand(1,"next",s.match.matchId,OnlineCommandType.NEXT_ROUND),Instant.EPOCH).state}
        s=blocked(listOf(6,8,6,10));assertEquals(0,s.round!!.winnerSeat);assertEquals(36,s.round!!.scoreAwarded);assertEquals(1,s.nextRoundMultiplier)
    }
}
