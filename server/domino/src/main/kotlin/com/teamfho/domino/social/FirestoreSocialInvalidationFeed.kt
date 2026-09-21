package com.teamfho.domino.social

import com.google.cloud.Timestamp
import com.google.cloud.firestore.*
import java.util.concurrent.TimeUnit

data class SocialFeedCursor(val time:Timestamp,val id:String?=null)
data class SocialFeedPage(val events:List<SocialInvalidation>,val cursor:SocialFeedCursor,val caughtUp:Boolean)

/** The outbox is also the immutable recovery feed. Publication never deletes an event.
 * Cursor uses Firestore commit timestamps, not application clocks. Empty reads advance to
 * the server snapshot readTime inclusively, so equal timestamps cannot create a gap. */
class FirestoreSocialInvalidationFeed(private val db:Firestore) {
    companion object { const val BATCH=20 }
    private fun collection()=db.collection(SocialInvalidation.COLLECTION)
    fun start():SocialFeedCursor {
        val snapshot=collection().orderBy("createdAt",Query.Direction.DESCENDING).limit(1).get().get(5,TimeUnit.SECONDS)
        return SocialFeedCursor(snapshot.readTime)
    }
    fun recover(cursor:SocialFeedCursor):SocialFeedPage {
        var query:Query=collection().orderBy("createdAt").orderBy(FieldPath.documentId())
        query=if(cursor.id==null)query.whereGreaterThanOrEqualTo("createdAt",cursor.time)else query.startAfter(cursor.time,cursor.id)
        val snapshot=query.limit(BATCH).get().get(5,TimeUnit.SECONDS)
        val docs=snapshot.documents
        val caughtUp=docs.size<BATCH
        val next=if(caughtUp)SocialFeedCursor(snapshot.readTime)else SocialFeedCursor(docs.last().getTimestamp("createdAt")!!,docs.last().id)
        return SocialFeedPage(docs.map{SocialInvalidation.decode(it.data)},next,caughtUp)
    }
    fun pending(limit:Int=BATCH):List<SocialInvalidation> {
        require(limit in 1..BATCH)
        return collection().whereEqualTo("published",false).limit(limit).get().get(5,TimeUnit.SECONDS)
            .documents.map{SocialInvalidation.decode(it.data)}
    }
    fun published(event:SocialInvalidation) {collection().document(event.eventId).update("published",true).get(5,TimeUnit.SECONDS)}
    fun listenPending(onChange:()->Unit,onFailure:()->Unit):ListenerRegistration =
        collection().whereEqualTo("published",false).limit(BATCH).addSnapshotListener { snapshot,error->
            if(error!=null)onFailure()else if(snapshot!=null && !snapshot.isEmpty)onChange()
        }
}
