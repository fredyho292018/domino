package com.teamfho.domino.economy.reward

import com.teamfho.domino.economy.Wallet
import org.springframework.boot.context.properties.ConfigurationProperties
import java.time.Instant
import java.time.ZoneOffset

@ConfigurationProperties("domino.monetization")
data class MonetizationPolicy(val version: Long = 1, val adsEnabled: Boolean = true,
    val rewarded: RewardedRules = RewardedRules()) {
    init { require(version > 0) }
    fun response() = MonetizationResponse(version, adsEnabled, RewardedPolicyResponse(rewarded.enabled,
        rewarded.coins, RewardLimits(rewarded.maxPerRound, rewarded.cooldownSeconds,
            rewarded.maxPerHour, rewarded.maxPerDay, rewarded.maxCoinsPerDay)))
}
data class RewardedRules(val enabled: Boolean = true, val coins: Long = 10, val maxPerRound: Int = 1,
    val cooldownSeconds: Long = 120, val maxPerHour: Int = 5, val maxPerDay: Int = 20,
    val maxCoinsPerDay: Long = 200) {
    init {
        require(coins in 1..Wallet.MAX_COINS)
        // Offline opportunities are single-use; multiple grants per opportunity are not supported.
        require(maxPerRound == 1)
        require(cooldownSeconds in 0..86400 && maxPerHour in 1..1000 && maxPerDay in 1..1000)
        require(maxCoinsPerDay in coins..Wallet.MAX_COINS)
    }
}
data class RewardLimits(val perRound: Int, val cooldownSeconds: Long, val perHour: Int, val perDay: Int, val maxCoinsPerDay: Long)
data class RewardedPolicyResponse(val enabled: Boolean, val rewardCoins: Long, val limits: RewardLimits)
data class MonetizationResponse(val version: Long, val adsEnabled: Boolean, val rewarded: RewardedPolicyResponse)
enum class EligibilityReason { ADS_DISABLED, REWARDED_DISABLED, ROUND_LIMIT, COOLDOWN, HOURLY_LIMIT, DAILY_LIMIT, DAILY_COIN_LIMIT, ACCOUNT_NOT_ELIGIBLE, DEPENDENCY_UNAVAILABLE }
data class RewardRemaining(val hour: Int, val day: Int, val coinsToday: Long)
data class RewardEligibility(val eligible: Boolean, val rewardCoins: Long, val reason: EligibilityReason?,
    val nextEligibleAt: Instant?, val remaining: RewardRemaining, val serverTime: Instant)
data class RewardCreditRecord(val at: Instant, val amount: Long)
data class RewardOpportunity(val opportunityId: String, val expiresAt: Instant)

object RewardEligibilityPolicy {
    fun evaluate(policy: MonetizationPolicy, records: List<RewardCreditRecord>, now: Instant,
        amount: Long = policy.rewarded.coins, used: Boolean = false, earned: Boolean = false): RewardEligibility {
        val limits = policy.rewarded
        val hour = records.filter { it.at > now.minusSeconds(3600) }.sortedBy { it.at }
        val dayStart = now.atZone(ZoneOffset.UTC).toLocalDate().atStartOfDay(ZoneOffset.UTC).toInstant()
        val day = records.filter { !it.at.isBefore(dayStart) }
        val dailyCoins = day.fold(0L) { sum, r -> Math.addExact(sum, r.amount) }
        val remaining = RewardRemaining((limits.maxPerHour - hour.size).coerceAtLeast(0),
            (limits.maxPerDay - day.size).coerceAtLeast(0), (limits.maxCoinsPerDay-dailyCoins).coerceAtLeast(0))
        val last = records.maxOfOrNull { it.at }
        val cooldown = last?.plusSeconds(limits.cooldownSeconds)
        var next: Instant? = null
        val reason = when {
            !earned && !policy.adsEnabled -> EligibilityReason.ADS_DISABLED
            !earned && !limits.enabled -> EligibilityReason.REWARDED_DISABLED
            used -> EligibilityReason.ROUND_LIMIT
            cooldown != null && now < cooldown -> { next = cooldown; EligibilityReason.COOLDOWN }
            hour.size >= limits.maxPerHour -> { next = hour[hour.size-limits.maxPerHour].at.plusSeconds(3600); EligibilityReason.HOURLY_LIMIT }
            day.size >= limits.maxPerDay -> { next = dayStart.plusSeconds(86400); EligibilityReason.DAILY_LIMIT }
            dailyCoins > limits.maxCoinsPerDay - amount -> { next = dayStart.plusSeconds(86400); EligibilityReason.DAILY_COIN_LIMIT }
            else -> null
        }
        return RewardEligibility(reason == null, amount, reason, next, remaining, now)
    }
}
