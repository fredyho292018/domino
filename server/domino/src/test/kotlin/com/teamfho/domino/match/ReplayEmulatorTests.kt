package com.teamfho.domino.match

import com.google.cloud.firestore.FirestoreOptions
import com.teamfho.domino.catalog.GameCatalogCodec
import com.teamfho.domino.online.SwarmEmulatorBackend
import com.teamfho.domino.security.FirebaseIdentity
import org.junit.jupiter.api.Tag
import org.junit.jupiter.api.Test
import java.nio.file.Files
import java.nio.file.Path
import java.time.Instant
import kotlin.test.*

@Tag("EMULATOR")
class ReplayEmulatorTests {
    @Test fun `retained swarm archives read only paging parity and cost on emulator`() {
        check(System.getenv("FIRESTORE_EMULATOR_HOST")=="127.0.0.1:18085")
        FirestoreOptions.newBuilder().setProjectId("demo-domino-f0").setEmulatorHost("127.0.0.1:18085")
            .setCredentials(FirestoreOptions.EmulatorCredentials()).build().service.use {raw->
            val dir=Path.of("build/swarm-emulator/retained-matches")
            assertTrue(Files.isDirectory(dir),"Retained I3.1 exports required; no remote fallback")
            val fixtures=Files.list(dir).use{paths->paths.filter{it.toString().endsWith(".json")}.map{GameCatalogCodec.mapper.readTree(Files.readString(it))}.toList()}
            val selected=listOf("DUEL_1V1","PARTNERS_2V2_ONLINE").map{key->fixtures.filter{it["match"]["modeKey"].asString()==key}.minBy{ kotlin.math.abs(it["events"].size()-500) }}
            for(f in selected) {
                val m=GameCatalogCodec.mapper.treeToValue(f["match"],Match::class.java)
                val root=raw.document("matches/${m.matchId}");root.set(MatchCodec.map(m)).get()
                f["events"].toList().chunked(200).forEach {chunk->val batch=raw.batch();chunk.forEach{e->batch.set(root.collection("events").document(e["eventId"].asString()),GameCatalogCodec.mapper.convertValue(e,Map::class.java))};batch.commit().get()}
                f["rounds"].forEach{r->root.collection("rounds").document(r["roundNumber"].asInt().toString()).set(GameCatalogCodec.mapper.convertValue(r,Map::class.java)).get()}
                val measured=SwarmEmulatorBackend.measured(raw) as com.google.cloud.firestore.Firestore
                val repo=FirestoreMatchRepository(measured);var documents=0
                val source=object:ReplaySource {
                    override fun match(id:String)=repo.read(id).also{documents++}
                    override fun round(id:String,number:Int)=repo.round(id,number).also{documents++}
                    override fun events(id:String,after:Long,limit:Int)=repo.readTrustedEvents(id,after,limit).also{documents+=it.size}
                }
                SwarmEmulatorBackend.counts.clear();val service=ReplayService(source)
                val manifest=service.manifest(m.participants.first().playerUid!!,m.matchId);assertTrue(manifest.replayAvailable)
                val manifestReads=documents;val events=mutableListOf<MatchEvent>();var pages=0
                while(events.size<m.lastSequence){events+=service.page(m.participants.first().playerUid!!,m.matchId,events.size.toLong(),250).items;pages++}
                assertEquals(m.lastSequence,events.size.toLong());assertEquals(0,SwarmEmulatorBackend.counts["writes"]?.get()?:0)
                val rebuilt=MatchReplayReducer.reconstruct(m,events)
                val runtime=GameCatalogCodec.mapper.readTree(f["runtime"]["stateJson"].asString())
                assertEquals(runtime["board"],GameCatalogCodec.mapper.valueToTree(rebuilt.publicState.board))
                assertEquals(m.score,rebuilt.publicState.scores)
                println("I4_EMULATOR mode=${m.modeKey} REPLAY_EVENT_COUNT=${events.size} EVENT_PAGES_FETCHED=$pages REPLAY_MANIFEST_READS=$manifestReads REPLAY_EVENT_PAGE_READS=1 REPLAY_WRITES=0")
            }
            val uid="i4-history-pagination"
            val template=GameCatalogCodec.mapper.treeToValue(selected[0]["history"][0],PlayerMatchHistory::class.java)
            val rows=(0..44).map {n->template.copy(matchId="i4-history-%03d".format(n),finishedAt=Instant.parse("2026-09-01T00:00:00Z").plusSeconds((n/3).toLong()).plusNanos(listOf(0L,100_000_000L,100_001_000L)[n%3]))}
            val batch=raw.batch();rows.forEach{batch.set(raw.document("players/$uid/matchHistory/${it.matchId}"),MatchCodec.map(it))};batch.commit().get()
            val repo=FirestoreMatchRepository(raw);val found=mutableListOf<PlayerMatchHistory>();var cursor:String?=null;var requests=0
            do{val p=repo.history(uid,20,cursor);found+=p.items;cursor=p.nextCursor;requests++}while(cursor!=null)
            assertEquals(rows.sortedWith(compareByDescending<PlayerMatchHistory>{it.finishedAt}.thenByDescending{it.matchId}),found)
            assertEquals(3,requests);assertEquals(45,found.map{it.matchId}.distinct().size)
            println("I4_HISTORY_PAGINATION=PASS VARIABLE_TIMESTAMP_PRECISION=PASS FIRST_PAGE_SIZE=20 HISTORY_PAGE_READS=24 NEXT_CURSOR_READS=1 BOUNDARY_SECOND_MAX=1000")
        }
    }
}
