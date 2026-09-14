package com.teamfho.domino.match

import com.teamfho.domino.catalog.*
import java.security.MessageDigest
import java.time.Instant

enum class MatchStatus { CREATED, STARTING, IN_PROGRESS, FINISHED, CANCELLED }
enum class MatchExecutionMode { LOCAL, ONLINE }
enum class ControlType { LOCAL_HUMAN, REMOTE_HUMAN, BOT }
enum class ConnectionState { CONNECTED, DISCONNECTED, RECONNECTED, ABANDONED }
enum class MatchVisibility { PUBLIC, PRIVATE, FRIENDS_ONLY }
enum class EventVisibility { PUBLIC, PLAYER_PRIVATE, SYSTEM_PRIVATE }
enum class RoundStatus { IN_PROGRESS, FINISHED }
enum class FinishType { NORMAL, CAPICUA, BLOCKED }
enum class MatchFinishReason { TARGET_REACHED, CANCELLED }
enum class ScoreOwnerType { PLAYER, TEAM }
enum class HistoryResult { WIN, LOSS, DRAW, CANCELLED }
enum class HandView { NONE, SELECTED_PLAYER_DELAYED }
enum class SpectatorViewMode { TABLE_ONLY, FOLLOW_PLAYER }
enum class ChainEnd { LEFT, RIGHT }
enum class AutoPlayReason { TURN_TIMEOUT, DISCONNECTED }

data class ScoreRecipient(val type: ScoreOwnerType, val index: Int)
data class MatchParticipant(val seatIndex: Int, val playerUid: String?, val displayNameSnapshot: String,
    val teamId: Int?, val controlType: ControlType, val connectionState: ConnectionState, val joinedAt: Instant,
    val disconnectedAt: Instant? = null, val reconnectDeadlineAt: Instant? = null, val abandonedAt: Instant? = null)
data class SpectatorPolicy(val enabled: Boolean = true, val visibility: MatchVisibility = MatchVisibility.PUBLIC,
    val delaySeconds: Long = 90, val handView: HandView = HandView.SELECTED_PLAYER_DELAYED)

// Canonical immutable JSON is the frozen value; decoded typed views are detached copies.
// Historical rules never resolve through the current catalog again.
data class MatchRuleSnapshot(val catalogVersion: Int, val gameModeId: String, val gameModeKey: String,
    val topologyVersion: Int, val ruleSetId: String, val ruleSetVersion: Int, val ruleSchemaVersion: Int,
    val engineSemanticsVersion: String, val effectiveModeJson: String, val contentHash: String) {
    fun mode(): ResolvedGameMode = GameCatalogCodec.mapper.readValue(effectiveModeJson, ResolvedGameMode::class.java)
    fun verify() { require(contentHash == digest(effectiveModeJson)); val m=mode()
        require(m.id==gameModeId && m.key==gameModeKey && m.topologyVersion==topologyVersion)
        require(m.ruleSet.id==ruleSetId && m.ruleSet.version==ruleSetVersion && m.ruleSet.ruleSchemaVersion==ruleSchemaVersion)
        require(engineSemanticsVersion=="domino-m3-v1" && m.ruleSet.contentHash==GameCatalogCodec.hash(m.ruleSet))
    }
    companion object {
        fun freeze(catalog: GameCatalogSnapshot, mode: ResolvedGameMode): MatchRuleSnapshot {
            val json=GameCatalogCodec.semantic(mode)
            return MatchRuleSnapshot(catalog.catalogVersion,mode.id,mode.key,mode.topologyVersion,mode.ruleSet.id,
                mode.ruleSet.version,mode.ruleSet.ruleSchemaVersion,"domino-m3-v1",json,digest(json))
        }
        private fun digest(s: String)=MessageDigest.getInstance("SHA-256").digest(s.toByteArray(Charsets.UTF_8)).joinToString(""){"%02x".format(it)}
    }
}
data class MatchResult(val winner: ScoreRecipient?, val finishReason: MatchFinishReason, val scores: List<Int>)
data class Match(val matchId: String, val status: MatchStatus, val modeKey: String, val executionMode: MatchExecutionMode,
    val catalogVersion: Int, val topologyVersion: Int, val ruleSetId: String, val ruleSetVersion: Int,
    val ruleSchemaVersion: Int, val ruleSnapshot: MatchRuleSnapshot, val participants: List<MatchParticipant>,
    val currentRoundNumber: Int, val currentTurnNumber: Int, val currentSeat: Int?, val score: List<Int>,
    val startedAt: Instant?, val finishedAt: Instant?, val result: MatchResult?, val lastSequence: Long,
    val visibility: MatchVisibility, val spectatorPolicy: SpectatorPolicy, val createdAt: Instant, val updatedAt: Instant,
    val validationData: Boolean = false)
data class StarterSelectionResult(val starterMethod: StarterMethod, val starterSeat: Int, val attempt: Int,
    val chosenTiles: List<DominoPips>, val selectingSeat: Int?, val guessingSeat: Int?, val guessedEven: Boolean?)
data class MatchRound(val roundNumber: Int, val starterSeat: Int, val startedAt: Instant, val finishedAt: Instant?,
    val status: RoundStatus, val startSequence: Long, val endSequence: Long?, val winnerSeat: Int?,
    val winnerTeam: Int?, val finishType: FinishType?, val scoreAwarded: Int, val scoreRecipient: ScoreRecipient?,
    val remainingPips: List<Int> = emptyList(), val dealtSeats: List<Int> = emptyList())
data class OpponentSummary(val seat: Int, val displayNameSnapshot: String, val teamId: Int?)
data class PlayerMatchHistory(val matchId: String, val modeKey: String, val ruleSetId: String, val ruleSetVersion: Int,
    val result: HistoryResult, val score: List<Int>, val opponents: List<OpponentSummary>, val startedAt: Instant?,
    val finishedAt: Instant, val finishReason: MatchFinishReason, val validationData: Boolean = false)
data class HistoryPage(val items: List<PlayerMatchHistory>, val nextCursor: String?)
data class DominoPips(val sideA: Int, val sideB: Int) { init { require(sideA in 0..9 && sideB in 0..9) } }
data class BoardPlacement(val tile: DominoPips, val chainEnd: ChainEnd, val actorSeat: Int)

object MatchIds {
    fun document(id: String): String { require(id.isNotBlank() && id.length<=128 && '/' !in id && id !in setOf(".","..")); return id }
    fun event(sequence: Long): String { require(sequence in 1..999999999999L); return "%012d".format(java.util.Locale.ROOT, sequence) }
}
