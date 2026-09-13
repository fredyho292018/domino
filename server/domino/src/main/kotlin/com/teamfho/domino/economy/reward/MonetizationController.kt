package com.teamfho.domino.economy.reward

import com.teamfho.domino.security.FirebaseIdentity
import org.springframework.security.core.annotation.AuthenticationPrincipal
import org.springframework.web.bind.annotation.*
import org.springframework.http.ResponseEntity
import org.slf4j.LoggerFactory
import tools.jackson.databind.JsonNode
import java.time.Clock
import java.util.LinkedHashMap

// Supplemental, bounded per-instance HTTP protection. Never an economic authority.
class MonetizationRequestGate(private val clock: Clock = Clock.systemUTC()) {
    private data class Window(val start: Long, var count: Int)
    private val windows = LinkedHashMap<String, Window>()
    @Synchronized fun check(uid: String) {
        val now = clock.instant().epochSecond
        windows.entries.removeIf { now - it.value.start >= 60 }
        if (!windows.containsKey(uid) && windows.size >= 10000) reject("RATE_LIMIT",429)
        val window = windows.getOrPut(uid) { Window(now,0) }
        if (++window.count > 60) reject("RATE_LIMIT",429)
    }
}
@RestController
class MonetizationController(private val policy: MonetizationPolicy, private val repository: RewardIntentRepository) {
    private val gate = MonetizationRequestGate()
    private val log = LoggerFactory.getLogger(javaClass)
    @GetMapping("/api/v1/monetization/config")
    fun config(@AuthenticationPrincipal identity: FirebaseIdentity): MonetizationResponse {
        gate.check(identity.uid); log.info("MONETIZATION_POLICY_LOADED")
        return policy.response()
    }
    @GetMapping("/api/v1/economy/ad-rewards/eligibility")
    fun eligibility(@AuthenticationPrincipal identity: FirebaseIdentity,
        @RequestParam(required = false) opportunityId: String?): RewardEligibility {
        gate.check(identity.uid)
        return repository.eligibility(identity.uid,opportunityId).also {
            if (it.eligible) log.info("REWARD_ELIGIBLE") else log.info("REWARD_BLOCKED reason={}",it.reason)
        }
    }
    @PostMapping("/api/v1/economy/ad-rewards/opportunities", consumes = ["application/json"])
    fun opportunity(@AuthenticationPrincipal identity: FirebaseIdentity, @RequestBody body: JsonNode): RewardOpportunity {
        gate.check(identity.uid)
        if (!body.isObject || !body.isEmpty) reject("REQUEST_INVALID")
        return repository.opportunity(identity.uid)
    }
    @ExceptionHandler(RewardFailure::class)
    fun failed(error: RewardFailure): ResponseEntity<Map<String,String>> =
        ResponseEntity.status(error.httpStatus).body(mapOf("code" to error.category))
}
