package com.teamfho.domino.economy

import com.teamfho.domino.player.FoundationTimestamp
import com.teamfho.domino.player.FoundationError
import com.teamfho.domino.player.PlayerFoundationException

data class Wallet(
    val coins: Long,
    val lifetimeCoinsEarned: Long,
    val lifetimeCoinsSpent: Long,
    val createdAt: FoundationTimestamp,
    val updatedAt: FoundationTimestamp
) {
    init {
        if (listOf(coins, lifetimeCoinsEarned, lifetimeCoinsSpent).any { it !in 0..MAX_COINS })
            throw PlayerFoundationException(FoundationError.WALLET_STATE_INVALID)
    }
    companion object { const val MAX_COINS: Long = 9_007_199_254_740_991L }
}
