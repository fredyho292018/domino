package com.teamfho.domino.economy.reward

import com.google.cloud.Timestamp
import com.google.cloud.firestore.*
import java.security.MessageDigest
import java.time.Clock
import java.time.Instant
import java.util.UUID
import java.util.concurrent.TimeUnit
import com.teamfho.domino.economy.Wallet
import com.teamfho.domino.player.FirestoreFoundationMapping
import com.teamfho.domino.player.PlayerFoundationException

// Global opaque intent IDs permit SSV lookup without sending Firebase UID to Google.
// All mutations stay in reward collections; no Wallet repository is referenced.
class FirestoreRewardIntentRepository(private val firestore: Firestore, private val clock: Clock,
    private val policy: RewardPolicy) : RewardIntentRepository {
    private fun digest(value: String) = MessageDigest.getInstance("SHA-256").digest(value.toByteArray())
        .joinToString("") { "%02x".format(it) }
    private fun intentRef(id: String): DocumentReference {
        if (!opaqueId(id)) reject("SSV_INTENT_NOT_FOUND", 404)
        return firestore.document("rewardIntents/$id")
    }
    private fun <T> atomic(block: (Transaction) -> T): T {
        try {
            return firestore.runTransaction({ tx -> block(tx) },
                TransactionOptions.createReadWriteOptionsBuilder().setNumberOfAttempts(5).build()).get(30, TimeUnit.SECONDS)
        } catch (_: InterruptedException) { Thread.currentThread().interrupt(); reject("SSV_DEPENDENCY_UNAVAILABLE", 503) }
        catch (error: Exception) {
            generateSequence<Throwable>(error) { it.cause }.take(20).filterIsInstance<RewardFailure>().firstOrNull()?.let { throw it }
            reject("SSV_DEPENDENCY_UNAVAILABLE", 503)
        }
    }
    override fun issue(uid: String): RewardIntent {
        require(uid.isNotBlank() && uid.length <= 128)
        val slot = firestore.document("rewardIntentOwners/" + digest(uid))
        val candidateId = UUID.randomUUID().toString()
        return atomic { tx ->
            val now = clock.instant()
            val slotData = tx.get(slot).get().data
            val previousRef = (slotData?.get("intentId") as? String)?.let(::intentRef)
            val previous = previousRef?.let { tx.get(it).get().data }?.let(::decode)
            if (previous != null && previous.uid != uid) reject("SSV_INTENT_INVALID", 409)
            // Preserve restart discovery until the previous verified reward is consumed.
            if (previous?.status == RewardIntentStatus.VERIFIED) reject("REWARD_PENDING", 409)
            if (previous != null && previous.status == RewardIntentStatus.ISSUED && now.isBefore(previous.expiresAt) &&
                previous.adUnitEnvironment == policy.environment) previous
            else {
                val intent = RewardIntent(candidateId, uid, RewardIntentStatus.ISSUED, now, now.plus(policy.intentTtl), policy.environment)
                if (previous?.status == RewardIntentStatus.ISSUED) tx.update(previousRef!!, mapOf(
                    "status" to (if (now.isBefore(previous.expiresAt)) "REJECTED" else "EXPIRED"), "updatedAt" to FieldValue.serverTimestamp()))
                tx.create(intentRef(candidateId), mapOf(
                    "intentId" to candidateId, "uid" to uid, "status" to "ISSUED", "source" to intent.source,
                    "rewardPolicyKey" to intent.rewardPolicyKey, "adUnitEnvironment" to intent.adUnitEnvironment.name,
                    "createdAt" to stamp(now), "expiresAt" to stamp(intent.expiresAt), "updatedAt" to FieldValue.serverTimestamp()))
                tx.set(slot, mapOf("intentId" to candidateId))
                intent
            }
        }
    }
    override fun status(uid: String, intentId: String): RewardIntent = atomic { tx ->
        val intent = tx.get(intentRef(intentId)).get().data?.let(::decode) ?: reject("SSV_INTENT_NOT_FOUND", 404)
        if (intent.uid != uid) reject("SSV_INTENT_NOT_FOUND", 404)
        if (intent.status == RewardIntentStatus.ISSUED && !clock.instant().isBefore(intent.expiresAt))
            intent.copy(status = RewardIntentStatus.EXPIRED) else intent
    }
    override fun verify(event: VerifiedAdMobEvent): Boolean {
        val intentRef = intentRef(event.intentId)
        val transactionRef = firestore.document("adMobRewardTransactions/" + digest(event.transactionId))
        return atomic { tx ->
            val now = clock.instant()
            val existing = tx.get(transactionRef).get().data
            val intent = tx.get(intentRef).get().data?.let(::decode) ?: reject("SSV_INTENT_NOT_FOUND", 404)
            if (existing != null) {
                if (existing["intentId"] != event.intentId || existing["transactionId"] != event.transactionId ||
                    existing["adUnit"] != event.adUnit || existing["eventTimestamp"] != stamp(event.timestamp) ||
                    intent.status !in setOf(RewardIntentStatus.VERIFIED, RewardIntentStatus.CONSUMED) || intent.adMobTransactionId != event.transactionId)
                    reject("SSV_TRANSACTION_CONFLICT", 409)
                false // Already committed: acknowledge retries even after intent TTL/event window.
            } else {
                policy.validate(intent, event, now)
                tx.create(transactionRef, mapOf("transactionId" to event.transactionId, "intentId" to event.intentId,
                    "adUnit" to event.adUnit, "eventTimestamp" to stamp(event.timestamp), "verifiedAt" to FieldValue.serverTimestamp()))
                tx.update(intentRef, mapOf("status" to "VERIFIED", "adMobTransactionId" to event.transactionId,
                    "verifiedAt" to FieldValue.serverTimestamp(), "updatedAt" to FieldValue.serverTimestamp()))
                true
            }
        }
    }
    override fun pending(uid: String): RewardIntent? = atomic { tx ->
        val slot = tx.get(firestore.document("rewardIntentOwners/" + digest(uid))).get().data
        val id = slot?.get("intentId") as? String
        val intent = id?.let { tx.get(intentRef(it)).get().data }?.let(::decode)
        if (intent != null && intent.uid != uid) reject("SSV_INTENT_NOT_FOUND", 404)
        intent?.takeIf { it.status == RewardIntentStatus.VERIFIED }
    }
    override fun consume(uid: String, intentId: String): RewardConsumeResponse {
        // Firebase UID never comes from request JSON; validate path segments defensively.
        if (uid.isBlank() || uid.length > 128 || uid.contains('/') || uid in setOf(".", "..")) reject("REQUEST_INVALID")
        val ref = intentRef(intentId)
        val walletRef = firestore.document("players/$uid/wallet/main")
        val ledgerId = RewardLedger.id(intentId)
        val ledgerRef = firestore.document("players/$uid/walletTransactions/$ledgerId")
        return atomic { tx ->
            // Read every document before scheduling any writes.
            val data = tx.get(ref).get().data ?: reject("SSV_INTENT_NOT_FOUND", 404)
            val intent = decode(data)
            if (intent.uid != uid) reject("SSV_INTENT_NOT_FOUND", 404)
            if (intent.status !in setOf(RewardIntentStatus.VERIFIED, RewardIntentStatus.CONSUMED))
                reject("REWARD_NOT_VERIFIED", 409)
            if (intent.source != "REWARDED_AD" || intent.rewardPolicyKey != "REWARDED_AD_STANDARD" ||
                intent.verifiedAt == null || intent.adMobTransactionId.isNullOrBlank()) reject("REWARD_INTENT_INVALID", 409)
            val walletData = tx.get(walletRef).get().data ?: reject("WALLET_STATE_INVALID", 409)
            val wallet = try { FirestoreFoundationMapping.wallet(walletData) }
                catch (_: PlayerFoundationException) { reject("WALLET_STATE_INVALID", 409) }
            val ledger = tx.get(ledgerRef).get().data
            if (intent.status == RewardIntentStatus.CONSUMED) {
                val amount = data["consumedAmount"] as? Long ?: reject("LEDGER_CONFLICT", 409)
                if (amount !in 1..Wallet.MAX_COINS || data["ledgerTransactionId"] != ledgerId ||
                    data["consumedAt"] !is Timestamp || ledger == null) reject("LEDGER_CONFLICT", 409)
                val before = ledger["balanceBefore"] as? Long ?: reject("LEDGER_CONFLICT", 409)
                val after = ledger["balanceAfter"] as? Long ?: reject("LEDGER_CONFLICT", 409)
                if (before !in 0..Wallet.MAX_COINS || after !in 0..Wallet.MAX_COINS ||
                    before > Wallet.MAX_COINS - amount || before + amount != after ||
                    ledger["transactionId"] != ledgerId || ledger["uid"] != uid || ledger["type"] != "CREDIT" ||
                    ledger["source"] != intent.source || ledger["amount"] != amount ||
                    ledger["rewardIntentId"] != intentId || ledger["version"] != RewardLedger.VERSION ||
                    ledger["rewardPolicyKey"] != intent.rewardPolicyKey || ledger["createdAt"] !is Timestamp)
                    reject("LEDGER_CONFLICT", 409)
                RewardConsumeResponse(RewardCredit(amount = amount), RewardWallet(wallet.coins), ConsumedIntent(intentId))
            } else {
                if (ledger != null) reject("LEDGER_CONFLICT", 409)
                val amount = policy.rewardCoins
                val after = safeCredit(wallet.coins, amount)
                val earned = safeCredit(wallet.lifetimeCoinsEarned, amount)
                tx.create(ledgerRef, mapOf("transactionId" to ledgerId, "uid" to uid, "type" to "CREDIT",
                    "source" to intent.source, "amount" to amount, "balanceBefore" to wallet.coins, "balanceAfter" to after,
                    "rewardIntentId" to intentId, "rewardPolicyKey" to intent.rewardPolicyKey,
                    "createdAt" to FieldValue.serverTimestamp(), "version" to RewardLedger.VERSION))
                tx.update(walletRef, mapOf("coins" to after, "lifetimeCoinsEarned" to earned,
                    "updatedAt" to FieldValue.serverTimestamp()))
                tx.update(ref, mapOf("status" to "CONSUMED", "consumedAmount" to amount,
                    "ledgerTransactionId" to ledgerId, "consumedAt" to FieldValue.serverTimestamp(), "updatedAt" to FieldValue.serverTimestamp()))
                RewardConsumeResponse(RewardCredit(amount = amount), RewardWallet(after), ConsumedIntent(intentId))
            }
        }
    }
    private fun safeCredit(current: Long, amount: Long): Long {
        val result = try { Math.addExact(current, amount) } catch (_: ArithmeticException) { reject("WALLET_OVERFLOW", 409) }
        // Preserve the existing Player Foundation safe-integer cap, stricter than Long.MAX_VALUE.
        if (result > Wallet.MAX_COINS) reject("WALLET_OVERFLOW", 409)
        return result
    }
    private fun stamp(time: Instant) = Timestamp.ofTimeSecondsAndNanos(time.epochSecond, time.nano)
    private fun decode(data: Map<String, Any>): RewardIntent {
        fun text(key: String) = data[key] as? String ?: reject("SSV_INTENT_INVALID", 409)
        fun time(key: String): Instant {
            val value = data[key] as? Timestamp ?: reject("SSV_INTENT_INVALID", 409)
            return Instant.ofEpochSecond(value.seconds, value.nanos.toLong())
        }
        return RewardIntent(text("intentId"), text("uid"), RewardIntentStatus.valueOf(text("status")),
            time("createdAt"), time("expiresAt"), AdUnitEnvironment.valueOf(text("adUnitEnvironment")),
            if (data["verifiedAt"] != null) time("verifiedAt") else null, data["adMobTransactionId"] as? String,
            text("source"), text("rewardPolicyKey"))
    }
}
