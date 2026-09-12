package com.teamfho.domino.player

import com.google.api.gax.rpc.ApiException
import com.google.api.gax.rpc.StatusCode
import com.google.cloud.firestore.FieldValue
import com.google.cloud.firestore.Firestore
import com.google.cloud.firestore.TransactionOptions
import com.teamfho.domino.economy.Wallet
import com.teamfho.domino.security.FirebaseIdentity
import java.time.Clock
import java.time.Duration
import java.util.concurrent.ExecutionException
import java.util.concurrent.TimeUnit
import java.util.concurrent.TimeoutException

class FirestorePlayerFoundationRepository(private val firestore: Firestore, private val clock: Clock) : PlayerFoundationRepository {
    override fun updateDisplayName(identity: FirebaseIdentity, displayName: String): BootstrapResult {
        require(identity.uid.isNotBlank() && identity.uid.length <= 128 && !identity.uid.contains('/') && identity.uid !in setOf(".", ".."))
        DisplayNameRules.validate(displayName)
        val playerRef = firestore.document("players/${identity.uid}")
        val walletRef = firestore.document("players/${identity.uid}/wallet/main")
        try {
            return firestore.runTransaction({ tx ->
                val playerDoc = tx.get(playerRef).get()
                val walletDoc = tx.get(walletRef).get()
                if (!playerDoc.exists()) throw PlayerFoundationException(FoundationError.PLAYER_STATE_CONFLICT)
                if (!walletDoc.exists()) throw PlayerFoundationException(FoundationError.WALLET_STATE_INVALID)
                val player = FirestoreFoundationMapping.player(playerDoc.data ?: emptyMap(), identity.uid)
                val wallet = FirestoreFoundationMapping.wallet(walletDoc.data ?: emptyMap())
                if (player.displayName == displayName) BootstrapResult(player, wallet)
                else {
                    tx.update(playerRef, mapOf("displayName" to displayName, "updatedAt" to FieldValue.serverTimestamp()))
                    BootstrapResult(player.copy(displayName = displayName, updatedAt = FoundationTimestamp.ServerAssigned), wallet)
                }
            }, TransactionOptions.createReadWriteOptionsBuilder().setNumberOfAttempts(5).build()).get(30, TimeUnit.SECONDS)
        } catch (_: InterruptedException) {
            Thread.currentThread().interrupt()
            throw PlayerFoundationException(FoundationError.FIRESTORE_UNAVAILABLE)
        } catch (_: TimeoutException) { throw PlayerFoundationException(FoundationError.FIRESTORE_UNAVAILABLE)
        } catch (error: ExecutionException) { throw classify(error)
        } catch (error: ApiException) { throw classify(error) }
    }
    override fun ensure(identity: FirebaseIdentity, initialLanguage: String, candidateDisplayName: String): BootstrapResult {
        require(identity.uid.isNotBlank() && identity.uid.length <= 128 && !identity.uid.contains('/') &&
            identity.uid !in setOf(".", "..")) { "Invalid document identity" }
        require(initialLanguage in setOf("en", "es")) { "Unsupported initial language" }
        require(Regex("Guest-[A-Z0-9]{8}").matches(candidateDisplayName)) { "Invalid initial alias" }
        val now = clock.instant()
        val playerRef = firestore.document("players/${identity.uid}")
        val walletRef = firestore.document("players/${identity.uid}/wallet/main")
        try {
            return firestore.runTransaction({ tx ->
                // All reads and validation happen before any write is scheduled.
                val playerDoc = tx.get(playerRef).get()
                val walletDoc = tx.get(walletRef).get()
                val existingPlayer = if (playerDoc.exists()) FirestoreFoundationMapping.player(playerDoc.data ?: emptyMap(), identity.uid) else null
                val existingWallet = if (walletDoc.exists()) FirestoreFoundationMapping.wallet(walletDoc.data ?: emptyMap()) else null
                val serverTime = FoundationTimestamp.ServerAssigned
                val initialType = if (identity.isAnonymous) PlayerAccountType.GUEST else PlayerAccountType.REGISTERED
                var player = existingPlayer ?: Player(identity.uid, initialType, candidateDisplayName, initialLanguage,
                    PlayerStatus.ACTIVE, serverTime, serverTime, serverTime)
                val wallet = existingWallet ?: Wallet(0, 0, 0, serverTime, serverTime)
                if (existingPlayer == null) {
                    tx.create(playerRef, FirestoreFoundationMapping.newPlayer(player))
                } else {
                    val changes = mutableMapOf<String, Any>()
                    if (existingPlayer.accountType == PlayerAccountType.GUEST && !identity.isAnonymous) {
                        player = player.copy(accountType = PlayerAccountType.REGISTERED, updatedAt = serverTime)
                        changes["accountType"] = PlayerAccountType.REGISTERED.name
                        changes["updatedAt"] = FieldValue.serverTimestamp()
                    }
                    val lastSeen = (existingPlayer.lastSeenAt as FoundationTimestamp.Recorded).instant
                    if (Duration.between(lastSeen, now) >= Duration.ofMinutes(15)) {
                        player = player.copy(lastSeenAt = serverTime)
                        changes["lastSeenAt"] = FieldValue.serverTimestamp()
                    }
                    if (changes.isNotEmpty()) tx.update(playerRef, changes)
                }
                if (existingWallet == null) tx.create(walletRef, FirestoreFoundationMapping.newWallet())
                BootstrapResult(player, wallet)
            }, TransactionOptions.createReadWriteOptionsBuilder().setNumberOfAttempts(5).build()).get(30, TimeUnit.SECONDS)
        } catch (error: InterruptedException) {
            Thread.currentThread().interrupt()
            throw PlayerFoundationException(FoundationError.FIRESTORE_UNAVAILABLE)
        } catch (_: TimeoutException) {
            // Commit outcome may be unknown; retrying ensure never resets an existing wallet.
            throw PlayerFoundationException(FoundationError.FIRESTORE_UNAVAILABLE)
        } catch (error: ExecutionException) {
            throw classify(error)
        } catch (error: ApiException) {
            throw classify(error)
        }
    }

    private fun classify(error: Throwable): PlayerFoundationException {
        val causes = generateSequence(error) { it.cause }.take(20).toList()
        causes.filterIsInstance<PlayerFoundationException>().firstOrNull()?.let { return it }
        val contention = causes.filterIsInstance<ApiException>().any { it.statusCode.code == StatusCode.Code.ABORTED }
        return PlayerFoundationException(if (contention) FoundationError.FIRESTORE_CONTENTION_EXHAUSTED else FoundationError.FIRESTORE_UNAVAILABLE)
    }
}
