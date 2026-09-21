package com.teamfho.domino.social

import com.google.cloud.firestore.*
import java.util.concurrent.TimeUnit
import java.util.concurrent.ExecutionException

/** Future.get() wraps read failures. Firestore's transaction runner only classifies
 * a direct ApiException for its bounded retry policy; preserve that original error.
 * No outer retry loop, custom backoff or retry of domain/application failures. */
internal fun <T> socialTransactionCallback(body:()->T):T = try {body()} catch(e:ExecutionException) {
    var cause:Throwable=e
    while(cause is ExecutionException && cause.cause!=null)cause=cause.cause!!
    if(cause is com.google.api.gax.rpc.ApiException)throw cause
    throw e
}

class FirestoreSocialTransaction(private val db:Firestore,private val tx:Transaction):SocialTransaction {
    val invalidations=mutableListOf<SocialInvalidation>()
    override fun read(path:String)=tx.get(db.document(path)).get().data
    override fun put(path:String,data:Map<String,Any>) {
        tx.set(db.document(path),data)
        if(path.startsWith("${SocialInvalidation.COLLECTION}/"))invalidations.add(SocialInvalidation.decode(data))
    }
    override fun delete(path:String) {tx.delete(db.document(path))}
}
class FirestoreFriendships(private val db:Firestore,private val invalidation:SocialInvalidationSink=SocialInvalidationSink {}):FriendshipRepository {
    override fun <T> atomic(body:(SocialTransaction)->T):T {
        val future=db.runTransaction {tx->socialTransactionCallback {
            val social=FirestoreSocialTransaction(db,tx)
            body(social) to social.invalidations.toList()
        }}
        // Attach to SDK completion, not the caller's timeout: a late successful commit must
        // still invalidate locally even if the HTTP request stopped waiting.
        return com.google.api.core.ApiFutures.transform(future,{result->
            invalidation.committed(result.second)
            result.first
        },java.util.concurrent.Executor{it.run()}).get(20,TimeUnit.SECONDS)
    }
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
