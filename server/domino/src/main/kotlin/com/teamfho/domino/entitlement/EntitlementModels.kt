package com.teamfho.domino.entitlement

import java.time.Instant

enum class Plan { FREE, PREMIUM }
enum class EntitlementSource { PROMOTIONAL_TRIAL, ADMIN_GRANT, GOOGLE_PLAY, APPLE_APP_STORE }
enum class GrantStatus { ACTIVE, REVOKED }
enum class EntitlementFeature {
    PUBLIC_DUEL, PUBLIC_PARTNERS, FOLLOW_PLAYER, FRIENDS, FRIEND_REQUESTS,
    PARTY_CREATE, PARTY_INVITE, PRIVATE_DUEL, PRIVATE_PARTNERS, CHOOSE_2V2_PARTNER,
    PARTY_MATCHMAKING, FULL_HISTORY, FULL_REPLAY, ADVANCED_STATS, PREMIUM_THEMES
}
enum class EntitlementLimit { FRIENDS_MAX, HISTORY_MAX, REPLAY_MAX }
data class LimitValue(val unlimited: Boolean = false, val maximum: Int? = null) {
    init { require(if (unlimited) maximum == null else maximum != null && maximum >= 0) }
}
data class EntitlementGrant(val id: String, val source: EntitlementSource, val plan: Plan = Plan.PREMIUM,
    val status: GrantStatus = GrantStatus.ACTIVE, val validFrom: Instant, val validUntil: Instant,
    val createdAt: Instant, val policyVersion: Long, val reason: String, val grantedBy: String) {
    init { require(id.isNotBlank() && '/' !in id && plan == Plan.PREMIUM && validUntil > validFrom)
        require(reason.isNotBlank() && grantedBy.isNotBlank() && policyVersion > 0) }
}
data class EntitlementState(val revision: Long = 0, val trialConsumed: Boolean = false,
    val grants: List<EntitlementGrant> = emptyList())
@org.springframework.boot.context.properties.ConfigurationProperties("domino.subscription")
data class SubscriptionPolicy(val enabled: Boolean = true, val policyVersion: Long = 1,
    val promotionalTrialEnabled: Boolean = true, val promotionalTrialDays: Int = 7,
    val trialRequiresLinkedAccount: Boolean = false,
    val freeFriendsMax: Int = 5, val premiumFriendsMax: Int = 100,
    val freeHistoryMax: Int = 10, val freeReplayMax: Int = 3,
    val freeFeatures: Set<EntitlementFeature> = FREE_FEATURES,
    val premiumFeatures: Set<EntitlementFeature> = EntitlementFeature.entries.toSet()) {
    init {
        require(policyVersion > 0 && promotionalTrialDays in 1..30)
        require(freeFriendsMax in 0..10000 && premiumFriendsMax in freeFriendsMax..10000)
        require(freeHistoryMax in 1..100 && freeReplayMax in 1..100)
        require(freeFeatures.containsAll(PUBLIC_FEATURES) && premiumFeatures.containsAll(freeFeatures))
        require(EntitlementFeature.FULL_HISTORY !in freeFeatures && EntitlementFeature.FULL_REPLAY !in freeFeatures)
    }
    companion object {
        val PUBLIC_FEATURES = setOf(EntitlementFeature.PUBLIC_DUEL, EntitlementFeature.PUBLIC_PARTNERS)
        val FREE_FEATURES = PUBLIC_FEATURES + setOf(EntitlementFeature.FOLLOW_PLAYER, EntitlementFeature.FRIENDS, EntitlementFeature.FRIEND_REQUESTS)
    }
}
data class EffectiveEntitlements(val plan: Plan, val sources: Set<EntitlementSource>, val status: String,
    val validUntil: Instant?, val trialActive: Boolean, val trialEndsAt: Instant?, val trialConsumed: Boolean,
    val features: Set<EntitlementFeature>, val limits: Map<EntitlementLimit, LimitValue>,
    val policyVersion: Long, val revision: Long, val serverTime: Instant, val nextTransitionAt: Instant?)
data class EntitlementSummary(val availability: String, val snapshot: EffectiveEntitlements? = null,
    val trialGranted: Boolean = false)
class EntitlementFailure(val code: String, val feature: EntitlementFeature? = null, val currentPlan: Plan? = null,
    val limitKey: EntitlementLimit? = null, val current: Int? = null, val maximum: Int? = null) : RuntimeException(code)

object EntitlementResolver {
    fun resolve(state: EntitlementState, policy: SubscriptionPolicy, now: Instant): EffectiveEntitlements {
        val active = state.grants.filter { it.status == GrantStatus.ACTIVE && now >= it.validFrom && now < it.validUntil }
        val premium = active.isNotEmpty()
        val trial = state.grants.firstOrNull { it.source == EntitlementSource.PROMOTIONAL_TRIAL }
        // Merge contiguous coverage only: a future grant after a gap does not bridge that gap.
        var until = active.maxOfOrNull { it.validUntil }
        if (until != null) for (g in state.grants.filter { it.status == GrantStatus.ACTIVE }.sortedBy { it.validFrom })
            if (g.validFrom <= until!! && g.validUntil > until!!) until = g.validUntil
        val features = if (premium) policy.premiumFeatures else policy.freeFeatures
        return EffectiveEntitlements(if(premium) Plan.PREMIUM else Plan.FREE, active.map { it.source }.toSet(),
            if(premium) "ACTIVE" else if(state.trialConsumed) "EXPIRED" else "FREE", until,
            active.any { it.source == EntitlementSource.PROMOTIONAL_TRIAL }, trial?.validUntil, state.trialConsumed,
            features, mapOf(EntitlementLimit.FRIENDS_MAX to LimitValue(maximum=if(premium) policy.premiumFriendsMax else policy.freeFriendsMax),
                EntitlementLimit.HISTORY_MAX to (if(EntitlementFeature.FULL_HISTORY in features) LimitValue(true) else LimitValue(maximum=policy.freeHistoryMax)),
                EntitlementLimit.REPLAY_MAX to (if(EntitlementFeature.FULL_REPLAY in features) LimitValue(true) else LimitValue(maximum=policy.freeReplayMax))),
            policy.policyVersion, state.revision, now,
            state.grants.filter { it.status == GrantStatus.ACTIVE }.flatMap { listOf(it.validFrom,it.validUntil) }.filter { it > now }.minOrNull())
    }
}
