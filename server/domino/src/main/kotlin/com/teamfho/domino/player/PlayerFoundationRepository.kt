package com.teamfho.domino.player

import com.teamfho.domino.economy.Wallet
import com.teamfho.domino.security.FirebaseIdentity

data class BootstrapResult(val player: Player, val wallet: Wallet)

enum class FoundationError {
    PLAYER_STATE_CONFLICT, WALLET_STATE_INVALID, FIRESTORE_UNAVAILABLE, FIRESTORE_CONTENTION_EXHAUSTED
}

class PlayerFoundationException(val code: FoundationError) : RuntimeException(code.name)

interface PlayerFoundationRepository {
    fun updateDisplayName(identity: FirebaseIdentity, displayName: String): BootstrapResult
    fun ensure(identity: FirebaseIdentity, initialLanguage: String, candidateDisplayName: String): BootstrapResult
}
