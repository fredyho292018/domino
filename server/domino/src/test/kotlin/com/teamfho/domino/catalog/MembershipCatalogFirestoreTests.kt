package com.teamfho.domino.catalog

import com.google.api.core.ApiFutures
import com.google.cloud.firestore.*
import org.mockito.Mockito.*
import org.junit.jupiter.api.Test
import kotlin.test.*

class MembershipCatalogFirestoreTests {
    @Test fun `SDK seed retries atomically preserves unrelated state and read is side effect free`() {
        val db=mock(Firestore::class.java)
        val docs=linkedMapOf<String,Map<String,Any>>(
            "players/fixture" to mapOf("status" to "ACTIVE"),
            "players/fixture/entitlementState/current" to mapOf("trialConsumed" to true,"revision" to 7),
            "players/fixture/promotions/initial-premium-trial" to mapOf("trialConsumed" to true),
            "players/fixture/onboarding/current" to mapOf("status" to "IN_PROGRESS"))
        val baseline=docs.toMap();var writes=0
        fun snapshot(path:String)=mock(DocumentSnapshot::class.java).also{`when`(it.data).thenReturn(docs[path])}
        `when`(db.document(anyString())).thenAnswer{call->val path=call.getArgument<String>(0);mock(DocumentReference::class.java).also{
            `when`(it.path).thenReturn(path);`when`(it.get()).thenAnswer{ApiFutures.immediateFuture(snapshot(path))}}}
        `when`(db.runTransaction(any<Transaction.Function<Any?>>())).thenAnswer{call->
            val f=call.getArgument<Transaction.Function<Any?>>(0);var result:Any?=null
            repeat(2){attempt->val tx=mock(Transaction::class.java);val buffered=linkedMapOf<String,Map<String,Any>>()
                `when`(tx.get(any(DocumentReference::class.java))).thenAnswer{read->assertTrue(buffered.isEmpty());ApiFutures.immediateFuture(snapshot(read.getArgument<DocumentReference>(0).path))}
                doAnswer{write->val path=write.getArgument<DocumentReference>(0).path;check(path !in docs);buffered[path]=write.getArgument<Map<String,Any>>(1);tx}.`when`(tx).create(any(DocumentReference::class.java),anyMap<String,Any>())
                doAnswer{write->buffered[write.getArgument<DocumentReference>(0).path]=write.getArgument<Map<String,Any>>(1);tx}.`when`(tx).set(any(DocumentReference::class.java),anyMap<String,Any>())
                result=f.updateCallback(tx);if(attempt==1){docs.putAll(buffered);writes+=buffered.size}
            };ApiFutures.immediateFuture(result)
        }
        val repository=FirestoreMembershipCatalogRepository(db)
        MembershipCatalogSeed.run(repository);val firstWrites=writes;MembershipCatalogSeed.run(repository)
        assertEquals(firstWrites,writes);assertEquals(4,firstWrites)
        assertEquals(baseline,docs.filterKeys{it.startsWith("players/")})
        val before=GameCatalogCodec.json(docs)
        val controller=MembershipCatalogController(MembershipCatalogService(repository),OnboardingCatalogAccess{identity->check(docs["players/${identity.uid}"]?.get("status")=="ACTIVE")})
        repeat(2){assertEquals(200,controller.catalog(com.teamfho.domino.security.FirebaseIdentity("fixture",true),"es-US",null,org.springframework.mock.web.MockHttpServletRequest()).statusCode.value())}
        assertEquals(before,GameCatalogCodec.json(docs));assertEquals(firstWrites,writes)
        assertFails{repository.publish(MembershipCatalogSeed.canonical().copy(publishedAt="2026-10-01T00:00:00Z"))}
        assertEquals(before,GameCatalogCodec.json(docs))
    }
}
