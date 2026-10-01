package com.teamfho.domino.entitlement

import com.teamfho.domino.catalog.GameCatalogCodec
import com.teamfho.domino.match.MatchCodec
import com.teamfho.domino.player.*
import com.teamfho.domino.security.FirebaseIdentity
import java.time.Clock
import java.time.Instant
import java.util.UUID

data class TrialActivationRequest(val operationId:String,val expectedPolicyVersion:Long)
enum class TrialActivationOutcome { ACTIVATED, ALREADY_ACTIVE }
data class TrialActivationResponse(val operationId:String,val outcome:TrialActivationOutcome,val entitlements:EntitlementSummary)
data class TrialEligibilityResponse(val state:String,val eligible:Boolean,val policyVersion:Long?,val periodDays:Int?,val activationMode:String="EXPLICIT")

// Shared buffered Firestore transaction adapter; no dependence on onboarding state or catalog.
class TrialActivationService(private val repository:OnboardingProgressRepository,
    private val fallback:SubscriptionPolicy=SubscriptionPolicy(),private val clock:Clock=Clock.systemUTC()) {
    private val grantId="initial-premium-trial"
    private fun root(id:FirebaseIdentity):String {
        onboardingCheck(id.uid.isNotBlank() && id.uid.length<=128 && '/' !in id.uid && id.uid !in setOf(".",".."),"REQUEST_INVALID",400)
        return "players/${id.uid}"
    }
    private data class Inputs(val policy:SubscriptionPolicy,val player:Map<String,Any>?,val test:Map<String,Any>?,
        val state:EntitlementState,val marker:Map<String,Any>?,val grant:EntitlementGrant?,val audit:Map<String,Any>?)
    private fun inputs(tx:OnboardingProgressTransaction,r:String,id:FirebaseIdentity):Inputs {
        val policy=tx.read("systemConfig/subscriptionPolicy")?.let{MatchCodec.read(it,SubscriptionPolicy::class.java)} ?: fallback
        val player=tx.read(r);val test=tx.read("developmentTestAccounts/${id.uid}")
        val state=tx.read("$r/entitlementState/current")?.let{MatchCodec.read(it,EntitlementState::class.java)} ?: EntitlementState()
        val marker=tx.read("$r/promotions/$grantId")
        val grant=tx.read("$r/entitlementGrants/$grantId")?.let{MatchCodec.read(it,EntitlementGrant::class.java)}
        val audit=tx.read("$r/entitlementAudit/TRIAL_GRANTED")
        onboardingCheck(state.revision>=0,"DEPENDENCY_UNAVAILABLE",503)
        val projected=state.grants.filter{it.source==EntitlementSource.PROMOTIONAL_TRIAL}
        onboardingCheck(projected.size<=1 && projected.all{it.id==grantId},"DEPENDENCY_UNAVAILABLE",503)
        if(marker!=null || grant!=null || projected.isNotEmpty()) {
            onboardingCheck(marker!=null && grant!=null && grant.id==grantId && grant.source==EntitlementSource.PROMOTIONAL_TRIAL,
                "DEPENDENCY_UNAVAILABLE",503)
            onboardingCheck(marker!!["trialConsumed"]==true && marker["grantId"]==grantId,"DEPENDENCY_UNAVAILABLE",503)
            onboardingCheck(marker["trialGrantedAt"]==grant!!.validFrom.toString() && marker["trialEndsAt"]==grant.validUntil.toString(),"DEPENDENCY_UNAVAILABLE",503)
            onboardingCheck(projected.isEmpty() || projected.single()==grant,"DEPENDENCY_UNAVAILABLE",503)
        } else onboardingCheck(audit==null,"DEPENDENCY_UNAVAILABLE",503)
        return Inputs(policy,player,test,state,marker,grant,audit)
    }
    private fun effective(i:Inputs):EntitlementState = if(i.grant!=null)
        i.state.copy(trialConsumed=true,grants=i.state.grants.filter{it.id!=grantId}+i.grant) else i.state
    private fun activeTrial(i:Inputs,now:Instant)=i.grant?.let{it.status==GrantStatus.ACTIVE && now>=it.validFrom && now<it.validUntil}==true
    private fun paid(i:Inputs,now:Instant)=i.state.grants.any{it.status==GrantStatus.ACTIVE && it.source!=EntitlementSource.PROMOTIONAL_TRIAL && now>=it.validFrom && now<it.validUntil}
    private fun eligible(i:Inputs,id:FirebaseIdentity) = i.player?.get("status")=="ACTIVE" && i.test?.get("isTestAccount")!=true &&
        (!i.policy.trialRequiresLinkedAccount || (!id.isAnonymous && i.player["accountType"]=="REGISTERED"))
    fun eligibility(id:FirebaseIdentity):TrialEligibilityResponse = try {
        OnboardingWriteAuthorization.check(id)
        repository.transaction {tx->val i=inputs(tx,root(id),id);val now=clock.instant()
            val state=when {
                !eligible(i,id)->"INELIGIBLE"
                paid(i,now)->"CONVERTED"
                activeTrial(i,now)->"ACTIVE"
                i.state.trialConsumed || i.marker!=null || i.grant!=null->"EXPIRED"
                !i.policy.enabled || !i.policy.promotionalTrialEnabled->"INELIGIBLE"
                else->"NOT_STARTED"
            }
            TrialEligibilityResponse(state,state=="NOT_STARTED",i.policy.policyVersion,i.policy.promotionalTrialDays)
        }
    } catch(e:OnboardingFailure) {
        if(e.status==403)TrialEligibilityResponse("INELIGIBLE",false,null,null) else TrialEligibilityResponse("UNKNOWN",false,null,null)
    } catch(_:Exception){TrialEligibilityResponse("UNKNOWN",false,null,null)}

    fun activate(id:FirebaseIdentity,request:TrialActivationRequest):TrialActivationResponse {
        OnboardingWriteAuthorization.check(id)
        val r=root(id)
        onboardingCheck(runCatching{UUID.fromString(request.operationId).toString()==request.operationId}.getOrDefault(false) && request.expectedPolicyVersion>0,"REQUEST_INVALID",400)
        val hash=java.security.MessageDigest.getInstance("SHA-256").digest(("TRIAL_ACTIVATE\n"+GameCatalogCodec.semantic(request)).toByteArray(Charsets.UTF_8)).joinToString(""){"%02x".format(it)}
        return repository.transaction {tx->
            val now=clock.instant();val receiptPath="$r/trialActivationReceipts/${request.operationId}"
            val receipt=tx.read(receiptPath)
            if(receipt!=null) {
                val expiry=receipt["expiresAt"] as? com.google.cloud.Timestamp ?: throw OnboardingFailure("DEPENDENCY_UNAVAILABLE",503)
                if(now<Instant.ofEpochSecond(expiry.seconds,expiry.nanos.toLong())) {
                    onboardingCheck(receipt["canonicalRequestHash"]==hash && receipt["operationType"]=="TRIAL_ACTIVATE","IDEMPOTENCY_CONFLICT")
                    onboardingCheck(tx.read(r)?.get("status")=="ACTIVE","TRIAL_NOT_ELIGIBLE",403)
                    return@transaction GameCatalogCodec.decode(receipt["responseSnapshot"] ?: throw OnboardingFailure("DEPENDENCY_UNAVAILABLE",503),TrialActivationResponse::class.java)
                }
            }
            val i=inputs(tx,r,id)
            onboardingCheck(i.policy.policyVersion==request.expectedPolicyVersion,"TRIAL_POLICY_VERSION_MISMATCH")
            onboardingCheck(eligible(i,id),"TRIAL_NOT_ELIGIBLE",403)
            onboardingCheck(!paid(i,now),"TRIAL_NOT_APPLICABLE")
            val already=activeTrial(i,now)
            val next:EntitlementState
            if(already) {
                next=effective(i).let{if(it!=i.state)it.copy(revision=i.state.revision+1) else it}
            } else {
                onboardingCheck(!i.state.trialConsumed && i.marker==null && i.grant==null,"TRIAL_ALREADY_CONSUMED")
                onboardingCheck(i.policy.enabled && i.policy.promotionalTrialEnabled,"TRIAL_DISABLED",403)
                val grant=EntitlementGrant(grantId,EntitlementSource.PROMOTIONAL_TRIAL,validFrom=now,
                    validUntil=now.plusSeconds(i.policy.promotionalTrialDays*86400L),createdAt=now,policyVersion=i.policy.policyVersion,
                    reason="WELCOME_PROMOTION",grantedBy="explicit-activation")
                next=i.state.copy(revision=i.state.revision+1,trialConsumed=true,grants=i.state.grants+grant)
                tx.write("$r/entitlementGrants/$grantId",MatchCodec.map(grant))
                tx.write("$r/promotions/$grantId",mapOf("trialConsumed" to true,"grantId" to grantId,"trialGrantedAt" to grant.validFrom.toString(),"trialEndsAt" to grant.validUntil.toString()))
                tx.write("$r/entitlementAudit/TRIAL_GRANTED",mapOf("type" to "TRIAL_GRANTED","at" to stamp(now),"grantId" to grantId,"operationId" to request.operationId,"policyVersion" to i.policy.policyVersion))
            }
            val result=TrialActivationResponse(request.operationId,if(already)TrialActivationOutcome.ALREADY_ACTIVE else TrialActivationOutcome.ACTIVATED,
                EntitlementSummary("AVAILABLE",EntitlementResolver.resolve(next,i.policy,now),!already))
            if(next!=i.state)tx.write("$r/entitlementState/current",MatchCodec.map(next))
            tx.write(receiptPath,mapOf("operationType" to "TRIAL_ACTIVATE","canonicalRequestHash" to hash,"eligibilityGroupKey" to "INITIAL_PREMIUM",
                "grantId" to grantId,"outcome" to result.outcome.name,"responseSnapshot" to GameCatalogCodec.map(result),"responseSchemaVersion" to 1,
                "createdAt" to stamp(now),"expiresAt" to stamp(now.plusSeconds(30L*86400))))
            result
        }
    }
    private fun stamp(now:Instant)=com.google.cloud.Timestamp.ofTimeSecondsAndNanos(now.epochSecond,now.nano)
}
