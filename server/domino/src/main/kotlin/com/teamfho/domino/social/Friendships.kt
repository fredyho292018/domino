package com.teamfho.domino.social

import com.teamfho.domino.entitlement.*
import com.teamfho.domino.match.MatchCodec
import java.security.MessageDigest
import java.time.Clock
import java.time.Instant
import java.util.UUID

enum class FriendRequestStatus { PENDING, ACCEPTED, DECLINED, CANCELED }
data class FriendRequest(val requestId:String, val pairId:String, val generation:Long, val senderUid:String,
    val recipientUid:String, val status:FriendRequestStatus, val createdAt:Instant, val resolvedAt:Instant?=null)
data class Friendship(val pairId:String,val lowerUid:String,val upperUid:String,val createdAt:Instant,val sourceRequestId:String,val generation:Long)
data class SocialCounters(val friendCount:Int=0,val pendingIncomingCount:Int=0,val pendingOutgoingCount:Int=0,val followerCount:Int=0,val followingCount:Int=0)
data class SocialPair(val lowerUid:String,val upperUid:String,val generation:Long=0,val pendingRequestId:String?=null,
    val declinedUntil:Instant?=null,val authorizationRevision:Long=0)
data class FriendRelationship(val friendship:String="NONE",val incomingRequestId:String?=null,val outgoingRequestId:String?=null,val following:Boolean=false,val followedBy:Boolean=false)
data class FriendItem(val profile:PublicPlayerProfile,val friendsSince:Instant)
data class FriendRequestItem(val requestId:String,val profile:PublicPlayerProfile,val direction:String,val createdAt:Instant)
data class FriendCapacity(val friendCount:Int,val effectiveFriendLimit:Int?,val canAddFriend:Boolean,val pendingIncomingCount:Int,val availability:String)

interface SocialTransaction {
    fun read(path:String):Map<String,Any>?
    fun put(path:String,data:Map<String,Any>)
    fun delete(path:String)
}
interface FriendshipRepository {
    fun <T> atomic(body:(SocialTransaction)->T):T
    fun read(path:String):Map<String,Any>?
    fun readAll(paths:List<String>):Map<String,Map<String,Any>?> = paths.associateWith{read(it)}
    fun page(collection:String,filters:Map<String,Any>,timeField:String,after:Pair<String,String>?,limit:Int):List<Pair<String,Map<String,Any>>>
}
object SocialPairIdentity {
    fun id(a:String,b:String):String {
        val pair=listOf(a,b).sorted()
        return MessageDigest.getInstance("SHA-256").digest(pair.joinToString(""){"${it.toByteArray(Charsets.UTF_8).size}:$it"}.toByteArray(Charsets.UTF_8)).joinToString(""){"%02x".format(it)}
    }
}

/** All mutable safety inputs, grant projections and policy are transaction reads, never cached booleans. */
class FriendshipService(private val repository:FriendshipRepository,private val cursor:SocialCursor,
    private val fallback:SubscriptionPolicy=SubscriptionPolicy(),private val clock:Clock=Clock.systemUTC()) {
    private inline fun <reified T> decode(data:Map<String,Any>?)=data?.let{MatchCodec.read(if(T::class==FriendRequest::class)it-"sortTime" else it,T::class.java)}
    private fun counter(tx:SocialTransaction,uid:String)=decode<SocialCounters>(tx.read("players/$uid/socialCounters/current"))?:SocialCounters()
    private fun writeCounter(tx:SocialTransaction,uid:String,c:SocialCounters) {
        check(c.friendCount>=0 && c.pendingIncomingCount>=0 && c.pendingOutgoingCount>=0 && c.followerCount>=0 && c.followingCount>=0)
        tx.put("players/$uid/socialCounters/current",MatchCodec.map(c))
    }
    private fun eligible(tx:SocialTransaction,uid:String) {
        val p=tx.read("players/$uid");val test=tx.read("developmentTestAccounts/$uid")
        socialCheck(p?.get("status")=="ACTIVE" && test?.get("isTestAccount")!=true,"PLAYER_NOT_FOUND",404)
    }
    private fun target(tx:SocialTransaction,id:String):String {
        SocialNames.publicId(id)
        return tx.read("publicPlayerProfiles/$id")?.get("internalUid") as? String?:throw SocialFailure("PLAYER_NOT_FOUND",404)
    }
    private fun limit(tx:SocialTransaction,uid:String,policy:SubscriptionPolicy,now:Instant):Int {
        val state=decode<EntitlementState>(tx.read("players/$uid/entitlementState/current"))?:EntitlementState()
        return EntitlementResolver.resolve(state,policy,now).limits.getValue(EntitlementLimit.FRIENDS_MAX).maximum!!
    }
    private fun policy(tx:SocialTransaction)=decode<SubscriptionPolicy>(tx.read("systemConfig/subscriptionPolicy"))?:fallback
    private fun safety(tx:SocialTransaction,a:String,b:String,recipient:String,enforcePrivacy:Boolean=true) {
        eligible(tx,a);eligible(tx,b)
        val ab=tx.read("players/$a/blocks/$b");val ba=tx.read("players/$b/blocks/$a")
        val privacy=tx.read("players/$recipient/socialSettings/current")
        socialCheck(ab==null && ba==null,"PLAYER_NOT_FOUND",404)
        socialCheck(!enforcePrivacy || privacy?.get("friendRequests")!="NO_ONE","SOCIAL_ACTION_NOT_ALLOWED",403)
    }
    private fun capacity(actor:String,uid:String,c:SocialCounters,max:Int) {
        socialCheck(c.friendCount<max,if(actor==uid)"FRIEND_LIMIT_REACHED" else "SOCIAL_ACTION_NOT_ALLOWED",409)
    }
    private fun relation(actor:String,r:FriendRequest?,friend:Boolean)=FriendRelationship(if(friend)"FRIENDS" else "NONE",
        r?.takeIf{it.status==FriendRequestStatus.PENDING && it.recipientUid==actor}?.requestId,
        r?.takeIf{it.status==FriendRequestStatus.PENDING && it.senderUid==actor}?.requestId)

    fun send(actor:String,publicId:String):FriendRelationship=repository.atomic { tx->
        val other=target(tx,publicId);socialCheck(actor!=other,"SELF_RELATION_NOT_ALLOWED")
        safety(tx,actor,other,other)
        val id=SocialPairIdentity.id(actor,other);val pair=decode<SocialPair>(tx.read("socialPairs/$id"))?:SocialPair(minOf(actor,other),maxOf(actor,other))
        val friend=tx.read("friendships/$id")
        val pending=pair.pendingRequestId?.let{decode<FriendRequest>(tx.read("friendRequests/$it"))}
        socialCheck(friend==null,"ALREADY_FRIENDS",409)
        if(pending?.status==FriendRequestStatus.PENDING) return@atomic relation(actor,pending,false)
        val now=clock.instant();socialCheck(pair.declinedUntil==null || now>=pair.declinedUntil,"SOCIAL_ACTION_NOT_ALLOWED",403)
        val a=counter(tx,actor);val b=counter(tx,other);val policy=policy(tx);val max=limit(tx,actor,policy,now)
        capacity(actor,actor,a,max);socialCheck(a.pendingOutgoingCount<20,"SOCIAL_ACTION_RATE_LIMITED",429)
        // Durable rolling minute/day windows are independent of Premium and survive Redis restart.
        val rate=tx.read("players/$actor/socialRate/friendRequests")?:emptyMap()
        val recent=(rate["sentAt"] as? List<*>)?.map{Instant.parse(it as String)}?.filter{it>now.minusSeconds(86400)}?:emptyList()
        socialCheck(recent.size<20 && recent.count{it>now.minusSeconds(60)}<5,"SOCIAL_ACTION_RATE_LIMITED",429)
        val req=FriendRequest(UUID.randomUUID().toString(),id,pair.generation+1,actor,other,FriendRequestStatus.PENDING,now)
        tx.put("friendRequests/${req.requestId}",MatchCodec.map(req)+("sortTime" to now.toEpochMilli()))
        tx.put("socialPairs/$id",MatchCodec.map(pair.copy(generation=req.generation,pendingRequestId=req.requestId)))
        writeCounter(tx,actor,a.copy(pendingOutgoingCount=a.pendingOutgoingCount+1));writeCounter(tx,other,b.copy(pendingIncomingCount=b.pendingIncomingCount+1))
        tx.put("players/$actor/socialRate/friendRequests",mapOf("sentAt" to (recent+now).map{it.toString()}))
        relation(actor,req,false)
    }
    fun resolve(actor:String,requestId:String,action:FriendRequestStatus):FriendRelationship=repository.atomic { tx->
        socialCheck(Regex("[a-f0-9-]{36}").matches(requestId),"FRIEND_REQUEST_NOT_FOUND",404)
        val req=decode<FriendRequest>(tx.read("friendRequests/$requestId"))?:throw SocialFailure("FRIEND_REQUEST_NOT_FOUND",404)
        val cancel=action==FriendRequestStatus.CANCELED
        socialCheck(actor==(if(cancel)req.senderUid else req.recipientUid),if(cancel)"FRIEND_REQUEST_NOT_SENDER" else "FRIEND_REQUEST_NOT_RECIPIENT",403)
        socialCheck(action in setOf(FriendRequestStatus.ACCEPTED,FriendRequestStatus.DECLINED,FriendRequestStatus.CANCELED),"SOCIAL_ACTION_NOT_ALLOWED")
        val pair=decode<SocialPair>(tx.read("socialPairs/${req.pairId}"))?:throw SocialFailure("FRIEND_REQUEST_NOT_PENDING",409)
        val friendship=tx.read("friendships/${req.pairId}")
        socialCheck(pair.generation==req.generation,"FRIEND_REQUEST_NOT_PENDING",409)
        if(req.status==action) return@atomic relation(actor,null,friendship!=null)
        socialCheck(req.status==FriendRequestStatus.PENDING && pair.pendingRequestId==requestId,"FRIEND_REQUEST_NOT_PENDING",409)
        val a=counter(tx,req.senderUid);val b=counter(tx,req.recipientUid)
        val now=clock.instant()
        if(action==FriendRequestStatus.ACCEPTED) {
            safety(tx,req.senderUid,req.recipientUid,req.recipientUid)
            val p=policy(tx);val al=limit(tx,req.senderUid,p,now);val bl=limit(tx,req.recipientUid,p,now)
            capacity(actor,req.senderUid,a,al);capacity(actor,req.recipientUid,b,bl)
            val ai=tx.read("players/${req.senderUid}/publicIdentity/current")?:throw SocialFailure("SOCIAL_SERVICE_UNAVAILABLE")
            val bi=tx.read("players/${req.recipientUid}/publicIdentity/current")?:throw SocialFailure("SOCIAL_SERVICE_UNAVAILABLE")
            val f=Friendship(req.pairId,pair.lowerUid,pair.upperUid,now,requestId,req.generation)
            tx.put("friendships/${req.pairId}",MatchCodec.map(f))
            tx.put("players/${req.senderUid}/friends/${req.recipientUid}",mapOf("pairId" to req.pairId,"friendPublicPlayerId" to bi.getValue("publicPlayerId"),"friendsSince" to now.toString(),"sortTime" to now.toEpochMilli()))
            tx.put("players/${req.recipientUid}/friends/${req.senderUid}",mapOf("pairId" to req.pairId,"friendPublicPlayerId" to ai.getValue("publicPlayerId"),"friendsSince" to now.toString(),"sortTime" to now.toEpochMilli()))
        }
        val added=if(action==FriendRequestStatus.ACCEPTED)1 else 0
        writeCounter(tx,req.senderUid,a.copy(friendCount=a.friendCount+added,pendingOutgoingCount=a.pendingOutgoingCount-1))
        writeCounter(tx,req.recipientUid,b.copy(friendCount=b.friendCount+added,pendingIncomingCount=b.pendingIncomingCount-1))
        tx.put("friendRequests/$requestId",MatchCodec.map(req.copy(status=action,resolvedAt=now))+("sortTime" to req.createdAt.toEpochMilli()))
        val revision=if(added==1)Math.addExact(pair.authorizationRevision,1)else pair.authorizationRevision
        tx.put("socialPairs/${req.pairId}",MatchCodec.map(pair.copy(pendingRequestId=null,declinedUntil=if(action==FriendRequestStatus.DECLINED)now.plusSeconds(604800)else pair.declinedUntil,authorizationRevision=revision)))
        if(added==1)tx.invalidate(SocialInvalidation.pair(req.pairId,revision))
        relation(actor,null,added==1)
    }

    /** Called by S1.1 block inside its transaction before any writes, to serialize with acceptance. */
    fun removeInTransaction(tx:SocialTransaction,actor:String,other:String,blocking:Boolean,blockChanged:Boolean=false) {
        val id=SocialPairIdentity.id(actor,other);val pair=decode<SocialPair>(tx.read("socialPairs/$id"))
        val f=tx.read("friendships/$id")
        val req=pair?.pendingRequestId?.let{decode<FriendRequest>(tx.read("friendRequests/$it"))}
        val a=counter(tx,actor);val b=counter(tx,other)
        val pending=blocking && req?.status==FriendRequestStatus.PENDING
        val ab=if(blocking)tx.read("players/$actor/following/$other") else null
        val ba=if(blocking)tx.read("players/$other/following/$actor") else null
        if(ab!=null){tx.delete("players/$actor/following/$other");tx.delete("players/$other/followers/$actor")}
        if(ba!=null){tx.delete("players/$other/following/$actor");tx.delete("players/$actor/followers/$other")}
        if(f!=null) {tx.delete("friendships/$id");tx.delete("players/$actor/friends/$other");tx.delete("players/$other/friends/$actor")}
        if(pending) {
            tx.put("friendRequests/${req!!.requestId}",MatchCodec.map(req.copy(status=FriendRequestStatus.CANCELED,resolvedAt=clock.instant()))+("sortTime" to req.createdAt.toEpochMilli()))
        }
        val changed=blockChanged || f!=null
        if(pending || changed) {
            val previous=pair?:SocialPair(minOf(actor,other),maxOf(actor,other))
            val revision=if(changed)Math.addExact(previous.authorizationRevision,1)else previous.authorizationRevision
            tx.put("socialPairs/$id",MatchCodec.map(previous.copy(pendingRequestId=if(pending)null else previous.pendingRequestId,authorizationRevision=revision)))
            if(changed)tx.invalidate(SocialInvalidation.pair(id,revision))
        }
        if(f!=null || pending || ab!=null || ba!=null) {
            fun next(uid:String,c:SocialCounters)=c.copy(friendCount=c.friendCount-if(f!=null)1 else 0,
                pendingOutgoingCount=c.pendingOutgoingCount-if(pending && req!!.senderUid==uid)1 else 0,
                pendingIncomingCount=c.pendingIncomingCount-if(pending && req!!.recipientUid==uid)1 else 0,
                followingCount=c.followingCount-if((uid==actor && ab!=null)||(uid==other && ba!=null))1 else 0,
                followerCount=c.followerCount-if((uid==actor && ba!=null)||(uid==other && ab!=null))1 else 0)
            writeCounter(tx,actor,next(actor,a));writeCounter(tx,other,next(other,b))
        }
    }
    fun remove(actor:String,publicId:String) { repository.atomic { tx->val other=target(tx,publicId);socialCheck(actor!=other,"SELF_RELATION_NOT_ALLOWED");removeInTransaction(tx,actor,other,false) } }
    fun relationship(actor:String,publicId:String):FriendRelationship=repository.atomic { tx->
        val other=target(tx,publicId);safety(tx,actor,other,other,false)
        val id=SocialPairIdentity.id(actor,other);val p=decode<SocialPair>(tx.read("socialPairs/$id"))
        val f=tx.read("friendships/$id");val req=p?.pendingRequestId?.let{decode<FriendRequest>(tx.read("friendRequests/$it"))}
        relation(actor,req,f!=null).copy(following=tx.read("players/$actor/following/$other")!=null,followedBy=tx.read("players/$other/following/$actor")!=null)
    }
    fun summary(actor:String):FriendCapacity=repository.atomic { tx->
        val c=counter(tx,actor)
        val max=limit(tx,actor,policy(tx),clock.instant())
        FriendCapacity(c.friendCount,max,c.friendCount<max,c.pendingIncomingCount,"AVAILABLE")
    }
    fun friends(actor:String,value:String?,limit:Int):SocialPage<FriendItem> {
        socialCheck(limit in 1..50,"INVALID_SEARCH_QUERY");val after=cursor.decode(value,actor,"friends")
        val rows=repository.page("players/$actor/friends",emptyMap(),"sortTime",after,limit+1)
        val result=rows.take(limit).mapNotNull {(_,d)-> profile(actor,d["friendPublicPlayerId"] as String)?.let{FriendItem(it,Instant.parse(d["friendsSince"] as String))} }
        return SocialPage(result,next(actor,"friends",rows,limit,"sortTime"))
    }
    fun requests(actor:String,direction:String,value:String?,limit:Int):SocialPage<FriendRequestItem> {
        socialCheck(direction in setOf("INCOMING","OUTGOING") && limit in 1..50,"INVALID_SEARCH_QUERY")
        val key="requests:$direction";val rows=repository.page("friendRequests",mapOf((if(direction=="INCOMING")"recipientUid" else "senderUid") to actor,"status" to "PENDING"),"sortTime",cursor.decode(value,actor,key),limit+1)
        val result=rows.take(limit).mapNotNull {(_,d)->val r=decode<FriendRequest>(d)!!;val other=if(direction=="INCOMING")r.senderUid else r.recipientUid
            val id=repository.read("players/$other/publicIdentity/current")?.get("publicPlayerId") as? String
            id?.let{profile(actor,it)}?.let{FriendRequestItem(r.requestId,it,direction,r.createdAt)} }
        return SocialPage(result,next(actor,key,rows,limit,"sortTime"))
    }
    private fun profile(actor:String,id:String):PublicPlayerProfile?=repository.read("publicPlayerProfiles/$id")?.let{
        val uid=it["internalUid"] as String
        if(repository.read("players/$uid")?.get("status")!="ACTIVE" || repository.read("developmentTestAccounts/$uid")?.get("isTestAccount")==true ||
            repository.read("players/$actor/blocks/$uid")!=null || repository.read("players/$uid/blocks/$actor")!=null) null
        else PublicPlayerProfile(id,it["friendCode"] as String,it["displayName"] as String)
    }
    private fun next(actor:String,key:String,rows:List<Pair<String,Map<String,Any>>>,limit:Int,field:String)=
        if(rows.size>limit)cursor.encode(actor,key,rows[limit-1].second[field].toString(),rows[limit-1].first)else null
}
