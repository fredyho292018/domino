package com.teamfho.domino.catalog

import com.google.cloud.firestore.Firestore
import com.teamfho.domino.security.FirebaseIdentity
import org.springframework.beans.factory.ObjectProvider
import org.springframework.context.annotation.Bean
import org.springframework.context.annotation.Configuration
import org.springframework.http.ResponseEntity
import org.springframework.security.core.annotation.AuthenticationPrincipal
import org.springframework.web.bind.annotation.*
import java.time.Clock
import java.time.Instant
import java.util.concurrent.TimeUnit

class OnboardingCatalogFailure(val status: Int, val code: String): RuntimeException(code)
fun interface OnboardingCatalogAccess { fun check(identity: FirebaseIdentity) }
class OnboardingCatalogService(private val repository: OnboardingCatalogRepository, private val clock: Clock = Clock.systemUTC()) {
    private var pointer: Int? = null
    private var pointerExpires = Instant.MIN
    private val versions = object: LinkedHashMap<Int,Pair<Instant,OnboardingCatalogPublication>>(16,0.75f,true) {
        override fun removeEldestEntry(eldest: MutableMap.MutableEntry<Int,Pair<Instant,OnboardingCatalogPublication>>?) = size > 32
    }
    @Synchronized fun read(locale: String?, version: Int?): OnboardingCatalogResponse {
        if(version != null && version <= 0) throw OnboardingCatalogFailure(400,"ONBOARDING_VERSION_INVALID")
        try {
            val now = clock.instant()
            val selected = version ?: run {
                if(pointer == null || now >= pointerExpires) { pointer = repository.currentVersion(); pointerExpires = now.plusSeconds(300) }
                pointer ?: throw OnboardingCatalogFailure(404,"ONBOARDING_CATALOG_NOT_FOUND")
            }
            var entry = versions[selected]
            if(entry == null || now >= entry.first) {
                val p = repository.read(selected) ?: throw OnboardingCatalogFailure(404,"ONBOARDING_CATALOG_NOT_FOUND")
                require(p.catalogVersion == selected); OnboardingCatalogValidation.validate(p,false)
                entry = now.plusSeconds(86400) to p; versions[selected] = entry
            }
            return OnboardingCatalogLocalization.localize(entry.second,locale)
        } catch(e: OnboardingCatalogFailure) { throw e }
        catch(e: Exception) { if(e is InterruptedException) Thread.currentThread().interrupt(); throw OnboardingCatalogFailure(503,"ONBOARDING_CATALOG_UNAVAILABLE") }
    }
}
@Configuration(proxyBeanMethods=false)
class OnboardingCatalogConfiguration {
    @Bean fun onboardingCatalogRepository(db: ObjectProvider<Firestore>): OnboardingCatalogRepository =
        db.ifAvailable?.let { FirestoreOnboardingCatalogRepository(it) } ?: object: OnboardingCatalogRepository {
            override fun currentVersion(): Int? = error("STORAGE_UNAVAILABLE")
            override fun read(version: Int): OnboardingCatalogPublication? = error("STORAGE_UNAVAILABLE")
            override fun publish(publication: OnboardingCatalogPublication) { error("STORAGE_UNAVAILABLE") }
        }
    @Bean fun onboardingCatalogService(repository: OnboardingCatalogRepository) = OnboardingCatalogService(repository)
    @Bean fun onboardingCatalogAccess(db: ObjectProvider<Firestore>) = OnboardingCatalogAccess { identity ->
        try {
            val database = db.ifAvailable ?: throw OnboardingCatalogFailure(503,"ONBOARDING_CATALOG_UNAVAILABLE")
            val player = database.document("players/${identity.uid}").get().get(5,TimeUnit.SECONDS)
            if(!player.exists()) throw OnboardingCatalogFailure(404,"PLAYER_NOT_FOUND")
            if(player.getString("status") != "ACTIVE") throw OnboardingCatalogFailure(403,"PLAYER_NOT_ACTIVE")
        } catch(e: OnboardingCatalogFailure) { throw e }
        catch(e: Exception) { if(e is InterruptedException) Thread.currentThread().interrupt(); throw OnboardingCatalogFailure(503,"ONBOARDING_CATALOG_UNAVAILABLE") }
    }
}
@RestController
class OnboardingCatalogController(private val service: OnboardingCatalogService, private val access: OnboardingCatalogAccess) {
    @GetMapping("/api/v1/onboarding/catalog")
    fun catalog(@AuthenticationPrincipal identity: FirebaseIdentity, @RequestParam(required=false) locale: String?,
        @RequestParam(required=false) version: Int?, request: jakarta.servlet.http.HttpServletRequest): ResponseEntity<*> {
        return try {
            access.check(identity)
            val response = service.read(locale,version)
            val etag = "\"onboarding-${response.catalogVersion}-${response.locale}\""
            val cache = "private, max-age=${if(version == null) 300 else 86400}"
            if(request.getHeader("If-None-Match") == etag) ResponseEntity.status(304).header("ETag",etag).header("Cache-Control",cache).build<Void>()
            else ResponseEntity.ok().header("ETag",etag).header("Cache-Control",cache).body(response)
        } catch(e: OnboardingCatalogFailure) {
            val id = request.getAttribute(com.teamfho.domino.common.RequestIdFilter.ATTRIBUTE)?.toString() ?: java.util.UUID.randomUUID().toString()
            ResponseEntity.status(e.status).header("Cache-Control","no-store").header("X-Request-ID",id)
                .body(mapOf("code" to e.code,"message" to "Onboarding catalog request could not be completed.","requestId" to id))
        }
    }
}
