package com.teamfho.domino.player

import com.teamfho.domino.catalog.OnboardingQuestionType
import com.teamfho.domino.security.FirebaseIdentity
import java.time.Instant

class OnboardingFailure(val code: String, val status: Int = 409): RuntimeException(code)
internal fun onboardingCheck(ok: Boolean, code: String, status: Int = 409) { if(!ok) throw OnboardingFailure(code,status) }
data class OnboardingAnswer(val questionKey: String, val type: OnboardingQuestionType, val optionKey: String)
data class OnboardingDomainRevisions(val profile: Long, val preferences: Long, val domino: Long)
data class OnboardingResponse(val status: OnboardingStatus, val catalogVersion: Int?, val currentStepKey: String?,
    val currentSubstepKey: String?, val lastCompletedStepKey: String?, val completedStepKeys: List<String>,
    val skippedStepKeys: List<String>, val startedAt: Instant?, val completedAt: Instant?, val updatedAt: Instant,
    val revision: Long, val completionOrigin: CompletionOrigin?, val requiredFieldsMissing: List<String>,
    val domainRevisions: OnboardingDomainRevisions, val answers: List<OnboardingAnswer>)
data class OnboardingMutationResponse(val onboarding: OnboardingResponse, val domino: DominoProfile? = null)
data class OnboardingStartRequest(val operationId: String, val expectedRevision: Long)
enum class OnboardingStepAction { SAVE, SKIP }
data class SaveStepRequest(val operationId: String, val expectedRevision: Long, val catalogVersion: Int,
    val domainRevisions: Map<String,Long>, val action: OnboardingStepAction, val answers: List<OnboardingAnswer>)
data class OnboardingCursorRequest(val operationId: String, val expectedRevision: Long, val catalogVersion: Int,
    val targetStepKey: String, val targetSubstepKey: String? = null)
data class OnboardingCompleteRequest(val operationId: String, val expectedRevision: Long, val catalogVersion: Int)

object OnboardingWriteAuthorization {
    fun check(identity: FirebaseIdentity) {
        if(identity.isAnonymous && !identity.hasPasswordProvider) return
        when(identity.signInProvider) {
            "password" -> onboardingCheck(identity.isEmailVerified,"EMAIL_VERIFICATION_REQUIRED",403)
            "google.com","facebook.com","phone" -> return
            else -> {
                if(identity.hasPasswordProvider) onboardingCheck(identity.isEmailVerified,"EMAIL_VERIFICATION_REQUIRED",403)
                else throw OnboardingFailure("AUTH_CONTEXT_UNSUPPORTED",403)
            }
        }
    }
}

// Future BE-04 supplies authoritative immutable-catalog validation inside the same transaction.
// No I/O or keys are invented by the default implementation.
fun interface OnboardingCoachValidation {
    fun validate(tx: OnboardingProgressTransaction, catalogVersion: Int?, key: String?)
}
object UnavailableOnboardingCoach: OnboardingCoachValidation {
    override fun validate(tx: OnboardingProgressTransaction, catalogVersion: Int?, key: String?) {
        throw OnboardingFailure("DEPENDENCY_UNAVAILABLE",503)
    }
}
