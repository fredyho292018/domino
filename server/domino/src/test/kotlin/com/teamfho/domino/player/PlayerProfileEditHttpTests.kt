package com.teamfho.domino.player

import com.teamfho.domino.catalog.*
import com.teamfho.domino.security.FakeAuthConfiguration
import org.junit.jupiter.api.Test
import org.junit.jupiter.api.BeforeEach
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
import java.util.UUID
import kotlin.test.*

@SpringBootTest
@ActiveProfiles("test")
@AutoConfigureMockMvc
@Import(FakeAuthConfiguration::class,FakePlayerFoundationConfiguration::class,PlayerProfileEditHttpTests.Fixture::class)
class PlayerProfileEditHttpTests {
 @TestConfiguration class Fixture { @Bean @Primary fun editingProgress()=ProgressMemory() }
 @Autowired lateinit var mvc:MockMvc
 @Autowired lateinit var repo:ProgressMemory
 @BeforeEach fun setup(){repo.docs.clear();repo.initialize("verified-guest")}
 private val path="/api/v1/player/profile"
 private fun payload()="""{"firstName":"Ana","lastName":"Rivera","displayName":"Fixture","country":"CU","preferredLanguage":"es","expectedProfileRevision":0,"expectedPreferencesRevision":0}"""
 @Test fun `authentication and private response no cache`() {
  mvc.perform(get("$path/editable")).andExpect(status().isUnauthorized)
  mvc.perform(put(path).contentType("application/json").content(payload())).andExpect(status().isUnauthorized)
  mvc.perform(get("$path/editable").header("Authorization","Bearer valid-guest")).andExpect(status().isOk).andExpect(header().string("Cache-Control","no-store")).andExpect(jsonPath("$.displayName").value("Fixture")).andExpect(jsonPath("$.uid").doesNotExist())
 }
 @Test fun `all fields update without onboarding writes`() {
  val onboarding=repo.docs["players/verified-guest/onboarding/current"]
  mvc.perform(put(path).header("Authorization","Bearer valid-guest").contentType("application/json").content(payload())).andExpect(status().isOk).andExpect(jsonPath("$.firstName").value("Ana"))
  assertEquals(onboarding,repo.docs["players/verified-guest/onboarding/current"])
 }
 @Test fun `reject identity spoof and non string fields`() {
  val before=repo.docs.toMap()
  for(p in listOf(payload().dropLast(1)+",\"uid\":\"other\"}",payload().replace("\"firstName\":\"Ana\"","\"firstName\":42"))){mvc.perform(put(path).header("Authorization","Bearer valid-guest").contentType("application/json").content(p)).andExpect(status().isBadRequest)}
  assertEquals(before,repo.docs)
 }
}