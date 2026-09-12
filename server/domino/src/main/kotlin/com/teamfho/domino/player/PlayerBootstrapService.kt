package com.teamfho.domino.player

import com.teamfho.domino.security.FirebaseIdentity
import org.springframework.stereotype.Service

class UnsupportedPlayerLanguageException : RuntimeException("LANGUAGE_UNSUPPORTED")

@Service
class PlayerBootstrapService(private val repository: PlayerFoundationRepository) {
    fun bootstrap(identity: FirebaseIdentity, request: PlayerBootstrapRequest?): BootstrapResult {
        val language = request?.language ?: "en"
        if (language !in setOf("en", "es")) throw UnsupportedPlayerLanguageException()
        val candidate = GuestDisplayNames.generate()
        return repository.ensure(identity, language, candidate)
    }
}
