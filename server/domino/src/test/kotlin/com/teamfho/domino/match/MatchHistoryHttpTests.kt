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
import java.time.Instant
import kotlin.test.assertFalse
import kotlin.test.assertTrue
import org.springframework.beans.factory.annotation.Qualifier
import org.springframework.web.servlet.mvc.method.annotation.RequestMappingHandlerMapping

@SpringBootTest
@ActiveProfiles("test")
@AutoConfigureMockMvc
@Import(FakeAuthConfiguration::class,FakePlayerFoundationConfiguration::class,MatchHistoryHttpTests.HistoryFixture::class)
class MatchHistoryHttpTests {
    @Autowired lateinit var mvc: MockMvc
    @Autowired @Qualifier("requestMappingHandlerMapping") lateinit var mappings: RequestMappingHandlerMapping
    @TestConfiguration(proxyBeanMethods=false)
    class HistoryFixture {
        @Bean @Primary fun testMatchHistory(): PlayerMatchHistoryRepository = object: PlayerMatchHistoryRepository {
            override fun history(uid: String,limit: Int,beforeMatchId: String?): HistoryPage = HistoryPage(listOf(
                PlayerMatchHistory("history-for-$uid","DUEL_1V1","double-nine-duel",1,HistoryResult.WIN,listOf(150,27),
                    listOf(OpponentSummary(1,"Old alias",null)),Instant.EPOCH,Instant.EPOCH,MatchFinishReason.TARGET_REACHED)),null)
        }
    }
    @Test fun `unauthenticated history is rejected`() {
        mvc.perform(get("/api/v1/players/me/matches")).andExpect(status().isUnauthorized)
    }
    @Test fun `history principal cannot be replaced by query uid`() {
        mvc.perform(get("/api/v1/players/me/matches?uid=victim&limit=2").header("Authorization","Bearer valid-guest"))
            .andExpect(status().isOk).andExpect(jsonPath("$.items[0].matchId").value("history-for-verified-guest"))
            .andExpect(jsonPath("$.items[0].ruleSetVersion").value(1))
            .andExpect(jsonPath("$.items[0].hand").doesNotExist()).andExpect(jsonPath("$.items[0].events").doesNotExist())
    }
    @Test fun `bad history page rejected safely`() {
        mvc.perform(get("/api/v1/players/me/matches?limit=101").header("Authorization","Bearer valid-guest"))
            .andExpect(status().isBadRequest).andExpect(jsonPath("$.code").value("HISTORY_PAGE_INVALID"))
    }
    @Test fun `public events endpoint not implemented`() {
        assertFalse(mappings.handlerMethods.keys.any {it.patternValues.any {path->path.startsWith("/api/v1/matches/")}})
        val response=mvc.perform(get("/api/v1/matches/arbitrary/events").header("Authorization","Bearer valid-guest")).andReturn().response
        // Existing global exception mapping may map absent routes to INTERNAL_ERROR, not 404.
        assertTrue(response.status>=400);assertFalse(response.contentAsString.contains("sideA"))
    }
}
