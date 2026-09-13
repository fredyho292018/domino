package com.teamfho.domino.catalog

import com.google.cloud.firestore.Firestore
import java.util.concurrent.TimeUnit

fun interface GameCatalogRepository { fun read(): GameCatalogPublication }

// Port shared by the explicit seed and its failure/idempotency tests.
interface CatalogDocumentStore {
    fun read(path: String): Map<String, Any>?
    fun createAndPublish(documents: Map<String, Map<String, Any>>, version: Int)
}

class FirestoreCatalogDocumentStore(private val db: Firestore) : CatalogDocumentStore {
    override fun read(path: String): Map<String, Any>? = db.document(path).get().get(5,TimeUnit.SECONDS).data
    override fun createAndPublish(documents: Map<String, Map<String, Any>>, version: Int) {
        // All reads precede all writes. A conflicting historical document aborts everything,
        // including the pointer; Firestore retries are safe and have no outside side effects.
        db.runTransaction { tx ->
            val existing = documents.keys.associateWith { tx.get(db.document(it)).get().data }
            val pointer = tx.get(db.document(GameCatalogSeed.POINTER)).get().data
            CatalogSeedChecks.verify(documents,existing,pointer,version)
            documents.forEach { (path,data) -> if (existing[path] == null) tx.create(db.document(path),data) }
            if (pointer == null) tx.create(db.document(GameCatalogSeed.POINTER),mapOf("publishedVersion" to version.toLong()))
            null
        }.get(30,TimeUnit.SECONDS)
    }
}

object CatalogSeedChecks {
    fun verify(expected: Map<String,Map<String,Any>>, existing: Map<String,Map<String,Any>?>,
        pointer: Map<String,Any>?, version: Int) {
        expected.forEach { (path,data) ->
            existing[path]?.let { require(GameCatalogCodec.documentContent(it) ==
                GameCatalogCodec.documentContent(data)) { "CATALOG_SEED_CONTENT_CONFLICT" } }
        }
        // Seed v1 must never roll a newer publication backwards.
        require(pointer == null || pointer["publishedVersion"] is Number && (pointer["publishedVersion"] as Number).toLong() == version.toLong()) { "CATALOG_SEED_POINTER_CONFLICT" }
    }
}

class FirestoreGameCatalogRepository(private val store: CatalogDocumentStore) : GameCatalogRepository {
    override fun read(): GameCatalogPublication {
        val pointer = store.read(GameCatalogSeed.POINTER) ?: error("CATALOG_NOT_PUBLISHED")
        val version = pointer["publishedVersion"] as? Long ?: error("CATALOG_POINTER_INVALID")
        require(version in 1..Int.MAX_VALUE)
        val data = store.read("gameCatalogs/$version") ?: error("CATALOG_PUBLICATION_MISSING")
        val publication = GameCatalogCodec.decode(GameCatalogCodec.restored(data)!!,GameCatalogPublication::class.java)
        require(publication.catalogVersion.toLong() == version)
        GameCatalogValidator.resolve(publication)
        return publication
    }
}
