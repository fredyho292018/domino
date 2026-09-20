package com.teamfho.domino.match

import org.springframework.context.annotation.Bean
import org.springframework.context.annotation.Configuration
import org.springframework.http.ResponseEntity
import org.springframework.security.core.annotation.AuthenticationPrincipal
import org.springframework.web.bind.annotation.*
import com.teamfho.domino.security.FirebaseIdentity

enum class ReplayAvailability { AVAILABLE, ACTIVE_MATCH, LEGACY_DATA, INCOMPLETE_EVENTS, UNSUPPORTED_SCHEMA, MISSING_PRIVATE_SNAPSHOT }
class ReplayFailure(val reason: ReplayAvailability): RuntimeException(reason.name)
class ReplayForbidden: RuntimeException()
data class ReplayManifest(val replaySchemaVersion: Int, val eventSchemaVersion: Int, val matchId: String,
    val modeKey: String, val ruleSnapshot: MatchRuleSnapshot, val participants: List<PublicParticipant>,
    val selfSeat: Int, val teams: List<List<Int>>, val rounds: List<MatchRound>, val finalScore: List<Int>,
    val result: MatchResult?, val firstSequence: Long, val lastSequence: Long, val perspectives: List<Int>,
    val replayAvailable: Boolean, val replayAvailabilityReason: ReplayAvailability, val accessSession:String?=null)
data class ReplayPage(val items: List<MatchEvent>, val nextSequence: Long?, val lastSequence: Long)

/** Read-only dependency surface: replay cannot obtain a transaction or an online command handler. */
interface ReplaySource {
    fun match(id: String): Match?
    fun events(id: String, after: Long, limit: Int): List<MatchEvent>
    fun round(id: String, number: Int): MatchRound?
}

class ReplayService(private val source: ReplaySource, private val authorizeEntry:((String,String)->Unit)?=null,
    private val clock:java.time.Clock=java.time.Clock.systemUTC()) {
    private data class Session(val uid:String,val id:String,val until:java.time.Instant)
    private val sessions=object:LinkedHashMap<String,Session>() {
        override fun removeEldestEntry(eldest:MutableMap.MutableEntry<String,Session>)=size>2048
    }
    @Synchronized private fun enter(uid:String,id:String):String? {
        authorizeEntry?.invoke(uid,id)?:return null
        sessions.entries.removeIf{clock.instant()>=it.value.until}
        return java.util.UUID.randomUUID().toString().also{sessions[it]=Session(uid,id,clock.instant().plusSeconds(1800))}
    }
    @Synchronized private fun access(uid:String,id:String,session:String?) {
        val s=session?.let{sessions[it]}
        if(s==null || s.uid!=uid || s.id!=id || clock.instant()>=s.until) authorizeEntry?.invoke(uid,id)
    }
    private data class Archive(val match: Match, val rounds: List<MatchRound>, val events: List<MatchEvent>)
    // Only finished immutable archives. Bounded by both match count and per-match event count.
    private val cache=object: LinkedHashMap<String,Archive>(16,.75f,true) {
        override fun removeEldestEntry(eldest: MutableMap.MutableEntry<String,Archive>)=size>8
    }
    private fun authorized(uid: String,id: String): Match {
        MatchIds.document(id)
        val m=source.match(id)?:throw ReplayForbidden()
        if(m.participants.none{it.playerUid==uid})throw ReplayForbidden()
        if(m.status!=MatchStatus.FINISHED)throw ReplayFailure(ReplayAvailability.ACTIVE_MATCH)
        return m
    }
    @Synchronized private fun archive(m: Match): Archive {
        cache[m.matchId]?.takeIf{it.match==m}?.let{return it}
        if(m.modeKey !in setOf("DUEL_1V1","PARTNERS_2V2_ONLINE") || m.executionMode!=MatchExecutionMode.ONLINE)
            throw ReplayFailure(ReplayAvailability.LEGACY_DATA)
        try {m.ruleSnapshot.verify()}catch(_:Exception){throw ReplayFailure(ReplayAvailability.UNSUPPORTED_SCHEMA)}
        if(m.lastSequence !in 1..100_000)throw ReplayFailure(ReplayAvailability.INCOMPLETE_EVENTS)
        val events=mutableListOf<MatchEvent>()
        while(events.size.toLong()<m.lastSequence) {
            val page=try{source.events(m.matchId,events.size.toLong(),250)}catch(_:tools.jackson.core.JacksonException){throw ReplayFailure(ReplayAvailability.UNSUPPORTED_SCHEMA)}
            if(page.isEmpty())throw ReplayFailure(ReplayAvailability.INCOMPLETE_EVENTS)
            for(e in page) {
                if(e.sequence!=events.size+1L || e.sequence>m.lastSequence || e.matchId!=m.matchId)
                    throw ReplayFailure(ReplayAvailability.INCOMPLETE_EVENTS)
                if(e.eventSchemaVersion!=1||e.type!=e.payload.type())throw ReplayFailure(ReplayAvailability.UNSUPPORTED_SCHEMA)
                events+=e
            }
        }
        val rounds=(1..m.currentRoundNumber).map{source.round(m.matchId,it)?:throw ReplayFailure(ReplayAvailability.INCOMPLETE_EVENTS)}
        for(r in rounds) {
            val start=events.singleOrNull{it.roundNumber==r.roundNumber&&it.payload is RoundStarted}
            val finish=events.singleOrNull{it.roundNumber==r.roundNumber&&it.payload is RoundFinished}
            val result=finish?.payload as? RoundFinished
            if(start?.sequence!=r.startSequence||finish?.sequence!=r.endSequence||result==null||result.finishType!=r.finishType||
                result.winnerSeat!=r.winnerSeat||result.scoreAwarded!=r.scoreAwarded||result.remainingPips!=r.remainingPips)
                throw ReplayFailure(ReplayAvailability.INCOMPLETE_EVENTS)
            val deals=events.filter{it.roundNumber==r.roundNumber}.mapNotNull{it.payload as? HandDealt}
            if(deals.map{it.seat}.sorted()!=m.participants.indices.toList())throw ReplayFailure(ReplayAvailability.MISSING_PRIVATE_SNAPSHOT)
            val tiles=deals.flatMap{it.tiles}.map{minOf(it.sideA,it.sideB)*10+maxOf(it.sideA,it.sideB)}
            if(tiles.distinct().size!=tiles.size)throw ReplayFailure(ReplayAvailability.INCOMPLETE_EVENTS)
        }
        try {
            val state=MatchReplayReducer.reconstruct(m,events).publicState
            require(state.scores==m.score&&state.currentRound==m.currentRoundNumber&&state.lastSequence==m.lastSequence)
            require((events.last().payload as? MatchFinished)?.result==m.result)
        }catch(_:Exception){throw ReplayFailure(ReplayAvailability.INCOMPLETE_EVENTS)}
        return Archive(m,rounds,events).also{cache[m.matchId]=it}
    }
    fun manifest(uid: String,id: String): ReplayManifest {
        val m=authorized(uid,id)
        val session=enter(uid,id)
        var availability=ReplayAvailability.AVAILABLE
        val archive=try{archive(m)}catch(e:ReplayFailure){availability=e.reason;null}
        return ReplayManifest(1,1,id,m.modeKey,m.ruleSnapshot,m.participants.map{PublicParticipant(it.seatIndex,it.displayNameSnapshot,it.teamId,it.controlType)},
            m.participants.single{it.playerUid==uid}.seatIndex,m.ruleSnapshot.mode().seatTeams,archive?.rounds.orEmpty(),m.score,m.result,1,m.lastSequence,
            if(archive!=null)m.participants.map{it.seatIndex}else emptyList(),archive!=null,availability,session)
    }
    fun page(uid: String,id: String,after: Long,limit: Int,session:String?=null): ReplayPage {
        require(after>=0&&limit in 1..250)
        val m=authorized(uid,id);access(uid,id,session)
        val a=archive(m);require(after<=a.match.lastSequence)
        val page=a.events.drop(after.toInt()).take(limit).map{it.copy(causedByCommandId=null)}
        val next=page.lastOrNull()?.sequence?.takeIf{it<a.match.lastSequence}
        return ReplayPage(page,next,a.match.lastSequence)
    }
}

@Configuration(proxyBeanMethods=false)
class ReplayConfiguration {
    @Bean fun replaySource(store: MatchStore)=object:ReplaySource {
        override fun match(id:String)=store.read(id)
        override fun events(id:String,after:Long,limit:Int)=store.readTrustedEvents(id,after,limit)
        override fun round(id:String,number:Int)=store.round(id,number)
    }
    @Bean fun replayService(source:ReplaySource,access:com.teamfho.domino.entitlement.EntitlementHistory)=ReplayService(source,access::requireReplay)
}
@RestController
class ReplayController(private val replay:ReplayService) {
    @GetMapping("/api/v1/matches/{id}/replay")
    fun manifest(@AuthenticationPrincipal principal:FirebaseIdentity,@PathVariable id:String)=respond{replay.manifest(principal.uid,id)}
    @GetMapping("/api/v1/matches/{id}/replay/events")
    fun page(@AuthenticationPrincipal principal:FirebaseIdentity,@PathVariable id:String,
        @RequestParam(defaultValue="0") after:Long,@RequestParam(defaultValue="250") limit:Int,
        @RequestParam(required=false) session:String?=null)=respond{replay.page(principal.uid,id,after,limit,session)}
    private fun respond(action:()->Any):ResponseEntity<*> = try{ResponseEntity.ok(action())}
        catch(_:ReplayForbidden){ResponseEntity.status(403).body(mapOf("code" to "REPLAY_FORBIDDEN"))}
        catch(e:com.teamfho.domino.entitlement.EntitlementFailure){throw e}
        catch(e:ReplayFailure){ResponseEntity.status(409).body(mapOf("code" to e.reason.name))}
        catch(_:IllegalArgumentException){ResponseEntity.badRequest().body(mapOf("code" to "REPLAY_REQUEST_INVALID"))}
        catch(_:Exception){ResponseEntity.status(503).body(mapOf("code" to "REPLAY_UNAVAILABLE"))}
}
