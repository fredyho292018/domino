package com.teamfho.domino.online

import com.teamfho.domino.match.*
import com.teamfho.domino.catalog.StarterMethod

enum class OnlinePhase { WAITING_FOR_PLAYER, STARTER_SELECTION, PLAYING, ROUND_FINISHED, MATCH_FINISHED }
enum class OnlineCommandType { SELECT_STARTER_TILE, SUBMIT_EVEN_ODD_GUESS, PLAY_TILE, PASS, NEXT_ROUND }
enum class OnlineError { MATCH_NOT_FOUND, NOT_PARTICIPANT, MATCH_FULL, SAME_PLAYER, MATCH_NOT_ACTIVE,
    NOT_YOUR_TURN, TILE_NOT_IN_HAND, ILLEGAL_MOVE, PASS_NOT_ALLOWED, INVALID_COMMAND,
    INVALID_STARTER_ACTION, STARTER_PHASE_REQUIRED, COMMAND_ID_CONFLICT, STALE_COMMAND, STORAGE_UNAVAILABLE, MODE_UNAVAILABLE,
    TURN_EXPIRED, TIMEOUT_NOT_DUE, PLAYER_ABANDONED }
class OnlineFailure(val code: OnlineError): RuntimeException(code.name)
fun checkOnline(value: Boolean, code: OnlineError) { if(!value)throw OnlineFailure(code) }

// No uid, seat, score, rules or sequence authority in the command DTO.
data class OnlineCommand(val protocolVersion: Int, val commandId: String, val matchId: String,
    val type: OnlineCommandType, val tile: DominoPips? = null, val chainEnd: ChainEnd? = null,
    val candidate: Int? = null, val even: Boolean? = null)
data class OnlineStarter(val method: StarterMethod, val candidates: List<DominoPips>,
    val selections: Map<String,Int> = emptyMap(), val guessingSeat: Int, val attempt: Int = 1)
// Trusted server state only; stored under the existing Match, never serialized by a controller.
data class OnlineState(val match: Match, val phase: OnlinePhase, val starter: OnlineStarter? = null,
    val hands: Map<String,List<DominoPips>> = emptyMap(), val reserve: List<DominoPips> = emptyList(),
    val board: List<BoardPlacement> = emptyList(), val consecutivePasses: Int = 0, val round: MatchRound? = null,
    val turnStartedAt: java.time.Instant? = null, val turnDeadlineAt: java.time.Instant? = null,
    val nextRoundMultiplier: Int = 1)
data class OnlineWrite(val state: OnlineState, val events: List<MatchEvent>, val rounds: List<MatchRound> = emptyList(),
    val histories: Map<String,PlayerMatchHistory> = emptyMap())
data class OnlineReceipt(val fingerprint: String, val firstSequence: Long, val resultingSequence: Long)
data class OnlineCommit(val write: OnlineWrite?, val receipt: OnlineReceipt)
data class OnlineStarterView(val method: StarterMethod, val guessingSeat: Int, val attempt: Int,
    val selectedSeats: List<Int>, val availableCandidates: List<Int>)
data class OnlineSnapshot(val publicState: PublicMatchSnapshot, val privateState: PrivatePlayerSnapshot,
    val phase: OnlinePhase, val starter: OnlineStarterView?, val roundResult: RoundFinished?, val lastSequence: Long,
    val ruleSnapshot: MatchRuleSnapshot, val turnStartedAt: java.time.Instant? = null,
    val turnDeadlineAt: java.time.Instant? = null, val serverNow: java.time.Instant? = null,
    val roundMultiplier: Int = 1)
// Redacted sequence cursors prevent private events from producing false gaps for the other seat.
data class OnlineEventView(val sequence: Long, val type: String, val event: MatchEvent?)
data class OnlineEventPage(val matchId: String, val events: List<OnlineEventView>, val throughSequence: Long)
