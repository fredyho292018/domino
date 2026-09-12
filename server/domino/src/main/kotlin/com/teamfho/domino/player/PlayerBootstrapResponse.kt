package com.teamfho.domino.player

data class PlayerResponse(
    val uid: String,
    val accountType: PlayerAccountType,
    val displayName: String,
    val language: String,
    val status: PlayerStatus
)

data class WalletResponse(val coins: Long)

data class PlayerBootstrapResponse(val player: PlayerResponse, val wallet: WalletResponse) {
    companion object {
        fun from(result: BootstrapResult) = PlayerBootstrapResponse(
            result.player.let { PlayerResponse(it.uid, it.accountType, it.displayName, it.language, it.status) },
            WalletResponse(result.wallet.coins)
        )
    }
}
