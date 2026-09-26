package com.teamfho.swarm

import com.google.auth.oauth2.GoogleCredentials
import com.google.cloud.firestore.FirestoreOptions
import java.nio.file.Files
import java.nio.file.Path
import java.util.UUID
import java.util.concurrent.TimeUnit

/** Future R2 only: exact allowlisted registry, TEST ADC, read-only metadata/event audit. */
fun main(args:Array<String>) {
    com.teamfho.domino.validation.RealFirestoreGuard.requireOptIn()
    require(System.getenv("DOMINO_SWARM_FIREBASE_PROJECT_ID")=="teamfho-domino")
    require(System.getenv("FIRESTORE_EMULATOR_HOST").isNullOrBlank())
    require(args.size==1)
    val registry=Json.read(Files.readString(Path.of(args[0])))
    val run=UUID.fromString(registry.text("runId")).toString()
    require(run!="275abc49-45e7-40d0-8019-11531e6c8d48"){"DIAGNOSTIC_RUN_PROTECTED"}
    val entries=registry.path("matches").toList();require(entries.size in 1..25)
    val ids=entries.map{UUID.fromString(it.text("matchId")).toString()};require(ids.distinct().size==ids.size)
    require(System.getenv("DOMINO_CAPACITY_READ_RUN_ID")==run){"EXPLICIT_RUN_READ_OPT_IN_REQUIRED"}
    FirestoreOptions.newBuilder().setProjectId("teamfho-domino").setCredentials(GoogleCredentials.getApplicationDefault()).build().service.use{db->
        for(id in ids) {
            val root=db.document("matches/$id")
            val pair=db.runTransaction {tx->
                val m=tx.get(root).get();val r=tx.get(root.collection("runtime").document("authoritative")).get()
                m to r
            }.get(20,TimeUnit.SECONDS)
            val m=pair.first;val state=pair.second.getString("stateJson")?.let(Json::read)
            require(!m.exists()||m.getBoolean("validationData")==true)
            val status=m.getString("status");val runtimeStatus=state?.path("match")?.text("status")
            val outcome=if(status!=runtimeStatus)"UNKNOWN" else when(status){"FINISHED"->"COMPLETED";"CANCELLED"->"CANCELLED";"CREATED","STARTING","IN_PROGRESS"->"ACTIVE";else->"UNKNOWN"}
            val audit=CapacityCorrectness();val last=m.getLong("lastSequence")?:0L;require(last in 0..100000)
            var through=0L
            while(through<last) {
                val page=root.collection("events").select("sequence","type","payload.seat","causedByCommandId","matchId")
                    .whereGreaterThan("sequence",through).whereLessThanOrEqualTo("sequence",last).orderBy("sequence").limit(250).get().get(20,TimeUnit.SECONDS).documents
                require(page.isNotEmpty()){"EVENT_GAP"}
                for(e in page) {
                    require(e.getString("matchId")==id){"CROSS_MATCH_EVENT"}
                    val sequence=e.getLong("sequence")!!
                    audit.event(sequence,e.getString("type")?:"",e.getLong("payload.seat")?.toInt(),e.getString("causedByCommandId"))
                    through=sequence
                }
            }
            println("CAPACITY_OUTCOME "+Json.write(mapOf("runId" to run,"matchId" to id,"rootStatus" to status,"runtimeStatus" to runtimeStatus,
                "terminalState" to outcome,"completedAt" to m.get("finishedAt")?.toString(),"sequence" to last,"correctness" to audit.counters(),"source" to "AUTHORITATIVE_TEST_READ")))
        }
    }
}
