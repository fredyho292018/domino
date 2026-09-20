package com.teamfho.domino.entitlement

import com.teamfho.domino.security.FirebaseIdentity
import org.junit.jupiter.api.Test
import java.time.*
import kotlin.test.*

class EntitlementClock(var now:Instant=Instant.parse("2026-09-20T00:00:00Z")):Clock() {
    override fun instant()=now
    override fun getZone()=ZoneOffset.UTC
    override fun withZone(zone:ZoneId)=this
}
class MemoryEntitlements:EntitlementRepository {
    val data=mutableMapOf<String,EntitlementState>();val tests=mutableSetOf<String>();var reads=0;var unavailable=false
    @Synchronized override fun read(uid:String):EntitlementState {reads++;check(!unavailable);return data[uid]?:EntitlementState()}
    @Synchronized override fun trial(uid:String,now:Instant,policy:SubscriptionPolicy):Pair<EntitlementState,Boolean> {
        val s=read(uid)
        if(s.trialConsumed||uid in tests)return s to false
        val g=EntitlementGrant("initial-premium-trial",EntitlementSource.PROMOTIONAL_TRIAL,validFrom=now,
            validUntil=now.plusSeconds(policy.promotionalTrialDays*86400L),createdAt=now,policyVersion=policy.policyVersion,reason="WELCOME",grantedBy="bootstrap")
        return s.copy(revision=s.revision+1,trialConsumed=true,grants=s.grants+g).also{data[uid]=it} to true
    }
    @Synchronized override fun adminGrant(uid:String,grant:EntitlementGrant)=read(uid).let{s->s.copy(revision=s.revision+1,grants=s.grants.filter{it.id!=grant.id}+grant).also{data[uid]=it}}
}
class EntitlementTests {
    val clock=EntitlementClock();val repo=MemoryEntitlements();var policy=SubscriptionPolicy()
    val policies=SubscriptionPolicyService({policy},clock=clock)
    val service=EntitlementService(policies,repo,clock)
    fun bootstrap(uid:String="normal")=service.bootstrap(FirebaseIdentity(uid,true))
    fun admin(days:Long=30)=EntitlementGrant("admin",EntitlementSource.ADMIN_GRANT,validFrom=clock.now,
        validUntil=clock.now.plusSeconds(days*86400),createdAt=clock.now,policyVersion=1,reason="TEST",grantedBy="test-admin")
    @Test fun `new and existing anonymous accounts receive lazy trial once across fresh client state`() {
        for(uid in listOf("new","existing")) {
            val first=bootstrap(uid);assertTrue(first.trialGranted);assertTrue(first.snapshot!!.trialActive)
            repeat(8){assertFalse(bootstrap(uid).trialGranted)}
            val fresh=EntitlementService(policies,repo,clock).bootstrap(FirebaseIdentity(uid,true))
            assertEquals(first.snapshot!!.validUntil,fresh.snapshot!!.validUntil);assertEquals(1,repo.data[uid]!!.grants.size)
        }
    }
    @Test fun `day six twenty three hours premium exact day seven free without reads or destructive writes`() {
        val initial=bootstrap().snapshot!!;val saved=repo.data["normal"]
        clock.now=clock.now.plusSeconds(7*86400L-3600);assertEquals(Plan.PREMIUM,service.resolve("normal").plan)
        clock.now=clock.now.plusSeconds(3600);assertEquals(Plan.FREE,service.resolve("normal").plan)
        assertEquals(saved,repo.data["normal"]);assertEquals(clock.now,initial.validUntil)
        assertFalse(bootstrap().trialGranted)
    }
    @Test fun `test account excluded and kill switch does not revoke issued grant`() {
        repo.tests.add("swarm");assertFalse(bootstrap("swarm").snapshot!!.trialActive)
        bootstrap();policy=policy.copy(policyVersion=2,promotionalTrialEnabled=false);policies.invalidate()
        assertFalse(bootstrap("another").trialGranted);assertEquals(Plan.PREMIUM,service.resolve("normal").plan)
    }
    @Test fun `policy version limits update without extending issued trial`() {
        val first=bootstrap().snapshot!!
        policy=policy.copy(policyVersion=2,promotionalTrialDays=14,freeHistoryMax=5,freeReplayMax=1)
        clock.now=clock.now.plusSeconds(61)
        assertEquals(first.validUntil,service.resolve("normal").validUntil)
        assertEquals(5,service.limit("free",EntitlementLimit.HISTORY_MAX).maximum)
        assertEquals(1,service.limit("free",EntitlementLimit.REPLAY_MAX).maximum)
    }
    @Test fun `admin restoration and multiple sources retain longer coverage`() {
        val trial=bootstrap().snapshot!!;val grant=admin()
        service.adminGrant("normal",grant)
        assertEquals(grant.validUntil,service.resolve("normal").validUntil)
        clock.now=trial.validUntil!!;assertEquals(setOf(EntitlementSource.ADMIN_GRANT),service.resolve("normal").sources)
        clock.now=grant.validUntil;assertEquals(Plan.FREE,service.resolve("normal").plan)
        service.adminGrant("normal",admin().copy(id="restored"));assertEquals(Plan.PREMIUM,service.resolve("normal").plan)
        assertTrue(repo.data["normal"]!!.trialConsumed)
    }
    @Test fun `future grants cannot bridge a gap and revoked grant cannot authorize`() {
        val g=admin(1);val future=g.copy(id="future",validFrom=g.validUntil.plusSeconds(10),validUntil=g.validUntil.plusSeconds(100))
        val s=EntitlementState(grants=listOf(g,future))
        assertEquals(g.validUntil,EntitlementResolver.resolve(s,policy,clock.now).validUntil)
        assertEquals(Plan.FREE,EntitlementResolver.resolve(s,policy,g.validUntil).plan)
        assertEquals(Plan.FREE,EntitlementResolver.resolve(s.copy(grants=listOf(g.copy(status=GrantStatus.REVOKED))),policy,clock.now).plan)
    }
    @Test fun `approved feature matrix limits and typed denials`() {
        val free=service.resolve("free");val premium=bootstrap().snapshot!!
        assertEquals(SubscriptionPolicy.FREE_FEATURES,free.features)
        assertEquals(EntitlementFeature.entries.toSet(),premium.features)
        assertEquals(5,free.limits[EntitlementLimit.FRIENDS_MAX]!!.maximum)
        assertEquals(100,premium.limits[EntitlementLimit.FRIENDS_MAX]!!.maximum)
        assertEquals(10,free.limits[EntitlementLimit.HISTORY_MAX]!!.maximum)
        assertEquals(3,free.limits[EntitlementLimit.REPLAY_MAX]!!.maximum)
        assertTrue(premium.limits[EntitlementLimit.HISTORY_MAX]!!.unlimited)
        for(feature in premium.features-free.features)assertEquals("FEATURE_NOT_ENTITLED",assertFailsWith<EntitlementFailure>{service.requireFeature("free",feature)}.code)
        assertEquals("LIMIT_REACHED",assertFailsWith<EntitlementFailure>{service.requireCapacity("free",EntitlementLimit.FRIENDS_MAX,20)}.code)
        assertTrue(repo.data["free"]==null)
    }
    @Test fun `unavailable distinct from free public features remain independent`() {
        repo.unavailable=true
        assertEquals("UNAVAILABLE",bootstrap().availability)
        assertEquals("ENTITLEMENTS_UNAVAILABLE",assertFailsWith<EntitlementFailure>{service.requireFeature("free",EntitlementFeature.PARTY_CREATE)}.code)
        service.requireFeature("free",EntitlementFeature.PUBLIC_DUEL);service.requireFeature("free",EntitlementFeature.PUBLIC_PARTNERS)
    }
    @Test fun `policy outage and version rollback cannot fail open or grant trial`() {
        val failed=SubscriptionPolicyService({error("OFF")},clock=clock)
        assertFalse(failed.resolve().available);assertFalse(failed.resolve().policy.promotionalTrialEnabled)
        val first=policies.resolve();policy=policy.copy(freeHistoryMax=5);policies.invalidate()
        assertFalse(policies.resolve().available)
    }
    @Test fun `cache shares policy reads and resolves expiration using server time`() {
        var calls=0;val p=SubscriptionPolicyService({calls++;policy},clock=clock)
        repeat(20){p.resolve()};assertEquals(1,calls)
        clock.now=clock.now.plusSeconds(60);p.resolve();assertEquals(2,calls)
        val s=EntitlementService(p,repo,clock);repeat(20){s.resolve("none")};assertEquals(1,repo.reads)
    }
}
