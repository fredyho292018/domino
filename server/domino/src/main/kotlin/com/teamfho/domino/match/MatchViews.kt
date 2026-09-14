package com.teamfho.domino.match

import java.time.Clock
import java.time.Instant

data class PublicParticipant(val seat: Int, val displayNameSnapshot: String, val teamId: Int?, val controlType: ControlType)
data class PublicMatchSnapshot(val matchId: String, val modeKey: String, val status: MatchStatus,
    val participants: List<PublicParticipant>, val scores: List<Int>, val currentRound: Int, val currentTurn: Int,
    val currentSeat: Int?, val board: List<BoardPlacement>, val tilesRemainingPerSeat: List<Int>, val starterSeat: Int?,
    val turnDeadline: Instant?, val lastSequence: Long, val spectatorDelaySeconds: Long)
data class PrivatePlayerSnapshot(val seat: Int, val hand: List<DominoPips>, val setup: StarterSelectionResult?, val lastSequence: Long)
data class DelayedSpectatorSnapshot(val cutoffTime: Instant, val sequence: Long, val publicState: PublicMatchSnapshot,
    val selectedSeat: Int?, val selectedPlayerHand: PrivatePlayerSnapshot?)

// Complete, ordered trusted stream required. Filtered public streams cannot reconstruct private hands.
// Private deals are never returned through public state or general-public event access.
object MatchReplayReducer {
    data class Reconstruction(val publicState: PublicMatchSnapshot, val privateStates: Map<Int,PrivatePlayerSnapshot>)
    fun reconstruct(match: Match, events: List<MatchEvent>, cutoff: Instant = Instant.MAX): Reconstruction {
        var expected=1L;var lastTime=match.createdAt
        var state=PublicMatchSnapshot(match.matchId,match.modeKey,MatchStatus.CREATED,
            match.participants.map {PublicParticipant(it.seatIndex,it.displayNameSnapshot,it.teamId,it.controlType)},
            List(match.score.size){0},0,0,null,emptyList(),List(match.participants.size){0},null,null,0,match.spectatorPolicy.delaySeconds)
        val hands=mutableMapOf<Int,List<DominoPips>>();val setups=mutableMapOf<Int,StarterSelectionResult>()
        for(e in events) {
            require(e.matchId==match.matchId && e.sequence==expected++ && e.eventId==MatchIds.event(e.sequence))
            require(e.eventSchemaVersion==1 && e.type==e.payload.type() && e.createdAt>=lastTime);lastTime=e.createdAt
            if(e.createdAt>cutoff)break
            require(e.sequence<=match.lastSequence)
            when(val p=e.payload) {
                is MatchStarted->state=state.copy(status=MatchStatus.IN_PROGRESS,scores=p.initialScores)
                is RoundStarted->{hands.clear();setups.clear();state=state.copy(board=emptyList(),tilesRemainingPerSeat=p.tilesRemainingPerSeat,starterSeat=p.starterSeat,currentSeat=p.starterSeat)}
                is HandDealt->{require(e.visibility==EventVisibility.PLAYER_PRIVATE && e.targetSeat==p.seat && p.seat !in hands);hands[p.seat]=p.tiles.toList()}
                is PrivateStarterSelection->{require(e.visibility==EventVisibility.PLAYER_PRIVATE);setups[requireNotNull(e.targetSeat)]=p.result}
                is TurnStarted->state=state.copy(currentSeat=p.seat,turnDeadline=p.turnDeadline)
                is TurnChanged->state=state.copy(currentSeat=p.seat)
                is TilePlayed->{
                    val board=state.board.toMutableList();val tile=BoardPlacement(p.tile,p.chainEnd,p.seat)
                    if(p.chainEnd==ChainEnd.LEFT)board.add(0,tile) else board.add(tile)
                    val counts=state.tilesRemainingPerSeat.toMutableList();require(counts[p.seat]>0);counts[p.seat]--
                    hands[p.seat]?.let {hand->val next=hand.toMutableList();val index=next.indexOfFirst {it==p.tile || (it.sideA==p.tile.sideB&&it.sideB==p.tile.sideA)};require(index>=0);next.removeAt(index);hands[p.seat]=next}
                    state=state.copy(board=board,tilesRemainingPerSeat=counts)
                }
                is RoundFinished->state=state.copy(scores=p.scoresAfter,currentSeat=null,turnDeadline=null)
                is MatchFinished->state=state.copy(status=if(p.result.finishReason==MatchFinishReason.CANCELLED)MatchStatus.CANCELLED else MatchStatus.FINISHED,scores=p.result.scores,currentSeat=null,turnDeadline=null)
                is PlayerPassed->Unit
                is StarterProgress->state=state.copy(status=MatchStatus.STARTING)
                else->error("UNSUPPORTED_REPLAY_SEMANTICS")
            }
            state=state.copy(lastSequence=e.sequence,currentRound=e.roundNumber,currentTurn=e.turnNumber)
        }
        return Reconstruction(state,hands.mapValues {(seat,hand)->PrivatePlayerSnapshot(seat,hand.toList(),setups[seat],state.lastSequence)})
    }
}

// Foundation authorization boundary, not an HTTP/WebSocket delivery service.
class MatchViewService(private val clock: Clock=Clock.systemUTC(),private val policies: SpectatorPolicyRules=SpectatorPolicyRules()) {
    fun publicEvents(match: Match, events: List<MatchEvent>): List<MatchEvent> {
        require(match.visibility==MatchVisibility.PUBLIC)
        return events.filter {it.matchId==match.matchId && it.visibility==EventVisibility.PUBLIC && it.targetSeat==null &&
            it.payload !is HandDealt && it.payload !is PrivateStarterSelection}
            .map {MatchCodec.copy(it,MatchEvent::class.java).copy(causedByCommandId=null)}
    }
    fun player(match: Match, events: List<MatchEvent>, authenticatedUid: String): PrivatePlayerSnapshot {
        val seat=match.participants.single {it.playerUid==authenticatedUid && it.controlType!=ControlType.BOT}.seatIndex
        require(events.lastOrNull()?.sequence==match.lastSequence || match.lastSequence==0L)
        val rebuilt=MatchReplayReducer.reconstruct(match,events)
        return rebuilt.privateStates[seat]?:PrivatePlayerSnapshot(seat,emptyList(),null,rebuilt.publicState.lastSequence)
    }
    fun spectator(match: Match, events: List<MatchEvent>, mode: SpectatorViewMode, selectedSeat: Int? = null): DelayedSpectatorSnapshot {
        val policy=match.spectatorPolicy;policies.validate(policy)
        require(match.visibility==MatchVisibility.PUBLIC && policy.enabled)
        require(if(mode==SpectatorViewMode.FOLLOW_PLAYER) selectedSeat in match.participants.indices && policy.handView==HandView.SELECTED_PLAYER_DELAYED else selectedSeat==null)
        require(events.lastOrNull()?.sequence==match.lastSequence || match.lastSequence==0L)
        val cutoff=clock.instant().minusSeconds(policy.delaySeconds)
        val rebuilt=MatchReplayReducer.reconstruct(match,events,cutoff)
        // Both pieces come from one reducer invocation and one cutoff. Never merge a live hand.
        val hand=selectedSeat?.let {rebuilt.privateStates[it]?:PrivatePlayerSnapshot(it,emptyList(),null,rebuilt.publicState.lastSequence)}
        return DelayedSpectatorSnapshot(cutoff,rebuilt.publicState.lastSequence,rebuilt.publicState,selectedSeat,hand)
    }
}
