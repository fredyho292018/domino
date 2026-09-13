package com.teamfho.domino.economy.reward

import com.teamfho.domino.security.FakeAuthConfiguration
import com.teamfho.domino.player.FakePlayerFoundationConfiguration
import org.junit.jupiter.api.BeforeEach
import org.junit.jupiter.api.Test
import org.junit.jupiter.params.ParameterizedTest
import org.junit.jupiter.params.provider.ValueSource
import org.mockito.Mockito.*
import org.springframework.beans.factory.annotation.Autowired
import org.springframework.boot.test.context.SpringBootTest
import org.springframework.boot.webmvc.test.autoconfigure.AutoConfigureMockMvc
import org.springframework.context.annotation.Import
import org.springframework.test.context.ActiveProfiles
import org.springframework.test.context.bean.override.mockito.MockitoBean
import org.springframework.test.web.servlet.MockMvc
import org.springframework.test.web.servlet.request.MockMvcRequestBuilders.*
import org.springframework.test.web.servlet.result.MockMvcResultMatchers.*
import java.time.Instant

@SpringBootTest
@ActiveProfiles("test")
@AutoConfigureMockMvc
@Import(FakeAuthConfiguration::class, FakePlayerFoundationConfiguration::class)
class RewardControllerTests {
    @Autowired lateinit var mvc: MockMvc
    @MockitoBean lateinit var repository: RewardIntentRepository
    @MockitoBean lateinit var verifier: AdMobSsvVerifier
    private val id = "12345678-1234-4234-8234-123456789012"
    private val path = "/api/v1/economy/ad-rewards/intents"
    @Test fun `H6 endpoints require authentication`() {
        mvc.perform(get("/api/v1/monetization/config")).andExpect(status().isUnauthorized)
        mvc.perform(get("/api/v1/economy/ad-rewards/eligibility")).andExpect(status().isUnauthorized)
        mvc.perform(post("/api/v1/economy/ad-rewards/opportunities").contentType("application/json").content("{}"))
            .andExpect(status().isUnauthorized)
        verifyNoInteractions(repository)
    }
    @Test fun `H6 policy is typed and eligibility uses principal`() {
        mvc.perform(get("/api/v1/monetization/config").header("Authorization","Bearer valid-guest"))
            .andExpect(status().isOk).andExpect(jsonPath("$.rewarded.rewardCoins").value(10))
            .andExpect(jsonPath("$.rewarded.limits.cooldownSeconds").value(120))
            .andExpect(jsonPath("$.rewarded.limits.perHour").value(5))
        `when`(repository.eligibility("verified-guest",null)).thenReturn(
            RewardEligibilityPolicy.evaluate(MonetizationPolicy(),emptyList(),Instant.now()))
        mvc.perform(get("/api/v1/economy/ad-rewards/eligibility").header("Authorization","Bearer valid-guest"))
            .andExpect(status().isOk).andExpect(jsonPath("$.eligible").value(true))
            .andExpect(jsonPath("$.remaining.day").value(20)).andExpect(jsonPath("$.serverTime").exists())
        verify(repository).eligibility("verified-guest",null)
    }
    @Test fun `H6 opportunity rejects client economic fields`() {
        for(field in listOf("uid","roundId","amount","coins","ledgerId")) {
            mvc.perform(post("/api/v1/economy/ad-rewards/opportunities").header("Authorization","Bearer valid-guest")
                .contentType("application/json").content("{\"$field\":\"fake\"}"))
                .andExpect(status().isBadRequest)
        }
        verifyNoInteractions(repository)
    }
    @BeforeEach fun setup() {
        `when`(repository.issue("verified-guest")).thenReturn(RewardIntent(id, "verified-guest", RewardIntentStatus.ISSUED,
            Instant.now(), Instant.now().plusSeconds(600), AdUnitEnvironment.DEVELOPMENT))
    }
    @Test fun `principal create and no privileged fields in response`() {
        mvc.perform(post(path).header("Authorization", "Bearer valid-guest").contentType("application/json").content("{}"))
            .andExpect(status().isOk).andExpect(jsonPath("$.intentId").value(id))
            .andExpect(jsonPath("$.reward.previewAmount").value(10)).andExpect(jsonPath("$.uid").doesNotExist())
        verify(repository).issue("verified-guest")
    }
    @Test fun `auth required for create and status`() {
        mvc.perform(post(path).contentType("application/json").content("{}")).andExpect(status().isUnauthorized)
        mvc.perform(get("$path/$id")).andExpect(status().isUnauthorized)
        verifyNoInteractions(repository)
    }
    @ParameterizedTest @ValueSource(strings = ["uid", "coins", "rewardAmount", "walletBalance", "transactionId", "accountType"])
    fun `unknown request fields rejected`(field: String) {
        mvc.perform(post(path).header("Authorization", "Bearer valid-guest").contentType("application/json").content("{\"$field\":10}"))
            .andExpect(status().isBadRequest)
        verifyNoInteractions(repository)
    }
    @Test fun `owner only lookup`() {
        `when`(repository.status("verified-guest", id)).thenThrow(RewardFailure("SSV_INTENT_NOT_FOUND", 404))
        mvc.perform(get("$path/$id").header("Authorization", "Bearer valid-guest")).andExpect(status().isNotFound)
        verify(repository).status("verified-guest", id)
    }
    @Test fun `only GET SSV public signature before repository and retries acknowledged`() {
        val query = "test-signed-fixture"
        val event = VerifiedAdMobEvent(id, "aabb", "5224354917", Instant.now())
        `when`(verifier.verify(query)).thenReturn(event)
        `when`(repository.verify(event)).thenReturn(true, false)
        repeat(2) {
            mvc.perform(get("/api/v1/admob/rewarded/ssv").with { it.queryString = query; it }).andExpect(status().isOk)
        }
        val order = inOrder(verifier, repository)
        order.verify(verifier).verify(query); order.verify(repository).verify(event)
        mvc.perform(post("/api/v1/admob/rewarded/ssv")).andExpect(status().isUnauthorized)
    }
    @Test fun `invalid signature no repository call`() {
        `when`(verifier.verify(null)).thenThrow(RewardFailure("SSV_SIGNATURE_INVALID"))
        mvc.perform(get("/api/v1/admob/rewarded/ssv")).andExpect(status().isBadRequest)
        verifyNoInteractions(repository)
    }
    @Test fun `unavailable key safe retry response`() {
        `when`(verifier.verify(null)).thenThrow(RewardFailure("SSV_DEPENDENCY_UNAVAILABLE", 503))
        mvc.perform(get("/api/v1/admob/rewarded/ssv")).andExpect(status().isServiceUnavailable)
        verifyNoInteractions(repository)
    }
    @Test fun `consume authentication and authoritative response`() {
        mvc.perform(post("$path/$id/consume").contentType("application/json").content("{}")).andExpect(status().isUnauthorized)
        `when`(repository.consume("verified-guest", id)).thenReturn(RewardConsumeResponse(RewardCredit(amount = 10),
            RewardWallet(10), ConsumedIntent(id)))
        mvc.perform(post("$path/$id/consume").header("Authorization", "Bearer valid-guest").contentType("application/json").content("{}"))
            .andExpect(status().isOk).andExpect(jsonPath("$.wallet.coins").value(10))
            .andExpect(jsonPath("$.intent.status").value("CONSUMED"))
        verify(repository).consume("verified-guest", id)
    }
    @ParameterizedTest @ValueSource(strings = ["uid", "coins", "amount", "transactionId", "walletBalance", "lifetimeEarned", "rewardAmount"])
    fun `consume never accepts client economy authority`(field: String) {
        mvc.perform(post("$path/$id/consume").header("Authorization", "Bearer valid-guest").contentType("application/json").content("{\"$field\":10}"))
            .andExpect(status().isBadRequest)
        verifyNoInteractions(repository)
    }
    @Test fun `pending lookup requires auth and uses owner only`() {
        mvc.perform(get("/api/v1/economy/ad-rewards/pending")).andExpect(status().isUnauthorized)
        mvc.perform(get("/api/v1/economy/ad-rewards/pending").header("Authorization", "Bearer valid-guest"))
            .andExpect(status().isOk).andExpect(jsonPath("$.intent").isEmpty)
        verify(repository).pending("verified-guest")
    }
}
