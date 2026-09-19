package com.teamfho.domino.online

import com.google.cloud.firestore.Firestore
import com.google.cloud.firestore.DocumentChange
import java.time.Instant

class FirestoreTurnWorkFeed(private val db:Firestore):TurnWorkFeed {
    override fun watch(changed:(String,Instant?)->Unit,failed:()->Unit):AutoCloseable {
        val listener=db.collection("onlineTurnWork").addSnapshotListener { snapshot,error ->
            if(error!=null){failed();return@addSnapshotListener}
            snapshot?.documentChanges?.forEach { change ->
                val date=change.document.getTimestamp("dueAt")
                if(change.type!=DocumentChange.Type.REMOVED&&date==null){failed();return@forEach}
                changed(change.document.id,if(change.type==DocumentChange.Type.REMOVED)null else Instant.ofEpochSecond(date!!.seconds,date.nanos.toLong()))
            }
        }
        return AutoCloseable {listener.remove()}
    }
}
