package com.teamfho.domino.match

import com.teamfho.domino.catalog.*
import com.teamfho.domino.security.FirebaseIdentity
import org.junit.jupiter.api.Test
import org.junit.jupiter.params.ParameterizedTest
import org.junit.jupiter.params.provider.ValueSource
import java.time.*
import java.util.concurrent.Executors
import kotlin.test.*

class MutableMatchClock(var now: Instant=Instant.ofEpochSecond(800)): Clock() {
    override fun instant()=now
    override fun getZone(): ZoneId=ZoneOffset.UTC
    override fun withZone(zone: ZoneId): Clock=this
}
class MatchFixture(val key: String="DUEL_1V1", val repo: InMemoryMatchRepository=InMemoryMatchRepository()) {
    val clock=MutableMatchClock()
    var publication=GameCatalogV2Publisher.canonical()
    val catalog=GameCatalogService(GameCatalogRepository {publication},1,clock)
    val service=MatchPersistenceService(catalog,repo,clock)
    val mode=GameCatalogValidator.resolve(publication).modes.single {it.key==key}
    val participants=(0 until mode.playerCount).map {seat->MatchParticipant(seat,"m4-test-$seat","Alias $seat",
        mode.seatTeams.indexOfFirst {seat in it}.takeIf {it>=0},ControlType.LOCAL_HUMAN,ConnectionState.CONNECTED,clock.instant())}
    var match=service.create(key,MatchExecutionMode.LOCAL,participants,MatchVisibility.PUBLIC,true)
    private val deck=(0..9).flatMap {a->(a..9).map {b->DominoPips(a,b)}}
    fun emit(p: MatchPayload,actor: Int?=null,target: Int?=null): Match {
        clock.now=clock.now.plusSeconds(1)
        match=service.append(match.matchId,match.lastSequence,"cmd-${match.lastSequence+1}",ConfirmedTransition(p,actor,target));return match
    }
    fun start(){emit(MatchStarted(match.score));startRound()}
    fun startRound(starter: Int=0){emit(RoundStarted(starter,List(mode.playerCount){10}));participants.forEach {emit(HandDealt(it.seatIndex,hand(it.seatIndex)),target=it.seatIndex)};emit(TurnStarted(starter))}
    fun hand(seat: Int)=deck.drop(seat*10).take(10)
    fun events()=repo.readTrustedEvents(match.matchId,0)
    fun finishRound(type: FinishType=FinishType.NORMAL,award: Int=150,seat: Int=0,pips: List<Int> = listOf(0,140)) {
        val recipient=MatchPersistenceService.owner(match,seat);val scores=match.score.toMutableList();scores[recipient.index]+=award
        emit(RoundFinished(type,seat,recipient,award,repo.round(match.matchId,match.currentRoundNumber)!!.starterSeat,pips,scores))
    }
    fun finish(){emit(MatchFinished(MatchResult(ScoreRecipient(ScoreOwnerType.PLAYER,0),MatchFinishReason.TARGET_REACHED,match.score)))}
}

class MatchFoundationTests {
    @Test fun `Firestore transaction read preserves retryable cause instead of wrapping`() {
        val cause=IllegalStateException("read aborted")
        val thrown=assertFails {FirestoreMatchRepository.transactionRead(com.google.api.core.ApiFutures.immediateFailedFuture<String>(cause))}
        assertSame(cause,thrown)
    }
    @ParameterizedTest @ValueSource(strings=["DUEL_1V1","PARTNERS_2V2"])
    fun `generic topology and snapshot round trip`(key: String) {
        val f=MatchFixture(key);val m=f.match
        assertEquals(f.mode.playerCount,m.participants.size);assertEquals(f.mode.ruleSet,m.ruleSnapshot.mode().ruleSet)
        assertEquals(m,MatchCodec.copy(m,Match::class.java));m.ruleSnapshot.verify()
        assertEquals(if(key=="DUEL_1V1")listOf<Int?>(null,null) else listOf<Int?>(0,1,0,1),m.participants.map {it.teamId})
        assertEquals("domino-m3-v1",m.ruleSnapshot.engineSemanticsVersion)
    }
    @Test fun `catalog publication cannot change a running match`() {
        val f=MatchFixture();f.start();val original=f.match.ruleSnapshot
        f.publication=GameCatalogSeed.canonical();f.clock.now=f.clock.now.plusSeconds(10)
        assertEquals(1,f.catalog.resolve()!!.catalogVersion)
        assertEquals(original,f.repo.read(f.match.matchId)!!.ruleSnapshot)
        assertFails {f.service.create("DUEL_1V1",MatchExecutionMode.LOCAL,f.participants)}
    }
    @Test fun `snapshot typed view edits do not mutate frozen rules`() {
        val f=MatchFixture();val view=f.match.ruleSnapshot.mode()
        (view.ruleSet.turnOrder as MutableList<Int>).clear()
        assertEquals(listOf(0,1),f.match.ruleSnapshot.mode().ruleSet.turnOrder)
        assertFails {f.match.ruleSnapshot.copy(effectiveModeJson="{}").verify()}
        assertEquals(TurnPolicy(60,true,AutoPlayPolicy.FIRST_VALID_MOVE),f.match.ruleSnapshot.mode().ruleSet.turnPolicy)
        assertEquals(180,f.match.ruleSnapshot.mode().onlinePolicy!!.reconnectWindowSeconds)
    }
    @Test fun `two human duel rejects bots and online runtime`() {
        val f=MatchFixture()
        assertFails {f.service.create("DUEL_1V1",MatchExecutionMode.LOCAL,f.participants.map {it.copy(controlType=ControlType.BOT,playerUid=null)})}
        assertFails {f.service.create("DUEL_1V1",MatchExecutionMode.ONLINE,f.participants)}
        assertFails {f.service.create("DUEL_1V1",MatchExecutionMode.LOCAL,f.participants.map {it.copy(playerUid="same")})}
    }
    @Test fun `partners accept bots without creating fabricated identities`() {
        val f=MatchFixture("PARTNERS_2V2")
        val p=f.participants.map {if(it.seatIndex==0)it else it.copy(playerUid=null,controlType=ControlType.BOT)}
        val m=f.service.create("PARTNERS_2V2",MatchExecutionMode.LOCAL,p)
        assertEquals(3,m.participants.count {it.controlType==ControlType.BOT});assertEquals(2,m.score.size)
    }
    @Test fun `participant list and returned match are detached`() {
        val f=MatchFixture();(f.match.participants as MutableList<MatchParticipant>).clear()
        assertEquals(2,f.repo.read(f.match.matchId)!!.participants.size)
    }
    @Test fun `sequence gaps stale version and failed transition leave no writes`() {
        val f=MatchFixture();f.start();val seq=f.match.lastSequence
        assertFails {f.service.append(f.match.matchId,seq+1,"bad-gap",ConfirmedTransition(PlayerPassed(0),0))}
        assertFails {f.service.append(f.match.matchId,seq,"bad-actor",ConfirmedTransition(TilePlayed(1,f.hand(1)[0],ChainEnd.RIGHT),1))}
        assertEquals(seq,f.repo.read(f.match.matchId)!!.lastSequence)
        f.emit(PlayerPassed(0),0)
        assertEquals((1L..seq+1).toList(),f.events().map {it.sequence})
    }
    @Test fun `duplicate command is exactly once and parallel sequences conflict`() {
        val f=MatchFixture();f.start();val seq=f.match.lastSequence
        val pool=Executors.newFixedThreadPool(8)
        try {
            val duplicates=(1..20).map {pool.submit<Match>{f.service.append(f.match.matchId,seq,"repeat",ConfirmedTransition(PlayerPassed(0),0))}}.map {it.get()}
            assertTrue(duplicates.all {it.lastSequence==seq+1});assertEquals(seq+1,f.repo.read(f.match.matchId)!!.lastSequence)
            val races=(1..10).map {i->pool.submit<Boolean>{runCatching {f.service.append(f.match.matchId,seq+1,"race-$i",ConfirmedTransition(PlayerPassed(0),0))}.isSuccess}}.map {it.get()}
            assertEquals(1,races.count {it});assertEquals(seq+2,f.repo.read(f.match.matchId)!!.lastSequence)
        } finally {pool.shutdownNow()}
    }
    @Test fun `repository prevents snapshot rewrite and duplicate assigned sequence`() {
        val f=MatchFixture();f.start();val seq=f.match.lastSequence
        fun malicious(modify: (MatchWrite)->MatchWrite) { assertFails {f.repo.transact(f.match.matchId,seq,"attack") {a->
            val e=MatchEvent(MatchIds.event(seq+1),a.match.matchId,seq+1,1,1,1,MatchEventType.PLAYER_PASSED,0,EventVisibility.PUBLIC,null,PlayerPassed(0),a.match.updatedAt,"attack")
            modify(MatchWrite(a.match.copy(lastSequence=seq+1),a.round,e))
        }} }
        malicious {it.copy(match=it.match.copy(ruleSnapshot=it.match.ruleSnapshot.copy(ruleSetVersion=999)))}
        malicious {it.copy(match=it.match.copy(ruleSetVersion=999))}
        malicious {it.copy(match=it.match.copy(catalogVersion=999))}
        malicious {it.copy(event=it.event.copy(sequence=seq))}
        assertEquals(seq,f.repo.read(f.match.matchId)!!.lastSequence)
    }
    @Test fun `round events preserve Capicua without multiplying bonus`() {
        val f=MatchFixture();f.start();f.finishRound(FinishType.CAPICUA,74,pips=listOf(0,32))
        val r=f.repo.round(f.match.matchId,1)!!
        assertEquals(FinishType.CAPICUA,r.finishType);assertEquals(74,r.scoreAwarded);assertEquals(0,r.starterSeat)
        assertEquals(1,f.match.currentRoundNumber);assertEquals(f.match.lastSequence,r.endSequence)
        val e=f.events().last().payload as RoundFinished;assertEquals(listOf(0,32),e.remainingPips)
    }
    @Test fun `Tranque audit preserves tied pips and starter winner`() {
        val f=MatchFixture();f.emit(MatchStarted(f.match.score));f.startRound(1)
        f.finishRound(FinishType.BLOCKED,24,1,listOf(24,24))
        val r=f.repo.round(f.match.matchId,1)!!
        assertEquals(FinishType.BLOCKED,r.finishType);assertEquals(1,r.starterSeat);assertEquals(1,r.winnerSeat);assertEquals(24,r.scoreAwarded)
        assertEquals(listOf(0,24),f.match.score)
    }
    @Test fun `finish atomic history and alias snapshots`() {
        val f=MatchFixture();f.start();f.finishRound();f.finish()
        assertEquals(MatchStatus.FINISHED,f.repo.read(f.match.matchId)!!.status)
        val win=f.repo.history("m4-test-0",20,null).items.single();val loss=f.repo.history("m4-test-1",20,null).items.single()
        assertEquals(HistoryResult.WIN,win.result);assertEquals(HistoryResult.LOSS,loss.result)
        assertEquals("Alias 1",win.opponents.single().displayNameSnapshot);assertEquals(listOf(150,0),win.score)
        assertEquals(f.match.finishedAt,win.finishedAt);assertTrue(win.validationData)
        assertFails {f.emit(PlayerPassed(0),0)}
    }
    @Test fun `history cursor is stable bounded and belongs to authenticated uid`() {
        val repo=InMemoryMatchRepository()
        repeat(3){val f=MatchFixture(repo=repo);f.start();f.finishRound();f.finish()}
        val first=repo.history("m4-test-0",2,null);val second=repo.history("m4-test-0",2,first.nextCursor)
        assertEquals(2,first.items.size);assertEquals(1,second.items.size);assertNull(second.nextCursor)
        assertEquals(3,(first.items+second.items).map {it.matchId}.distinct().size)
        val controller=MatchHistoryController(repo)
        assertTrue((controller.history(FirebaseIdentity("intruder",true),20,null).body as HistoryPage).items.isEmpty())
        assertEquals(400,controller.history(FirebaseIdentity("m4-test-0",true),101,null).statusCode.value())
        assertEquals(400,controller.history(FirebaseIdentity("m4-test-0",true),20,"../bad").statusCode.value())
    }
    @Test fun `cancelled match writes history without winner or economy`() {
        val f=MatchFixture();f.emit(MatchFinished(MatchResult(null,MatchFinishReason.CANCELLED,f.match.score)))
        assertEquals(MatchStatus.CANCELLED,f.match.status)
        assertEquals(HistoryResult.CANCELLED,f.repo.history("m4-test-0",20,null).items.single().result)
    }
    @Test fun `private payload cannot enter public events or snapshot`() {
        val f=MatchFixture();f.start();val events=f.events();val views=MatchViewService(f.clock)
        val public=views.publicEvents(f.match,events)
        assertTrue(public.none {it.payload is HandDealt||it.visibility!=EventVisibility.PUBLIC})
        assertTrue(public.all {it.causedByCommandId==null})
        val json=GameCatalogCodec.json(MatchReplayReducer.reconstruct(f.match,events).publicState)
        assertFalse(json.contains("sideA"));assertFalse(json.contains("hand"));assertFalse(json.contains("playerUid"))
        assertEquals(10,views.player(f.match,events,"m4-test-0").hand.size)
        assertFails {views.player(f.match,events,"intruder")}
        assertFails {f.emit(HandDealt(0,f.hand(0)),target=1)}
        assertFails {f.emit(HandDealt(0,f.hand(0)),target=0)}
    }
    @Test fun `cutoff 910 excludes 911 and shares hand and board timeline`() {
        val f=MatchFixture();f.start()
        f.clock.now=Instant.ofEpochSecond(908);f.emit(TilePlayed(0,f.hand(0)[0],ChainEnd.RIGHT),0) // 909
        val at909=f.match.lastSequence
        f.clock.now=Instant.ofEpochSecond(910);f.emit(TilePlayed(0,f.hand(0)[1],ChainEnd.LEFT),0) // 911
        f.clock.now=Instant.ofEpochSecond(1000)
        val view=MatchViewService(f.clock).spectator(f.match,f.events(),SpectatorViewMode.FOLLOW_PLAYER,0)
        assertEquals(Instant.ofEpochSecond(910),view.cutoffTime);assertEquals(at909,view.sequence)
        assertEquals(1,view.publicState.board.size);assertEquals(9,view.selectedPlayerHand!!.hand.size)
        assertEquals(view.publicState.lastSequence,view.selectedPlayerHand.lastSequence)
        assertTrue(f.hand(0)[1] in view.selectedPlayerHand.hand)
    }
    @Test fun `cutoff includes exactly boundary and no finished status leak`() {
        val f=MatchFixture();f.start();f.clock.now=Instant.ofEpochSecond(909);f.emit(PlayerPassed(0),0)
        val boundary=f.match.lastSequence;f.finishRound();f.finish();f.clock.now=Instant.ofEpochSecond(1000)
        val view=MatchViewService(f.clock).spectator(f.match,f.events(),SpectatorViewMode.TABLE_ONLY)
        assertEquals(boundary,view.sequence);assertEquals(MatchStatus.IN_PROGRESS,view.publicState.status)
        assertEquals(listOf(0,0),view.publicState.scores);assertNull(view.selectedPlayerHand)
    }
    @ParameterizedTest @ValueSource(longs=[0,30,59,-1])
    fun `minimum hand delay rejects unsafe values`(delay: Long) { assertFails {SpectatorPolicyRules().validate(SpectatorPolicy(delaySeconds=delay))} }
    @ParameterizedTest @ValueSource(longs=[60,90,180])
    fun `safe hand delay accepted`(delay: Long) {SpectatorPolicyRules().validate(SpectatorPolicy(delaySeconds=delay))}
    @Test fun `server minimum cannot be disabled and private spectators denied`() {
        assertFails {SpectatorPolicyRules(0)}
        val f=MatchFixture();f.start();val views=MatchViewService(f.clock)
        assertEquals(90,f.match.spectatorPolicy.delaySeconds)
        assertFails {views.spectator(f.match.copy(visibility=MatchVisibility.PRIVATE),f.events(),SpectatorViewMode.TABLE_ONLY)}
        assertFails {views.spectator(f.match,f.events(),SpectatorViewMode.FOLLOW_PLAYER,null)}
        assertFails {views.spectator(f.match,f.events(),SpectatorViewMode.FOLLOW_PLAYER,9)}
        assertFalse(SpectatorPolicyRules().defaults(MatchVisibility.PRIVATE).enabled)
    }
    @Test fun `reducer reconstructs board turn counts score deterministically`() {
        val f=MatchFixture();f.start();f.emit(TilePlayed(0,f.hand(0)[0],ChainEnd.RIGHT),0);f.emit(TurnChanged(1));f.emit(TurnStarted(1));f.emit(PlayerPassed(1),1)
        f.finishRound(FinishType.BLOCKED,27,0,listOf(18,27))
        val a=MatchReplayReducer.reconstruct(f.match,f.events());val b=MatchReplayReducer.reconstruct(f.match,f.events())
        assertEquals(a,b);assertEquals(1,a.publicState.board.size);assertEquals(listOf(9,10),a.publicState.tilesRemainingPerSeat)
        assertEquals(listOf(27,0),a.publicState.scores);assertEquals(2,a.publicState.currentTurn)
        assertFails {MatchReplayReducer.reconstruct(f.match,f.events().drop(1))}
        assertFails {MatchReplayReducer.reconstruct(f.match,f.events().reversed())}
    }
    @Test fun `future schemas round trip but never execute`() {
        val f=MatchFixture();f.start()
        val payloads=listOf(TurnTimeout(0),AutoPlayed(0,null,null,AutoPlayReason.DISCONNECTED),PlayerDisconnected(0),PlayerReconnected(0),PlayerAbandoned(0))
        payloads.forEach {p->val e=MatchEvent("000000000001","future",1,1,1,1,p.type(),0,EventVisibility.PUBLIC,null,p,f.clock.instant(),"command")
            assertEquals(e,MatchCodec.copy(e,MatchEvent::class.java));assertFails {f.emit(p,0)}}
    }
    @Test fun `private starter audit serializes with target and excludes public`() {
        val f=MatchFixture();f.start()
        val result=StarterSelectionResult(StarterMethod.HIGH_TILE_SELECTION,0,1,listOf(DominoPips(9,9),DominoPips(2,3)),0,null,null)
        f.emit(PrivateStarterSelection(result),target=0)
        assertEquals(result,MatchViewService(f.clock).player(f.match,f.events(),"m4-test-0").setup)
        assertFalse(MatchViewService(f.clock).publicEvents(f.match,f.events()).any {it.payload is PrivateStarterSelection})
    }
    @Test fun `document identifiers cannot traverse and event IDs sort`() {
        listOf("", ".", "..", "a/b").forEach {assertFails {MatchIds.document(it)}}
        assertEquals(listOf(1L,2L,10L,100L).map(MatchIds::event),listOf(100L,2L,10L,1L).map(MatchIds::event).sorted())
    }
}
