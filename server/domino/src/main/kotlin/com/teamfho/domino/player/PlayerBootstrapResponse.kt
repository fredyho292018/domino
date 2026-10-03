package com.teamfho.domino.player

data class PlayerResponse(
    val uid: String,
    val accountType: PlayerAccountType,
    val displayName: String,
    val language: String,
    val status: PlayerStatus,
    // Only a recorded Firestore instant is presentation authority. Unresolved/new or legacy missing is null.
    val createdAt: String? = null
)

data class WalletResponse(val coins: Long)

data class PlayerBootstrapResponse(val player: PlayerResponse, val wallet: WalletResponse,
    val entitlements: com.teamfho.domino.entitlement.EntitlementSummary? = null,
    val trialEligibility:com.teamfho.domino.entitlement.TrialEligibilityResponse?=null,
    val capabilities:Map<String,String> = mapOf("trialActivationMode" to "EXPLICIT","trialActivationContractVersion" to "1")) {
    companion object {
        fun from(result: BootstrapResult) = PlayerBootstrapResponse(
            result.player.let { PlayerResponse(it.uid, it.accountType, it.displayName, it.language, it.status,
                (it.createdAt as? FoundationTimestamp.Recorded)?.instant?.toString()) },
            WalletResponse(result.wallet.coins)
        )
    }
}
