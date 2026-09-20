package com.teamfho.domino.social

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
class SocialRulesEmulatorTests {
    @Test fun `authenticated client cannot read or write public profiles codes identity settings blocks or inverse blocks`() {
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
        val token=b64("""{"alg":"none","typ":"JWT"}""")+"."+b64("""{"iss":"https://securetoken.google.com/demo-domino-f0","aud":"demo-domino-f0","sub":"s11-attacker","user_id":"s11-attacker","iat":$now,"exp":${now+3600},"firebase":{"sign_in_provider":"anonymous"}}""")+"."
        fun write(path:String)=http.send(HttpRequest.newBuilder(URI("$host/v1/projects/demo-domino-f0/databases/(default)/documents/$path"))
            .header("Authorization","Bearer $token").header("Content-Type","application/json")
            .method("PATCH",HttpRequest.BodyPublishers.ofString("""{"fields":{"plan":{"stringValue":"PREMIUM"}}}""")).build(),HttpResponse.BodyHandlers.ofString()).statusCode()
        // Verify the test token really authenticates; a denial caused by malformed auth is not evidence.
        val probe=rules.replace("// All current Domino", "match /__s11AuthProbe/{id} { allow write: if request.auth.uid == 's11-attacker'; }\n    // All current Domino")
        try {
            load(probe);assertEquals(200,write("__s11AuthProbe/check"))
            val paths=listOf("publicPlayerProfiles/fake", "publicPlayerCodes/FHO-000000000000", "players/s11-attacker/publicIdentity/current", "players/s11-attacker/blocks/target", "players/s11-attacker/blockedBy/target", "players/s11-attacker/socialSettings/current")
            for(path in paths) {
                assertEquals(403,write(path),path)
                val response=http.send(HttpRequest.newBuilder(URI("$host/v1/projects/demo-domino-f0/databases/(default)/documents/$path")).header("Authorization","Bearer $token").GET().build(),HttpResponse.BodyHandlers.ofString())
                assertEquals(403,response.statusCode(),path)
            }
            val list=http.send(HttpRequest.newBuilder(URI("$host/v1/projects/demo-domino-f0/databases/(default)/documents/publicPlayerProfiles")).header("Authorization","Bearer $token").GET().build(),HttpResponse.BodyHandlers.ofString())
            assertEquals(403,list.statusCode())
        }finally{load(rules)}
    }
}
