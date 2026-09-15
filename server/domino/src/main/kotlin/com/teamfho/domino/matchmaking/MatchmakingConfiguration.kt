package com.teamfho.domino.matchmaking

import com.teamfho.domino.catalog.GameCatalogService
import com.teamfho.domino.online.OnlineMatchService
import com.teamfho.domino.security.FirebaseIdentity
import org.springframework.context.annotation.Bean
import org.springframework.context.annotation.Configuration
import org.springframework.data.redis.core.StringRedisTemplate
import org.springframework.scheduling.annotation.Scheduled
import org.springframework.security.core.annotation.AuthenticationPrincipal
import org.springframework.web.bind.annotation.*
import org.springframework.http.ResponseEntity

@Configuration(proxyBeanMethods=false)
class MatchmakingConfiguration {
    @Bean fun matchmakingStore(redis:StringRedisTemplate):MatchmakingStore=RedisMatchmakingStore(redis)
    @Bean fun matchmakingService(store:MatchmakingStore,catalog:GameCatalogService,online:OnlineMatchService,registry:io.micrometer.core.instrument.MeterRegistry)=MatchmakingService(store,catalog,online,MatchmakingMetrics(store,registry))
    @Bean fun matchmakingWorker(service:MatchmakingService)=MatchmakingWorker(service)
    @Bean fun matchmakingScheduler()=org.springframework.scheduling.concurrent.ThreadPoolTaskScheduler().apply{poolSize=1;setThreadNamePrefix("matchmaking-")}
}
class MatchmakingWorker(private val service:MatchmakingService) {
    @Scheduled(fixedDelay=1000,scheduler="matchmakingScheduler") fun tick()=service.tick()
}
@RestController
class MatchmakingController(private val service:MatchmakingService) {
    @PostMapping("/api/v1/matchmaking/queue")
    fun join(@AuthenticationPrincipal identity:FirebaseIdentity,@RequestBody body:Map<String,String>):QueueStatus {
        if(body.keys!=setOf("modeKey"))throw MatchmakingFailure("INVALID_REQUEST")
        return service.join(identity.uid,body.getValue("modeKey"))
    }
    @DeleteMapping("/api/v1/matchmaking/queue") fun leave(@AuthenticationPrincipal identity:FirebaseIdentity)=service.leave(identity.uid)
    @GetMapping("/api/v1/matchmaking/queue") fun status(@AuthenticationPrincipal identity:FirebaseIdentity)=service.status(identity.uid)
    @GetMapping("/api/v1/matches/active") fun active(@AuthenticationPrincipal identity:FirebaseIdentity)=service.active(identity.uid)
    @ExceptionHandler(MatchmakingFailure::class) fun failure(e:MatchmakingFailure):ResponseEntity<*> = ResponseEntity.status(if(e.code=="RATE_LIMIT")429 else 409).body(mapOf("code" to e.code))
    @ExceptionHandler(org.springframework.dao.DataAccessException::class) fun unavailable():ResponseEntity<*> = ResponseEntity.status(503).body(mapOf("code" to "MATCHMAKING_UNAVAILABLE"))
}
