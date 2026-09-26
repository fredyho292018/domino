package com.teamfho.swarm

import com.google.auth.oauth2.GoogleCredentials
import com.google.cloud.firestore.*
import java.nio.file.*
import java.time.Instant
import java.util.concurrent.TimeUnit

/** Read-only TEST admin tool; never joins queues or obtains a domain write dependency.
 * Usage --args=<manifest>. Manifest: runId, startedAt, through, loadDirectory,
 * clientMatchIds. Fresh source union includes active roots, pending work, assignments
 * and every root created in the bounded run window (including terminal matches).
 */
fun main(args:Array<String>) {
    com.teamfho.domino.validation.RealFirestoreGuard.requireOptIn()
    require(System.getenv("DOMINO_SWARM_FIREBASE_PROJECT_ID")=="teamfho-domino")
    require(System.getenv("FIRESTORE_EMULATOR_HOST").isNullOrBlank());require(args.size==1)
    val plan=Json.read(Files.readString(Path.of(args.single())))
    val run=plan.text("runId");require(System.getenv("DOMINO_CAPACITY_READ_RUN_ID")==run)
    val load=Path.of(plan.text("loadDirectory")).toAbsolutePath().normalize()
    require(load.fileName.toString()=="LOAD" && generateSequence(load){it.parent}.none{Files.exists(it.resolve(".git"))})
    val slots=mutableMapOf<String,String>()
    val players=(1..5).flatMap{g->(1..20).map{s->
        val identity=Json.read(Files.readString(load.resolve("group-%02d/slot-%02d.json".format(g,s))))
        require(identity.text("projectId")=="teamfho-domino" && identity.text("environment")=="TEST" && identity.path("isTestAccount").asBoolean() && identity.text("testSource")=="BOT_SWARM")
        identity.text("uid").also{require(it.matches(Regex("[A-Za-z0-9_-]+")));slots[it]="group-%02d/slot-%02d".format(g,s)}
    }};require(players.distinct().size==100)
    val scope=RunScope(run,Instant.parse(plan.text("startedAt")),Instant.parse(plan.text("through")),players.toSet(),plan.path("clientMatchIds").toList().map{it.asText()}.toSet())
    require(scope.through<=Instant.now().plusSeconds(5))
    FirestoreOptions.newBuilder().setProjectId("teamfho-domino").setCredentials(GoogleCredentials.getApplicationDefault()).build().service.use{db->
        val records=FirestoreCapacityDiscovery(db).read(scope)
        val result=AuthoritativeRunRegistry.discover(scope,records)
        val matches=records.filter{it.id in result.matchIds}.map{mapOf("matchId" to it.id,"participantSlots" to it.players.map{uid->slots.getValue(uid)}.sorted(),"startedAt" to it.createdAt.toString(),"rootStatus" to it.status,"runtimeStatus" to it.runtimeStatus,"completedAt" to it.finishedAt)}
        println("CAPACITY_DISCOVERY "+Json.write(mapOf("runId" to run,"source" to "AUTHORITATIVE_TEST_DISCOVERY","summary" to result,"matches" to matches)))
    }
}

class FirestoreCapacityDiscovery(private val db:Firestore) {
    fun read(scope:RunScope):List<AuthorityMatch> {
        val roots=linkedMapOf<String,DocumentSnapshot>();val pending=mutableSetOf<String>()
        fun root(id:String){require(java.util.UUID.fromString(id).toString()==id);if(id !in roots)roots[id]=db.document("matches/$id").get().get(20,TimeUnit.SECONDS)}
        fun scan(query:Query) {
            var cursor:DocumentSnapshot?=null;var count=0
            while(true) {
                val q=if(cursor==null)query else query.startAfter(cursor)
                val page=q.limit(200).get().get(30,TimeUnit.SECONDS).documents;count+=page.size
                require(count<=10000){"DISCOVERY_BOUND_EXCEEDED"}
                for(doc in page)roots[doc.id]=doc
                if(page.size<200)break
                cursor=page.last()
            }
        }
        // Stored createdAt is ISO-8601 text. A one-second query margin avoids
        // fractional-text boundary ordering; exact Instants are filtered by policy.
        scan(db.collection("matches").whereGreaterThanOrEqualTo("createdAt",scope.start.minusSeconds(1).toString())
            .whereLessThanOrEqualTo("createdAt",scope.through.plusSeconds(1).toString()).orderBy("createdAt"))
        for(status in AuthoritativeRunRegistry.active)scan(db.collection("matches").whereEqualTo("status",status).orderBy(FieldPath.documentId()))
        for(uid in scope.loadPlayers) {
            val work=db.collection("onlineTurnWork").whereArrayContains("uids",uid).limit(101).get().get(20,TimeUnit.SECONDS).documents
            require(work.size<=100){"WORK_BOUND_EXCEEDED"};for(w in work){pending+=w.id;root(w.id)}
            db.document("onlinePlayerAssignments/$uid").get().get(20,TimeUnit.SECONDS).getString("matchId")?.let(::root)
        }
        scope.clientIds.forEach(::root)
        require(roots.size<=30000){"DISCOVERY_BOUND_EXCEEDED"}
        val records=mutableListOf<AuthorityMatch>()
        for((id,m) in roots) {
            require(m.exists()){ "MISSING_AUTHORITY_DOCUMENT" }
            val participants=(m.get("participants") as? List<*>)?:error("MISSING_PARTICIPANTS")
            val players=participants.mapNotNull{(it as? Map<*,*>)?.get("playerUid") as? String}.toSet()
            if(players.none{it in scope.loadPlayers} && id !in scope.clientIds)continue
            val state=db.document("matches/$id/runtime/authoritative").get().get(20,TimeUnit.SECONDS).getString("stateJson")?.let(Json::read)
            records+=AuthorityMatch(id,Instant.parse(m.getString("createdAt")),players,participants.size,m.getString("status")?:"UNKNOWN",state?.path("match")?.text("status"),m.getBoolean("validationData")==true,id in pending,m.getString("finishedAt"))
        }
        return records
    }
}
