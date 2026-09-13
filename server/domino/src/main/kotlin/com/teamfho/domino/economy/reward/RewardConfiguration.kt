package com.teamfho.domino.economy.reward

import com.google.cloud.firestore.Firestore
import org.springframework.beans.factory.ObjectProvider
import org.springframework.boot.context.properties.EnableConfigurationProperties
import org.springframework.context.annotation.Bean
import org.springframework.context.annotation.Configuration
import tools.jackson.databind.ObjectMapper
import java.time.Clock

@Configuration(proxyBeanMethods = false)
@EnableConfigurationProperties(RewardPolicy::class, MonetizationPolicy::class, MonetizationPolicyCacheSettings::class)
class RewardConfiguration {
    @Bean fun monetizationPolicyRepository(firestore: ObjectProvider<Firestore>): MonetizationPolicyRepository =
        firestore.ifAvailable?.let { FirestoreMonetizationPolicyRepository(it) }
            ?: MonetizationPolicyRepository { PolicyRead.Unavailable }
    @Bean fun monetizationPolicyService(repository: MonetizationPolicyRepository, fallback: MonetizationPolicy,
        cache: MonetizationPolicyCacheSettings) = MonetizationPolicyService(repository, fallback, Clock.systemUTC(), cache)
    @Bean fun rewardIntentRepository(firestore: ObjectProvider<Firestore>, policy: RewardPolicy, monetization: MonetizationPolicyService): RewardIntentRepository {
        // Firebase disabled (e.g. tests) must not introduce a verification bypass.
        val db = firestore.ifAvailable ?: return object : RewardIntentRepository {
            override fun consume(uid: String, intentId: String): RewardConsumeResponse = reject("SSV_DEPENDENCY_UNAVAILABLE", 503)
            override fun pending(uid: String): RewardIntent? = reject("SSV_DEPENDENCY_UNAVAILABLE", 503)
            override fun issue(uid: String): RewardIntent = reject("SSV_DEPENDENCY_UNAVAILABLE", 503)
            override fun status(uid: String, intentId: String): RewardIntent = reject("SSV_DEPENDENCY_UNAVAILABLE", 503)
            override fun verify(event: VerifiedAdMobEvent): Boolean = reject("SSV_DEPENDENCY_UNAVAILABLE", 503)
        }
        return FirestoreRewardIntentRepository(db, Clock.systemUTC(), policy, monetization)
    }
    @Bean fun adMobPublicKeyProvider(mapper: ObjectMapper, policy: RewardPolicy): AdMobPublicKeyProvider =
        CachedAdMobPublicKeyProvider(GoogleAdMobKeyFetcher(mapper), Clock.systemUTC(), policy.keyCacheTtl)
    @Bean fun adMobSsvVerifier(keys: AdMobPublicKeyProvider): AdMobSsvVerifier = GoogleAdMobSsvVerifier(keys)
}
