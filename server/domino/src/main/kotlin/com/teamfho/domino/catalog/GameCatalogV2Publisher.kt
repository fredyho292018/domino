package com.teamfho.domino.catalog

import com.google.auth.oauth2.GoogleCredentials
import com.google.cloud.firestore.Firestore
import com.google.cloud.firestore.FirestoreOptions
import com.teamfho.domino.config.FirebaseProperties
import org.springframework.boot.WebApplicationType
import org.springframework.boot.builder.SpringApplicationBuilder
import java.util.concurrent.TimeUnit

// Explicit local administrative operation; never runs during backend startup.
object GameCatalogV2Publisher {
    fun canonical(): GameCatalogPublication = GameCatalogCodec.mapper.readValue(
        requireNotNull(javaClass.getResourceAsStream("/game-catalog-v2.json")), GameCatalogPublication::class.java)

    fun documents(p: GameCatalogPublication): Map<String, Map<String, Any>> {
        GameCatalogValidator.resolve(p)
        val docs=linkedMapOf<String,Map<String,Any>>()
        fun add(path:String,value:Any) {
            @Suppress("UNCHECKED_CAST")
            val data=GameCatalogCodec.storage(GameCatalogCodec.map(value)) as Map<String,Any>
            docs[path]=data
        }
        p.modes.forEach { add("gameModes/${it.id}",it) }
        p.ruleSets.forEach { add("ruleSets/${it.id}",it) }
        p.versions.forEach { add("ruleSets/${it.id}/versions/${it.version}",it) }
        p.bindings.forEach { add("gameModeRuleBindings/${it.id}",it) }
        add("gameCatalogs/${p.catalogVersion}",p)
        return docs
    }
    fun verifyImmutable(expected:Map<String,Map<String,Any>>,existing:Map<String,Map<String,Any>?>) {
        expected.forEach { (path,data) -> existing[path]?.let {
            require(GameCatalogCodec.documentContent(data)==GameCatalogCodec.documentContent(it)) { "CATALOG_IMMUTABLE_CONFLICT" }
        } }
    }
    fun publish(db:Firestore) {
        val docs=documents(canonical())
        db.runTransaction { tx ->
            val current=docs.keys.associateWith { tx.get(db.document(it)).get().data }
            val pointer=tx.get(db.document(GameCatalogSeed.POINTER)).get().data
            require((pointer?.get("publishedVersion") as? Number)?.toInt() in setOf(1,2)) { "EXPECTED_V1_OR_V2" }
            verifyImmutable(docs,current)
            docs.forEach { (path,data) -> if(current[path]==null)tx.create(db.document(path),data) }
            tx.update(db.document(GameCatalogSeed.POINTER),"publishedVersion",2L)
            null
        }.get(30,TimeUnit.SECONDS)
    }
    fun switchPointer(db:Firestore,expected:Int,next:Int) {
        require(expected in 1..2 && next in 1..2)
        db.runTransaction { tx ->
            val pointer=tx.get(db.document(GameCatalogSeed.POINTER)).get().data
            val target=tx.get(db.document("gameCatalogs/$next")).get().data ?: error("TARGET_MISSING")
            require((pointer?.get("publishedVersion") as? Number)?.toInt()==expected) { "POINTER_CONCURRENT_CHANGE" }
            val publication=GameCatalogCodec.decode(GameCatalogCodec.restored(target)!!,GameCatalogPublication::class.java)
            require(publication.catalogVersion==next);GameCatalogValidator.resolve(publication)
            tx.update(db.document(GameCatalogSeed.POINTER),"publishedVersion",next.toLong())
            null
        }.get(30,TimeUnit.SECONDS)
    }
    @JvmStatic fun main(args:Array<String>) {
        com.teamfho.domino.validation.RealFirestoreGuard.requireOptIn()
        require(args.contains("--publish-v2")) { "EXPLICIT_PUBLICATION_REQUIRED" }
        SpringApplicationBuilder(CatalogSeedConfiguration::class.java).web(WebApplicationType.NONE).logStartupInfo(false)
            .run(*args.filter { it!="--publish-v2" && it!="--verify-rollback" }.toTypedArray()).use { context ->
                val project=context.getBean(FirebaseProperties::class.java).projectId
                FirestoreOptions.newBuilder().setProjectId(project).setCredentials(GoogleCredentials.getApplicationDefault()).build().service.use { db ->
                    val store=FirestoreCatalogDocumentStore(db)
                    val old=store.read("gameCatalogs/1") ?: error("M1_PUBLICATION_REQUIRED")
                    publish(db);publish(db)
                    if(args.contains("--verify-rollback")) {
                        switchPointer(db,2,1)
                        try { check(FirestoreGameCatalogRepository(store).read().catalogVersion==1) }
                        finally { switchPointer(db,1,2) }
                    }
                    check(GameCatalogCodec.documentContent(old)==GameCatalogCodec.documentContent(store.read("gameCatalogs/1")!!))
                    check(FirestoreGameCatalogRepository(store).read().catalogVersion==2)
                    println("M3_PUBLICATION=PASS IDEMPOTENT=PASS V1_UNCHANGED=PASS PUBLISHED_POINTER=2")
                }
            }
    }
}
