package com.teamfho.domino.entitlement

import com.google.cloud.firestore.Firestore
import com.teamfho.domino.match.MatchCodec
import com.teamfho.domino.player.FirestoreFoundationMapping
import com.teamfho.domino.player.FoundationTimestamp
import com.teamfho.domino.security.FirebaseIdentity
import org.springframework.beans.factory.ObjectProvider
import org.springframework.boot.context.properties.EnableConfigurationProperties
import org.springframework.context.annotation.Bean
import org.springframework.context.annotation.Configuration
import org.springframework.http.ResponseEntity
import org.springframework.security.core.annotation.AuthenticationPrincipal
import org.springframework.web.bind.annotation.*
import java.util.concurrent.TimeUnit

@Configuration(proxyBeanMethods=false)
@EnableConfigurationProperties(SubscriptionPolicy::class)
class EntitlementConfiguration {
    @Bean fun subscriptionPolicyService(db:ObjectProvider<Firestore>,fallback:SubscriptionPolicy)=SubscriptionPolicyService({
        val store=db.ifAvailable?:throw IllegalStateException("STORAGE_UNAVAILABLE")
        store.document("systemConfig/subscriptionPolicy").get().get(5,TimeUnit.SECONDS).data?.let {MatchCodec.read(it,SubscriptionPolicy::class.java)}
    },fallback)
    @Bean fun entitlementRepository(db:ObjectProvider<Firestore>):EntitlementRepository=object:EntitlementRepository {
        fun ready()=FirestoreEntitlements(db.ifAvailable?:throw IllegalStateException("STORAGE_UNAVAILABLE"))
        override fun read(uid:String)=ready().read(uid)
        override fun trial(uid:String,now:java.time.Instant,policy:SubscriptionPolicy)=ready().trial(uid,now,policy)
        override fun adminGrant(uid:String,grant:EntitlementGrant)=ready().adminGrant(uid,grant)
    }
    @Bean fun entitlementService(policy:SubscriptionPolicyService,repository:EntitlementRepository)=EntitlementService(policy,repository)
}

@RestController
class EntitlementController(private val entitlements:EntitlementService,private val db:ObjectProvider<Firestore>) {
    @GetMapping("/api/v1/player/entitlements")
    fun entitlements(@AuthenticationPrincipal identity:FirebaseIdentity):ResponseEntity<*> {
        val result=entitlements.summary(identity.uid)
        return ResponseEntity.status(if(result.availability=="AVAILABLE")200 else 503).body(result)
    }
    @GetMapping("/api/v1/player/profile")
    fun profile(@AuthenticationPrincipal identity:FirebaseIdentity):ResponseEntity<*> = try {
        val data=db.ifAvailable?.document("players/${identity.uid}")?.get()?.get(5,TimeUnit.SECONDS)?.data
        if(data==null) ResponseEntity.status(404).body(mapOf("code" to "PROFILE_NOT_FOUND")) else {
            val p=FirestoreFoundationMapping.player(data,identity.uid)
            ResponseEntity.ok(mapOf("uid" to p.uid,"displayName" to p.displayName,"language" to p.language,
                "accountType" to p.accountType,"createdAt" to (p.createdAt as FoundationTimestamp.Recorded).instant))
        }
    } catch(_:Exception) {ResponseEntity.status(503).body(mapOf("code" to "PROFILE_UNAVAILABLE"))}
}

@RestControllerAdvice
@org.springframework.core.annotation.Order(org.springframework.core.Ordered.HIGHEST_PRECEDENCE)
class EntitlementErrors {
    @ExceptionHandler(org.springframework.web.HttpRequestMethodNotSupportedException::class)
    fun methodNotAllowed()=ResponseEntity.status(405).body(mapOf("code" to "METHOD_NOT_ALLOWED"))
    @ExceptionHandler(EntitlementFailure::class)
    fun error(e:EntitlementFailure)=ResponseEntity.status(if(e.code=="ENTITLEMENTS_UNAVAILABLE")503 else 403)
        .body(mapOf("code" to e.code,"feature" to e.feature,"currentPlan" to e.currentPlan,
            "limitKey" to e.limitKey,"current" to e.current,"maximum" to e.maximum))
}
