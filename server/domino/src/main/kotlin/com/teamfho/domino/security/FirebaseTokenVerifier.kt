package com.teamfho.domino.security

import com.teamfho.domino.common.ApiErrorCode

fun interface FirebaseTokenVerifier {
    fun verify(token: String): FirebaseIdentity
}

// Do not retain SDK exception messages/causes, which may contain request data.
class AuthFailure(val code: ApiErrorCode) : RuntimeException(code.name)
