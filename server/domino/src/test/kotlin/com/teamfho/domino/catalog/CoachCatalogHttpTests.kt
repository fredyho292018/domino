package com.teamfho.domino.catalog

import com.teamfho.domino.player.*
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
@Import(FakeAuthConfiguration::class,FakePlayerFoundationConfiguration::class,CoachCatalogHttpTests.Fixture::class)
class CoachCatalogHttpTests {
    @TestConfiguration class Fixture {
        @Bean @Primary fun coaches():CoachCatalogRepository=MemoryCoachCatalog().also{CoachCatalogSeed.run(it)}
        @Bean @Primary fun coachAccess()=OnboardingCatalogAccess{}
        @Bean @Primary fun coachProgress()=ProgressMemory()
    }
    @Autowired lateinit var mvc:MockMvc
    @Autowired lateinit var progress:ProgressMemory
    @BeforeEach fun setup(){
        progress.docs.clear();progress.initialize("verified-guest")
        progress.docs["coachCatalogs/1"]=GameCatalogCodec.map(CoachCatalogSeed.canonical())
        progress.docs["onboardingCatalogs/2"]=GameCatalogCodec.map(CoachCatalogSeed.compatibleOnboarding())
        progress.docs["systemConfig/onboardingCatalog"]=mapOf("publishedVersion" to 2)
    }
    @Test fun `catalog authentication required`() {mvc.perform(get("/api/v1/coaches")).andExpect(status().isUnauthorized)}
    @Test fun `localized safe DTO and cache contract`() {
        val response=mvc.perform(get("/api/v1/coaches?locale=es-US").header("Authorization","Bearer valid-guest"))
            .andExpect(status().isOk).andExpect(jsonPath("$.items.length()").value(10)).andExpect(jsonPath("$.resolvedLocale").value("es"))
            .andExpect(jsonPath("$.items[0].key").value("LUCIA")).andExpect(jsonPath("$.items[0].name").value("Lucía"))
            .andExpect(jsonPath("$.items[0].avatar.assetVersion").value(1)).andExpect(jsonPath("$.publishedAt").doesNotExist())
            .andExpect(header().string("Cache-Control","private, max-age=300")).andReturn().response
        mvc.perform(get("/api/v1/coaches?locale=es-US").header("Authorization","Bearer valid-guest").header("If-None-Match",response.getHeader("ETag")!!))
            .andExpect(status().isNotModified)
        mvc.perform(get("/api/v1/coaches?locale=en-US&version=1").header("Authorization","Bearer valid-guest"))
            .andExpect(status().isOk).andExpect(jsonPath("$.resolvedLocale").value("en"))
    }
    @Test fun `missing version and denied player are safe`() {
        mvc.perform(get("/api/v1/coaches?version=999").header("Authorization","Bearer valid-guest"))
            .andExpect(status().isNotFound).andExpect(jsonPath("$.code").value("COACH_CATALOG_NOT_FOUND"))
        val controller=CoachCatalogController(CoachCatalogService(MemoryCoachCatalog()),OnboardingCatalogAccess{throw OnboardingCatalogFailure(403,"PLAYER_NOT_ACTIVE")})
        assertEquals(403,controller.catalog(com.teamfho.domino.security.FirebaseIdentity("fixture",true),"en",null,org.springframework.mock.web.MockHttpServletRequest()).statusCode.value())
    }
    @Test fun `wired production validator permits coach selection via authenticated request only`() {
        val path="/api/v1/player/onboarding"
        mvc.perform(post("$path/start").header("Authorization","Bearer valid-guest").contentType("application/json")
            .content(GameCatalogCodec.json(OnboardingStartRequest(UUID.randomUUID().toString(),0)))).andExpect(status().isOk)
        fun save(step:String,revision:Long,domain:Long,question:String,type:OnboardingQuestionType,key:String,extra:Boolean=false)=mvc.perform(
            put("$path/steps/$step").header("Authorization","Bearer valid-guest").contentType("application/json").content(
                GameCatalogCodec.json(SaveStepRequest(UUID.randomUUID().toString(),revision,2,mapOf("domino" to domain),OnboardingStepAction.SAVE,listOf(OnboardingAnswer(question,type,key)))).let{
                    if(extra)it.dropLast(1)+",\"playerId\":\"another-fixture\"}" else it}))
        save("EXPERIENCE_STEP",1,0,"DOMINO_EXPERIENCE",OnboardingQuestionType.SINGLE_SELECT,"BEGINNER").andExpect(status().isOk)
        save("COACH_STEP",2,1,"COACH_SELECTION",OnboardingQuestionType.COACH_SELECT,"LUCIA",true).andExpect(status().isBadRequest)
        save("COACH_STEP",2,1,"COACH_SELECTION",OnboardingQuestionType.COACH_SELECT,"LUCIA").andExpect(status().isOk)
            .andExpect(jsonPath("$.domino.preferredCoachKey").value("LUCIA")).andExpect(jsonPath("$.onboarding.currentStepKey").value("CONTACTS_STEP"))
        mvc.perform(post("$path/complete").header("Authorization","Bearer valid-guest").contentType("application/json")
            .content(GameCatalogCodec.json(OnboardingCompleteRequest(UUID.randomUUID().toString(),3,2))))
            .andExpect(status().isOk).andExpect(jsonPath("$.status").value("COMPLETED"))
        assertTrue(progress.docs.keys.none{it.startsWith("players/another-fixture")})
    }
}
