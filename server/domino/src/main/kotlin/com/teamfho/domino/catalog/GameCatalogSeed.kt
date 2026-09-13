package com.teamfho.domino.catalog

import com.google.auth.oauth2.GoogleCredentials
import com.google.cloud.firestore.FirestoreOptions
import com.teamfho.domino.config.FirebaseProperties
import org.springframework.boot.WebApplicationType
import org.springframework.boot.builder.SpringApplicationBuilder
import org.springframework.boot.context.properties.EnableConfigurationProperties

@EnableConfigurationProperties(FirebaseProperties::class)
class CatalogSeedConfiguration

object GameCatalogSeed {
    const val POINTER = "systemConfig/gameCatalog"
    fun canonical(): GameCatalogPublication = GameCatalogCodec.mapper.readValue(
        requireNotNull(javaClass.getResourceAsStream("/game-catalog-v1.json")), GameCatalogPublication::class.java)
    fun run(store: CatalogDocumentStore, p: GameCatalogPublication = canonical()) {
        GameCatalogValidator.resolve(p)
        val docs = linkedMapOf<String,Map<String,Any>>()
        fun add(path: String,value: Any) {
            @Suppress("UNCHECKED_CAST")
            val stored = GameCatalogCodec.storage(GameCatalogCodec.map(value)) as Map<String,Any>
            docs[path] = stored
        }
        p.modes.forEach { add("gameModes/${it.id}",it) }
        p.ruleSets.forEach { add("ruleSets/${it.id}",it) }
        p.versions.forEach { add("ruleSets/${it.id}/versions/${it.version}",it) }
        p.bindings.forEach { add("gameModeRuleBindings/${it.id}",it) }
        add("gameCatalogs/${p.catalogVersion}",p)
        store.createAndPublish(docs,p.catalogVersion)
    }
    @JvmStatic fun main(args: Array<String>) {
        require(args.contains("--seed-if-absent")) { "EXPLICIT_SEED_FLAG_REQUIRED" }
        SpringApplicationBuilder(CatalogSeedConfiguration::class.java).web(WebApplicationType.NONE).logStartupInfo(false)
            .run(*args.filter { it != "--seed-if-absent" }.toTypedArray()).use { context ->
                val project = context.getBean(FirebaseProperties::class.java).projectId
                FirestoreOptions.newBuilder().setProjectId(project).setCredentials(GoogleCredentials.getApplicationDefault())
                    .build().service.use { db ->
                        val store = FirestoreCatalogDocumentStore(db)
                        run(store); run(store)
                        val p = FirestoreGameCatalogRepository(store).read()
                        println("GAME_CATALOG_SEED=PASS version=${p.catalogVersion} IDEMPOTENT=PASS")
                    }
            }
    }
}
