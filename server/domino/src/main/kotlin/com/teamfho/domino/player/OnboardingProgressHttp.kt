package com.teamfho.domino.player

import com.google.cloud.firestore.Firestore
import com.teamfho.domino.catalog.GameCatalogCodec
import com.teamfho.domino.security.FirebaseIdentity
import jakarta.servlet.http.HttpServletRequest
import org.springframework.beans.factory.ObjectProvider
import org.springframework.context.annotation.Bean
import org.springframework.context.annotation.Configuration
import org.springframework.http.ResponseEntity
import org.springframework.security.core.annotation.AuthenticationPrincipal
import org.springframework.web.bind.annotation.*

@Configuration(proxyBeanMethods=false)
class OnboardingProgressConfiguration {
    @Bean fun onboardingProgressRepository(db:ObjectProvider<Firestore>):OnboardingProgressRepository =
        db.ifAvailable?.let{FirestoreOnboardingProgressRepository(it)} ?: object:OnboardingProgressRepository {
            override fun <T> transaction(action:(OnboardingProgressTransaction)->T):T=throw OnboardingFailure("DEPENDENCY_UNAVAILABLE",503)
        }
    @Bean fun onboardingProgressService(repository:OnboardingProgressRepository)=OnboardingProgressService(repository,com.teamfho.domino.catalog.AuthoritativeCoachValidation())
}
@RestController
@RequestMapping("/api/v1/player/onboarding")
class OnboardingProgressController(private val service:OnboardingProgressService) {
    private fun <T> body(request:HttpServletRequest,type:Class<T>):T {
        val bytes=request.inputStream.readNBytes(16385)
        onboardingCheck(bytes.size<=16384,"REQUEST_INVALID",400)
        try { return GameCatalogCodec.mapper.readValue(bytes,type) }
        catch(e:Exception){throw OnboardingFailure("REQUEST_INVALID",400)}
    }
    private fun <T> ok(value:T)=ResponseEntity.ok().header("Cache-Control","no-store").body(value)
    @GetMapping fun get(@AuthenticationPrincipal identity:FirebaseIdentity)=ok(service.get(identity))
    @PostMapping("/start",consumes=["application/json"])
    fun start(@AuthenticationPrincipal identity:FirebaseIdentity,request:HttpServletRequest)=ok(service.start(identity,body(request,OnboardingStartRequest::class.java)))
    @PutMapping("/steps/{stepKey}",consumes=["application/json"])
    fun save(@AuthenticationPrincipal identity:FirebaseIdentity,@PathVariable stepKey:String,request:HttpServletRequest)=ok(service.save(identity,stepKey,body(request,SaveStepRequest::class.java)))
    @PutMapping("/cursor",consumes=["application/json"])
    fun cursor(@AuthenticationPrincipal identity:FirebaseIdentity,request:HttpServletRequest)=ok(service.cursor(identity,body(request,OnboardingCursorRequest::class.java)))
    @PostMapping("/complete",consumes=["application/json"])
    fun complete(@AuthenticationPrincipal identity:FirebaseIdentity,request:HttpServletRequest)=ok(service.complete(identity,body(request,OnboardingCompleteRequest::class.java)))
    @ExceptionHandler(OnboardingFailure::class)
    fun failure(e:OnboardingFailure,request:HttpServletRequest):ResponseEntity<*> {
        val id=request.getAttribute(com.teamfho.domino.common.RequestIdFilter.ATTRIBUTE)?.toString() ?: java.util.UUID.randomUUID().toString()
        return ResponseEntity.status(e.status).header("Cache-Control","no-store").header("X-Request-ID",id)
            .body(mapOf("code" to e.code,"message" to "Onboarding request could not be completed.","requestId" to id))
    }
}
