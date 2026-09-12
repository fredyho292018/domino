package com.teamfho.domino.security

import com.google.firebase.ErrorCode
import com.google.firebase.auth.AuthErrorCode
import com.google.firebase.auth.FirebaseAuth
import com.google.firebase.auth.FirebaseAuthException
import com.teamfho.domino.common.ApiErrorCode
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty
import org.springframework.stereotype.Component

@Component
@ConditionalOnProperty(prefix = "firebase", name = ["enabled"], havingValue = "true", matchIfMissing = true)
class FirebaseAdminTokenVerifier(private val auth: FirebaseAuth) : FirebaseTokenVerifier {
    override fun verify(token: String): FirebaseIdentity {
        if (token.isBlank()) throw AuthFailure(ApiErrorCode.AUTH_TOKEN_INVALID)
        try {
            // The configured FirebaseApp supplies the project/audience validation.
            val decoded = auth.verifyIdToken(token, true)
            val uid = decoded.uid
            if (uid.isNullOrBlank()) throw AuthFailure(ApiErrorCode.AUTH_TOKEN_INVALID)
            val user = auth.getUser(uid)
            if (user.isDisabled) throw AuthFailure(ApiErrorCode.AUTH_SESSION_INVALID)
            if (user.uid != uid) throw AuthFailure(ApiErrorCode.INTERNAL_ERROR)
            val registered = user.providerData.any {
                !it.providerId.isNullOrBlank() && it.providerId != "anonymous"
            }
            return FirebaseIdentity(uid, !registered)
        } catch (failure: AuthFailure) {
            throw failure
        } catch (error: FirebaseAuthException) {
            throw AuthFailure(mapError(error))
        } catch (_: Exception) {
            throw AuthFailure(ApiErrorCode.INTERNAL_ERROR)
        }
    }

    private fun mapError(error: FirebaseAuthException): ApiErrorCode = when (error.authErrorCode) {
        AuthErrorCode.INVALID_ID_TOKEN, AuthErrorCode.TENANT_ID_MISMATCH -> ApiErrorCode.AUTH_TOKEN_INVALID
        AuthErrorCode.EXPIRED_ID_TOKEN -> ApiErrorCode.AUTH_TOKEN_EXPIRED
        AuthErrorCode.REVOKED_ID_TOKEN, AuthErrorCode.USER_DISABLED, AuthErrorCode.USER_NOT_FOUND -> ApiErrorCode.AUTH_SESSION_INVALID
        AuthErrorCode.CERTIFICATE_FETCH_FAILED -> ApiErrorCode.DEPENDENCY_UNAVAILABLE
        else -> when (error.errorCode) {
            ErrorCode.UNAVAILABLE, ErrorCode.DEADLINE_EXCEEDED, ErrorCode.RESOURCE_EXHAUSTED -> ApiErrorCode.DEPENDENCY_UNAVAILABLE
            // Generic IAM/configuration failures are not invalid player credentials.
            else -> ApiErrorCode.INTERNAL_ERROR
        }
    }
}
