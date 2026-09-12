package com.teamfho.domino.player

import com.teamfho.domino.security.FirebaseIdentity
import org.junit.jupiter.api.Test
import org.junit.jupiter.params.ParameterizedTest
import org.junit.jupiter.params.provider.ValueSource
import kotlin.test.*

class PlayerBootstrapServiceTests {
    private val repository = FakePlayerFoundationRepository()
    private val service = PlayerBootstrapService(repository)
    private val identity = FirebaseIdentity("service-user", true)

    @Test fun `repeated bootstrap preserves profile and wallet`() {
        val first = service.bootstrap(identity, PlayerBootstrapRequest("es"))
        val second = service.bootstrap(identity, PlayerBootstrapRequest("en"))
        assertEquals(first.player.uid, second.player.uid)
        assertEquals(first.player.displayName, second.player.displayName)
        assertEquals("es", second.player.language)
        assertEquals(0L, second.wallet.coins)
        assertEquals(2, repository.store.documents.size)
        assertTrue(first.player.displayName.matches(Regex("Guest-[A-Z0-9]{8}")))
    }

    @Test fun `absent request defaults to English`() {
        assertEquals("en", service.bootstrap(identity, null).player.language)
    }

    @ParameterizedTest @ValueSource(strings = ["fr", "de", "ES", "english", ""])
    fun `invalid language never reaches repository`(language: String) {
        assertFailsWith<UnsupportedPlayerLanguageException> { service.bootstrap(identity, PlayerBootstrapRequest(language)) }
        assertEquals(0, repository.calls)
    }
}
