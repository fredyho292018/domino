package com.teamfho.domino.match

import com.teamfho.domino.catalog.GameCatalogCodec

data class MatchAggregate(val match: Match, val round: MatchRound?)
data class MatchWrite(val match: Match, val round: MatchRound?, val event: MatchEvent,
    val histories: Map<String,PlayerMatchHistory> = emptyMap())

// Trusted server interface only. No raw mutation, sequence or rule JSON exposed to clients.
interface MatchRepository {
    fun create(match: Match)
    fun read(matchId: String): Match?
    fun round(matchId: String, number: Int): MatchRound?
    fun transact(matchId: String, expectedSequence: Long, commandId: String,
        transition: (MatchAggregate)->MatchWrite): Match
}
interface MatchEventRepository {
    // Internal persistence/reconstruction access, including private data; never a controller dependency.
    fun readTrustedEvents(matchId: String, afterSequence: Long, limit: Int = 500): List<MatchEvent>
}
interface PlayerMatchHistoryRepository {
    fun history(uid: String, limit: Int, afterMatchId: String?): HistoryPage
}
object MatchCodec {
    fun <T> copy(value: T, type: Class<T>): T = GameCatalogCodec.decode(value as Any,type)
    fun map(value: Any)=GameCatalogCodec.map(value)
    fun <T> read(value: Map<String,Any>, type: Class<T>): T = GameCatalogCodec.decode(value,type)
}
object MatchWriteRules {
    fun validate(before: Match, write: MatchWrite, commandId: String) {
        val after=write.match;val e=write.event
        require(after.matchId==before.matchId && after.ruleSnapshot==before.ruleSnapshot)
        require(after.catalogVersion==before.catalogVersion && after.modeKey==before.modeKey && after.topologyVersion==before.topologyVersion)
        require(after.ruleSetId==before.ruleSetId && after.ruleSetVersion==before.ruleSetVersion && after.ruleSchemaVersion==before.ruleSchemaVersion)
        require(after.createdAt==before.createdAt && after.validationData==before.validationData)
        require(after.participants==before.participants && after.executionMode==before.executionMode)
        require(after.visibility==before.visibility && after.spectatorPolicy==before.spectatorPolicy)
        require(after.lastSequence==before.lastSequence+1 && e.sequence==after.lastSequence)
        require(e.matchId==before.matchId && e.eventId==MatchIds.event(e.sequence) && e.causedByCommandId==commandId)
        require(e.type==e.payload.type() && e.eventSchemaVersion==1 && e.createdAt>=before.updatedAt)
        require(after.updatedAt==e.createdAt)
        val privatePayload=e.payload is HandDealt || e.payload is PrivateStarterSelection
        require(if(privatePayload) e.visibility==EventVisibility.PLAYER_PRIVATE && e.targetSeat in before.participants.indices
            else e.visibility==EventVisibility.PUBLIC && e.targetSeat==null)
        if(e.payload is HandDealt)require(e.targetSeat==e.payload.seat)
    }
}

// Deterministic transaction double; production wiring uses Firestore, never this memory store.
class InMemoryMatchRepository: MatchRepository,MatchEventRepository,PlayerMatchHistoryRepository {
    private val matches=mutableMapOf<String,Match>()
    private val rounds=mutableMapOf<Pair<String,Int>,MatchRound>()
    private val events=mutableMapOf<String,MutableList<MatchEvent>>()
    private val receipts=mutableMapOf<Pair<String,String>,Match>()
    private val histories=mutableMapOf<String,MutableMap<String,PlayerMatchHistory>>()
    @Synchronized override fun create(match: Match) {
        require(match.matchId !in matches && match.lastSequence==0L && match.status==MatchStatus.CREATED)
        match.ruleSnapshot.verify();matches[match.matchId]=MatchCodec.copy(match,Match::class.java)
    }
    @Synchronized override fun read(matchId: String)=matches[matchId]?.let { MatchCodec.copy(it,Match::class.java) }
    @Synchronized override fun round(matchId: String, number: Int)=rounds[matchId to number]?.let { MatchCodec.copy(it,MatchRound::class.java) }
    @Synchronized override fun transact(matchId: String, expectedSequence: Long, commandId: String, transition: (MatchAggregate)->MatchWrite): Match {
        receipts[matchId to commandId]?.let { return MatchCodec.copy(it,Match::class.java) }
        val before=read(matchId)?:error("MATCH_NOT_FOUND");require(before.lastSequence==expectedSequence){"SEQUENCE_CONFLICT"}
        val write=transition(MatchAggregate(before,round(matchId,before.currentRoundNumber)))
        MatchWriteRules.validate(before,write,commandId)
        val detached=MatchCodec.copy(write,MatchWrite::class.java)
        matches[matchId]=detached.match
        detached.round?.let { rounds[matchId to it.roundNumber]=it }
        events.getOrPut(matchId){mutableListOf()}.add(detached.event)
        detached.histories.forEach { (uid,h)->histories.getOrPut(uid){mutableMapOf()}[matchId]=h }
        receipts[matchId to commandId]=detached.match
        return MatchCodec.copy(detached.match,Match::class.java)
    }
    @Synchronized override fun readTrustedEvents(matchId: String, afterSequence: Long, limit: Int): List<MatchEvent> {
        require(afterSequence>=0 && limit in 1..500)
        return events[matchId].orEmpty().filter {it.sequence>afterSequence}.take(limit).map {MatchCodec.copy(it,MatchEvent::class.java)}
    }
    @Synchronized override fun history(uid: String, limit: Int, afterMatchId: String?): HistoryPage {
        require(limit in 1..100);MatchIds.document(uid);afterMatchId?.let(MatchIds::document)
        val ordered=histories[uid].orEmpty().values.sortedWith(compareByDescending<PlayerMatchHistory>{it.finishedAt}.thenByDescending{it.matchId})
        val offset=if(afterMatchId==null)0 else ordered.indexOfFirst{it.matchId==afterMatchId}.also{require(it>=0)}+1
        val all=ordered.drop(offset).take(limit+1)
        return HistoryPage(all.take(limit).map {MatchCodec.copy(it,PlayerMatchHistory::class.java)},if(all.size>limit)all[limit-1].matchId else null)
    }
}
