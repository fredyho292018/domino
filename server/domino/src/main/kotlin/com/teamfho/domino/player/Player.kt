package com.teamfho.domino.player

enum class PlayerAccountType { GUEST, REGISTERED }
enum class PlayerStatus { ACTIVE }

data class Player(
    val uid: String,
    val accountType: PlayerAccountType,
    val displayName: String,
    val language: String,
    val status: PlayerStatus,
    val createdAt: FoundationTimestamp?,
    val updatedAt: FoundationTimestamp,
    val lastSeenAt: FoundationTimestamp,
    val firstName: String? = null,
    val lastName: String? = null,
    val countryCode: String? = null,
    val profileRevision: Long = 0
) {
    init {
        if (uid.isBlank() || displayName.isBlank() || language !in setOf("en", "es"))
            throw PlayerFoundationException(FoundationError.PLAYER_STATE_CONFLICT)
        require(profileRevision >= 0)
        require(firstName == PlayerProfileRules.name(firstName))
        require(lastName == PlayerProfileRules.name(lastName))
        require(countryCode == PlayerProfileRules.country(countryCode))
    }
}
