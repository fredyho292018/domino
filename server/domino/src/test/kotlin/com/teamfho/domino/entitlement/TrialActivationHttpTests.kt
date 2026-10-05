package com.teamfho.domino.entitlement

import com.teamfho.domino.player.*
import com.teamfho.domino.security.FakeAuthConfiguration
import com.teamfho.domino.match.MatchCodec
import org.junit.jupiter.api.Test
import org.junit.jupiter.api.BeforeEach
import org.springframework.beans.factory.annotation.Autowired
import org.springframework.boot.test.context.SpringBootTest
import org.springframework.boot.test.context.TestConfiguration
import org.springframework.boot.webmvc.test.autoconfigure.AutoConfigureMockMvc
import org.springframework.context.annotation.*
import org.springframework.test.context.ActiveProfiles
import org.springframework.test.web.servlet.MockMvc
import org.springframework.test.web.servlet.request.MockMvcRequestBuilders.*
import org.springframework.test.web.servlet.result.MockMvcResultMatchers.*
import java.util.UUID
import kotlin.test.*

@SpringBootTest(properties=["domino.trial.require-compatible-client=true"])
@ActiveProfiles("test")
@AutoConfigureMockMvc
@Import(FakeAuthConfiguration::class,FakePlayerFoundationConfiguration::class,TrialActivationHttpTests.Fixture::class)
class TrialActivationHttpTests {
    @TestConfiguration class Fixture {
        @Bean @Primary fun trialMemory()=TrialMemory()
        @Bean @Primary fun trialPolicies()=SubscriptionPolicyService({SubscriptionPolicy()})
        @Bean @Primary fun trialEntitlements(db:TrialMemory):EntitlementRepository=object:EntitlementRepository {
            override fun read(uid:String)=db.docs["players/$uid/entitlementState/current"]?.let{MatchCodec.read(it,EntitlementState::class.java)} ?: EntitlementState()
            override fun adminGrant(uid:String,grant:EntitlementGrant):EntitlementState=error("not part of HTTP fixture")
        }
    }
    @Autowired lateinit var mvc:MockMvc
    @Autowired lateinit var db:TrialMemory
    @Autowired lateinit var entitlements:EntitlementService
    @BeforeEach fun setup(){db.docs.clear();db.catalog();db.docs["players/verified-guest"]=mapOf("status" to "ACTIVE","accountType" to "GUEST");entitlements.invalidate("verified-guest")}
    private fun payload()="""{"operationId":"${UUID.randomUUID()}","expectedPolicyVersion":1,"plan":"DIAMOND","billingPeriod":"YEARLY"}"""
    private fun activate(body:String)=mvc.perform(post("/api/v1/player/trial/activate").header("Authorization","Bearer valid-guest")
        .header("X-Trial-Activation-Contract","1").contentType("application/json").content(body))
    @Test fun `unauthenticated activation rejected`() {mvc.perform(post("/api/v1/player/trial/activate").contentType("application/json").content(payload())).andExpect(status().isUnauthorized)}
    @Test fun `old clients blocked on both paths before writes`() {
        val before=db.docs.toMap()
        for(path in listOf("bootstrap","trial/activate"))mvc.perform(post("/api/v1/player/$path").header("Authorization","Bearer valid-guest").contentType("application/json").content(payload()))
            .andExpect(status().isConflict).andExpect(jsonPath("$.code").value("CLIENT_UPDATE_REQUIRED"))
        assertEquals(before,db.docs)
    }
    @Test fun `compatible bootstrap advertises explicit and never grants`() {
        repeat(2){mvc.perform(post("/api/v1/player/bootstrap").header("Authorization","Bearer valid-guest").header("X-Trial-Activation-Contract","1").contentType("application/json").content("{}"))
            .andExpect(status().isOk).andExpect(jsonPath("$.entitlements.trialGranted").value(false))
            .andExpect(jsonPath("$.entitlements.snapshot.plan").value("FREE"))
            .andExpect(jsonPath("$.capabilities.trialActivationMode").value("EXPLICIT"))
            .andExpect(jsonPath("$.trialEligibility.eligible").value(true))}
        assertEquals(setOf("players/verified-guest","systemConfig/membershipCatalog","membershipCatalogs/1"),db.docs.keys)
    }
    @Test fun `activation response receipt and current entitlement refresh`() {
        val body=payload();val first=activate(body).andExpect(status().isOk).andExpect(jsonPath("$.outcome").value("ACTIVATED"))
            .andExpect(jsonPath("$.entitlements.snapshot.plan").value("PREMIUM")).andExpect(header().string("Cache-Control","no-store")).andReturn().response.contentAsString
        val second=activate(body).andExpect(status().isOk).andReturn().response.contentAsString;assertEquals(first,second)
        assertFalse(first.contains("verified-guest"));assertFalse(first.contains("grantedBy"))
        mvc.perform(get("/api/v1/player/entitlements").header("Authorization","Bearer valid-guest")).andExpect(status().isOk).andExpect(jsonPath("$.snapshot.plan").value("PREMIUM"))
    }
    @Test fun `extra fields coercions and oversized input rejected without writes`() {
        val before=db.docs.toMap()
        for(field in listOf("uid","playerId","duration","trialStartedAt","trialEndsAt","reminderAt","firstChargeAt","features","trialConsumed")) {
            activate(payload().dropLast(1)+",\"$field\":\"untrusted\"}").andExpect(status().isBadRequest)
        }
        activate(payload().replace(":1",":\"1\"")).andExpect(status().isBadRequest)
        activate(payload().replace(":1",":1.5")).andExpect(status().isBadRequest)
        activate(" ".repeat(16385)).andExpect(status().isBadRequest)
        assertEquals(before,db.docs)
    }
    @Test fun `missing player and stale policy typed errors`() {
        activate(payload().replace(":1",":2")).andExpect(status().isConflict).andExpect(jsonPath("$.code").value("TRIAL_POLICY_VERSION_MISMATCH"))
        db.docs.clear();activate(payload()).andExpect(status().isForbidden).andExpect(jsonPath("$.code").value("TRIAL_NOT_ELIGIBLE"))
    }
}
