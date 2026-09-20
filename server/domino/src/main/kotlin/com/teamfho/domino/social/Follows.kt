package com.teamfho.domino.social

import com.teamfho.domino.match.MatchCodec
import java.time.Clock
import java.time.Instant

data class FollowRelationship(val profile:PublicPlayerProfile,val followedSince:Instant)
data class FollowCounts(val followerCount:Int,val followingCount:Int)

/** Two directional projections and shared counters commit together; no entitlement dependency. */
class FollowService(private val repository:FriendshipRepository,private val cursor:SocialCursor,private val clock:Clock=Clock.systemUTC()) {
    private fun counter(tx:SocialTransaction,uid:String)=tx.read("players/$uid/socialCounters/current")?.let{MatchCodec.read(it,SocialCounters::class.java)}?:SocialCounters()
    fun set(actor:String,id:String,enabled:Boolean) = repository.atomic { tx ->
        SocialNames.publicId(id)
        val other=tx.read("publicPlayerProfiles/$id")?.get("internalUid") as? String?:throw SocialFailure("PLAYER_NOT_FOUND",404)
        socialCheck(actor!=other,"SELF_RELATION_NOT_ALLOWED")
        val forward="players/$actor/following/$other";val inverse="players/$other/followers/$actor"
        val existing=tx.read(forward)
        if(!enabled && existing==null)return@atomic Unit
        val a=counter(tx,actor);val b=counter(tx,other)
        if(enabled) {
            for(uid in listOf(actor,other)) {
                val p=tx.read("players/$uid");val marker=tx.read("developmentTestAccounts/$uid")
                socialCheck(p?.get("status")=="ACTIVE" && marker?.get("isTestAccount")!=true,"PLAYER_NOT_FOUND",404)
            }
            val ab=tx.read("players/$actor/blocks/$other");val ba=tx.read("players/$other/blocks/$actor")
            socialCheck(ab==null && ba==null,"PLAYER_NOT_FOUND",404)
            // Existing follows survive later NO_ONE privacy; retries never create another edge.
            if(existing!=null)return@atomic Unit
            socialCheck(tx.read("players/$other/socialSettings/current")?.get("follow")!="NO_ONE","SOCIAL_ACTION_NOT_ALLOWED",403)
            val ownId=tx.read("players/$actor/publicIdentity/current")?.get("publicPlayerId") as? String?:throw SocialFailure("SOCIAL_SERVICE_UNAVAILABLE")
            val now=clock.instant();val fields=mapOf("followedSince" to now.toString(),"sortTime" to now.toEpochMilli())
            tx.put(forward,fields+("publicPlayerId" to id));tx.put(inverse,fields+("publicPlayerId" to ownId))
        } else { tx.delete(forward);tx.delete(inverse) }
        val delta=if(enabled)1 else -1
        val nextA=a.copy(followingCount=a.followingCount+delta);val nextB=b.copy(followerCount=b.followerCount+delta)
        check(nextA.followingCount>=0 && nextB.followerCount>=0)
        tx.put("players/$actor/socialCounters/current",MatchCodec.map(nextA));tx.put("players/$other/socialCounters/current",MatchCodec.map(nextB))
    }
    fun counts(actor:String):FollowCounts=repository.atomic {tx->val c=counter(tx,actor);FollowCounts(c.followerCount,c.followingCount)}
    fun isFollowing(actor:String,targetUid:String)=repository.read("players/$actor/following/$targetUid")!=null
    fun list(actor:String,following:Boolean,value:String?,limit:Int):SocialPage<FollowRelationship> {
        socialCheck(limit in 1..50,"INVALID_SEARCH_QUERY")
        val key=if(following)"following" else "followers"
        val rows=repository.page("players/$actor/$key",emptyMap(),"sortTime",cursor.decode(value,actor,key),limit+1)
        val profiles=repository.readAll(rows.take(limit).map{"publicPlayerProfiles/${it.second["publicPlayerId"]}"})
        val safetyPaths=profiles.values.filterNotNull().flatMap {p->val uid=p["internalUid"] as String;listOf("players/$uid","developmentTestAccounts/$uid","players/$actor/blocks/$uid","players/$uid/blocks/$actor")}
        val safety=repository.readAll(safetyPaths)
        val result=rows.take(limit).mapNotNull {(_,edge)->
            val id=edge["publicPlayerId"] as String;val p=profiles["publicPlayerProfiles/$id"]?:return@mapNotNull null
            val uid=p["internalUid"] as String
            if(safety["players/$uid"]?.get("status")!="ACTIVE" || safety["developmentTestAccounts/$uid"]?.get("isTestAccount")==true || safety["players/$actor/blocks/$uid"]!=null || safety["players/$uid/blocks/$actor"]!=null) null
            else FollowRelationship(PublicPlayerProfile(id,p["friendCode"] as String,p["displayName"] as String),Instant.parse(edge["followedSince"] as String))
        }
        val next=if(rows.size>limit)cursor.encode(actor,key,rows[limit-1].second["sortTime"].toString(),rows[limit-1].first)else null
        return SocialPage(result,next)
    }
}
