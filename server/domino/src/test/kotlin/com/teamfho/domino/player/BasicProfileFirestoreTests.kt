package com.teamfho.domino.player

import com.google.api.core.ApiFutures
import com.google.cloud.firestore.*
import org.mockito.Mockito.*
import org.junit.jupiter.api.Test
import java.time.Clock
import java.time.Instant
import java.time.ZoneOffset
import java.util.UUID
import kotlin.test.*

class BasicProfileFirestoreTests {
    @Test fun `basic profile SDK retry atomically buffers root preferences progress receipt after reads`() {
        val db=mock(Firestore::class.java);val fixture=ProgressMemory().also{it.initialize()}
        fixture.docs["onboardingCatalogs/2"]=com.teamfho.domino.catalog.GameCatalogCodec.map(com.teamfho.domino.catalog.OnboardingCatalogV2.canonical())
        fixture.docs["systemConfig/onboardingCatalog"]=mapOf("publishedVersion" to 2)
        val traces=mutableListOf<List<String>>()
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
        val service=OnboardingProgressService(FirestoreOnboardingProgressRepository(db),clock=Clock.fixed(Instant.parse("2026-02-01T00:00:00Z"),ZoneOffset.UTC))
        val user=com.teamfho.domino.security.FirebaseIdentity("fixture-player",true)
        service.start(user,OnboardingStartRequest(UUID.randomUUID().toString(),0))
        val request=SaveStepRequest(UUID.randomUUID().toString(),1,2,mapOf("profile" to 0,"preferences" to 0),OnboardingStepAction.SAVE,
            listOf(OnboardingAnswer("FIRST_NAME",com.teamfho.domino.catalog.OnboardingQuestionType.TEXT,textValue="Test"),
                OnboardingAnswer("LAST_NAME",com.teamfho.domino.catalog.OnboardingQuestionType.TEXT,textValue="Player"),
                OnboardingAnswer("DISPLAY_NAME",com.teamfho.domino.catalog.OnboardingQuestionType.TEXT,textValue="TestPlayer"),
                OnboardingAnswer("COUNTRY",com.teamfho.domino.catalog.OnboardingQuestionType.COUNTRY_SELECT,"US"),
                OnboardingAnswer("PREFERRED_LANGUAGE",com.teamfho.domino.catalog.OnboardingQuestionType.LOCALE_SELECT,"es")))
        val result=service.save(user,"BASIC_PROFILE_STEP",request)
        val written=traces.last().filter{it.startsWith("write:")}
        assertEquals(setOf("write:players/fixture-player/onboarding/current","write:players/fixture-player","write:players/fixture-player/preferences/current","write:players/fixture-player/mutationReceipts/${request.operationId}"),written.filterNot { it.startsWith("write:playerAliases/") }.toSet())
        assertEquals(2,written.count { it.startsWith("write:playerAliases/") })
        assertEquals(result,service.save(user,"BASIC_PROFILE_STEP",request))
        assertTrue(traces.last().none{it.startsWith("write:")})
        assertEquals("EXPERIENCE_STEP",service.get(user).currentStepKey)
    }
}
