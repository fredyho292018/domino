package com.teamfho.domino.social

import com.google.cloud.firestore.*
import java.util.concurrent.TimeUnit

class FirestoreSocialTransaction(private val db:Firestore,private val tx:Transaction):SocialTransaction {
    override fun read(path:String)=tx.get(db.document(path)).get().data
    override fun put(path:String,data:Map<String,Any>) {tx.set(db.document(path),data)}
    override fun delete(path:String) {tx.delete(db.document(path))}
}
class FirestoreFriendships(private val db:Firestore):FriendshipRepository {
    override fun <T> atomic(body:(SocialTransaction)->T):T=db.runTransaction {tx->body(FirestoreSocialTransaction(db,tx))}.get(20,TimeUnit.SECONDS)
    override fun read(path:String)=db.document(path).get().get(5,TimeUnit.SECONDS).data
    override fun readAll(paths:List<String>):Map<String,Map<String,Any>?> {
        if(paths.isEmpty())return emptyMap()
        return db.getAll(*paths.distinct().map{db.document(it)}.toTypedArray()).get(5,TimeUnit.SECONDS).associate{it.reference.path to it.data}
    }
    override fun page(collection:String,filters:Map<String,Any>,timeField:String,after:Pair<String,String>?,limit:Int):List<Pair<String,Map<String,Any>>> {
        var q:Query=db.collection(collection)
        for((k,v) in filters)q=q.whereEqualTo(k,v)
        q=q.orderBy(timeField,Query.Direction.DESCENDING).orderBy(FieldPath.documentId(),Query.Direction.DESCENDING)
        if(after!=null)q=q.startAfter(after.first.toLong(),after.second)
        return q.limit(limit).get().get(5,TimeUnit.SECONDS).documents.map{it.id to it.data}
    }
}
