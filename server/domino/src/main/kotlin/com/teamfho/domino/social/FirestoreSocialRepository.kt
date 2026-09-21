package com.teamfho.domino.social

import com.google.cloud.firestore.*
import java.util.concurrent.TimeUnit

/** No background work, no client Firestore access, and no relation writes outside transactions. */
class FirestoreSocialRepository(private val db: Firestore,private val invalidation:SocialInvalidationSink=SocialInvalidationSink {}) : PublicIdentityRepository, SocialPrivacyRepository, BlockRepository {
    private fun doc(path: String) = db.document(path)
    private fun <T> transaction(afterCommit:(T)->Unit={},body: (Transaction) -> T): T {
        val future=db.runTransaction {tx->socialTransactionCallback {body(tx)}}
        return com.google.api.core.ApiFutures.transform(future,{result->afterCommit(result);result},
            java.util.concurrent.Executor{it.run()}).get(15,TimeUnit.SECONDS)
    }
    private fun get(path: String) = doc(path).get().get(5, TimeUnit.SECONDS)
    private fun active(p: DocumentSnapshot, marker: DocumentSnapshot) = p.exists() && p.getString("status") == "ACTIVE" && marker.getBoolean("isTestAccount") != true
    private fun identity(uid: String, d: DocumentSnapshot) = PublicPlayerIdentity(uid, d.getString("publicPlayerId")!!, d.getString("friendCode")!!)
    private fun settings(d: DocumentSnapshot) = SocialPrivacySettings(d.getBoolean("discoverableByName") ?: false,
        friendRequests=ContactPermission.valueOf(d.getString("friendRequests")?:"EVERYONE"),follow=ContactPermission.valueOf(d.getString("follow")?:"EVERYONE"),presenceVisibility=SocialVisibility.valueOf(d.getString("presenceVisibility")?:"FRIENDS"),matchActivityVisibility=SocialVisibility.valueOf(d.getString("matchActivityVisibility")?:"FRIENDS"),revision=d.getLong("revision") ?: 1)
    override fun ensure(uid: String, candidate: PublicPlayerIdentity): PublicPlayerIdentity? = transaction { tx ->
        val owner = tx.get(doc("players/$uid/publicIdentity/current")).get()
        val player = tx.get(doc("players/$uid")).get()
        val marker = tx.get(doc("developmentTestAccounts/$uid")).get()
        socialCheck(active(player, marker), "SOCIAL_ACTION_NOT_ALLOWED", 403)
        if (owner.exists()) identity(uid, owner) else {
            val profileRef = doc("publicPlayerProfiles/${candidate.publicPlayerId}")
            val codeRef = doc("publicPlayerCodes/${candidate.friendCode}")
            val profile = tx.get(profileRef).get(); val code = tx.get(codeRef).get()
            val privacyRef = doc("players/$uid/socialSettings/current")
            val privacy = tx.get(privacyRef).get()
            if (profile.exists() || code.exists()) null else {
                val discoverable = if (privacy.exists()) settings(privacy).discoverableByName else player.getBoolean("socialDefaultDiscoverable") == true
                val time = FieldValue.serverTimestamp()
                tx.create(owner.reference, mapOf("publicPlayerId" to candidate.publicPlayerId, "friendCode" to candidate.friendCode, "createdAt" to time))
                tx.create(codeRef, mapOf("publicPlayerId" to candidate.publicPlayerId))
                tx.create(profileRef, mapOf("internalUid" to uid, "publicPlayerId" to candidate.publicPlayerId,
                    "friendCode" to candidate.friendCode, "displayName" to player.getString("displayName")!!,
                    "normalizedDisplayName" to SocialNames.normalize(player.getString("displayName")!!),
                    "searchEligible" to discoverable, "createdAt" to time, "updatedAt" to time))
                if (!privacy.exists()) tx.create(privacyRef, mapOf("discoverableByName" to discoverable,
                    "friendRequests" to "EVERYONE", "follow" to "EVERYONE", "presenceVisibility" to "FRIENDS",
                    "matchActivityVisibility" to "FRIENDS", "revision" to 1L))
                candidate
            }
        }
    }
    override fun resolve(publicId: String): SocialCandidate? {
        val p = get("publicPlayerProfiles/$publicId"); if (!p.exists()) return null
        val uid = p.getString("internalUid") ?: return null
        val docs = db.getAll(doc("players/$uid"), doc("developmentTestAccounts/$uid")).get(5, TimeUnit.SECONDS)
        if (!active(docs[0], docs[1])) return null
        return SocialCandidate(uid, PublicPlayerProfile(publicId, p.getString("friendCode")!!, p.getString("displayName")!!), p.getString("normalizedDisplayName")!!)
    }
    override fun code(code: String) = get("publicPlayerCodes/$code").getString("publicPlayerId")
    override fun search(prefix: String, afterName: String?, afterId: String?, budget: Int): CandidatePage {
        var query: Query = db.collection("publicPlayerProfiles").whereEqualTo("searchEligible", true)
            .whereGreaterThanOrEqualTo("normalizedDisplayName", prefix).whereLessThan("normalizedDisplayName", prefix + "\uf8ff")
            .orderBy("normalizedDisplayName").orderBy(FieldPath.documentId())
        if (afterName != null && afterId != null) query = query.startAfter(afterName, afterId)
        val rows = query.limit(budget).get().get(5, TimeUnit.SECONDS).documents
        return CandidatePage(rows.map { SocialCandidate(it.getString("internalUid")!!,
            PublicPlayerProfile(it.id,it.getString("friendCode")!!,it.getString("displayName")!!),it.getString("normalizedDisplayName")!!) }, rows.size == budget)
    }
    override fun privacy(uid: String) = settings(get("players/$uid/socialSettings/current"))
    override fun patchPrivacy(uid: String, patch: PrivacyPatch): SocialPrivacySettings {
      val result=transaction(afterCommit={r:Pair<SocialPrivacySettings,SocialInvalidation?>->invalidation.committed(listOfNotNull(r.second))}) { tx ->
        val ref = doc("players/$uid/socialSettings/current")
        val current = settings(tx.get(ref).get())
        val owner = tx.get(doc("players/$uid/publicIdentity/current")).get()
        val p = tx.get(doc("players/$uid")).get(); val marker = tx.get(doc("developmentTestAccounts/$uid")).get()
        socialCheck(active(p,marker),"SOCIAL_ACTION_NOT_ALLOWED",403)
        socialCheck(owner.exists(),"SOCIAL_IDENTITY_UNAVAILABLE",503)
        socialCheck(current.revision == patch.revision,"REVISION_MISMATCH",409)
        val next=patch.apply(current)
        if(next!=current) {
            tx.set(ref,com.teamfho.domino.match.MatchCodec.map(next))
            if(next.discoverableByName!=current.discoverableByName)tx.update(doc("publicPlayerProfiles/${owner.getString("publicPlayerId")}"),mapOf("searchEligible" to next.discoverableByName,"updatedAt" to FieldValue.serverTimestamp()))
        }
        val event=if(next.presenceVisibility!=current.presenceVisibility || next.matchActivityVisibility!=current.matchActivityVisibility)
            SocialInvalidation.privacy(uid,next.revision) else null
        if(event!=null)tx.set(doc("${SocialInvalidation.COLLECTION}/${event.eventId}"),event.durableData())
        next to event
      }
      return result.first
    }
    override fun hasBlockEitherDirection(a: String,b: String): Boolean = db.getAll(doc("players/$a/blocks/$b"),doc("players/$b/blocks/$a"))
        .get(5,TimeUnit.SECONDS).any { it.exists() }
    override fun blockedTarget(a: String, publicId: String): SocialCandidate? {
        val stored=db.collection("players/$a/blocks").whereEqualTo("publicPlayerId",publicId).limit(1).get().get(5,TimeUnit.SECONDS).documents.firstOrNull()?:return null
        return SocialCandidate(stored.id,PublicPlayerProfile(publicId,stored.getString("friendCode")!!,stored.getString("displayName")!!),"")
    }
    override fun block(a: String,target: SocialCandidate,enabled: Boolean) {
      transaction(afterCommit={events:List<SocialInvalidation>->invalidation.committed(events)}) { tx ->
        val forward = doc("players/$a/blocks/${target.uid}"); val inverse = doc("players/${target.uid}/blockedBy/$a")
        val existing = tx.get(forward).get(); val reverse = tx.get(inverse).get()
        val social=FirestoreSocialTransaction(db,tx)
        if(enabled) FriendshipService(FirestoreFriendships(db),SocialCursor()).removeInTransaction(social,a,target.uid,true,!existing.exists())
        else if(existing.exists()) {
            val id=SocialPairIdentity.id(a,target.uid)
            val pair=social.read("socialPairs/$id")?:mapOf("lowerUid" to minOf(a,target.uid),"upperUid" to maxOf(a,target.uid))
            val revision=Math.addExact((pair["authorizationRevision"] as? Number)?.toLong()?:0,1)
            social.put("socialPairs/$id",pair+("authorizationRevision" to revision))
            social.invalidate(SocialInvalidation.pair(id,revision))
        }
        if (enabled && !existing.exists()) {
            tx.create(forward,mapOf("publicPlayerId" to target.profile.publicPlayerId,"displayName" to target.profile.displayName,
                "friendCode" to target.profile.friendCode,"createdAt" to FieldValue.serverTimestamp()))
            tx.set(inverse,mapOf("ownerUid" to a))
        } else if (!enabled) { if (existing.exists()) tx.delete(forward); if (reverse.exists()) tx.delete(inverse) }
        social.invalidations.toList()
      }
    }
    override fun blocks(a: String,afterId: String?,limit: Int): List<BlockRelationship> {
        var q: Query = db.collection("players/$a/blocks").orderBy("publicPlayerId")
        if (afterId != null) q=q.startAfter(afterId)
        return q.limit(limit).get().get(5,TimeUnit.SECONDS).documents.map {
            BlockRelationship(it.getString("publicPlayerId")!!,it.getString("displayName")!!,it.getString("friendCode")!!)
        }
    }
}
