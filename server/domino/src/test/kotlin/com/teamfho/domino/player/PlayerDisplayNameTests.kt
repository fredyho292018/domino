package com.teamfho.domino.player

import com.google.cloud.Timestamp
import com.teamfho.domino.security.FakeAuthConfiguration
import com.teamfho.domino.security.FirebaseIdentity
import org.junit.jupiter.api.BeforeEach
import org.junit.jupiter.api.Test
import org.junit.jupiter.params.ParameterizedTest
import org.junit.jupiter.params.provider.ValueSource
import org.springframework.beans.factory.annotation.Autowired
import org.springframework.boot.test.context.SpringBootTest
import org.springframework.boot.webmvc.test.autoconfigure.AutoConfigureMockMvc
import org.springframework.context.annotation.Import
import org.springframework.test.context.ActiveProfiles
import org.springframework.test.web.servlet.MockMvc
import org.springframework.test.web.servlet.request.MockMvcRequestBuilders.put
import org.springframework.test.web.servlet.result.MockMvcResultMatchers.*
import kotlin.test.*

@SpringBootTest @ActiveProfiles("test") @AutoConfigureMockMvc
@Import(FakeAuthConfiguration::class, FakePlayerFoundationConfiguration::class)
class PlayerDisplayNameTests {
    @Autowired lateinit var mvc: MockMvc
    @Autowired lateinit var repository: FakePlayerFoundationRepository
    private val identity = FirebaseIdentity("verified-guest", true)
    private val playerPath = "players/verified-guest"
    private val walletPath = "$playerPath/wallet/main"
    @BeforeEach fun reset() {
        repository.reset()
        repository.ensure(identity, "es", "Guest-ABCDEFGH")
    }
    private fun rename(body: String, authenticated: Boolean = true) = mvc.perform(
        put("/api/v1/player/display-name").contentType("application/json").content(body).apply {
            if (authenticated) header("Authorization", "Bearer valid-guest")
        })
    @ParameterizedTest @ValueSource(strings = ["abc", "Abcdefghijklmn16", "Abcdefghijklmn_9", "Fredy92", "Fredy_HO", "player-99"])
    fun `valid case preserved`(value: String) {
        rename("""{"displayName":"$value"}""").andExpect(status().isOk)
            .andExpect(jsonPath("$.player.displayName").value(value)).andExpect(jsonPath("$.wallet.coins").value(0))
    }
    @ParameterizedTest @ValueSource(strings = ["ab", "abcdefghijklmnopq", "Fredy HO", " Fredy", "Fredy ", "Fredy!", "🔥Fredy", "https://test", "<b>x</b>", "Fredy\\n", "Fredy\\t", "ñame"])
    fun `invalid aliases`(value: String) {
        rename("""{"displayName":"$value"}""").andExpect(status().isBadRequest)
            .andExpect(jsonPath("$.code").value("DISPLAY_NAME_INVALID"))
    }
    @ParameterizedTest @ValueSource(strings = ["admin", "ADMIN", "Administrator", "moderator", "support", "TeamFHO", "system"])
    fun `protected names`(value: String) {
        rename("""{"displayName":"$value"}""").andExpect(status().isBadRequest)
            .andExpect(jsonPath("$.code").value("DISPLAY_NAME_RESERVED"))
    }
    @ParameterizedTest @ValueSource(strings = ["uid", "coins", "language", "accountType", "status"])
    fun `client cannot select owner or economy`(field: String) {
        rename("""{"displayName":"Fredy92","$field":"other"}""").andExpect(status().isBadRequest)
            .andExpect(jsonPath("$.code").value("REQUEST_INVALID"))
    }
    @Test fun `unauthenticated`() { rename("""{"displayName":"Fredy92"}""", false).andExpect(status().isUnauthorized) }
    @ParameterizedTest @ValueSource(strings = ["null", "123", "true", "{}", "[]"])
    fun `only JSON strings accepted`(value: String) {
        rename("""{"displayName":$value}""").andExpect(status().isBadRequest)
            .andExpect(jsonPath("$.code").value("DISPLAY_NAME_INVALID"))
    }
    @Test fun `missing player never created`() {
        repository.store.documents.remove(playerPath)
        rename("""{"displayName":"Fredy92"}""").andExpect(status().isConflict).andExpect(jsonPath("$.code").value("PLAYER_STATE_CONFLICT"))
        assertFalse(repository.store.documents.containsKey(playerPath))
    }
    @Test fun `missing wallet never created`() {
        repository.store.documents.remove(walletPath)
        rename("""{"displayName":"Fredy92"}""").andExpect(status().isConflict)
        assertFalse(repository.store.documents.containsKey(walletPath))
    }
    @Test fun `partial update preserves all other fields and same name does not write`() {
        val oldTime = Timestamp.ofTimeSecondsAndNanos(10, 0)
        repository.store.documents[playerPath] = repository.store.documents.getValue(playerPath) + mapOf("createdAt" to oldTime, "updatedAt" to oldTime)
        repository.store.documents[walletPath] = repository.store.documents.getValue(walletPath) + mapOf("coins" to 1234L, "lifetimeCoinsEarned" to 5678L)
        val before = repository.store.documents.getValue(playerPath)
        val wallet = repository.store.documents.getValue(walletPath)
        rename("""{"displayName":"Fredy92"}""").andExpect(status().isOk)
        val after = repository.store.documents.getValue(playerPath)
        assertEquals(before - setOf("displayName", "updatedAt"), after - setOf("displayName", "updatedAt"))
        assertNotEquals(before["updatedAt"], after["updatedAt"])
        assertEquals(wallet, repository.store.documents[walletPath])
        assertEquals(listOf("read:$playerPath", "read:$walletPath", "read:$playerPath/publicIdentity/current", "update:$playerPath"), repository.store.callbacks.last())
        rename("""{"displayName":"Fredy92"}""").andExpect(status().isOk)
        assertEquals(listOf("read:$playerPath", "read:$walletPath"), repository.store.callbacks.last())
        assertEquals(after, repository.store.documents[playerPath])
    }
}
