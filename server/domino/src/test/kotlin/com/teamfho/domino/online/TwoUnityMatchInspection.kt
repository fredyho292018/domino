package com.teamfho.domino.online
import com.google.auth.oauth2.GoogleCredentials
import com.google.cloud.firestore.FirestoreOptions
import com.teamfho.domino.catalog.CatalogSeedConfiguration
import com.teamfho.domino.config.FirebaseProperties
import com.teamfho.domino.match.*
import org.springframework.boot.WebApplicationType
import org.springframework.boot.builder.SpringApplicationBuilder

/** Read-only inspection of a real two-Unity validation match. No participant simulator. */
object TwoUnityMatchInspection {
 @JvmStatic fun main(args:Array<String>) {
        com.teamfho.domino.validation.RealFirestoreGuard.requireOptIn()
  require(args.size==1);val id=java.util.UUID.fromString(args[0]).toString()
  SpringApplicationBuilder(CatalogSeedConfiguration::class.java).web(WebApplicationType.NONE).logStartupInfo(false).run().use { context ->
   FirestoreOptions.newBuilder().setProjectId(context.getBean(FirebaseProperties::class.java).projectId).setCredentials(GoogleCredentials.getApplicationDefault()).build().service.use { db ->
    val repo=FirestoreMatchRepository(db);val m=requireNotNull(repo.read(id));val events=mutableListOf<MatchEvent>()
    while((events.lastOrNull()?.sequence?:0)<m.lastSequence){val page=repo.readTrustedEvents(id,events.lastOrNull()?.sequence?:0);check(page.isNotEmpty());events+=page}
    check(m.participants.size==2&&m.participants.map{it.playerUid}.distinct().size==2)
    check(events.map{it.sequence}==(1L..m.lastSequence).toList());check(db.collection("matches/$id/players").get().get().size()==2)
    check(m.status==MatchStatus.FINISHED&&m.score.max()>=150)
    for(round in 1..m.currentRoundNumber)check(repo.round(id,round)?.status==RoundStatus.FINISHED)
    m.participants.forEach {check(repo.history(it.playerUid!!,100,null).items.any{h->h.matchId==id})}
    check(events.any{it.payload is TurnTimeout}&&events.any{it.payload is AutoPlayed}&&events.any{it.payload is PlayerDisconnected}&&events.any{it.payload is PlayerReconnected})
    val timeout=events.first{it.payload is TurnTimeout};val started=events.last{it.sequence<timeout.sequence&&it.payload is TurnStarted}.payload as TurnStarted
    check(started.turnDeadlineAt==started.turnStartedAt!!.plusSeconds(60))
    check((timeout.payload as TurnTimeout).turnDeadlineAt==started.turnDeadlineAt)
    val disconnected=events.first{it.payload is PlayerDisconnected}.payload as PlayerDisconnected
    val reconnected=events.first{it.payload is PlayerReconnected}.payload as PlayerReconnected
    check(disconnected.reconnectDeadlineAt==disconnected.disconnectedAt!!.plusSeconds(180))
    check(reconnected.reconnectedAt!!<disconnected.reconnectDeadlineAt)
    check(events.filter{it.payload is TilePlayed}.groupBy{it.causedByCommandId}.values.all{it.size==1})
    println("TWO_UNITY_FIRESTORE=PASS MATCH_ID=$id SEQUENCE=${m.lastSequence} ROUNDS=${m.currentRoundNumber} PARTICIPANTS=2 HISTORY=2 SCORES=${m.score} TIMEOUTS=${events.count{it.payload is TurnTimeout}} AUTOPLAYS=${events.count{it.payload is AutoPlayed}} INSPECTION_WRITES=0")
   }
  }
 }
}
