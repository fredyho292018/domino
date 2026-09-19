package com.teamfho.domino.online

import com.teamfho.domino.catalog.*
import com.teamfho.domino.security.FakeAuthConfiguration
import com.teamfho.domino.player.FakePlayerFoundationConfiguration
import org.junit.jupiter.api.Test
import org.springframework.beans.factory.annotation.Autowired
import org.springframework.boot.test.context.SpringBootTest
import org.springframework.boot.test.context.TestConfiguration
import org.springframework.boot.webmvc.test.autoconfigure.AutoConfigureMockMvc
import org.springframework.context.annotation.*
import org.springframework.http.MediaType
import org.springframework.test.context.ActiveProfiles
import org.springframework.test.web.servlet.MockMvc
import org.springframework.test.web.servlet.request.MockMvcRequestBuilders.*
import org.springframework.test.web.servlet.result.MockMvcResultMatchers.*
import kotlin.test.*

@SpringBootTest
@ActiveProfiles("test")
@AutoConfigureMockMvc
@Import(FakeAuthConfiguration::class,FakePlayerFoundationConfiguration::class,OnlineHttpTests.Fixture::class)
class OnlineHttpTests {
    @Autowired lateinit var mvc: MockMvc
    @TestConfiguration(proxyBeanMethods=false) class Fixture {
        @Bean @Primary fun testOnlineRepository(): OnlineRepository=MemoryOnlineRepository()
        @Bean @Primary fun testOnlineCatalog(): GameCatalogRepository=GameCatalogRepository {GameCatalogV2Publisher.canonical()}
        @Bean @Primary fun testOnlineProfiles()=OnlineParticipantProfiles { OnlineParticipantProfile("Test Player") }
    }
    @Test fun `create and join principal authority snapshot and event isolation`() {
        mvc.perform(post("/api/v1/matches").contentType(MediaType.APPLICATION_JSON).content("{\"modeKey\":\"DUEL_1V1\"}")).andExpect(status().isUnauthorized)
        val response=mvc.perform(post("/api/v1/matches").header("Authorization","Bearer valid-guest").contentType(MediaType.APPLICATION_JSON)
            .content("{\"modeKey\":\"DUEL_1V1\"}")).andExpect(status().isOk).andExpect(jsonPath("$.privateState.seat").value(0)).andReturn().response.contentAsString
        val id=GameCatalogCodec.mapper.readTree(response).path("publicState").path("matchId").asString()
        mvc.perform(get("/api/v1/matches/$id/snapshot?uid=verified-guest").header("Authorization","Bearer valid-registered"))
            .andExpect(status().isForbidden).andExpect(jsonPath("$.code").value("NOT_PARTICIPANT"))
        mvc.perform(post("/api/v1/matches/$id/join").header("Authorization","Bearer valid-guest").contentType(MediaType.APPLICATION_JSON).content("{\"commandId\":\"same-seat\"}"))
            .andExpect(status().isConflict).andExpect(jsonPath("$.code").value("SAME_PLAYER"))
        mvc.perform(post("/api/v1/matches/$id/join").header("Authorization","Bearer valid-registered").contentType(MediaType.APPLICATION_JSON).content("{\"commandId\":\"join\"}"))
            .andExpect(status().isOk).andExpect(jsonPath("$.privateState.seat").value(1)).andExpect(jsonPath("$.phase").value("STARTER_SELECTION"))
        val events=mvc.perform(get("/api/v1/matches/$id/events").header("Authorization","Bearer valid-registered")).andExpect(status().isOk).andReturn().response.contentAsString
        assertFalse(events.contains("sideA"));assertFalse(events.contains("sideB"));assertFalse(events.contains("verified-guest"))
    }
    @Test fun `strict command parser rejects UID and score tampering`() {
        for(extra in listOf("uid","score","winner","seat","newBoard")) {
            val payload="{\"protocolVersion\":1,\"commandId\":\"id\",\"matchId\":\"match\",\"type\":\"PASS\",\"$extra\":1}"
            assertFails {GameCatalogCodec.mapper.readValue(payload,OnlineCommand::class.java)}
        }
    }
}
