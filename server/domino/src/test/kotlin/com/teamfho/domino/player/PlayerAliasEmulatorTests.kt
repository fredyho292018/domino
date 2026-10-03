package com.teamfho.domino.player

import com.google.cloud.firestore.FirestoreOptions
import com.teamfho.domino.security.FirebaseIdentity
import org.junit.jupiter.api.Tag
import org.junit.jupiter.api.Test
import java.time.Clock
import java.util.UUID
import java.util.concurrent.Callable
import java.util.concurrent.CountDownLatch
import java.util.concurrent.Executors
import kotlin.test.*

@Tag("EMULATOR")
class PlayerAliasEmulatorTests {
    @Test fun `Firestore transaction retries permit exactly one alias owner`() {
        check(System.getenv("FIRESTORE_EMULATOR_HOST")=="127.0.0.1:18085")
        FirestoreOptions.newBuilder().setProjectId("demo-domino-f0").setEmulatorHost("127.0.0.1:18085")
            .setCredentials(FirestoreOptions.EmulatorCredentials()).build().service.use { db ->
                db.document(PlayerAliasReservations.rolloutPath).set(mapOf("status" to "READY","normalizationVersion" to 1L)).get()
                val repo=FirestorePlayerFoundationRepository(db,Clock.systemUTC())
                val ids=listOf("alias-a-${UUID.randomUUID()}","alias-b-${UUID.randomUUID()}")
                val guests=ids.map { GuestDisplayNames.generate() }
                ids.forEachIndexed { i,id -> repo.ensure(FirebaseIdentity(id,true),"en",guests[i]) }
                val alias="Alias"+UUID.randomUUID().toString().replace("-","").take(10)
                val start=CountDownLatch(1);val pool=Executors.newFixedThreadPool(2)
                val results=try {
                    val futures=ids.mapIndexed { i,id -> pool.submit(Callable {
                        start.await()
                        try { repo.updateDisplayName(FirebaseIdentity(id,true),if(i==0)alias else alias.uppercase()); "OK" }
                        catch(e:PlayerFoundationException){ e.code.name }
                    }) }
                    start.countDown();futures.map { it.get() }
                } finally { pool.shutdownNow() }
                assertEquals(listOf("DISPLAY_NAME_TAKEN","OK"),results.sorted())
                val winner=results.indexOf("OK");val loser=1-winner
                assertEquals(ids[winner],db.document(PlayerAliasReservations.path(alias)).get().get().getString("playerId"))
                assertEquals(guests[loser],db.document("players/${ids[loser]}").get().get().getString("displayName"))
                assertEquals("RELEASED",db.document(PlayerAliasReservations.path(guests[winner])).get().get().getString("state"))
                val doc=db.document("players/${ids[winner]}").get().get()
                repo.updateDisplayName(FirebaseIdentity(ids[winner],true),doc.getString("displayName")!!)
                assertEquals(doc.updateTime,db.document(doc.reference.path).get().get().updateTime)
            }
    }
}
