package com.teamfho.domino.catalog

enum class RuleCapability { DUEL_TOPOLOGY, RANDOM_START_METHOD, HIGH_TILE_SELECTION, EVEN_ODD_GUESS, PREVIOUS_ROUND_WINNER_START, BLOCKED_TIE_STARTER_WINS, CAPICUA_SCORING_V1 }

enum class TeamMode { FIXED_TEAMS, NONE }
enum class ExecutionMode { LOCAL }
enum class Availability { ALL }
enum class TileSet { DOUBLE_N }
enum class DealMethod { ROUND_ROBIN }
enum class ReservePolicy { RESERVE }
enum class DrawPolicy { NONE }
enum class StartingMode { FIXED_SEAT, RANDOM_START_METHOD, PREVIOUS_ROUND_WINNER }
enum class StarterMethod { HIGH_TILE_SELECTION, EVEN_ODD_GUESS }
enum class AutoPlayPolicy { FIRST_VALID_MOVE }
enum class CapicuaDetection { LAST_TILE_PLAYABLE_ON_BOTH_ENDS }
enum class OpeningPolicy { ANY }
enum class PassPolicy { NO_LEGAL_MOVE }
enum class BlockDetection { ALL_PLAYERS_PASS_CONSECUTIVELY }
enum class BlockWinner { LOWEST_INDIVIDUAL_PIPS }
enum class OpposingTie { ROUND_TIE, ROUND_STARTER_WINS }
enum class SameTeamTie { TEAM_WINS }
enum class Representative { FIRST_SEAT_IN_WINNING_MINIMUM }
enum class PointsSource { OPPONENTS_ONLY }
enum class RepeatedTie { KEEP_MULTIPLIER }
enum class MultiplierScope { TOTAL_INCLUDING_BONUS }
enum class AfterAward { RESET_TO_ONE }

data class GameMode(val id: String, val key: String, val nameKey: String, val descriptionKey: String,
    val active: Boolean, val sortOrder: Int, val iconKey: String, val playerCount: Int,
    val teamMode: TeamMode, val teamSize: Int?, val seatTeams: List<List<Int>>,
    val minHumans: Int, val maxHumans: Int, val botsAllowed: Boolean,
    val executionModesSupported: List<ExecutionMode>, val defaultRuleSetId: String,
    val availability: Availability, val topologyVersion: Int, val schemaVersion: Int,
    val createdAt: String, val updatedAt: String,
    @get:com.fasterxml.jackson.annotation.JsonInclude(com.fasterxml.jackson.annotation.JsonInclude.Include.NON_NULL) val onlinePolicy: DisconnectPolicy? = null)
data class RuleSet(val id: String, val key: String, val nameKey: String, val descriptionKey: String,
    val active: Boolean, val createdAt: String, val updatedAt: String)
data class DealPolicy(val method: DealMethod, val seatOrder: List<Int>, val undealtTiles: ReservePolicy)
data class StartingPolicy(val mode: StartingMode, val seat: Int,
    @get:com.fasterxml.jackson.annotation.JsonInclude(com.fasterxml.jackson.annotation.JsonInclude.Include.NON_NULL) val methods: List<StarterMethod>? = null)
data class TurnPolicy(val timeLimitSeconds: Int, val autoPlayOnTimeout: Boolean, val autoPlayPolicy: AutoPlayPolicy)
data class DisconnectPolicy(val reconnectWindowSeconds: Int, val turnClockContinuesWhileDisconnected: Boolean, val autoPlayWhileDisconnected: Boolean)
data class CapicuaPolicy(val detection: CapicuaDetection, val pipMultiplier: Int, val multiplyBonus: Boolean)
data class BlockedPolicy(val detection: BlockDetection, val winner: BlockWinner,
    val opposingTeamsMinimumTie: OpposingTie, val sameTeamMinimumTie: SameTeamTie,
    val winningRepresentative: Representative)
data class ScoringPolicy(val source: PointsSource, val bonus: Int)
data class TiePolicy(val award: Int, val nextRoundMultiplier: Int, val repeatedTie: RepeatedTie,
    val appliesTo: MultiplierScope, val afterAward: AfterAward)
data class RuleSetVersion(val id: String, val version: Int, val ruleSchemaVersion: Int,
    val requiredCapabilities: List<String>, val tileSet: TileSet, val maxPip: Int, val tilesPerPlayer: Int,
    val dealPolicy: DealPolicy, val drawPolicy: DrawPolicy, val reservePolicy: ReservePolicy,
    val targetScore: Int, val firstRoundStarting: StartingPolicy, val followingRoundStarting: StartingPolicy,
    val openingTilePolicy: OpeningPolicy, val turnOrder: List<Int>, val passPolicy: PassPolicy,
    val blockedPolicy: BlockedPolicy, val finishScoring: ScoringPolicy, val blockedScoring: ScoringPolicy,
    val tiePolicy: TiePolicy, val createdAt: String, val contentHash: String,
    @get:com.fasterxml.jackson.annotation.JsonInclude(com.fasterxml.jackson.annotation.JsonInclude.Include.NON_NULL) val turnPolicy: TurnPolicy? = null,
    @get:com.fasterxml.jackson.annotation.JsonInclude(com.fasterxml.jackson.annotation.JsonInclude.Include.NON_NULL) val capicuaPolicy: CapicuaPolicy? = null)
data class GameModeRuleBinding(val id: String, val modeId: String, val topologyVersion: Int,
    val ruleSetId: String, val ruleSetVersion: Int, val active: Boolean, val isDefault: Boolean, val createdAt: String)
// A publication embeds its complete graph. Mutable identity documents cannot change a published catalog.
data class GameCatalogPublication(val catalogSchemaVersion: Int, val catalogVersion: Int,
    val modes: List<GameMode>, val ruleSets: List<RuleSet>, val versions: List<RuleSetVersion>,
    val bindings: List<GameModeRuleBinding>, val createdAt: String)
data class ResolvedGameMode(val id: String, val key: String, val topologyVersion: Int,
    val nameKey: String, val descriptionKey: String, val active: Boolean, val sortOrder: Int,
    val iconKey: String, val availability: Availability, val playerCount: Int, val teamMode: TeamMode,
    val teamSize: Int?, val seatTeams: List<List<Int>>, val minHumans: Int, val maxHumans: Int,
    val botsAllowed: Boolean, val executionModesSupported: List<ExecutionMode>, val defaultRuleSetId: String,
    val ruleSet: RuleSetVersion,
    @get:com.fasterxml.jackson.annotation.JsonInclude(com.fasterxml.jackson.annotation.JsonInclude.Include.NON_NULL) val onlinePolicy: DisconnectPolicy? = null)
data class GameCatalogSnapshot(val catalogSchemaVersion: Int, val catalogVersion: Int, val modes: List<ResolvedGameMode>)
