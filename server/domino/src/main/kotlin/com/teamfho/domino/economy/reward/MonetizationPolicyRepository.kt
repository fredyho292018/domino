package com.teamfho.domino.economy.reward

import com.google.cloud.Timestamp
import com.google.cloud.firestore.Firestore
import com.google.cloud.firestore.FieldValue
import com.google.api.gax.rpc.AlreadyExistsException
import java.util.concurrent.TimeUnit

sealed interface PolicyRead {
    data class Active(val policy: MonetizationPolicy) : PolicyRead
    data object NotFound : PolicyRead
    data class Invalid(val reason: String) : PolicyRead
    data object Unavailable : PolicyRead
}

fun interface MonetizationPolicyRepository { fun read(): PolicyRead }

class FirestoreMonetizationPolicyRepository(
    private val firestore: Firestore,
    private val documentPath: String = DOCUMENT_PATH
) : MonetizationPolicyRepository {
    companion object { const val DOCUMENT_PATH = "systemConfig/monetization" }

    override fun read(): PolicyRead = try {
        val document = firestore.document(documentPath).get().get(5, TimeUnit.SECONDS)
        if (!document.exists()) PolicyRead.NotFound else decode(document.data ?: emptyMap())
    } catch (_: InterruptedException) {
        Thread.currentThread().interrupt(); PolicyRead.Unavailable
    } catch (_: Exception) { PolicyRead.Unavailable }

    // Explicit command only. Firestore create has an exists=false precondition;
    // concurrent seeds never overwrite an existing policy.
    fun seedIfAbsent(policy: MonetizationPolicy): Boolean = try {
        firestore.document(documentPath).create(encode(policy)).get(10, TimeUnit.SECONDS)
        true
    } catch (error: Exception) {
        if (error is InterruptedException) Thread.currentThread().interrupt()
        if (generateSequence<Throwable>(error) { it.cause }.take(10).any { it is AlreadyExistsException }) false
        else throw IllegalStateException("MONETIZATION_POLICY_SEED_UNAVAILABLE")
    }

    internal fun decode(data: Map<String, Any>): PolicyRead {
        if (data["status"] != "ACTIVE") return PolicyRead.Invalid("STATUS")
        return try {
            fun number(key: String) = data[key] as? Long ?: throw IllegalArgumentException()
            fun integer(key: String) = Math.toIntExact(number(key))
            fun flag(key: String) = data[key] as? Boolean ?: throw IllegalArgumentException()
            // Metadata is optional for an initial manual document, but validated when present.
            for (key in listOf("createdAt", "updatedAt")) require(data[key] == null || data[key] is Timestamp)
            require(data["updatedBy"] == null || data["updatedBy"] in setOf("bootstrap", "manual-admin", "migration"))
            PolicyRead.Active(MonetizationPolicy(number("version"), flag("adsEnabled"), RewardedRules(
                flag("rewardedEnabled"), number("rewardCoins"), integer("maxPerRound"), number("cooldownSeconds"),
                integer("maxPerHour"), integer("maxPerDay"), number("maxCoinsPerDay"))))
        } catch (_: Exception) { PolicyRead.Invalid("DOCUMENT_CONTRACT") }
    }

    internal fun encode(policy: MonetizationPolicy): Map<String, Any> = with(policy.rewarded) {
        mapOf("version" to policy.version, "adsEnabled" to policy.adsEnabled, "rewardedEnabled" to enabled,
            "rewardCoins" to coins, "maxPerRound" to maxPerRound.toLong(), "cooldownSeconds" to cooldownSeconds,
            "maxPerHour" to maxPerHour.toLong(), "maxPerDay" to maxPerDay.toLong(), "maxCoinsPerDay" to maxCoinsPerDay,
            "status" to "ACTIVE", "createdAt" to FieldValue.serverTimestamp(), "updatedAt" to FieldValue.serverTimestamp(),
            "updatedBy" to "bootstrap")
    }
}
