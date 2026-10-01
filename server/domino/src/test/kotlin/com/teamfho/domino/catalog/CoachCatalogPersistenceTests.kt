package com.teamfho.domino.catalog

import com.google.api.core.ApiFutures
import com.google.cloud.firestore.*
import org.junit.jupiter.api.Test
import org.mockito.Mockito.*
import kotlin.test.*

class CoachCatalogPersistenceTests {
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
        val repo=FirestoreCoachCatalogRepository(db)
        CoachCatalogSeed.run(repo);val original=documents.toMap();CoachCatalogSeed.run(repo)
        assertEquals(original,documents)
        assertEquals(setOf("coachCatalogs/1","systemConfig/coachCatalog"),documents.keys)
        assertTrue(traces.takeLast(2).all{t->t.all{it.startsWith("read:")}})
        assertEquals(CoachCatalogSeed.canonical(),GameCatalogCodec.decode(documents.getValue("coachCatalogs/1"),CoachCatalogPublication::class.java))
        assertFails {repo.publish(CoachCatalogSeed.canonical().copy(coaches=CoachCatalogSeed.canonical().coaches.map{it.copy(active=false)}))}
        assertEquals(original,documents)
    }
}

