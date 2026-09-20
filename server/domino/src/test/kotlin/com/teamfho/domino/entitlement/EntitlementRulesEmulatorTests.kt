package com.teamfho.domino.entitlement

import com.teamfho.domino.catalog.GameCatalogCodec
import org.junit.jupiter.api.Tag
import org.junit.jupiter.api.Test
import java.net.URI
import java.net.http.*
import java.nio.file.*
import java.time.Instant
import java.util.Base64
import kotlin.test.*

@Tag("EMULATOR")
class EntitlementRulesEmulatorTests {
    @Test fun `authenticated direct client cannot write own grants projection trial marker policy or test marker`() {
        check(System.getenv("FIRESTORE_EMULATOR_HOST")=="127.0.0.1:18085")
        val http=HttpClient.newHttpClient();val host="http://127.0.0.1:18085"
        val rules=Files.readString(Path.of("firestore.rules"))
        fun load(content:String) {
            val json=GameCatalogCodec.mapper.writeValueAsString(mapOf("rules" to mapOf("files" to listOf(mapOf("content" to content)))))
            val response=http.send(HttpRequest.newBuilder(URI("$host/emulator/v1/projects/demo-domino-f0:securityRules"))
                .PUT(HttpRequest.BodyPublishers.ofString(json)).header("Content-Type","application/json").build(),HttpResponse.BodyHandlers.ofString())
            assertEquals(200,response.statusCode())
        }
        fun b64(value:String)=Base64.getUrlEncoder().withoutPadding().encodeToString(value.toByteArray())
        val now=Instant.now().epochSecond
        // Unsigned emulator identity only. Never accepted by real Firebase verification.
        val token=b64("""{"alg":"none","typ":"JWT"}""")+"."+b64("""{"iss":"https://securetoken.google.com/demo-domino-f0","aud":"demo-domino-f0","sub":"p01-attacker","user_id":"p01-attacker","iat":$now,"exp":${now+3600},"firebase":{"sign_in_provider":"anonymous"}}""")+"."
        fun write(path:String)=http.send(HttpRequest.newBuilder(URI("$host/v1/projects/demo-domino-f0/databases/(default)/documents/$path"))
            .header("Authorization","Bearer $token").header("Content-Type","application/json")
            .method("PATCH",HttpRequest.BodyPublishers.ofString("""{"fields":{"plan":{"stringValue":"PREMIUM"}}}""")).build(),HttpResponse.BodyHandlers.ofString()).statusCode()
        // Verify the test token really authenticates; a denial caused by malformed auth is not evidence.
        val probe=rules.replace("// All current Domino", "match /__p01AuthProbe/{id} { allow write: if request.auth.uid == 'p01-attacker'; }\n    // All current Domino")
        try {
            load(probe);assertEquals(200,write("__p01AuthProbe/check"))
            for(path in listOf("players/p01-attacker/entitlementGrants/hack","players/p01-attacker/entitlementState/current",
                "players/p01-attacker/promotions/initial-premium-trial","systemConfig/subscriptionPolicy","developmentTestAccounts/p01-attacker"))
                assertEquals(403,write(path),path)
        }finally{load(rules)}
    }
}
