package com.teamfho.domino.security

import com.teamfho.domino.common.ApiErrorCode
import org.springframework.boot.test.context.TestConfiguration
import org.springframework.context.annotation.Bean
import java.util.concurrent.atomic.AtomicInteger

class FakeFirebaseTokenVerifier : FirebaseTokenVerifier {
    val calls = AtomicInteger()
    override fun verify(token: String): FirebaseIdentity {
        calls.incrementAndGet()
        return when (token) {
            "valid-guest" -> FirebaseIdentity("verified-guest", true)
            "valid-registered" -> FirebaseIdentity("verified-registered", false)
            "expired" -> throw AuthFailure(ApiErrorCode.AUTH_TOKEN_EXPIRED)
            "revoked", "disabled", "deleted" -> throw AuthFailure(ApiErrorCode.AUTH_SESSION_INVALID)
            "unavailable" -> throw AuthFailure(ApiErrorCode.DEPENDENCY_UNAVAILABLE)
            "unexpected" -> throw IllegalStateException("Sensitive internal detail")
            else -> throw AuthFailure(ApiErrorCode.AUTH_TOKEN_INVALID)
        }
    }
}

@TestConfiguration(proxyBeanMethods = false)
class FakeAuthConfiguration {
    @Bean
    fun firebaseTokenVerifier() = FakeFirebaseTokenVerifier()
}
