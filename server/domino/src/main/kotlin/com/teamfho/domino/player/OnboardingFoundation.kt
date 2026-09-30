package com.teamfho.domino.player

import java.time.Instant

enum class ExperienceLevel { BEGINNER, RULES_KNOWN, STRATEGY, COMPETITIVE }
enum class NotificationCategory { ACCOUNT_SECURITY, MEMBERSHIP_TRANSACTIONAL, GAME_SOCIAL, MARKETING }
enum class NotificationChannel { IN_APP, PUSH, EMAIL }
enum class NotificationChoice { ENABLED, DISABLED }
data class PlayerPreferences(val preferredLocale: String, val timeZone: String? = null,
    val notificationPreferences: Map<NotificationCategory, Map<NotificationChannel, NotificationChoice>> = emptyMap(),
    val revision: Long = 0, val updatedAt: Instant) {
    init { require(revision >= 0); require(preferredLocale == PlayerProfileRules.locale(preferredLocale)); PlayerProfileRules.timeZone(timeZone) }
}
data class DominoProfile(val experienceLevel: ExperienceLevel? = null, val preferredCoachKey: String? = null,
    val selectedCoachCatalogVersion: Int? = null, val revision: Long = 0, val updatedAt: Instant) {
    init {
        require(revision >= 0)
        require((preferredCoachKey == null) == (selectedCoachCatalogVersion == null))
        require(preferredCoachKey == null || Regex("[A-Z][A-Z0-9_]{0,63}").matches(preferredCoachKey))
        require(selectedCoachCatalogVersion == null || selectedCoachCatalogVersion > 0)
    }
}
enum class OnboardingStatus { NOT_STARTED, IN_PROGRESS, COMPLETED }
enum class CompletionOrigin { FLOW, LEGACY_EXEMPT }

// No HTTP binding and no completion mutation: catalog-aware completion belongs to a later phase.
data class PlayerOnboarding(val status: OnboardingStatus = OnboardingStatus.NOT_STARTED,
    val catalogVersion: Int? = null, val currentStepKey: String? = null, val currentSubstepKey: String? = null,
    val lastCompletedStepKey: String? = null, val completedStepKeys: List<String> = emptyList(),
    val skippedStepKeys: List<String> = emptyList(), val startedAt: Instant? = null,
    val completedAt: Instant? = null, val updatedAt: Instant, val revision: Long = 0,
    val completionOrigin: CompletionOrigin? = null) {
    init {
        require(revision >= 0)
        require(catalogVersion == null || catalogVersion > 0)
        val keys = completedStepKeys + skippedStepKeys + listOfNotNull(currentStepKey, currentSubstepKey, lastCompletedStepKey)
        require(keys.all { Regex("[A-Z][A-Z0-9_]{0,63}").matches(it) })
        require(completedStepKeys.distinct().size == completedStepKeys.size && skippedStepKeys.distinct().size == skippedStepKeys.size)
        require(completedStepKeys.intersect(skippedStepKeys.toSet()).isEmpty())
        require(lastCompletedStepKey == null || lastCompletedStepKey in completedStepKeys)
        require(currentSubstepKey == null || currentStepKey != null)
        require(startedAt == null || updatedAt >= startedAt)
        require(completedAt == null || updatedAt >= completedAt)
        when (status) {
            OnboardingStatus.NOT_STARTED -> require(catalogVersion == null && currentStepKey == null && startedAt == null &&
                completedAt == null && completionOrigin == null && completedStepKeys.isEmpty() && skippedStepKeys.isEmpty() && revision == 0L)
            OnboardingStatus.IN_PROGRESS -> require(catalogVersion != null && currentStepKey != null && startedAt != null &&
                completedAt == null && completionOrigin == null && revision > 0)
            OnboardingStatus.COMPLETED -> {
                require(currentStepKey == null && currentSubstepKey == null && completedAt != null && completionOrigin != null)
                if (completionOrigin == CompletionOrigin.FLOW) require(catalogVersion != null && startedAt != null && completedAt >= startedAt && revision > 0)
                else require(catalogVersion == null && startedAt == null && completedStepKeys.isEmpty() && skippedStepKeys.isEmpty())
            }
        }
    }
    fun start(version: Int, firstStep: String, at: Instant): PlayerOnboarding {
        check(status == OnboardingStatus.NOT_STARTED)
        return copy(status = OnboardingStatus.IN_PROGRESS, catalogVersion = version, currentStepKey = firstStep,
            startedAt = at, updatedAt = at, revision = 1)
    }
}

data class OnboardingRolloutBoundary(val createdAtCutoff: Instant)
object OnboardingInitialization {
    fun state(createdAt: Instant, boundary: OnboardingRolloutBoundary, at: Instant): PlayerOnboarding {
        require(at >= createdAt)
        return if (createdAt < boundary.createdAtCutoff) PlayerOnboarding(status = OnboardingStatus.COMPLETED,
            completedAt = at, updatedAt = at, completionOrigin = CompletionOrigin.LEGACY_EXEMPT)
        else PlayerOnboarding(updatedAt = at)
    }
}
