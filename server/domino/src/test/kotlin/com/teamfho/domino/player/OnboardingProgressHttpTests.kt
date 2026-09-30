package com.teamfho.domino.player

import com.teamfho.domino.catalog.GameCatalogCodec
import com.teamfho.domino.security.FakeAuthConfiguration
import org.junit.jupiter.api.BeforeEach
import org.junit.jupiter.api.Test
import org.springframework.beans.factory.annotation.Autowired
import org.springframework.boot.test.context.SpringBootTest
import org.springframework.boot.test.context.TestConfiguration
import org.springframework.boot.webmvc.test.autoconfigure.AutoConfigureMockMvc
import org.springframework.context.annotation.Bean
import org.springframework.context.annotation.Primary
import org.springframework.context.annotation.Import
import org.springframework.test.context.ActiveProfiles
import org.springframework.test.web.servlet.MockMvc
import org.springframework.test.web.servlet.request.MockMvcRequestBuilders.*
import org.springframework.test.web.servlet.result.MockMvcResultMatchers.*
import java.util.UUID
import kotlin.test.*

@SpringBootTest
@ActiveProfiles("test")
@AutoConfigureMockMvc
@Import(FakeAuthConfiguration::class,FakePlayerFoundationConfiguration::class,OnboardingProgressHttpTests.Fixture::class)
class OnboardingProgressHttpTests {
    @TestConfiguration class Fixture { @Bean @Primary fun progressFixture()=ProgressMemory() }
    @Autowired lateinit var mvc:MockMvc
    @Autowired lateinit var repo:ProgressMemory
    @BeforeEach fun reset(){repo.docs.clear();repo.initialize("verified-guest")}
    private val path="/api/v1/player/onboarding"
    private fun request()=GameCatalogCodec.json(OnboardingStartRequest(UUID.randomUUID().toString(),0))
    @Test fun `GET requires authentication and never creates state`() {
        val before=repo.docs.toMap()
        mvc.perform(get(path)).andExpect(status().isUnauthorized)
        mvc.perform(get(path).header("Authorization","Bearer valid-guest")).andExpect(status().isOk)
            .andExpect(jsonPath("$.status").value("NOT_STARTED")).andExpect(jsonPath("$.revision").value(0))
            .andExpect(jsonPath("$.uid").doesNotExist()).andExpect(header().string("Cache-Control","no-store"))
        assertEquals(before,repo.docs)
    }
    @Test fun `real HTTP start save and resume contract`() {
        mvc.perform(post("$path/start").header("Authorization","Bearer valid-guest").contentType("application/json").content(request()))
            .andExpect(status().isOk).andExpect(jsonPath("$.currentStepKey").value("EXPERIENCE_STEP"))
        val payload="""{"operationId":"${UUID.randomUUID()}","expectedRevision":1,"catalogVersion":1,"domainRevisions":{"domino":0},"action":"SAVE","answers":[{"questionKey":"DOMINO_EXPERIENCE","type":"SINGLE_SELECT","optionKey":"STRATEGY"}]}"""
        mvc.perform(put("$path/steps/EXPERIENCE_STEP").header("Authorization","Bearer valid-guest").contentType("application/json").content(payload))
            .andExpect(status().isOk).andExpect(jsonPath("$.onboarding.currentStepKey").value("COACH_STEP"))
            .andExpect(jsonPath("$.domino.experienceLevel").value("STRATEGY"))
        mvc.perform(get(path).header("Authorization","Bearer valid-guest"))
            .andExpect(status().isOk).andExpect(jsonPath("$.answers[0].optionKey").value("STRATEGY"))
    }
    @Test fun `unknown identity and status fields are rejected with safe errors`() {
        val before=repo.docs.toMap()
        listOf("playerUid","playerId","status").forEach { field ->
            val body=request().dropLast(1)+",\"$field\":\"UNTRUSTED\"}"
            mvc.perform(post("$path/start").header("Authorization","Bearer valid-guest").contentType("application/json").content(body))
                .andExpect(status().isBadRequest).andExpect(jsonPath("$.code").value("REQUEST_INVALID"))
                .andExpect(jsonPath("$.requestId").isNotEmpty).andExpect(content().string(org.hamcrest.Matchers.not(org.hamcrest.Matchers.containsString("UNTRUSTED"))))
        }
        assertEquals(before,repo.docs)
    }
    @Test fun `oversize malformed and incomplete payloads do not mutate`() {
        val before=repo.docs.toMap()
        listOf("{", "{}", " ".repeat(16385), """{"operationId":"not-uuid","expectedRevision":0}""").forEach { payload ->
            mvc.perform(post("$path/start").header("Authorization","Bearer valid-guest").contentType("application/json").content(payload))
                .andExpect(status().isBadRequest).andExpect(jsonPath("$.code").value("REQUEST_INVALID"))
        }
        assertEquals(before,repo.docs)
    }
    @Test fun `missing state remains unresolved and POST cannot initialize it`() {
        repo.docs.remove("players/verified-guest/onboarding/current")
        mvc.perform(get(path).header("Authorization","Bearer valid-guest"))
            .andExpect(status().isConflict).andExpect(jsonPath("$.code").value("ONBOARDING_ROLLOUT_UNRESOLVED"))
        mvc.perform(post("$path/start").header("Authorization","Bearer valid-guest").contentType("application/json").content(request()))
            .andExpect(status().isConflict)
    }
}
