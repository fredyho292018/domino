package com.teamfho.domino.player

import com.teamfho.domino.catalog.*
import com.teamfho.domino.security.FakeAuthConfiguration
import org.junit.jupiter.api.Test
import org.junit.jupiter.api.BeforeEach
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
@Import(FakeAuthConfiguration::class,FakePlayerFoundationConfiguration::class,BasicProfileHttpTests.Fixture::class)
class BasicProfileHttpTests {
    @TestConfiguration class Fixture { @Bean @Primary fun basicProgress()=ProgressMemory() }
    @Autowired lateinit var mvc:MockMvc
    @Autowired lateinit var repo:ProgressMemory
    @BeforeEach fun setup(){
        repo.docs.clear();repo.initialize("verified-guest")
        repo.docs["systemConfig/onboardingCatalog"]=mapOf("publishedVersion" to 2)
        repo.docs["onboardingCatalogs/2"]=GameCatalogCodec.map(OnboardingCatalogV2.canonical())
    }
    private val path="/api/v1/player/onboarding"
    private fun start(){mvc.perform(post("$path/start").header("Authorization","Bearer valid-guest").contentType("application/json")
        .content(GameCatalogCodec.json(OnboardingStartRequest(UUID.randomUUID().toString(),0))))
        .andExpect(status().isOk).andExpect(jsonPath("$.currentStepKey").value("BASIC_PROFILE_STEP"))}
    private fun payload()="""{"operationId":"${UUID.randomUUID()}","expectedRevision":1,"catalogVersion":2,"domainRevisions":{"profile":0,"preferences":0},"action":"SAVE","answers":[{"questionKey":"FIRST_NAME","type":"TEXT","textValue":"Test"},{"questionKey":"LAST_NAME","type":"TEXT","textValue":"Player"},{"questionKey":"DISPLAY_NAME","type":"TEXT","textValue":"TestPlayer"},{"questionKey":"COUNTRY","type":"COUNTRY_SELECT","optionKey":"US"},{"questionKey":"PREFERRED_LANGUAGE","type":"LOCALE_SELECT","optionKey":"es"}],"detectedTimeZone":"America/Chicago"}"""
    @Test fun `HTTP typed profile save and authenticated own prefill`() {
        start()
        mvc.perform(put("$path/steps/BASIC_PROFILE_STEP").header("Authorization","Bearer valid-guest").contentType("application/json").content(payload()))
            .andExpect(status().isOk).andExpect(jsonPath("$.onboarding.currentStepKey").value("EXPERIENCE_STEP"))
            .andExpect(jsonPath("$.onboarding.basicProfile.firstName").value("Test"))
            .andExpect(jsonPath("$.onboarding.basicProfile.preferredLocale").value("es"))
        mvc.perform(get(path).header("Authorization","Bearer valid-guest"))
            .andExpect(status().isOk).andExpect(jsonPath("$.basicProfile.timeZone").value("America/Chicago"))
            .andExpect(jsonPath("$.uid").doesNotExist()).andExpect(header().string("Cache-Control","no-store"))
        mvc.perform(get(path)).andExpect(status().isUnauthorized)
    }
    @Test fun `identity field and numeric text cannot mutate another player`() {
        start();val before=repo.docs.toMap()
        val invalid=listOf(payload().dropLast(1)+",\"playerId\":\"fixture-other\"}",payload().replace("\"textValue\":\"Test\"","\"textValue\":42"))
        invalid.forEach{p->mvc.perform(put("$path/steps/BASIC_PROFILE_STEP").header("Authorization","Bearer valid-guest").contentType("application/json").content(p))
            .andExpect(status().isBadRequest)}
        assertEquals(before,repo.docs)
    }
}
