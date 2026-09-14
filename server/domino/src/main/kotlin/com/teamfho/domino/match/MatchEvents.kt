package com.teamfho.domino.match

import com.fasterxml.jackson.annotation.JsonSubTypes
import com.fasterxml.jackson.annotation.JsonTypeInfo
import java.time.Instant

enum class MatchEventType { STARTER_PROGRESS, MATCH_STARTED, ROUND_STARTED, HAND_DEALT, STARTER_SELECTION_PRIVATE,
    TURN_STARTED, TILE_PLAYED, TURN_CHANGED, PLAYER_PASSED, ROUND_FINISHED, MATCH_FINISHED,
    TURN_TIMEOUT, AUTO_PLAYED, PLAYER_DISCONNECTED, PLAYER_RECONNECTED, PLAYER_ABANDONED }

@JsonTypeInfo(use=JsonTypeInfo.Id.NAME, property="kind")
@JsonSubTypes(
    JsonSubTypes.Type(StarterProgress::class,name="STARTER_PROGRESS"),
    JsonSubTypes.Type(MatchStarted::class,name="MATCH_STARTED"), JsonSubTypes.Type(RoundStarted::class,name="ROUND_STARTED"),
    JsonSubTypes.Type(HandDealt::class,name="HAND_DEALT"), JsonSubTypes.Type(PrivateStarterSelection::class,name="STARTER_SELECTION_PRIVATE"),
    JsonSubTypes.Type(TurnStarted::class,name="TURN_STARTED"), JsonSubTypes.Type(TilePlayed::class,name="TILE_PLAYED"),
    JsonSubTypes.Type(TurnChanged::class,name="TURN_CHANGED"), JsonSubTypes.Type(PlayerPassed::class,name="PLAYER_PASSED"),
    JsonSubTypes.Type(RoundFinished::class,name="ROUND_FINISHED"), JsonSubTypes.Type(MatchFinished::class,name="MATCH_FINISHED"),
    JsonSubTypes.Type(TurnTimeout::class,name="TURN_TIMEOUT"), JsonSubTypes.Type(AutoPlayed::class,name="AUTO_PLAYED"),
    JsonSubTypes.Type(PlayerDisconnected::class,name="PLAYER_DISCONNECTED"), JsonSubTypes.Type(PlayerReconnected::class,name="PLAYER_RECONNECTED"),
    JsonSubTypes.Type(PlayerAbandoned::class,name="PLAYER_ABANDONED"))
sealed interface MatchPayload
data class StarterProgress(val method: com.teamfho.domino.catalog.StarterMethod, val attempt: Int,
    val selectedSeats: List<Int>, val guessingSeat: Int, val resolvedSeat: Int? = null): MatchPayload
data class MatchStarted(val initialScores: List<Int>): MatchPayload
data class RoundStarted(val starterSeat: Int, val tilesRemainingPerSeat: List<Int>): MatchPayload
data class HandDealt(val seat: Int, val tiles: List<DominoPips>): MatchPayload
data class PrivateStarterSelection(val result: StarterSelectionResult): MatchPayload
data class TurnStarted(val seat: Int, val turnDeadline: Instant? = null): MatchPayload
data class TilePlayed(val seat: Int, val tile: DominoPips, val chainEnd: ChainEnd): MatchPayload
data class TurnChanged(val seat: Int): MatchPayload
data class PlayerPassed(val seat: Int): MatchPayload
data class RoundFinished(val finishType: FinishType, val winnerSeat: Int?, val scoreRecipient: ScoreRecipient?,
    val scoreAwarded: Int, val roundStarter: Int, val remainingPips: List<Int>, val scoresAfter: List<Int>): MatchPayload
data class MatchFinished(val result: MatchResult): MatchPayload
// Schema only. The M4 persistence service explicitly rejects runtime generation of these payloads.
data class TurnTimeout(val seat: Int): MatchPayload
data class AutoPlayed(val seat: Int, val tile: DominoPips?, val chainEnd: ChainEnd?, val reason: AutoPlayReason): MatchPayload
data class PlayerDisconnected(val seat: Int): MatchPayload
data class PlayerReconnected(val seat: Int): MatchPayload
data class PlayerAbandoned(val seat: Int): MatchPayload

fun MatchPayload.type(): MatchEventType = when(this) {
    is StarterProgress->MatchEventType.STARTER_PROGRESS
    is MatchStarted->MatchEventType.MATCH_STARTED; is RoundStarted->MatchEventType.ROUND_STARTED
    is HandDealt->MatchEventType.HAND_DEALT; is PrivateStarterSelection->MatchEventType.STARTER_SELECTION_PRIVATE
    is TurnStarted->MatchEventType.TURN_STARTED; is TilePlayed->MatchEventType.TILE_PLAYED
    is TurnChanged->MatchEventType.TURN_CHANGED; is PlayerPassed->MatchEventType.PLAYER_PASSED
    is RoundFinished->MatchEventType.ROUND_FINISHED; is MatchFinished->MatchEventType.MATCH_FINISHED
    is TurnTimeout->MatchEventType.TURN_TIMEOUT; is AutoPlayed->MatchEventType.AUTO_PLAYED
    is PlayerDisconnected->MatchEventType.PLAYER_DISCONNECTED; is PlayerReconnected->MatchEventType.PLAYER_RECONNECTED
    is PlayerAbandoned->MatchEventType.PLAYER_ABANDONED
}
data class MatchEvent(val eventId: String, val matchId: String, val sequence: Long, val eventSchemaVersion: Int,
    val roundNumber: Int, val turnNumber: Int, val type: MatchEventType, val actorSeat: Int?,
    val visibility: EventVisibility, val targetSeat: Int?, val payload: MatchPayload, val createdAt: Instant,
    val causedByCommandId: String?)
data class ConfirmedTransition(val payload: MatchPayload, val actorSeat: Int? = null, val targetSeat: Int? = null)
