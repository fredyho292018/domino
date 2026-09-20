package com.teamfho.domino.entitlement

import com.google.cloud.firestore.*
import com.teamfho.domino.match.MatchCodec
import com.teamfho.domino.player.FirestorePlayerFoundationRepository
import com.teamfho.domino.security.FirebaseIdentity
import com.teamfho.domino.online.SwarmEmulatorBackend
import org.junit.jupiter.api.Tag
import org.junit.jupiter.api.Test
import java.util.concurrent.Executors
import java.util.concurrent.Callable
import kotlin.test.*

@Tag("EMULATOR")
class EntitlementEmulatorTests {
    fun database():Firestore {
        check(System.getenv("FIRESTORE_EMULATOR_HOST")=="127.0.0.1:18085")
        return FirestoreOptions.newBuilder().setProjectId("demo-domino-f0").setEmulatorHost("127.0.0.1:18085")
            .setCredentials(FirestoreOptions.EmulatorCredentials()).build().service
    }
    @Test fun `atomic concurrent trial retries preserve immutable marker and grant`() = database().use {db->
        val uid="p01-concurrent-${java.util.UUID.randomUUID()}";val clock=EntitlementClock()
        val players=FirestorePlayerFoundationRepository(db,clock);val repo=FirestoreEntitlements(db)
        players.ensure(FirebaseIdentity(uid,true),"en","Guest-ABCDEFGH")
        val pool=Executors.newFixedThreadPool(6)
        try {
            val results=pool.invokeAll((1..12).map{Callable{repo.trial(uid,clock.now,SubscriptionPolicy())}}).map{it.get()}
            assertEquals(1,results.count{it.second});assertEquals(1,db.collection("players/$uid/entitlementGrants").get().get().size())
            val first=repo.read(uid);clock.now=clock.now.plusSeconds(9*86400)
            assertFalse(repo.trial(uid,clock.now,SubscriptionPolicy(promotionalTrialDays=14)).second)
            assertEquals(first,repo.read(uid));assertEquals(Plan.FREE,EntitlementResolver.resolve(first,SubscriptionPolicy(),clock.now).plan)
            assertTrue(db.document("players/$uid/promotions/initial-premium-trial").get().get().getBoolean("trialConsumed")!!)
            // Rebuild a missing projection from retained evidence; never issue a new trial.
            db.document("players/$uid/entitlementState/current").delete().get()
            assertFalse(repo.trial(uid,clock.now,SubscriptionPolicy()).second)
            assertEquals(first.grants,repo.read(uid).grants)
        }finally{pool.shutdownNow()}
    }
    @Test fun `test accounts excluded and transient bootstrap grant failure recoverable`() = database().use {db->
        val uid="p01-test-${java.util.UUID.randomUUID()}";val clock=EntitlementClock()
        val players=FirestorePlayerFoundationRepository(db,clock);val repo=FirestoreEntitlements(db)
        players.ensure(FirebaseIdentity(uid,true),"es","Guest-ABCDEFGH")
        db.document("developmentTestAccounts/$uid").set(mapOf("isTestAccount" to true)).get()
        assertFalse(repo.trial(uid,clock.now,SubscriptionPolicy()).second)
        assertFalse(repo.read(uid).trialConsumed)
        val normal="$uid-normal";players.ensure(FirebaseIdentity(normal,true),"en","Guest-ABCDEFGH")
        // Player exists before entitlement service recovers; lazy ensure still grants once.
        assertTrue(repo.trial(normal,clock.now,SubscriptionPolicy()).second)
        assertFalse(repo.trial(normal,clock.now,SubscriptionPolicy()).second)
        val g=EntitlementGrant("admin",EntitlementSource.ADMIN_GRANT,validFrom=clock.now,validUntil=clock.now.plusSeconds(30*86400),createdAt=clock.now,
            policyVersion=1,reason="CONTROLLED_TEST",grantedBy="test")
        repo.adminGrant(normal,g);repo.adminGrant(normal,g)
        assertEquals(2,repo.read(normal).grants.size)
        assertEquals(g.validUntil,EntitlementResolver.resolve(repo.read(normal),SubscriptionPolicy(),clock.now).validUntil)
    }
    @Test fun `Firestore policy refresh and exact measured bootstrap document cost`() = database().use {raw->
        val clock=EntitlementClock();val db=SwarmEmulatorBackend.measured(raw) as Firestore
        val path="systemConfig/subscriptionPolicy-p01-test"
        raw.document(path).set(MatchCodec.map(SubscriptionPolicy())).get()
        var reads=0
        val policies=SubscriptionPolicyService({reads++;MatchCodec.read(db.document(path).get().get().data!!,SubscriptionPolicy::class.java)},clock=clock)
        val repo=FirestoreEntitlements(db);val service=EntitlementService(policies,repo,clock)
        val uid="p01-cost-${java.util.UUID.randomUUID()}";val identity=FirebaseIdentity(uid,true)
        val players=FirestorePlayerFoundationRepository(db,clock)
        fun readsNow()=SwarmEmulatorBackend.counts["reads"]?.get()?:0L
        SwarmEmulatorBackend.counts.clear()
        players.ensure(identity,"en","Guest-ABCDEFGH");assertEquals(2,readsNow())
        val before=readsNow();assertTrue(service.bootstrap(identity).trialGranted)
        val extra=readsNow()-before;assertEquals(7,extra)
        val repeatBefore=readsNow();players.ensure(identity,"en","Guest-ABCDEFGH");assertFalse(service.bootstrap(identity).trialGranted)
        assertEquals(2,readsNow()-repeatBefore)
        raw.document(path).set(MatchCodec.map(SubscriptionPolicy(policyVersion=2,freeHistoryMax=5,freeReplayMax=1,promotionalTrialEnabled=false))).get()
        clock.now=clock.now.plusSeconds(61)
        val firstUntil=repo.read(uid).grants.single().validUntil
        assertEquals(5,service.limit("free",EntitlementLimit.HISTORY_MAX).maximum)
        assertEquals(1,service.limit("free",EntitlementLimit.REPLAY_MAX).maximum)
        assertEquals(firstUntil,service.resolve(uid).validUntil);assertFalse(service.bootstrap(FirebaseIdentity("uncreated",true)).trialGranted)
        assertEquals(2,reads)
        println("P01_COST BOOTSTRAP_READS_BEFORE=2 BOOTSTRAP_READS_AFTER_FIRST_GRANT=9 ENTITLEMENT_READS_FIRST=7 BOOTSTRAP_WARM=2 ENTITLEMENT_READS_WARM=0 POLICY_TTL=60 EXPIRATION_POLLING=0")
    }
}
