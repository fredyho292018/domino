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
        private val db=MemoryFriendships()
        @Bean @Primary fun verifier()=FirebaseTokenVerifier{token->
            if(token !in setOf("s11-loopback","s12-loopback-b","s12-loopback-limit"))throw AuthFailure(ApiErrorCode.AUTH_TOKEN_INVALID)
            FirebaseIdentity(when(token){"s11-loopback"->"s11-local-viewer";"s12-loopback-b"->"local-social-1";else->"local-social-2"},true)
        }
        @Bean @Primary fun socialValidationServices()=SocialHttpTests.memoryServices(db).also {
            db.docs["players/local-social-2/socialCounters/current"]=com.teamfho.domino.match.MatchCodec.map(SocialCounters(friendCount=20))
            val now=java.time.Instant.now()
            for(n in 3..22) {
                val a="local-social-2";val b="local-social-$n";val pair=SocialPairIdentity.id(a,b)
                db.docs["friendships/$pair"]=com.teamfho.domino.match.MatchCodec.map(Friendship(pair,minOf(a,b),maxOf(a,b),now,"local-fixture",1))
                db.docs["players/$a/friends/$b"]=mapOf("pairId" to pair,"friendPublicPlayerId" to n.toString().padStart(22,'0'),"friendsSince" to now.toString(),"sortTime" to now.toEpochMilli())
                db.docs["players/$b/friends/$a"]=mapOf("pairId" to pair,"friendPublicPlayerId" to "2".padStart(22,'0'),"friendsSince" to now.toString(),"sortTime" to now.toEpochMilli())
                db.docs["players/$b/socialCounters/current"]=com.teamfho.domino.match.MatchCodec.map(SocialCounters(friendCount=1))
            }
        }
        @Bean @Primary fun friendValidationServices()=FriendshipServices{FriendshipService(db,SocialCursor())}
        @Bean @Primary fun socialValidationRate()=SocialRateLimiter{_,_->}
    }
}
