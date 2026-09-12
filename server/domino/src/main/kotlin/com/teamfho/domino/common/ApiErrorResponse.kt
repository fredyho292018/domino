package com.teamfho.domino.common

enum class ApiErrorCode(val status: Int, val publicMessage: String) {
    REQUEST_INVALID(400, "Request body is invalid."),
    LANGUAGE_UNSUPPORTED(400, "Supported languages are en and es."),
    AUTH_TOKEN_MISSING(401, "Authentication is required."),
    AUTH_TOKEN_INVALID(401, "Authentication is required."),
    AUTH_TOKEN_EXPIRED(401, "Authentication is required."),
    AUTH_SESSION_INVALID(401, "Authentication is required."),
    ACCESS_DENIED(403, "Access is denied."),
    PLAYER_STATE_CONFLICT(409, "Player state is inconsistent."),
    WALLET_STATE_INVALID(409, "Wallet state is invalid."),
    DEPENDENCY_UNAVAILABLE(503, "Service is temporarily unavailable."),
    FIRESTORE_CONTENTION_EXHAUSTED(503, "Concurrent updates could not be completed. Please retry."),
    INTERNAL_ERROR(500, "An internal error occurred.")
}

data class ApiErrorResponse(val code: String, val message: String, val requestId: String)
