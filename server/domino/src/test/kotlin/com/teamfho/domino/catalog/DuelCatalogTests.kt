package com.teamfho.domino.catalog

import org.junit.jupiter.api.Test
import org.junit.jupiter.params.ParameterizedTest
import org.junit.jupiter.params.provider.ValueSource
import kotlin.test.*

class DuelCatalogTests {
    private val p=GameCatalogV2Publisher.canonical()
    @Test fun `duel exact topology and rules`() {
        val duel=GameCatalogValidator.resolve(p).modes.single { it.key=="DUEL_1V1" }
        assertEquals(2,duel.playerCount);assertEquals(TeamMode.NONE,duel.teamMode)
        assertNull(duel.teamSize);assertTrue(duel.seatTeams.isEmpty());assertFalse(duel.botsAllowed)
        assertEquals(2,duel.minHumans);assertEquals(2,duel.maxHumans)
        assertEquals(listOf(ExecutionMode.LOCAL),duel.executionModesSupported)
        val r=duel.ruleSet
        assertEquals(150,r.targetScore);assertEquals(9,r.maxPip);assertEquals(10,r.tilesPerPlayer)
        assertEquals(35,55-duel.playerCount*r.tilesPerPlayer)
        assertEquals(listOf(0,1),r.turnOrder);assertEquals(listOf(0,1),r.dealPolicy.seatOrder)
        assertEquals(StartingMode.RANDOM_START_METHOD,r.firstRoundStarting.mode)
        assertEquals(StarterMethod.entries.toSet(),r.firstRoundStarting.methods!!.toSet())
        assertEquals(StartingMode.PREVIOUS_ROUND_WINNER,r.followingRoundStarting.mode)
        assertEquals(OpposingTie.ROUND_STARTER_WINS,r.blockedPolicy.opposingTeamsMinimumTie)
        assertEquals(TurnPolicy(60,true,AutoPlayPolicy.FIRST_VALID_MOVE),r.turnPolicy)
        assertEquals(DisconnectPolicy(180,true,true),duel.onlinePolicy)
        assertEquals(CapicuaPolicy(CapicuaDetection.LAST_TILE_PLAYABLE_ON_BOTH_ENDS,2,false),r.capicuaPolicy)
        assertEquals(ScoringPolicy(PointsSource.OPPONENTS_ONLY,0),r.blockedScoring)
        assertEquals(ScoringPolicy(PointsSource.OPPONENTS_ONLY,10),r.finishScoring)
    }
    @Test fun `publication round trip retains policies and hashes`() {
        assertEquals(p,GameCatalogCodec.decode(GameCatalogCodec.map(p),GameCatalogPublication::class.java))
        p.versions.forEach { assertEquals(it.contentHash,GameCatalogCodec.hash(it)) }
        val storage=GameCatalogCodec.storage(GameCatalogCodec.map(p))
        assertEquals(p,GameCatalogCodec.decode(GameCatalogCodec.restored(storage)!!,GameCatalogPublication::class.java))
    }
    @Test fun `partners graph remains exactly v1`() {
        val old=GameCatalogSeed.canonical()
        assertEquals(old.modes.single(),p.modes.first());assertEquals(old.versions.single(),p.versions.first())
        assertEquals(old.bindings.single(),p.bindings.first());assertEquals(old.ruleSets.single(),p.ruleSets.first())
        assertEquals(GameCatalogValidator.resolve(old).modes.single(),GameCatalogValidator.resolve(p).modes.first())
    }
    @Test fun `resolved wire policies keep turn and reconnect clocks separate with local execution only`() {
        val duel=GameCatalogValidator.resolve(p).modes.single { it.key=="DUEL_1V1" }
        val wire=GameCatalogCodec.map(duel)
        val rules=wire["ruleSet"] as Map<*,*>
        val turn=rules["turnPolicy"] as Map<*,*>
        val disconnect=wire["onlinePolicy"] as Map<*,*>
        assertEquals(60,turn["timeLimitSeconds"])
        assertEquals(true,turn["autoPlayOnTimeout"])
        assertEquals("FIRST_VALID_MOVE",turn["autoPlayPolicy"])
        assertEquals(180,disconnect["reconnectWindowSeconds"])
        assertEquals(true,disconnect["turnClockContinuesWhileDisconnected"])
        assertEquals(true,disconnect["autoPlayWhileDisconnected"])
        assertFalse(rules.containsKey("onlinePolicy"))
        assertFalse(wire.containsKey("turnPolicy"))
        assertEquals(listOf("LOCAL"),wire["executionModesSupported"])
        assertFalse(duel.botsAllowed)
    }
    @Test fun `v2 documents never include v1 publication or economy`() {
        val docs=GameCatalogV2Publisher.documents(p)
        assertTrue(docs.containsKey("gameCatalogs/2"));assertFalse(docs.containsKey("gameCatalogs/1"))
        assertFalse(docs.keys.any { it.startsWith("systemConfig") || it.contains("wallet") })
        GameCatalogV2Publisher.verifyImmutable(docs,docs)
        assertFails {GameCatalogV2Publisher.verifyImmutable(docs,docs+ ("gameModes/duel-1v1" to mapOf("invalid" to true)))}
    }
    @ParameterizedTest @ValueSource(ints=[0,1,2,3,4,5,6,7,8,9,10])
    fun `unsupported duel graphs fail closed`(case:Int) {
        var m=p.modes.last();var r=p.versions.last()
        when(case) {
            0 -> m=m.copy(botsAllowed=true)
            1 -> m=m.copy(minHumans=1)
            2 -> m=m.copy(teamSize=1)
            3 -> m=m.copy(seatTeams=listOf(listOf(0),listOf(1)))
            4 -> m=m.copy(onlinePolicy=DisconnectPolicy(181,true,true))
            5 -> r=r.copy(turnPolicy=TurnPolicy(61,true,AutoPlayPolicy.FIRST_VALID_MOVE))
            6 -> r=r.copy(capicuaPolicy=CapicuaPolicy(CapicuaDetection.LAST_TILE_PLAYABLE_ON_BOTH_ENDS,2,true))
            7 -> r=r.copy(firstRoundStarting=StartingPolicy(StartingMode.FIXED_SEAT,0))
            8 -> r=r.copy(blockedPolicy=r.blockedPolicy.copy(opposingTeamsMinimumTie=OpposingTie.ROUND_TIE))
            9 -> r=r.copy(requiredCapabilities=emptyList())
            10 -> r=r.copy(turnOrder=listOf(0,0))
        }
        r=r.copy(contentHash=GameCatalogCodec.hash(r))
        assertFails {GameCatalogValidator.resolve(p.copy(modes=listOf(p.modes.first(),m),versions=listOf(p.versions.first(),r)))}
    }
}
