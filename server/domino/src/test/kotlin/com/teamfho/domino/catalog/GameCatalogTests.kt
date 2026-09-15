package com.teamfho.domino.catalog

import org.junit.jupiter.api.Test
import org.junit.jupiter.params.ParameterizedTest
import org.junit.jupiter.params.provider.ValueSource
import java.time.Clock
import java.time.Instant
import java.time.ZoneId
import kotlin.test.*

class GameCatalogTests {
    private val p = GameCatalogSeed.canonical()
    private class Store : CatalogDocumentStore {
        val data = linkedMapOf<String,Map<String,Any>>()
        var fail = false
        override fun read(path: String) = data[path]
        override fun createAndPublish(documents: Map<String,Map<String,Any>>,version: Int) {
            CatalogSeedChecks.verify(documents,documents.keys.associateWith { data[it] },data[GameCatalogSeed.POINTER],version)
            if(fail) error("FAIL_BEFORE_PUBLICATION")
            documents.forEach { (k,v) -> data.putIfAbsent(k,v) }
            data.putIfAbsent(GameCatalogSeed.POINTER,mapOf("publishedVersion" to version.toLong()))
        }
    }
    @Test fun `canonical graph serializes hashes and resolves exact rules`() {
        assertEquals(p,GameCatalogCodec.decode(GameCatalogCodec.map(p),GameCatalogPublication::class.java))
        val r = GameCatalogValidator.resolve(p).modes.single().ruleSet
        assertEquals(listOf(0,1,2,3),r.dealPolicy.seatOrder)
        assertEquals(listOf(0,3,2,1),r.turnOrder)
        assertEquals(55,(r.maxPip+1)*(r.maxPip+2)/2)
        assertEquals(15,55-4*r.tilesPerPlayer)
        assertEquals(200,r.targetScore); assertEquals(10,r.finishScoring.bonus); assertEquals(0,r.blockedScoring.bonus)
    }
    @Test fun `hash excludes timestamps and remains sensitive to semantics`() {
        val r=p.versions.single()
        assertEquals(r.contentHash,GameCatalogCodec.hash(r.copy(createdAt="2027-01-01T00:00:00Z")))
        assertNotEquals(r.contentHash,GameCatalogCodec.hash(r.copy(targetScore=201)))
        assertEquals(GameCatalogCodec.semantic(r),GameCatalogCodec.semantic(GameCatalogCodec.map(r).toList().reversed().toMap()))
    }
    @ParameterizedTest @ValueSource(ints=[0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21])
    fun `invalid complete graphs rejected`(case: Int) {
        var m=p.modes.single();var r=p.versions.single();var b=p.bindings.single()
        when(case) {
            0 -> m=m.copy(playerCount=2)
            1 -> m=m.copy(seatTeams=listOf(listOf(0,2),listOf(1,2)))
            2 -> m=m.copy(teamSize=3)
            3 -> r=r.copy(dealPolicy=r.dealPolicy.copy(seatOrder=listOf(0,3,2,2)))
            4 -> r=r.copy(turnOrder=listOf(0,3,2,4))
            5 -> r=r.copy(maxPip=1)
            6 -> r=r.copy(firstRoundStarting=r.firstRoundStarting.copy(seat=4))
            7 -> r=r.copy(followingRoundStarting=r.followingRoundStarting.copy(seat=-1))
            8 -> r=r.copy(targetScore=0)
            9 -> r=r.copy(finishScoring=r.finishScoring.copy(bonus=-1))
            10 -> r=r.copy(blockedScoring=r.blockedScoring.copy(bonus=-1))
            11 -> r=r.copy(requiredCapabilities=listOf("DRAW"))
            12 -> r=r.copy(ruleSchemaVersion=2)
            13 -> b=b.copy(ruleSetVersion=2)
            14 -> b=b.copy(topologyVersion=2)
            15 -> b=b.copy(isDefault=false)
            16 -> m=m.copy(minHumans=5)
            17 -> m=m.copy(executionModesSupported=emptyList())
            18 -> r=r.copy(tilesPerPlayer=0)
            19 -> r=r.copy(tiePolicy=r.tiePolicy.copy(nextRoundMultiplier=4))
            20 -> r=r.copy(maxPip=10)
            21 -> m=m.copy(defaultRuleSetId="unknown")
        }
        r=r.copy(contentHash=GameCatalogCodec.hash(r))
        assertFails { GameCatalogValidator.resolve(p.copy(modes=listOf(m),versions=listOf(r),bindings=listOf(b))) }
    }
    @Test fun `duplicate defaults and tampered hash rejected`() {
        assertFails { GameCatalogValidator.resolve(p.copy(bindings=p.bindings+p.bindings.single().copy(id="duplicate"))) }
        assertFails { GameCatalogValidator.resolve(p.copy(versions=listOf(p.versions.single().copy(contentHash="bad")))) }
    }
    @Test fun `seed twice creates six documents without overwriting and storage roundtrips`() {
        val store=Store();GameCatalogSeed.run(store,p);val before=store.data.toMap();GameCatalogSeed.run(store,p)
        assertEquals(before,store.data);assertEquals(6,store.data.size)
        assertEquals(p,FirestoreGameCatalogRepository(store).read())
        val storedTeams=store.data["gameModes/partners-2v2"]!!["seatTeams"] as List<*>
        assertTrue(storedTeams.first() is Map<*,*>)
    }
    @Test fun `existing changed version stops seed without touching pointer`() {
        val store=Store();GameCatalogSeed.run(store,p)
        val path="ruleSets/double-nine-partners/versions/1"
        store.data[path]=store.data[path]!!+mapOf("targetScore" to 999)
        val before=store.data.toMap()
        assertFails { GameCatalogSeed.run(store,p) };assertEquals(before,store.data)
    }
    @Test fun `failed seed leaves old publication untouched`() {
        val store=Store();GameCatalogSeed.run(store,p);val before=store.data.toMap()
        store.fail=true
        assertFails { GameCatalogSeed.run(store,p) };assertEquals(before,store.data)
        store.fail=false;store.data[GameCatalogSeed.POINTER]=mapOf("publishedVersion" to 2L)
        assertFails { GameCatalogSeed.run(store,p) };assertEquals(2L,store.data[GameCatalogSeed.POINTER]!!["publishedVersion"])
    }
    @Test fun `published embedded graph ignores mutable live identity changes`() {
        val store=Store();GameCatalogSeed.run(store,p)
        store.data["gameModes/partners-2v2"]=mapOf("corrupt" to true)
        assertEquals(p,FirestoreGameCatalogRepository(store).read())
        store.data[GameCatalogSeed.POINTER]=mapOf("publishedVersion" to 9L)
        assertFails { FirestoreGameCatalogRepository(store).read() }
    }
    @Test fun `cache ttl stale recovery and unavailable without cache`() {
        var instant=Instant.parse("2026-09-13T00:00:00Z");var reads=0;var failure=false
        val clock=object:Clock(){override fun getZone()=ZoneId.of("UTC");override fun withZone(zone:ZoneId)=this;override fun instant()=instant}
        val repo=GameCatalogRepository { reads++;if(failure)error("down");p }
        val service=GameCatalogService(repo,300,clock)
        val first=service.resolve();assertNotNull(first);assertEquals(first,service.resolve());assertEquals(1,reads)
        instant=instant.plusSeconds(301);failure=true
        assertEquals(first,service.resolve());assertEquals(2,reads)
        assertNull(GameCatalogService(repo,300,clock).resolve())
        failure=false;instant=instant.plusSeconds(301);assertEquals(first,service.resolve())
    }
    @Test fun `scalar coercion and unsupported enums rejected`() {
        assertFails { GameCatalogCodec.mapper.readValue(GameCatalogCodec.json(p).replace("\"LOCAL\"","\"UNKNOWN_EXECUTION\""),GameCatalogPublication::class.java) }
        assertFails { GameCatalogCodec.mapper.readValue(GameCatalogCodec.json(p).replace("\"playerCount\":4","\"playerCount\":\"4\""),GameCatalogPublication::class.java) }
    }
}
