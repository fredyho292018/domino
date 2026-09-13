package com.teamfho.domino.economy.reward

import org.springframework.boot.context.properties.ConfigurationProperties
import org.slf4j.LoggerFactory
import java.time.Clock
import java.time.Instant

@ConfigurationProperties("domino.monetization.policy")
data class MonetizationPolicyCacheSettings(val cacheSeconds: Long = 60) {
    init { require(cacheSeconds in 1..3600) }
}
enum class MonetizationPolicySource { FIRESTORE, FALLBACK_CONFIG }
data class ResolvedMonetizationPolicy(val policy: MonetizationPolicy, val source: MonetizationPolicySource, val loadedAt: Instant)

class MonetizationPolicyService(
    private val repository: MonetizationPolicyRepository,
    private val fallback: MonetizationPolicy,
    private val clock: Clock = Clock.systemUTC(),
    private val cacheSettings: MonetizationPolicyCacheSettings = MonetizationPolicyCacheSettings()
) {
    private val log = LoggerFactory.getLogger(javaClass)
    private var cached: ResolvedMonetizationPolicy? = null
    private var refreshAt = Instant.MIN
    private var lastAcceptedFirestore: MonetizationPolicy? = null

    // One refresh in flight per application instance. Immutable snapshots are captured
    // once per economic operation, including retries of its Firestore transaction.
    @Synchronized fun resolve(): ResolvedMonetizationPolicy {
        val now = clock.instant()
        cached?.takeIf { now < refreshAt }?.let {
            log.debug("MONETIZATION_POLICY_CACHE_HIT version={}", it.policy.version)
            return it
        }
        val read = try { repository.read() } catch (_: Exception) { PolicyRead.Unavailable }
        val result = when (read) {
            is PolicyRead.Active -> {
                val previous = lastAcceptedFirestore
                if (previous != null && (read.policy.version < previous.version ||
                        (read.policy.version == previous.version && read.policy != previous))) {
                    log.warn("MONETIZATION_POLICY_INVALID reason=VERSION_NOT_INCREMENTED")
                    fallback(now)
                } else {
                    lastAcceptedFirestore = read.policy
                    ResolvedMonetizationPolicy(read.policy, MonetizationPolicySource.FIRESTORE, now)
                }
            }
            is PolicyRead.Invalid -> {
                log.warn("MONETIZATION_POLICY_INVALID reason={}", read.reason)
                fallback(now)
            }
            PolicyRead.NotFound -> fallback(now)
            PolicyRead.Unavailable -> {
                log.warn("MONETIZATION_POLICY_STORAGE_UNAVAILABLE")
                cached ?: fallback(now)
            }
        }
        cached = result
        refreshAt = now.plusSeconds(cacheSettings.cacheSeconds)
        log.info("MONETIZATION_POLICY_LOADED source={} version={}", result.source, result.policy.version)
        log.info("MONETIZATION_POLICY_REFRESHED version={}", result.policy.version)
        return result
    }

    @Synchronized fun invalidate() { refreshAt = Instant.MIN }
    private fun fallback(now: Instant) = ResolvedMonetizationPolicy(fallback, MonetizationPolicySource.FALLBACK_CONFIG, now)
}
