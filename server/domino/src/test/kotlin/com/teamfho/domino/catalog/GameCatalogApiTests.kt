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

@SpringBootTest
@ActiveProfiles("test")
@AutoConfigureMockMvc
@Import(FakeAuthConfiguration::class,GameCatalogApiTests.CatalogFixture::class,
    com.teamfho.domino.player.FakePlayerFoundationConfiguration::class)
class GameCatalogApiTests {
    @TestConfiguration class CatalogFixture {
        @Bean @Primary fun fixtureCatalogRepository() = GameCatalogRepository { GameCatalogSeed.canonical() }
    }
    @Autowired lateinit var mvc:MockMvc
    @Test fun `unauthenticated request rejected`() {
        mvc.perform(get("/api/v1/game-modes")).andExpect(status().isUnauthorized)
    }
    @Test fun `Firebase identity receives embedded canonical catalog`() {
        mvc.perform(get("/api/v1/game-modes").header("Authorization","Bearer valid-guest"))
            .andExpect(status().isOk).andExpect(jsonPath("$.catalogVersion").value(1))
            .andExpect(jsonPath("$.modes.length()").value(1))
            .andExpect(jsonPath("$.modes[0].key").value("PARTNERS_2V2"))
            .andExpect(jsonPath("$.modes[0].ruleSet.targetScore").value(200))
            .andExpect(jsonPath("$.modes[0].ruleSet.finishScoring.bonus").value(10))
    }
}
