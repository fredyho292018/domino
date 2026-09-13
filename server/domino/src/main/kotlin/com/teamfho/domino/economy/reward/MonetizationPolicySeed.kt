package com.teamfho.domino.economy.reward

import com.google.auth.oauth2.GoogleCredentials
import com.google.cloud.firestore.FirestoreOptions
import com.teamfho.domino.config.FirebaseProperties
import org.springframework.boot.WebApplicationType
import org.springframework.boot.builder.SpringApplicationBuilder
import org.springframework.boot.context.properties.EnableConfigurationProperties

// Explicit primary source for the CLI only; deliberately NOT a scanned @Configuration.
@EnableConfigurationProperties(MonetizationPolicy::class, FirebaseProperties::class, MonetizationPolicyCacheSettings::class)
class MonetizationSeedConfiguration

object MonetizationPolicySeed {
    @JvmStatic fun main(args: Array<String>) {
        require(args.contains("--seed-if-absent")) { "EXPLICIT_SEED_FLAG_REQUIRED" }
        SpringApplicationBuilder(MonetizationSeedConfiguration::class.java)
            .web(WebApplicationType.NONE).logStartupInfo(false)
            .run(*args.filter { it != "--seed-if-absent" }.toTypedArray()).use { context ->
                val fallback = context.getBean(MonetizationPolicy::class.java)
                val project = context.getBean(FirebaseProperties::class.java).projectId
                FirestoreOptions.newBuilder().setProjectId(project)
                    .setCredentials(GoogleCredentials.getApplicationDefault()).build().service.use { db ->
                        val created = FirestoreMonetizationPolicyRepository(db).seedIfAbsent(fallback)
                        println("MONETIZATION_POLICY_SEED=" + if (created) "CREATED" else "ALREADY_EXISTS_UNCHANGED")
                }
            }
    }
}
