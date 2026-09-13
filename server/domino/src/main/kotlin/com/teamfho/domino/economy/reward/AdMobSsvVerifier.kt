package com.teamfho.domino.economy.reward

import java.net.URLDecoder
import java.nio.charset.StandardCharsets.UTF_8
import java.security.Signature
import java.time.Instant
import java.util.Base64

// Returned only after the original bytes have passed cryptographic verification.
data class VerifiedAdMobEvent(val intentId: String, val transactionId: String, val adUnit: String, val timestamp: Instant)
fun interface AdMobSsvVerifier { fun verify(rawQuery: String?): VerifiedAdMobEvent }

class GoogleAdMobSsvVerifier(private val keys: AdMobPublicKeyProvider) : AdMobSsvVerifier {
    override fun verify(rawQuery: String?): VerifiedAdMobEvent {
        try {
            val raw = rawQuery ?: reject("SSV_SIGNATURE_INVALID")
            if (raw.length !in 1..8192 || raw.any { it.code !in 33..126 }) reject("SSV_SIGNATURE_INVALID")
            val parts = raw.split('&')
            if (parts.size !in 9..10 || !parts[parts.size - 2].startsWith("signature=") ||
                !parts.last().startsWith("key_id=")) reject("SSV_SIGNATURE_INVALID")
            val fields = linkedMapOf<String, String>()
            for (part in parts) {
                val i = part.indexOf('=')
                if (i <= 0) reject("SSV_SIGNATURE_INVALID")
                val name = part.substring(0, i)
                if (fields.put(name, part.substring(i + 1)) != null) reject("SSV_SIGNATURE_INVALID")
            }
            val required = setOf("ad_network", "ad_unit", "custom_data", "reward_amount", "reward_item",
                "timestamp", "transaction_id", "signature", "key_id")
            if (!fields.keys.containsAll(required) || (fields.keys - required - "user_id").isNotEmpty())
                reject("SSV_SIGNATURE_INVALID")
            val keyId = fields.getValue("key_id")
            if (!keyId.matches(Regex("[0-9]{1,20}"))) reject("SSV_KEY_UNKNOWN")
            val signature = fields.getValue("signature")
            if (!signature.matches(Regex("[A-Za-z0-9_-]{80,110}={0,2}"))) reject("SSV_SIGNATURE_INVALID")
            // Preserve the raw encoding and ordering. Never sign a reconstructed parameter map.
            val content = raw.substring(0, raw.lastIndexOf("&signature=")).toByteArray(UTF_8)
            val verifier = Signature.getInstance("SHA256withECDSA")
            verifier.initVerify(keys.get(keyId))
            verifier.update(content)
            if (!verifier.verify(Base64.getUrlDecoder().decode(signature))) reject("SSV_SIGNATURE_INVALID")
            fun decoded(name: String) = URLDecoder.decode(fields.getValue(name), UTF_8)
            val intent = decoded("custom_data")
            if (!opaqueId(intent)) reject("SSV_INTENT_NOT_FOUND", 404)
            val transaction = decoded("transaction_id")
            if (!transaction.matches(Regex("[A-Fa-f0-9]{1,128}"))) reject("SSV_TRANSACTION_INVALID")
            val unit = decoded("ad_unit")
            if (!unit.matches(Regex("[0-9]{1,30}"))) reject("SSV_AD_UNIT_INVALID")
            if (!decoded("reward_amount").matches(Regex("[0-9]{1,12}")) || decoded("reward_item").length !in 1..128)
                reject("SSV_REWARD_METADATA_INVALID")
            val timestamp = decoded("timestamp").toLongOrNull() ?: reject("SSV_TIMESTAMP_INVALID")
            return VerifiedAdMobEvent(intent, transaction, unit, Instant.ofEpochMilli(timestamp))
        } catch (failure: RewardFailure) { throw failure }
        catch (_: Exception) { reject("SSV_SIGNATURE_INVALID") }
    }
}
