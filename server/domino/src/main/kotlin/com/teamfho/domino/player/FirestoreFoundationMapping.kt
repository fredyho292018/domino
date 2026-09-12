package com.teamfho.domino.player

import com.google.cloud.Timestamp
import com.google.cloud.firestore.FieldValue
import com.teamfho.domino.economy.Wallet
import java.time.Instant

internal object FirestoreFoundationMapping {
    fun player(data: Map<String, Any>, uid: String): Player = validate(FoundationError.PLAYER_STATE_CONFLICT) {
        require(data["uid"] == uid)
        Player(
            data["uid"] as String,
            PlayerAccountType.valueOf(data["accountType"] as String),
            data["displayName"] as String,
            data["language"] as String,
            PlayerStatus.valueOf(data["status"] as String),
            timestamp(data["createdAt"]), timestamp(data["updatedAt"]), timestamp(data["lastSeenAt"])
        )
    }

    fun wallet(data: Map<String, Any>): Wallet = validate(FoundationError.WALLET_STATE_INVALID) {
        Wallet(integer(data["coins"]), integer(data["lifetimeCoinsEarned"]), integer(data["lifetimeCoinsSpent"]),
            timestamp(data["createdAt"]), timestamp(data["updatedAt"]))
    }

    private fun integer(value: Any?): Long = when (value) {
        is Long -> value
        is Int -> value.toLong()
        is Short -> value.toLong()
        is Byte -> value.toLong()
        else -> throw IllegalArgumentException("Expected an integer")
    }

    private fun timestamp(value: Any?): FoundationTimestamp.Recorded {
        require(value is Timestamp)
        return FoundationTimestamp.Recorded(Instant.ofEpochSecond(value.seconds, value.nanos.toLong()))
    }

    private inline fun <T> validate(code: FoundationError, block: () -> T): T = try { block() }
    catch (_: RuntimeException) { throw PlayerFoundationException(code) }

    fun newPlayer(player: Player): Map<String, Any> = mapOf(
        "uid" to player.uid, "accountType" to player.accountType.name, "displayName" to player.displayName,
        "language" to player.language, "status" to player.status.name,
        "createdAt" to FieldValue.serverTimestamp(), "updatedAt" to FieldValue.serverTimestamp(),
        "lastSeenAt" to FieldValue.serverTimestamp()
    )

    fun newWallet(): Map<String, Any> = mapOf(
        "coins" to 0L, "lifetimeCoinsEarned" to 0L, "lifetimeCoinsSpent" to 0L,
        "createdAt" to FieldValue.serverTimestamp(), "updatedAt" to FieldValue.serverTimestamp()
    )
}
