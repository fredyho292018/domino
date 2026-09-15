package com.teamfho.domino.online

import com.google.cloud.firestore.Firestore
import com.google.cloud.firestore.TransactionOptions
import com.teamfho.domino.catalog.GameCatalogCodec
import com.teamfho.domino.match.*
import java.util.concurrent.TimeUnit
import java.time.Instant
import com.google.cloud.Timestamp

interface OnlineRepository {
    fun createPaired(write: OnlineWrite): OnlineState = throw UnsupportedOperationException("Atomic paired creation unavailable")
    fun settleFailedCreation(id: String): OnlineState? = throw UnsupportedOperationException("Creation receipt unavailable")
    fun create(state: OnlineState)
    fun read(matchId: String): OnlineState?
    fun events(matchId: String, after: Long): List<MatchEvent>
    fun transact(matchId: String, expectedSequence: Long, commandId: String, fingerprint: String,
        transition: (OnlineState)->OnlineWrite): OnlineCommit
    fun due(now: Instant): List<String> = emptyList()
    fun activeFor(uid: String): List<String> = emptyList()
    fun refreshDiscovery(matchId: String, now: Instant) { }
}
object OnlineWrites {
    fun validate(before: OnlineState, write: OnlineWrite, commandId: String) {
        val a=before.match;val b=write.state.match
        require(a.matchId==b.matchId && a.ruleSnapshot==b.ruleSnapshot && a.createdAt==b.createdAt && a.validationData==b.validationData)
        require(a.executionMode==MatchExecutionMode.ONLINE && b.executionMode==a.executionMode)
        require(a.modeKey==b.modeKey && a.catalogVersion==b.catalogVersion && a.ruleSetId==b.ruleSetId && a.ruleSetVersion==b.ruleSetVersion)
        require(b.participants.size in 1..2 && b.participants.map {it.playerUid}.distinct().size==b.participants.size)
        require(b.participants.take(a.participants.size).mapIndexed {i,p->p.copy(connectionState=a.participants[i].connectionState,
            disconnectedAt=a.participants[i].disconnectedAt,reconnectDeadlineAt=a.participants[i].reconnectDeadlineAt,abandonedAt=a.participants[i].abandonedAt)}==a.participants)
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
    private fun creation(id:String)=db.document("onlineMatchCreationReceipts/${MatchIds.document(id)}")
    private fun assignment(uid:String)=db.document("onlinePlayerAssignments/${MatchIds.document(uid)}")
    override fun createPaired(write:OnlineWrite):OnlineState {
        val state=write.state;val id=state.match.matchId
        require(state.match.participants.size==2 && state.match.participants.map{it.playerUid}.distinct().size==2)
        state.match.ruleSnapshot.verify()
        return db.runTransaction {tx ->
            val receipt=tx.get(creation(id)).get()
            if(receipt.exists()) {
                checkOnline(receipt.getString("status")=="COMMITTED",OnlineError.MATCH_NOT_ACTIVE)
                val existing=decode(tx.get(root(id).collection("runtime").document("authoritative")).get().data!!)
                require(existing.match.participants.map{it.playerUid}.toSet()==state.match.participants.map{it.playerUid}.toSet())
                return@runTransaction existing
            }
            val refs=state.match.participants.map{assignment(requireNotNull(it.playerUid))}
            val assignments=refs.map{tx.get(it).get().getString("matchId")}
            assignments.filterNotNull().distinct().forEach {previous ->
                val data=tx.get(root(previous).collection("runtime").document("authoritative")).get().data
                val prior=data?.let(::decode)
                checkOnline(prior==null || prior.match.status in setOf(MatchStatus.FINISHED,MatchStatus.CANCELLED) ||
                    prior.match.participants.filter{it.playerUid in state.match.participants.map{p->p.playerUid}}.all{it.connectionState==ConnectionState.ABANDONED},OnlineError.MATCH_FULL)
            }
            tx.create(root(id),MatchCodec.map(state.match))
            tx.create(root(id).collection("runtime").document("authoritative"),stateMap(state))
            state.match.participants.forEach{tx.create(root(id).collection("players").document(it.seatIndex.toString()),MatchCodec.map(it))}
            write.events.forEach{tx.create(root(id).collection("events").document(it.eventId),MatchCodec.map(it))}
            write.rounds.forEach{tx.create(root(id).collection("rounds").document(it.roundNumber.toString()),MatchCodec.map(it))}
            tx.create(work(id),workMap(state,state.match.updatedAt))
            refs.forEach{tx.set(it,mapOf("matchId" to id))}
            tx.create(creation(id),mapOf("status" to "COMMITTED","matchId" to id))
            state
        }.get(30,TimeUnit.SECONDS)
    }
    override fun settleFailedCreation(id:String):OnlineState? = db.runTransaction {tx ->
        val receipt=tx.get(creation(id)).get()
        if(receipt.getString("status")=="COMMITTED")
            return@runTransaction decode(tx.get(root(id).collection("runtime").document("authoritative")).get().data!!)
        // Fences a late/ambiguous creation transaction before Redis members can be released.
        if(!receipt.exists())tx.create(creation(id),mapOf("status" to "ABORTED"))
        null
    }.get(30,TimeUnit.SECONDS)
    private fun work(id: String)=db.document("onlineTurnWork/${MatchIds.document(id)}")
    private fun timestamp(t: Instant)=Timestamp.ofTimeSecondsAndNanos(t.epochSecond,t.nano)
    private fun workMap(s: OnlineState, now: Instant, checkAt: Instant=now.plusSeconds(30)): Map<String,Any> {
        val dates=listOfNotNull(s.turnDeadlineAt,checkAt)+s.match.participants.filter {it.connectionState==ConnectionState.DISCONNECTED}.mapNotNull {it.reconnectDeadlineAt}
        return mapOf("dueAt" to timestamp(dates.min()),"presenceCheckAt" to timestamp(checkAt),"uids" to s.match.participants.mapNotNull {it.playerUid})
    }
    private fun root(id: String)=db.document("matches/${MatchIds.document(id)}")
    private fun stateMap(s: OnlineState)=mapOf("stateJson" to GameCatalogCodec.mapper.writeValueAsString(s))
    private fun decode(data: Map<String,Any>)=GameCatalogCodec.mapper.readValue(data.getValue("stateJson") as String,OnlineState::class.java)
    override fun create(state: OnlineState) {
        state.match.ruleSnapshot.verify();val ref=root(state.match.matchId);val batch=db.batch()
        batch.create(ref,MatchCodec.map(state.match));batch.create(ref.collection("runtime").document("authoritative"),stateMap(state))
        state.match.participants.forEach {batch.create(ref.collection("players").document(it.seatIndex.toString()),MatchCodec.map(it))}
        batch.create(work(state.match.matchId),workMap(state,state.match.createdAt))
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
            val workRef=work(matchId)
            val priorCheck=FirestoreMatchRepository.transactionRead(tx.get(workRef)).getTimestamp("presenceCheckAt")?.let {Instant.ofEpochSecond(it.seconds,it.nanos.toLong())}
            checkOnline(before.match.lastSequence==expectedSequence,OnlineError.STALE_COMMAND)
            val write=transition(before);OnlineWrites.validate(before,write,commandId)
            val r=OnlineReceipt(fingerprint,expectedSequence+1,write.state.match.lastSequence)
            tx.set(root,MatchCodec.map(write.state.match));tx.set(stateRef,stateMap(write.state))
            write.state.match.participants.forEach {tx.set(root.collection("players").document(it.seatIndex.toString()),MatchCodec.map(it))}
            write.rounds.forEach {tx.set(root.collection("rounds").document(it.roundNumber.toString()),MatchCodec.map(it))}
            write.events.forEach {tx.create(root.collection("events").document(it.eventId),MatchCodec.map(it))}
            write.histories.forEach {(uid,h)->tx.create(db.document("players/${MatchIds.document(uid)}/matchHistory/$matchId"),MatchCodec.map(h))}
            if(write.state.match.status in setOf(MatchStatus.FINISHED,MatchStatus.CANCELLED))tx.delete(workRef)
            else tx.set(workRef,workMap(write.state,write.state.match.updatedAt,priorCheck?:write.state.match.updatedAt.plusSeconds(30)))
            tx.create(receipt,MatchCodec.map(r));OnlineCommit(write,r)
        },TransactionOptions.createReadWriteOptionsBuilder().setNumberOfAttempts(8).build()))
    }
    override fun due(now: Instant)=db.collection("onlineTurnWork").whereLessThanOrEqualTo("dueAt",timestamp(now)).orderBy("dueAt").limit(100).get().get(15,TimeUnit.SECONDS).documents.map {it.id}
    override fun activeFor(uid: String)=db.collection("onlineTurnWork").whereArrayContains("uids",uid).get().get(15,TimeUnit.SECONDS).documents.map {it.id}
    override fun refreshDiscovery(matchId: String,now: Instant) {
        db.runTransaction {tx->
            val data=tx.get(root(matchId).collection("runtime").document("authoritative")).get().data
            val s=data?.let(::decode)
            if(s==null||s.match.status in setOf(MatchStatus.FINISHED,MatchStatus.CANCELLED))tx.delete(work(matchId))
            else tx.set(work(matchId),workMap(s,now))
            null
        }.get(30,TimeUnit.SECONDS)
    }
}
