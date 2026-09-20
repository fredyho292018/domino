package com.teamfho.domino.social

import com.teamfho.domino.security.FakeAuthConfiguration
import com.teamfho.domino.player.FakePlayerFoundationConfiguration
import org.junit.jupiter.api.Test
import org.springframework.beans.factory.annotation.Autowired
import org.springframework.boot.test.context.SpringBootTest
import org.springframework.boot.test.context.TestConfiguration
import org.springframework.boot.webmvc.test.autoconfigure.AutoConfigureMockMvc
import org.springframework.context.annotation.*
import org.springframework.test.context.ActiveProfiles
import org.springframework.test.web.servlet.MockMvc
import org.springframework.test.web.servlet.request.MockMvcRequestBuilders.*
import org.springframework.test.web.servlet.result.MockMvcResultMatchers.*

@SpringBootTest
@ActiveProfiles("test")
@AutoConfigureMockMvc
@Import(FakeAuthConfiguration::class,FakePlayerFoundationConfiguration::class,SocialHttpTests.Fixture::class)
class SocialHttpTests {
    @Autowired lateinit var mvc:MockMvc
    @TestConfiguration(proxyBeanMethods=false) class Fixture {
        @Bean @Primary fun socialTestServices():SocialServices=memoryServices()
        @Bean @Primary fun socialTestRate()=SocialRateLimiter{_,_->}
    }
    @Test fun `all social routes require identity`() {
        for(path in listOf("player/social-summary","player/social-settings","player/blocks","players/search?mode=NAME&q=ali","players/aaaaaaaaaaaaaaaaaaaaaa/profile"))
            mvc.perform(get("/api/v1/$path")).andExpect(status().isUnauthorized)
    }
    @Test fun `public summary and search contain no private authority`() {
        mvc.perform(get("/api/v1/player/social-summary").header("Authorization","Bearer valid-guest"))
            .andExpect(status().isOk).andExpect(jsonPath("$.profile.publicPlayerId").exists()).andExpect(jsonPath("$.profile.uid").doesNotExist())
            .andExpect(jsonPath("$.privacy.presenceVisibility").value("FRIENDS"))
        mvc.perform(get("/api/v1/players/search?mode=NAME&q=ali").header("Authorization","Bearer valid-guest"))
            .andExpect(status().isOk).andExpect(jsonPath("$.items[0].friendCode").exists()).andExpect(jsonPath("$.items[0].uid").doesNotExist())
    }
    @Test fun `malformed and forged privacy fields rejected`() {
        mvc.perform(patch("/api/v1/player/social-settings").header("Authorization","Bearer valid-guest").contentType("application/json")
            .content("""{"discoverableByName":true,"revision":1,"sourceUid":"victim"}""")).andExpect(status().isBadRequest)
        mvc.perform(get("/api/v1/players/search?mode=NAME&q=ab").header("Authorization","Bearer valid-guest")).andExpect(status().isBadRequest)
        mvc.perform(get("/api/v1/players/invalid/profile").header("Authorization","Bearer valid-guest"))
            .andExpect(status().isNotFound).andExpect(jsonPath("$.code").value("PLAYER_NOT_FOUND"))
    }
    @Test fun `authenticated block is idempotent and profile denial is generic`() {
        val target="0000000000000000000001"
        mvc.perform(post("/api/v1/players/$target/block")).andExpect(status().isUnauthorized)
        repeat(2){mvc.perform(post("/api/v1/players/$target/block").header("Authorization","Bearer valid-guest").contentType("application/json").content("""{"sourceUid":"victim"}""")).andExpect(status().isOk)}
        mvc.perform(get("/api/v1/player/blocks").header("Authorization","Bearer valid-guest")).andExpect(status().isOk)
            .andExpect(jsonPath("$.items[0].publicPlayerId").value(target)).andExpect(jsonPath("$.items[0].uid").doesNotExist())
        mvc.perform(get("/api/v1/players/$target/profile").header("Authorization","Bearer valid-guest"))
            .andExpect(status().isNotFound).andExpect(jsonPath("$.code").value("PLAYER_NOT_FOUND"))
        repeat(2){mvc.perform(delete("/api/v1/players/$target/block").header("Authorization","Bearer valid-guest")).andExpect(status().isOk)}
        mvc.perform(get("/api/v1/players/$target/profile").header("Authorization","Bearer valid-guest")).andExpect(status().isOk)
    }
    companion object {
        fun memoryServices():SocialServices {
            val r=MemorySocial();val cursor=SocialCursor();val identity=PublicPlayerIdentityService(r);val access=SocialAccess(r,r);val privacy=SocialPrivacyService(identity,r)
            for(i in 1..25)r.ensure("local-social-$i",PublicPlayerIdentity("local-social-$i",i.toString().padStart(22,'0'),"FHO-"+i.toString().padStart(12,'0')))
            val bundle=SocialServices.Bundle(identity,PublicPlayerProfileService(identity,r,privacy,access),PlayerDiscoveryService(r,r,access,cursor),privacy,BlockService(r,access,cursor))
            return object:SocialServices({throw IllegalStateException("NO_FIRESTORE")},cursor){override fun ready()=bundle}
        }
    }
}
