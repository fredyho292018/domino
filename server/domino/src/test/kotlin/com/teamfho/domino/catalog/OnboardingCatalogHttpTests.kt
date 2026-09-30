package com.teamfho.domino.catalog

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
import org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get
import org.springframework.test.web.servlet.result.MockMvcResultMatchers.*
import com.teamfho.domino.security.FakeAuthConfiguration
import kotlin.test.*

@SpringBootTest
@ActiveProfiles("test")
@AutoConfigureMockMvc
@Import(FakeAuthConfiguration::class,OnboardingCatalogHttpTests.Fixture::class,
    com.teamfho.domino.player.FakePlayerFoundationConfiguration::class)
class OnboardingCatalogHttpTests {
    @TestConfiguration class Fixture {
        @Bean @Primary fun catalogRepo(): OnboardingCatalogRepository = MemoryOnboardingCatalog().also{OnboardingCatalogSeed.run(it)}
        @Bean @Primary fun catalogAccess() = OnboardingCatalogAccess { }
    }
    @Autowired lateinit var mvc: MockMvc
    @Test fun `authentication required`() { mvc.perform(get("/api/v1/onboarding/catalog")).andExpect(status().isUnauthorized) }
    @Test fun `explicit localized JSON contract cache and conditional response`() {
        val response=mvc.perform(get("/api/v1/onboarding/catalog?locale=es-US").header("Authorization","Bearer valid-guest"))
            .andExpect(status().isOk).andExpect(jsonPath("$.catalogVersion").value(1))
            .andExpect(jsonPath("$.locale").value("es")).andExpect(jsonPath("$.steps.length()").value(4))
            .andExpect(jsonPath("$.steps[0].questions[0].key").value("DOMINO_EXPERIENCE"))
            .andExpect(jsonPath("$.steps[0].questions[0].options[0].title").value("No sé jugar"))
            .andExpect(header().string("Cache-Control","private, max-age=300")).andReturn().response
        mvc.perform(get("/api/v1/onboarding/catalog?locale=es&version=1").header("Authorization","Bearer valid-guest").header("If-None-Match",requireNotNull(response.getHeader("ETag"))))
            .andExpect(status().isNotModified).andExpect(header().string("Cache-Control","private, max-age=86400"))
        mvc.perform(get("/api/v1/onboarding/catalog?locale=en").header("Authorization","Bearer valid-guest").header("If-None-Match",requireNotNull(response.getHeader("ETag"))))
            .andExpect(status().isOk).andExpect(jsonPath("$.locale").value("en"))
        val json=response.contentAsString
        listOf("uid","email","token","publishedAt","binding").forEach{assertFalse(json.contains("\"$it\""))}
    }
    @Test fun `missing version returns safe error contract`() {
        mvc.perform(get("/api/v1/onboarding/catalog?version=999").header("Authorization","Bearer valid-guest"))
            .andExpect(status().isNotFound).andExpect(jsonPath("$.code").value("ONBOARDING_CATALOG_NOT_FOUND"))
            .andExpect(jsonPath("$.requestId").isNotEmpty).andExpect(header().string("Cache-Control","no-store"))
    }
    @Test fun `access denial is enforced before catalog resolution`() {
        val repository=MemoryOnboardingCatalog()
        val controller=OnboardingCatalogController(OnboardingCatalogService(repository),OnboardingCatalogAccess {throw OnboardingCatalogFailure(403,"PLAYER_NOT_ACTIVE")})
        val result=controller.catalog(com.teamfho.domino.security.FirebaseIdentity("fixture",true),"en",null,org.springframework.mock.web.MockHttpServletRequest())
        assertEquals(403,result.statusCode.value());assertEquals(0,repository.reads);assertEquals(0,repository.writes)
    }
}

