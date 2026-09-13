package com.teamfho.domino.economy.reward

import com.google.cloud.Timestamp
import com.teamfho.domino.economy.Wallet
import org.junit.jupiter.api.Test
import org.junit.jupiter.params.ParameterizedTest
import org.junit.jupiter.params.provider.ValueSource
import java.util.concurrent.Executors
import kotlin.test.*

class RewardConsumptionTests {
    private val clock = RewardClock()
    private val store = RewardFirestoreTransactions(clock.instant())
    private val repo = FirestoreRewardIntentRepository(store.firestore, clock, RewardPolicy())
    private val walletPath = "players/owner/wallet/main"
    private val stamp = Timestamp.ofTimeSecondsAndNanos(clock.instant().epochSecond, 0)
    init { store.documents[walletPath] = mapOf("coins" to 0L, "lifetimeCoinsEarned" to 0L,
        "lifetimeCoinsSpent" to 0L, "createdAt" to stamp, "updatedAt" to stamp) }
    private fun verified(): RewardIntent {
        val intent = repo.issue("owner")
        repo.verify(VerifiedAdMobEvent(intent.intentId, intent.intentId.replace("-", ""), "5224354917", clock.instant()))
        return intent
    }
    private fun ledgerPath(id: String) = "players/owner/walletTransactions/reward:$id"
    @Test fun `first consume duplicate policy change and SSV replay`() {
        val intent = verified()
        assertEquals(intent.intentId, repo.pending("owner")?.intentId)
        assertNull(repo.pending("other"))
        assertEquals("REWARD_PENDING", assertFailsWith<RewardFailure> { repo.issue("owner") }.category)
        val response = repo.consume("owner", intent.intentId)
        assertEquals(10L, response.wallet.coins); assertEquals(10L, response.reward.amount)
        assertEquals("CONSUMED", response.intent.status)
        val ledger = store.documents.getValue(ledgerPath(intent.intentId)).toMap()
        assertEquals(0L, ledger["balanceBefore"]); assertEquals(10L, ledger["balanceAfter"])
        assertEquals("CREDIT", ledger["type"]); assertEquals("REWARDED_AD", ledger["source"])
        assertEquals(1L, ledger["version"])
        assertEquals(response, repo.consume("owner", intent.intentId))
        val changedPolicy = FirestoreRewardIntentRepository(store.firestore, clock, RewardPolicy(), MonetizationPolicy(version=2,rewarded=RewardedRules(coins=20)))
        assertEquals(response, changedPolicy.consume("owner", intent.intentId))
        assertFalse(repo.verify(VerifiedAdMobEvent(intent.intentId, intent.intentId.replace("-", ""), "5224354917", clock.instant())))
        assertEquals(ledger, store.documents[ledgerPath(intent.intentId)])
        assertEquals(10L, store.documents[walletPath]?.get("coins"))
        assertEquals(10L, store.documents[walletPath]?.get("lifetimeCoinsEarned"))
        assertEquals(0L, store.documents[walletPath]?.get("lifetimeCoinsSpent"))
        assertEquals(stamp, store.documents[walletPath]?.get("createdAt"))
        assertNull(repo.pending("owner"))
        assertEquals("COOLDOWN", assertFailsWith<RewardFailure> { repo.issue("owner") }.category)
    }
    @Test fun `parallel consumes one credit and immutable ledger`() {
        val intent = verified(); val pool = Executors.newFixedThreadPool(10)
        try {
            val result = pool.invokeAll((1..30).map { java.util.concurrent.Callable { repo.consume("owner", intent.intentId) } })
            assertTrue(result.all { it.get().wallet.coins == 10L })
            assertEquals(1, store.documents.keys.count { it.contains("/walletTransactions/") })
            assertEquals(1, store.callbacks.flatten().count { it == "create:" + ledgerPath(intent.intentId) })
            assertEquals(0, store.callbacks.flatten().count { it == "update:" + ledgerPath(intent.intentId) })
        } finally { pool.shutdownNow() }
    }
    @Test fun `unknown outcome after committed transaction is safely retryable`() {
        val intent = verified(); store.failAfterCommitOnce = true
        assertEquals(503, assertFailsWith<RewardFailure> { repo.consume("owner", intent.intentId) }.httpStatus)
        assertEquals(10L, repo.consume("owner", intent.intentId).wallet.coins)
        assertEquals(1, store.documents.keys.count { it.contains("/walletTransactions/") })
    }
    @Test fun `transaction callback retry commits once`() {
        val intent = verified(); store.retryFirstCallback = true
        assertEquals(10L, repo.consume("owner", intent.intentId).wallet.coins)
        assertEquals(10L, store.documents[walletPath]?.get("lifetimeCoinsEarned"))
    }
    @ParameterizedTest @ValueSource(strings = ["ISSUED", "EXPIRED", "REJECTED", "foreign", "unknown", "missing-wallet",
        "invalid-wallet", "long-overflow", "coin-cap", "earned-cap", "ledger-exists", "policy", "source"])
    fun `rejections do not partially mutate`(case: String) {
        val intent = verified()
        var id = intent.intentId; var uid = "owner"
        val ref = "rewardIntents/$id"
        when (case) {
            "ISSUED", "EXPIRED", "REJECTED" -> store.documents[ref] = store.documents.getValue(ref) + ("status" to case)
            "foreign" -> uid = "other"
            "unknown" -> id = "00000000-0000-4000-8000-000000000000"
            "missing-wallet" -> store.documents.remove(walletPath)
            "invalid-wallet" -> store.documents[walletPath] = store.documents.getValue(walletPath) + ("coins" to -1L)
            "long-overflow" -> store.documents[walletPath] = store.documents.getValue(walletPath) + ("coins" to Long.MAX_VALUE)
            "coin-cap" -> store.documents[walletPath] = store.documents.getValue(walletPath) + ("coins" to Wallet.MAX_COINS)
            "earned-cap" -> store.documents[walletPath] = store.documents.getValue(walletPath) + ("lifetimeCoinsEarned" to Wallet.MAX_COINS)
            "ledger-exists" -> store.documents[ledgerPath(id)] = mapOf("amount" to 123L)
            "policy" -> store.documents[ref] = store.documents.getValue(ref) + ("rewardPolicyKey" to "OTHER")
            "source" -> store.documents[ref] = store.documents.getValue(ref) + ("source" to "OTHER")
        }
        val before = store.documents.toMap()
        assertFailsWith<RewardFailure> { repo.consume(uid, id) }
        assertEquals(before, store.documents)
    }
    @ParameterizedTest @ValueSource(strings = ["amount", "source", "uid", "rewardIntentId", "balanceAfter", "version", "missing"])
    fun `consumed ledger corruption is never overwritten`(field: String) {
        val intent = verified(); repo.consume("owner", intent.intentId)
        val path = ledgerPath(intent.intentId)
        if (field == "missing") store.documents.remove(path)
        else store.documents[path] = store.documents.getValue(path) + (field to "bad")
        val before = store.documents.toMap()
        assertEquals("LEDGER_CONFLICT", assertFailsWith<RewardFailure> { repo.consume("owner", intent.intentId) }.category)
        assertEquals(before, store.documents)
    }
    @Test fun `verified intent consumption after issue TTL and policy amount`() {
        val intent = verified(); clock.value = clock.instant().plusSeconds(86400)
        val configured = FirestoreRewardIntentRepository(store.firestore, clock, RewardPolicy(), MonetizationPolicy(version=2,rewarded=RewardedRules(coins=17)))
        assertEquals(10L, configured.consume("owner", intent.intentId).wallet.coins)
    }
    @Test fun `two rewards preserve spent and replay returns current wallet`() {
        val old = Timestamp.ofTimeSecondsAndNanos(clock.instant().minusSeconds(86400).epochSecond, 0)
        store.documents[walletPath] = mapOf("coins" to 77L, "lifetimeCoinsEarned" to 100L,
            "lifetimeCoinsSpent" to 23L, "createdAt" to old, "updatedAt" to old)
        val first = verified(); repo.consume("owner", first.intentId)
        clock.value = clock.instant().plusSeconds(120); store.now = clock.instant()
        val second = verified(); repo.consume("owner", second.intentId)
        assertEquals(97L, repo.consume("owner", first.intentId).wallet.coins)
        assertEquals(120L, store.documents[walletPath]?.get("lifetimeCoinsEarned"))
        assertEquals(23L, store.documents[walletPath]?.get("lifetimeCoinsSpent"))
        assertEquals(old, store.documents[walletPath]?.get("createdAt"))
        assertEquals(Timestamp.ofTimeSecondsAndNanos(clock.instant().epochSecond,0), store.documents[walletPath]?.get("updatedAt"))
        assertEquals(2, store.documents.keys.count { it.contains("/walletTransactions/") })
    }
}
