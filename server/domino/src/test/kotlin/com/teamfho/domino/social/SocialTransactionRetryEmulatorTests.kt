package com.teamfho.domino.social

import com.google.cloud.firestore.*
import com.google.api.gax.rpc.ApiExceptionFactory
import com.google.api.gax.grpc.GrpcStatusCode
import com.teamfho.domino.player.FirestorePlayerFoundationRepository
import com.teamfho.domino.security.FirebaseIdentity
import org.junit.jupiter.api.Tag
import org.junit.jupiter.api.Test
import java.time.Clock
import java.util.UUID
import java.util.concurrent.*
import java.util.concurrent.atomic.AtomicInteger
import kotlin.test.*

@Tag("EMULATOR")
class SocialTransactionRetryEmulatorTests {
    private fun db():Firestore {
        check(System.getenv("FIRESTORE_EMULATOR_HOST")=="127.0.0.1:18085")
        return FirestoreOptions.newBuilder().setProjectId("demo-domino-f0").setEmulatorHost("127.0.0.1:18085")
            .setCredentials(FirestoreOptions.EmulatorCredentials()).build().service
    }
    private fun aborted()=ApiExceptionFactory.createException(IllegalStateException("test injected ABORTED"),GrpcStatusCode.of(io.grpc.Status.Code.ABORTED),true)
    @Test fun `wrapped aborted prevents SDK retry but normalized callback retries and commits once`()=db().use {db->
        val attempts=AtomicInteger()
        assertFailsWith<ExecutionException> {db.runTransaction<Unit>{attempts.incrementAndGet();throw ExecutionException(aborted())}.get(15,TimeUnit.SECONDS)}
        assertEquals(1,attempts.get())
        attempts.set(0)
        val ref=db.document("a2rRetry/${UUID.randomUUID()}")
        FirestoreFriendships(db).atomic {tx->
            val old=(tx.read(ref.path)?.get("value") as? Number)?.toInt()?:0
            if(attempts.incrementAndGet()==1)throw ExecutionException(ExecutionException(aborted()))
            tx.put(ref.path,mapOf("value" to old+1));Unit
        }
        assertEquals(2,attempts.get());assertEquals(1L,ref.get().get().getLong("value"))
        println("A2R_RETRY wrappedAttempts=1 fixedAttempts=2 committedValue=1 defaultMaxAttempts=${TransactionOptions.create().numberOfAttempts} backoff=${db.options.retrySettings}")
    }
    @Test fun `SDK attempts stay bounded and domain failure is not retried`()=db().use {db->
        val attempts=AtomicInteger()
        assertFailsWith<ExecutionException>{FirestoreFriendships(db).atomic<Unit>{attempts.incrementAndGet();throw ExecutionException(aborted())}}
        assertEquals(5,attempts.get())
        attempts.set(0)
        val original=ExecutionException(SocialFailure("PLAYER_NOT_FOUND",404))
        val error=assertFailsWith<ExecutionException>{FirestoreFriendships(db).atomic<Unit>{attempts.incrementAndGet();throw original}}
        assertSame(original,error.cause);assertEquals(1,attempts.get())
        println("A2R_RETRY exhaustedAttempts=5 domainAttempts=1")
    }
    private fun player(db:Firestore):PublicPlayerIdentity {
        val uid="a2r-${UUID.randomUUID()}"
        FirestorePlayerFoundationRepository(db,Clock.systemUTC()).ensure(FirebaseIdentity(uid,true),"en","Guest-ABCDEFGH")
        return PublicPlayerIdentityService(FirestoreSocialRepository(db)).ensure(uid)
    }
    @Test fun `fifty concurrent follow or unfollow versus block races preserve all projections`()=db().use {db->
        val pool=Executors.newFixedThreadPool(2)
        try {
            repeat(50){iteration->
                val a=player(db);val b=player(db)
                val follows=FollowService(FirestoreFriendships(db),SocialCursor());val blocks=FirestoreSocialRepository(db)
                val enable=iteration%2==0
                // Always exercise cleanup of the reverse relation; odd runs reproduce Unfollow/Block.
                follows.set(b.uid,a.publicPlayerId,true)
                if(!enable)follows.set(a.uid,b.publicPlayerId,true)
                val barrier=CyclicBarrier(2)
                val follow=pool.submit(Callable {
                    barrier.await(5,TimeUnit.SECONDS)
                    try {follows.set(a.uid,b.publicPlayerId,enable)} catch(e:Exception) {
                        val domain=generateSequence<Throwable>(e){it.cause}.filterIsInstance<SocialFailure>().firstOrNull()
                        // Only an authorized rejection caused by the winning Block is acceptable.
                        if(!enable || domain?.code!="PLAYER_NOT_FOUND" || domain.status!=404)throw e
                    }
                })
                val block=pool.submit(Callable {
                    barrier.await(5,TimeUnit.SECONDS)
                    blocks.block(b.uid,SocialCandidate(a.uid,PublicPlayerProfile(a.publicPlayerId,a.friendCode,"Guest"),""),true)
                })
                follow.get(30,TimeUnit.SECONDS);block.get(30,TimeUnit.SECONDS)
                assertTrue(db.document("players/${b.uid}/blocks/${a.uid}").get().get().exists())
                assertTrue(db.document("players/${a.uid}/blockedBy/${b.uid}").get().get().exists())
                for((x,y) in listOf(a to b,b to a)) {
                    assertFalse(follows.isFollowing(x.uid,y.uid),"iteration=$iteration")
                    assertFalse(db.document("players/${y.uid}/followers/${x.uid}").get().get().exists())
                    assertEquals(FollowCounts(0,0),follows.counts(x.uid))
                }
                println("A2R_STRESS iteration=${iteration+1} invariant=PASS")
            }
        } finally {pool.shutdownNow()}
    }
}
