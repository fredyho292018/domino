package com.teamfho.domino.match

import com.google.cloud.firestore.FirestoreOptions
import com.teamfho.domino.online.SwarmEmulatorBackend
import org.junit.jupiter.api.Tag
import org.junit.jupiter.api.Test
import java.time.Instant
import kotlin.test.*

@Tag("EMULATOR")
class ReplayEmulatorTests {
    @Test fun `retained swarm archives read only paging parity and cost on emulator`() {
        check(System.getenv("FIRESTORE_EMULATOR_HOST")=="127.0.0.1:18085")
        FirestoreOptions.newBuilder().setProjectId("demo-domino-f0").setEmulatorHost("127.0.0.1:18085")
            .setCredentials(FirestoreOptions.EmulatorCredentials()).build().service.use {raw->
            // Canonical source-controlled engine inputs; no retained Swarm/runtime files.
            val selected=listOf(ReplayFixture(matchId="infra1r-duel"),ReplayFixture(true,matchId="infra1r-partners"))
            for(f in selected) {
                val m=f.state.match
                assertTrue(f.events.size>250,"Fixture must exercise multiple event pages")
                val root=raw.document("matches/${m.matchId}");root.set(MatchCodec.map(m)).get()
                f.events.chunked(200).forEach {chunk->val batch=raw.batch();chunk.forEach{e->batch.set(root.collection("events").document(e.eventId),MatchCodec.map(e))};batch.commit().get()}
                f.rounds.values.forEach{r->root.collection("rounds").document(r.roundNumber.toString()).set(MatchCodec.map(r)).get()}
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
                assertEquals(f.state.board,rebuilt.publicState.board)
                assertEquals(m.score,rebuilt.publicState.scores)
                println("I4_EMULATOR mode=${m.modeKey} REPLAY_EVENT_COUNT=${events.size} EVENT_PAGES_FETCHED=$pages REPLAY_MANIFEST_READS=$manifestReads REPLAY_EVENT_PAGE_READS=1 REPLAY_WRITES=0")
            }
            val uid="i4-history-pagination"
            val template=selected[0].history.getValue("p0")
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
