package com.teamfho.domino.economy.reward

import org.junit.jupiter.api.Test
import org.junit.jupiter.params.ParameterizedTest
import org.junit.jupiter.params.provider.ValueSource
import java.time.*
import java.util.concurrent.Executors
import kotlin.test.*

class RewardClock(var value: Instant = Instant.parse("2026-09-12T12:00:00Z")) : Clock() {
    override fun instant() = value
    override fun getZone() = ZoneOffset.UTC
    override fun withZone(zone: ZoneId): Clock = this
}
class RewardIntentTests {
    private val clock = RewardClock()
    private val store = RewardFirestoreTransactions(clock.instant())
    private val repo = FirestoreRewardIntentRepository(store.firestore, clock, RewardPolicy())
    private val wallet = mapOf<String, Any>("coins" to 77L, "lifetimeCoinsEarned" to 100L, "lifetimeCoinsSpent" to 23L)
    init { store.documents["players/owner/wallet/main"] = wallet }
    private fun unchanged() {
        assertEquals(wallet, store.documents["players/owner/wallet/main"])
        assertTrue(store.callbacks.flatten().filter { it.startsWith("create:") || it.startsWith("update:") || it.startsWith("set:") }
            .none { it.contains("/wallet/") || it.contains("/walletTransactions/") })
    }
    private fun event(id: String, transaction: String = "aabbcc") = VerifiedAdMobEvent(id, transaction, "5224354917", clock.instant())
    @Test fun `issue reuse principal expiry and ownership`() {
        val intent = repo.issue("owner")
        assertTrue(opaqueId(intent.intentId))
        assertEquals("owner", intent.uid)
        assertEquals(RewardIntentStatus.ISSUED, intent.status)
        assertEquals(clock.instant().plusSeconds(600), intent.expiresAt)
        assertEquals(intent.intentId, repo.issue("owner").intentId)
        assertEquals(404, assertFailsWith<RewardFailure> { repo.status("other", intent.intentId) }.httpStatus)
        clock.value = clock.value.plusSeconds(601)
        assertEquals(RewardIntentStatus.EXPIRED, repo.status("owner", intent.intentId).status)
        assertNotEquals(intent.intentId, repo.issue("owner").intentId)
        unchanged()
    }
    @Test fun `atomic duplicate transition and cross intent conflicts`() {
        val a = repo.issue("owner")
        val callback = event(a.intentId)
        assertTrue(repo.verify(callback))
        assertFalse(repo.verify(callback))
        val verified = repo.status("owner", a.intentId)
        assertEquals(RewardIntentStatus.VERIFIED, verified.status)
        assertEquals(clock.instant(), verified.verifiedAt)
        assertEquals("aabbcc", verified.adMobTransactionId)
        val b = repo.issue("other-owner")
        assertFailsWith<RewardFailure> { repo.verify(event(b.intentId)) }
        assertFailsWith<RewardFailure> { repo.verify(event(a.intentId, "bbccdd")) }
        clock.value = clock.value.plusSeconds(86400)
        assertFalse(repo.verify(callback))
        assertEquals(1, store.documents.keys.count { it.startsWith("adMobRewardTransactions/") })
        unchanged()
    }
    @Test fun `concurrent create and duplicate callbacks one logical transition`() {
        val pool = Executors.newFixedThreadPool(10)
        try {
            val intents = pool.invokeAll((1..20).map { java.util.concurrent.Callable { repo.issue("owner").intentId } }).map { it.get() }
            assertEquals(1, intents.toSet().size)
            val result = pool.invokeAll((1..20).map { java.util.concurrent.Callable { repo.verify(event(intents.first())) } }).map { it.get() }
            assertEquals(1, result.count { it })
            unchanged()
        } finally { pool.shutdownNow() }
    }
    @Test fun `Firestore retry stages do not duplicate documents`() {
        store.retryFirstCallback = true
        val intent = repo.issue("owner")
        assertTrue(repo.verify(event(intent.intentId)))
        assertEquals(1, store.documents.keys.count { it.startsWith("adMobRewardTransactions/") })
        unchanged()
    }
    @ParameterizedTest @ValueSource(strings = ["unit", "future", "old", "expired", "unknown", "source", "policy", "environment"])
    fun `invalid event never writes reward or wallet`(variant: String) {
        val intent = repo.issue("owner")
        var callback = event(intent.intentId)
        when (variant) {
            "unit" -> callback = callback.copy(adUnit = "8685423732")
            "future" -> callback = callback.copy(timestamp = clock.instant().plusSeconds(121))
            "old" -> callback = callback.copy(timestamp = clock.instant().minusSeconds(601))
            "expired" -> clock.value = clock.instant().plusSeconds(601)
            "unknown" -> callback = callback.copy(intentId = "00000000-0000-4000-8000-000000000000")
            else -> {
                val field = when (variant) { "source" -> "source"; "policy" -> "rewardPolicyKey"; else -> "adUnitEnvironment" }
                val value = if (variant == "environment") "PRODUCTION" else "OTHER"
                store.documents["rewardIntents/" + intent.intentId] = store.documents.getValue("rewardIntents/" + intent.intentId) + (field to value)
            }
        }
        assertFailsWith<RewardFailure> { repo.verify(callback) }
        assertEquals(0, store.documents.keys.count { it.startsWith("adMobRewardTransactions/") })
        unchanged()
    }
    @Test fun `dependency failure closed`() {
        store.injectedFailure = IllegalStateException("secret")
        assertEquals(503, assertFailsWith<RewardFailure> { repo.issue("owner") }.httpStatus)
        unchanged()
    }
}
