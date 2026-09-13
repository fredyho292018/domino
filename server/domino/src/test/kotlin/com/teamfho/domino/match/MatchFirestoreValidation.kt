package com.teamfho.domino.match

import com.google.auth.oauth2.GoogleCredentials
import com.google.cloud.firestore.FirestoreOptions
import com.teamfho.domino.catalog.*
import com.teamfho.domino.config.FirebaseProperties
import org.springframework.boot.WebApplicationType
import org.springframework.boot.builder.SpringApplicationBuilder
import java.time.Instant
import java.util.UUID
import java.util.concurrent.Executors

// Explicit integration executable, never run at startup or during ordinary tests.
// Synthetic namespace + validationData=true on root/history; no Firebase user, wallet or ledger writes.
object MatchFirestoreValidation {
    @JvmStatic fun main(args: Array<String>) {
        require(args.contains("--validate-m4"))
        SpringApplicationBuilder(CatalogSeedConfiguration::class.java).web(WebApplicationType.NONE).logStartupInfo(false)
            .run(*args.filter {it!="--validate-m4"}.toTypedArray()).use {context->
                val project=context.getBean(FirebaseProperties::class.java).projectId
                FirestoreOptions.newBuilder().setProjectId(project).setCredentials(GoogleCredentials.getApplicationDefault()).build().service.use {db->
                    val repo=FirestoreMatchRepository(db)
                    val inspect=args.singleOrNull {it.startsWith("--inspect-match=")}?.substringAfter('=')
                    if(inspect!=null) {
                        require(inspect.startsWith("m4-validation-"));val stored=repo.read(inspect)!!;require(stored.validationData && stored.status==MatchStatus.FINISHED)
                        val events=repo.readTrustedEvents(inspect,0)
                        check(events.map {it.sequence}==(1L..stored.lastSequence).toList())
                        check(events.count {it.causedByCommandId=="same-command"}==1)
                        check(repo.round(inspect,1)!!.finishType==FinishType.BLOCKED && repo.round(inspect,2)!!.status==RoundStatus.FINISHED)
                        stored.participants.forEach {p->check(db.document("matches/$inspect/players/${p.seatIndex}").get().get().exists())
                            check(repo.history(p.playerUid!!,20,null).items.single().validationData)}
                        check(MatchReplayReducer.reconstruct(stored,events).publicState.scores==listOf(150,0))
                        check(MatchViewService().publicEvents(stored,events).none {it.payload is HandDealt})
                        println("REAL_FIRESTORE_M4=PASS MATCH_ID=$inspect EVENTS=${events.size} PARTICIPANTS=2 ROUNDS=2 HISTORY=2 IDEMPOTENT=PASS DATA_MARKED_VALIDATION=true INSPECTION_ONLY=true WALLET_WRITES=0")
                        return
                    }
                    val catalog=GameCatalogService(FirestoreGameCatalogRepository(FirestoreCatalogDocumentStore(db)))
                    val id="m4-validation-${UUID.randomUUID()}"
                    println("M4_VALIDATION_MATCH_ID=$id")
                    val service=MatchPersistenceService(catalog,repo,newId={id})
                    val participants=(0..1).map {MatchParticipant(it,"$id-p$it","M4 validation $it",null,ControlType.LOCAL_HUMAN,ConnectionState.CONNECTED,Instant.now())}
                    var m=service.create("DUEL_1V1",MatchExecutionMode.LOCAL,participants,MatchVisibility.PUBLIC,true)
                    check(m.validationData && m.ruleSnapshot.mode().ruleSet.targetScore==150)
                    fun emit(p: MatchPayload,actor: Int?=null,target: Int?=null) {m=service.append(id,m.lastSequence,"m4-${m.lastSequence+1}",ConfirmedTransition(p,actor,target))}
                    val deck=(0..9).flatMap {a->(a..9).map {DominoPips(a,it)}}
                    fun round(){emit(RoundStarted(0,listOf(10,10)));(0..1).forEach {emit(HandDealt(it,deck.drop(it*10).take(10)),target=it)};emit(TurnStarted(0))}
                    emit(MatchStarted(listOf(0,0)));round();emit(TilePlayed(0,deck[0],ChainEnd.RIGHT),0);emit(TurnChanged(1));emit(TurnStarted(1))
                    val seq=m.lastSequence;val pool=Executors.newFixedThreadPool(4)
                    try {
                        val calls=(1..8).map {pool.submit<Match>{service.append(id,seq,"same-command",ConfirmedTransition(PlayerPassed(1),1))}}.map {it.get()}
                        check(calls.all {it.lastSequence==seq+1})
                    } finally {pool.shutdown();if(!pool.awaitTermination(35,java.util.concurrent.TimeUnit.SECONDS))pool.shutdownNow()}
                    m=repo.read(id)!!;check(m.lastSequence==seq+1)
                    emit(RoundFinished(FinishType.BLOCKED,0,ScoreRecipient(ScoreOwnerType.PLAYER,0),27,0,listOf(18,27),listOf(27,0)))
                    round();emit(RoundFinished(FinishType.NORMAL,0,ScoreRecipient(ScoreOwnerType.PLAYER,0),123,0,listOf(0,113),listOf(150,0)))
                    emit(MatchFinished(MatchResult(ScoreRecipient(ScoreOwnerType.PLAYER,0),MatchFinishReason.TARGET_REACHED,m.score)))
                    val stored=repo.read(id)!!;val events=repo.readTrustedEvents(id,0)
                    check(stored==m && events.map {it.sequence}==(1L..m.lastSequence).toList())
                    check(repo.readTrustedEvents(id,3,2).map {it.sequence}==listOf(4L,5L))
                    check(repo.round(id,1)!!.finishType==FinishType.BLOCKED && repo.round(id,2)!!.status==RoundStatus.FINISHED)
                    participants.forEach {p->check(db.document("matches/$id/players/${p.seatIndex}").get().get().exists())
                        check(repo.history(p.playerUid!!,20,null).items.single().validationData)}
                    val rebuilt=MatchReplayReducer.reconstruct(stored,events)
                    check(rebuilt.publicState.scores==listOf(150,0) && rebuilt.publicState.status==MatchStatus.FINISHED)
                    check(MatchViewService().publicEvents(stored,events).none {it.payload is HandDealt})
                    check(stored.ruleSnapshot==m.ruleSnapshot)
                    println("REAL_FIRESTORE_M4=PASS MATCH_ID=$id EVENTS=${events.size} PARTICIPANTS=2 ROUNDS=2 HISTORY=2 IDEMPOTENT=PASS DATA_MARKED_VALIDATION=true WALLET_WRITES=0")
                }
            }
    }
}
