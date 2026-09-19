package com.teamfho.domino.online

import com.teamfho.domino.match.*
import org.junit.jupiter.api.Test
import java.time.Instant
import kotlin.test.*

class ParticipantPersistenceTests {
    private val now=Instant.parse("2026-01-01T00:00:00Z")
    private val participant=MatchParticipant(0,"p0","Player",null,ControlType.REMOTE_HUMAN,ConnectionState.CONNECTED,now)

    @Test fun `persisted equality is structural across detached instances`() {
        assertNotSame(participant,participant.copy())
        assertEquals(MatchCodec.map(participant),MatchCodec.map(MatchCodec.copy(participant,MatchParticipant::class.java)))
    }

    @Test fun `every persisted participant field participates in document equality`() {
        val original=MatchCodec.map(participant)
        assertEquals(setOf("seatIndex","playerUid","displayNameSnapshot","teamId","controlType","connectionState",
            "joinedAt","disconnectedAt","reconnectDeadlineAt","abandonedAt"),original.keys)
        val changes=listOf(participant.copy(seatIndex=1),participant.copy(playerUid="p1"),participant.copy(displayNameSnapshot="Other"),
            participant.copy(teamId=1),participant.copy(controlType=ControlType.BOT),participant.copy(connectionState=ConnectionState.DISCONNECTED),
            participant.copy(joinedAt=now.plusNanos(1)),participant.copy(disconnectedAt=now),
            participant.copy(reconnectDeadlineAt=now),participant.copy(abandonedAt=now))
        changes.forEach {assertNotEquals(original,MatchCodec.map(it))}
    }

    @Test fun `connection timestamp changes and clearing are not ignored`() {
        val disconnected=participant.copy(connectionState=ConnectionState.DISCONNECTED,disconnectedAt=now,reconnectDeadlineAt=now.plusSeconds(180))
        assertNotEquals(MatchCodec.map(disconnected),MatchCodec.map(disconnected.copy(disconnectedAt=now.plusNanos(1))))
        assertNotEquals(MatchCodec.map(disconnected),MatchCodec.map(disconnected.copy(reconnectDeadlineAt=now.plusSeconds(181))))
        assertNotEquals(MatchCodec.map(disconnected),MatchCodec.map(participant))
        assertNotEquals(MatchCodec.map(disconnected.copy(abandonedAt=now)),MatchCodec.map(disconnected.copy(abandonedAt=now.plusNanos(1))))
    }
}
