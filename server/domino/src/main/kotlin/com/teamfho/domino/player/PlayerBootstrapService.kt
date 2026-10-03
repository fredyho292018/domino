package com.teamfho.domino.player

import com.teamfho.domino.security.FirebaseIdentity
import org.springframework.stereotype.Service

class UnsupportedPlayerLanguageException : RuntimeException("LANGUAGE_UNSUPPORTED")

@Service
class PlayerBootstrapService(private val repository: PlayerFoundationRepository) {
    fun bootstrap(identity: FirebaseIdentity, request: PlayerBootstrapRequest?): BootstrapResult {
        val language = request?.language ?: "en"
        if (language !in setOf("en", "es")) throw UnsupportedPlayerLanguageException()
        repeat(5) {
            try { return repository.ensure(identity, language, GuestDisplayNames.generate()) }
            catch (e: PlayerFoundationException) { if (e.code != FoundationError.DISPLAY_NAME_TAKEN) throw e }
        }
        throw PlayerFoundationException(FoundationError.FIRESTORE_CONTENTION_EXHAUSTED)
    }
}
