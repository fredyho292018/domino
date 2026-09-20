package com.teamfho.domino.social

import com.teamfho.domino.entitlement.*
import com.teamfho.domino.match.MatchCodec
import org.junit.jupiter.api.Test
import java.time.*
import java.util.concurrent.*
import kotlin.test.*

class MemoryFriendships:FriendshipRepository {
    val docs=mutableMapOf<String,Map<String,Any>>()
    @Synchronized override fun <T> atomic(body:(SocialTransaction)->T):T {
        val copy=docs.toMutableMap();var writing=false
        val value=body(object:SocialTransaction {
            override fun read(path:String):Map<String,Any>? {check(!writing){"read after write"};return copy[path]}
            override fun put(path:String,data:Map<String,Any>){writing=true;copy[path]=data}
            override fun delete(path:String){writing=true;copy.remove(path)}
        });docs.clear();docs.putAll(copy);return value
    }
    @Synchronized override fun read(path:String)=docs[path]
    @Synchronized override fun page(collection:String,filters:Map<String,Any>,timeField:String,after:Pair<String,String>?,limit:Int):List<Pair<String,Map<String,Any>>> = docs.entries.filter{it.key.startsWith("$collection/") && '/' !in it.key.removePrefix("$collection/") && filters.all{(k,v)->it.value[k]==v}}
        .map{it.key.substringAfterLast('/') to it.value}.sortedWith(compareByDescending<Pair<String,Map<String,Any>>>{(it.second[timeField] as Number).toLong()}.thenByDescending{it.first})
        .filter{after==null || (it.second[timeField] as Number).toLong()<after.first.toLong() || (it.second[timeField] as Number).toLong()==after.first.toLong() && it.first<after.second}.take(limit)
}
class FriendFixture {
    val db=MemoryFriendships();var now=Instant.parse("2026-09-20T12:00:00Z")
    val clock=object:Clock(){override fun getZone()=ZoneOffset.UTC;override fun withZone(z:ZoneId)=this;override fun instant()=now}
    val service=FriendshipService(db,SocialCursor(clock=clock),clock=clock)
    fun player(uid:String):String {val id=uid.padEnd(22,'_');db.docs["players/$uid"]=mapOf("status" to "ACTIVE")
        db.docs["players/$uid/publicIdentity/current"]=mapOf("publicPlayerId" to id)
        db.docs["publicPlayerProfiles/$id"]=mapOf("internalUid" to uid,"displayName" to "Alice","friendCode" to "FHO-000000000000")
        return id}
    fun count(uid:String)=MatchCodec.read(db.docs["players/$uid/socialCounters/current"]?:emptyMap(),SocialCounters::class.java)
    fun seedCount(uid:String,n:Int){db.docs["players/$uid/socialCounters/current"]=MatchCodec.map(count(uid).copy(friendCount=n))}
    fun premium(uid:String,trial:Boolean=false){db.docs["players/$uid/entitlementState/current"]=MatchCodec.map(EntitlementState(grants=listOf(EntitlementGrant("g",if(trial)EntitlementSource.PROMOTIONAL_TRIAL else EntitlementSource.ADMIN_GRANT,validFrom=now.minusSeconds(1),validUntil=now.plusSeconds(60),createdAt=now,policyVersion=1,reason="TEST",grantedBy="test"))))}
    fun send(a:String,b:String)=service.send(a,b.padEnd(22,'_')).outgoingRequestId!!
    fun block(a:String,b:String){db.atomic{tx->service.removeInTransaction(tx,a,b,true);tx.put("players/$a/blocks/$b",mapOf("blocked" to true))}}
}
class FriendshipTests {
    private fun failure(code:String,body:()->Unit){assertEquals(code,assertFailsWith<SocialFailure>(block=body).code)}
    @Test fun `mutual lifecycle counters retries and old generation`() {
        val f=FriendFixture();val a=f.player("a");val b=f.player("b")
        val id=f.send("a","b");assertEquals(id,f.send("a","b"));assertEquals(id,f.service.send("b",a).incomingRequestId)
        assertEquals(0,f.count("a").friendCount)
        f.service.resolve("b",id,FriendRequestStatus.ACCEPTED);f.service.resolve("b",id,FriendRequestStatus.ACCEPTED)
        assertEquals(1,f.count("a").friendCount);assertEquals(1,f.count("b").friendCount)
        assertEquals(b,f.service.friends("a",null,20).items.single().profile.publicPlayerId)
        assertEquals(a,f.service.friends("b",null,20).items.single().profile.publicPlayerId)
        f.service.remove("a",b);f.service.remove("a",b);assertEquals(0,f.count("a").friendCount)
        val next=f.send("a","b");failure("FRIEND_REQUEST_NOT_PENDING"){f.service.resolve("b",id,FriendRequestStatus.ACCEPTED)}
        assertEquals(next,f.service.requests("b","INCOMING",null,20).items.single().requestId)
    }
    @Test fun `actor authorization terminal requests and decline cooldown`() {
        for(action in listOf(FriendRequestStatus.DECLINED,FriendRequestStatus.CANCELED)) {
            val f=FriendFixture();f.player("a");f.player("b");val id=f.send("a","b")
            val who=if(action==FriendRequestStatus.DECLINED)"b" else "a"
            assertFailsWith<SocialFailure>{f.service.resolve(if(who=="a")"b" else "a",id,action)}
            f.service.resolve(who,id,action);f.service.resolve(who,id,action)
            assertEquals(0,f.count("a").pendingOutgoingCount);assertEquals(0,f.count("b").pendingIncomingCount)
            failure("FRIEND_REQUEST_NOT_PENDING"){f.service.resolve("b",id,FriendRequestStatus.ACCEPTED)}
            if(action==FriendRequestStatus.DECLINED) {failure("SOCIAL_ACTION_NOT_ALLOWED"){f.send("a","b")};f.now=f.now.plusSeconds(604800);f.send("a","b")}
        }
    }
    @Test fun `privacy eligibility self and sender capacity`() {
        val f=FriendFixture();val a=f.player("a");f.player("b")
        failure("SELF_RELATION_NOT_ALLOWED"){f.service.send("a",a)}
        f.db.docs["players/b/socialSettings/current"]=mapOf("friendRequests" to "NO_ONE")
        failure("SOCIAL_ACTION_NOT_ALLOWED"){f.send("a","b")};f.db.docs.remove("players/b/socialSettings/current")
        f.db.docs["developmentTestAccounts/b"]=mapOf("isTestAccount" to true)
        failure("PLAYER_NOT_FOUND"){f.send("a","b")};f.db.docs.remove("developmentTestAccounts/b")
        f.seedCount("a",5);failure("FRIEND_LIMIT_REACHED"){f.send("a","b")}
    }
    @Test fun `both capacity limits free premium trial and expiration`() {
        for(plan in 0..2) {
            val f=FriendFixture();f.player("a");f.player("b");val max=if(plan==0)5 else 100
            if(plan>0){f.premium("a",plan==2);f.premium("b",plan==2)}
            f.seedCount("a",max-1);f.seedCount("b",max-1);val id=f.send("a","b")
            f.service.resolve("b",id,FriendRequestStatus.ACCEPTED);assertEquals(max,f.count("a").friendCount)
            f.player("c");failure("FRIEND_LIMIT_REACHED"){f.send("a","c")}
            if(plan>0){f.now=f.now.plusSeconds(61);assertEquals(max,f.service.summary("a").friendCount);assertEquals(5,f.service.summary("a").effectiveFriendLimit)}
        }
        val f=FriendFixture();f.player("a");f.player("b");val id=f.send("a","b");f.seedCount("a",5)
        failure("SOCIAL_ACTION_NOT_ALLOWED"){f.service.resolve("b",id,FriendRequestStatus.ACCEPTED)}
        assertEquals(1,f.service.requests("b","INCOMING",null,20).items.size)
    }
    private fun race(vararg actions:()->Unit):List<Boolean> {
        val pool=Executors.newFixedThreadPool(actions.size);val gate=CyclicBarrier(actions.size)
        return try{pool.invokeAll(actions.map{a->Callable{gate.await();try{a();true}catch(_:SocialFailure){false}}}).map{it.get()}}finally{pool.shutdownNow()}
    }
    @Test fun `last free and premium slot is atomic`() {
        for(premium in listOf(false,true)) {val f=FriendFixture();f.player("a");f.player("b");f.player("c");if(premium)f.premium("a")
            val max=if(premium)100 else 5;f.seedCount("a",max-1)
            val r1=f.send("b","a");val r2=f.send("c","a")
            val results=race({f.service.resolve("a",r1,FriendRequestStatus.ACCEPTED)},{f.service.resolve("a",r2,FriendRequestStatus.ACCEPTED)})
            assertEquals(1,results.count{it});assertEquals(max,f.count("a").friendCount)
        }
    }
    @Test fun `concurrent cross send and accept block always favor block`() {
        repeat(10){val f=FriendFixture();val a=f.player("a");val b=f.player("b")
            race({f.service.send("a",b)},{f.service.send("b",a)})
            val req=f.db.docs.filterKeys{it.startsWith("friendRequests/")}.values.single();val id=req["requestId"] as String;val recipient=req["recipientUid"] as String
            race({f.service.resolve(recipient,id,FriendRequestStatus.ACCEPTED)},{f.block("a","b")})
            assertEquals(0,f.count("a").friendCount);assertEquals(0,f.count("b").friendCount)
            assertEquals(0,f.count("a").pendingIncomingCount+f.count("a").pendingOutgoingCount)
            assertNull(f.db.docs["friendships/${SocialPairIdentity.id("a","b")}"])
        }
    }
    @Test fun `pagination cursor isolation and current display names`() {
        val f=FriendFixture();f.player("a")
        repeat(4){val b="b$it";f.player(b);f.send("a",b)}
        val page=f.service.requests("a","OUTGOING",null,2);assertEquals(2,page.items.size);assertNotNull(page.nextCursor)
        assertEquals(2,f.service.requests("a","OUTGOING",page.nextCursor,2).items.size)
        failure("INVALID_SEARCH_QUERY"){f.service.requests("b0","OUTGOING",page.nextCursor,2)}
        failure("INVALID_SEARCH_QUERY"){f.service.requests("a","INCOMING",page.nextCursor,2)}
    }
    @Test fun `pending cap daily cap and minute cap`() {
        val f=FriendFixture();f.player("a")
        repeat(20){val b="b$it";f.player(b);if(it%5==0)f.now=f.now.plusSeconds(61);f.send("a",b)}
        f.player("extra");failure("SOCIAL_ACTION_RATE_LIMITED"){f.send("a","extra")}
        val g=FriendFixture();g.player("a");repeat(5){g.player("b$it");g.send("a","b$it")};g.player("extra")
        failure("SOCIAL_ACTION_RATE_LIMITED"){g.send("a","extra")}
    }
    @Test fun `unfriend block and unavailable entitlement removal paths`() {
        val f=FriendFixture();f.player("a");val b=f.player("b");val id=f.send("a","b");f.service.resolve("b",id,FriendRequestStatus.ACCEPTED)
        f.db.docs["systemConfig/subscriptionPolicy"]=mapOf("freeFriendsMax" to -1)
        race({f.service.remove("a",b)},{f.block("a","b")})
        assertEquals(0,f.count("a").friendCount);assertEquals(0,f.count("b").friendCount)
    }
    @Test fun `accept versus terminal action has one durable outcome`() {
        for(action in listOf(FriendRequestStatus.ACCEPTED,FriendRequestStatus.CANCELED,FriendRequestStatus.DECLINED))repeat(5) {
            val f=FriendFixture();f.player("a");f.player("b");val id=f.send("a","b")
            race({f.service.resolve("b",id,FriendRequestStatus.ACCEPTED)},{f.service.resolve(if(action==FriendRequestStatus.CANCELED)"a" else "b",id,action)})
            val accepted=f.db.docs["friendRequests/$id"]!!["status"]=="ACCEPTED"
            assertEquals(if(accepted)1 else 0,f.count("a").friendCount);assertEquals(f.count("a").friendCount,f.count("b").friendCount)
            assertEquals(0,f.count("a").pendingOutgoingCount);assertEquals(0,f.count("b").pendingIncomingCount)
        }
    }
    @Test fun `send race block and unblock never restore requests`() {
        repeat(5){val f=FriendFixture();f.player("a");val b=f.player("b")
            race({f.send("a","b")},{f.block("a","b")});assertEquals(0,f.count("a").pendingOutgoingCount)
            f.db.docs.remove("players/a/blocks/b");assertEquals(FriendRelationship(),f.service.relationship("a",b))
        }
    }
    @Test fun `policy and revocation revalidated inside acceptance transaction`() {
        val f=FriendFixture();f.player("a");f.player("b");f.premium("a");f.seedCount("a",20);val id=f.send("a","b")
        f.db.docs["players/a/entitlementState/current"]=MatchCodec.map(EntitlementState())
        failure("SOCIAL_ACTION_NOT_ALLOWED"){f.service.resolve("b",id,FriendRequestStatus.ACCEPTED)}
        assertEquals(20,f.count("a").friendCount);f.seedCount("a",4)
        f.db.docs["systemConfig/subscriptionPolicy"]=MatchCodec.map(SubscriptionPolicy(freeFriendsMax=4))
        failure("SOCIAL_ACTION_NOT_ALLOWED"){f.service.resolve("b",id,FriendRequestStatus.ACCEPTED)}
        f.db.docs["systemConfig/subscriptionPolicy"]=MatchCodec.map(SubscriptionPolicy())
        f.service.resolve("b",id,FriendRequestStatus.ACCEPTED);assertEquals(5,f.count("a").friendCount)
    }
    @Test fun `private DTOs and list filtering no billing UID or blocked state`() {
        val f=FriendFixture();f.player("a");val b=f.player("b");f.send("a","b")
        val json=com.teamfho.domino.catalog.GameCatalogCodec.json(f.service.requests("b","INCOMING",null,20))
        for(field in listOf("senderUid","recipientUid","internalUid","plan","friendCount","blockedBy"))assertFalse(json.contains(field))
        f.db.docs["developmentTestAccounts/a"]=mapOf("isTestAccount" to true)
        assertTrue(f.service.requests("b","INCOMING",null,20).items.isEmpty())
        assertEquals(SocialPairIdentity.id("a","bc"),SocialPairIdentity.id("bc","a"));assertNotEquals(SocialPairIdentity.id("a","bc"),SocialPairIdentity.id("ab","c"))
    }
}
