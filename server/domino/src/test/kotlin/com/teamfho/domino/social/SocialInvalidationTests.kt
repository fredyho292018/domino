package com.teamfho.domino.social

import org.junit.jupiter.api.Test
import java.util.concurrent.Executor
import java.util.concurrent.RejectedExecutionException
import kotlin.test.*

class SocialInvalidationTests {
    private class Queue:Executor {val tasks=ArrayDeque<Runnable>();override fun execute(r:Runnable){tasks.add(r)};fun run(){while(tasks.isNotEmpty())tasks.removeFirst().run()}}
    @Test fun `invalidated before reauthorization and stale result cannot restore permission`() {
        val q=Queue();val i=LocalSocialAuthorizationIndex(q);var s=SocialAuthorizationSnapshot(true,0,1)
        val h=i.register("c","a","b"){_,_->s};i.recovered();q.run();assertTrue(h.canDeliver())
        i.invalidate(SocialInvalidation.pair(h.pair,2));assertFalse(h.canDeliver())
        q.run();assertFalse(h.canDeliver()) // durable revision 0 is older than notification 2
        s=SocialAuthorizationSnapshot(false,2,1);i.refreshExpired();q.run();assertFalse(h.canDeliver())
        i.invalidate(SocialInvalidation.pair(h.pair,1));assertTrue(q.tasks.isEmpty());assertFalse(h.canDeliver())
    }
    @Test fun `duplicate and out of order events cause no repeated reads`() {
        val q=Queue();val i=LocalSocialAuthorizationIndex(q);var n=0
        val h=i.register("c","a","b"){_,_->n++;SocialAuthorizationSnapshot(true,8,1)}
        i.recovered();q.run();val before=n
        for(r in listOf(8L,8L,7L,6L))i.invalidate(SocialInvalidation.pair(h.pair,r))
        q.run();assertEquals(before,n);assertTrue(h.canDeliver())
    }
    @Test fun `failure rejected executor and expired recovery lease fail closed`() {
        var time=0L;val q=Queue();val i=LocalSocialAuthorizationIndex(q,{time});var fail=false
        val h=i.register("c","a","b"){_,_->if(fail)error("unavailable")else SocialAuthorizationSnapshot(true,1,1)}
        i.recovered();q.run();assertTrue(h.canDeliver());time=31_000_000_000;assertFalse(h.canDeliver())
        fail=true;i.recovered();q.run();assertFalse(h.canDeliver())
        val rejected=LocalSocialAuthorizationIndex(Executor{throw RejectedExecutionException()})
        val denied=rejected.register("c","a","b"){_,_->SocialAuthorizationSnapshot(true,0,1)}
        rejected.recovered();assertFalse(denied.canDeliver())
    }
    @Test fun `target and pair indexes affect only matching handles with batched identical readers`() {
        val q=Queue();val i=LocalSocialAuthorizationIndex(q);var calls=0;var revision=1L
        val read=SocialAuthorizationReader{_,_->calls++;SocialAuthorizationSnapshot(true,0,revision)}
        val handles=(1..50).map{i.register("c","a","b",read)}
        val other=i.register("other","a","d",read);i.recovered();q.run();calls=0
        revision=2;i.invalidate(SocialInvalidation.privacy("b",2))
        assertTrue(handles.none{it.canDeliver()});assertTrue(other.canDeliver());q.run()
        assertEquals(1,calls);assertTrue(handles.all{it.canDeliver()})
        assertFails{i.register("c","a","x",read)}
        i.closeConnection("c");other.close();assertEquals(listOf(0,0,0,0),i.indexSizes())
    }
    @Test fun `account switching discards previous handles and lazy bounds apply`() {
        val q=Queue();val i=LocalSocialAuthorizationIndex(q,maxConnections=1)
        val h=i.register("c","a","b"){_,_->SocialAuthorizationSnapshot(true,0,1)}
        i.register("c","new","b"){_,_->SocialAuthorizationSnapshot(true,0,1)}
        assertFalse(h.canDeliver());assertEquals(1,i.size())
        assertFails{i.register("d","new","b"){_,_->SocialAuthorizationSnapshot(true,0,1)}}
        i.closeAccount("new");q.run();assertEquals(0,i.size())
    }
    @Test fun `untrusted event schemas cannot grant authority`() {
        val e=SocialInvalidation.pair(SocialPairIdentity.id("a","b"),1)
        for(bad in listOf(e.copy(schemaVersion=2),e.copy(revision=0),e.copy(eventId=""),e.copy(pairId="bad"),e.copy(targetUid="uid")))assertFails{bad.validate()}
        assertFails{SocialInvalidation.privacy("../bad",1).validate()}
        assertEquals(e,SocialInvalidation.decode(e.durableData()))
    }
    @Test fun `friend accept and unfriend revisions independent of request generation`() {
        val f=FriendFixture();f.player("a");val b=f.player("b");val r=f.send("a","b")
        fun events()=f.db.docs.keys.count{it.startsWith(SocialInvalidation.COLLECTION)}
        assertEquals(0,events());f.service.resolve("b",r,FriendRequestStatus.ACCEPTED)
        assertEquals(1,events());f.service.resolve("b",r,FriendRequestStatus.ACCEPTED);assertEquals(1,events())
        f.service.remove("a",b);f.service.remove("a",b);assertEquals(2,events())
        val pair=f.db.docs.getValue("socialPairs/${SocialPairIdentity.id("a","b")}")
        assertEquals(2L,(pair["authorizationRevision"] as Number).toLong());assertEquals(1L,(pair["generation"] as Number).toLong())
    }
    @Test fun `send decline cancel and follow do not emit invalidations`() {
        for(action in listOf(FriendRequestStatus.DECLINED,FriendRequestStatus.CANCELED)) {
            val f=FriendFixture();f.player("a");val b=f.player("b");val r=f.send("a","b")
            f.service.resolve(if(action==FriendRequestStatus.CANCELED)"a" else "b",r,action)
            FollowService(f.db,SocialCursor()).set("a",b,true);FollowService(f.db,SocialCursor()).set("a",b,false)
            assertTrue(f.db.docs.keys.none{it.startsWith(SocialInvalidation.COLLECTION)})
        }
    }
    @Test fun `outbox failure aborts friendship and preserves atomic limit state`() {
        val f=FriendFixture();f.player("a");f.player("b");val r=f.send("a","b");val before=f.db.docs.toMap()
        val repo=object:FriendshipRepository by f.db {
            override fun <T> atomic(body:(SocialTransaction)->T)=f.db.atomic {tx->body(object:SocialTransaction by tx {
                override fun put(path:String,data:Map<String,Any>){if(path.startsWith(SocialInvalidation.COLLECTION))error("outbox failure");tx.put(path,data)}
            })}
        }
        assertFails{FriendshipService(repo,SocialCursor()).resolve("b",r,FriendRequestStatus.ACCEPTED)}
        assertEquals(before,f.db.docs)
    }
}
