package com.teamfho.domino.player

import com.google.cloud.Timestamp
import com.teamfho.domino.match.MatchCodec
import org.junit.jupiter.api.Test
import java.time.Instant
import kotlin.test.*

class OnboardingFoundationTests {
    private val now = Instant.parse("2026-01-02T00:00:00Z")
    private val boundary = OnboardingRolloutBoundary(now)
    @Test fun `new player starts without invented answers`() {
        val state = OnboardingInitialization.state(now, boundary, now)
        assertEquals(OnboardingStatus.NOT_STARTED, state.status)
        assertNull(state.startedAt); assertNull(state.completedAt); assertNull(state.catalogVersion)
        assertNull(DominoProfile(updatedAt=now).experienceLevel)
    }
    @Test fun `legacy exemption does not manufacture flow completion`() {
        val state = OnboardingInitialization.state(now.minusSeconds(1), boundary, now)
        assertEquals(OnboardingStatus.COMPLETED, state.status)
        assertEquals(CompletionOrigin.LEGACY_EXEMPT, state.completionOrigin)
        assertNull(state.startedAt); assertTrue(state.completedStepKeys.isEmpty())
    }
    @Test fun `start is one way and does not allow arbitrary completed state`() {
        val state = PlayerOnboarding(updatedAt=now).start(1,"DOMINO_EXPERIENCE",now)
        assertEquals(1L,state.revision); assertEquals(OnboardingStatus.IN_PROGRESS,state.status)
        assertFails { state.start(2,"COACH",now) }
        assertFails { PlayerOnboarding(status=OnboardingStatus.COMPLETED,updatedAt=now) }
        assertFails { state.copy(status=OnboardingStatus.COMPLETED,completedAt=now,completionOrigin=CompletionOrigin.FLOW) }
        assertFails { PlayerOnboarding(updatedAt=now,currentSubstepKey="INVALID") }
    }
    @Test fun `profile normalization and bounds`() {
        assertEquals("José",PlayerProfileRules.name(" Jose\u0301 "))
        assertNull(PlayerProfileRules.name(null))
        assertEquals("US",PlayerProfileRules.country(" us "))
        listOf("", "a".repeat(81), "a\nname", "a\u202Ename").forEach { assertFails { PlayerProfileRules.name(it) } }
        assertFails { PlayerProfileRules.country("ZZ") }
    }
    @Test fun `locale and timezone never imply country`() {
        assertEquals("es",PlayerProfileRules.locale("es-US")); assertEquals("en",PlayerProfileRules.locale("en-US"))
        listOf("fr", "es-unknown", "", "en_US").forEach { assertFails { PlayerProfileRules.locale(it) } }
        assertEquals("America/Chicago",PlayerProfileRules.timeZone("America/Chicago"))
        assertNull(PlayerProfileRules.timeZone(null)); assertFails { PlayerProfileRules.timeZone("+02:00") }
    }
    @Test fun `experience enum rejects localized values and rating`() {
        assertEquals(4,ExperienceLevel.entries.size)
        assertFails { ExperienceLevel.valueOf("Tournament player") }
        assertFails { ExperienceLevel.valueOf("1800") }
    }
    @Test fun `documents round trip with Firestore timestamp`() {
        val values = listOf(PlayerPreferences("es",updatedAt=now),DominoProfile(updatedAt=now),PlayerOnboarding(updatedAt=now))
        values.forEach { value ->
            val data = FoundationDocumentCodec.encode(value)
            assertIs<Timestamp>(data["updatedAt"])
            assertEquals(value,FoundationDocumentCodec.decode(data,value.javaClass))
        }
    }
    @Test fun `legacy player mapping preserves name and timestamps without private DTO leakage`() {
        val ts=Timestamp.ofTimeSecondsAndNanos(now.epochSecond,0)
        val data=mapOf("uid" to "fixture-player", "accountType" to "REGISTERED", "displayName" to "Existing",
            "language" to "en", "status" to "ACTIVE", "createdAt" to ts, "updatedAt" to ts, "lastSeenAt" to ts)
        val player=FirestoreFoundationMapping.player(data,"fixture-player")
        assertEquals("Existing",player.displayName);assertEquals(FoundationTimestamp.Recorded(now),player.createdAt)
        assertNull(player.firstName);assertEquals(0L,player.profileRevision)
        val response=PlayerResponse(player.uid,player.accountType,player.displayName,player.language,player.status)
        assertEquals(setOf("uid","accountType","displayName","language","status"),MatchCodec.map(response).keys)
    }
}
