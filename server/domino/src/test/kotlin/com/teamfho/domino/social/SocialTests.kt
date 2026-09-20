package com.teamfho.domino.social

import org.junit.jupiter.api.Test
import kotlin.test.*
import java.util.concurrent.Executors
import java.util.concurrent.Callable

class MemorySocial(private val friendStore:MemoryFriendships?=null) : PublicIdentityRepository, SocialPrivacyRepository, BlockRepository {
    val identities=mutableMapOf<String,PublicPlayerIdentity>();val profiles=mutableMapOf<String,SocialCandidate>()
    val settings=mutableMapOf<String,SocialPrivacySettings>();val blocked=mutableSetOf<Pair<String,String>>()
    val hidden=mutableSetOf<String>();var budget=0
    @Synchronized override fun ensure(uid:String,candidate:PublicPlayerIdentity):PublicPlayerIdentity? {
        identities[uid]?.let{return it}
        if(profiles.containsKey(candidate.publicPlayerId)||identities.values.any{it.friendCode==candidate.friendCode})return null
        identities[uid]=candidate;profiles[candidate.publicPlayerId]=SocialCandidate(uid,PublicPlayerProfile(candidate.publicPlayerId,candidate.friendCode,"Alice"),"alice")
        settings[uid]=SocialPrivacySettings(true)
        friendStore?.docs?.put("players/$uid",mapOf("status" to "ACTIVE"))
        friendStore?.docs?.put("players/$uid/publicIdentity/current",mapOf("publicPlayerId" to candidate.publicPlayerId))
        friendStore?.docs?.put("publicPlayerProfiles/${candidate.publicPlayerId}",mapOf("internalUid" to uid,"displayName" to "Alice","friendCode" to candidate.friendCode))
        return candidate
    }
    override fun resolve(publicId:String)=profiles[publicId]?.takeUnless{it.uid in hidden}
    override fun code(code:String)=identities.values.find{it.friendCode==code}?.publicPlayerId
    override fun privacy(uid:String)=settings.getValue(uid)
    @Synchronized override fun patchPrivacy(uid:String,patch:PrivacyPatch):SocialPrivacySettings {
        val old=privacy(uid);socialCheck(old.revision==patch.revision,"REVISION_MISMATCH",409)
        return patch.apply(old).also{settings[uid]=it;friendStore?.docs?.put("players/$uid/socialSettings/current",com.teamfho.domino.match.MatchCodec.map(it))}
    }
    override fun search(prefix:String,afterName:String?,afterId:String?,budget:Int):CandidatePage {
        this.budget=budget
        val all=profiles.values.filter{it.normalizedName.startsWith(prefix)&&privacy(it.uid).discoverableByName}
            .sortedWith(compareBy({it.normalizedName},{it.profile.publicPlayerId}))
            .filter{afterName==null||it.normalizedName>afterName||(it.normalizedName==afterName&&it.profile.publicPlayerId>afterId!!)}
        return CandidatePage(all.take(budget),all.size>=budget)
    }
    @Synchronized override fun hasBlockEitherDirection(a:String,b:String)=(a to b) in blocked||(b to a) in blocked
    override fun blockedTarget(a:String,publicId:String)=profiles[publicId]?.takeIf{(a to it.uid) in blocked}
    @Synchronized override fun block(a:String,target:SocialCandidate,enabled:Boolean){
        friendStore?.atomic{tx->if(enabled){FriendshipService(friendStore,SocialCursor()).removeInTransaction(tx,a,target.uid,true);tx.put("players/$a/blocks/${target.uid}",mapOf("blocked" to true))}else tx.delete("players/$a/blocks/${target.uid}")}
        if(enabled)blocked.add(a to target.uid)else blocked.remove(a to target.uid)
    }
    override fun blocks(a:String,afterId:String?,limit:Int)=blocked.filter{it.first==a}.map{profiles[identities.getValue(it.second).publicPlayerId]!!.profile}
        .filter{afterId==null||it.publicPlayerId>afterId}.sortedBy{it.publicPlayerId}.take(limit).map{BlockRelationship(it.publicPlayerId,it.displayName,it.friendCode)}
}
class SocialTests {
    private fun candidate(uid:String,n:Int)=PublicPlayerIdentity(uid,n.toString().padStart(22,'a'),"FHO-"+n.toString().padStart(12,'0'))
    private fun fixture():MemorySocial=MemorySocial().also {r->(1..65).forEach{r.ensure("u$it",candidate("u$it",it))}}
    private fun search(r:MemorySocial)=PlayerDiscoveryService(r,r,SocialAccess(r,r),SocialCursor())
    @Test fun `secure identifiers format and unique samples`() {
        val values=(1..500).map{SecureIdentityGenerator().next("private-uid")}
        assertEquals(500,values.map{it.publicPlayerId}.toSet().size);assertEquals(500,values.map{it.friendCode}.toSet().size)
        values.forEach{SocialNames.publicId(it.publicPlayerId);assertEquals(it.friendCode,SocialNames.code(it.friendCode.lowercase()))}
    }
    @Test fun `identity rejects invalid document segments before repository access`() {
        val r=MemorySocial();val s=PublicPlayerIdentityService(r)
        for(uid in listOf("",".","..","a/b","a".repeat(129)))assertFailsWith<SocialFailure>{s.ensure(uid)}
        assertTrue(r.identities.isEmpty())
    }
    @Test fun `identity retries public ID and code collisions without reservations`() {
        val r=MemorySocial();val first=candidate("one",1);r.ensure("one",first)
        val attempts=ArrayDeque(listOf(first.copy(uid="two",friendCode=candidate("two",2).friendCode),candidate("two",2).copy(friendCode=first.friendCode),candidate("two",3)))
        val service=PublicPlayerIdentityService(r,object:IdentityGenerator{override fun next(uid:String)=attempts.removeFirst()})
        assertEquals(candidate("two",3),service.ensure("two"));assertEquals(2,r.identities.size);assertEquals(2,r.profiles.size)
    }
    @Test fun `retry exhaustion is bounded`() {
        val r=fixture();var count=0
        val s=PublicPlayerIdentityService(r,object:IdentityGenerator{override fun next(uid:String)=candidate(uid,1).also{count++}})
        assertEquals("SOCIAL_IDENTITY_UNAVAILABLE",assertFailsWith<SocialFailure>{s.ensure("new")}.code);assertEquals(5,count)
    }
    @Test fun `concurrent creation same UID returns exactly one identity`() {
        val r=MemorySocial();val service=PublicPlayerIdentityService(r);val pool=Executors.newFixedThreadPool(6)
        try{val result=pool.invokeAll((1..20).map{Callable{service.ensure("same")}}).map{it.get()};assertEquals(1,result.toSet().size);assertEquals(1,r.profiles.size)}finally{pool.shutdownNow()}
    }
    @Test fun `name normalization and invalid queries`() {
        assertEquals("alice_1",SocialNames.query(" ALICE_1 "))
        for(q in listOf("ab","a".repeat(17),"abc*","a/b","abc@"))assertFailsWith<SocialFailure>{SocialNames.query(q)}
    }
    @Test fun `code normalization is exact`() {
        val code="FHO-0123456789AB";assertEquals(code,SocialNames.code(" $code ".lowercase()))
        for(q in listOf("XYZ-0123456789AB","FHO-0123","FHO-OOOOOOOOOOOO","FHO0123456789AB"))assertFailsWith<SocialFailure>{SocialNames.code(q)}
    }
    @Test fun `duplicate aliases pagination has no overlap`() {
        val r=fixture();val s=search(r);val ids=mutableSetOf<String>();var cursor:String?=null
        do{val page=s.search("viewer","NAME","Ali",null,cursor,20);page.items.forEach{assertTrue(ids.add(it.publicPlayerId))};cursor=page.nextCursor}while(cursor!=null)
        assertEquals(65,ids.size);assertEquals(60,r.budget)
    }
    @Test fun `cursor bound to viewer query and tamper and expiration`() {
        val c=SocialCursor();val token=c.encode("a","alice","alice","id")
        assertEquals("alice" to "id",c.decode(token,"a","alice"))
        for(pair in listOf("b" to "alice","a" to "bob"))assertFailsWith<SocialFailure>{c.decode(token,pair.first,pair.second)}
        assertFailsWith<SocialFailure>{c.decode("invalid", "a","alice")}
        val key=ByteArray(32);val old=SocialCursor(key,java.time.Clock.fixed(java.time.Instant.EPOCH,java.time.ZoneOffset.UTC))
        assertFailsWith<SocialFailure>{SocialCursor(key).decode(old.encode("a","q","n","i"),"a","q")}
    }
    @Test fun `blocked candidate budget returns partial page with cursor`() {
        val r=fixture();r.profiles.values.sortedBy{it.profile.publicPlayerId}.take(60).forEach{r.blocked.add("viewer" to it.uid)}
        val page=search(r).search("viewer","NAME","alice",null,null,20)
        assertTrue(page.items.isEmpty());assertNotNull(page.nextCursor);assertEquals(60,r.budget)
    }
    @Test fun `both block directions hide name code and profile`() {
        val r=fixture();val target=r.profiles.getValue(candidate("u1",1).publicPlayerId)
        for(pair in listOf("viewer" to "u1","u1" to "viewer")) {
            r.blocked.clear();r.blocked.add(pair)
            assertTrue(search(r).search("viewer","FRIEND_CODE",null,target.profile.friendCode,null,20).items.isEmpty())
            assertFailsWith<SocialFailure>{SocialAccess(r,r).visible("viewer",target.profile.publicPlayerId)}
        }
    }
    @Test fun `privacy hides name but preserves exact code and revision`() {
        val r=fixture();val p=candidate("u1",1);r.patchPrivacy("u1",PrivacyPatch(false,1))
        assertFalse(search(r).search("viewer","NAME","alice",null,null,50).items.any{it.publicPlayerId==p.publicPlayerId})
        assertEquals(1,search(r).search("viewer","FRIEND_CODE",null,p.friendCode,null,20).items.size)
        assertFailsWith<SocialFailure>{r.patchPrivacy("u1",PrivacyPatch(true,1))}
    }
    @Test fun `self and test inactive accounts excluded`() {
        val r=fixture();r.hidden.add("u2")
        val page=search(r).search("u1","NAME","alice",null,null,50)
        assertFalse(page.items.any{it.publicPlayerId in listOf(candidate("u1",1).publicPlayerId,candidate("u2",2).publicPlayerId)})
    }
    @Test fun `block idempotency list and self guard`() {
        val r=fixture();val b=BlockService(r,SocialAccess(r,r),SocialCursor());val id=candidate("u2",2).publicPlayerId
        repeat(2){b.set("u1",id,true)};assertEquals(1,b.list("u1",null,20).items.size)
        repeat(2){b.set("u1",id,false)};assertTrue(b.list("u1",null,20).items.isEmpty())
        assertEquals("SELF_RELATION_NOT_ALLOWED",assertFailsWith<SocialFailure>{b.set("u2",id,true)}.code)
    }
    @Test fun `redis unavailable cannot prevent safety block`() {
        val rate=RedisSocialRateLimiter{null};rate.check("a","block")
        assertEquals("SOCIAL_SERVICE_UNAVAILABLE",assertFailsWith<SocialFailure>{rate.check("a","NAME")}.code)
    }
    @Test fun `unblock hidden target and paginated own list`() {
        val r=fixture();val b=BlockService(r,SocialAccess(r,r),SocialCursor())
        b.set("u1",candidate("u2",2).publicPlayerId,true);b.set("u1",candidate("u3",3).publicPlayerId,true)
        val first=b.list("u1",null,1);val second=b.list("u1",first.nextCursor,1)
        assertEquals(1,first.items.size);assertEquals(1,second.items.size);assertNotEquals(first.items,second.items)
        r.hidden.add("u2");repeat(2){b.set("u1",candidate("u2",2).publicPlayerId,false)};assertFalse(b.hasBlockEitherDirection("u1","u2"))
    }
    @Test fun `public serialization never contains private identity`() {
        val r=fixture();val json=com.teamfho.domino.catalog.GameCatalogCodec.mapper.writeValueAsString(r.profiles.values.first().profile)
        for(field in listOf("uid","internalUid","testSource","isTestAccount","email","presence","matchId","grants"))assertFalse(json.contains("\"$field\""))
    }
}
