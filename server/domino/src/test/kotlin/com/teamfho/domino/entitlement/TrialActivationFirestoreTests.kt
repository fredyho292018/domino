package com.teamfho.domino.entitlement

import com.google.api.core.ApiFutures
import com.google.cloud.firestore.*
import com.teamfho.domino.player.*
import com.teamfho.domino.security.FirebaseIdentity
import com.teamfho.domino.catalog.GameCatalogCodec
import org.mockito.Mockito.*
import org.junit.jupiter.api.Test
import java.util.UUID
import kotlin.test.*

class TrialActivationFirestoreTests {
    @Test fun `SDK transaction retry uses latest policy all reads precede atomic writes`() {
        val db=mock(Firestore::class.java);val fixture=TrialMemory();val clock=EntitlementClock();val traces=mutableListOf<List<String>>()
        `when`(db.document(anyString())).thenAnswer{c->val p=c.getArgument<String>(0);mock(DocumentReference::class.java).also{`when`(it.path).thenReturn(p)}}
        `when`(db.runTransaction(any<Transaction.Function<Any?>>(),any(TransactionOptions::class.java))).thenAnswer{call ->
            val function=call.getArgument<Transaction.Function<Any?>>(0)
            assertEquals(5,call.getArgument<TransactionOptions>(1).numberOfAttempts)
            var result:Any?=null
            repeat(2){attempt ->
                val tx=mock(Transaction::class.java);val writes=linkedMapOf<String,Map<String,Any>>();val trace=mutableListOf<String>()
                `when`(tx.get(any(DocumentReference::class.java))).thenAnswer{read ->
                    assertTrue(writes.isEmpty());val p=read.getArgument<DocumentReference>(0).path;trace+="read:$p"
                    val doc=mock(DocumentSnapshot::class.java);`when`(doc.data).thenReturn(fixture.docs[p]);ApiFutures.immediateFuture(doc)
                }
                doAnswer{write->val p=write.getArgument<DocumentReference>(0).path;trace+="write:$p";writes[p]=write.getArgument<Map<String,Any>>(1);tx}
                    .`when`(tx).set(any(DocumentReference::class.java),anyMap<String,Any>())
                result=function.updateCallback(tx);traces+=trace
                if(attempt==1)fixture.docs.putAll(writes)
            }
            ApiFutures.immediateFuture(result)
        }
        val service=TrialActivationService(FirestoreOnboardingProgressRepository(db),clock=clock)
        val id=FirebaseIdentity("fixture",true);val request=TrialActivationRequest(UUID.randomUUID().toString(),1)
        val first=service.activate(id,request)
        assertEquals(5,traces.last().count{it.startsWith("write:")})
        assertTrue(traces.last().contains("read:systemConfig/subscriptionPolicy"))
        val before=GameCatalogCodec.json(fixture.docs)
        assertEquals(first,service.activate(id,request));assertEquals(before,GameCatalogCodec.json(fixture.docs));assertTrue(traces.last().none{it.startsWith("write:")})
        fixture.docs["systemConfig/subscriptionPolicy"]=com.teamfho.domino.match.MatchCodec.map(SubscriptionPolicy(policyVersion=2))
        assertEquals("TRIAL_POLICY_VERSION_MISMATCH",assertFailsWith<OnboardingFailure>{service.activate(id,request.copy(operationId=UUID.randomUUID().toString()))}.code)
    }
}
