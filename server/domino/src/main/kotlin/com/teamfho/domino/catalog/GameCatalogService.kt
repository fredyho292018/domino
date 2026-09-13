package com.teamfho.domino.catalog

import com.google.cloud.firestore.Firestore
import org.springframework.beans.factory.ObjectProvider
import org.springframework.beans.factory.annotation.Value
import org.springframework.context.annotation.Bean
import org.springframework.context.annotation.Configuration
import org.springframework.web.bind.annotation.GetMapping
import org.springframework.web.bind.annotation.RestController
import org.springframework.http.ResponseEntity
import org.slf4j.LoggerFactory
import java.time.Clock
import java.time.Instant

class GameCatalogService(private val repository: GameCatalogRepository, private val ttl: Long = 300,
    private val clock: Clock = Clock.systemUTC()) {
    init { require(ttl in 1..3600) }
    private var cached: GameCatalogSnapshot? = null
    private var refreshAt = Instant.MIN
    @Synchronized fun resolve(): GameCatalogSnapshot? {
        val now = clock.instant()
        if (now < refreshAt) return cached
        refreshAt = now.plusSeconds(ttl)
        try {
            val next = GameCatalogValidator.resolve(repository.read())
            // Same publication version must never acquire different semantics.
            require(cached == null || cached!!.catalogVersion != next.catalogVersion || cached == next)
            cached = next
        } catch (e: Exception) {
            if (e is InterruptedException) Thread.currentThread().interrupt()
            LoggerFactory.getLogger(javaClass).warn("[GAME-CATALOG] unavailable category=STORAGE_OR_CONTRACT")
        }
        return cached // no invented server fallback
    }
}
@Configuration(proxyBeanMethods=false)
class GameCatalogConfiguration {
    @Bean fun gameCatalogRepository(db: ObjectProvider<Firestore>): GameCatalogRepository =
        db.ifAvailable?.let { FirestoreGameCatalogRepository(FirestoreCatalogDocumentStore(it)) }
            ?: GameCatalogRepository { error("CATALOG_STORAGE_UNAVAILABLE") }
    @Bean fun gameCatalogService(repository: GameCatalogRepository,
        @Value("\${domino.game-catalog.cache-seconds:\${DOMINO_GAME_CATALOG_CACHE_SECONDS:300}}") ttl: Long) = GameCatalogService(repository,ttl)
}
@RestController
class GameCatalogController(private val service: GameCatalogService) {
    @GetMapping("/api/v1/game-modes")
    fun catalog(): ResponseEntity<*> = service.resolve()?.let { ResponseEntity.ok(it) }
        ?: ResponseEntity.status(503).body(mapOf("code" to "GAME_CATALOG_UNAVAILABLE"))
}
