package com.teamfho.domino.social

import com.google.cloud.firestore.Firestore
import java.util.concurrent.TimeUnit

/** The A.3 consumer reader. Both projections and their revisions come from one read transaction. */
class SocialPresenceAuthorization(private val database:()->Firestore) : SocialAuthorizationReader {
    fun resolve(publicId:String):String? {
        SocialNames.publicId(publicId)
        return FirestoreSocialRepository(database()).resolve(publicId)?.uid
    }
    override fun read(viewer:String,target:String):SocialAuthorizationSnapshot {
        val db=database()
        return db.runTransaction { tx ->
            val paths=listOf("socialPairs/${SocialPairIdentity.id(viewer,target)}",
                "players/$target/socialSettings/current", "players/$viewer/blocks/$target",
                "players/$target/blocks/$viewer", "friendships/${SocialPairIdentity.id(viewer,target)}",
                "players/$target", "developmentTestAccounts/$target", "players/$viewer", "developmentTestAccounts/$viewer")
            val docs=tx.getAll(*paths.map(db::document).toTypedArray()).get()
            val eligible=viewer!=target && listOf(5,7).all{docs[it].getString("status")=="ACTIVE"} &&
                listOf(6,8).none{docs[it].getBoolean("isTestAccount")==true} && !docs[2].exists() && !docs[3].exists()
            fun permits(field:String)=when(docs[1].getString(field)?:"FRIENDS") {
                "EVERYONE"->true; "FRIENDS"->docs[4].exists(); else->false
            }
            val general=eligible && permits("presenceVisibility")
            SocialAuthorizationSnapshot(general,docs[0].getLong("authorizationRevision")?:0,
                docs[1].getLong("revision")?:1,general && permits("matchActivityVisibility"))
        }.get(5,TimeUnit.SECONDS)
    }
}
