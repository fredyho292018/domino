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
        if (value == null || !Regex("[A-Za-z0-9_-]{3,16}").matches(value)) throw DisplayNameException()
        if (value.lowercase(Locale.ROOT) in protectedNames) throw DisplayNameException(true)
        return value
    }
}

@Service
class PlayerDisplayNameService(private val repository: PlayerFoundationRepository) {
    fun update(identity: FirebaseIdentity, request: PlayerDisplayNameRequest): BootstrapResult =
        repository.updateDisplayName(identity, DisplayNameRules.validate(request.displayName as? String))
}
