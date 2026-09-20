package com.teamfho.domino.social

import com.google.cloud.firestore.*
import com.teamfho.domino.player.FirestorePlayerFoundationRepository
import com.teamfho.domino.security.FirebaseIdentity
import org.junit.jupiter.api.Tag
import org.junit.jupiter.api.Test
import java.time.Clock
import java.util.UUID
import java.util.concurrent.*
import kotlin.test.*

@Tag("EMULATOR")
class FollowEmulatorTests {
    private fun db():Firestore {check(System.getenv("FIRESTORE_EMULATOR_HOST")=="127.0.0.1:18085");return FirestoreOptions.newBuilder().setProjectId("demo-domino-f0").setEmulatorHost("127.0.0.1:18085").setCredentials(FirestoreOptions.EmulatorCredentials()).build().service}
    private fun player(db:Firestore):PublicPlayerIdentity {val uid="s13-${UUID.randomUUID()}";FirestorePlayerFoundationRepository(db,Clock.systemUTC()).ensure(FirebaseIdentity(uid,true),"en","Guest-ABCDEFGH");return PublicPlayerIdentityService(FirestoreSocialRepository(db)).ensure(uid)}
    private fun candidate(p:PublicPlayerIdentity)=SocialCandidate(p.uid,PublicPlayerProfile(p.publicPlayerId,p.friendCode,"Guest"),"")
    private fun reconcile(db:Firestore,vararg users:PublicPlayerIdentity) {
        for(p in users) {val c=FollowService(FirestoreFriendships(db),SocialCursor()).counts(p.uid)
            assertEquals(db.collection("players/${p.uid}/followers").get().get().size(),c.followerCount)
            assertEquals(db.collection("players/${p.uid}/following").get().get().size(),c.followingCount)
        }
    }
    private fun measure(name:String,body:()->Unit) {SocialMeasurements.counts.clear();body();println("S13_COST $name reads=${SocialMeasurements.counts["reads"]?.get()?:0} writes=${SocialMeasurements.counts["writes"]?.get()?:0}")}
    private fun race(vararg actions:()->Unit) {val gate=CyclicBarrier(actions.size);val pool=Executors.newFixedThreadPool(actions.size)
        try{pool.invokeAll(actions.map{a->Callable{gate.await();a()}}).forEach{it.get()}}finally{pool.shutdownNow()}}
    @Test fun `follow persistence privacy revision and measured lifecycle`()=db().use {raw->
        val a=player(raw);val b=player(raw);val db=SocialMeasurements.measured(raw) as Firestore;val s=FollowService(FirestoreFriendships(db),SocialCursor());val privacy=FirestoreSocialRepository(db)
        measure("FOLLOW"){s.set(a.uid,b.publicPlayerId,true)}
        measure("DUPLICATE_FOLLOW"){s.set(a.uid,b.publicPlayerId,true)}
        measure("FOLLOW_RELATIONSHIP_LOOKUP"){assertTrue(s.isFollowing(a.uid,b.uid))}
        measure("PRIVACY_UPDATE"){privacy.patchPrivacy(b.uid,PrivacyPatch(revision=1,follow=ContactPermission.NO_ONE,friendRequests=ContactPermission.NO_ONE,presenceVisibility=SocialVisibility.NO_ONE,matchActivityVisibility=SocialVisibility.EVERYONE))}
        assertEquals(SocialVisibility.NO_ONE,privacy.privacy(b.uid).presenceVisibility)
        assertFails{privacy.patchPrivacy(b.uid,PrivacyPatch(revision=1,follow=ContactPermission.EVERYONE))}
        s.set(a.uid,b.publicPlayerId,true)
        measure("UNFOLLOW"){s.set(a.uid,b.publicPlayerId,false)};measure("DUPLICATE_UNFOLLOW"){s.set(a.uid,b.publicPlayerId,false)}
        assertFails{s.set(a.uid,b.publicPlayerId,true)};reconcile(raw,a,b)
    }
    @Test fun `block write budget stranger one way mutual friend and pending`()=db().use {raw->
        val a=player(raw);val b=player(raw);val db=SocialMeasurements.measured(raw) as Firestore;val s=FollowService(FirestoreFriendships(db),SocialCursor());val block=FirestoreSocialRepository(db);val friends=FriendshipService(FirestoreFriendships(db),SocialCursor())
        measure("BLOCK_STRANGER"){block.block(a.uid,candidate(b),true)};block.block(a.uid,candidate(b),false)
        s.set(a.uid,b.publicPlayerId,true);measure("BLOCK_ONE_WAY_FOLLOW"){block.block(a.uid,candidate(b),true)};block.block(a.uid,candidate(b),false);reconcile(raw,a,b)
        measure("MUTUAL_FOLLOW"){s.set(a.uid,b.publicPlayerId,true);s.set(b.uid,a.publicPlayerId,true)}
        measure("BLOCK_MUTUAL_FOLLOW"){block.block(a.uid,candidate(b),true)};block.block(a.uid,candidate(b),false);reconcile(raw,a,b)
        for(accept in listOf(true,false)) {
            val r=friends.send(a.uid,b.publicPlayerId).outgoingRequestId!!;if(accept)friends.resolve(b.uid,r,FriendRequestStatus.ACCEPTED)
            s.set(a.uid,b.publicPlayerId,true);s.set(b.uid,a.publicPlayerId,true)
            measure(if(accept)"BLOCK_FRIEND_MUTUAL" else "BLOCK_PENDING_MUTUAL"){block.block(a.uid,candidate(b),true)}
            reconcile(raw,a,b);assertEquals(0,friends.summary(a.uid).friendCount);assertEquals(0,friends.summary(b.uid).pendingIncomingCount)
            block.block(a.uid,candidate(b),false);assertFalse(s.isFollowing(a.uid,b.uid))
        }
    }
    @Test fun `real concurrency directional shared counters block and privacy`()=db().use {db->
        val a=player(db);val b=player(db);val c=player(db);val s=FollowService(FirestoreFriendships(db),SocialCursor());val block=FirestoreSocialRepository(db)
        race({s.set(a.uid,b.publicPlayerId,true)},{s.set(a.uid,b.publicPlayerId,true)});reconcile(db,a,b)
        race({s.set(a.uid,b.publicPlayerId,false)},{s.set(a.uid,b.publicPlayerId,true)});reconcile(db,a,b)
        race({s.set(a.uid,b.publicPlayerId,true)},{s.set(b.uid,a.publicPlayerId,true)},{s.set(c.uid,b.publicPlayerId,true)},{s.set(a.uid,c.publicPlayerId,true)})
        reconcile(db,a,b,c);assertEquals(2,s.counts(b.uid).followerCount)
        race({try{s.set(a.uid,b.publicPlayerId,true)}catch(_:Exception){}},{block.block(b.uid,candidate(a),true)})
        assertFalse(s.isFollowing(a.uid,b.uid));assertFalse(s.isFollowing(b.uid,a.uid));reconcile(db,a,b,c)
        race({s.set(a.uid,c.publicPlayerId,false)},{block.block(c.uid,candidate(a),true)});reconcile(db,a,b,c)
        s.set(c.uid,b.publicPlayerId,false)
        race({try{s.set(c.uid,b.publicPlayerId,true)}catch(_:Exception){}},{block.patchPrivacy(b.uid,PrivacyPatch(revision=1,follow=ContactPermission.NO_ONE))})
        reconcile(db,a,b,c);assertEquals(ContactPermission.NO_ONE,block.privacy(b.uid).follow)
    }
    @Test fun `twenty row batched pages stable cursor and renamed profiles`()=db().use {raw->
        val a=player(raw);val targets=(1..21).map{player(raw)};val cursor=SocialCursor();val db=SocialMeasurements.measured(raw) as Firestore;val s=FollowService(FirestoreFriendships(db),cursor)
        targets.forEach{s.set(a.uid,it.publicPlayerId,true);s.set(it.uid,a.publicPlayerId,true)}
        for(following in listOf(true,false)) {
            var page:SocialPage<FollowRelationship>?=null
            measure(if(following)"FOLLOWING_PAGE_20" else "FOLLOWERS_PAGE_20"){page=s.list(a.uid,following,null,20)}
            assertEquals(20,page!!.items.size);assertEquals(1,s.list(a.uid,following,page!!.nextCursor,20).items.size)
            assertFailsWith<SocialFailure>{s.list(targets.first().uid,following,page!!.nextCursor,20)}
        }
        raw.document("publicPlayerProfiles/${targets.first().publicPlayerId}").update("displayName","Renamed").get()
        assertTrue(s.list(a.uid,true,null,50).items.any{it.profile.displayName=="Renamed"});reconcile(raw,a,*targets.toTypedArray())
    }
}
