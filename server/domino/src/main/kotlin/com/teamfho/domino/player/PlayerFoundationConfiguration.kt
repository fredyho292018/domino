package com.teamfho.domino.player

import com.google.cloud.firestore.Firestore
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty
import org.springframework.context.annotation.Bean
import org.springframework.context.annotation.Configuration
import java.time.Clock
import java.time.Instant
import org.springframework.beans.factory.annotation.Value

@Configuration(proxyBeanMethods = false)
@ConditionalOnProperty(prefix = "firebase", name = ["enabled"], havingValue = "true", matchIfMissing = true)
class PlayerFoundationConfiguration {
    @Bean
    fun playerFoundationRepository(firestore: Firestore,
        @Value("\${domino.onboarding.created-at-cutoff:}") cutoff: String): PlayerFoundationRepository =
        FirestorePlayerFoundationRepository(firestore, Clock.systemUTC(),
            cutoff.takeIf { it.isNotBlank() }?.let { OnboardingRolloutBoundary(Instant.parse(it)) })
}
