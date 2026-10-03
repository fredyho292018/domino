package com.teamfho.domino.player

import com.google.api.core.ApiFutures
import com.google.cloud.firestore.*
import org.junit.jupiter.api.Test
import org.mockito.Mockito.*
import java.time.Instant
import kotlin.test.*

class OnboardingPersistenceTests {
    private val now = Instant.parse("2026-01-02T00:00:00Z")
    private val db = mock(Firestore::class.java)
    private val stored = mutableMapOf<String, Map<String, Any>>()
    @Test fun `bootstrap boundary does not repair existing incomplete player`() {
        val store=InMemoryFirestoreTransactions(now)
        val clock=java.time.Clock.fixed(now,java.time.ZoneOffset.UTC)
        val identity=com.teamfho.domino.security.FirebaseIdentity("fixture-player",true)
        val legacy=FirestorePlayerFoundationRepository(store.firestore,clock)
        legacy.ensure(identity,"es","Guest-ABCDEFGH")
        listOf("preferences", "dominoProfile", "onboarding").forEach { store.documents.remove("players/fixture-player/$it/current") }
        val original=store.documents.toMap()
        val enabled=FirestorePlayerFoundationRepository(store.firestore,clock,OnboardingRolloutBoundary(now.plusSeconds(1)))
        store.retryFirstCallback=true
        enabled.ensure(identity,"en","Guest-ZYXWVUTS")
        original.forEach { (key,value) -> assertEquals(value,store.documents[key]) }
        val first=store.documents.toMap()
        enabled.ensure(identity,"en","Guest-ZYXWVUTS")
        assertEquals(first,store.documents)
        assertEquals(2,store.documents.keys.count { it.startsWith("players/") })
    }
    @Test fun `new bootstrap at boundary atomically creates five documents`() {
        val store=InMemoryFirestoreTransactions(now)
        val repo=FirestorePlayerFoundationRepository(store.firestore,java.time.Clock.fixed(now,java.time.ZoneOffset.UTC),OnboardingRolloutBoundary(now))
        repo.ensure(com.teamfho.domino.security.FirebaseIdentity("fixture-player",true),"en","Guest-ABCDEFGH")
        assertEquals(5,store.documents.keys.count { it.startsWith("players/") })
        assertEquals("NOT_STARTED",store.documents.getValue("players/fixture-player/onboarding/current")["status"])
    }
    private fun prepare(player: Player) {
        val tx = mock(Transaction::class.java)
        for (part in listOf("preferences", "dominoProfile", "onboarding")) {
            val path = "players/${player.uid}/$part/current"
            val ref = mock(DocumentReference::class.java)
            val doc = mock(DocumentSnapshot::class.java)
            `when`(db.document(path)).thenReturn(ref)
            `when`(doc.exists()).thenReturn(path in stored)
            `when`(doc.data).thenReturn(stored[path])
            `when`(tx.get(ref)).thenReturn(ApiFutures.immediateFuture(doc))
            doAnswer { call ->
                assertFalse(path in stored, "initializer must create only missing documents")
                @Suppress("UNCHECKED_CAST")
                val value = call.getArgument<Map<String, Any>>(1)
                stored[path] = value
                tx
            }.`when`(tx).create(eq(ref), anyMap<String, Any>())
        }
        FirestoreOnboardingFoundation(db, OnboardingRolloutBoundary(now)).prepare(tx,player,now).invoke()
    }
    private fun player(created: Instant) = Player("fixture-player",PlayerAccountType.REGISTERED,"Existing","es",PlayerStatus.ACTIVE,
        FoundationTimestamp.Recorded(created),FoundationTimestamp.Recorded(created),FoundationTimestamp.Recorded(created))
    @Test fun `legacy initialization is create only and repeatable with preserved root`() {
        val player=player(now.minusSeconds(50));val original=player.copy()
        prepare(player);val first=stored.toMap();prepare(player)
        assertEquals(first,stored);assertEquals(original,player);assertEquals(3,stored.size)
        assertEquals("COMPLETED",stored.getValue("players/fixture-player/onboarding/current")["status"])
        assertEquals("es",stored.getValue("players/fixture-player/preferences/current")["preferredLocale"])
    }
    @Test fun `boundary player starts not started and existing preferences survive`() {
        prepare(player(now))
        assertEquals("NOT_STARTED",stored.getValue("players/fixture-player/onboarding/current")["status"])
        val path="players/fixture-player/preferences/current"
        stored[path]=FoundationDocumentCodec.encode(PlayerPreferences("en","America/Chicago",revision=5,updatedAt=now))
        prepare(player(now))
        assertEquals(5L,FoundationDocumentCodec.decode(stored.getValue(path),PlayerPreferences::class.java).revision)
        assertEquals("en",stored.getValue(path)["preferredLocale"])
    }
}
