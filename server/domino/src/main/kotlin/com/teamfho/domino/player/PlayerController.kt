package com.teamfho.domino.player

import com.teamfho.domino.security.FirebaseIdentity
import org.springframework.security.core.annotation.AuthenticationPrincipal
import org.springframework.web.bind.annotation.PostMapping
import org.springframework.web.bind.annotation.PutMapping
import org.springframework.web.bind.annotation.RequestBody
import org.springframework.web.bind.annotation.RestController

@RestController
class PlayerController(private val service: PlayerBootstrapService, private val displayNames: PlayerDisplayNameService,
    private val entitlements: com.teamfho.domino.entitlement.EntitlementService) {
    @PutMapping("/api/v1/player/display-name", consumes = ["application/json"], produces = ["application/json"])
    fun updateDisplayName(@AuthenticationPrincipal identity: FirebaseIdentity,
        @RequestBody request: PlayerDisplayNameRequest): PlayerBootstrapResponse =
        PlayerBootstrapResponse.from(displayNames.update(identity, request))
    @PostMapping("/api/v1/player/bootstrap", produces = ["application/json"])
    fun bootstrap(
        @AuthenticationPrincipal identity: FirebaseIdentity,
        @RequestBody(required = false) request: PlayerBootstrapRequest?
    ): PlayerBootstrapResponse = PlayerBootstrapResponse.from(service.bootstrap(identity, request))
        .copy(entitlements=entitlements.bootstrap(identity))
}
