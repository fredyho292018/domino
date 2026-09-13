package com.teamfho.domino.match

import com.teamfho.domino.catalog.GameCatalogService
import com.teamfho.domino.catalog.TeamMode
import java.time.Clock
import java.util.UUID

class SpectatorPolicyRules(val minimumHandDelaySeconds: Long = 60) {
    init { require(minimumHandDelaySeconds>=60) }
    fun validate(policy: SpectatorPolicy) {
        require(policy.delaySeconds>=0)
        if(policy.handView==HandView.SELECTED_PLAYER_DELAYED)require(policy.delaySeconds>=minimumHandDelaySeconds)
        if(policy.visibility!=MatchVisibility.PUBLIC)require(!policy.enabled)
    }
    fun defaults(visibility: MatchVisibility)=SpectatorPolicy(visibility==MatchVisibility.PUBLIC,visibility,
        maxOf(90,minimumHandDelaySeconds),HandView.SELECTED_PLAYER_DELAYED)
}

// No creation/transition HTTP route: participants and confirmed transitions are trusted server inputs.
// This validates lifecycle/storage integrity, not legal Domino moves. I1 supplies the authoritative engine.
class MatchPersistenceService(private val catalog: GameCatalogService, private val repository: MatchRepository,
    private val clock: Clock = Clock.systemUTC(), private val spectators: SpectatorPolicyRules = SpectatorPolicyRules(),
    private val newId: ()->String = { UUID.randomUUID().toString() }) {
    fun create(modeKey: String, execution: MatchExecutionMode, participants: List<MatchParticipant>,
        visibility: MatchVisibility = MatchVisibility.PRIVATE, validationData: Boolean = false): Match {
        require(execution==MatchExecutionMode.LOCAL){"ONLINE_ENGINE_NOT_IMPLEMENTED"}
        val published=catalog.resolve()?:error("CATALOG_UNAVAILABLE")
        val mode=published.modes.single {it.key==modeKey && it.active}
        require(mode.executionModesSupported.any {it.name==execution.name})
        val seats=participants.sortedBy {it.seatIndex};require(seats.map {it.seatIndex}==mode.turnOrderSeats())
        val humans=seats.count {it.controlType!=ControlType.BOT};require(humans in mode.minHumans..mode.maxHumans)
        require(mode.botsAllowed || humans==seats.size)
        require(seats.none {it.controlType==ControlType.REMOTE_HUMAN})
        seats.forEach { p ->
            require(p.displayNameSnapshot.isNotBlank() && p.displayNameSnapshot.length<=80)
            if(p.controlType!=ControlType.BOT)MatchIds.document(requireNotNull(p.playerUid)) else require(p.playerUid==null)
            require(p.teamId==mode.seatTeams.indexOfFirst {p.seatIndex in it}.takeIf {it>=0})
        }
        require(seats.mapNotNull {it.playerUid}.distinct().size==humans)
        val now=clock.instant();val policy=spectators.defaults(visibility);spectators.validate(policy)
        val snapshot=MatchRuleSnapshot.freeze(published,mode);snapshot.verify()
        val match=Match(MatchIds.document(newId()),MatchStatus.CREATED,mode.key,execution,published.catalogVersion,mode.topologyVersion,
            mode.ruleSet.id,mode.ruleSet.version,mode.ruleSet.ruleSchemaVersion,snapshot,seats,0,0,null,
            List(if(mode.teamMode==TeamMode.NONE)mode.playerCount else mode.seatTeams.size){0},null,null,null,0,
            visibility,policy,now,now,validationData)
        repository.create(match);return requireNotNull(repository.read(match.matchId))
    }
    fun append(matchId: String, expectedSequence: Long, commandId: String, confirmed: ConfirmedTransition): Match {
        MatchIds.document(matchId);MatchIds.document(commandId)
        // Snapshot caller-owned lists before a transaction can retry.
        val input=MatchCodec.copy(confirmed,ConfirmedTransition::class.java)
        val now=clock.instant()
        return repository.transact(matchId,expectedSequence,commandId) { aggregate ->
            val m=aggregate.match;val p=input.payload;val sequence=m.lastSequence+1
            require(now>=m.updatedAt);require(m.status !in setOf(MatchStatus.FINISHED,MatchStatus.CANCELLED))
            fun seat(s: Int) {require(s in m.participants.indices)}
            input.actorSeat?.let(::seat);input.targetSeat?.let(::seat)
            var next=m;var round=aggregate.round
            fun inRound(){require(m.status==MatchStatus.IN_PROGRESS && round?.status==RoundStatus.IN_PROGRESS)}
            when(p) {
                is MatchStarted -> {require(m.status==MatchStatus.CREATED && p.initialScores==m.score);next=m.copy(status=MatchStatus.IN_PROGRESS,startedAt=now)}
                is RoundStarted -> {
                    require(m.status==MatchStatus.IN_PROGRESS && (round==null||round!!.status==RoundStatus.FINISHED));seat(p.starterSeat)
                    require(p.tilesRemainingPerSeat==List(m.participants.size){m.ruleSnapshot.mode().ruleSet.tilesPerPlayer})
                    val n=m.currentRoundNumber+1
                    round=MatchRound(n,p.starterSeat,now,null,RoundStatus.IN_PROGRESS,sequence,null,null,null,null,0,null)
                    next=m.copy(currentRoundNumber=n,currentTurnNumber=0,currentSeat=p.starterSeat)
                }
                is HandDealt -> {inRound();seat(p.seat);require(input.targetSeat==p.seat)
                    val rules=m.ruleSnapshot.mode().ruleSet
                    require(p.seat !in round!!.dealtSeats && m.currentTurnNumber==0)
                    require(p.tiles.size==rules.tilesPerPlayer && p.tiles.map {minOf(it.sideA,it.sideB) to maxOf(it.sideA,it.sideB)}.distinct().size==p.tiles.size)
                    require(p.tiles.all {it.sideA<=rules.maxPip && it.sideB<=rules.maxPip})
                    round=round!!.copy(dealtSeats=round!!.dealtSeats+p.seat)
                }
                is PrivateStarterSelection -> {inRound();seat(p.result.starterSeat);require(input.targetSeat!=null && p.result.attempt>0)}
                is TurnStarted -> {inRound();seat(p.seat);require(p.turnDeadline==null && round!!.dealtSeats.size==m.participants.size);next=m.copy(currentSeat=p.seat,currentTurnNumber=m.currentTurnNumber+1)}
                is TurnChanged -> {inRound();seat(p.seat);next=m.copy(currentSeat=p.seat)}
                is TilePlayed -> {inRound();seat(p.seat);require(m.currentSeat==p.seat && input.actorSeat==p.seat)}
                is PlayerPassed -> {inRound();seat(p.seat);require(m.currentSeat==p.seat && input.actorSeat==p.seat)}
                is RoundFinished -> {
                    inRound();require(p.roundStarter==round!!.starterSeat)
                    require(p.remainingPips.size==m.participants.size && p.remainingPips.all {it>=0} && p.scoreAwarded>=0)
                    p.winnerSeat?.let(::seat)
                    require((p.winnerSeat==null)==(p.scoreRecipient==null))
                    val scores=m.score.toMutableList()
                    if(p.scoreRecipient!=null){validateRecipient(m,p.scoreRecipient);require(p.scoreRecipient==owner(m,p.winnerSeat!!));scores[p.scoreRecipient.index]=Math.addExact(scores[p.scoreRecipient.index],p.scoreAwarded)}
                    else require(p.scoreAwarded==0)
                    require(scores==p.scoresAfter)
                    round=round!!.copy(status=RoundStatus.FINISHED,finishedAt=now,endSequence=sequence,winnerSeat=p.winnerSeat,
                        winnerTeam=p.scoreRecipient?.takeIf {it.type==ScoreOwnerType.TEAM}?.index,finishType=p.finishType,
                        scoreAwarded=p.scoreAwarded,scoreRecipient=p.scoreRecipient,remainingPips=p.remainingPips)
                    next=m.copy(score=scores,currentSeat=null)
                }
                is MatchFinished -> {
                    require(p.result.scores==m.score)
                    if(p.result.finishReason==MatchFinishReason.TARGET_REACHED){require(round?.status==RoundStatus.FINISHED)
                        val winner=requireNotNull(p.result.winner);validateRecipient(m,winner);require(m.score[winner.index]>=m.ruleSnapshot.mode().ruleSet.targetScore)
                    } else require(p.result.winner==null)
                    next=m.copy(status=if(p.result.finishReason==MatchFinishReason.CANCELLED)MatchStatus.CANCELLED else MatchStatus.FINISHED,
                        finishedAt=now,result=p.result,currentSeat=null)
                }
                else -> error("FUTURE_RUNTIME_EVENT_NOT_IMPLEMENTED")
            }
            val privateEvent=p is HandDealt || p is PrivateStarterSelection
            require(privateEvent || input.targetSeat==null)
            val event=MatchEvent(MatchIds.event(sequence),m.matchId,sequence,1,next.currentRoundNumber,next.currentTurnNumber,
                p.type(),input.actorSeat,if(privateEvent)EventVisibility.PLAYER_PRIVATE else EventVisibility.PUBLIC,input.targetSeat,p,now,commandId)
            next=next.copy(lastSequence=sequence,updatedAt=now)
            MatchWrite(next,round,event,if(p is MatchFinished)history(next) else emptyMap())
        }
    }
    private fun history(m: Match): Map<String,PlayerMatchHistory> = m.participants.filter {it.playerUid!=null}.associate { p ->
        val r=m.result!!
        val result=when {r.finishReason==MatchFinishReason.CANCELLED->HistoryResult.CANCELLED;r.winner==null->HistoryResult.DRAW;
            owner(m,p.seatIndex)==r.winner->HistoryResult.WIN;else->HistoryResult.LOSS}
        p.playerUid!! to PlayerMatchHistory(m.matchId,m.modeKey,m.ruleSetId,m.ruleSetVersion,result,m.score,
            m.participants.filter {owner(m,it.seatIndex)!=owner(m,p.seatIndex)}.map {OpponentSummary(it.seatIndex,it.displayNameSnapshot,it.teamId)},
            m.startedAt,m.finishedAt!!,r.finishReason,m.validationData)
    }
    companion object {
        fun owner(m: Match, seat: Int)=m.participants[seat].teamId?.let {ScoreRecipient(ScoreOwnerType.TEAM,it)}?:ScoreRecipient(ScoreOwnerType.PLAYER,seat)
        fun validateRecipient(m: Match,r: ScoreRecipient){require(r.index in m.score.indices && r.type==if(m.ruleSnapshot.mode().teamMode==TeamMode.NONE)ScoreOwnerType.PLAYER else ScoreOwnerType.TEAM)}
    }
}
private fun com.teamfho.domino.catalog.ResolvedGameMode.turnOrderSeats()=(0 until playerCount).toList()
