package com.teamfho.domino.social

import com.google.cloud.firestore.*
import com.teamfho.domino.entitlement.*
import com.teamfho.domino.match.MatchCodec
import com.teamfho.domino.player.FirestorePlayerFoundationRepository
import com.teamfho.domino.security.FirebaseIdentity
import org.junit.jupiter.api.Tag
import org.junit.jupiter.api.Test
import java.time.*
import java.util.UUID
import java.util.concurrent.*
import kotlin.test.*

@Tag("EMULATOR")
class FriendshipEmulatorTests {
    private fun db():Firestore {check(System.getenv("FIRESTORE_EMULATOR_HOST")=="127.0.0.1:18085");return FirestoreOptions.newBuilder().setProjectId("demo-domino-f0").setEmulatorHost("127.0.0.1:18085").setCredentials(FirestoreOptions.EmulatorCredentials()).build().service}
    private fun player(db:Firestore):PublicPlayerIdentity {val uid="s12-${UUID.randomUUID()}";FirestorePlayerFoundationRepository(db,Clock.systemUTC()).ensure(FirebaseIdentity(uid,true),"en","Guest-ABCDEFGH");return PublicPlayerIdentityService(FirestoreSocialRepository(db)).ensure(uid)}
    private fun count(db:Firestore,uid:String)=db.document("players/$uid/socialCounters/current").get().get().getLong("friendCount")?:0L
    @Test fun `real transactions last free premium slot and two grant snapshots`()=db().use {db->
        for(premium in listOf(false,true)) {
            val a=player(db);val b=player(db);val c=player(db);val max=if(premium)100L else 5L
            db.document("players/${a.uid}/socialCounters/current").set(MatchCodec.map(SocialCounters(friendCount=(max-1).toInt()))).get()
            if(premium){val now=Instant.now();FirestoreEntitlements(db).adminGrant(a.uid,EntitlementGrant("test",EntitlementSource.ADMIN_GRANT,validFrom=now.minusSeconds(1),validUntil=now.plusSeconds(120),createdAt=now,policyVersion=1,reason="TEST",grantedBy="emulator"))}
            val s=FriendshipService(FirestoreFriendships(db),SocialCursor());val r1=s.send(b.uid,a.publicPlayerId).outgoingRequestId!!;val r2=s.send(c.uid,a.publicPlayerId).outgoingRequestId!!
            val pool=Executors.newFixedThreadPool(2);val gate=CyclicBarrier(2)
            val results=try{pool.invokeAll(listOf(r1,r2).map{r->Callable{gate.await();runCatching{s.resolve(a.uid,r,FriendRequestStatus.ACCEPTED)}.isSuccess}}).map{it.get()}}finally{pool.shutdownNow()}
            assertEquals(1,results.count{it});assertEquals(max,count(db,a.uid))
        }
    }
    @Test fun `accept races block with inverse and projections consistent`()=db().use {db->
        val a=player(db);val b=player(db);val s=FriendshipService(FirestoreFriendships(db),SocialCursor());val r=s.send(a.uid,b.publicPlayerId).outgoingRequestId!!
        val pool=Executors.newFixedThreadPool(2);val gate=CyclicBarrier(2)
        try{pool.invokeAll(listOf(Callable{gate.await();runCatching{s.resolve(b.uid,r,FriendRequestStatus.ACCEPTED)}},Callable{gate.await();runCatching{FirestoreSocialRepository(db).block(a.uid,SocialCandidate(b.uid,PublicPlayerProfile(b.publicPlayerId,b.friendCode,"Guest"),""),true)}})).forEach{it.get()}}finally{pool.shutdownNow()}
        assertTrue(db.document("players/${a.uid}/blocks/${b.uid}").get().get().exists())
        assertFalse(db.document("friendships/${SocialPairIdentity.id(a.uid,b.uid)}").get().get().exists())
        assertFalse(db.document("players/${a.uid}/friends/${b.uid}").get().get().exists());assertEquals(0,count(db,a.uid));assertEquals(0,count(db,b.uid))
    }
    @Test fun `emulator lifecycle costs and current profiles`()=db().use {raw->
        val a=player(raw);val b=player(raw);val db=SocialMeasurements.measured(raw) as Firestore;val s=FriendshipService(FirestoreFriendships(db),SocialCursor())
        fun measure(name:String,work:()->Unit){SocialMeasurements.counts.clear();work();println("S12_COST $name reads=${SocialMeasurements.counts["reads"]?.get()?:0} writes=${SocialMeasurements.counts["writes"]?.get()?:0}")}
        var id=""
        measure("SEND"){id=s.send(a.uid,b.publicPlayerId).outgoingRequestId!!};measure("DUPLICATE_SEND"){s.send(a.uid,b.publicPlayerId)}
        measure("ACCEPT"){s.resolve(b.uid,id,FriendRequestStatus.ACCEPTED)}
        measure("FRIENDS_PAGE_ONE"){assertEquals(1,s.friends(a.uid,null,20).items.size)}
        measure("UNFRIEND"){s.remove(a.uid,b.publicPlayerId)}
        measure("CANCEL"){id=s.send(a.uid,b.publicPlayerId).outgoingRequestId!!;SocialMeasurements.counts.clear();s.resolve(a.uid,id,FriendRequestStatus.CANCELED)}
        id=s.send(a.uid,b.publicPlayerId).outgoingRequestId!!
        measure("DECLINE"){s.resolve(b.uid,id,FriendRequestStatus.DECLINED)}
        assertEquals(0,count(raw,a.uid));assertEquals(0,count(raw,b.uid))
    }
    @Test fun `twenty row pages and blocking friendship costs`()=db().use {raw->
        val a=player(raw);var now=Instant.now();val clock=object:Clock(){override fun getZone()=ZoneOffset.UTC;override fun withZone(z:ZoneId)=this;override fun instant()=now}
        FirestoreEntitlements(raw).adminGrant(a.uid,EntitlementGrant("page-test",EntitlementSource.ADMIN_GRANT,validFrom=now.minusSeconds(1),validUntil=now.plusSeconds(3600),createdAt=now,policyVersion=1,reason="TEST",grantedBy="emulator"))
        val targets=(1..20).map{player(raw)};val db=SocialMeasurements.measured(raw) as Firestore;val s=FriendshipService(FirestoreFriendships(db),SocialCursor(),clock=clock)
        val requests=targets.map {s.send(it.uid,a.publicPlayerId).outgoingRequestId!!}
        fun measure(name:String,work:()->Unit){SocialMeasurements.counts.clear();work();println("S12_COST $name reads=${SocialMeasurements.counts["reads"]?.get()?:0} writes=${SocialMeasurements.counts["writes"]?.get()?:0}")}
        measure("INCOMING_PAGE_20"){assertEquals(20,s.requests(a.uid,"INCOMING",null,20).items.size)}
        requests.forEach{s.resolve(a.uid,it,FriendRequestStatus.ACCEPTED)}
        measure("FRIENDS_PAGE_20"){assertEquals(20,s.friends(a.uid,null,20).items.size)}
        val target=targets.first()
        measure("BLOCK_EXISTING_FRIEND"){FirestoreSocialRepository(db).block(a.uid,SocialCandidate(target.uid,PublicPlayerProfile(target.publicPlayerId,target.friendCode,"Guest"),""),true)}
        assertEquals(19,count(raw,a.uid));assertEquals(0,count(raw,target.uid))
        targets.forEach{s.remove(a.uid,it.publicPlayerId)}
        // Reuse the same twenty users with no extra emulator population. Unblock explicitly.
        FirestoreSocialRepository(raw).block(a.uid,SocialCandidate(target.uid,PublicPlayerProfile(target.publicPlayerId,target.friendCode,"Guest"),""),false)
        targets.forEachIndexed {i,p->if(i%5==0)now=now.plusSeconds(61);s.send(a.uid,p.publicPlayerId)}
        measure("OUTGOING_PAGE_20"){assertEquals(20,s.requests(a.uid,"OUTGOING",null,20).items.size)}
    }
}
