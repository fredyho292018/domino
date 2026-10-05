package com.teamfho.domino.entitlement

import com.teamfho.domino.catalog.*
import com.teamfho.domino.match.MatchCodec
import com.teamfho.domino.player.OnboardingFailure
import com.teamfho.domino.security.FirebaseIdentity
import java.time.*
import java.util.UUID
import org.junit.jupiter.api.Test
import kotlin.test.*

class PlanBoundTrialTests {
    private val now=Instant.parse("2026-10-04T12:00:00Z")
    private val id=FirebaseIdentity("fixture",true)
    private fun request(plan:String="DIAMOND",period:String="ANNUAL")=TrialActivationRequest(UUID.randomUUID().toString(),1,plan,period)
    private fun service(db:TrialMemory,at:Instant=now)=TrialActivationService(db,clock=Clock.fixed(at,ZoneOffset.UTC))
    @Test fun `commercial matrix preserved all plans and statuses explicit`() {
        val catalog=MembershipCatalogSeed.canonical()
        for(plan in listOf("GOLD","PLATINUM","DIAMOND","FRIENDS_AND_FAMILY")) {
            val benefits=catalog.planFeatures.filter{it.planKey==plan&&it.included}.map{CommercialCapabilityImplementation.describe(it.featureKey)}
            assertEquals(catalog.planFeatures.count{it.planKey==plan&&it.included},benefits.size)
            assertTrue(benefits.isNotEmpty());assertTrue(benefits.none{it.currentlyUsable})
        }
    }
    @Test fun `requested individual plan period combinations return authoritative UTC timeline`() {
        for((plan,period) in listOf("DIAMOND" to "MONTHLY","DIAMOND" to "ANNUAL","PLATINUM" to "MONTHLY","GOLD" to "ANNUAL")) {
            val db=TrialMemory();val result=service(db).activate(id,request(plan,period));val trial=result.trial!!
            assertEquals(plan,trial.trialPlan);assertEquals(if(period=="ANNUAL")MembershipBillingPeriod.YEARLY else MembershipBillingPeriod.MONTHLY,trial.trialBillingPeriod)
            assertEquals(now,trial.trialStartedAt);assertEquals(Instant.parse("2026-10-11T12:00:00Z"),trial.trialEndsAt)
            assertEquals(Instant.parse("2026-10-09T12:00:00Z"),trial.reminderAt);assertFalse(trial.legacy)
            assertEquals(Plan.FREE,result.entitlements.snapshot!!.membershipPlan)
            assertTrue(EntitlementFeature.ADVANCED_STATS !in result.entitlements.snapshot!!.features)
            val wanted=MembershipCatalogSeed.canonical().planFeatures.filter{it.planKey==plan&&it.included}.map{it.featureKey}.toSet()
            assertEquals(wanted,trial.commercialPlanBenefits.map{it.key}.toSet())
        }
    }
    @Test fun `configured duration and reminder are pinned and not clock localized`() {
        val db=TrialMemory();db.docs["systemConfig/subscriptionPolicy"]=MatchCodec.map(SubscriptionPolicy(promotionalTrialDays=12,trialReminderBeforeEndDays=3))
        val result=service(db).activate(id,request()).trial!!
        assertEquals(now.plusSeconds(12*86400),result.trialEndsAt);assertEquals(now.plusSeconds(9*86400),result.reminderAt)
    }
    @Test fun `missing binding family invalid period and invalid policy never mutate`() {
        for(req in listOf(request().copy(plan=null),request().copy(billingPeriod=null),request("FRIENDS_AND_FAMILY"),request("FREE"),request(period="WEEKLY"))) {
            val db=TrialMemory();val before=GameCatalogCodec.json(db.docs);assertFailsWith<OnboardingFailure>{service(db).activate(id,req)};assertEquals(before,GameCatalogCodec.json(db.docs))
        }
        val db=TrialMemory();db.docs["systemConfig/subscriptionPolicy"]=MatchCodec.map(SubscriptionPolicy(trialReminderBeforeEndDays=8))
        val before=GameCatalogCodec.json(db.docs);assertFailsWith<OnboardingFailure>{service(db).activate(id,request())};assertEquals(before,GameCatalogCodec.json(db.docs))
    }
    @Test fun `active binding immutable and cross plan expiry remains consumed`() {
        val db=TrialMemory();val s=service(db);val req=request();val result=s.activate(id,req)
        assertEquals(result,s.activate(id,req))
        val before=GameCatalogCodec.json(db.docs)
        for(r in listOf(request("GOLD"),request(period="MONTHLY")))assertEquals("TRIAL_BINDING_CONFLICT",assertFailsWith<OnboardingFailure>{s.activate(id,r)}.code)
        assertEquals(before,GameCatalogCodec.json(db.docs))
        assertEquals("TRIAL_ALREADY_CONSUMED",assertFailsWith<OnboardingFailure>{service(db,now.plusSeconds(8*86400)).activate(id,request("GOLD"))}.code)
    }
    @Test fun `legacy grant reads no plan no billing no fabricated reminder`() {
        val g=EntitlementGrant("initial-premium-trial",EntitlementSource.PROMOTIONAL_TRIAL,validFrom=now,validUntil=now.plusSeconds(86400),createdAt=now,policyVersion=1,reason="legacy",grantedBy="legacy")
        val decoded=MatchCodec.read(MatchCodec.map(g).filterKeys{it!="planBoundTrial"},EntitlementGrant::class.java)
        val state=EntitlementResolver.resolve(EntitlementState(trialConsumed=true,grants=listOf(decoded)),SubscriptionPolicy(),now)
        assertTrue(state.trial!!.legacy);assertNull(state.trial!!.trialPlan);assertNull(state.trial!!.trialBillingPeriod);assertNull(state.trial!!.reminderAt)
        assertEquals(SubscriptionPolicy().premiumFeatures,state.features)
    }
    @Test fun `gold gets target permissions not generic premium and expiry removes grant`() {
        val db=TrialMemory();service(db).activate(id,request("GOLD"))
        val state=MatchCodec.read(db.docs.getValue("players/fixture/entitlementState/current"),EntitlementState::class.java)
        val active=EntitlementResolver.resolve(state,SubscriptionPolicy(),now)
        assertTrue(EntitlementFeature.PARTY_CREATE in active.features);assertFalse(EntitlementFeature.FULL_REPLAY in active.features)
        val expired=EntitlementResolver.resolve(state,SubscriptionPolicy(),now.plusSeconds(8*86400))
        assertEquals(SubscriptionPolicy().freeFeatures,expired.features);assertEquals("EXPIRED",expired.trial!!.trialStatus)
        assertEquals("GOLD",expired.trial!!.trialPlan)
    }
    @Test fun `legacy receipt remains readable with original two field fingerprint`() {
        val db=TrialMemory();val req=TrialActivationRequest(UUID.randomUUID().toString(),1)
        val oldBody=mapOf("operationId" to req.operationId,"expectedPolicyVersion" to req.expectedPolicyVersion)
        val hash=java.security.MessageDigest.getInstance("SHA-256").digest(("TRIAL_ACTIVATE\n"+GameCatalogCodec.semantic(oldBody)).toByteArray()).joinToString(""){"%02x".format(it)}
        val response=TrialActivationResponse(req.operationId,TrialActivationOutcome.ALREADY_ACTIVE,EntitlementSummary("UNAVAILABLE"))
        db.docs["players/fixture/trialActivationReceipts/${req.operationId}"]=mapOf("canonicalRequestHash" to hash,"operationType" to "TRIAL_ACTIVATE",
            "expiresAt" to com.google.cloud.Timestamp.ofTimeSecondsAndNanos(now.plusSeconds(60).epochSecond,0),
            "responseSnapshot" to GameCatalogCodec.map(response).filterKeys{it!="trial"})
        val before=GameCatalogCodec.json(db.docs);assertEquals(response,service(db).activate(id,req));assertEquals(before,GameCatalogCodec.json(db.docs))
    }
    @Test fun `annual and yearly replay normalize without extra grant`() {
        val db=TrialMemory();val req=request();val first=service(db).activate(id,req)
        assertEquals(first,service(db).activate(id,req.copy(billingPeriod="YEARLY")))
        assertEquals(1,MatchCodec.read(db.docs.getValue("players/fixture/entitlementState/current"),EntitlementState::class.java).grants.size)
    }
}
