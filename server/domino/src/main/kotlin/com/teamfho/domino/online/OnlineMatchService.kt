package com.teamfho.domino.online

import com.teamfho.domino.catalog.*
import com.teamfho.domino.match.*
import java.time.Clock
import java.security.MessageDigest
import java.util.UUID
import org.slf4j.LoggerFactory

class OnlineMatchService(private val catalog: GameCatalogService, private val repository: OnlineRepository,
    private val engine: OnlineEngine=OnlineEngine(), private val clock: Clock=Clock.systemUTC()) {
    private val log=LoggerFactory.getLogger(javaClass)
    // Injected transport callback runs only after Firestore acknowledges the transaction.
    var committed: (OnlineWrite)->Unit = {}
    fun create(uid: String, modeKey: String, validationData: Boolean=false): OnlineSnapshot {
        checkOnline(modeKey=="DUEL_1V1",OnlineError.MODE_UNAVAILABLE)
        val catalog=catalog.resolve()?:throw OnlineFailure(OnlineError.MODE_UNAVAILABLE)
        val mode=catalog.modes.singleOrNull {it.key==modeKey && it.active}?:throw OnlineFailure(OnlineError.MODE_UNAVAILABLE)
        checkOnline(mode.playerCount==2 && mode.teamMode==TeamMode.NONE && mode.ruleSet.id=="double-nine-duel" && mode.ruleSet.version==1,OnlineError.MODE_UNAVAILABLE)
        // I1 explicit development endpoint opts into ONLINE without mutating immutable v2 publication.
        val snapshot=MatchRuleSnapshot.freeze(catalog,mode);val now=clock.instant()
        val match=Match(UUID.randomUUID().toString(),MatchStatus.CREATED,mode.key,MatchExecutionMode.ONLINE,catalog.catalogVersion,
            mode.topologyVersion,mode.ruleSet.id,mode.ruleSet.version,mode.ruleSet.ruleSchemaVersion,snapshot,listOf(participant(uid,0)),
            0,0,null,listOf(0,0),null,null,null,0,MatchVisibility.PRIVATE,SpectatorPolicy(false,MatchVisibility.PRIVATE),now,now,validationData)
        val state=OnlineState(match,OnlinePhase.WAITING_FOR_PLAYER);repository.create(state)
        log.info("ONLINE_MATCH_CREATED");return snapshot(state,uid)
    }
    private fun participant(uid: String,seat: Int)=MatchParticipant(seat,MatchIds.document(uid),"Player ${seat+1}",null,ControlType.REMOTE_HUMAN,ConnectionState.CONNECTED,clock.instant())
    fun join(uid: String,id: String,commandId: String): OnlineCommit {
        validId(commandId);val before=read(id)
        val result=repository.transact(id,before.match.lastSequence,commandId,fingerprint(uid,"JOIN:$id")) {engine.join(it,participant(uid,1),commandId,clock.instant())}
        publish(result);log.info("ONLINE_MATCH_JOINED");return result
    }
    fun command(uid: String,c: OnlineCommand): OnlineCommit {
        validId(c.commandId);checkOnline(c.protocolVersion==1,OnlineError.INVALID_COMMAND)
        checkOnline(when(c.type) {
            OnlineCommandType.PLAY_TILE->c.tile!=null&&c.chainEnd!=null&&c.candidate==null&&c.even==null
            OnlineCommandType.SELECT_STARTER_TILE->c.candidate!=null&&c.tile==null&&c.chainEnd==null&&c.even==null
            OnlineCommandType.SUBMIT_EVEN_ODD_GUESS->c.even!=null&&c.tile==null&&c.chainEnd==null&&c.candidate==null
            else->c.tile==null&&c.chainEnd==null&&c.candidate==null&&c.even==null
        },OnlineError.INVALID_COMMAND)
        val before=read(c.matchId);OnlineEngine.seat(before,uid)
        val result=repository.transact(c.matchId,before.match.lastSequence,c.commandId,fingerprint(uid,GameCatalogCodec.mapper.writeValueAsString(c))) {
            engine.command(it,uid,c,clock.instant())
        }
        publish(result);log.info("ONLINE_COMMAND_ACCEPTED");return result
    }
    private fun publish(result: OnlineCommit) {result.write?.let {
        log.info("MATCH_EVENT_APPENDED sequence={}",it.state.match.lastSequence)
        if(it.state.match.status==MatchStatus.FINISHED)log.info("ONLINE_MATCH_FINISHED")
        // A transport failure cannot roll back a committed move or turn a retry into a second move.
        try {committed(it)} catch(_: Exception) {log.warn("ONLINE_DELIVERY_UNAVAILABLE")}
    }}
    fun snapshot(uid: String,id: String)=snapshot(read(id),uid)
    fun events(uid: String,id: String,after: Long): OnlineEventPage {
        checkOnline(after>=0,OnlineError.INVALID_COMMAND)
        val state=read(id);val seat=OnlineEngine.seat(state,uid)
        val events=repository.events(id,after).filter {it.sequence<=state.match.lastSequence}.map {authorized(it,seat)}
        return OnlineEventPage(id,events,events.lastOrNull()?.sequence?:minOf(after,state.match.lastSequence))
    }
    private fun read(id: String): OnlineState {validId(id);return repository.read(id)?:throw OnlineFailure(OnlineError.MATCH_NOT_FOUND)}
    companion object {
        fun validId(id: String) {checkOnline(id.matches(Regex("[A-Za-z0-9_-]{1,128}")),OnlineError.INVALID_COMMAND)}
        fun fingerprint(uid: String,payload: String)=MessageDigest.getInstance("SHA-256").digest((uid+"\n"+payload).toByteArray()).joinToString(""){"%02x".format(it)}
        fun authorized(e: MatchEvent,seat: Int)=if(e.visibility==EventVisibility.PUBLIC || (e.visibility==EventVisibility.PLAYER_PRIVATE&&e.targetSeat==seat))
            OnlineEventView(e.sequence,e.type.name,e.copy(causedByCommandId=null)) else OnlineEventView(e.sequence,"SEQUENCE_ADVANCED",null)
        fun snapshot(s: OnlineState,uid: String): OnlineSnapshot {
            val seat=OnlineEngine.seat(s,uid);val m=s.match;val seq=m.lastSequence
            val public=PublicMatchSnapshot(m.matchId,m.modeKey,m.status,m.participants.map {PublicParticipant(it.seatIndex,it.displayNameSnapshot,null,it.controlType)},
                m.score,m.currentRoundNumber,m.currentTurnNumber,m.currentSeat,s.board,listOf(s.hands["0"].orEmpty().size,s.hands["1"].orEmpty().size),s.round?.starterSeat,null,seq,0)
            val private=PrivatePlayerSnapshot(seat,s.hands[seat.toString()].orEmpty(),null,seq)
            val starter=s.starter?.let {OnlineStarterView(it.method,it.guessingSeat,it.attempt,it.selections.keys.map(String::toInt).sorted(),
                if(it.method==StarterMethod.HIGH_TILE_SELECTION)(0..1).filter {n->n !in it.selections.values} else emptyList())}
            val round=s.round?.takeIf {it.status==RoundStatus.FINISHED}?.let {RoundFinished(it.finishType!!,it.winnerSeat,it.scoreRecipient,it.scoreAwarded,it.starterSeat,it.remainingPips,m.score)}
            return OnlineSnapshot(public,private,s.phase,starter,round,seq,m.ruleSnapshot)
        }
    }
}
