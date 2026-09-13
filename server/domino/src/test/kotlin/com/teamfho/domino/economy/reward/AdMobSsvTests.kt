package com.teamfho.domino.economy.reward

import org.junit.jupiter.api.Test
import org.junit.jupiter.params.ParameterizedTest
import org.junit.jupiter.params.provider.ValueSource
import java.security.KeyPairGenerator
import java.security.Signature
import java.security.interfaces.ECPublicKey
import java.security.spec.ECGenParameterSpec
import java.time.Duration
import java.util.Base64
import kotlin.test.*

class AdMobSsvTests {
    private val pair = KeyPairGenerator.getInstance("EC").apply { initialize(ECGenParameterSpec("secp256r1")) }.generateKeyPair()
    private val key = pair.public as ECPublicKey
    private val verifier = GoogleAdMobSsvVerifier { if (it != "123") reject("SSV_KEY_UNKNOWN") else key }
    private val content = "ad_network=5450213213286189855&ad_unit=5224354917&custom_data=%312345678-1234-4234-8234-123456789012&reward_amount=99&reward_item=gold%20coins&timestamp=1789214400000&transaction_id=aabbcc"
    private fun signed(data: String = content): String {
        val signer = Signature.getInstance("SHA256withECDSA")
        signer.initSign(pair.private); signer.update(data.toByteArray(Charsets.UTF_8))
        return data + "&signature=" + Base64.getUrlEncoder().withoutPadding().encodeToString(signer.sign()) + "&key_id=123"
    }
    @Test fun `valid encoded fixture and metadata not authoritative`() {
        val result = verifier.verify(signed())
        assertEquals("12345678-1234-4234-8234-123456789012", result.intentId)
        assertEquals("aabbcc", result.transactionId)
        // Verified event exposes no coin amount, UID or reward authority from Google metadata.
        assertEquals(4, VerifiedAdMobEvent::class.java.declaredFields.count { !it.isSynthetic })
    }
    @ParameterizedTest @ValueSource(strings = ["tamper", "signature", "key", "missing_signature", "missing_key", "duplicate", "reorder", "suffix", "encoding"])
    fun `malformed or altered callbacks rejected`(variant: String) {
        val original = signed()
        val invalid = when (variant) {
            "tamper" -> original.replace("reward_amount=99", "reward_amount=10")
            "signature" -> original.replace("signature=", "signature=A")
            "key" -> original.replace("key_id=123", "key_id=456")
            "missing_signature" -> content + "&key_id=123"
            "missing_key" -> original.substringBefore("&key_id")
            "duplicate" -> signed(content + "&ad_unit=5224354917")
            "reorder" -> original.replace("ad_network=5450213213286189855&ad_unit=5224354917", "ad_unit=5224354917&ad_network=5450213213286189855")
            "suffix" -> original + "&custom_data=other"
            else -> original.replace("%31", "1")
        }
        assertFailsWith<RewardFailure> { verifier.verify(invalid) }
    }
    @Test fun `signed missing custom data rejected`() {
        assertFailsWith<RewardFailure> { verifier.verify(signed(content.replace(Regex("&custom_data=[^&]+"), ""))) }
    }
    @Test fun `key cache hit expiry rotation and outage fail closed`() {
        val clock = RewardClock()
        var calls = 0; var fail = false; var keys = mapOf("123" to key)
        val cache = CachedAdMobPublicKeyProvider(AdMobKeyFetcher { calls++; if (fail) error("private"); keys }, clock, Duration.ofHours(12))
        assertEquals(key, cache.get("123")); assertEquals(key, cache.get("123")); assertEquals(1, calls)
        assertFailsWith<RewardFailure> { cache.get("456") }; assertEquals(1, calls)
        clock.value = clock.value.plusSeconds(61); keys = keys + ("456" to key)
        assertEquals(key, cache.get("456")); assertEquals(2, calls)
        clock.value = clock.value.plusSeconds(12 * 3600 + 1); fail = true
        assertEquals(503, assertFailsWith<RewardFailure> { cache.get("123") }.httpStatus)
        assertEquals(503, assertFailsWith<RewardFailure> { cache.get("123") }.httpStatus)
        assertEquals(3, calls)
        clock.value = clock.value.plusSeconds(61); fail = false
        assertEquals(key, cache.get("123")); assertEquals(4, calls)
    }
    @Test fun `key provider failure does not bypass crypto`() {
        assertEquals(503, assertFailsWith<RewardFailure> {
            GoogleAdMobSsvVerifier { reject("SSV_DEPENDENCY_UNAVAILABLE", 503) }.verify(signed())
        }.httpStatus)
    }
}
