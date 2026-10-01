package com.teamfho.domino.catalog

import com.google.cloud.firestore.Firestore
import com.teamfho.domino.security.FirebaseIdentity
import org.springframework.beans.factory.ObjectProvider
import org.springframework.context.annotation.Bean
import org.springframework.context.annotation.Configuration
import org.springframework.http.ResponseEntity
import org.springframework.security.core.annotation.AuthenticationPrincipal
import org.springframework.web.bind.annotation.*

class CoachCatalogFailure(val status:Int,val code:String):RuntimeException(code)
class CoachCatalogService(private val repository:CoachCatalogRepository) {
    fun read(locale:String?,version:Int?):CoachCatalogResponse {
        if(version!=null && version<=0)throw CoachCatalogFailure(400,"COACH_VERSION_INVALID")
        try {
            val selected=version ?: repository.currentVersion() ?: throw CoachCatalogFailure(404,"COACH_CATALOG_NOT_FOUND")
            val p=repository.read(selected) ?: throw CoachCatalogFailure(404,"COACH_CATALOG_NOT_FOUND")
            require(p.catalogVersion==selected)
            return CoachCatalogLocalization.localize(p,locale,historical=version!=null)
        } catch(e:CoachCatalogFailure){throw e}
        catch(e:Exception){if(e is InterruptedException)Thread.currentThread().interrupt();throw CoachCatalogFailure(503,"COACH_CATALOG_UNAVAILABLE")}
    }
}
@Configuration(proxyBeanMethods=false)
class CoachCatalogConfiguration {
    @Bean fun coachCatalogRepository(db:ObjectProvider<Firestore>):CoachCatalogRepository =
        db.ifAvailable?.let{FirestoreCoachCatalogRepository(it)} ?: object:CoachCatalogRepository {
            override fun currentVersion():Int?=error("STORAGE_UNAVAILABLE")
            override fun read(version:Int):CoachCatalogPublication?=error("STORAGE_UNAVAILABLE")
            override fun publish(publication:CoachCatalogPublication){error("STORAGE_UNAVAILABLE")}
        }
    @Bean fun coachCatalogService(repository:CoachCatalogRepository)=CoachCatalogService(repository)
}
@RestController
class CoachCatalogController(private val service:CoachCatalogService,private val access:OnboardingCatalogAccess) {
    @GetMapping("/api/v1/coaches")
    fun catalog(@AuthenticationPrincipal identity:FirebaseIdentity,@RequestParam(required=false) locale:String?,
        @RequestParam(required=false) version:Int?,request:jakarta.servlet.http.HttpServletRequest):ResponseEntity<*> {
        try {
            access.check(identity)
            val r=service.read(locale,version)
            val etag="\"coaches-${r.catalogVersion}-${r.resolvedLocale}-${if(version==null)"active" else "historical"}\""
            val cache="private, max-age=${if(version==null)300 else 86400}"
            return if(request.getHeader("If-None-Match")==etag)ResponseEntity.status(304).header("ETag",etag).header("Cache-Control",cache).build<Void>()
                else ResponseEntity.ok().header("ETag",etag).header("Cache-Control",cache).body(r)
        } catch(e:CoachCatalogFailure){return failure(e.status,e.code,request)}
        catch(e:OnboardingCatalogFailure){return failure(e.status,if(e.code=="ONBOARDING_CATALOG_UNAVAILABLE")"COACH_CATALOG_UNAVAILABLE" else e.code,request)}
    }
    private fun failure(status:Int,code:String,r:jakarta.servlet.http.HttpServletRequest):ResponseEntity<*> {
        val id=r.getAttribute(com.teamfho.domino.common.RequestIdFilter.ATTRIBUTE)?.toString() ?: java.util.UUID.randomUUID().toString()
        return ResponseEntity.status(status).header("Cache-Control","no-store").header("X-Request-ID",id)
            .body(mapOf("code" to code,"message" to "Coach catalog request could not be completed.","requestId" to id))
    }
}
