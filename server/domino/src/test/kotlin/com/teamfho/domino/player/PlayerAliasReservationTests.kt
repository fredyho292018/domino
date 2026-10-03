package com.teamfho.domino.player

import com.teamfho.domino.security.FirebaseIdentity
import org.junit.jupiter.api.Test
import java.time.Clock
import java.time.Instant
import java.time.ZoneOffset
import java.util.concurrent.Callable
import java.util.concurrent.CountDownLatch
import java.util.concurrent.Executors
import kotlin.test.*

class PlayerAliasReservationTests {
    private val now = Instant.parse("2026-10-01T00:00:00Z")
    private val store = InMemoryFirestoreTransactions(now)
    private val repository = FirestorePlayerFoundationRepository(store.firestore, Clock.fixed(now, ZoneOffset.UTC))
    private fun create(id: String, alias: String) = repository.ensure(FirebaseIdentity(id, true), "en", alias)
    @Test fun `new guest is reserved and keeping it is write free`() {
        val name=GuestDisplayNames.generate()
        create("alias-a",name)
        assertEquals("alias-a",store.documents[PlayerAliasReservations.path(name)]?.get("playerId"))
        val before=store.documents.toMap()
        repository.updateDisplayName(FirebaseIdentity("alias-a",true),name)
        create("alias-a",GuestDisplayNames.generate())
        assertEquals(before,store.documents)
    }
    @Test fun `concurrent case insensitive claim has one winner and no split ownership`() {
        create("alias-a","Guest-ABCDEFGH");create("alias-b","Guest-BCDEFGHJ")
        val start=CountDownLatch(1);val pool=Executors.newFixedThreadPool(2)
        try {
            val futures=listOf("alias-a" to "ElCubano85","alias-b" to "elcubano85").map { (id,alias) -> pool.submit(Callable {
                start.await()
                try { repository.updateDisplayName(FirebaseIdentity(id,true),alias); "OK" }
                catch(e:PlayerFoundationException) { e.code.name }
            }) }
            start.countDown()
            assertEquals(listOf("DISPLAY_NAME_TAKEN","OK"),futures.map{it.get()}.sorted())
            val owner=store.documents.getValue(PlayerAliasReservations.path("ELCUBANO85"))["playerId"]
            assertEquals("elcubano85",PlayerAliasReservations.canonical(store.documents.getValue("players/$owner")["displayName"] as String))
            val prior=if(owner=="alias-a")"Guest-ABCDEFGH" else "Guest-BCDEFGHJ"
            assertEquals(mapOf("state" to "RELEASED","normalizationVersion" to 1L),store.documents[PlayerAliasReservations.path(prior)])
        } finally { pool.shutdownNow() }
    }
    @Test fun `missing rollout and missing legacy reservation fail closed without repair`() {
        store.documents.remove(PlayerAliasReservations.rolloutPath)
        assertEquals(FoundationError.DISPLAY_NAME_RESERVATIONS_NOT_READY,assertFailsWith<PlayerFoundationException>{create("alias-a","Guest-ABCDEFGH")}.code)
        assertTrue(store.documents.isEmpty())
        store.documents[PlayerAliasReservations.rolloutPath]=mapOf("status" to "READY","normalizationVersion" to 1L)
        create("alias-a","Guest-ABCDEFGH")
        store.documents.remove(PlayerAliasReservations.path("Guest-ABCDEFGH"))
        val before=store.documents.toMap()
        assertEquals(FoundationError.DISPLAY_NAME_RESERVATIONS_NOT_READY,assertFailsWith<PlayerFoundationException>{repository.updateDisplayName(FirebaseIdentity("alias-a",true),"CustomAlias")}.code)
        assertEquals(before,store.documents)
        create("alias-a","Guest-BCDEFGHJ") // existing bootstrap never reopens onboarding or repairs alias state
        assertEquals(before,store.documents)
    }
    @Test fun `guest collision creates no second foundation`() {
        create("alias-a","Guest-ABCDEFGH");val before=store.documents.toMap()
        assertEquals(FoundationError.DISPLAY_NAME_TAKEN,assertFailsWith<PlayerFoundationException>{create("alias-b","Guest-ABCDEFGH")}.code)
        assertEquals(before,store.documents)
    }
    @Test fun `canonical normalization trims case and composed unicode without changing displayed capitalization`() {
        assertEquals("ElCubano85",DisplayNameRules.validate("  ElCubano85  "))
        assertEquals(PlayerAliasReservations.path("Épreuve"),PlayerAliasReservations.path("  E\u0301PREUVE  "))
        assertEquals(PlayerAliasReservations.path("Alias"),PlayerAliasReservations.path("ALIAS"))
        assertFailsWith<DisplayNameException>{DisplayNameRules.validate("<b>alias</b>")}
        assertFailsWith<DisplayNameException>{DisplayNameRules.validate("support")}
    }
}
