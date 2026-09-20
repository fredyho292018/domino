package com.teamfho.domino.entitlement

import com.teamfho.domino.DominoApplication
import com.teamfho.domino.match.*
import com.teamfho.domino.security.*
import com.teamfho.domino.common.ApiErrorCode
import org.springframework.boot.builder.SpringApplicationBuilder
import org.springframework.boot.test.context.TestConfiguration
import org.springframework.context.annotation.*
import org.springframework.web.bind.annotation.*
import java.nio.file.*

/** Test classpath only, exact loopback, no Firestore/Firebase client. Never packaged in production. */
object EntitlementValidationServer {
    val clock=EntitlementClock();val repo=MemoryEntitlements()
    var policy=SubscriptionPolicy(promotionalTrialEnabled=false)
    val policies=SubscriptionPolicyService({policy},clock=clock)
    val service=EntitlementService(policies,repo,clock)
    val fixture=EntitlementHistoryTests()
    @JvmStatic fun main(args:Array<String>) {
        check(System.getenv("DOMINO_P01_LOOPBACK")=="true")
        check(System.getenv("DOMINO_REAL_FIRESTORE_TESTS")!="true")
        val stop=Path.of("build/p01-loopback.stop");check(!Files.exists(stop))
        SpringApplicationBuilder(DominoApplication::class.java,Beans::class.java,Scenario::class.java).run(
            "--firebase.enabled=false","--server.address=127.0.0.1","--server.port=18088","--domino.realtime.enabled=false").use {
            Files.writeString(Path.of("build/p01-loopback.ready"),"READY")
            try{while(!Files.exists(stop))Thread.sleep(250)}finally{Files.deleteIfExists(Path.of("build/p01-loopback.ready"))}
        }
    }
    @RestController class Scenario {
        @PostMapping("/api/v1/validation/scenario/{value}") fun scenario(@PathVariable value:String):Map<String,String> {
            require(value in setOf("FREE","TRIAL","PREMIUM","EXPIRED"))
            repo.data.clear();service.invalidate("p0")
            policy=policy.copy(policyVersion=policy.policyVersion+1,promotionalTrialEnabled=value=="TRIAL");policies.invalidate()
            if(value=="PREMIUM")service.adminGrant("p0",EntitlementGrant("admin",EntitlementSource.ADMIN_GRANT,validFrom=clock.now,
                validUntil=clock.now.plusSeconds(86400),createdAt=clock.now,policyVersion=policy.policyVersion,reason="LOCAL_VALIDATION",grantedBy="fixture"))
            if(value=="EXPIRED") {
                repo.trial("p0",clock.now.minusSeconds(8*86400),SubscriptionPolicy());service.invalidate("p0")
            }
            return mapOf("scenario" to value)
        }
    }
    @TestConfiguration(proxyBeanMethods=false)
    @Import(com.teamfho.domino.player.FakePlayerFoundationConfiguration::class)
    class Beans {
        @Bean @Primary fun verifier()=FirebaseTokenVerifier{token->
            if(token!="p01-loopback")throw AuthFailure(ApiErrorCode.AUTH_TOKEN_INVALID)
            FirebaseIdentity("p0",true)
        }
        @Bean @Primary fun entitlements()=service
        @Bean @Primary fun history():PlayerMatchHistoryRepository=fixture.history
        @Bean @Primary fun source():ReplaySource=fixture.source
    }
}
