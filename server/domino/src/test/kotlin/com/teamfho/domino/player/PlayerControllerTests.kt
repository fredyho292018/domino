package com.teamfho.domino.player

import com.teamfho.domino.security.FakeAuthConfiguration
import org.junit.jupiter.api.BeforeEach
import org.junit.jupiter.api.Test
import org.junit.jupiter.params.ParameterizedTest
import org.junit.jupiter.params.provider.CsvSource
import org.junit.jupiter.params.provider.ValueSource
import org.springframework.beans.factory.annotation.Autowired
import org.springframework.boot.test.context.SpringBootTest
import org.springframework.boot.webmvc.test.autoconfigure.AutoConfigureMockMvc
import org.springframework.context.annotation.Import
import org.springframework.test.context.ActiveProfiles
import org.springframework.test.web.servlet.MockMvc
import org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post
import org.springframework.test.web.servlet.result.MockMvcResultMatchers.*
import tools.jackson.databind.ObjectMapper
import java.util.UUID
import kotlin.test.*

@SpringBootTest
@ActiveProfiles("test")
@AutoConfigureMockMvc
@Import(FakeAuthConfiguration::class, FakePlayerFoundationConfiguration::class)
class PlayerControllerTests {
    @Autowired lateinit var mvc: MockMvc
    @Autowired lateinit var repository: FakePlayerFoundationRepository
    @Autowired lateinit var mapper: ObjectMapper
    @BeforeEach fun reset() = repository.reset()

    private fun call(body: String? = null, token: String? = "valid-guest") = mvc.perform(
        post("/api/v1/player/bootstrap").header("X-Request-ID", "untrusted-client-id").apply {
            token?.let { header("Authorization", "Bearer $it") }
            body?.let { contentType("application/json"); content(it) }
        }
    )

    private fun error(status: Int, code: String, body: String? = null, token: String? = "valid-guest") {
        val response = call(body, token).andExpect(status().`is`(status))
            .andExpect(jsonPath("$.code").value(code)).andReturn().response
        val json = mapper.readTree(response.contentAsString)
        assertEquals(3, json.size())
        val id = json["requestId"].asText()
        UUID.fromString(id)
        assertEquals(id, response.getHeader("X-Request-ID"))
        assertNotEquals("untrusted-client-id", id)
        assertFalse(response.contentAsString.contains("secret-internal"))
        assertFalse(response.contentAsString.contains("stackTrace"))
    }

    @Test fun `missing authentication`() { error(401, "AUTH_TOKEN_MISSING", token = null); assertEquals(0, repository.calls) }
    @Test fun `invalid authentication`() { error(401, "AUTH_TOKEN_INVALID", token = "invalid"); assertEquals(0, repository.calls) }

    @ParameterizedTest @CsvSource("valid-guest,verified-guest,GUEST", "valid-registered,verified-registered,REGISTERED")
    fun `verified identity and exact minimal response`(token: String, uid: String, type: String) {
        val response = call("""{"language":"es"}""", token).andExpect(status().isOk).andReturn().response
        val json = mapper.readTree(response.contentAsString)
        val name = json["player"]["displayName"].asText()
        assertTrue(name.matches(Regex("Guest-[A-Z0-9]{8}")))
        assertEquals(mapper.readTree("""{"player":{"uid":"$uid","accountType":"$type","displayName":"$name","language":"es","status":"ACTIVE"},"wallet":{"coins":0}}"""), json)
    }

    @Test fun `absent body accepted without content type`() { call().andExpect(status().isOk).andExpect(jsonPath("$.player.language").value("en")) }
    @ParameterizedTest @ValueSource(strings = ["{}", "{\"language\":null}", "{\"language\":\"en\"}"])
    fun `optional language defaults or accepts en`(body: String) { call(body).andExpect(status().isOk).andExpect(jsonPath("$.player.language").value("en")) }

    @ParameterizedTest @ValueSource(strings = ["fr", "de", "ES", "english", ""])
    fun `unsupported language`(language: String) { error(400, "LANGUAGE_UNSUPPORTED", """{"language":"$language"}"""); assertEquals(0, repository.calls) }

    @ParameterizedTest @ValueSource(strings = ["uid", "coins", "accountType", "status", "isAnonymous", "unexpected"])
    fun `unknown fields rejected by actual Jackson mapper`(field: String) { error(400, "REQUEST_INVALID", """{"$field":"secret-internal"}"""); assertEquals(0, repository.calls) }

    @ParameterizedTest @ValueSource(strings = ["{", "[]", "{\"language\":{}}", "{\"coins\":null}"])
    fun `invalid body uses common error contract`(body: String) { error(400, "REQUEST_INVALID", body); assertEquals(0, repository.calls) }

    @ParameterizedTest @CsvSource("PLAYER_STATE_CONFLICT,409,PLAYER_STATE_CONFLICT", "WALLET_STATE_INVALID,409,WALLET_STATE_INVALID", "FIRESTORE_UNAVAILABLE,503,DEPENDENCY_UNAVAILABLE", "FIRESTORE_CONTENTION_EXHAUSTED,503,FIRESTORE_CONTENTION_EXHAUSTED")
    fun `domain failures mapped`(failure: FoundationError, status: Int, code: String) { repository.failure = PlayerFoundationException(failure); error(status, code) }

    @Test fun `unexpected failure sanitized`() { repository.failure = IllegalStateException("secret-internal"); error(500, "INTERNAL_ERROR") }
    @Test fun `repeated requests preserve initial language and name`() {
        val first = call("""{"language":"es"}""").andExpect(status().isOk).andReturn().response.contentAsString
        val second = call().andExpect(status().isOk).andReturn().response.contentAsString
        assertEquals(mapper.readTree(first), mapper.readTree(second))
        assertEquals(2, repository.store.documents.size)
    }
}
