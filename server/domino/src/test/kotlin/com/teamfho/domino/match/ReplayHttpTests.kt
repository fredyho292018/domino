package com.teamfho.domino.match

import com.teamfho.domino.security.FakeAuthConfiguration
import com.teamfho.domino.player.FakePlayerFoundationConfiguration
import org.junit.jupiter.api.Test
import org.springframework.beans.factory.annotation.Autowired
import org.springframework.boot.test.context.SpringBootTest
import org.springframework.boot.test.context.TestConfiguration
import org.springframework.boot.webmvc.test.autoconfigure.AutoConfigureMockMvc
import org.springframework.context.annotation.Bean
import org.springframework.context.annotation.Import
import org.springframework.context.annotation.Primary
import org.springframework.test.context.ActiveProfiles
import org.springframework.test.web.servlet.MockMvc
import org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get
import org.springframework.test.web.servlet.result.MockMvcResultMatchers.*

@SpringBootTest
@ActiveProfiles("test")
@AutoConfigureMockMvc
@Import(FakeAuthConfiguration::class, FakePlayerFoundationConfiguration::class, ReplayHttpTests.Fixture::class)
class ReplayHttpTests {
    @Autowired lateinit var mvc: MockMvc
    @TestConfiguration(proxyBeanMethods=false)
    class Fixture {
        @Bean @Primary fun entitlementFixture():com.teamfho.domino.entitlement.EntitlementService {
            val repo=com.teamfho.domino.entitlement.MemoryEntitlements()
            val now=java.time.Instant.now()
            repo.adminGrant("verified-guest",com.teamfho.domino.entitlement.EntitlementGrant("fixture",com.teamfho.domino.entitlement.EntitlementSource.ADMIN_GRANT,
                validFrom=now.minusSeconds(60),validUntil=now.plusSeconds(3600),createdAt=now,policyVersion=1,reason="I4_PRIVACY_FIXTURE",grantedBy="test"))
            return com.teamfho.domino.entitlement.EntitlementService(com.teamfho.domino.entitlement.SubscriptionPolicyService({com.teamfho.domino.entitlement.SubscriptionPolicy()}),repo)
        }
        @Bean @Primary fun replayFixture():ReplaySource {
            val f=ReplayFixture()
            return object:ReplaySource {
                override fun match(id:String):Match? {
                    if(id=="absent")return null
                    val m=f.state.match
                    return m.copy(participants=m.participants.map{if(it.seatIndex==0&&id!="foreign")it.copy(playerUid="verified-guest")else it},
                        status=if(id=="active")MatchStatus.IN_PROGRESS else m.status)
                }
                override fun events(id:String,after:Long,limit:Int)=f.events.filter{it.sequence>after}.take(limit)
                override fun round(id:String,number:Int)=f.rounds[number]
            }
        }
    }
    @Test fun `unauthenticated replay endpoints rejected by existing Firebase security`() {
        for(path in listOf("players/me/history","matches/replay-test/replay","matches/replay-test/replay/events"))
            mvc.perform(get("/api/v1/$path")).andExpect(status().isUnauthorized)
    }
    @Test fun `verified principal receives completed private archive but cannot spoof membership`() {
        mvc.perform(get("/api/v1/matches/replay-test/replay").header("Authorization","Bearer valid-guest"))
            .andExpect(status().isOk).andExpect(jsonPath("$.replayAvailable").value(true))
            .andExpect(jsonPath("$.participants[0].playerUid").doesNotExist())
        for(id in listOf("foreign","absent"))for(suffix in listOf("replay","replay/events"))
            mvc.perform(get("/api/v1/matches/$id/$suffix?uid=p0").header("Authorization","Bearer valid-guest"))
                .andExpect(status().isForbidden).andExpect(jsonPath("$.code").value("REPLAY_FORBIDDEN"))
    }
    @Test fun `active private archive refused at both routes`() {
        for(suffix in listOf("replay","replay/events"))
            mvc.perform(get("/api/v1/matches/active/$suffix").header("Authorization","Bearer valid-guest"))
                .andExpect(status().isConflict).andExpect(jsonPath("$.code").value("ACTIVE_MATCH"))
    }
}
