package com.teamfho.domino.online

import com.google.cloud.firestore.Firestore
import com.google.cloud.firestore.TransactionOptions
import com.teamfho.domino.catalog.GameCatalogCodec
import com.teamfho.domino.match.*
import java.util.concurrent.TimeUnit

interface OnlineRepository {
    fun create(state: OnlineState)
    fun read(matchId: String): OnlineState?
    fun events(matchId: String, after: Long): List<MatchEvent>
    fun transact(matchId: String, expectedSequence: Long, commandId: String, fingerprint: String,
        transition: (OnlineState)->OnlineWrite): OnlineCommit
}
object OnlineWrites {
    fun validate(before: OnlineState, write: OnlineWrite, commandId: String) {
        val a=before.match;val b=write.state.match
        require(a.matchId==b.matchId && a.ruleSnapshot==b.ruleSnapshot && a.createdAt==b.createdAt && a.validationData==b.validationData)
        require(a.executionMode==MatchExecutionMode.ONLINE && b.executionMode==a.executionMode)
        require(a.modeKey==b.modeKey && a.catalogVersion==b.catalogVersion && a.ruleSetId==b.ruleSetId && a.ruleSetVersion==b.ruleSetVersion)
        require(b.participants.size==2 && b.participants.map {it.playerUid}.distinct().size==2)
        require(b.participants.take(a.participants.size)==a.participants)
        require(write.events.isNotEmpty() && b.lastSequence==a.lastSequence+write.events.size)
        write.events.forEachIndexed {i,e->
            require(e.sequence==a.lastSequence+i+1 && e.eventId==MatchIds.event(e.sequence) && e.matchId==a.matchId && e.causedByCommandId==commandId)
            require(e.type==e.payload.type() && e.createdAt>=a.updatedAt)
            require(if(e.payload is HandDealt || e.payload is PrivateStarterSelection)e.visibility==EventVisibility.PLAYER_PRIVATE && e.targetSeat in 0..1
                else e.visibility==EventVisibility.PUBLIC && e.targetSeat==null)
            if(e.payload is HandDealt)require(e.targetSeat==e.payload.seat)
        }
        require(b.updatedAt==write.events.last().createdAt)
        b.ruleSnapshot.verify()
    }
    fun prior(receipt: OnlineReceipt,fingerprint: String): OnlineCommit {
        checkOnline(receipt.fingerprint==fingerprint,OnlineError.COMMAND_ID_CONFLICT)
        return OnlineCommit(null,receipt)
    }
}
/** Same M4 Match/round/event/history paths. Private engine state is a transactional child,
 * encoded as JSON because Firestore cannot persist nested hand arrays. No Redis authority. */
class FirestoreOnlineRepository(private val db: Firestore): OnlineRepository {
    private fun root(id: String)=db.document("matches/${MatchIds.document(id)}")
    private fun stateMap(s: OnlineState)=mapOf("stateJson" to GameCatalogCodec.mapper.writeValueAsString(s))
    private fun decode(data: Map<String,Any>)=GameCatalogCodec.mapper.readValue(data.getValue("stateJson") as String,OnlineState::class.java)
    override fun create(state: OnlineState) {
        state.match.ruleSnapshot.verify();val ref=root(state.match.matchId);val batch=db.batch()
        batch.create(ref,MatchCodec.map(state.match));batch.create(ref.collection("runtime").document("authoritative"),stateMap(state))
        state.match.participants.forEach {batch.create(ref.collection("players").document(it.seatIndex.toString()),MatchCodec.map(it))}
        batch.commit().get(30,TimeUnit.SECONDS)
    }
    override fun read(matchId: String)=root(matchId).collection("runtime").document("authoritative").get().get(15,TimeUnit.SECONDS).data?.let(::decode)
    override fun events(matchId: String,after: Long)=FirestoreMatchRepository(db).readTrustedEvents(matchId,after,500)
    override fun transact(matchId: String,expectedSequence: Long,commandId: String,fingerprint: String,transition:(OnlineState)->OnlineWrite): OnlineCommit {
        val root=root(matchId);MatchIds.document(commandId)
        return FirestoreMatchRepository.transactionRead(db.runTransaction({tx->
            val receipt=root.collection("commands").document(commandId)
            val prior=FirestoreMatchRepository.transactionRead(tx.get(receipt)).data
            if(prior!=null)return@runTransaction OnlineWrites.prior(MatchCodec.read(prior,OnlineReceipt::class.java),fingerprint)
            val stateRef=root.collection("runtime").document("authoritative")
            val before=FirestoreMatchRepository.transactionRead(tx.get(stateRef)).data?.let(::decode)?:throw OnlineFailure(OnlineError.MATCH_NOT_FOUND)
            checkOnline(before.match.lastSequence==expectedSequence,OnlineError.STALE_COMMAND)
            val write=transition(before);OnlineWrites.validate(before,write,commandId)
            val r=OnlineReceipt(fingerprint,expectedSequence+1,write.state.match.lastSequence)
            tx.set(root,MatchCodec.map(write.state.match));tx.set(stateRef,stateMap(write.state))
            write.state.match.participants.drop(before.match.participants.size).forEach {tx.create(root.collection("players").document(it.seatIndex.toString()),MatchCodec.map(it))}
            write.rounds.forEach {tx.set(root.collection("rounds").document(it.roundNumber.toString()),MatchCodec.map(it))}
            write.events.forEach {tx.create(root.collection("events").document(it.eventId),MatchCodec.map(it))}
            write.histories.forEach {(uid,h)->tx.create(db.document("players/${MatchIds.document(uid)}/matchHistory/$matchId"),MatchCodec.map(h))}
            tx.create(receipt,MatchCodec.map(r));OnlineCommit(write,r)
        },TransactionOptions.createReadWriteOptionsBuilder().setNumberOfAttempts(8).build()))
    }
}
