package com.teamfho.domino.social

import com.google.cloud.firestore.*
import com.teamfho.domino.player.FirestorePlayerFoundationRepository
import com.teamfho.domino.security.FirebaseIdentity
import org.junit.jupiter.api.Tag
import org.junit.jupiter.api.Test
import java.time.Clock
import java.util.UUID
import java.util.concurrent.Callable
import java.util.concurrent.Executors
import kotlin.test.*

@Tag("EMULATOR")
class SocialEmulatorTests {
    private fun database():Firestore {
        check(System.getenv("FIRESTORE_EMULATOR_HOST")=="127.0.0.1:18085")
        return FirestoreOptions.newBuilder().setProjectId("demo-domino-f0").setEmulatorHost("127.0.0.1:18085")
            .setCredentials(FirestoreOptions.EmulatorCredentials()).build().service
    }
    private fun player(db:Firestore,uid:String) = FirestorePlayerFoundationRepository(db,Clock.systemUTC()).ensure(FirebaseIdentity(uid,true),"en","Guest-ABCDEFGH")
    @Test fun `concurrent creation atomic immutable linked identity and alias projection`()=database().use { db->
        val uid="s11-${UUID.randomUUID()}";player(db,uid);val repo=FirestoreSocialRepository(db);val s=PublicPlayerIdentityService(repo)
        val pool=Executors.newFixedThreadPool(4)
        val id=try{val results=pool.invokeAll((1..8).map{Callable{s.ensure(uid)}}).map{it.get()};assertEquals(1,results.toSet().size);results.first()}finally{pool.shutdownNow()}
        assertEquals(id.publicPlayerId,repo.code(id.friendCode))
        val players=FirestorePlayerFoundationRepository(db,Clock.systemUTC());players.ensure(FirebaseIdentity(uid,false),"en","Guest-ABCDEFGH")
        players.updateDisplayName(FirebaseIdentity(uid,false),"Alice_1")
        assertEquals(id,s.ensure(uid));assertEquals("Alice_1",repo.resolve(id.publicPlayerId)!!.profile.displayName)
        val before=db.document("publicPlayerProfiles/${id.publicPlayerId}").get().get().updateTime
        players.updateDisplayName(FirebaseIdentity(uid,false),"Alice_1")
        assertEquals(before,db.document("publicPlayerProfiles/${id.publicPlayerId}").get().get().updateTime)
    }
    @Test fun `collisions leave no partial reservations and exhaustion bounded`()=database().use {db->
        val repo=FirestoreSocialRepository(db);val a="s11-${UUID.randomUUID()}";val b="s11-${UUID.randomUUID()}";player(db,a);player(db,b)
        val existing=PublicPlayerIdentityService(repo).ensure(a);val next=SecureIdentityGenerator().next(b)
        val attempts=ArrayDeque(listOf(next.copy(publicPlayerId=existing.publicPlayerId),next.copy(friendCode=existing.friendCode),next))
        val result=PublicPlayerIdentityService(repo,object:IdentityGenerator{override fun next(uid:String)=attempts.removeFirst()}).ensure(b)
        assertEquals(next,result);assertEquals(existing.publicPlayerId,repo.code(existing.friendCode))
    }
    @Test fun `legacy privacy code lookup revisions inverse blocks and current test marker filtering`()=database().use {db->
        val a="s11-${UUID.randomUUID()}";val b="s11-${UUID.randomUUID()}";player(db,a);player(db,b)
        db.document("players/$a").update("socialDefaultDiscoverable",FieldValue.delete()).get()
        val r=FirestoreSocialRepository(db);val ids=PublicPlayerIdentityService(r);val ai=ids.ensure(a);val bi=ids.ensure(b)
        assertFalse(r.privacy(a).discoverableByName);assertTrue(r.privacy(b).discoverableByName)
        val access=SocialAccess(r,r);val search=PlayerDiscoveryService(r,r,access,SocialCursor());val blocks=BlockService(r,access,SocialCursor())
        assertEquals(1,search.search(b,"FRIEND_CODE",null,ai.friendCode,null,20).items.size)
        blocks.set(a,bi.publicPlayerId,true);blocks.set(a,bi.publicPlayerId,true)
        assertEquals(1,blocks.list(a,null,20).items.size);assertTrue(db.document("players/$b/blockedBy/$a").get().get().exists())
        assertTrue(search.search(b,"FRIEND_CODE",null,ai.friendCode,null,20).items.isEmpty())
        assertFailsWith<SocialFailure>{access.visible(b,ai.publicPlayerId)}
        blocks.set(a,bi.publicPlayerId,false);blocks.set(a,bi.publicPlayerId,false)
        assertFalse(r.hasBlockEitherDirection(a,b));assertFalse(db.document("players/$b/blockedBy/$a").get().get().exists())
        assertEquals(2,r.patchPrivacy(a,PrivacyPatch(true,1)).revision)
        assertFails{r.patchPrivacy(a,PrivacyPatch(false,1))}
        db.document("developmentTestAccounts/$a").set(mapOf("isTestAccount" to true,"testSource" to "BOT_SWARM")).get()
        assertNull(r.resolve(ai.publicPlayerId));assertTrue(search.search(b,"FRIEND_CODE",null,ai.friendCode,null,20).items.isEmpty())
    }
    @Test fun `search bounded paging and emulator operation counts`()=database().use {raw->
        val db=SocialMeasurements.measured(raw) as Firestore;val r=FirestoreSocialRepository(db);val identities=PublicPlayerIdentityService(r)
        val uid="s11-cost-${UUID.randomUUID()}";player(raw,uid)
        fun measured(name:String,body:()->Unit) {SocialMeasurements.counts.clear();body();println("S11_COST $name "+SocialMeasurements.counts.mapValues{it.value.get()})}
        lateinit var owner:PublicPlayerIdentity
        measured("IDENTITY_CREATE"){owner=identities.ensure(uid)}
        measured("IDENTITY_EXISTING"){assertEquals(owner,identities.ensure(uid))}
        val prefix="s"+UUID.randomUUID().toString().replace("-", "").take(7)
        val targets=(1..21).map {n->val other="s11-${UUID.randomUUID()}";player(raw,other)
            FirestorePlayerFoundationRepository(raw,Clock.systemUTC()).updateDisplayName(FirebaseIdentity(other,true),prefix+n.toString().padStart(2,'0'));identities.ensure(other)}
        val access=SocialAccess(r,r);val discovery=PlayerDiscoveryService(r,r,access,SocialCursor());val blocks=BlockService(r,access,SocialCursor())
        var page:SocialPage<PublicPlayerProfile>?=null
        measured("NAME_SEARCH_20"){page=discovery.search(uid,"NAME",prefix,null,null,20);assertEquals(20,page!!.items.size)}
        assertEquals(1,discovery.search(uid,"NAME",prefix,null,page!!.nextCursor,20).items.size)
        measured("CODE_LOOKUP"){assertEquals(1,discovery.search(uid,"FRIEND_CODE",null,targets[0].friendCode,null,20).items.size)}
        measured("PROFILE"){access.visible(uid,targets[0].publicPlayerId)}
        measured("BLOCK"){blocks.set(uid,targets[0].publicPlayerId,true)}
        measured("BLOCK_REPEAT"){blocks.set(uid,targets[0].publicPlayerId,true)}
        measured("BLOCK_LIST"){blocks.list(uid,null,20)}
        measured("UNBLOCK"){blocks.set(uid,targets[0].publicPlayerId,false)}
        measured("PRIVACY"){r.patchPrivacy(uid,PrivacyPatch(false,1))}
    }
    @Test fun `concurrent block unblock preserve inverse relation and hidden target can be unblocked`()=database().use {db->
        val a="s11-${UUID.randomUUID()}";val b="s11-${UUID.randomUUID()}";player(db,a);player(db,b)
        val r=FirestoreSocialRepository(db);val id=PublicPlayerIdentityService(r).ensure(b);val target=r.resolve(id.publicPlayerId)!!
        val pool=Executors.newFixedThreadPool(4)
        try{pool.invokeAll((1..12).map{n->Callable{r.block(a,target,n%2==0)}}).forEach{it.get()}}finally{pool.shutdownNow()}
        assertEquals(db.document("players/$a/blocks/$b").get().get().exists(),db.document("players/$b/blockedBy/$a").get().get().exists())
        r.block(a,target,true);db.document("developmentTestAccounts/$b").set(mapOf("isTestAccount" to true)).get()
        val service=BlockService(r,SocialAccess(r,r),SocialCursor());repeat(2){service.set(a,id.publicPlayerId,false)}
        assertFalse(r.hasBlockEitherDirection(a,b))
    }
}
