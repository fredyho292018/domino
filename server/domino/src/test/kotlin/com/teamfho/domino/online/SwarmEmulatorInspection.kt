package com.teamfho.domino.online

import com.google.cloud.firestore.FirestoreOptions
import com.teamfho.domino.catalog.GameCatalogCodec
import com.teamfho.domino.match.*
import java.nio.file.*
import java.util.concurrent.TimeUnit

/** Reads only the demo emulator and retains completed matches for later I4 fixtures. */
object SwarmEmulatorInspection {
    @JvmStatic fun main(args:Array<String>) {
        check(System.getenv("DOMINO_SWARM_EMULATOR")=="true")
        check(System.getenv("FIRESTORE_EMULATOR_HOST")=="127.0.0.1:18085")
        val folder=Path.of("build/swarm-emulator/retained-matches");Files.createDirectories(folder)
        FirestoreOptions.newBuilder().setProjectId("demo-domino-swarm").setHost("127.0.0.1:18085")
            .setEmulatorHost("127.0.0.1:18085").setCredentials(FirestoreOptions.EmulatorCredentials()).build().service.use {db->
            val all=db.collection("matches").get().get(30,TimeUnit.SECONDS).documents
            var histories=0;var events=0;val byMode=mutableMapOf<String,Int>()
            val windows=mutableMapOf<String,MutableList<Pair<java.time.Instant,java.time.Instant>>>()
            for(doc in all) {
                val m=MatchCodec.read(doc.data,Match::class.java)
                check(m.status==MatchStatus.FINISHED){"UNFINISHED_MATCH"}
                check(m.validationData)
                val count=if(m.modeKey=="DUEL_1V1")2 else 4
                check(m.participants.size==count&&m.participants.map{it.playerUid}.distinct().size==count)
                check(m.participants.all{it.controlType==ControlType.REMOTE_HUMAN})
                if(count==4){check(m.ruleSetId=="double-nine-partners"&&m.ruleSetVersion==1);check(m.ruleSnapshot.mode().seatTeams==listOf(listOf(0,2),listOf(1,3)))}
                val stream=doc.reference.collection("events").orderBy("sequence").get().get().documents
                check(stream.map{it.getLong("sequence")}==(1L..m.lastSequence).toList())
                events+=stream.size
                val history=m.participants.map {p->
                    val uid=p.playerUid!!
                    windows.getOrPut(uid){mutableListOf()}.add(m.createdAt to m.updatedAt)
                    val h=db.document("players/$uid/matchHistory/${m.matchId}").get().get()
                    check(h.exists()&&h.getBoolean("validationData")==true);histories++
                    val wallet=db.document("players/$uid/wallet/main").get().get()
                    check(wallet.getLong("coins")==0L&&wallet.getLong("lifetimeCoinsEarned")==0L)
                    h.data
                }
                val fixture=mapOf("match" to doc.data,"runtime" to doc.reference.collection("runtime").document("authoritative").get().get().data,
                    "participants" to doc.reference.collection("players").get().get().documents.map{it.data},
                    "rounds" to doc.reference.collection("rounds").get().get().documents.map{it.data},"events" to stream.map{it.data},"history" to history)
                Files.writeString(folder.resolve("${m.matchId}.json"),GameCatalogCodec.mapper.writeValueAsString(fixture))
                byMode[m.modeKey]=(byMode[m.modeKey]?:0)+1
            }
            windows.values.forEach{list->list.sortedBy{it.first}.zipWithNext().forEach{(a,b)->check(a.second<=b.first){"DUPLICATE_PAIRING"}}}
            check(db.collection("rewardIntents").limit(1).get().get().isEmpty)
            val report=mapOf("completedByMode" to byMode,"histories" to histories,"events" to events,"distinctUidsPerMatch" to true,
                "engineBots" to 0,"duplicatePairing" to false,"sequenceCorruption" to false,"rewardIntents" to 0,"rewardCoins" to 0,"testDataMarked" to true)
            Files.writeString(folder.parent.resolve("inspection.json"),GameCatalogCodec.mapper.writeValueAsString(report))
            println("SWARM_EMULATOR_INSPECTION="+GameCatalogCodec.mapper.writeValueAsString(report))
        }
    }
}
