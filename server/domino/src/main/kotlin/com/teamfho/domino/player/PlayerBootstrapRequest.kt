package com.teamfho.domino.player

import com.fasterxml.jackson.annotation.JsonAnySetter

data class PlayerBootstrapRequest(val language: String? = null) {
    // Request-local strictness: never silently accept client-owned identity or economy fields.
    @JsonAnySetter
    fun rejectUnknownField(name: String, value: Any?) {
        throw IllegalArgumentException("Unknown request field")
    }
}
