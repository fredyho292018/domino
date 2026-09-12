package com.teamfho.domino.config

import jakarta.validation.constraints.NotBlank
import jakarta.validation.constraints.Pattern
import org.springframework.boot.context.properties.ConfigurationProperties
import org.springframework.validation.annotation.Validated

@Validated
@ConfigurationProperties("firebase")
data class FirebaseProperties(
    @field:NotBlank(message = "firebase.project-id is required")
    @field:Pattern(
        regexp = "[a-z][a-z0-9-]{4,28}[a-z0-9]",
        message = "firebase.project-id must be a valid Google Cloud project ID (6-30 lowercase characters)"
    )
    val projectId: String = "",
    val enabled: Boolean = true
)
