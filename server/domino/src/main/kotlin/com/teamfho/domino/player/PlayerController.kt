package com.teamfho.domino.player

import com.teamfho.domino.security.FirebaseIdentity
import org.springframework.security.core.annotation.AuthenticationPrincipal
import org.springframework.web.bind.annotation.PostMapping
import org.springframework.web.bind.annotation.RequestBody
import org.springframework.web.bind.annotation.RestController

@RestController
class PlayerController(private val service: PlayerBootstrapService) {
    @PostMapping("/api/v1/player/bootstrap", produces = ["application/json"])
    fun bootstrap(
        @AuthenticationPrincipal identity: FirebaseIdentity,
        @RequestBody(required = false) request: PlayerBootstrapRequest?
    ): PlayerBootstrapResponse = PlayerBootstrapResponse.from(service.bootstrap(identity, request))
}
