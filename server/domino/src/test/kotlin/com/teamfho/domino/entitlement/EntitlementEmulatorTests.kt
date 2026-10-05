package com.teamfho.domino.entitlement

import com.google.cloud.firestore.*
import com.teamfho.domino.catalog.FirestoreMembershipCatalogRepository
import com.teamfho.domino.catalog.MembershipCatalogSeed
import com.teamfho.domino.catalog.MembershipBillingPeriod
import com.teamfho.domino.player.*
import com.teamfho.domino.security.FirebaseIdentity
import org.junit.jupiter.api.Tag
import org.junit.jupiter.api.Test
import java.util.UUID
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
    @Test fun concurrentActivation() = database().use { db ->
        val id=FirebaseIdentity("trial-fixture-"+UUID.randomUUID(),true);val clock=EntitlementClock()
        com.teamfho.domino.player.ensureEmulatorPlayer(db,id,clock)
        FirestoreMembershipCatalogRepository(db).publish(MembershipCatalogSeed.canonical())
        val service=TrialActivationService(FirestoreOnboardingProgressRepository(db),clock=clock)
        val pool=Executors.newFixedThreadPool(6)
        try {
            val results=pool.invokeAll((1..12).map{Callable{service.activate(id,TrialActivationRequest(UUID.randomUUID().toString(),1,"DIAMOND","YEARLY"))}}).map{it.get()}
            assertEquals(1,results.count{it.outcome==TrialActivationOutcome.ACTIVATED})
            assertEquals(11,results.count{it.outcome==TrialActivationOutcome.ALREADY_ACTIVE})
            assertEquals(1,db.collection("players/${id.uid}/entitlementGrants").get().get().size())
            val state=FirestoreEntitlements(db).read(id.uid)
            assertTrue(state.trialConsumed)
            val binding=assertNotNull(state.grants.single().planBoundTrial)
            assertEquals("DIAMOND",binding.trialPlan)
            assertEquals(MembershipBillingPeriod.YEARLY,binding.trialBillingPeriod)
            assertTrue(results.all{it.trial?.trialPlan==binding.trialPlan && it.trial?.trialEndsAt==binding.trialEndsAt})
            assertEquals(1,db.collection("players/${id.uid}/entitlementAudit").get().get().size())
            assertEquals(true,db.document("players/${id.uid}/promotions/initial-premium-trial").get().get().getBoolean("trialConsumed"))
            val expired=TrialActivationService(FirestoreOnboardingProgressRepository(db),clock=java.time.Clock.fixed(binding.trialEndsAt.plusSeconds(1),java.time.ZoneOffset.UTC))
            assertEquals("TRIAL_ALREADY_CONSUMED",assertFailsWith<OnboardingFailure>{
                expired.activate(id,TrialActivationRequest(UUID.randomUUID().toString(),1,"GOLD","MONTHLY"))
            }.code)
            assertEquals(state,FirestoreEntitlements(db).read(id.uid))
        } finally {pool.shutdownNow()}
    }
    @Test fun testAccountExcluded() = database().use { db ->
        val id=FirebaseIdentity("trial-fixture-"+UUID.randomUUID(),true)
        com.teamfho.domino.player.ensureEmulatorPlayer(db,id,java.time.Clock.systemUTC())
        db.document("developmentTestAccounts/${id.uid}").set(mapOf("isTestAccount" to true)).get()
        val service=TrialActivationService(FirestoreOnboardingProgressRepository(db))
        assertEquals("TRIAL_NOT_ELIGIBLE",assertFailsWith<OnboardingFailure>{service.activate(id,TrialActivationRequest(UUID.randomUUID().toString(),1))}.code)
        assertFalse(FirestoreEntitlements(db).read(id.uid).trialConsumed)
    }
    @Test fun bootstrapNoTrialWrites() = database().use { db ->
        val id=FirebaseIdentity("trial-fixture-"+UUID.randomUUID(),true)
        com.teamfho.domino.player.ensureEmulatorPlayer(db,id,java.time.Clock.systemUTC())
        val service=EntitlementService(SubscriptionPolicyService({SubscriptionPolicy()}),FirestoreEntitlements(db))
        repeat(2){assertFalse(service.bootstrap(id).trialGranted)}
        assertFalse(db.document("players/${id.uid}/entitlementState/current").get().get().exists())
        assertEquals(0,db.collection("players/${id.uid}/entitlementGrants").get().get().size())
    }
}

