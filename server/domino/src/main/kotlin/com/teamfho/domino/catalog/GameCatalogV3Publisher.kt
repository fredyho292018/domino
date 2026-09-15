package com.teamfho.domino.catalog

import com.google.auth.oauth2.GoogleCredentials
import com.google.cloud.firestore.FirestoreOptions
import com.teamfho.domino.config.FirebaseProperties
import org.springframework.boot.WebApplicationType
import org.springframework.boot.builder.SpringApplicationBuilder
import java.util.concurrent.TimeUnit

/** Explicit immutable publication. The repository reads the versioned aggregate, not legacy indexes. */
object GameCatalogV3Publisher {
    fun canonical(): GameCatalogPublication = GameCatalogV2Publisher.canonical().let { old ->
        old.copy(catalogVersion=3,modes=old.modes.map { if(it.key=="DUEL_1V1")
            it.copy(executionModesSupported=listOf(ExecutionMode.LOCAL,ExecutionMode.ONLINE)) else it })
    }
    @JvmStatic fun main(args:Array<String>) {
        require(args.contentEquals(arrayOf("--publish-v3")))
        SpringApplicationBuilder(CatalogSeedConfiguration::class.java).web(WebApplicationType.NONE).logStartupInfo(false).run().use { context ->
            val project=context.getBean(FirebaseProperties::class.java).projectId
            FirestoreOptions.newBuilder().setProjectId(project).setCredentials(GoogleCredentials.getApplicationDefault()).build().service.use { db ->
                val next=canonical();GameCatalogValidator.resolve(next)
                @Suppress("UNCHECKED_CAST")
                val data=GameCatalogCodec.storage(GameCatalogCodec.map(next)) as Map<String,Any>
                db.runTransaction { tx ->
                    val pointer=tx.get(db.document(GameCatalogSeed.POINTER)).get().data
                    val old=tx.get(db.document("gameCatalogs/2")).get().data ?: error("V2_REQUIRED")
                    val existing=tx.get(db.document("gameCatalogs/3")).get().data
                    val expected=GameCatalogV2Publisher.documents(GameCatalogV2Publisher.canonical()).getValue("gameCatalogs/2")
                    require(GameCatalogCodec.documentContent(old)==GameCatalogCodec.documentContent(expected)) {"V2_CONTENT_CONFLICT"}
                    require((pointer?.get("publishedVersion") as? Number)?.toInt() in setOf(2,3)) {"POINTER_CONFLICT"}
                    require(existing==null||GameCatalogCodec.documentContent(existing)==GameCatalogCodec.documentContent(data)) {"V3_IMMUTABLE_CONFLICT"}
                    if(existing==null)tx.create(db.document("gameCatalogs/3"),data)
                    tx.update(db.document(GameCatalogSeed.POINTER),"publishedVersion",3L)
                    null
                }.get(30,TimeUnit.SECONDS)
                println("CATALOG_V3_PUBLISHED=PASS V2_UNCHANGED=YES DUEL_EXECUTION=LOCAL,ONLINE RULES_UNCHANGED=YES")
            }
        }
    }
}
