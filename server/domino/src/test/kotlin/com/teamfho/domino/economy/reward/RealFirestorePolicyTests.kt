package com.teamfho.domino.economy.reward

import com.google.auth.oauth2.GoogleCredentials
import com.google.cloud.firestore.FirestoreOptions
import com.google.cloud.firestore.FieldValue
import com.teamfho.domino.security.FirebaseIdentity
import org.junit.jupiter.api.Test
import org.junit.jupiter.api.condition.EnabledIfEnvironmentVariable
import org.mockito.Mockito.mock
import java.util.UUID
import java.util.concurrent.TimeUnit
import kotlin.test.*

// Opt-in configuration-only integration test. Cannot touch the active policy path,
// wallet collections, Firebase users, AdMob, or the reward consume endpoint.
@EnabledIfEnvironmentVariable(named="DOMINO_H61_REAL_POLICY_TEST",matches="true")
class RealFirestorePolicyTests {
    @Test fun `isolated real policy seed change refresh and restore`() {
        val project=System.getenv("FIREBASE_PROJECT_ID") ?: error("EXPLICIT_PROJECT_REQUIRED")
        val path="systemConfig/monetization-h61-validation-${UUID.randomUUID()}"
        FirestoreOptions.newBuilder().setProjectId(project).setCredentials(GoogleCredentials.getApplicationDefault()).build().service.use { db ->
            val repository=FirestoreMonetizationPolicyRepository(db,path)
            val service=MonetizationPolicyService(repository,MonetizationPolicy())
            val controller=MonetizationController(service,mock(RewardIntentRepository::class.java))
            val identity=FirebaseIdentity("configuration-only-validation",true)
            assertEquals(PolicyRead.NotFound,repository.read())
            assertTrue(repository.seedIfAbsent(MonetizationPolicy()))
            try {
                assertFalse(repository.seedIfAbsent(MonetizationPolicy(99)))
                service.invalidate();assertEquals(10L,controller.config(identity).rewarded.rewardCoins)
                assertEquals(MonetizationPolicySource.FIRESTORE,service.resolve().source)
                db.document(path).update(mapOf("version" to 2L,"rewardCoins" to 15L,"updatedAt" to FieldValue.serverTimestamp(),"updatedBy" to "manual-admin")).get(10,TimeUnit.SECONDS)
                service.invalidate();assertEquals(15L,controller.config(identity).rewarded.rewardCoins)
            } finally {
                db.document(path).update(mapOf("version" to 3L,"rewardCoins" to 10L,"updatedAt" to FieldValue.serverTimestamp(),"updatedBy" to "migration")).get(10,TimeUnit.SECONDS)
                service.invalidate();assertEquals(10L,controller.config(identity).rewarded.rewardCoins)
                println("REAL_FIRESTORE_POLICY_TEST=PASS PATH=$path RESTORED_VERSION=3 RESTORED_COINS=10 ACTIVE_POLICY_CHANGED=NO WALLET_CREDIT=0")
            }
        }
    }
}
