package com.teamfho.domino.player

import java.nio.charset.StandardCharsets
import java.security.MessageDigest
import java.text.Normalizer
import java.util.Locale

/** Reservation authority shared by bootstrap, profile saves and the existing alias endpoint.
 * The rollout marker must be published only after an audited, write-frozen migration.
 * No request repairs legacy reservations or arbitrates legacy duplicate ownership.
 */
object PlayerAliasReservations {
    const val rolloutPath = "systemConfig/playerAliases"
    const val normalizationVersion = 1L
    fun canonical(value: String): String = Normalizer.normalize(value.trim(), Normalizer.Form.NFC).lowercase(Locale.ROOT)
    fun path(value: String): String = "playerAliases/" + MessageDigest.getInstance("SHA-256")
        .digest(canonical(value).toByteArray(StandardCharsets.UTF_8)).joinToString("") { "%02x".format(it) }

    // Reads first; the caller buffers these writes in its existing domain transaction.
    fun prepare(read: (String) -> Map<String, Any>?, owner: String, previous: String?, requested: String): Map<String, Map<String, Any>> {
        val rollout = read(rolloutPath)
        if (rollout?.get("status") != "READY" || (rollout["normalizationVersion"] as? Number)?.toLong() != normalizationVersion)
            throw PlayerFoundationException(FoundationError.DISPLAY_NAME_RESERVATIONS_NOT_READY)
        val targetPath = path(requested)
        val target = read(targetPath)
        val priorPath = previous?.let(::path)
        val prior = if (priorPath == targetPath) target else priorPath?.let(read)
        fun owned(record: Map<String, Any>?) = record?.get("state") == "CLAIMED" && record["playerId"] == owner
        if (previous != null && !owned(prior))
            throw PlayerFoundationException(FoundationError.DISPLAY_NAME_RESERVATIONS_NOT_READY)
        if (target != null && !owned(target) && target["state"] != "RELEASED")
            throw PlayerFoundationException(FoundationError.DISPLAY_NAME_TAKEN)
        if (target?.get("state") == "RELEASED" && target.containsKey("playerId"))
            throw PlayerFoundationException(FoundationError.DISPLAY_NAME_RESERVATIONS_NOT_READY)
        val changes = linkedMapOf<String, Map<String, Any>>()
        if (!owned(target)) changes[targetPath] = mapOf("state" to "CLAIMED", "playerId" to owner,
            "normalizationVersion" to normalizationVersion)
        if (priorPath != null && priorPath != targetPath)
            changes[priorPath] = mapOf("state" to "RELEASED", "normalizationVersion" to normalizationVersion)
        return changes
    }
}
