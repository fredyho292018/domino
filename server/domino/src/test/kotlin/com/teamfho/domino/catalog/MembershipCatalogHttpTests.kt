package com.teamfho.domino.catalog

import com.teamfho.domino.player.FakePlayerFoundationConfiguration
import com.teamfho.domino.security.FakeAuthConfiguration
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
import kotlin.test.*

@SpringBootTest
@ActiveProfiles("test")
@AutoConfigureMockMvc
@Import(FakeAuthConfiguration::class,FakePlayerFoundationConfiguration::class,MembershipCatalogHttpTests.Fixture::class)
class MembershipCatalogHttpTests {
    @TestConfiguration class Fixture {
        @Bean @Primary fun membershipFixture():MembershipCatalogRepository=MemoryMembershipCatalog().also{MembershipCatalogSeed.run(it)}
        @Bean @Primary fun membershipAccess()=OnboardingCatalogAccess{}
    }
    @Autowired lateinit var mvc:MockMvc
    @Test fun `authentication required`() {mvc.perform(get("/api/v1/membership/catalog")).andExpect(status().isUnauthorized)}
    @Test fun `localized explicit DTO contains no player persistence or target internals`() {
        val response=mvc.perform(get("/api/v1/membership/catalog?locale=es-US").header("Authorization","Bearer valid-guest"))
            .andExpect(status().isOk).andExpect(jsonPath("$.resolvedLocale").value("es"))
            .andExpect(jsonPath("$.plans.length()").value(5)).andExpect(jsonPath("$.features.length()").value(8))
            .andExpect(jsonPath("$.plans[0].key").value("DIAMOND"))
            .andExpect(jsonPath("$.trialPresentation.product").value("PREMIUM_LEGACY"))
            .andExpect(jsonPath("$.trialPresentation.familyTrialEnabled").value(false))
            .andExpect(jsonPath("$.targetPolicy").doesNotExist()).andExpect(jsonPath("$.publishedAt").doesNotExist())
            .andExpect(header().string("Cache-Control","private, max-age=300")).andReturn().response
        val json=response.contentAsString
        for(forbidden in listOf("uid","email","phone","receipt","grantedBy","exemption","isTestAccount","purchaseToken"))
            assertFalse(json.contains("\"$forbidden\""),forbidden)
        mvc.perform(get("/api/v1/membership/catalog?locale=es-US").header("Authorization","Bearer valid-guest").header("If-None-Match",response.getHeader("ETag")!!)).andExpect(status().isNotModified)
    }
    @Test fun `historical english contract`() {mvc.perform(get("/api/v1/membership/catalog?locale=en-US&version=1").header("Authorization","Bearer valid-guest")).andExpect(status().isOk).andExpect(jsonPath("$.resolvedLocale").value("en")).andExpect(header().string("Cache-Control","private, max-age=86400"))}
    @Test fun `invalid and absent versions`() {
        mvc.perform(get("/api/v1/membership/catalog?version=999").header("Authorization","Bearer valid-guest")).andExpect(status().isNotFound).andExpect(jsonPath("$.code").value("MEMBERSHIP_CATALOG_NOT_FOUND"))
        mvc.perform(get("/api/v1/membership/catalog?version=0").header("Authorization","Bearer valid-guest")).andExpect(status().isBadRequest)
        mvc.perform(get("/api/v1/membership/catalog?version=no").header("Authorization","Bearer valid-guest")).andExpect(status().isBadRequest)
    }
    @Test fun `inactive player and storage failures remain safe`() {
        val request=org.springframework.mock.web.MockHttpServletRequest();val id=com.teamfho.domino.security.FirebaseIdentity("fixture",true)
        val denied=MembershipCatalogController(MembershipCatalogService(MemoryMembershipCatalog()),OnboardingCatalogAccess{throw OnboardingCatalogFailure(403,"PLAYER_NOT_ACTIVE")})
        assertEquals(403,denied.catalog(id,null,null,request).statusCode.value())
        val unavailable=MembershipCatalogController(MembershipCatalogService(object:MembershipCatalogRepository{
            override fun currentVersion():Int?=error("private diagnostic")
            override fun read(version:Int):MembershipCatalogPublication?=error("private diagnostic")
            override fun publish(publication:MembershipCatalogPublication)=error("unexpected write")
        }),OnboardingCatalogAccess{})
        val response=unavailable.catalog(id,null,null,request);assertEquals(503,response.statusCode.value());assertFalse(response.body.toString().contains("private diagnostic"))
    }
}
