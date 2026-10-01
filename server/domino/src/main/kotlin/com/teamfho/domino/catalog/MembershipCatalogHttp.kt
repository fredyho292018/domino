package com.teamfho.domino.catalog

import com.google.cloud.firestore.Firestore
import com.teamfho.domino.security.FirebaseIdentity
import org.springframework.beans.factory.ObjectProvider
import org.springframework.context.annotation.Bean
import org.springframework.context.annotation.Configuration
import org.springframework.http.ResponseEntity
import org.springframework.security.core.annotation.AuthenticationPrincipal
import org.springframework.web.bind.annotation.*

class MembershipCatalogFailure(val status:Int,val code:String):RuntimeException(code)
class MembershipCatalogService(private val repository:MembershipCatalogRepository) {
    fun read(locale:String?,version:Int?):MembershipCatalogResponse {
        if(version!=null && version<=0)throw MembershipCatalogFailure(400,"MEMBERSHIP_VERSION_INVALID")
        try {
            val selected=version ?: repository.currentVersion() ?: throw MembershipCatalogFailure(404,"MEMBERSHIP_CATALOG_NOT_FOUND")
            val p=repository.read(selected) ?: throw MembershipCatalogFailure(404,"MEMBERSHIP_CATALOG_NOT_FOUND")
            require(p.catalogVersion==selected)
            return MembershipCatalogLocalization.localize(p,locale,historical=version!=null)
        } catch(e:MembershipCatalogFailure){throw e}
        catch(e:Exception){if(e is InterruptedException)Thread.currentThread().interrupt();throw MembershipCatalogFailure(503,"MEMBERSHIP_CATALOG_UNAVAILABLE")}
    }
}
@Configuration(proxyBeanMethods=false)
class MembershipCatalogConfiguration {
    @Bean fun membershipCatalogRepository(db:ObjectProvider<Firestore>):MembershipCatalogRepository =
        db.ifAvailable?.let{FirestoreMembershipCatalogRepository(it)} ?: object:MembershipCatalogRepository {
            override fun currentVersion():Int?=error("STORAGE_UNAVAILABLE")
            override fun read(version:Int):MembershipCatalogPublication?=error("STORAGE_UNAVAILABLE")
            override fun publish(publication:MembershipCatalogPublication){error("STORAGE_UNAVAILABLE")}
        }
    @Bean fun membershipCatalogService(repository:MembershipCatalogRepository)=MembershipCatalogService(repository)
}
@RestController
class MembershipCatalogController(private val service:MembershipCatalogService,private val access:OnboardingCatalogAccess) {
    @ExceptionHandler(org.springframework.web.method.annotation.MethodArgumentTypeMismatchException::class)
    fun invalidQuery(request:jakarta.servlet.http.HttpServletRequest):ResponseEntity<*> = failure(400,"MEMBERSHIP_VERSION_INVALID",request)
    @GetMapping("/api/v1/membership/catalog")
    fun catalog(@AuthenticationPrincipal identity:FirebaseIdentity,@RequestParam(required=false) locale:String?,
        @RequestParam(required=false) version:Int?,request:jakarta.servlet.http.HttpServletRequest):ResponseEntity<*> {
        try {
            access.check(identity)
            val r=service.read(locale,version)
            val etag="\"membership/catalog-${r.catalogVersion}-${r.resolvedLocale}-${if(version==null)"active" else "historical"}\""
            val cache="private, max-age=${if(version==null)300 else 86400}"
            return if(request.getHeader("If-None-Match")==etag)ResponseEntity.status(304).header("ETag",etag).header("Cache-Control",cache).build<Void>()
                else ResponseEntity.ok().header("ETag",etag).header("Cache-Control",cache).body(r)
        } catch(e:MembershipCatalogFailure){return failure(e.status,e.code,request)}
        catch(e:OnboardingCatalogFailure){return failure(e.status,if(e.code=="ONBOARDING_CATALOG_UNAVAILABLE")"MEMBERSHIP_CATALOG_UNAVAILABLE" else e.code,request)}
    }
    private fun failure(status:Int,code:String,r:jakarta.servlet.http.HttpServletRequest):ResponseEntity<*> {
        val id=r.getAttribute(com.teamfho.domino.common.RequestIdFilter.ATTRIBUTE)?.toString() ?: java.util.UUID.randomUUID().toString()
        return ResponseEntity.status(status).header("Cache-Control","no-store").header("X-Request-ID",id)
            .body(mapOf("code" to code,"message" to "Membership catalog request could not be completed.","requestId" to id))
    }
}


