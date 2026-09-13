package com.teamfho.domino.economy.reward

import com.google.cloud.Timestamp
import com.google.cloud.firestore.*
import java.security.MessageDigest
import java.time.Clock
import java.time.Instant
import java.util.UUID
import java.util.concurrent.TimeUnit

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
                    intent.status != RewardIntentStatus.VERIFIED || intent.adMobTransactionId != event.transactionId)
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
