package com.teamfho.domino.player

import com.google.cloud.firestore.Firestore
import com.teamfho.domino.security.FirebaseIdentity
import com.teamfho.domino.social.SocialOperation
import com.teamfho.domino.social.SocialRateGate
import org.springframework.beans.factory.ObjectProvider
import org.springframework.security.core.annotation.AuthenticationPrincipal
import org.springframework.web.bind.annotation.*

data class AliasAvailabilityResponse(val state: String)

/** Advisory only. The save transaction remains the sole reservation authority. */
object AliasAvailability {
    fun check(owner: String, candidate: String?, read: (String) -> Map<String, Any>?): AliasAvailabilityResponse {
        val alias = DisplayNameRules.validate(candidate)
        val marker = read(PlayerAliasReservations.rolloutPath)
        if (marker?.get("status") != "READY" || (marker["normalizationVersion"] as? Number)?.toLong() != 1L)
            throw PlayerFoundationException(FoundationError.DISPLAY_NAME_RESERVATIONS_NOT_READY)
        val claim = read(PlayerAliasReservations.path(alias))
        val available = when {
            claim == null -> true
            (claim["normalizationVersion"] as? Number)?.toLong() != PlayerAliasReservations.normalizationVersion ->
                throw PlayerFoundationException(FoundationError.DISPLAY_NAME_RESERVATIONS_NOT_READY)
            claim["state"] == "RELEASED" && !claim.containsKey("playerId") -> true
            claim["state"] == "CLAIMED" && claim["playerId"] == owner -> true
            claim["state"] == "CLAIMED" && (claim["playerId"] as? String)?.isNotBlank() == true -> false
            else -> throw PlayerFoundationException(FoundationError.DISPLAY_NAME_RESERVATIONS_NOT_READY)
        }
        return AliasAvailabilityResponse(if (available) "AVAILABLE" else "TAKEN")
    }
}

@RestController
class PlayerAliasAvailabilityController(private val database: ObjectProvider<Firestore>, private val rate: SocialRateGate) {
    // POST keeps the candidate out of URL/access-log query strings. This operation never writes Firestore.
    @PostMapping("/api/v1/player/display-name/availability", consumes = ["application/json"], produces = ["application/json"])
    fun availability(@AuthenticationPrincipal identity: FirebaseIdentity,
        @RequestBody request: PlayerDisplayNameRequest): AliasAvailabilityResponse {
        val alias = DisplayNameRules.validate(request.displayName as? String)
        rate.check(identity.uid, SocialOperation.PROFILE)
        val db = database.ifAvailable ?: throw PlayerFoundationException(FoundationError.FIRESTORE_UNAVAILABLE)
        return AliasAvailability.check(identity.uid, alias) { db.document(it).get().get().data }
    }
}
