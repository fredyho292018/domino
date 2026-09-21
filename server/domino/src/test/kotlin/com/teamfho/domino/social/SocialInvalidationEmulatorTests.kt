package com.teamfho.domino.social

import com.google.cloud.firestore.*
import com.teamfho.domino.player.FirestorePlayerFoundationRepository
import com.teamfho.domino.security.FirebaseIdentity
import org.junit.jupiter.api.Tag
import org.junit.jupiter.api.Test
import org.mockito.Mockito
import org.mockito.stubbing.Answer
import java.time.Clock
import java.util.UUID
import java.util.concurrent.*
import kotlin.test.*

@Tag("EMULATOR")
class SocialInvalidationEmulatorTests {
    private fun db():Firestore {check(System.getenv("FIRESTORE_EMULATOR_HOST")=="127.0.0.1:18085");return FirestoreOptions.newBuilder().setProjectId("demo-domino-f0").setEmulatorHost("127.0.0.1:18085").setCredentials(FirestoreOptions.EmulatorCredentials()).build().service}
    private fun player(db:Firestore):PublicPlayerIdentity {val uid="a3-${UUID.randomUUID()}";FirestorePlayerFoundationRepository(db,Clock.systemUTC()).ensure(FirebaseIdentity(uid,true),"en","Guest-ABCDEFGH");return PublicPlayerIdentityService(FirestoreSocialRepository(db)).ensure(uid)}
    private fun candidate(p:PublicPlayerIdentity)=SocialCandidate(p.uid,PublicPlayerProfile(p.publicPlayerId,p.friendCode,"Guest"),"")
    private fun revision(db:Firestore,a:PublicPlayerIdentity,b:PublicPlayerIdentity)=db.document("socialPairs/${SocialPairIdentity.id(a.uid,b.uid)}").get().get().getLong("authorizationRevision")?:0
    private fun event(db:Firestore,e:SocialInvalidation)=db.document("${SocialInvalidation.COLLECTION}/${e.eventId}").get().get()
    private fun measure(name:String,body:()->Unit){SocialMeasurements.counts.clear();body();println("A3_COST $name reads=${SocialMeasurements.counts["reads"]?.get()?:0} writes=${SocialMeasurements.counts["writes"]?.get()?:0}")}
    @Test fun `atomic lifecycle noops legacy revision and precise costs`()=db().use {raw->
        val a=player(raw);val b=player(raw);val measured=SocialMeasurements.measured(raw) as Firestore
        val received=mutableListOf<SocialInvalidation>();val sink=SocialInvalidationSink{received.addAll(it)}
        val social=FirestoreSocialRepository(measured,sink);val friends=FriendshipService(FirestoreFriendships(measured,sink),SocialCursor())
        val id=SocialPairIdentity.id(a.uid,b.uid)
        measure("BLOCK_STRANGER"){social.block(a.uid,candidate(b),true)}
        assertEquals(1,received.size);assertEquals(1,revision(raw,a,b));assertTrue(event(raw,received.last()).exists())
        social.block(a.uid,candidate(b),true);assertEquals(1,received.size)
        measure("UNBLOCK"){social.block(a.uid,candidate(b),false)};assertEquals(2,received.size)
        social.block(a.uid,candidate(b),false);assertEquals(2,received.size)
        val r=friends.send(a.uid,b.publicPlayerId).outgoingRequestId!!;assertEquals(2,received.size)
        measure("ACCEPT"){friends.resolve(b.uid,r,FriendRequestStatus.ACCEPTED)};assertEquals(3,received.size)
        friends.resolve(b.uid,r,FriendRequestStatus.ACCEPTED);assertEquals(3,received.size)
        measure("UNFRIEND"){friends.remove(a.uid,b.publicPlayerId)};assertEquals(4,received.size)
        friends.remove(a.uid,b.publicPlayerId);assertEquals(4,received.size)
        measure("PRIVACY"){social.patchPrivacy(b.uid,PrivacyPatch(revision=1,presenceVisibility=SocialVisibility.NO_ONE))}
        assertEquals(5,received.size)
        social.patchPrivacy(b.uid,PrivacyPatch(revision=2,presenceVisibility=SocialVisibility.NO_ONE));assertEquals(5,received.size)
        assertFails{social.patchPrivacy(b.uid,PrivacyPatch(revision=1,presenceVisibility=SocialVisibility.EVERYONE))}
        social.patchPrivacy(b.uid,PrivacyPatch(revision=2,follow=ContactPermission.NO_ONE));assertEquals(5,received.size)
        assertEquals(listOf(1L,2L,3L,4L),received.filter{it.pairId==id}.map{it.revision})
        assertTrue(received.all{event(raw,it).getTimestamp("createdAt")!=null})
    }
    private fun abortAfterWrites(raw:Firestore):Firestore = Mockito.mock(Firestore::class.java,Answer {call->
        val args=call.rawArguments.copyOf()
        if(call.method.name=="runTransaction") {
            @Suppress("UNCHECKED_CAST") val body=args[0] as Transaction.Function<Any?>
            args[0]=Transaction.Function<Any?>{tx->body.updateCallback(tx);throw IllegalStateException("injected before commit")}
        }
        try{call.method.invoke(raw,*args)}catch(e:java.lang.reflect.InvocationTargetException){throw e.targetException}
    })
    @Test fun `all five mutation transactions abort outbox and changes together`()=db().use {raw->
        val a=player(raw);val b=player(raw);val id=SocialPairIdentity.id(a.uid,b.uid)
        val normal=FirestoreSocialRepository(raw);var notices=0
        val failing=FirestoreSocialRepository(abortAfterWrites(raw),SocialInvalidationSink{notices+=it.size})
        assertFails{failing.block(a.uid,candidate(b),true)};assertFalse(normal.hasBlockEitherDirection(a.uid,b.uid));assertEquals(0,revision(raw,a,b));assertFalse(event(raw,SocialInvalidation.pair(id,1)).exists())
        normal.block(a.uid,candidate(b),true)
        assertFails{failing.block(a.uid,candidate(b),false)};assertTrue(normal.hasBlockEitherDirection(a.uid,b.uid));assertEquals(1,revision(raw,a,b));assertFalse(event(raw,SocialInvalidation.pair(id,2)).exists())
        normal.block(a.uid,candidate(b),false)
        val friends=FriendshipService(FirestoreFriendships(raw),SocialCursor());val badFriends=FriendshipService(FirestoreFriendships(abortAfterWrites(raw)),SocialCursor())
        val r=friends.send(a.uid,b.publicPlayerId).outgoingRequestId!!
        assertFails{badFriends.resolve(b.uid,r,FriendRequestStatus.ACCEPTED)};assertEquals(0,friends.summary(a.uid).friendCount);assertFalse(event(raw,SocialInvalidation.pair(id,3)).exists())
        friends.resolve(b.uid,r,FriendRequestStatus.ACCEPTED)
        assertFails{badFriends.remove(a.uid,b.publicPlayerId)};assertEquals(1,friends.summary(a.uid).friendCount);assertFalse(event(raw,SocialInvalidation.pair(id,4)).exists())
        assertFails{failing.patchPrivacy(b.uid,PrivacyPatch(revision=1,presenceVisibility=SocialVisibility.NO_ONE))}
        assertEquals(1,normal.privacy(b.uid).revision);assertFalse(event(raw,SocialInvalidation.privacy(b.uid,2)).exists());assertEquals(0,notices)
    }
    @Test fun `missed publication recovered with pagination and retained published events`()=db().use {raw->
        val measured=SocialMeasurements.measured(raw) as Firestore;val feed=FirestoreSocialInvalidationFeed(measured)
        var cursor=feed.start();val a=player(raw);val b=player(raw);val social=FirestoreSocialRepository(raw)
        val remote=LocalSocialAuthorizationIndex(Executor{it.run()});var reads=0
        val h=remote.register("remote","a",b.uid){_,_->reads++;SocialAuthorizationSnapshot(social.privacy(b.uid).presenceVisibility!=SocialVisibility.NO_ONE,0,social.privacy(b.uid).revision)}
        remote.recovered();assertTrue(h.canDeliver())
        social.patchPrivacy(b.uid,PrivacyPatch(revision=1,presenceVisibility=SocialVisibility.NO_ONE))
        // No Pub/Sub at all. Only the durable feed revokes the remote handle.
        var page:SocialFeedPage?=null
        measure("FEED_ONE"){page=feed.recover(cursor)};page!!.events.forEach(remote::invalidate);cursor=page!!.cursor
        assertFalse(h.canDeliver())
        measure("FEED_EMPTY"){page=feed.recover(cursor)};assertTrue(page!!.events.isEmpty());cursor=page!!.cursor
        repeat(21){social.block(a.uid,candidate(b),it%2==0)}
        measure("FEED_TWENTY"){page=feed.recover(cursor)};assertEquals(20,page!!.events.size);assertFalse(page!!.caughtUp)
        val first=page!!.events;page=feed.recover(page!!.cursor);assertEquals(1,page.events.size)
        first.forEach(feed::published);assertTrue(first.all{event(raw,it).exists()})
        val recovery=feed.recover(cursor);assertEquals(first,recovery.events)
    }
    @Test fun `complex block and accept preserve limits with outbox`()=db().use {raw->
        val a=player(raw);val b=player(raw);val measured=SocialMeasurements.measured(raw) as Firestore
        val friends=FriendshipService(FirestoreFriendships(measured),SocialCursor());val follow=FollowService(FirestoreFriendships(raw),SocialCursor())
        val r=friends.send(a.uid,b.publicPlayerId).outgoingRequestId!!;friends.resolve(b.uid,r,FriendRequestStatus.ACCEPTED)
        follow.set(a.uid,b.publicPlayerId,true);follow.set(b.uid,a.publicPlayerId,true)
        measure("BLOCK_FRIEND_MUTUAL"){FirestoreSocialRepository(measured).block(a.uid,candidate(b),true)}
        assertEquals(0,friends.summary(a.uid).friendCount);assertFalse(follow.isFollowing(a.uid,b.uid));assertFalse(follow.isFollowing(b.uid,a.uid))
        assertEquals(2,revision(raw,a,b))
    }
    @Test fun `concurrent duplicate blocks increment exactly once and retry deterministic event id`()=db().use {raw->
        val a=player(raw);val b=player(raw);val social=FirestoreSocialRepository(raw);val pool=Executors.newFixedThreadPool(2);val gate=CyclicBarrier(2)
        try{pool.invokeAll((1..2).map{Callable{gate.await();social.block(a.uid,candidate(b),true)}}).forEach{it.get()}}finally{pool.shutdownNow()}
        assertEquals(1,revision(raw,a,b));assertTrue(event(raw,SocialInvalidation.pair(SocialPairIdentity.id(a.uid,b.uid),1)).exists())
    }
    @Test fun `SDK retry commits one logical outbox and invokes local invalidation once`()=db().use {raw->
        val a=player(raw);val b=player(raw);val pair=SocialPairIdentity.id(a.uid,b.uid);var attempts=0;var notices=0
        FirestoreFriendships(raw,SocialInvalidationSink{notices+=it.size}).atomic {tx->
            val data=tx.read("socialPairs/$pair")?:emptyMap()
            val revision=((data["authorizationRevision"] as? Number)?.toLong()?:0)+1
            tx.put("socialPairs/$pair",mapOf("lowerUid" to minOf(a.uid,b.uid),"upperUid" to maxOf(a.uid,b.uid),"authorizationRevision" to revision))
            tx.invalidate(SocialInvalidation.pair(pair,revision))
            if(++attempts==1)throw ExecutionException(com.google.api.gax.rpc.ApiExceptionFactory.createException(IllegalStateException("injected"),com.google.api.gax.grpc.GrpcStatusCode.of(io.grpc.Status.Code.ABORTED),true))
        }
        assertEquals(2,attempts);assertEquals(1,notices);assertEquals(1,revision(raw,a,b))
        assertTrue(event(raw,SocialInvalidation.pair(pair,1)).exists());assertFalse(event(raw,SocialInvalidation.pair(pair,2)).exists())
    }
    @Test fun `dispatch batch costs and duplicate publishers never remove durable events`()=db().use {raw->
        val measured=SocialMeasurements.measured(raw) as Firestore;val feed=FirestoreSocialInvalidationFeed(measured)
        val a=player(raw);val b=player(raw);val social=FirestoreSocialRepository(raw)
        repeat(21){social.block(a.uid,candidate(b),it%2==0)}
        var pending=listOf<SocialInvalidation>()
        measure("DISPATCH_ONE"){assertEquals(1,feed.pending(1).size)}
        measure("DISPATCH_TWENTY"){pending=feed.pending()};assertEquals(20,pending.size)
        val event=pending.first();measure("DISPATCH_ACK_ONE"){feed.published(event)};feed.published(event)
        assertTrue(event(raw,event).exists());assertEquals(true,event(raw,event).getBoolean("published"))
    }
    @Test fun `reauthorization coalesces one twenty and fifty identical subscriptions`()=db().use {raw->
        val a=player(raw);val b=player(raw);val measured=SocialMeasurements.measured(raw) as Firestore
        for(size in listOf(1,20,50)) {
            val i=LocalSocialAuthorizationIndex(Executor{it.run()})
            val reader=SocialAuthorizationReader {viewer,target->FirestoreFriendships(measured).atomic {tx->
                val pair=tx.read("socialPairs/${SocialPairIdentity.id(viewer,target)}")
                val privacy=tx.read("players/$target/socialSettings/current")!!
                val ab=tx.read("players/$viewer/blocks/$target");val ba=tx.read("players/$target/blocks/$viewer")
                val friend=tx.read("friendships/${SocialPairIdentity.id(viewer,target)}")
                SocialAuthorizationSnapshot(ab==null && ba==null && (privacy["presenceVisibility"]=="EVERYONE" || friend!=null),
                    (pair?.get("authorizationRevision") as? Number)?.toLong()?:0,(privacy["revision"] as Number).toLong())
            }}
            repeat(size){i.register("c",a.uid,b.uid,reader)};i.recovered()
            val social=FirestoreSocialRepository(raw)
            val next=social.patchPrivacy(b.uid,PrivacyPatch(revision=social.privacy(b.uid).revision,presenceVisibility=if(size==20)SocialVisibility.EVERYONE else SocialVisibility.NO_ONE))
            measure("REAUTHORIZE_$size"){i.invalidate(SocialInvalidation.privacy(b.uid,next.revision))}
            assertEquals(5,SocialMeasurements.counts.getValue("reads").get());i.closeConnection("c")
        }
    }
}
