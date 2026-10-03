package com.teamfho.domino.player

import com.teamfho.domino.security.FirebaseIdentity
import org.junit.jupiter.api.Test
import java.time.Clock
import java.time.Instant
import java.time.ZoneOffset
import java.util.concurrent.Executors
import kotlin.test.*

class NewPlayerFoundationTests {
    private val now = Instant.parse("2026-10-02T00:00:00Z")
    private val clock = Clock.fixed(now, ZoneOffset.UTC)
    private val identity = FirebaseIdentity("new-foundation-fixture", true)
    private val root = "players/${identity.uid}"
    private fun assertFoundation(store: InMemoryFirestoreTransactions) {
        assertEquals(setOf(root, "$root/wallet/main", "$root/preferences/current", "$root/dominoProfile/current", "$root/onboarding/current"),store.documents.keys.filter { it.startsWith("players/") }.toSet())
        val p=FoundationDocumentCodec.decode(store.documents.getValue("$root/preferences/current"),PlayerPreferences::class.java)
        assertEquals("es",p.preferredLocale);assertNull(p.timeZone);assertTrue(p.notificationPreferences.isEmpty());assertEquals(0L,p.revision)
        val d=FoundationDocumentCodec.decode(store.documents.getValue("$root/dominoProfile/current"),DominoProfile::class.java)
        assertNull(d.experienceLevel);assertNull(d.preferredCoachKey);assertNull(d.selectedCoachCatalogVersion);assertEquals(0L,d.revision)
        val o=FoundationDocumentCodec.decode(store.documents.getValue("$root/onboarding/current"),PlayerOnboarding::class.java)
        assertEquals(PlayerOnboarding(updatedAt=o.updatedAt),o)
    }
    @Test fun `absent boundary initializes mandatory defaults`() {
        val store=InMemoryFirestoreTransactions(now)
        FirestorePlayerFoundationRepository(store.firestore,clock).ensure(identity,"es","Guest-ABCDEFGH")
        assertFoundation(store)
    }
    @Test fun `blank configuration initializes while malformed fails configuration`() {
        for (cutoff in listOf("", "   ")) {
            val store=InMemoryFirestoreTransactions(now)
            PlayerFoundationConfiguration().playerFoundationRepository(store.firestore,cutoff).ensure(identity,"es","Guest-ABCDEFGH")
            assertFoundation(store)
        }
        val store=InMemoryFirestoreTransactions(now)
        assertFails { PlayerFoundationConfiguration().playerFoundationRepository(store.firestore,"not-an-instant") }
        assertEquals(setOf(PlayerAliasReservations.rolloutPath),store.documents.keys)
    }
    @Test fun `valid past present and future cutoff never exempts a new player`() {
        for (cutoff in listOf(now.minusSeconds(1),now,now.plusSeconds(86400))) {
            val store=InMemoryFirestoreTransactions(now)
            FirestorePlayerFoundationRepository(store.firestore,clock,OnboardingRolloutBoundary(cutoff)).ensure(identity,"es","Guest-ABCDEFGH")
            assertFoundation(store)
        }
    }
    @Test fun `orphan preparation failure leaves no partial initialization`() {
        for (part in listOf("wallet/main","preferences/current","dominoProfile/current","onboarding/current")) {
            val store=InMemoryFirestoreTransactions(now)
            // A valid wallet orphan or an opaque foundation orphan must never be adopted.
            store.documents["$root/$part"]=if(part=="wallet/main") mapOf("coins" to 0L,"lifetimeCoinsEarned" to 0L,"lifetimeCoinsSpent" to 0L,"createdAt" to com.google.cloud.Timestamp.ofTimeSecondsAndNanos(now.epochSecond,0),"updatedAt" to com.google.cloud.Timestamp.ofTimeSecondsAndNanos(now.epochSecond,0)) else mapOf("preserve" to true)
            val before=store.documents.toMap()
            assertEquals(FoundationError.PLAYER_STATE_CONFLICT, assertFailsWith<PlayerFoundationException> {
                FirestorePlayerFoundationRepository(store.firestore,clock).ensure(identity,"es","Guest-ABCDEFGH")
            }.code)
            assertEquals(before,store.documents);assertFalse(store.documents.containsKey(root))
        }
    }
    @Test fun `repeated complete and partial existing foundation is never rewritten`() {
        val store=InMemoryFirestoreTransactions(now);val repo=FirestorePlayerFoundationRepository(store.firestore,clock,OnboardingRolloutBoundary(now.plusSeconds(1)))
        repo.ensure(identity,"es","Guest-ABCDEFGH")
        val complete=store.documents.toMap();repo.ensure(identity,"en","Guest-ZYXWVUTS");assertEquals(complete,store.documents)
        store.documents.remove("$root/preferences/current")
        val partial=store.documents.toMap();repo.ensure(identity,"en","Guest-ZYXWVUTS");assertEquals(partial,store.documents)
    }
    @Test fun `concurrent bootstrap and losing callback reread committed foundation`() {
        val store=InMemoryFirestoreTransactions(now);val repo=FirestorePlayerFoundationRepository(store.firestore,clock)
        val pool=Executors.newFixedThreadPool(2)
        try { val jobs=(1..2).map { pool.submit<BootstrapResult> { repo.ensure(identity,"es","Guest-ABCDEFGH") } };jobs.forEach { it.get() } }
        finally { pool.shutdownNow() }
        assertFoundation(store)
        val committed=store.documents.toMap()
        val race=InMemoryFirestoreTransactions(now);race.retryFirstCallback=true
        race.beforeRetry={race.documents.putAll(committed)}
        FirestorePlayerFoundationRepository(race.firestore,clock).ensure(identity,"es","Guest-ABCDEFGH")
        assertEquals(committed,race.documents)
        assertTrue(race.callbacks[0].any { it.startsWith("create:") })
        assertFalse(race.callbacks[1].any { it.startsWith("create:")||it.startsWith("update:") })
    }
    @Test fun `existing trial and grants remain unchanged during bootstrap`() {
        val store=InMemoryFirestoreTransactions(now);val repo=FirestorePlayerFoundationRepository(store.firestore,clock)
        repo.ensure(identity,"es","Guest-ABCDEFGH")
        store.documents["$root/entitlementState/current"]=mapOf("trialConsumed" to true)
        store.documents["$root/entitlementGrants/fixture"]=mapOf("status" to "ACTIVE")
        val before=store.documents.toMap();repo.ensure(identity,"es","Guest-ABCDEFGH");assertEquals(before,store.documents)
    }
}
