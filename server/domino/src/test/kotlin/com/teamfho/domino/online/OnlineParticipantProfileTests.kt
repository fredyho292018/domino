package com.teamfho.domino.online

import com.teamfho.domino.catalog.*
import com.teamfho.domino.match.*
import java.time.Instant
import org.junit.jupiter.api.Test
import kotlin.test.*

class OnlineParticipantProfileTests {
    private fun create(marked:Boolean):OnlineState {
        val memory=MemoryOnlineRepository()
        val repo=object:OnlineRepository by memory {override fun createPaired(write:OnlineWrite):OnlineState {memory.create(write.state);return write.state}}
        val catalog=GameCatalogService(GameCatalogRepository{GameCatalogV4Publisher.canonical()})
        val c=catalog.resolve()!!;val mode=c.modes.single{it.key=="PARTNERS_2V2_ONLINE"}
        val names=mapOf("p0" to "Mateo","p1" to "Sofia","p2" to "Lucas","p3" to "Camila")
        val service=OnlineMatchService(catalog,repo,profiles={uid->OnlineParticipantProfile(names.getValue(uid),marked&&uid=="p0")})
        return service.createPaired("profile-match",names.keys.toList(),MatchRuleSnapshot.freeze(c,mode))
    }
    @Test fun `mixed test match uses normal aliases and existing validation flag`() {
        val s=create(true)
        assertTrue(s.match.validationData)
        assertEquals(setOf("Mateo","Sofia","Lucas","Camila"),s.match.participants.map{it.displayNameSnapshot}.toSet())
        assertTrue(s.match.participants.all{it.controlType==ControlType.REMOTE_HUMAN})
        assertEquals(listOf(0,1,0,1),s.match.participants.map{it.teamId})
    }
    @Test fun `ordinary matches remain ordinary`() {assertFalse(create(false).match.validationData)}
    @Test fun `normal history preserves test marker`() {
        val initial=create(true);val seat=initial.match.currentSeat!!
        val before=initial.copy(hands=initial.hands+(seat.toString() to listOf(DominoPips(0,0))),match=initial.match.copy(score=listOf(199,199)))
        val write=OnlineEngine().command(before,before.match.participants[seat].playerUid!!,OnlineCommand(1,"finish",before.match.matchId,OnlineCommandType.PLAY_TILE,DominoPips(0,0),ChainEnd.RIGHT),Instant.now())
        assertEquals(MatchStatus.FINISHED,write.state.match.status)
        assertEquals(4,write.histories.size);assertTrue(write.histories.values.all{it.validationData})
    }
}
