package com.teamfho.domino.matchmaking

import com.teamfho.domino.catalog.*
import com.teamfho.domino.match.MatchRuleSnapshot

data class MatchmakingKey(val modeKey: String, val ruleSetId: String, val ruleSetVersion: Int, val executionMode: String = "ONLINE") {
    val value: String get() = "$modeKey:$ruleSetId:$ruleSetVersion:$executionMode"
    companion object {
        fun resolve(catalog: GameCatalogSnapshot, requested: String): MatchmakingKey {
            val mode = catalog.modes.singleOrNull { it.key == requested && it.active }
                ?: throw MatchmakingFailure("MODE_UNAVAILABLE")
            val compatible=mode.key=="DUEL_1V1"&&mode.playerCount==2&&mode.ruleSet.id=="double-nine-duel" ||
                mode.key==GameCatalogV4Publisher.KEY&&mode.playerCount==4&&mode.ruleSet.id=="double-nine-partners"&&mode.minHumans==4&&!mode.botsAllowed
            if (!compatible || ExecutionMode.ONLINE !in mode.executionModesSupported || mode.ruleSet.version != 1)
                throw MatchmakingFailure("MODE_UNAVAILABLE")
            return MatchmakingKey(mode.key, mode.ruleSet.id, mode.ruleSet.version)
        }
    }
}
enum class QueueState { NOT_QUEUED, QUEUED, RESERVED, MATCHED, FAILED }
data class MatchFound(val matchId: String, val seat: Int, val opponentDisplayName: String, val modeKey: String,
    val ruleSetId: String, val ruleSetVersion: Int)
data class QueueStatus(val state: QueueState, val match: MatchFound? = null, val reason: String? = null)
data class PairReservation(val id: String, val uidA: String, val uidB: String, val key: String,
    val rules: MatchRuleSnapshot, val owner: String, val waitMillis:Long=0, val additionalUids:List<String> = emptyList()) {
    val uids:List<String> get()=listOf(uidA,uidB)+additionalUids
}
class MatchmakingFailure(val code: String): RuntimeException(code)

interface MatchmakingStore {
    fun join(uid: String, key: MatchmakingKey): QueueStatus
    fun leave(uid: String): QueueStatus
    fun status(uid: String): QueueStatus
    fun reserve(key: MatchmakingKey, rules: MatchRuleSnapshot): PairReservation?
    fun recover(): PairReservation?
    fun complete(reservation: PairReservation):Boolean
    fun failed(reservation: PairReservation)
    fun cleanup(): List<String>
    fun waiting(): Long
}
