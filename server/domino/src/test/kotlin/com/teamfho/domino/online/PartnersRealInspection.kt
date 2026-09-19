package com.teamfho.domino.online

import com.google.auth.oauth2.GoogleCredentials
import com.google.cloud.firestore.FirestoreOptions
import com.teamfho.domino.catalog.*
import com.teamfho.domino.config.FirebaseProperties
import com.teamfho.domino.match.*
import org.springframework.boot.WebApplicationType
import org.springframework.boot.builder.SpringApplicationBuilder

/** Read-only verification of an actual four-client validation Match; never creates a user or Match. */
object PartnersRealInspection {
    @JvmStatic fun main(args:Array<String>) {
        com.teamfho.domino.validation.RealFirestoreGuard.requireOptIn()
        require(args.size==1);val id=java.util.UUID.fromString(args[0]).toString()
        SpringApplicationBuilder(CatalogSeedConfiguration::class.java).web(WebApplicationType.NONE).logStartupInfo(false).run().use{context->
            FirestoreOptions.newBuilder().setProjectId(context.getBean(FirebaseProperties::class.java).projectId)
                .setCredentials(GoogleCredentials.getApplicationDefault()).build().service.use{db->
                val repository=FirestoreOnlineRepository(db);val s=requireNotNull(repository.read(id));val m=s.match
                require(m.modeKey==GameCatalogV4Publisher.KEY&&m.participants.size==4&&m.participants.map{it.playerUid}.distinct().size==4)
                require(m.participants.all{it.controlType==ControlType.REMOTE_HUMAN});require(m.participants.map{it.teamId}==listOf(0,1,0,1))
                require(m.ruleSnapshot.mode().ruleSet==GameCatalogValidator.resolve(GameCatalogV4Publisher.canonical()).modes.single{it.key=="PARTNERS_2V2"}.ruleSet)
                require(db.collection("matches/$id/players").get().get().size()==4)
                require(db.document("onlineMatchCreationReceipts/$id").get().get().getString("status")=="COMMITTED")
                m.participants.forEach{require(db.document("onlinePlayerAssignments/${it.playerUid}").get().get().getString("matchId")==id)}
                val events=repository.events(id,0);require(events.map{it.sequence}==(1L..m.lastSequence).toList())
                val hands=events.filter{it.payload is HandDealt};require(hands.size==4&&hands.all{(it.payload as HandDealt).tiles.size==10})
                require(events.none{it.payload is AutoPlayed||it.payload is TurnTimeout})
                require(s.reserve.size==15);require(events.count{it.payload is TilePlayed}>=8)
                for(p in m.participants){val private=OnlineMatchService.snapshot(s,p.playerUid!!);require(private.privateState.seat==p.seatIndex)
                    require(events.map{OnlineMatchService.authorized(it,p.seatIndex)}.count{it.event?.payload is HandDealt}==1)}
                println("M5_REAL_FIRESTORE=PASS MATCH_ID=$id HUMANS=4 BOTS=0 PRIVATE_DEALS=4 RESERVE=15 CREATION_RECEIPTS=1 ASSIGNMENTS=4 PLAYED=${events.count{it.payload is TilePlayed}} READ_ONLY=YES")
            }
        }
    }
}
