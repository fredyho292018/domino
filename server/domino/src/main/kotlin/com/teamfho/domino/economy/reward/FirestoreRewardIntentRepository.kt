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
    private val policy: RewardPolicy, private val monetization: MonetizationPolicy = MonetizationPolicy()) : RewardIntentRepository {
    private fun usageRef(uid: String): DocumentReference {
        if (uid.isBlank() || uid.length > 128 || uid.contains('/') || uid in setOf(".","..")) reject("ACCOUNT_NOT_ELIGIBLE",403)
        return firestore.document("players/$uid/rewardUsage/current")
    }
    private fun opportunityRef(id: String): DocumentReference {
        if (!opaqueId(id)) reject("ROUND_LIMIT", 409)
        return firestore.document("rewardOpportunities/$id")
    }
    private fun records(tx: Transaction, uid: String, now: Instant): List<RewardCreditRecord> {
        // Single-field timestamp index. Includes legacy H4 ledger credits on first H6 request.
        val day = now.atZone(java.time.ZoneOffset.UTC).toLocalDate().atStartOfDay(java.time.ZoneOffset.UTC).toInstant()
        val cutoff = minOf(day, now.minusSeconds(maxOf(3600, monetization.rewarded.cooldownSeconds)))
        val rows = tx.get(firestore.collection("players/$uid/walletTransactions")
            .whereGreaterThanOrEqualTo("createdAt", stamp(cutoff)).limit(4097)).get().documents
        if (rows.size > 4096) reject("DEPENDENCY_UNAVAILABLE", 503) // Fail closed if future sources outgrow this read budget.
        return rows.filter { it.getString("source") == "REWARDED_AD" && it.getString("type") == "CREDIT" }.map {
            val time = it.getTimestamp("createdAt") ?: reject("DEPENDENCY_UNAVAILABLE", 503)
            val amount = it.getLong("amount") ?: reject("DEPENDENCY_UNAVAILABLE", 503)
            if (amount !in 1..Wallet.MAX_COINS) reject("DEPENDENCY_UNAVAILABLE", 503)
            RewardCreditRecord(Instant.ofEpochSecond(time.seconds, time.nanos.toLong()), amount)
        }
    }
    private fun enforce(eligibility: RewardEligibility) { eligibility.reason?.let { reject(it.name, 409) } }
    override fun eligibility(uid: String, opportunityId: String?): RewardEligibility = atomic { tx ->
        val now = clock.instant()
        tx.get(usageRef(uid)).get() // Same lock as consume; queries alone must not permit write skew.
        val opportunity = opportunityId?.let { tx.get(opportunityRef(it)).get().data }
        val used = opportunityId != null && (opportunity == null || opportunity["uid"] != uid || opportunity["status"] != "OPEN" ||
            (opportunity["expiresAt"] as? Timestamp)?.let { Instant.ofEpochSecond(it.seconds,it.nanos.toLong()) <= now } != false)
        RewardEligibilityPolicy.evaluate(monetization, records(tx, uid, now), now, used = used)
    }
    override fun opportunity(uid: String): RewardOpportunity {
        val candidate = java.util.UUID.randomUUID().toString()
        return atomic { tx ->
            val now = clock.instant(); val usage = tx.get(usageRef(uid)).get().data
            val existingId = usage?.get("opportunityId") as? String
            val existing = existingId?.let { tx.get(opportunityRef(it)).get().data }
            enforce(RewardEligibilityPolicy.evaluate(monetization, records(tx, uid, now), now))
            val expires = existing?.get("expiresAt") as? Timestamp
            if (existing != null && existing["uid"] == uid && existing["status"] == "OPEN" && expires != null &&
                Instant.ofEpochSecond(expires.seconds, expires.nanos.toLong()) > now)
                RewardOpportunity(existingId!!, Instant.ofEpochSecond(expires.seconds,expires.nanos.toLong()))
            else {
                val expiry = now.plus(policy.intentTtl)
                tx.create(opportunityRef(candidate), mapOf("opportunityId" to candidate,"uid" to uid,"source" to "ROUND_COMPLETE",
                    "status" to "OPEN","createdAt" to stamp(now),"expiresAt" to stamp(expiry)))
                tx.set(usageRef(uid), (usage ?: emptyMap()) + mapOf("opportunityId" to candidate))
                RewardOpportunity(candidate,expiry)
            }
        }
    }
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
        val rewardOpportunity = opportunity(uid)
        val slot = firestore.document("rewardIntentOwners/" + digest(uid))
        val candidateId = UUID.randomUUID().toString()
        return atomic { tx ->
            val now = clock.instant()
            val slotData = tx.get(slot).get().data
            val previousRef = (slotData?.get("intentId") as? String)?.let(::intentRef)
            val previous = previousRef?.let { tx.get(it).get().data }?.let(::decode)
            tx.get(usageRef(uid)).get()
            val opportunityData = tx.get(opportunityRef(rewardOpportunity.opportunityId)).get().data
            enforce(RewardEligibilityPolicy.evaluate(monetization, records(tx,uid,now), now,
                used = opportunityData?.get("uid") != uid || opportunityData["status"] != "OPEN"))
            if (previous != null && previous.uid != uid) reject("SSV_INTENT_INVALID", 409)
            // Preserve restart discovery until the previous verified reward is consumed.
            if (previous?.status == RewardIntentStatus.VERIFIED) reject("REWARD_PENDING", 409)
            if (previous != null && previous.status == RewardIntentStatus.ISSUED && now.isBefore(previous.expiresAt) &&
                previous.adUnitEnvironment == policy.environment) previous
            else {
                val intent = RewardIntent(candidateId, uid, RewardIntentStatus.ISSUED, now, now.plus(policy.intentTtl), policy.environment,
                    policyVersion = monetization.version, rewardAmountSnapshot = monetization.rewarded.coins, opportunityId = rewardOpportunity.opportunityId)
                if (previous?.status == RewardIntentStatus.ISSUED) tx.update(previousRef!!, mapOf(
                    "status" to (if (now.isBefore(previous.expiresAt)) "REJECTED" else "EXPIRED"), "updatedAt" to FieldValue.serverTimestamp()))
                tx.create(intentRef(candidateId), mapOf(
                    "intentId" to candidateId, "uid" to uid, "status" to "ISSUED", "source" to intent.source,
                    "rewardPolicyKey" to intent.rewardPolicyKey, "adUnitEnvironment" to intent.adUnitEnvironment.name,
                    "policyVersion" to intent.policyVersion, "rewardAmountSnapshot" to intent.rewardAmountSnapshot,
                    "opportunityId" to rewardOpportunity.opportunityId,
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
                if (intent.policyVersion < 1 || intent.opportunityId == null) reject("DEPENDENCY_UNAVAILABLE", 503)
                val amount = intent.rewardAmountSnapshot
                if (amount !in 1..Wallet.MAX_COINS) reject("REWARD_INTENT_INVALID", 409)
                val usage = tx.get(usageRef(uid)).get().data ?: emptyMap()
                val opportunity = tx.get(opportunityRef(intent.opportunityId)).get().data
                enforce(RewardEligibilityPolicy.evaluate(monetization, records(tx, uid, clock.instant()), clock.instant(), amount,
                    used = opportunity?.get("uid") != uid || opportunity["status"] != "OPEN", earned = true))
                val after = safeCredit(wallet.coins, amount)
                val earned = safeCredit(wallet.lifetimeCoinsEarned, amount)
                tx.create(ledgerRef, mapOf("transactionId" to ledgerId, "uid" to uid, "type" to "CREDIT",
                    "source" to intent.source, "amount" to amount, "balanceBefore" to wallet.coins, "balanceAfter" to after,
                    "rewardIntentId" to intentId, "rewardPolicyKey" to intent.rewardPolicyKey,
                    "policyVersion" to intent.policyVersion, "opportunityId" to intent.opportunityId,
                    "createdAt" to FieldValue.serverTimestamp(), "version" to RewardLedger.VERSION))
                tx.update(opportunityRef(intent.opportunityId), mapOf("status" to "USED", "intentId" to intentId,
                    "usedAt" to FieldValue.serverTimestamp()))
                tx.set(usageRef(uid), usage + mapOf("updatedAt" to FieldValue.serverTimestamp(), "lastIntentId" to intentId))
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
            text("source"), text("rewardPolicyKey"), data["policyVersion"] as? Long ?: 0,
            data["rewardAmountSnapshot"] as? Long ?: 10, data["opportunityId"] as? String)
    }
}
