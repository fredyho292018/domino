package com.teamfho.swarm

import com.google.auth.oauth2.GoogleCredentials
import com.google.cloud.firestore.FirestoreOptions
import java.nio.file.Files
import java.nio.file.Path
import java.util.UUID
import java.util.concurrent.TimeUnit

/** Targeted TEST administration. Reads exact registered IDs, never emits identities or payloads. */
fun main(args:Array<String>) {
    com.teamfho.domino.validation.RealFirestoreGuard.requireOptIn()
    require(System.getenv("DOMINO_SWARM_FIREBASE_PROJECT_ID")=="teamfho-domino")
    require(System.getenv("FIRESTORE_EMULATOR_HOST").isNullOrBlank())
    require(args.size==1)
    val registry=Json.read(Files.readString(Path.of(args[0])))
    require(registry.text("runId")=="275abc49-45e7-40d0-8019-11531e6c8d48")
    val ids=registry.path("matchIds").toList().map { UUID.fromString(it.asText()).toString() }
    require(ids.size==22 && ids.distinct().size==22)
    FirestoreOptions.newBuilder().setProjectId("teamfho-domino")
        .setCredentials(GoogleCredentials.getApplicationDefault()).build().service.use { db ->
        val comparison="efe78abb-6582-4984-a433-1bef37ddf497"
        for(id in ids+comparison) {
            val root=db.document("matches/$id")
            val m=root.get().get(20,TimeUnit.SECONDS)
            val runtime=root.collection("runtime").document("authoritative").get().get(20,TimeUnit.SECONDS)
            val state=runtime.getString("stateJson")?.let(Json::read)
            val participants=(m.get("participants") as? List<*>)?.map { it as Map<*,*> }.orEmpty()
            val uids=participants.mapNotNull { it["playerUid"] as? String }
            require(!m.exists() || m.getBoolean("validationData")==true)
            var histories=0
            for(uid in uids) if(db.document("players/$uid/matchHistory/$id").get().get(20,TimeUnit.SECONDS).exists())histories++
            val events=root.collection("events").count().get().get(20,TimeUnit.SECONDS).count
            val rounds=root.collection("rounds").count().get().get(20,TimeUnit.SECONDS).count
            val raw=m.getString("status")
            val authoritative=state?.path("match")?.path("status")?.asText()
            val classification=if(raw!=authoritative)"UNKNOWN" else when(raw) {
                "FINISHED"->"COMPLETED"
                "CREATED","STARTING","IN_PROGRESS"->"ACTIVE"
                "CANCELLED"->"FAILED"
                else->"UNKNOWN"
            }
            println("SERVER7R_MATCH "+Json.write(mapOf("matchId" to id,
                "scope" to if(id==comparison)"SERVER6_COMPARISON" else "SERVER7_RUN",
                "exists" to m.exists(),"status" to raw,"authoritativeStatus" to authoritative,
                "classification" to classification,"phase" to state?.path("phase")?.asText(),
                "participantCount" to participants.size,"distinctParticipants" to uids.distinct().size,
                "abandonedParticipants" to participants.count { it["connectionState"]=="ABANDONED" },
                "connectedParticipants" to participants.count { it["connectionState"] in setOf("CONNECTED","RECONNECTED") },
                "disconnectedStateParticipants" to participants.count { it["connectionState"]=="DISCONNECTED" },
                "turnDeadlinePresent" to (state?.path("turnDeadlineAt")?.isNull==false),
                "historyRecords" to histories,"replayEventRecords" to events,"roundRecords" to rounds,
                "lastSequence" to m.getLong("lastSequence"),
                "turnWorkExists" to db.document("onlineTurnWork/$id").get().get(20,TimeUnit.SECONDS).exists())))
        }
    }
}
