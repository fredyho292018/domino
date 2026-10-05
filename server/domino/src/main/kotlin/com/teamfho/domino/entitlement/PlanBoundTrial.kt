package com.teamfho.domino.entitlement

import com.teamfho.domino.catalog.*
import com.teamfho.domino.player.*
import java.time.Instant

enum class CapabilityImplementationStatus { IMPLEMENTED, PARTIALLY_IMPLEMENTED, NOT_IMPLEMENTED, FUTURE }
data class CommercialBenefit(val key:String,val implementationStatus:CapabilityImplementationStatus,val currentlyUsable:Boolean=false)
// Conservative availability: a catalog declaration is not evidence of a complete service/enforcement path.
object CommercialCapabilityImplementation {
    fun describe(key:String)=CommercialBenefit(key,when(key) {
        "ADVANCED_STATS","NO_ADS","BOTS" -> CapabilityImplementationStatus.PARTIALLY_IMPLEMENTED
        "GAME_REVIEW","MOVE_EXPLANATIONS","PUZZLES","LESSONS","COACH_GAMES" -> CapabilityImplementationStatus.NOT_IMPLEMENTED
        else -> CapabilityImplementationStatus.FUTURE
    })
}
data class PlanBoundTrial(val trialPlan:String,val trialBillingPeriod:MembershipBillingPeriod,
    val catalogVersion:Int,val entitlementPolicyVersion:Int,val trialStartedAt:Instant,val trialEndsAt:Instant,
    val reminderAt:Instant,val commercialPlanBenefits:List<CommercialBenefit>,
    val effectiveFeatures:Set<EntitlementFeature>,val effectiveLimits:Map<EntitlementLimit,LimitValue>) {
    init {
        require(trialPlan in setOf("DIAMOND","PLATINUM","GOLD"))
        require(catalogVersion>0 && entitlementPolicyVersion>0 && trialEndsAt>trialStartedAt)
        require(reminderAt>=trialStartedAt && reminderAt<=trialEndsAt)
        require(effectiveLimits.keys==EntitlementLimit.entries.toSet())
        require(commercialPlanBenefits.none{it.currentlyUsable && it.implementationStatus!=CapabilityImplementationStatus.IMPLEMENTED})
    }
}
data class TrialStateResponse(val trialStatus:String,val trialPlan:String?=null,
    val trialBillingPeriod:MembershipBillingPeriod?=null,val trialStartedAt:Instant?=null,
    val trialEndsAt:Instant?=null,val reminderAt:Instant?=null,val legacy:Boolean=false,
    val commercialPlanBenefits:List<CommercialBenefit> = emptyList())

object PlanBoundTrials {
    fun period(value:String?)=when(value){"MONTHLY"->MembershipBillingPeriod.MONTHLY;"ANNUAL","YEARLY"->MembershipBillingPeriod.YEARLY;else->null}
    fun create(tx:OnboardingProgressTransaction,request:TrialActivationRequest,policy:SubscriptionPolicy,now:Instant):PlanBoundTrial {
        val period=period(request.billingPeriod)
        onboardingCheck(!request.plan.isNullOrBlank() && period!=null,"REQUEST_INVALID",400)
        onboardingCheck(request.plan!="FRIENDS_AND_FAMILY","TRIAL_PLAN_NOT_SUPPORTED",403)
        onboardingCheck(policy.trialReminderBeforeEndDays>=0 && policy.trialReminderBeforeEndDays<policy.promotionalTrialDays,"TRIAL_POLICY_INVALID",503)
        val version=(tx.read("systemConfig/membershipCatalog")?.get("publishedVersion") as? Number)?.toInt()
            ?: throw OnboardingFailure("DEPENDENCY_UNAVAILABLE",503)
        val raw=tx.read("membershipCatalogs/$version") ?: throw OnboardingFailure("DEPENDENCY_UNAVAILABLE",503)
        val catalog=try{MembershipCatalogValidation.decode(raw,version)}catch(_:Exception){throw OnboardingFailure("DEPENDENCY_UNAVAILABLE",503)}
        val plan=catalog.plans.singleOrNull{it.key==request.plan && it.active && it.key!="FREE"}
            ?: throw OnboardingFailure("TRIAL_PLAN_NOT_SUPPORTED",403)
        onboardingCheck(catalog.billingProducts.any{it.planKey==plan.key&&it.billingPeriod==period},"TRIAL_BILLING_PERIOD_NOT_SUPPORTED",400)
        val target=catalog.targetPolicy.plans.getValue(plan.key)
        val benefits=catalog.planFeatures.filter{it.planKey==plan.key&&it.included}.map{CommercialCapabilityImplementation.describe(it.featureKey)}
        // Reuse versioned technical permissions, never infer commercial services from similarly named keys.
        // Advanced statistics has no complete implemented service; legacy grants are not changed.
        val features=target.capabilities.map{EntitlementFeature.valueOf(it)}.filter{it!=EntitlementFeature.ADVANCED_STATS}.toSet()
        val limits=target.limits.mapKeys{EntitlementLimit.valueOf(it.key)}.mapValues{LimitValue(it.value.unlimited,it.value.maximum)}
        val end=now.plusSeconds(policy.promotionalTrialDays*86400L)
        return PlanBoundTrial(plan.key,period!!,version,catalog.targetPolicy.version,now,end,
            end.minusSeconds(policy.trialReminderBeforeEndDays*86400L),benefits,features,limits)
    }
    fun response(grant:EntitlementGrant?,now:Instant):TrialStateResponse {
        if(grant==null)return TrialStateResponse("NOT_STARTED")
        val bound=grant.planBoundTrial
        val status=if(grant.status==GrantStatus.REVOKED)"REVOKED" else if(now<grant.validFrom)"SCHEDULED" else if(now<grant.validUntil)"ACTIVE" else "EXPIRED"
        return TrialStateResponse(status,bound?.trialPlan,bound?.trialBillingPeriod,grant.validFrom,grant.validUntil,bound?.reminderAt,bound==null,bound?.commercialPlanBenefits?:emptyList())
    }
}
