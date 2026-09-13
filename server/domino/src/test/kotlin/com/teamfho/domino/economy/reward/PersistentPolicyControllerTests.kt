package com.teamfho.domino.economy.reward

import com.teamfho.domino.security.FakeAuthConfiguration
import com.teamfho.domino.player.FakePlayerFoundationConfiguration
import org.junit.jupiter.api.Test
import org.mockito.Mockito.*
import org.springframework.beans.factory.annotation.Autowired
import org.springframework.boot.test.context.SpringBootTest
import org.springframework.boot.webmvc.test.autoconfigure.AutoConfigureMockMvc
import org.springframework.context.annotation.Import
import org.springframework.test.context.ActiveProfiles
import org.springframework.test.context.bean.override.mockito.MockitoBean
import org.springframework.test.web.servlet.MockMvc
import org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get
import org.springframework.test.web.servlet.result.MockMvcResultMatchers.*

@SpringBootTest
@ActiveProfiles("test")
@AutoConfigureMockMvc
@Import(FakeAuthConfiguration::class,FakePlayerFoundationConfiguration::class)
class PersistentPolicyControllerTests {
    @Autowired lateinit var mvc: MockMvc
    @Autowired lateinit var service: MonetizationPolicyService
    @MockitoBean lateinit var policyRepository: MonetizationPolicyRepository
    @Test fun `authenticated config exposes refreshed persisted DTO without restarting Spring`() {
        `when`(policyRepository.read()).thenReturn(PolicyRead.NotFound);service.invalidate()
        val path="/api/v1/monetization/config"
        mvc.perform(get(path)).andExpect(status().isUnauthorized)
        mvc.perform(get(path).header("Authorization","Bearer valid-guest"))
            .andExpect(status().isOk).andExpect(jsonPath("$.rewarded.rewardCoins").value(10))
        val policy=MonetizationPolicy(2,true,RewardedRules(coins=15,cooldownSeconds=180,maxPerHour=4,maxPerDay=12,maxCoinsPerDay=180))
        `when`(policyRepository.read()).thenReturn(PolicyRead.Active(policy));service.invalidate()
        mvc.perform(get(path).header("Authorization","Bearer valid-guest"))
            .andExpect(status().isOk).andExpect(jsonPath("$.version").value(2))
            .andExpect(jsonPath("$.rewarded.rewardCoins").value(15))
            .andExpect(jsonPath("$.rewarded.limits.cooldownSeconds").value(180))
            .andExpect(jsonPath("$.rewarded.limits.perHour").value(4))
            .andExpect(jsonPath("$.rewarded.limits.perDay").value(12))
            .andExpect(jsonPath("$.rewarded.limits.maxCoinsPerDay").value(180))
        `when`(policyRepository.read()).thenReturn(PolicyRead.Active(policy.copy(version=3,rewarded=policy.rewarded.copy(enabled=false))))
        service.invalidate()
        mvc.perform(get(path).header("Authorization","Bearer valid-guest"))
            .andExpect(status().isOk).andExpect(jsonPath("$.rewarded.enabled").value(false))
    }
}
