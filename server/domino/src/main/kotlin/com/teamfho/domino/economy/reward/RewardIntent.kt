package com.teamfho.domino.economy.reward

import java.time.Duration
import java.time.Instant
import org.springframework.boot.context.properties.ConfigurationProperties

enum class RewardIntentStatus { ISSUED, VERIFIED, EXPIRED, REJECTED }
enum class AdUnitEnvironment { DEVELOPMENT, PRODUCTION }
data class RewardIntent(
    val intentId: String, val uid: String, val status: RewardIntentStatus,
    val createdAt: Instant, val expiresAt: Instant, val adUnitEnvironment: AdUnitEnvironment,
    val verifiedAt: Instant? = null, val adMobTransactionId: String? = null,
    val source: String = "REWARDED_AD", val rewardPolicyKey: String = "REWARDED_AD_STANDARD"
)
data class RewardPreview(val type: String = "COINS", val previewAmount: Int = 10)
data class RewardIntentResponse(val intentId: String, val status: RewardIntentStatus,
    val expiresAt: Instant, val reward: RewardPreview = RewardPreview()) {
    companion object { fun from(intent: RewardIntent) = RewardIntentResponse(intent.intentId, intent.status, intent.expiresAt) }
}
class RewardFailure(val category: String, val httpStatus: Int = 400) : RuntimeException(category)
fun reject(category: String, status: Int = 400): Nothing = throw RewardFailure(category, status)
fun opaqueId(id: String) = Regex("[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}").matches(id)

@ConfigurationProperties("domino.economy.rewarded-ad")
data class RewardPolicy(
    val intentTtl: Duration = Duration.ofMinutes(10),
    val environment: AdUnitEnvironment = AdUnitEnvironment.DEVELOPMENT,
    // SSV ad_unit is the numeric unit component, not the publisher/unit SDK string.
    val developmentAdUnit: String = "5224354917",
    val productionAdUnit: String = "8685423732",
    val clockSkew: Duration = Duration.ofMinutes(2),
    val maxEventAge: Duration = Duration.ofMinutes(10),
    val keyCacheTtl: Duration = Duration.ofHours(12)
) {
    init {
        require(!intentTtl.isNegative && !intentTtl.isZero && intentTtl <= Duration.ofHours(1))
        require(!clockSkew.isNegative && clockSkew <= Duration.ofMinutes(5))
        require(!maxEventAge.isNegative && !maxEventAge.isZero && maxEventAge <= Duration.ofHours(1))
        require(!keyCacheTtl.isNegative && !keyCacheTtl.isZero && keyCacheTtl <= Duration.ofHours(24))
        require(developmentAdUnit.matches(Regex("[0-9]{1,30}")) && productionAdUnit.matches(Regex("[0-9]{1,30}")))
        require(developmentAdUnit != productionAdUnit)
    }
    fun validate(intent: RewardIntent, event: VerifiedAdMobEvent, now: Instant) {
        if (intent.source != "REWARDED_AD" || intent.rewardPolicyKey != "REWARDED_AD_STANDARD")
            reject("SSV_INTENT_INVALID")
        if (intent.adUnitEnvironment != environment ||
            event.adUnit != if (environment == AdUnitEnvironment.DEVELOPMENT) developmentAdUnit else productionAdUnit)
            reject("SSV_AD_UNIT_INVALID")
        if (intent.status != RewardIntentStatus.ISSUED) reject("SSV_INTENT_USED", 409)
        if (!now.isBefore(intent.expiresAt)) reject("SSV_INTENT_EXPIRED")
        if (event.timestamp.isAfter(now.plus(clockSkew)) || event.timestamp.isBefore(now.minus(maxEventAge)) ||
            event.timestamp.isBefore(intent.createdAt.minus(clockSkew)) || event.timestamp.isAfter(intent.expiresAt))
            reject("SSV_TIMESTAMP_INVALID")
    }
}

interface RewardIntentRepository {
    fun issue(uid: String): RewardIntent
    fun status(uid: String, intentId: String): RewardIntent
    fun verify(event: VerifiedAdMobEvent): Boolean // true=new transition; false=identical duplicate
}
