package com.teamfho.domino.online

import com.teamfho.domino.catalog.*
import com.teamfho.domino.match.*
import java.security.SecureRandom
import java.time.Instant

fun interface OnlineRandom { fun next(bound: Int): Int }
class SecureOnlineRandom: OnlineRandom { private val random=SecureRandom(); override fun next(bound: Int)=random.nextInt(bound) }

/** Pure transition builder. Randomness and server time are injected; nothing is published before commit. */
class OnlineEngine(private val random: OnlineRandom=SecureOnlineRandom()) {
    fun join(before: OnlineState, participant: MatchParticipant, commandId: String, now: Instant): OnlineWrite {
        checkOnline(before.match.participants.none {it.playerUid==participant.playerUid},OnlineError.SAME_PLAYER)
        val mode=before.match.ruleSnapshot.mode()
        checkOnline(before.match.status==MatchStatus.CREATED && before.match.participants.size==mode.playerCount-1,OnlineError.MATCH_FULL)
        checkOnline(participant.seatIndex==before.match.participants.size,OnlineError.INVALID_COMMAND)
        if(mode.teamMode==TeamMode.FIXED_TEAMS) {
            val next=before.copy(match=before.match.copy(participants=before.match.participants+participant,status=MatchStatus.STARTING,startedAt=now))
            return Builder(next,commandId,now).apply {emit(MatchStarted(next.match.score));deal(mode.ruleSet.firstRoundStarting.seat)}.build()
        }
        val next=before.copy(match=before.match.copy(participants=before.match.participants+participant,
            status=MatchStatus.STARTING,startedAt=now),phase=OnlinePhase.STARTER_SELECTION,starter=starter(1))
        return Builder(next,commandId,now).apply {emit(MatchStarted(next.match.score));progress()}.build()
    }
    fun command(before: OnlineState, uid: String, command: OnlineCommand, now: Instant): OnlineWrite {
        val seat=seat(before,uid)
        checkOnline(before.match.participants[seat].connectionState!=ConnectionState.ABANDONED,OnlineError.PLAYER_ABANDONED)
        checkOnline(before.match.participants[seat].reconnectDeadlineAt?.let {now<it}!=false,OnlineError.PLAYER_ABANDONED)
        checkOnline(before.match.status !in setOf(MatchStatus.FINISHED,MatchStatus.CANCELLED),OnlineError.MATCH_NOT_ACTIVE)
        if(command.type in setOf(OnlineCommandType.PLAY_TILE,OnlineCommandType.PASS)) {
            checkOnline(before.match.currentSeat==seat,OnlineError.NOT_YOUR_TURN)
            checkOnline(before.turnDeadlineAt==null || now<before.turnDeadlineAt,OnlineError.TURN_EXPIRED)
        }
        val b=Builder(before,command.commandId,now)
        when(command.type) {
            OnlineCommandType.SELECT_STARTER_TILE,OnlineCommandType.SUBMIT_EVEN_ODD_GUESS -> b.select(seat,command)
            OnlineCommandType.PLAY_TILE -> b.play(seat,command)
            OnlineCommandType.PASS -> b.pass(seat)
            OnlineCommandType.NEXT_ROUND -> {
                checkOnline(before.phase==OnlinePhase.ROUND_FINISHED,OnlineError.MATCH_NOT_ACTIVE)
                val starting=before.match.ruleSnapshot.mode().ruleSet.followingRoundStarting
                b.deal(if(starting.mode==StartingMode.FIXED_SEAT)starting.seat else requireNotNull(before.round?.winnerSeat))
            }
        }
        return b.build()
    }
    fun timeout(before: OnlineState, commandId: String, now: Instant): OnlineWrite {
        checkOnline(before.phase==OnlinePhase.PLAYING&&before.turnDeadlineAt!=null&&now>=before.turnDeadlineAt,OnlineError.TIMEOUT_NOT_DUE)
        val mode=before.match.ruleSnapshot.mode();val policy=requireNotNull(mode.ruleSet.turnPolicy)
        require(policy.autoPlayOnTimeout&&policy.autoPlayPolicy==AutoPlayPolicy.FIRST_VALID_MOVE)
        require(mode.onlinePolicy!!.autoPlayWhileDisconnected&&mode.onlinePolicy.turnClockContinuesWhileDisconnected)
        val seat=before.match.currentSeat!!;val b=Builder(before,commandId,now)
        b.emit(TurnTimeout(seat,before.match.currentTurnNumber,before.turnDeadlineAt),seat)
        val legal=before.hands.getValue(seat.toString()).firstNotNullOfOrNull {tile->
            listOf(ChainEnd.LEFT,ChainEnd.RIGHT).firstOrNull {fits(before,tile,it)}?.let {tile to it}}
        // Audit before the normal action. Its final sequence is patched after the complete transition.
        b.emit(AutoPlayed(seat,legal?.first,legal?.second,AutoPlayReason.TURN_TIMEOUT),seat)
        if(legal==null)b.pass(seat) else b.play(seat,OnlineCommand(1,commandId,before.match.matchId,OnlineCommandType.PLAY_TILE,legal.first,legal.second))
        val write=b.build()
        return write.copy(events=write.events.map {e->if(e.payload is AutoPlayed)e.copy(payload=e.payload.copy(resultingSequence=write.state.match.lastSequence)) else e})
    }
    fun connection(before: OnlineState, seat: Int, connected: Boolean, id: String, now: Instant): OnlineWrite? {
        if(before.match.status in setOf(MatchStatus.FINISHED,MatchStatus.CANCELLED))return null
        val p=before.match.participants[seat];if(p.connectionState==ConnectionState.ABANDONED)return null
        val expired=p.reconnectDeadlineAt?.let {now>=it}==true
        val next=when {
            expired->p.copy(connectionState=ConnectionState.ABANDONED,abandonedAt=now)
            connected&&p.connectionState==ConnectionState.DISCONNECTED->p.copy(connectionState=ConnectionState.CONNECTED,disconnectedAt=null,reconnectDeadlineAt=null)
            !connected&&p.connectionState==ConnectionState.CONNECTED->p.copy(connectionState=ConnectionState.DISCONNECTED,disconnectedAt=now,
                reconnectDeadlineAt=now.plusSeconds(requireNotNull(before.match.ruleSnapshot.mode().onlinePolicy).reconnectWindowSeconds.toLong()))
            else->return null
        }
        val b=Builder(before.copy(match=before.match.copy(participants=before.match.participants.map {if(it.seatIndex==seat)next else it})),id,now)
        b.emit(when(next.connectionState){
            ConnectionState.ABANDONED->PlayerAbandoned(seat,now)
            ConnectionState.DISCONNECTED->PlayerDisconnected(seat,now,next.reconnectDeadlineAt)
            else->PlayerReconnected(seat,now)
        },seat)
        return b.build()
    }
    private fun starter(attempt: Int, method: StarterMethod?=null): OnlineStarter {
        val deck=deck();val selected=method?:StarterMethod.entries[random.next(2)]
        val first=deck.removeAt(random.next(deck.size));val second=deck.removeAt(random.next(deck.size))
        return OnlineStarter(selected,if(selected==StarterMethod.HIGH_TILE_SELECTION)listOf(first,second)else listOf(first),guessingSeat=random.next(2),attempt=attempt)
    }
    private fun deck()=(0..9).flatMap {a->(a..9).map {b->DominoPips(a,b)}}.toMutableList()
    private inner class Builder(var s: OnlineState,val id: String,val now: Instant) {
        val events=mutableListOf<MatchEvent>();val rounds=mutableListOf<MatchRound>()
        fun emit(payload: MatchPayload, actor: Int?=null, target: Int?=null) {
            val seq=s.match.lastSequence+1
            events+=MatchEvent(MatchIds.event(seq),s.match.matchId,seq,1,s.match.currentRoundNumber,s.match.currentTurnNumber,
                payload.type(),actor,if(target==null)EventVisibility.PUBLIC else EventVisibility.PLAYER_PRIVATE,target,payload,now,id)
            s=s.copy(match=s.match.copy(lastSequence=seq,updatedAt=now))
        }
        fun progress(resolved: Int?=null) {val st=s.starter!!;emit(StarterProgress(st.method,st.attempt,st.selections.keys.map(String::toInt).sorted(),st.guessingSeat,resolved))}
        fun select(seat: Int,c: OnlineCommand) {
            checkOnline(s.phase==OnlinePhase.STARTER_SELECTION,OnlineError.STARTER_PHASE_REQUIRED)
            var st=s.starter!!;var winner: Int?=null
            if(st.method==StarterMethod.HIGH_TILE_SELECTION) {
                checkOnline(c.type==OnlineCommandType.SELECT_STARTER_TILE && c.candidate in 0..1 && seat.toString() !in st.selections && c.candidate !in st.selections.values,OnlineError.INVALID_STARTER_ACTION)
                st=st.copy(selections=st.selections+(seat.toString() to c.candidate!!));s=s.copy(starter=st);progress()
                if(st.selections.size==2) {
                    val a=pips(st.candidates[st.selections.getValue("0")]);val b=pips(st.candidates[st.selections.getValue("1")])
                    if(a==b) {s=s.copy(starter=starter(st.attempt+1,st.method));progress();return}
                    winner=if(a>b)0 else 1
                }
            } else {
                checkOnline(c.type==OnlineCommandType.SUBMIT_EVEN_ODD_GUESS && seat==st.guessingSeat && c.even!=null,OnlineError.INVALID_STARTER_ACTION)
                winner=if((pips(st.candidates.first())%2==0)==c.even)seat else 1-seat
            }
            if(winner!=null) {
                progress(winner)
                // Reveal the resolved audit only after the guess/selection, privately to each participant.
                val audit=StarterSelectionResult(st.method,winner,st.attempt,st.candidates,1-st.guessingSeat,st.guessingSeat,c.even)
                for(target in 0..1)emit(PrivateStarterSelection(audit),target=target)
                deal(winner)
            }
        }
        fun deal(winner: Int) {
            val rules=s.match.ruleSnapshot.mode().ruleSet
            val deck=deck()
            for(i in deck.lastIndex downTo 1) {val j=random.next(i+1);val t=deck[i];deck[i]=deck[j];deck[j]=t}
            val seats=(0 until s.match.ruleSnapshot.mode().playerCount).toList()
            val hands=seats.associate{it.toString() to mutableListOf<DominoPips>()}
            repeat(rules.tilesPerPlayer){ for(seat in rules.dealPolicy.seatOrder)hands.getValue(seat.toString())+=deck.removeAt(0) }
            val round=MatchRound(s.match.currentRoundNumber+1,winner,now,null,RoundStatus.IN_PROGRESS,s.match.lastSequence+1,null,null,null,null,0,null,dealtSeats=seats)
            s=s.copy(phase=OnlinePhase.PLAYING,starter=null,hands=hands,reserve=deck,board=emptyList(),consecutivePasses=0,round=round,
                match=s.match.copy(status=MatchStatus.IN_PROGRESS,currentRoundNumber=round.roundNumber,currentTurnNumber=1,currentSeat=winner))
            emit(RoundStarted(winner,seats.map{rules.tilesPerPlayer}))
            for(seat in seats)emit(HandDealt(seat,hands.getValue(seat.toString())),target=seat)
            startDeadline(winner);rounds+=round
        }
        fun playing(seat: Int) {
            checkOnline(s.phase==OnlinePhase.PLAYING && s.match.status==MatchStatus.IN_PROGRESS,OnlineError.MATCH_NOT_ACTIVE)
            checkOnline(s.match.currentSeat==seat,OnlineError.NOT_YOUR_TURN)
        }
        fun play(seat: Int,c: OnlineCommand) {
            playing(seat)
            val hand=s.hands.getValue(seat.toString());val tile=hand.find {same(it,c.tile)}
            checkOnline(tile!=null,OnlineError.TILE_NOT_IN_HAND)
            checkOnline(c.chainEnd!=null && fits(s,tile!!,c.chainEnd),OnlineError.ILLEGAL_MOVE)
            val capicua=s.match.ruleSnapshot.mode().ruleSet.capicuaPolicy!=null && hand.size==1 && s.board.isNotEmpty() && fits(s,tile!!,ChainEnd.LEFT) && fits(s,tile,ChainEnd.RIGHT)
            val oriented=orient(s,tile!!,c.chainEnd!!)
            val placement=BoardPlacement(oriented,c.chainEnd,seat)
            val board=if(c.chainEnd==ChainEnd.LEFT)listOf(placement)+s.board else s.board+placement
            s=s.copy(hands=s.hands+(seat.toString() to hand.filterNot {same(it,tile)}),board=board,consecutivePasses=0)
            emit(TilePlayed(seat,oriented,c.chainEnd),seat)
            if(hand.size==1)finish(seat,if(capicua)FinishType.CAPICUA else FinishType.NORMAL) else turn(nextSeat(seat))
        }
        fun pass(seat: Int) {
            playing(seat)
            checkOnline(s.hands.getValue(seat.toString()).none {fits(s,it,ChainEnd.LEFT)||fits(s,it,ChainEnd.RIGHT)},OnlineError.PASS_NOT_ALLOWED)
            s=s.copy(consecutivePasses=s.consecutivePasses+1);emit(PlayerPassed(seat),seat)
            if(s.consecutivePasses==s.match.ruleSnapshot.mode().playerCount) {
                if(s.match.ruleSnapshot.mode().teamMode==TeamMode.FIXED_TEAMS) {
                    val points=(0 until s.match.ruleSnapshot.mode().playerCount).map{handPips(s,it)}
                    val minima=points.indices.filter{points[it]==points.min()}
                    finish(if(minima.map{scoreOwner(it)}.distinct().size>1)-1 else minima.first(),FinishType.BLOCKED)
                    return
                }
                val a=handPips(s,0);val b=handPips(s,1)
                finish(if(a==b)s.round!!.starterSeat else if(a<b)0 else 1,FinishType.BLOCKED)
            } else turn(nextSeat(seat))
        }
        fun startDeadline(seat: Int) {
            val policy=s.match.ruleSnapshot.mode().ruleSet.turnPolicy
            if(policy==null){s=s.copy(turnStartedAt=null,turnDeadlineAt=null);emit(TurnStarted(seat,null,s.match.currentTurnNumber));return}
            val seconds=policy.timeLimitSeconds.toLong()
            s=s.copy(turnStartedAt=now,turnDeadlineAt=now.plusSeconds(seconds))
            emit(TurnStarted(seat,s.turnDeadlineAt,s.match.currentTurnNumber,now,s.turnDeadlineAt))
        }
        fun turn(seat: Int) {s=s.copy(match=s.match.copy(currentSeat=seat,currentTurnNumber=s.match.currentTurnNumber+1));emit(TurnChanged(seat));startDeadline(seat)}
        fun nextSeat(seat:Int):Int {val order=s.match.ruleSnapshot.mode().ruleSet.turnOrder;return order[(order.indexOf(seat)+1)%order.size]}
        fun scoreOwner(seat:Int):Int {val m=s.match.ruleSnapshot.mode();return if(m.teamMode==TeamMode.NONE)seat else m.seatTeams.indexOfFirst{seat in it}}
        fun finish(winner: Int,type: FinishType) {
            val mode=s.match.ruleSnapshot.mode();val rules=mode.ruleSet
            val remaining=(0 until mode.playerCount).map{handPips(s,it)}
            val owner=if(winner<0)null else ScoreRecipient(if(mode.teamMode==TeamMode.NONE)ScoreOwnerType.PLAYER else ScoreOwnerType.TEAM,scoreOwner(winner))
            val opponent=if(winner<0)0 else remaining.indices.filter{scoreOwner(it)!=owner!!.index}.sumOf{remaining[it]}
            val award=if(winner<0)0 else Math.multiplyExact(s.nextRoundMultiplier,when(type) {FinishType.BLOCKED->opponent+rules.blockedScoring.bonus
                FinishType.NORMAL->opponent+rules.finishScoring.bonus
                FinishType.CAPICUA->opponent*rules.capicuaPolicy!!.pipMultiplier+rules.finishScoring.bonus})
            val score=s.match.score.toMutableList();if(owner!=null)score[owner.index]=Math.addExact(score[owner.index],award)
            val round=s.round!!.copy(status=RoundStatus.FINISHED,finishedAt=now,endSequence=s.match.lastSequence+1,winnerSeat=winner.takeIf{it>=0},
                winnerTeam=owner?.takeIf{it.type==ScoreOwnerType.TEAM}?.index,
                finishType=type,scoreAwarded=award,scoreRecipient=owner,remainingPips=remaining)
            s=s.copy(phase=OnlinePhase.ROUND_FINISHED,round=round,turnStartedAt=null,turnDeadlineAt=null,
                nextRoundMultiplier=if(winner<0)rules.tiePolicy.nextRoundMultiplier else 1,match=s.match.copy(score=score,currentSeat=null));rounds+=round
            emit(RoundFinished(type,round.winnerSeat,owner,award,round.starterSeat,remaining,score))
            if(owner!=null&&score[owner.index]>=rules.targetScore) {
                val result=MatchResult(owner,MatchFinishReason.TARGET_REACHED,score)
                s=s.copy(phase=OnlinePhase.MATCH_FINISHED,match=s.match.copy(status=MatchStatus.FINISHED,finishedAt=now,result=result))
                emit(MatchFinished(result))
            }
        }
        fun build(): OnlineWrite {
            val m=s.match
            val history=if(m.status!=MatchStatus.FINISHED)emptyMap() else m.participants.associate {p->p.playerUid!! to
                PlayerMatchHistory(m.matchId,m.modeKey,m.ruleSetId,m.ruleSetVersion,if(m.result!!.winner!!.index==scoreOwner(p.seatIndex))HistoryResult.WIN else HistoryResult.LOSS,
                    m.score,m.participants.filter {scoreOwner(it.seatIndex)!=scoreOwner(p.seatIndex)}.map {OpponentSummary(it.seatIndex,it.displayNameSnapshot,it.teamId)},m.startedAt,now,MatchFinishReason.TARGET_REACHED,m.validationData)}
            return OnlineWrite(s,events,rounds,history)
        }
    }
    companion object {
        fun seat(s: OnlineState,uid: String): Int = s.match.participants.singleOrNull {it.playerUid==uid}?.seatIndex?:throw OnlineFailure(OnlineError.NOT_PARTICIPANT)
        fun pips(t: DominoPips)=t.sideA+t.sideB
        fun handPips(s: OnlineState,seat: Int)=s.hands[seat.toString()].orEmpty().sumOf(::pips)
        fun same(a: DominoPips,b: DominoPips?)=b!=null && ((a==b)||(a.sideA==b.sideB&&a.sideB==b.sideA))
        fun fits(s: OnlineState,t: DominoPips,end: ChainEnd): Boolean {
            if(s.board.isEmpty())return true
            val pip=if(end==ChainEnd.LEFT)s.board.first().tile.sideA else s.board.last().tile.sideB
            return t.sideA==pip||t.sideB==pip
        }
        fun orient(s: OnlineState,t: DominoPips,end: ChainEnd): DominoPips {
            if(s.board.isEmpty())return t
            return if(end==ChainEnd.LEFT) {if(t.sideB==s.board.first().tile.sideA)t else DominoPips(t.sideB,t.sideA)}
                else {if(t.sideA==s.board.last().tile.sideB)t else DominoPips(t.sideB,t.sideA)}
        }
    }
}
