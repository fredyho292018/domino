package com.teamfho.domino.economy.reward

import com.google.cloud.Timestamp
import org.junit.jupiter.api.Test
import java.time.Instant
import kotlin.test.*

class PersistentPolicyRewardIntegrationTests {
    private val clock=RewardClock(Instant.parse("2026-09-13T12:00:00Z"))
    private val store=RewardFirestoreTransactions(clock.instant())
    private var active=MonetizationPolicy(rewarded=RewardedRules(cooldownSeconds=0))
    private val policies=MonetizationPolicyService(MonetizationPolicyRepository { PolicyRead.Active(active) },MonetizationPolicy(),clock)
    private val repository=FirestoreRewardIntentRepository(store.firestore,clock,RewardPolicy(),policies)
    init {
        val now=Timestamp.ofTimeSecondsAndNanos(clock.instant().epochSecond,0)
        store.documents["players/u/wallet/main"]=mapOf("coins" to 0L,"lifetimeCoinsEarned" to 0L,"lifetimeCoinsSpent" to 0L,"createdAt" to now,"updatedAt" to now)
    }
    private fun verified():RewardIntent = repository.issue("u").also {
        repository.verify(VerifiedAdMobEvent(it.intentId,it.intentId,"5224354917",clock.instant()))
    }
    @Test fun `old amount retained new amount updated without recreating repository`() {
        val old=verified()
        active=active.copy(version=2,rewarded=active.rewarded.copy(coins=15));policies.invalidate()
        assertEquals(10L,repository.consume("u",old.intentId).reward.amount)
        assertEquals(10L,repository.consume("u",old.intentId).wallet.coins)
        val next=verified();assertEquals(15L,next.rewardAmountSnapshot);assertEquals(2L,next.policyVersion)
        assertEquals(25L,repository.consume("u",next.intentId).wallet.coins)
        assertEquals(2,store.documents.keys.count{it.contains("walletTransactions")})
    }
    @Test fun `new hourly limit enforced inside consume of existing verified intent`() {
        repeat(3){repository.consume("u",verified().intentId)}
        val pending=verified()
        active=active.copy(version=2,rewarded=active.rewarded.copy(maxPerHour=3));policies.invalidate()
        assertEquals(EligibilityReason.HOURLY_LIMIT,repository.eligibility("u").reason)
        assertEquals("HOURLY_LIMIT",assertFailsWith<RewardFailure>{repository.consume("u",pending.intentId)}.category)
        assertEquals(30L,store.documents["players/u/wallet/main"]?.get("coins"))
        assertEquals("VERIFIED",store.documents["rewardIntents/${pending.intentId}"]?.get("status"))
    }
    @Test fun `kill switches block new activity but earned intent can settle`() {
        val pending=verified();active=active.copy(version=2,adsEnabled=false);policies.invalidate()
        assertEquals(EligibilityReason.ADS_DISABLED,repository.eligibility("u").reason)
        assertEquals("ADS_DISABLED",assertFailsWith<RewardFailure>{repository.issue("u")}.category)
        assertEquals(10L,repository.consume("u",pending.intentId).wallet.coins)
        active=active.copy(version=3,adsEnabled=true,rewarded=active.rewarded.copy(enabled=false));policies.invalidate()
        assertEquals("REWARDED_DISABLED",assertFailsWith<RewardFailure>{repository.opportunity("u")}.category)
    }
    @Test fun `cooldown change uses new server policy`() {
        repository.consume("u",verified().intentId)
        active=active.copy(version=2,rewarded=active.rewarded.copy(cooldownSeconds=180));policies.invalidate()
        assertEquals(clock.instant().plusSeconds(180),repository.eligibility("u").nextEligibleAt)
    }
}
