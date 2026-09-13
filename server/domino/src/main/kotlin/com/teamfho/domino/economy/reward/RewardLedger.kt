package com.teamfho.domino.economy.reward

data class RewardCredit(val type: String = "COINS", val amount: Long)
data class RewardWallet(val coins: Long)
data class ConsumedIntent(val intentId: String, val status: String = "CONSUMED")
data class RewardConsumeResponse(val reward: RewardCredit, val wallet: RewardWallet, val intent: ConsumedIntent)

// Amount is frozen on the consumed intent and immutable ledger. Policy changes affect only
// future consumption; replay validates the frozen amount rather than rewriting history.
object RewardLedger {
    const val VERSION = 1L
    fun id(intentId: String) = "reward:$intentId"
}
