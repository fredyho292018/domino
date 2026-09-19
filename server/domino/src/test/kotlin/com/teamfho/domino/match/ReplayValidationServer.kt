package com.teamfho.domino.match

import com.teamfho.domino.DominoApplication
import com.teamfho.domino.catalog.GameCatalogCodec
import com.teamfho.domino.online.OnlineState
import com.teamfho.domino.security.*
import com.teamfho.domino.common.ApiErrorCode
import org.springframework.boot.builder.SpringApplicationBuilder
import org.springframework.boot.test.context.TestConfiguration
import org.springframework.context.annotation.Bean
import org.springframework.context.annotation.Primary
import java.nio.file.*

/** Explicit test-classpath-only loopback host. Production bootJar cannot contain this verifier.
 * Uses retained I3.1 archives without mutation; no Firebase/Admin/Firestore client is created. */
object ReplayValidationServer {
    data class Fixture(val match:Match,val events:List<MatchEvent>,val rounds:List<MatchRound>)
    lateinit var fixtures:List<Fixture>
    lateinit var participant:String
    @JvmStatic fun main(args:Array<String>) {
        check(System.getenv("DOMINO_I4_LOOPBACK")=="true")
        check(System.getenv("DOMINO_REAL_FIRESTORE_TESTS")!="true")
        val dir=Path.of("../../client/Validation/Generated/I4Tests")
        fixtures=listOf("DUEL_1V1","PARTNERS_2V2_ONLINE").map{key->
            val json=GameCatalogCodec.mapper.readTree(Files.readString(dir.resolve("$key.json")))
            val state=GameCatalogCodec.mapper.treeToValue(json["expected"],OnlineState::class.java)
            Fixture(state.match,json["events"].toList().map{GameCatalogCodec.mapper.treeToValue(it,MatchEvent::class.java)},json["manifest"]["rounds"].toList().map{GameCatalogCodec.mapper.treeToValue(it,MatchRound::class.java)})
        }
        participant=fixtures.map{it.match.participants.mapNotNull{p->p.playerUid}.toSet()}.reduce{a,b->a.intersect(b)}.first()
        val stop=Path.of("build/i4-loopback.stop");check(!Files.exists(stop)){"Use a fresh validation lifecycle"}
        SpringApplicationBuilder(DominoApplication::class.java,Beans::class.java).run(
            "--firebase.enabled=false","--server.address=127.0.0.1","--server.port=18087",
            "--domino.realtime.enabled=false").use {
            Files.writeString(Path.of("build/i4-loopback.ready"),"READY")
            try{while(!Files.exists(stop))Thread.sleep(500)}finally{Files.deleteIfExists(Path.of("build/i4-loopback.ready"))}
        }
    }
    @TestConfiguration(proxyBeanMethods=false)
    @org.springframework.context.annotation.Import(com.teamfho.domino.player.FakePlayerFoundationConfiguration::class)
    class Beans {
        @Bean @Primary fun verifier()=FirebaseTokenVerifier {token->
            if(token!="i4-loopback-participant")throw AuthFailure(ApiErrorCode.AUTH_TOKEN_INVALID)
            FirebaseIdentity(participant,true)
        }
        @Bean @Primary fun source()=object:ReplaySource {
            override fun match(id:String)=fixtures.singleOrNull{it.match.matchId==id}?.match
            override fun events(id:String,after:Long,limit:Int)=fixtures.single{it.match.matchId==id}.events.filter{it.sequence>after}.take(limit)
            override fun round(id:String,number:Int)=fixtures.single{it.match.matchId==id}.rounds.singleOrNull{it.roundNumber==number}
        }
        @Bean @Primary fun history()=object:PlayerMatchHistoryRepository {
            override fun history(uid:String,limit:Int,afterMatchId:String?):HistoryPage {
                val items=fixtures.filter{it.match.participants.any{p->p.playerUid==uid}}.map {f->val m=f.match
                    val seat=m.participants.single{it.playerUid==uid};val owner=seat.teamId?:seat.seatIndex
                    PlayerMatchHistory(m.matchId,m.modeKey,m.ruleSetId,m.ruleSetVersion,if(m.result!!.winner?.index==owner)HistoryResult.WIN else HistoryResult.LOSS,
                        m.score,m.participants.filter{it.playerUid!=uid}.map{OpponentSummary(it.seatIndex,it.displayNameSnapshot,it.teamId)},m.startedAt,m.finishedAt!!,m.result.finishReason,true)
                }.sortedByDescending{it.finishedAt}
                val start=if(afterMatchId==null)0 else items.indexOfFirst{it.matchId==afterMatchId}+1
                val page=items.drop(start).take(limit+1)
                return HistoryPage(page.take(limit),if(page.size>limit)page[limit-1].matchId else null)
            }
        }
    }
}
