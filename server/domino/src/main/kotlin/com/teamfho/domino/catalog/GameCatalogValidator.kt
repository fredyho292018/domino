package com.teamfho.domino.catalog

import java.time.Instant
import java.util.Collections

object GameCatalogValidator {
    val duelCapabilities = RuleCapability.entries.map { it.name }.toSet()
    private fun id(value: String) = require(value.matches(Regex("[a-z0-9][a-z0-9-]{0,79}")))
    private fun metadata(created: String, updated: String = created) { Instant.parse(created); Instant.parse(updated) }
    fun resolve(p: GameCatalogPublication): GameCatalogSnapshot {
        require(p.catalogSchemaVersion == 1 && p.catalogVersion > 0)
        metadata(p.createdAt)
        require(p.modes.isNotEmpty() && p.modes.size <= 32)
        require(p.modes.map { it.id }.distinct().size == p.modes.size)
        require(p.modes.map { it.key }.distinct().size == p.modes.size)
        require(p.ruleSets.map { it.id }.distinct().size == p.ruleSets.size)
        require(p.versions.map { it.id to it.version }.distinct().size == p.versions.size)
        require(p.bindings.map { it.id }.distinct().size == p.bindings.size)
        p.ruleSets.forEach { id(it.id); require(it.key.isNotBlank() && it.nameKey.isNotBlank() && it.descriptionKey.isNotBlank()); metadata(it.createdAt,it.updatedAt) }
        p.modes.forEach { m ->
            id(m.id); id(m.defaultRuleSetId); metadata(m.createdAt,m.updatedAt)
            require(m.key.isNotBlank() && m.nameKey.isNotBlank() && m.descriptionKey.isNotBlank() && m.iconKey.isNotBlank())
            require(m.schemaVersion == 1 && m.topologyVersion > 0)
            if(m.teamMode == TeamMode.FIXED_TEAMS) {
                require(m.playerCount == 4 && m.teamSize == 2)
                require(m.seatTeams.size == 2 && m.seatTeams.all { it.size == m.teamSize })
                require(m.seatTeams.flatten().sorted() == (0 until m.playerCount).toList())
            } else {
                require(m.playerCount == 2 && m.teamSize == null && m.seatTeams.isEmpty())
                require(m.minHumans == 2 && m.maxHumans == 2 && !m.botsAllowed)
                require(m.onlinePolicy == DisconnectPolicy(180, true, true))
            }
            require(m.minHumans in 1..m.playerCount && m.maxHumans in m.minHumans..m.playerCount)
            require(m.botsAllowed || m.minHumans == m.playerCount)
            require(m.executionModesSupported == listOf(ExecutionMode.LOCAL))
            require(p.ruleSets.any { it.id == m.defaultRuleSetId })
        }
        p.versions.forEach { r ->
            id(r.id); metadata(r.createdAt)
            require(p.ruleSets.any { it.id == r.id })
            require(r.version > 0 && r.ruleSchemaVersion == 1 && r.requiredCapabilities.all { it in duelCapabilities })
            require(r.maxPip in 1..9 && r.tilesPerPlayer in 1..10 && r.targetScore > 0)
            require(r.finishScoring.bonus in 0..1000000 && r.blockedScoring.bonus in 0..1000000)
            require(r.tiePolicy.award == 0 && r.tiePolicy.nextRoundMultiplier == 2)
            require(r.contentHash == GameCatalogCodec.hash(r))
        }
        p.bindings.forEach { b ->
            require(b.id.matches(Regex("[a-z0-9_-]{1,160}"))); metadata(b.createdAt)
            val m = p.modes.single { it.id == b.modeId && it.topologyVersion == b.topologyVersion }
            val r = p.versions.single { it.id == b.ruleSetId && it.version == b.ruleSetVersion }
            val seats = (0 until m.playerCount).toList()
            require(r.turnOrder.sorted() == seats && r.dealPolicy.seatOrder.sorted() == seats)
            if(m.teamMode == TeamMode.NONE) {
                require(r.requiredCapabilities.toSet() == duelCapabilities)
                require(r.firstRoundStarting.mode == StartingMode.RANDOM_START_METHOD && r.firstRoundStarting.seat == -1)
                val methods = requireNotNull(r.firstRoundStarting.methods)
                require(methods.isNotEmpty() && methods.distinct().size == methods.size)
                require(r.followingRoundStarting.mode == StartingMode.PREVIOUS_ROUND_WINNER && r.followingRoundStarting.seat == -1)
                require(r.blockedPolicy.opposingTeamsMinimumTie == OpposingTie.ROUND_STARTER_WINS)
                require(r.turnPolicy == TurnPolicy(60, true, AutoPlayPolicy.FIRST_VALID_MOVE))
                require(r.capicuaPolicy == CapicuaPolicy(CapicuaDetection.LAST_TILE_PLAYABLE_ON_BOTH_ENDS, 2, false))
            } else {
                require(r.firstRoundStarting.mode == StartingMode.FIXED_SEAT && r.followingRoundStarting.mode == StartingMode.FIXED_SEAT)
                require(r.firstRoundStarting.seat in seats && r.followingRoundStarting.seat in seats)
            }
            require(m.playerCount.toLong() * r.tilesPerPlayer <= (r.maxPip + 1L) * (r.maxPip + 2L) / 2)
            if (b.active) require(p.ruleSets.single { it.id == b.ruleSetId }.active)
        }
        val modes = p.modes.mapNotNull { m ->
            val defaults = p.bindings.filter { it.modeId == m.id && it.active && it.isDefault }
            require(defaults.size <= 1)
            if (!m.active) return@mapNotNull null
            val b = defaults.single()
            require(b.ruleSetId == m.defaultRuleSetId)
            val r = p.versions.single { it.id == b.ruleSetId && it.version == b.ruleSetVersion }
            ResolvedGameMode(m.id,m.key,m.topologyVersion,m.nameKey,m.descriptionKey,m.active,m.sortOrder,m.iconKey,m.availability,
                m.playerCount,m.teamMode,m.teamSize,Collections.unmodifiableList(m.seatTeams.map { Collections.unmodifiableList(it.toList()) }),
                m.minHumans,m.maxHumans,m.botsAllowed,Collections.unmodifiableList(m.executionModesSupported.toList()),m.defaultRuleSetId,
                r.copy(requiredCapabilities=Collections.unmodifiableList(r.requiredCapabilities.toList()), turnOrder=Collections.unmodifiableList(r.turnOrder.toList()),
                    dealPolicy=r.dealPolicy.copy(seatOrder=Collections.unmodifiableList(r.dealPolicy.seatOrder.toList())),
                    firstRoundStarting=r.firstRoundStarting.copy(methods=r.firstRoundStarting.methods?.let { Collections.unmodifiableList(it.toList()) })), m.onlinePolicy)
        }.sortedWith(compareBy({it.sortOrder},{it.id}))
        return GameCatalogSnapshot(1,p.catalogVersion,Collections.unmodifiableList(modes))
    }
}
