package com.teamfho.domino.catalog

import com.google.auth.oauth2.GoogleCredentials
import com.google.cloud.firestore.FirestoreOptions
import com.teamfho.domino.config.FirebaseProperties
import org.springframework.boot.WebApplicationType
import org.springframework.boot.builder.SpringApplicationBuilder
import java.util.concurrent.TimeUnit

/** Adds one execution mode; existing modes and RuleSet values remain byte-for-byte semantic copies. */
object GameCatalogV4Publisher {
    const val KEY="PARTNERS_2V2_ONLINE"
    fun canonical():GameCatalogPublication {
        val old=GameCatalogV3Publisher.canonical()
        val local=old.modes.single{it.key=="PARTNERS_2V2"}
        val mode=local.copy(id="partners-2v2-online",key=KEY,nameKey="mode.partners_online.title",
            descriptionKey="mode.partners_online.subtitle",sortOrder=30,minHumans=4,maxHumans=4,botsAllowed=false,
            executionModesSupported=listOf(ExecutionMode.ONLINE),onlinePolicy=DisconnectPolicy(180,false,false))
        val binding=old.bindings.single{it.modeId==local.id&&it.active&&it.isDefault}
            .copy(id="partners-2v2-online-default-v1",modeId=mode.id)
        return old.copy(catalogVersion=4,modes=old.modes+mode,bindings=old.bindings+binding)
    }
    @JvmStatic fun main(args:Array<String>) {
        com.teamfho.domino.validation.RealFirestoreGuard.requireOptIn()
        require(args.contentEquals(arrayOf("--publish-v4")))
        SpringApplicationBuilder(CatalogSeedConfiguration::class.java).web(WebApplicationType.NONE).logStartupInfo(false).run().use {context->
            FirestoreOptions.newBuilder().setProjectId(context.getBean(FirebaseProperties::class.java).projectId)
                .setCredentials(GoogleCredentials.getApplicationDefault()).build().service.use {db->
                val next=canonical();GameCatalogValidator.resolve(next)
                @Suppress("UNCHECKED_CAST") fun stored(value:Any)=GameCatalogCodec.storage(GameCatalogCodec.map(value)) as Map<String,Any>
                val documents=mapOf("gameCatalogs/4" to stored(next),"gameModes/partners-2v2-online" to stored(next.modes.last()),
                    "gameModeRuleBindings/partners-2v2-online-default-v1" to stored(next.bindings.last()))
                db.runTransaction {tx->
                    val pointer=tx.get(db.document(GameCatalogSeed.POINTER)).get()
                    val old=tx.get(db.document("gameCatalogs/3")).get().data?:error("V3_REQUIRED")
                    val existing=documents.mapValues{tx.get(db.document(it.key)).get().data}
                    require(pointer.getLong("publishedVersion") in setOf(3L,4L)){"POINTER_CONFLICT"}
                    require(GameCatalogCodec.documentContent(old)==GameCatalogCodec.documentContent(stored(GameCatalogV3Publisher.canonical()))){"V3_CONTENT_CONFLICT"}
                    documents.forEach{(path,value)->require(existing[path]==null||GameCatalogCodec.documentContent(existing[path]!!)==GameCatalogCodec.documentContent(value)){"V4_CONTENT_CONFLICT"}}
                    documents.forEach{(path,value)->if(existing[path]==null)tx.create(db.document(path),value)}
                    tx.update(db.document(GameCatalogSeed.POINTER),"publishedVersion",4L)
                    null
                }.get(30,TimeUnit.SECONDS)
                println("CATALOG_V4_PUBLISHED=PASS HISTORICAL_UNCHANGED=YES SHARED_RULESET=double-nine-partners:1")
            }
        }
    }
}
