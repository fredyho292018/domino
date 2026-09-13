package com.teamfho.domino.match

import com.google.cloud.firestore.FieldPath
import com.google.cloud.firestore.Firestore
import com.google.cloud.firestore.Query
import com.google.cloud.firestore.TransactionOptions
import com.google.api.core.ApiFuture
import java.util.concurrent.ExecutionException
import java.util.concurrent.TimeUnit

class FirestoreMatchRepository(private val db: Firestore): MatchRepository,MatchEventRepository,PlayerMatchHistoryRepository {
    private fun ref(id: String)=db.document("matches/${MatchIds.document(id)}")
    override fun create(match: Match) {
        require(match.lastSequence==0L && match.status==MatchStatus.CREATED);match.ruleSnapshot.verify()
        val batch=db.batch();batch.create(ref(match.matchId),MatchCodec.map(match))
        match.participants.forEach {batch.create(ref(match.matchId).collection("players").document(it.seatIndex.toString()),MatchCodec.map(it))}
        batch.commit().get(30,TimeUnit.SECONDS)
    }
    override fun read(matchId: String)=ref(matchId).get().get(15,TimeUnit.SECONDS).data?.let {MatchCodec.read(it,Match::class.java).also {m->m.ruleSnapshot.verify()}}
    override fun round(matchId: String,number: Int): MatchRound? {
        require(number>=0)
        return ref(matchId).collection("rounds").document(number.toString()).get().get(15,TimeUnit.SECONDS).data?.let {MatchCodec.read(it,MatchRound::class.java)}
    }
    override fun transact(matchId: String, expectedSequence: Long, commandId: String, transition: (MatchAggregate)->MatchWrite): Match {
        MatchIds.document(commandId)
        return db.runTransaction({tx ->
            val root=ref(matchId);val receipt=root.collection("commands").document(commandId)
            val prior=transactionRead(tx.get(receipt)).data
            if(prior!=null) return@runTransaction MatchCodec.read(prior,Match::class.java)
            val m=MatchCodec.read(transactionRead(tx.get(root)).data?:error("MATCH_NOT_FOUND"),Match::class.java)
            require(m.lastSequence==expectedSequence){"SEQUENCE_CONFLICT"}
            val current=if(m.currentRoundNumber==0)null else transactionRead(tx.get(root.collection("rounds").document(m.currentRoundNumber.toString()))).data?.let {MatchCodec.read(it,MatchRound::class.java)}
            val write=transition(MatchAggregate(m,current));MatchWriteRules.validate(m,write,commandId)
            // All reads precede writes. Retry commits either the whole transition or nothing.
            tx.set(root,MatchCodec.map(write.match))
            write.round?.let {tx.set(root.collection("rounds").document(it.roundNumber.toString()),MatchCodec.map(it))}
            tx.create(root.collection("events").document(write.event.eventId),MatchCodec.map(write.event))
            write.histories.forEach {(uid,h)->tx.create(db.document("players/${MatchIds.document(uid)}/matchHistory/$matchId"),MatchCodec.map(h))}
            tx.create(receipt,MatchCodec.map(write.match))
            write.match
        },TransactionOptions.createReadWriteOptionsBuilder().setNumberOfAttempts(8).build()).get(30,TimeUnit.SECONDS)
    }
    override fun readTrustedEvents(matchId: String,afterSequence: Long,limit: Int): List<MatchEvent> {
        require(afterSequence>=0 && limit in 1..500)
        return ref(matchId).collection("events").whereGreaterThan("sequence",afterSequence).orderBy("sequence").limit(limit)
            .get().get(15,TimeUnit.SECONDS).documents.map {MatchCodec.read(it.data,MatchEvent::class.java)}
    }
    override fun history(uid: String,limit: Int,afterMatchId: String?): HistoryPage {
        require(limit in 1..100);MatchIds.document(uid)
        var query: Query=db.collection("players/$uid/matchHistory").orderBy(FieldPath.documentId(),Query.Direction.ASCENDING)
        if(afterMatchId!=null)query=query.startAfter(MatchIds.document(afterMatchId))
        val page=query.limit(limit+1).get().get(15,TimeUnit.SECONDS).documents.map {MatchCodec.read(it.data,PlayerMatchHistory::class.java)}
        return HistoryPage(page.take(limit),if(page.size>limit)page[limit-1].matchId else null)
    }
    companion object {
        // Firestore retries ABORTED thrown from the transaction callback. Future.get wraps
        // read contention in ExecutionException; preserve the actual retryable SDK cause.
        internal fun <T> transactionRead(future: ApiFuture<T>): T = try {future.get()}
        catch(e: ExecutionException) {throw (e.cause?:e)}
        catch(e: InterruptedException) {Thread.currentThread().interrupt();throw e}
    }
}
