package com.teamfho.domino.player

import com.teamfho.domino.security.FirebaseIdentity
import org.springframework.boot.test.context.TestConfiguration
import org.springframework.context.annotation.Bean
import java.time.Clock
import java.time.Instant
import java.time.ZoneOffset

class FakePlayerFoundationRepository : PlayerFoundationRepository {
    override fun updateDisplayName(identity: FirebaseIdentity, displayName: String): BootstrapResult {
        calls++
        failure?.let { throw it }
        return FirestorePlayerFoundationRepository(store.firestore, Clock.fixed(now, ZoneOffset.UTC)).updateDisplayName(identity, displayName)
    }
    private val now = Instant.parse("2026-09-11T12:00:00Z")
    internal var store = InMemoryFirestoreTransactions(now)
        private set
    var failure: RuntimeException? = null
    var calls = 0
    fun reset() { store = InMemoryFirestoreTransactions(now); failure = null; calls = 0 }
    override fun ensure(identity: FirebaseIdentity, initialLanguage: String, candidateDisplayName: String): BootstrapResult {
        calls++
        failure?.let { throw it }
        return FirestorePlayerFoundationRepository(store.firestore, Clock.fixed(now, ZoneOffset.UTC))
            .ensure(identity, initialLanguage, candidateDisplayName)
    }
}

@TestConfiguration(proxyBeanMethods = false)
class FakePlayerFoundationConfiguration {
    @Bean fun testPlayerFoundationRepository() = FakePlayerFoundationRepository()
}
