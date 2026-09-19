package com.teamfho.swarm

import com.google.auth.oauth2.GoogleCredentials
import com.google.cloud.firestore.FirestoreOptions
import java.util.UUID

/** Explicit read-only validation of one known match. Never scans collections or prints hands/tokens. */
fun main(args:Array<String>) {
    com.teamfho.domino.validation.RealFirestoreGuard.requireOptIn()
    require(args.size==1)
    val id=UUID.fromString(args[0]).toString()
    val project=requireNotNull(System.getenv("DOMINO_SWARM_FIREBASE_PROJECT_ID"))
    FirestoreOptions.newBuilder().setProjectId(project).setCredentials(GoogleCredentials.getApplicationDefault()).build().service.use { db ->
        val match=requireNotNull(db.document("matches/$id").get().get().data)
        val participants=match["participants"] as List<*>
        val members=participants.map{it as Map<*,*>}
        val uids=members.map{it["playerUid"] as String}
        require(uids.distinct().size==uids.size)
        require(members.all{it["controlType"]=="REMOTE_HUMAN"})
        val mode=match["modeKey"] as String
        val expected=if(mode=="DUEL_1V1")2 else if(mode=="PARTNERS_2V2_ONLINE")4 else error("UNEXPECTED_MODE")
        require(members.size==expected)
        if(expected==4)require(members.map{(it["teamId"] as Number).toInt()}==listOf(0,1,0,1))
        require(match["validationData"]==true)
        // Targeted human-validation evidence: one known round only, no collection scan or private hands.
        val firstRound=db.document("matches/$id/rounds/1").get().get().data
        val rules=match["ruleSnapshot"] as Map<*,*>
        val modeRules=Json.read(rules["effectiveModeJson"] as String).path("ruleSet")
        println("SWARM_HUMAN_ROUND_INSPECTION "+Json.write(mapOf(
            "matchId" to id,"mode" to mode,"score" to match["score"],
            "ruleSetId" to match["ruleSetId"],"ruleSetVersion" to match["ruleSetVersion"],
            "participants" to members.map { mapOf("seat" to it["seatIndex"],"alias" to it["displayNameSnapshot"],"team" to it["teamId"],"control" to it["controlType"]) },
            "round" to firstRound,"targetScore" to modeRules.number("targetScore"),
            "turnOrder" to modeRules.path("turnOrder").toList().map{it.asInt()})))
        var histories=0;var markers=0
        for(uid in uids) {
            val marker=db.document("developmentTestAccounts/$uid").get().get()
            if(marker.getBoolean("isTestAccount")==true&&marker.getString("testSource")=="BOT_SWARM")markers++
            val history=db.document("players/$uid/matchHistory/$id").get().get()
            if(history.exists()){require(history.getBoolean("validationData")==true);histories++}
        }
        println("SWARM_MATCH_INSPECTION "+Json.write(mapOf("matchId" to id,"mode" to mode,"status" to match["status"],"distinctParticipants" to uids.size,"engineBots" to 0,"testAccountMarkers" to markers,"testMatchMarker" to true,"histories" to histories,"lastSequence" to match["lastSequence"])))
    }
}
