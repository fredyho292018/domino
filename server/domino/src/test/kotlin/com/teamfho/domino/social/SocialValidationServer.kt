package com.teamfho.domino.social

import com.teamfho.domino.DominoApplication
import com.teamfho.domino.security.*
import com.teamfho.domino.common.ApiErrorCode
import org.springframework.boot.builder.SpringApplicationBuilder
import org.springframework.boot.test.context.TestConfiguration
import org.springframework.context.annotation.*
import java.nio.file.*

/** Local validation host, never part of the production application artifact. */
object SocialValidationServer {
    @JvmStatic fun main(args:Array<String>) {
        check(System.getenv("DOMINO_S11_LOOPBACK")=="true" && System.getenv("DOMINO_REAL_FIRESTORE_TESTS")!="true")
        val stop=Path.of("build/s11-loopback.stop");check(!Files.exists(stop))
        SpringApplicationBuilder(DominoApplication::class.java,Beans::class.java).run("--firebase.enabled=false","--server.address=127.0.0.1","--server.port=18089").use {
            Files.writeString(Path.of("build/s11-loopback.ready"),"READY")
            try{while(!Files.exists(stop))Thread.sleep(250)}finally{Files.deleteIfExists(Path.of("build/s11-loopback.ready"))}
        }
    }
    @TestConfiguration(proxyBeanMethods=false)
    @Import(com.teamfho.domino.player.FakePlayerFoundationConfiguration::class)
    class Beans {
        @Bean @Primary fun verifier()=FirebaseTokenVerifier{token->
            if(token!="s11-loopback")throw AuthFailure(ApiErrorCode.AUTH_TOKEN_INVALID)
            FirebaseIdentity("s11-local-viewer",true)
        }
        @Bean @Primary fun socialValidationServices()=SocialHttpTests.memoryServices()
        @Bean @Primary fun socialValidationRate()=SocialRateLimiter{_,_->}
    }
}
