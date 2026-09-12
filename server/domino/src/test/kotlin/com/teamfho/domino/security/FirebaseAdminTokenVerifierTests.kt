package com.teamfho.domino.security

import com.google.firebase.ErrorCode
import com.google.firebase.auth.AuthErrorCode
import com.google.firebase.auth.FirebaseAuth
import com.google.firebase.auth.FirebaseAuthException
import com.google.firebase.auth.FirebaseToken
import com.google.firebase.auth.UserInfo
import com.google.firebase.auth.UserRecord
import com.teamfho.domino.common.ApiErrorCode
import org.junit.jupiter.api.Test
import org.junit.jupiter.params.ParameterizedTest
import org.junit.jupiter.params.provider.CsvSource
import org.junit.jupiter.params.provider.ValueSource
import org.mockito.Mockito.*
import kotlin.test.*

class FirebaseAdminTokenVerifierTests {
    private val auth = mock(FirebaseAuth::class.java)
    private val verifier = FirebaseAdminTokenVerifier(auth)
    private fun user(vararg providers: String): UserRecord {
        val token = mock(FirebaseToken::class.java)
        `when`(token.uid).thenReturn("verified-uid")
        `when`(auth.verifyIdToken("test-token", true)).thenReturn(token)
        val user = mock(UserRecord::class.java)
        `when`(user.uid).thenReturn("verified-uid")
        val providerData = providers.map { provider ->
            mock(UserInfo::class.java).also { `when`(it.providerId).thenReturn(provider) }
        }.toTypedArray()
        `when`(user.providerData).thenReturn(providerData)
        `when`(auth.getUser("verified-uid")).thenReturn(user)
        return user
    }
    @Test fun `empty providers means anonymous and revocation is checked`() {
        user()
        assertEquals(FirebaseIdentity("verified-uid", true), verifier.verify("test-token"))
        verify(auth).verifyIdToken("test-token", true)
        verify(auth).getUser("verified-uid")
        verifyNoMoreInteractions(auth)
    }
    @ParameterizedTest
    @ValueSource(strings = ["password", "google.com", "apple.com"])
    fun `linked providers determine registered identity`(provider: String) {
        user("anonymous", provider)
        assertFalse(verifier.verify("test-token").isAnonymous)
    }
    @Test fun `anonymous provider is not a linked account`() {
        user("anonymous")
        assertTrue(verifier.verify("test-token").isAnonymous)
    }
    @ParameterizedTest
    @CsvSource("INVALID_ID_TOKEN,AUTH_TOKEN_INVALID", "EXPIRED_ID_TOKEN,AUTH_TOKEN_EXPIRED",
        "REVOKED_ID_TOKEN,AUTH_SESSION_INVALID", "USER_DISABLED,AUTH_SESSION_INVALID",
        "USER_NOT_FOUND,AUTH_SESSION_INVALID", "TENANT_ID_MISMATCH,AUTH_TOKEN_INVALID",
        "CERTIFICATE_FETCH_FAILED,DEPENDENCY_UNAVAILABLE")
    fun `SDK rejection categories are sanitized`(sdk: AuthErrorCode, expected: ApiErrorCode) {
        val error = FirebaseAuthException(ErrorCode.INVALID_ARGUMENT, "internal test-token", null, null, sdk)
        `when`(auth.verifyIdToken("test-token", true)).thenThrow(error)
        val failure = assertFailsWith<AuthFailure> { verifier.verify("test-token") }
        assertEquals(expected, failure.code)
        assertNull(failure.cause)
        assertFalse(failure.toString().contains("test-token"))
        verify(auth, never()).getUser(anyString())
    }
    @ParameterizedTest
    @CsvSource("UNAVAILABLE,DEPENDENCY_UNAVAILABLE", "DEADLINE_EXCEEDED,DEPENDENCY_UNAVAILABLE",
        "RESOURCE_EXHAUSTED,DEPENDENCY_UNAVAILABLE", "PERMISSION_DENIED,INTERNAL_ERROR", "INTERNAL,INTERNAL_ERROR")
    fun `infrastructure failures are not invalid credentials`(sdk: ErrorCode, expected: ApiErrorCode) {
        user()
        `when`(auth.getUser("verified-uid")).thenThrow(FirebaseAuthException(sdk, "internal", null, null, null))
        assertEquals(expected, assertFailsWith<AuthFailure> { verifier.verify("test-token") }.code)
    }
    @Test fun `disabled user from current lookup is rejected`() {
        `when`(user().isDisabled).thenReturn(true)
        assertEquals(ApiErrorCode.AUTH_SESSION_INVALID, assertFailsWith<AuthFailure> { verifier.verify("test-token") }.code)
    }
    @Test fun `mismatched user record is not trusted`() {
        `when`(user().uid).thenReturn("different-uid")
        assertEquals(ApiErrorCode.INTERNAL_ERROR, assertFailsWith<AuthFailure> { verifier.verify("test-token") }.code)
    }
    @Test fun `blank token is rejected without calling SDK`() {
        assertEquals(ApiErrorCode.AUTH_TOKEN_INVALID, assertFailsWith<AuthFailure> { verifier.verify(" ") }.code)
        verifyNoInteractions(auth)
    }
    @Test fun `unexpected errors do not expose internal messages`() {
        `when`(auth.verifyIdToken("test-token", true)).thenThrow(IllegalStateException("sensitive test-token"))
        val failure = assertFailsWith<AuthFailure> { verifier.verify("test-token") }
        assertEquals(ApiErrorCode.INTERNAL_ERROR, failure.code)
        assertNull(failure.cause)
    }
}
