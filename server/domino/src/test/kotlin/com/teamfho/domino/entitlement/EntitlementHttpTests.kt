package com.teamfho.domino.entitlement

import com.teamfho.domino.player.FakePlayerFoundationConfiguration
import com.teamfho.domino.security.FakeAuthConfiguration
import org.junit.jupiter.api.Test
import org.springframework.beans.factory.annotation.Autowired
import org.springframework.boot.test.context.SpringBootTest
import org.springframework.boot.test.context.TestConfiguration
import org.springframework.boot.webmvc.test.autoconfigure.AutoConfigureMockMvc
import org.springframework.context.annotation.*
import org.springframework.test.context.ActiveProfiles
import org.springframework.test.web.servlet.MockMvc
import org.springframework.test.web.servlet.request.MockMvcRequestBuilders.*
import org.springframework.test.web.servlet.result.MockMvcResultMatchers.*

@SpringBootTest
@ActiveProfiles("test")
@AutoConfigureMockMvc
@Import(FakeAuthConfiguration::class,FakePlayerFoundationConfiguration::class,EntitlementHttpTests.Fixture::class)
class EntitlementHttpTests {
    @Autowired lateinit var mvc:MockMvc
    @TestConfiguration(proxyBeanMethods=false) class Fixture {
        @Bean @Primary fun testEntitlements()=EntitlementService(SubscriptionPolicyService({SubscriptionPolicy()}),MemoryEntitlements())
    }
    @Test fun `bootstrap issues anonymous trial and response exposes capabilities but no grant internals`() {
        mvc.perform(post("/api/v1/player/bootstrap").header("Authorization","Bearer valid-guest").contentType("application/json").content("{}"))
            .andExpect(status().isOk).andExpect(jsonPath("$.entitlements.snapshot.plan").value("PREMIUM"))
            .andExpect(jsonPath("$.entitlements.snapshot.trialActive").value(true))
            .andExpect(jsonPath("$.entitlements.snapshot.limits.FRIENDS_MAX.maximum").value(100))
            .andExpect(jsonPath("$.entitlements.snapshot.grants").doesNotExist())
        mvc.perform(get("/api/v1/player/entitlements?uid=victim").header("Authorization","Bearer valid-guest"))
            .andExpect(status().isOk).andExpect(jsonPath("$.snapshot.plan").value("PREMIUM"))
            .andExpect(jsonPath("$.snapshot.grantedBy").doesNotExist())
    }
    @Test fun `identity required profile and entitlement routes cannot write client premium`() {
        for(path in listOf("profile","entitlements"))mvc.perform(get("/api/v1/player/$path")).andExpect(status().isUnauthorized)
        for(field in listOf("plan","trial","validUntil","features"))mvc.perform(post("/api/v1/player/bootstrap")
            .header("Authorization","Bearer valid-guest").contentType("application/json").content("""{"$field":"PREMIUM"}"""))
            .andExpect(status().isBadRequest)
        mvc.perform(put("/api/v1/player/entitlements").header("Authorization","Bearer valid-guest")
            .contentType("application/json").content("""{"plan":"PREMIUM"}"""))
            .andExpect(status().is4xxClientError)
    }
}
