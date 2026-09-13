package com.teamfho.domino.economy.reward

import java.net.URI
import java.net.http.HttpClient
import java.net.http.HttpRequest
import java.net.http.HttpResponse
import java.security.KeyFactory
import java.security.interfaces.ECPublicKey
import java.security.spec.X509EncodedKeySpec
import java.time.Clock
import java.time.Duration
import java.time.Instant
import java.util.Base64
import java.util.concurrent.TimeUnit
import tools.jackson.databind.ObjectMapper

fun interface AdMobPublicKeyProvider { fun get(keyId: String): ECPublicKey }
fun interface AdMobKeyFetcher { fun fetch(): Map<String, ECPublicKey> }

class GoogleAdMobKeyFetcher(private val mapper: ObjectMapper) : AdMobKeyFetcher {
    private val client = HttpClient.newBuilder().connectTimeout(Duration.ofSeconds(5))
        .followRedirects(HttpClient.Redirect.NEVER).build()
    override fun fetch(): Map<String, ECPublicKey> {
        try {
            val request = HttpRequest.newBuilder(URI.create("https://www.gstatic.com/admob/reward/verifier-keys.json"))
                .timeout(Duration.ofSeconds(5)).GET().build()
            // Bound the whole response, including the body (not just arrival of headers).
            val pending = client.sendAsync(request, HttpResponse.BodyHandlers.ofByteArray())
            val response = try { pending.get(8, TimeUnit.SECONDS) } finally { if (!pending.isDone) pending.cancel(true) }
            val body = response.body()
            if (response.statusCode() != 200 || body.size > 65536) reject("SSV_DEPENDENCY_UNAVAILABLE", 503)
            val result = mutableMapOf<String, ECPublicKey>()
            val entries = mapper.readTree(body)["keys"]
            if (entries == null || !entries.isArray || entries.size() !in 1..32) reject("SSV_DEPENDENCY_UNAVAILABLE", 503)
            for (entry in entries) {
                val id = entry["keyId"]?.asText() ?: reject("SSV_DEPENDENCY_UNAVAILABLE", 503)
                val encoded = entry["base64"]?.asText() ?: reject("SSV_DEPENDENCY_UNAVAILABLE", 503)
                val key = KeyFactory.getInstance("EC").generatePublic(X509EncodedKeySpec(Base64.getDecoder().decode(encoded))) as ECPublicKey
                if (!id.matches(Regex("[0-9]{1,20}")) || key.params.curve.field.fieldSize != 256 || result.put(id, key) != null)
                    reject("SSV_DEPENDENCY_UNAVAILABLE", 503)
            }
            return result.toMap()
        } catch (failure: RewardFailure) { throw failure }
        catch (_: InterruptedException) { Thread.currentThread().interrupt(); reject("SSV_DEPENDENCY_UNAVAILABLE", 503) }
        catch (_: Exception) { reject("SSV_DEPENDENCY_UNAVAILABLE", 503) }
    }
}

class CachedAdMobPublicKeyProvider(private val fetcher: AdMobKeyFetcher, private val clock: Clock,
    private val ttl: Duration) : AdMobPublicKeyProvider {
    private var keys = emptyMap<String, ECPublicKey>()
    private var validUntil = Instant.MIN
    private var nextRefresh = Instant.MIN
    init { require(ttl > Duration.ZERO && ttl <= Duration.ofHours(24)) }
    @Synchronized override fun get(keyId: String): ECPublicKey {
        val now = clock.instant()
        if (now.isBefore(validUntil)) keys[keyId]?.let { return it }
        // Coalesce unknown keys/outages: an attacker cannot trigger an outbound fetch per request.
        if (!now.isBefore(nextRefresh)) {
            nextRefresh = now.plusSeconds(60)
            val fetched = try { fetcher.fetch() } catch (_: Exception) { reject("SSV_DEPENDENCY_UNAVAILABLE", 503) }
            if (fetched.isEmpty()) reject("SSV_DEPENDENCY_UNAVAILABLE", 503)
            keys = fetched.toMap()
            validUntil = now.plus(ttl)
        }
        if (!now.isBefore(validUntil)) reject("SSV_DEPENDENCY_UNAVAILABLE", 503)
        return keys[keyId] ?: reject("SSV_KEY_UNKNOWN")
    }
}
