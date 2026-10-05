package com.teamfho.domino.entitlement

import com.teamfho.domino.catalog.GameCatalogCodec
import com.teamfho.domino.player.OnboardingFailure
import com.teamfho.domino.player.onboardingCheck
import com.teamfho.domino.security.FirebaseIdentity
import jakarta.servlet.http.HttpServletRequest
import jakarta.servlet.http.HttpServletResponse
import org.springframework.http.ResponseEntity
import org.springframework.web.bind.annotation.*
import org.springframework.security.core.annotation.AuthenticationPrincipal

@RestController
class TrialActivationController(private val entitlements:EntitlementService) {
    @PostMapping("/api/v1/player/trial/activate",consumes=["application/json"])
    fun activate(@AuthenticationPrincipal identity:FirebaseIdentity,request:HttpServletRequest):ResponseEntity<*> {
        val bytes=request.inputStream.readNBytes(16385)
        onboardingCheck(bytes.size<=16384,"REQUEST_INVALID",400)
        val body=try {
            val tree=GameCatalogCodec.mapper.readTree(bytes)
            require(tree.isObject && tree.get("operationId")?.isString==true && tree.get("expectedPolicyVersion")?.isIntegralNumber==true)
            require(tree.properties().map{it.key}.toSet() in setOf(setOf("operationId","expectedPolicyVersion"),setOf("operationId","expectedPolicyVersion","plan","billingPeriod")))
            if(tree.size()==4)require(tree.get("plan")?.isString==true && tree.get("billingPeriod")?.isString==true)
            GameCatalogCodec.mapper.readValue(bytes,TrialActivationRequest::class.java)
        } catch(_:Exception){throw OnboardingFailure("REQUEST_INVALID",400)}
        return ResponseEntity.ok().header("Cache-Control","no-store").body(entitlements.activateTrial(identity,body))
    }
    @ExceptionHandler(OnboardingFailure::class)
    fun failure(e:OnboardingFailure,request:HttpServletRequest):ResponseEntity<*> {
        val id=request.getAttribute(com.teamfho.domino.common.RequestIdFilter.ATTRIBUTE)?.toString() ?: java.util.UUID.randomUUID().toString()
        return ResponseEntity.status(e.status).header("Cache-Control","no-store").header("X-Request-ID",id)
            .body(mapOf("code" to e.code,"message" to "Trial activation could not be completed.","requestId" to id))
    }
}

// Compatibility signal only, never authentication or permission to grant automatically.
class TrialClientCompatibilityGuard(private val required:Boolean=true):org.springframework.web.servlet.HandlerInterceptor {
    override fun preHandle(request:HttpServletRequest,response:HttpServletResponse,handler:Any):Boolean {
        if(!required || request.getHeader("X-Trial-Activation-Contract")=="1")return true
        val id=request.getAttribute(com.teamfho.domino.common.RequestIdFilter.ATTRIBUTE)?.toString() ?: java.util.UUID.randomUUID().toString()
        response.status=409;response.contentType="application/json";response.setHeader("Cache-Control","no-store");response.setHeader("X-Request-ID",id)
        response.writer.write(GameCatalogCodec.json(mapOf("code" to "CLIENT_UPDATE_REQUIRED","message" to "Update the client to continue.","requestId" to id)))
        return false
    }
}
@org.springframework.context.annotation.Configuration(proxyBeanMethods=false)
class TrialClientCompatibilityConfiguration(@org.springframework.beans.factory.annotation.Value("\${domino.trial.require-compatible-client:true}") private val required:Boolean):org.springframework.web.servlet.config.annotation.WebMvcConfigurer {
    override fun addInterceptors(registry:org.springframework.web.servlet.config.annotation.InterceptorRegistry) {
        registry.addInterceptor(TrialClientCompatibilityGuard(required)).addPathPatterns("/api/v1/player/bootstrap","/api/v1/player/trial/activate")
    }
}
