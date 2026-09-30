package com.teamfho.domino.catalog

import com.google.api.core.ApiFutures
import com.google.cloud.firestore.*
import org.junit.jupiter.api.Test
import org.mockito.Mockito.*
import kotlin.test.*

class OnboardingCatalogPersistenceTests {
    @Test fun `Firestore publication retry is atomic create only and repeated seed has no writes`() {
        val db=mock(Firestore::class.java)
        val documents=mutableMapOf<String,Map<String,Any>>()
        val traces=mutableListOf<List<String>>()
        `when`(db.document(anyString())).thenAnswer { call ->
            val path=call.getArgument<String>(0)
            mock(DocumentReference::class.java).also { `when`(it.path).thenReturn(path) }
        }
        `when`(db.runTransaction(any<Transaction.Function<Any?>>())).thenAnswer { call ->
            val callback=call.getArgument<Transaction.Function<Any?>>(0)
            repeat(2) { attempt ->
                val pending=mutableMapOf<String,Map<String,Any>>()
                val trace=mutableListOf<String>()
                val tx=mock(Transaction::class.java)
                `when`(tx.get(any(DocumentReference::class.java))).thenAnswer { read ->
                    assertTrue(pending.isEmpty(),"all reads before writes")
                    val path=read.getArgument<DocumentReference>(0).path;trace+="read:$path"
                    val doc=mock(DocumentSnapshot::class.java)
                    `when`(doc.data).thenReturn(documents[path]);`when`(doc.exists()).thenReturn(path in documents)
                    ApiFutures.immediateFuture(doc)
                }
                doAnswer { write ->
                    val path=write.getArgument<DocumentReference>(0).path
                    assertFalse(path in documents);trace+="create:$path"
                    pending[path]=write.getArgument<Map<String,Any>>(1);tx
                }.`when`(tx).create(any(DocumentReference::class.java),anyMap<String,Any>())
                doAnswer { write ->
                    val path=write.getArgument<DocumentReference>(0).path;trace+="set:$path"
                    pending[path]=write.getArgument<Map<String,Any>>(1);tx
                }.`when`(tx).set(any(DocumentReference::class.java),anyMap<String,Any>())
                callback.updateCallback(tx);traces+=trace
                if(attempt==1) documents.putAll(pending)
            }
            ApiFutures.immediateFuture(null)
        }
        val repo=FirestoreOnboardingCatalogRepository(db)
        OnboardingCatalogSeed.run(repo);val original=documents.toMap();OnboardingCatalogSeed.run(repo)
        assertEquals(original,documents)
        assertEquals(setOf("onboardingCatalogs/1","systemConfig/onboardingCatalog"),documents.keys)
        assertTrue(traces.takeLast(2).all{t->t.all{it.startsWith("read:")}})
        assertEquals(OnboardingCatalogSeed.canonical(),GameCatalogCodec.decode(documents.getValue("onboardingCatalogs/1"),OnboardingCatalogPublication::class.java))
        assertFails {repo.publish(OnboardingCatalogSeed.canonical().copy(coachCatalogVersion=1,catalogVersion=2))}
        assertEquals(original,documents)
    }
    @Test fun `cache expiration refreshes pointer while historical version remains addressable`() {
        val repo=MemoryOnboardingCatalog();OnboardingCatalogSeed.run(repo)
        var now=java.time.Instant.parse("2026-01-01T00:00:00Z")
        val clock=object:java.time.Clock(){override fun getZone()=java.time.ZoneOffset.UTC;override fun withZone(zone:java.time.ZoneId)=this;override fun instant()=now}
        val service=OnboardingCatalogService(repo,clock)
        assertEquals(1,service.read("en",null).catalogVersion)
        repo.publish(OnboardingCatalogSeed.canonical().copy(catalogVersion=2))
        assertEquals(1,service.read("es",null).catalogVersion)
        now=now.plusSeconds(301)
        assertEquals(2,service.read("en",null).catalogVersion)
        assertEquals(1,service.read("es",1).catalogVersion)
        val before=repo.reads;now=now.plusSeconds(86401);service.read("es",1);assertEquals(before+1,repo.reads)
    }
}
