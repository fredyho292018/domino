package com.teamfho.domino.social

import com.teamfho.domino.match.MatchCodec
import org.junit.jupiter.api.Test
import java.util.concurrent.*
import kotlin.test.*

class FollowTests {
    private fun service(f:FriendFixture)=FollowService(f.db,SocialCursor(),f.clock)
    private fun reconcile(f:FriendFixture) {
        for(uid in listOf("a","b","c")) {
            assertEquals(f.db.docs.keys.count{it.startsWith("players/$uid/followers/")},f.count(uid).followerCount)
            assertEquals(f.db.docs.keys.count{it.startsWith("players/$uid/following/")},f.count(uid).followingCount)
        }
    }
    private fun race(vararg actions:()->Unit) {
        val pool=Executors.newFixedThreadPool(actions.size);val gate=CyclicBarrier(actions.size)
        try{pool.invokeAll(actions.map{a->Callable{gate.await();a()}}).forEach{it.get()}}finally{pool.shutdownNow()}
    }
    @Test fun `directional idempotency mutual follow and independent removal`() {
        val f=FriendFixture();val a=f.player("a");val b=f.player("b");val s=service(f)
        repeat(2){s.set("a",b,true)};assertEquals(FollowCounts(0,1),s.counts("a"));assertEquals(FollowCounts(1,0),s.counts("b"))
        s.set("b",a,true);assertEquals(FollowCounts(1,1),s.counts("a"));assertEquals(FollowCounts(1,1),s.counts("b"))
        repeat(2){s.set("a",b,false)};assertTrue(s.isFollowing("b","a"));assertFalse(s.isFollowing("a","b"));reconcile(f)
    }
    @Test fun `friend accept remove decline cancel preserve follows and no auto follow`() {
        for(action in listOf(FriendRequestStatus.ACCEPTED,FriendRequestStatus.DECLINED,FriendRequestStatus.CANCELED)) {
            val f=FriendFixture();f.player("a");val b=f.player("b");val s=service(f)
            val id=f.send("a","b");s.set("a",b,true)
            f.service.resolve(if(action==FriendRequestStatus.CANCELED)"a" else "b",id,action)
            f.service.remove("a",b);assertTrue(s.isFollowing("a","b"));assertFalse(s.isFollowing("b","a"));reconcile(f)
        }
        val f=FriendFixture();f.player("a");f.player("b");f.service.resolve("b",f.send("a","b"),FriendRequestStatus.ACCEPTED)
        assertEquals(FollowCounts(0,0),service(f).counts("a"))
    }
    @Test fun `privacy changes preserve existing follows but reject new formation`() {
        val f=FriendFixture();f.player("a");val b=f.player("b");val s=service(f);s.set("a",b,true)
        f.db.docs["players/b/socialSettings/current"]=mapOf("follow" to "NO_ONE")
        s.set("a",b,true);assertTrue(s.isFollowing("a","b"));s.set("a",b,false)
        assertEquals("SOCIAL_ACTION_NOT_ALLOWED",assertFailsWith<SocialFailure>{s.set("a",b,true)}.code);reconcile(f)
    }
    @Test fun `block cleans both directions friends and pending atomically`() {
        for(friend in listOf(false,true)) {
            val f=FriendFixture();val a=f.player("a");val b=f.player("b");val s=service(f)
            val request=f.send("a","b");if(friend)f.service.resolve("b",request,FriendRequestStatus.ACCEPTED)
            s.set("a",b,true);s.set("b",a,true);repeat(2){f.block("a","b")}
            assertEquals(SocialCounters(),f.count("a"));assertEquals(SocialCounters(),f.count("b"))
            assertFalse(f.db.docs.containsKey("friendships/${SocialPairIdentity.id("a","b")}"))
            assertFailsWith<SocialFailure>{s.set("b",a,true)}
            f.db.docs.remove("players/a/blocks/b");assertFalse(s.isFollowing("a","b"));assertFalse(s.isFollowing("b","a"));reconcile(f)
        }
    }
    @Test fun `concurrent duplicate opposing and shared counter mutations`() {
        val f=FriendFixture();val a=f.player("a");val b=f.player("b");val c=f.player("c");val s=service(f)
        race({s.set("a",b,true)},{s.set("a",b,true)});reconcile(f)
        race({s.set("a",b,false)},{s.set("a",b,true)});reconcile(f)
        race({s.set("a",b,true)},{s.set("b",a,true)},{s.set("c",b,true)},{s.set("a",c,true)});reconcile(f)
        assertEquals(2,s.counts("b").followerCount);assertEquals(2,s.counts("a").followingCount)
    }
    @Test fun `follow and unfollow races block always leave no edges`() {
        for(enabled in listOf(false,true)) {
            val f=FriendFixture();f.player("a");val b=f.player("b");val s=service(f);s.set("a",b,true)
            race({try{s.set("a",b,enabled)}catch(_:SocialFailure){}},{f.block("b","a")})
            assertFalse(s.isFollowing("a","b"));assertEquals(FollowCounts(0,0),s.counts("a"));reconcile(f)
        }
    }
    @Test fun `eligibility self block and test accounts do not mutate`() {
        val f=FriendFixture();val a=f.player("a");val b=f.player("b");val s=service(f)
        assertEquals("SELF_RELATION_NOT_ALLOWED",assertFailsWith<SocialFailure>{s.set("a",a,true)}.code)
        f.db.docs["developmentTestAccounts/b"]=mapOf("isTestAccount" to true)
        assertEquals("PLAYER_NOT_FOUND",assertFailsWith<SocialFailure>{s.set("a",b,true)}.code)
        f.db.docs.remove("developmentTestAccounts/b");f.db.docs["players/b"]=mapOf("status" to "DELETED")
        assertFailsWith<SocialFailure>{s.set("a",b,true)};reconcile(f)
    }
    @Test fun `paged current profiles cursor account binding and safe DTO`() {
        val f=FriendFixture();f.player("a");val s=service(f)
        (1..21).forEach{s.set("a",f.player("p$it"),true)}
        val page=s.list("a",true,null,20);assertEquals(20,page.items.size);assertNotNull(page.nextCursor)
        assertEquals(1,s.list("a",true,page.nextCursor,20).items.size)
        assertFailsWith<SocialFailure>{s.list("b",true,page.nextCursor,20)}
        assertFailsWith<SocialFailure>{s.list("a",false,page.nextCursor,20)}
        assertFailsWith<SocialFailure>{s.list("a",true,page.nextCursor+"x",20)}
        val json=MatchCodec.map(page).toString();assertFalse(json.contains("internalUid"));assertFalse(json.contains("followerCount"));assertFalse(json.contains("blockedBy"))
        // Linking changes provider metadata, not the UID-scoped graph.
        f.db.docs["players/a"]=mapOf("status" to "ACTIVE","isAnonymous" to false)
        assertEquals(21,s.counts("a").followingCount)
    }
    @Test fun `privacy patch preserves unspecified fields and revision no op`() {
        val old=SocialPrivacySettings(true,follow=ContactPermission.NO_ONE)
        val next=PrivacyPatch(revision=1,friendRequests=ContactPermission.NO_ONE,presenceVisibility=SocialVisibility.NO_ONE).apply(old)
        assertEquals(ContactPermission.NO_ONE,next.follow);assertTrue(next.discoverableByName);assertEquals(2,next.revision)
        assertEquals(next,PrivacyPatch(revision=2,follow=ContactPermission.NO_ONE).apply(next))
    }
}
