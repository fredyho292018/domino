package com.teamfho.domino.player

import com.google.api.gax.rpc.ApiException
import com.google.api.gax.rpc.StatusCode
import com.google.cloud.Timestamp
import com.teamfho.domino.economy.Wallet
import com.teamfho.domino.security.FirebaseIdentity
import org.junit.jupiter.api.Test
import org.junit.jupiter.params.ParameterizedTest
import org.junit.jupiter.params.provider.ValueSource
import org.mockito.Mockito.*
import java.time.Clock
import java.time.Instant
import java.time.ZoneOffset
import java.util.concurrent.Callable
import java.util.concurrent.Executors
import java.util.concurrent.CountDownLatch
import java.util.concurrent.TimeUnit
import kotlin.test.*

class PlayerFoundationRepositoryTests {
    private val now = Instant.parse("2026-09-11T12:00:00Z")
    private val store = InMemoryFirestoreTransactions(now)
    private val repository = FirestorePlayerFoundationRepository(store.firestore, Clock.fixed(now, ZoneOffset.UTC))
    private val playerPath = "players/test-uid"
    private val walletPath = "$playerPath/wallet/main"
    private val old = Timestamp.ofTimeSecondsAndNanos(now.minusSeconds(3600).epochSecond, 0)
    private val recent = Timestamp.ofTimeSecondsAndNanos(now.minusSeconds(60).epochSecond, 0)
    private fun player(type: String = "GUEST", seen: Timestamp = recent) = mapOf<String, Any>(
        "uid" to "test-uid", "accountType" to type, "displayName" to "Existing name", "language" to "es",
        "status" to "ACTIVE", "createdAt" to old, "updatedAt" to old, "lastSeenAt" to seen, "futureField" to "keep"
    )
    private fun wallet(coins: Any = 123L) = mapOf<String, Any>(
        "coins" to coins, "lifetimeCoinsEarned" to 500L, "lifetimeCoinsSpent" to 377L,
        "createdAt" to old, "updatedAt" to old, "futureField" to "keep"
    )
    private fun seed(type: String = "GUEST", seen: Timestamp = recent) {
        store.documents[playerPath] = player(type, seen); store.documents[walletPath] = wallet()
    }
    private fun ensure(anonymous: Boolean = true, name: String = "Guest-ABCDEFGH") =
        repository.ensure(FirebaseIdentity("test-uid", anonymous), "en", name)
    private fun assertNoWrites() = assertTrue(store.callbacks.flatten().none { it.startsWith("create:") || it.startsWith("update:") })

    @ParameterizedTest @ValueSource(booleans = [true, false])
    fun `new identity creates two documents with zero wallet`(anonymous: Boolean) {
        val result = ensure(anonymous)
        assertEquals(if (anonymous) PlayerAccountType.GUEST else PlayerAccountType.REGISTERED, result.player.accountType)
        assertEquals("en", result.player.language)
        assertEquals("Guest-ABCDEFGH", result.player.displayName)
        assertEquals(0L, result.wallet.coins)
        assertEquals(0L, result.wallet.lifetimeCoinsEarned)
        assertEquals(0L, result.wallet.lifetimeCoinsSpent)
        assertEquals(FoundationTimestamp.ServerAssigned, result.player.createdAt)
        assertEquals(setOf(playerPath, walletPath), store.documents.keys)
        assertEquals(listOf("read:$playerPath", "read:$walletPath", "create:$playerPath", "create:$walletPath"), store.callbacks.single())
        assertEquals(Timestamp.ofTimeSecondsAndNanos(now.epochSecond, now.nano), store.documents.getValue(playerPath)["createdAt"])
    }
    @Test fun `second ensure conserves alias language wallet and timestamps`() {
        ensure()
        val before = store.documents.toMap()
        store.callbacks.clear()
        val result = ensure(name = "Guest-ZZZZZZZZ")
        assertEquals(before, store.documents)
        assertEquals("Guest-ABCDEFGH", result.player.displayName)
        assertTrue(result.player.createdAt is FoundationTimestamp.Recorded)
        assertNoWrites()
    }
    @Test fun `existing guest stays guest without overwriting state`() {
        seed(); val before = store.documents.toMap()
        val result = ensure()
        assertEquals(PlayerAccountType.GUEST, result.player.accountType)
        assertEquals("es", result.player.language)
        assertEquals(before, store.documents); assertNoWrites()
    }
    @Test fun `guest upgrades using a partial update and preserves wallet`() {
        seed(); val wallet = store.documents[walletPath]
        val result = ensure(false)
        assertEquals(PlayerAccountType.REGISTERED, result.player.accountType)
        assertEquals(FoundationTimestamp.ServerAssigned, result.player.updatedAt)
        assertEquals(old, store.documents.getValue(playerPath)["createdAt"])
        assertEquals(recent, store.documents.getValue(playerPath)["lastSeenAt"])
        assertEquals("keep", store.documents.getValue(playerPath)["futureField"])
        assertEquals(wallet, store.documents[walletPath])
        assertEquals(1, store.callbacks.flatten().count { it.startsWith("update:") })
    }
    @Test fun `registered never downgrades`() {
        seed("REGISTERED")
        assertEquals(PlayerAccountType.REGISTERED, ensure().player.accountType); assertNoWrites()
    }
    @Test fun `missing wallet repaired preserving player`() {
        store.documents[playerPath] = player(); val before = store.documents[playerPath]
        assertEquals(0L, ensure().wallet.coins)
        assertEquals(before, store.documents[playerPath])
        assertEquals(1, store.callbacks.flatten().count { it.startsWith("create:") })
    }
    @Test fun `missing player repaired preserving wallet exactly`() {
        store.documents[walletPath] = wallet(9_007_199_254_740_991L); val before = store.documents[walletPath]
        assertEquals(Wallet.MAX_COINS, ensure().wallet.coins)
        assertEquals(before, store.documents[walletPath])
        assertEquals(1, store.callbacks.flatten().count { it.startsWith("create:") })
    }
    @ParameterizedTest @ValueSource(longs = [899, 900, 901])
    fun `last seen threshold preserves business timestamps`(elapsed: Long) {
        val seen = Timestamp.ofTimeSecondsAndNanos(now.minusSeconds(elapsed).epochSecond, 0)
        seed(seen = seen); val result = ensure()
        assertEquals(old, store.documents.getValue(playerPath)["createdAt"])
        assertEquals(old, store.documents.getValue(playerPath)["updatedAt"])
        assertEquals(elapsed >= 900, result.player.lastSeenAt is FoundationTimestamp.ServerAssigned)
        assertEquals(if (elapsed >= 900) 1 else 0, store.callbacks.flatten().count { it.startsWith("update:") })
    }
    @Test fun `upgrade plus last seen is one update`() {
        seed(seen = old); ensure(false)
        assertEquals(1, store.callbacks.flatten().count { it.startsWith("update:") })
    }
    @ParameterizedTest @ValueSource(strings = ["coins", "lifetimeCoinsEarned", "lifetimeCoinsSpent"])
    fun `each numeric field rejects negative floating string and overflow`(field: String) {
        for (value in listOf<Any>(-1L, Wallet.MAX_COINS + 1, Long.MAX_VALUE, 1.0, 1f, "1")) {
            seed(); store.documents[walletPath] = wallet() + (field to value)
            val before = store.documents.toMap(); store.callbacks.clear()
            assertEquals(FoundationError.WALLET_STATE_INVALID, assertFailsWith<PlayerFoundationException> { ensure(false) }.code)
            assertEquals(before, store.documents); assertNoWrites()
        }
    }
    @ParameterizedTest @ValueSource(strings = ["uid", "accountType", "displayName", "language", "status", "createdAt", "updatedAt", "lastSeenAt"])
    fun `corrupt player rejects without repairing or overwriting`(field: String) {
        store.documents[playerPath] = player() + (field to "")
        val before = store.documents.toMap()
        assertEquals(FoundationError.PLAYER_STATE_CONFLICT, assertFailsWith<PlayerFoundationException> { ensure() }.code)
        assertEquals(before, store.documents); assertNoWrites()
    }
    @Test fun `UID mismatch rejects an otherwise valid player`() {
        seed(); store.documents[playerPath] = player() + ("uid" to "someone-else")
        assertEquals(FoundationError.PLAYER_STATE_CONFLICT, assertFailsWith<PlayerFoundationException> { ensure() }.code)
        assertNoWrites()
    }
    @ParameterizedTest @ValueSource(strings = ["coins", "createdAt", "updatedAt"])
    fun `wallet missing fields cannot be reset`(field: String) {
        store.documents[walletPath] = wallet() - field
        assertEquals(FoundationError.WALLET_STATE_INVALID, assertFailsWith<PlayerFoundationException> { ensure() }.code)
        assertFalse(store.documents.containsKey(playerPath)); assertNoWrites()
    }
    @Test fun `retry callback keeps candidate stable and commits once`() {
        store.retryFirstCallback = true
        val candidate = GuestDisplayNames.generate()
        val result = ensure(name = candidate)
        assertEquals(2, store.callbacks.size)
        assertEquals(2, store.documents.size)
        assertEquals(candidate, result.player.displayName)
        assertEquals(candidate, store.documents.getValue(playerPath)["displayName"])
    }
    @ParameterizedTest @ValueSource(booleans = [true, false])
    fun `simultaneous ensures share one persisted winner and never reset balance`(existingWallet: Boolean) {
        if (existingWallet) store.documents[walletPath] = wallet(5000000000L)
        val before = store.documents[walletPath]
        val executor = Executors.newFixedThreadPool(8)
        val start = CountDownLatch(1)
        try {
            val futures = (1..8).map { executor.submit(Callable { start.await(); ensure(name = GuestDisplayNames.generate()) }) }
            start.countDown()
            val results = futures.map { it.get(10, TimeUnit.SECONDS) }
            assertEquals(1, results.map { it.player.displayName }.toSet().size)
            assertEquals(2, store.documents.size)
            if (existingWallet) assertEquals(before, store.documents[walletPath])
            else assertEquals(0L, store.documents.getValue(walletPath)["coins"])
            assertEquals(if (existingWallet) 1 else 2, store.callbacks.flatten().count { it.startsWith("create:") })
        } finally { executor.shutdownNow() }
    }
    @ParameterizedTest @ValueSource(strings = ["ABORTED", "UNAVAILABLE", "DEADLINE_EXCEEDED"])
    fun `transaction errors have domain categories`(code: String) {
        val status = mock(StatusCode::class.java)
        `when`(status.code).thenReturn(StatusCode.Code.valueOf(code))
        store.injectedFailure = ApiException(null, status, false)
        val failure = assertFailsWith<PlayerFoundationException> { ensure() }
        assertEquals(if (code == "ABORTED") FoundationError.FIRESTORE_CONTENTION_EXHAUSTED else FoundationError.FIRESTORE_UNAVAILABLE, failure.code)
    }
    @Test fun `invalid input never starts transaction`() {
        assertFailsWith<IllegalArgumentException> { repository.ensure(FirebaseIdentity("path/injection", true), "en", "Guest-ABCDEFGH") }
        assertFailsWith<IllegalArgumentException> { repository.ensure(FirebaseIdentity("uid", true), "fr", "Guest-ABCDEFGH") }
        assertTrue(store.callbacks.isEmpty())
    }
    @Test fun `alias uses presentation safe random characters`() {
        repeat(100) { assertTrue(Regex("Guest-[A-Z2-9]{8}").matches(GuestDisplayNames.generate())) }
    }
}
