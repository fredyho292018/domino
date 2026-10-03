package com.teamfho.domino.player

import tools.jackson.databind.json.JsonMapper
import org.junit.jupiter.api.Test
import kotlin.test.*

class PlayerAliasAvailabilityTests {
    private val docs = mutableMapOf<String, Map<String, Any>>(
        PlayerAliasReservations.rolloutPath to mapOf("status" to "READY", "normalizationVersion" to 1L))
    private fun check(alias: String) = AliasAvailability.check("fixture-owner", alias) { docs[it] }.state
    private fun claim(owner: String) {
        docs[PlayerAliasReservations.path("Candidate")] = mapOf("state" to "CLAIMED", "playerId" to owner, "normalizationVersion" to 1L)
    }
    @Test fun `available lookup is read only`() {
        val before=docs.toMap();assertEquals("AVAILABLE",check("Candidate"));assertEquals(before,docs)
    }
    @Test fun `taken normalizes case and whitespace without disclosing owner`() {
        claim("other-fixture");val before=docs.toMap()
        for(alias in listOf("Candidate","CANDIDATE","  candidate  ")) assertEquals("TAKEN",check(alias))
        assertEquals("{\"state\":\"TAKEN\"}",JsonMapper.builder().build().writeValueAsString(AliasAvailability.check("fixture-owner","Candidate"){docs[it]}))
        assertEquals(before,docs)
    }
    @Test fun `own alias is available`() {claim("fixture-owner");assertEquals("AVAILABLE",check(" CANDIDATE "))}
    @Test fun `invalid and reserved candidates never read storage`() {
        for(alias in listOf("ab","bad name","<alias>","x".repeat(17),"ADMIN","administrator","moderator","support","teamfho","system"))
            assertFailsWith<DisplayNameException>{AliasAvailability.check("fixture-owner",alias){error("Unexpected storage read")}}
    }
    @Test fun `released reservation is available`() {
        docs[PlayerAliasReservations.path("Candidate")]=mapOf("state" to "RELEASED","normalizationVersion" to 1L)
        assertEquals("AVAILABLE",check("Candidate"))
    }
    @Test fun `unready or malformed state is not misreported as taken`() {
        for(record in listOf(mapOf("state" to "CLAIMED"),mapOf("state" to "RELEASED","playerId" to "other-fixture","normalizationVersion" to 1L))) {
            docs[PlayerAliasReservations.path("Candidate")]=record
            assertEquals(FoundationError.DISPLAY_NAME_RESERVATIONS_NOT_READY,assertFailsWith<PlayerFoundationException>{check("Candidate")}.code)
        }
        docs.remove(PlayerAliasReservations.rolloutPath)
        assertEquals(FoundationError.DISPLAY_NAME_RESERVATIONS_NOT_READY,assertFailsWith<PlayerFoundationException>{check("Candidate")}.code)
    }
}
