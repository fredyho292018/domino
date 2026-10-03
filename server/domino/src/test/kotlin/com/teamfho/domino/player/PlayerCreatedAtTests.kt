package com.teamfho.domino.player

import com.google.cloud.Timestamp
import com.google.cloud.firestore.Firestore
import com.teamfho.domino.entitlement.EntitlementController
import com.teamfho.domino.entitlement.EntitlementService
import com.teamfho.domino.security.FirebaseIdentity
import org.junit.jupiter.api.Test
import org.junit.jupiter.params.ParameterizedTest
import org.junit.jupiter.params.provider.ValueSource
import org.mockito.Mockito.*
import org.springframework.beans.factory.ObjectProvider
import java.time.Clock
import java.time.Instant
import java.time.ZoneOffset
import kotlin.test.*

class PlayerCreatedAtTests {
    private val now=Instant.parse("2030-02-04T12:00:00Z")
    private val creation=Instant.parse("2022-01-21T23:59:59.123456789Z")
    private val identity=FirebaseIdentity("created-at-fixture",true)
    private val store=InMemoryFirestoreTransactions(now)
    private val repository=FirestorePlayerFoundationRepository(store.firestore,Clock.fixed(now,ZoneOffset.UTC))
    private val path="players/${identity.uid}"
    private fun seed() {
        repository.ensure(identity,"en","Guest-ABCDEFGH")
        store.documents[path]=store.documents.getValue(path)+("createdAt" to Timestamp.ofTimeSecondsAndNanos(creation.epochSecond,creation.nano))
        store.callbacks.clear()
    }
    @Test fun `bootstrap projects exact recorded instant without using last seen or clock`() {
        seed();val before=store.documents.toMap()
        val result=repository.ensure(identity,"es","Guest-ZZZZZZZZ")
        assertEquals(creation.toString(),PlayerBootstrapResponse.from(result).player.createdAt)
        assertNotEquals(now.toString(),PlayerBootstrapResponse.from(result).player.createdAt)
        assertEquals(before,store.documents)
        assertTrue(store.callbacks.flatten().none{it.startsWith("create:")||it.startsWith("update:")})
    }
    @Test fun `new unresolved server timestamp stays null no fabricated date or additional read`() {
        val result=repository.ensure(identity,"en","Guest-ABCDEFGH")
        assertNull(PlayerBootstrapResponse.from(result).player.createdAt)
        assertEquals(7,store.callbacks.flatten().count{it.startsWith("read:")}) // foundation reads plus rollout/reservation; no createdAt follow-up
    }
    @ParameterizedTest @ValueSource(booleans=[false,true])
    fun `legacy missing or null creation supported without backfill`(explicitNull:Boolean) {
        seed()
        @Suppress("UNCHECKED_CAST")
        val legacy=(store.documents.getValue(path)-"createdAt").toMutableMap().also { if(explicitNull)(it as MutableMap<String,Any?>)["createdAt"]=null }
        store.documents[path]=legacy
        val before=store.documents.toMap()
        val result=repository.ensure(identity,"en","Guest-ABCDEFGH")
        assertNull(result.player.createdAt);assertNull(PlayerBootstrapResponse.from(result).player.createdAt)
        assertEquals(before,store.documents)
        assertTrue(store.callbacks.flatten().none{it.startsWith("create:")||it.startsWith("update:")})
    }
    @Test fun `malformed present creation still rejected`() {
        seed();store.documents[path]=store.documents.getValue(path)+("createdAt" to "not-a-timestamp")
        assertEquals(FoundationError.PLAYER_STATE_CONFLICT,assertFailsWith<PlayerFoundationException>{repository.ensure(identity,"en","Guest-ABCDEFGH")}.code)
    }
    @Test fun `existing profile GET reads nullable creation without writes`() {
        seed();store.documents[path]=store.documents.getValue(path)-"createdAt"
        val before=store.documents.toMap()
        val ref=store.firestore.document(path)
        val snapshot=mock(com.google.cloud.firestore.DocumentSnapshot::class.java)
        `when`(snapshot.data).thenReturn(store.documents[path])
        `when`(ref.get()).thenReturn(com.google.api.core.ApiFutures.immediateFuture(snapshot))
        @Suppress("UNCHECKED_CAST")
        val provider=mock(ObjectProvider::class.java) as ObjectProvider<Firestore>
        `when`(provider.ifAvailable).thenReturn(store.firestore)
        val response=EntitlementController(mock(EntitlementService::class.java),provider).profile(identity)
        assertEquals(200,response.statusCode.value());assertNull((response.body as Map<*,*>)["createdAt"])
        assertEquals(before,store.documents)
        verify(ref).get()
        verify(ref,never()).delete()
        assertTrue(store.callbacks.flatten().none{it.startsWith("create:")||it.startsWith("update:")})
    }
    @Test fun `missing creation cannot be substituted in explicit historical rollout initialization`() {
        seed();store.documents[path]=store.documents.getValue(path)-"createdAt"
        val before=store.documents.toMap()
        assertFails { FirestoreOnboardingFoundation(store.firestore,OnboardingRolloutBoundary(now)).initialize(identity,now) }
        assertEquals(before,store.documents)
    }
}
