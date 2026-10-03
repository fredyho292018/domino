package com.teamfho.domino.player

import com.fasterxml.jackson.annotation.JsonAnySetter
import com.teamfho.domino.security.FirebaseIdentity
import org.springframework.stereotype.Service
import java.util.Locale

// Keep the JSON type until validation, rather than coercing numbers/booleans into aliases.
data class PlayerDisplayNameRequest(val displayName: Any? = null) {
    @JsonAnySetter
    fun rejectUnknownField(name: String, value: Any?) { throw IllegalArgumentException("Unknown request field") }
}

class DisplayNameException(val reserved: Boolean = false) : RuntimeException("Display name rejected")

object DisplayNameRules {
    private val protectedNames = setOf("admin", "administrator", "moderator", "support", "teamfho", "system")
    fun validate(value: String?): String {
        val accepted = value?.let { java.text.Normalizer.normalize(it.trim(), java.text.Normalizer.Form.NFC) }
        if (accepted == null || !Regex("[A-Za-z0-9_-]{3,16}").matches(accepted)) throw DisplayNameException()
        if (accepted.lowercase(Locale.ROOT) in protectedNames) throw DisplayNameException(true)
        return accepted
    }
}

@Service
class PlayerDisplayNameService(private val repository: PlayerFoundationRepository) {
    fun update(identity: FirebaseIdentity, request: PlayerDisplayNameRequest): BootstrapResult =
        repository.updateDisplayName(identity, DisplayNameRules.validate(request.displayName as? String))
}
