package com.teamfho.domino.security

import jakarta.servlet.http.Cookie
import jakarta.servlet.http.HttpServletRequest
import org.junit.jupiter.api.BeforeEach
import org.junit.jupiter.api.Test
import org.junit.jupiter.params.ParameterizedTest
import org.junit.jupiter.params.provider.CsvSource
import org.junit.jupiter.params.provider.ValueSource
import org.springframework.beans.factory.annotation.Autowired
import org.springframework.boot.test.context.SpringBootTest
import org.springframework.boot.webmvc.test.autoconfigure.AutoConfigureMockMvc
import org.springframework.context.annotation.Import
import org.springframework.security.core.Authentication
import org.springframework.security.core.context.SecurityContextHolder
import org.springframework.test.context.ActiveProfiles
import org.springframework.test.web.servlet.MockMvc
import org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get
import org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post
import org.springframework.test.web.servlet.result.MockMvcResultMatchers.*
import org.springframework.web.bind.annotation.GetMapping
import org.springframework.web.bind.annotation.PostMapping
import org.springframework.web.bind.annotation.RestController
import java.util.UUID
import kotlin.test.*

@SpringBootTest
@ActiveProfiles("test")
@AutoConfigureMockMvc
@Import(FakeAuthConfiguration::class, FirebaseSecurityTests.ProbeController::class,
    com.teamfho.domino.player.FakePlayerFoundationConfiguration::class)
class FirebaseSecurityTests {
    @Autowired lateinit var mvc: MockMvc
    @Autowired lateinit var verifier: FakeFirebaseTokenVerifier
    @BeforeEach fun reset() { verifier.calls.set(0) }

    @RestController
    class ProbeController {
        @GetMapping("/api/test-identity")
        fun identity(auth: Authentication, request: HttpServletRequest) = mapOf(
            "uid" to (auth.principal as FirebaseIdentity).uid,
            "isAnonymous" to (auth.principal as FirebaseIdentity).isAnonymous,
            "credentialsAbsent" to (auth.credentials == null),
            "authenticated" to auth.isAuthenticated,
            "sessionAbsent" to (request.getSession(false) == null)
        )
        @PostMapping("/api/test-identity")
        fun postIdentity(auth: Authentication, request: HttpServletRequest) = identity(auth, request)
    }

    @Test fun `missing header returns JSON without redirect or session`() {
        val result = mvc.perform(get("/api/test-identity")).andExpect(status().isUnauthorized)
            .andExpect(content().contentTypeCompatibleWith("application/json"))
            .andExpect(jsonPath("$.code").value("AUTH_TOKEN_MISSING"))
            .andExpect(header().doesNotExist("Location"))
            .andExpect(header().string("WWW-Authenticate", "Bearer")).andReturn()
        assertNull(result.request.getSession(false))
        assertEquals(0, verifier.calls.get())
    }

    @ParameterizedTest
    @ValueSource(strings = ["", " ", "Basic xyz", "Bearer", "Bearer ", "Bearer token extra", "Bearer a,b"])
    fun `malformed authorization fails without verification`(header: String) {
        mvc.perform(get("/api/test-identity").header("Authorization", header))
            .andExpect(status().isUnauthorized).andExpect(content().contentTypeCompatibleWith("application/json"))
        assertEquals(0, verifier.calls.get())
    }

    @ParameterizedTest
    @CsvSource("invalid,401,AUTH_TOKEN_INVALID", "expired,401,AUTH_TOKEN_EXPIRED",
        "revoked,401,AUTH_SESSION_INVALID", "disabled,401,AUTH_SESSION_INVALID", "deleted,401,AUTH_SESSION_INVALID",
        "unavailable,503,DEPENDENCY_UNAVAILABLE", "unexpected,500,INTERNAL_ERROR")
    fun `verifier failures map to safe JSON`(token: String, status: Int, code: String) {
        val result = mvc.perform(get("/api/test-identity").header("Authorization", "Bearer $token"))
            .andExpect(status().`is`(status)).andExpect(jsonPath("$.code").value(code))
            .andExpect(header().doesNotExist("Location")).andReturn()
        assertFalse(result.response.contentAsString.contains("Sensitive internal"))
        assertFalse(result.response.contentAsString.contains("Bearer"))
        assertEquals(1, verifier.calls.get())
        assertNull(SecurityContextHolder.getContext().authentication)
    }

    @ParameterizedTest
    @CsvSource("valid-guest,verified-guest,true", "valid-registered,verified-registered,false")
    fun `verified identity is stateless and ignores client UID`(token: String, uid: String, anonymous: Boolean) {
        val result = mvc.perform(get("/api/test-identity").header("Authorization", "bEaReR $token")
            .header("X-UID", "attacker").param("uid", "attacker"))
            .andExpect(status().isOk).andExpect(jsonPath("$.uid").value(uid))
            .andExpect(jsonPath("$.isAnonymous").value(anonymous))
            .andExpect(jsonPath("$.credentialsAbsent").value(true))
            .andExpect(jsonPath("$.authenticated").value(true))
            .andExpect(jsonPath("$.sessionAbsent").value(true)).andReturn()
        assertEquals(1, verifier.calls.get())
        assertNull(result.request.getSession(false))
        assertNull(result.response.getCookie("JSESSIONID"))
        assertNull(SecurityContextHolder.getContext().authentication)
        mvc.perform(get("/api/test-identity")).andExpect(status().isUnauthorized)
    }

    @Test fun `query cookie and alternate headers are not authentication`() {
        mvc.perform(get("/api/test-identity").param("access_token", "valid-guest")
            .cookie(Cookie("token", "valid-guest")).header("X-Auth-Token", "valid-guest"))
            .andExpect(status().isUnauthorized)
        assertEquals(0, verifier.calls.get())
    }

    @Test fun `multiple authorization headers are rejected`() {
        mvc.perform(get("/api/test-identity").header("Authorization", "Bearer valid-guest", "Bearer valid-registered"))
            .andExpect(status().isUnauthorized)
        assertEquals(0, verifier.calls.get())
    }

    @Test fun `public health exposes only status and does not verify token`() {
        mvc.perform(get("/actuator/health").header("Authorization", "Bearer invalid"))
            .andExpect(status().isOk).andExpect(jsonPath("$.status").value("UP"))
            .andExpect(jsonPath("$.components").doesNotExist()).andExpect(jsonPath("$.details").doesNotExist())
        assertEquals(0, verifier.calls.get())
    }

    @Test fun `other routes are denied with JSON 403`() {
        mvc.perform(get("/actuator/env").header("Authorization", "Bearer valid-guest"))
            .andExpect(status().isForbidden).andExpect(jsonPath("$.code").value("ACCESS_DENIED"))
    }

    @Test fun `post bearer needs neither CSRF nor a session`() {
        mvc.perform(post("/api/test-identity").header("Authorization", "Bearer valid-guest"))
            .andExpect(status().isOk).andExpect(jsonPath("$.sessionAbsent").value(true))
    }

    @Test fun `request IDs are generated by server and match error body`() {
        val result = mvc.perform(get("/api/test-identity").header("X-Request-ID", "client-supplied"))
            .andExpect(status().isUnauthorized).andReturn()
        val id = result.response.getHeader("X-Request-ID")!!
        UUID.fromString(id)
        assertNotEquals("client-supplied", id)
        assertTrue(result.response.contentAsString.contains(id))
        assertNull(org.slf4j.MDC.get("requestId"))
    }
}
