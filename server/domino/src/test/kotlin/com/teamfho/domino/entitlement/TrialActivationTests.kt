package com.teamfho.domino.entitlement

import com.teamfho.domino.player.*
import com.teamfho.domino.catalog.GameCatalogCodec
import com.teamfho.domino.match.MatchCodec
import com.teamfho.domino.security.FirebaseIdentity
import org.junit.jupiter.api.Test
import java.util.UUID
import java.util.concurrent.Callable
import java.util.concurrent.Executors
import kotlin.test.*

class TrialMemory:OnboardingProgressRepository {
    val docs=linkedMapOf<String,Map<String,Any>>("players/fixture" to mapOf("status" to "ACTIVE","accountType" to "GUEST"))
    init { catalog() }
    fun catalog(){docs["systemConfig/membershipCatalog"]=mapOf("publishedVersion" to 1);docs["membershipCatalogs/1"]=GameCatalogCodec.map(com.teamfho.domino.catalog.MembershipCatalogSeed.canonical())}
    @Synchronized override fun <T> transaction(action:(OnboardingProgressTransaction)->T):T {
        val writes=linkedMapOf<String,Map<String,Any>>()
        val result=action(object:OnboardingProgressTransaction {
            override fun read(path:String):Map<String,Any>? {check(writes.isEmpty());return docs[path]}
            override fun write(path:String,value:Map<String,Any>){writes[path]=value}
        })
        docs.putAll(writes);return result
    }
}
class TrialActivationTests {
    val db=TrialMemory();val clock=EntitlementClock();val id=FirebaseIdentity("fixture",true)
    val service=TrialActivationService(db,clock=clock)
    fun request(v:Long=1)=TrialActivationRequest(UUID.randomUUID().toString(),v,"DIAMOND","YEARLY")
    fun state()=db.docs["players/fixture/entitlementState/current"]?.let{MatchCodec.read(it,EntitlementState::class.java)} ?: EntitlementState()
    fun policy(p:SubscriptionPolicy){db.docs["systemConfig/subscriptionPolicy"]=MatchCodec.map(p)}
    fun error(code:String,action:()->Unit){val before=GameCatalogCodec.json(db.docs);assertEquals(code,assertFailsWith<OnboardingFailure>{action()}.code);assertEquals(before,GameCatalogCodec.json(db.docs))}
    @Test fun `activation grants legacy premium using server policy time`() {
        val r=service.activate(id,request());assertEquals(TrialActivationOutcome.ACTIVATED,r.outcome);assertTrue(r.entitlements.trialGranted)
        assertEquals(Plan.PREMIUM,r.entitlements.snapshot!!.plan);assertTrue(state().trialConsumed);assertEquals(1,state().revision)
        val g=state().grants.single();assertEquals(clock.now,g.validFrom);assertEquals(clock.now.plusSeconds(7*86400),g.validUntil)
        assertEquals("explicit-activation",g.grantedBy)
    }
    @Test fun `lost response retry exact even after policy and time change`() {
        val req=request();val first=service.activate(id,req);val before=state()
        clock.now=clock.now.plusSeconds(10*86400);policy(SubscriptionPolicy(policyVersion=2,promotionalTrialDays=14))
        assertEquals(first,service.activate(id,req));assertEquals(before,state())
    }
    @Test fun `same operation different request conflicts`() {val req=request();service.activate(id,req);error("IDEMPOTENCY_CONFLICT"){service.activate(id,req.copy(expectedPolicyVersion=2))}}
    @Test fun `new operation active returns already active no revision or extension`() {
        service.activate(id,request());val first=state();clock.now=clock.now.plusSeconds(60)
        val result=service.activate(id,request());assertEquals(TrialActivationOutcome.ALREADY_ACTIVE,result.outcome);assertFalse(result.entitlements.trialGranted);assertEquals(first,state())
    }
    @Test fun `consumed expired refuses new operation`() {service.activate(id,request());clock.now=clock.now.plusSeconds(7*86400);error("TRIAL_ALREADY_CONSUMED"){service.activate(id,request())}}
    @Test fun `consumed marker without grants never resets`() {db.docs["players/fixture/entitlementState/current"]=MatchCodec.map(EntitlementState(trialConsumed=true));error("TRIAL_ALREADY_CONSUMED"){service.activate(id,request())}}
    @Test fun `stale policy rejects without writes`() {policy(SubscriptionPolicy(policyVersion=2));error("TRIAL_POLICY_VERSION_MISMATCH"){service.activate(id,request())}}
    @Test fun `disabled rejects without writes`() {policy(SubscriptionPolicy(promotionalTrialEnabled=false));error("TRIAL_DISABLED"){service.activate(id,request())}}
    @Test fun `test account excluded`() {db.docs["developmentTestAccounts/fixture"]=mapOf("isTestAccount" to true);error("TRIAL_NOT_ELIGIBLE"){service.activate(id,request())}}
    @Test fun `inactive or absent player excluded`() {db.docs.remove("players/fixture");error("TRIAL_NOT_ELIGIBLE"){service.activate(id,request())};db.docs["players/fixture"]=mapOf("status" to "SUSPENDED");error("TRIAL_NOT_ELIGIBLE"){service.activate(id,request())}}
    @Test fun `linked policy excludes anonymous`() {policy(SubscriptionPolicy(trialRequiresLinkedAccount=true));error("TRIAL_NOT_ELIGIBLE"){service.activate(id,request())}}
    @Test fun `unverified password rejected`() {error("EMAIL_VERIFICATION_REQUIRED"){service.activate(FirebaseIdentity("fixture",false,false,"password",true),request())}}
    @Test fun `verified password allowed`() {assertEquals(TrialActivationOutcome.ACTIVATED,service.activate(FirebaseIdentity("fixture",false,true,"password",true),request()).outcome)}
    @Test fun `unsupported authentication rejected`() {error("AUTH_CONTEXT_UNSUPPORTED"){service.activate(FirebaseIdentity("fixture",false),request())}}
    @Test fun `paid store or admin coverage incompatible`() {
        for(source in listOf(EntitlementSource.ADMIN_GRANT,EntitlementSource.GOOGLE_PLAY,EntitlementSource.APPLE_APP_STORE)) {
            val g=EntitlementGrant("paid",source,validFrom=clock.now,validUntil=clock.now.plusSeconds(900),createdAt=clock.now,policyVersion=1,reason="fixture",grantedBy="fixture")
            db.docs["players/fixture/entitlementState/current"]=MatchCodec.map(EntitlementState(grants=listOf(g)))
            error("TRIAL_NOT_APPLICABLE"){service.activate(id,request())}
        }
    }
    @Test fun `policy duration new only existing dates unchanged`() {
        policy(SubscriptionPolicy(policyVersion=2,promotionalTrialDays=14));val first=service.activate(id,request(2));assertEquals(clock.now.plusSeconds(14*86400),first.entitlements.snapshot!!.trialEndsAt)
        policy(SubscriptionPolicy(policyVersion=3,promotionalTrialDays=7));assertEquals(first.entitlements.snapshot!!.trialEndsAt,service.activate(id,request(3)).entitlements.snapshot!!.trialEndsAt)
    }
    @Test fun `legacy projection loss restores same active grant without rewriting evidence`() {
        service.activate(id,request());val g=db.docs.getValue("players/fixture/entitlementGrants/initial-premium-trial");val marker=db.docs.getValue("players/fixture/promotions/initial-premium-trial")
        db.docs.remove("players/fixture/entitlementState/current")
        assertEquals(TrialActivationOutcome.ALREADY_ACTIVE,service.activate(id,request()).outcome)
        assertEquals(g,db.docs["players/fixture/entitlementGrants/initial-premium-trial"]);assertEquals(marker,db.docs["players/fixture/promotions/initial-premium-trial"]);assertTrue(state().trialConsumed)
    }
    @Test fun `partial historical evidence fails closed`() {db.docs["players/fixture/promotions/initial-premium-trial"]=mapOf("trialConsumed" to true,"grantId" to "initial-premium-trial");error("DEPENDENCY_UNAVAILABLE"){service.activate(id,request())}}
    @Test fun `conflicting historical dates fail closed`() {
        service.activate(id,request());val path="players/fixture/promotions/initial-premium-trial"
        db.docs[path]=db.docs.getValue(path)+("trialEndsAt" to clock.now.toString())
        error("DEPENDENCY_UNAVAILABLE"){service.activate(id,request())}
    }
    @Test fun `revoked trial remains consumed`() {
        service.activate(id,request());val s=state();val g=s.grants.single().copy(status=GrantStatus.REVOKED)
        db.docs["players/fixture/entitlementState/current"]=MatchCodec.map(s.copy(grants=listOf(g)))
        db.docs["players/fixture/entitlementGrants/initial-premium-trial"]=MatchCodec.map(g)
        error("TRIAL_ALREADY_CONSUMED"){service.activate(id,request())}
    }
    @Test fun `expired receipt never permits regrant`() {val req=request();service.activate(id,req);clock.now=clock.now.plusSeconds(31*86400);error("TRIAL_ALREADY_CONSUMED"){service.activate(id,req)}}
    @Test fun `receipt retention thirty days with native timestamp`() {val req=request();service.activate(id,req);val receipt=db.docs.getValue("players/fixture/trialActivationReceipts/${req.operationId}");assertEquals(clock.now.plusSeconds(30*86400).epochSecond,(receipt["expiresAt"] as com.google.cloud.Timestamp).seconds)}
    @Test fun `concurrent instances share persistent eligibility`() {
        val pool=Executors.newFixedThreadPool(8)
        try {val results=pool.invokeAll((1..16).map{Callable{TrialActivationService(db,clock=clock).activate(id,request())}}).map{it.get()}
            assertEquals(1,results.count{it.outcome==TrialActivationOutcome.ACTIVATED});assertEquals(1,state().grants.size);assertEquals(1,state().revision)
        } finally {pool.shutdownNow()}
    }
    @Test fun `invalid operation and policy input rejected`() {error("REQUEST_INVALID"){service.activate(id,TrialActivationRequest("invalid",1))};error("REQUEST_INVALID"){service.activate(id,request(0))}}
    @Test fun `eligibility is read only and derives state`() {
        val before=GameCatalogCodec.json(db.docs);assertEquals("NOT_STARTED",service.eligibility(id).state);assertEquals(before,GameCatalogCodec.json(db.docs))
        service.activate(id,request());assertEquals("ACTIVE",service.eligibility(id).state);clock.now=clock.now.plusSeconds(7*86400);assertEquals("EXPIRED",service.eligibility(id).state)
    }
    @Test fun `dependency failure unknown eligibility no optimistic grant`() {
        val fail=TrialActivationService(object:OnboardingProgressRepository{override fun <T> transaction(action:(OnboardingProgressTransaction)->T):T=throw OnboardingFailure("DEPENDENCY_UNAVAILABLE",503)})
        assertEquals("UNKNOWN",fail.eligibility(id).state);assertEquals(503,assertFailsWith<OnboardingFailure>{fail.activate(id,request())}.status)
    }
    @Test fun `bootstrap preserves active expired admin and free without writes`() {
        val repo=MemoryEntitlements();val e=EntitlementService(SubscriptionPolicyService({SubscriptionPolicy()}),repo,clock,service)
        repeat(2){assertEquals(Plan.FREE,e.bootstrap(id).snapshot!!.plan)};assertTrue(repo.data.isEmpty())
        service.activate(id,request());repo.data[id.uid]=state();e.invalidate(id.uid);val before=repo.data.toMap()
        assertTrue(e.bootstrap(id).snapshot!!.trialActive);clock.now=clock.now.plusSeconds(7*86400);assertFalse(e.bootstrap(id).snapshot!!.trialActive);assertEquals(before,repo.data)
        val admin=EntitlementGrant("admin",EntitlementSource.ADMIN_GRANT,validFrom=clock.now,validUntil=clock.now.plusSeconds(900),createdAt=clock.now,policyVersion=1,reason="fixture",grantedBy="fixture")
        e.adminGrant(id.uid,admin);val after=repo.data.toMap();assertEquals(Plan.PREMIUM,e.bootstrap(id).snapshot!!.plan);assertEquals(after,repo.data)
    }
    @Test fun `activation invalidates service cached free state`() {
        val repo=object:EntitlementRepository {
            override fun read(uid:String)=state()
            override fun adminGrant(uid:String,grant:EntitlementGrant):EntitlementState=error("unused")
        }
        val e=EntitlementService(SubscriptionPolicyService({SubscriptionPolicy()}),repo,clock,service)
        assertEquals(Plan.FREE,e.resolve(id.uid).plan);e.activateTrial(id,request());assertEquals(Plan.PREMIUM,e.resolve(id.uid).plan)
    }
}
