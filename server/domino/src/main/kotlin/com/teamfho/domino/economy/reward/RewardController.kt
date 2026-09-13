package com.teamfho.domino.economy.reward

import com.teamfho.domino.security.FirebaseIdentity
import jakarta.servlet.http.HttpServletRequest
import org.slf4j.LoggerFactory
import org.springframework.http.ResponseEntity
import org.springframework.security.core.annotation.AuthenticationPrincipal
import org.springframework.web.bind.annotation.*
import tools.jackson.databind.JsonNode

@RestController
class RewardController(private val repository: RewardIntentRepository, private val verifier: AdMobSsvVerifier, private val policy: RewardPolicy) {
    private val log = LoggerFactory.getLogger(javaClass)
    @PostMapping("/api/v1/economy/ad-rewards/intents", consumes = ["application/json"])
    fun issue(@AuthenticationPrincipal identity: FirebaseIdentity, @RequestBody body: JsonNode): RewardIntentResponse {
        if (!body.isObject || !body.isEmpty) reject("REQUEST_INVALID")
        return RewardIntentResponse.from(repository.issue(identity.uid), policy.rewardCoins)
    }
    @GetMapping("/api/v1/economy/ad-rewards/intents/{intentId}")
    fun status(@AuthenticationPrincipal identity: FirebaseIdentity, @PathVariable intentId: String): RewardIntentResponse =
        RewardIntentResponse.from(repository.status(identity.uid, intentId), policy.rewardCoins)
    @PostMapping("/api/v1/economy/ad-rewards/intents/{intentId}/consume", consumes = ["application/json"])
    fun consume(@AuthenticationPrincipal identity: FirebaseIdentity, @PathVariable intentId: String,
        @RequestBody body: JsonNode): RewardConsumeResponse {
        if (!body.isObject || !body.isEmpty) reject("REQUEST_INVALID")
        log.info("[ECONOMY] reward consume started")
        return repository.consume(identity.uid, intentId).also { log.info("[ECONOMY] reward consumed or already consumed") }
    }
    @GetMapping("/api/v1/economy/ad-rewards/pending")
    fun pending(@AuthenticationPrincipal identity: FirebaseIdentity): Map<String, RewardIntentResponse?> =
        mapOf("intent" to repository.pending(identity.uid)?.let { RewardIntentResponse.from(it, policy.rewardCoins) })
    @GetMapping("/api/v1/admob/rewarded/ssv")
    fun ssv(request: HttpServletRequest): ResponseEntity<Void> {
        val event = verifier.verify(request.queryString)
        val changed = repository.verify(event)
        log.info(if (changed) "[ADS-SSV] verified" else "[ADS-SSV] duplicate ignored")
        return ResponseEntity.ok().build()
    }
    @ExceptionHandler(RewardFailure::class)
    fun failed(error: RewardFailure, request: HttpServletRequest): ResponseEntity<Map<String, String>> {
        log.info(if (request.requestURI.endsWith("/consume")) "[ECONOMY] reward consume failed category={}" else
            "[ADS-SSV] rejected category={}", error.category)
        return ResponseEntity.status(error.httpStatus).body(mapOf("code" to error.category))
    }
}
