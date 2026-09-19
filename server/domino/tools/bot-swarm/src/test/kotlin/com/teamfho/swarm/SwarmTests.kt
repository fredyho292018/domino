package com.teamfho.swarm

import kotlin.test.*
import tools.jackson.databind.node.ObjectNode

class SwarmTests {
    @Test fun emulatorRequiresBothExactLoopbackServicesAndDemoProject(){
        val good=mapOf("DOMINO_SWARM_EMULATOR" to "true","DOMINO_SWARM_FIREBASE_PROJECT_ID" to ValidationTarget.project,
            "FIRESTORE_EMULATOR_HOST" to "127.0.0.1:18085","FIREBASE_AUTH_EMULATOR_HOST" to "127.0.0.1:19099")
        assertTrue(ValidationTarget.emulator(good));assertFalse(ValidationTarget.emulator(emptyMap()))
        for(key in good.keys)assertFails{ValidationTarget.emulator(good-key)}
        assertFails{ValidationTarget.emulator(good+mapOf("DOMINO_SWARM_FIREBASE_PROJECT_ID" to "teamfho-domino"))}
        assertFails{ValidationTarget.emulator(good+mapOf("FIRESTORE_EMULATOR_HOST" to "example.com:18085"))}
        assertFails{ValidationTarget.emulator(mapOf("FIRESTORE_EMULATOR_HOST" to "127.0.0.1:18085"))}
    }
    @Test fun completionTargetAndConcurrentMetrics(){
        assertEquals(0,Config().targetMatches);assertFails{Config(targetMatches=-1).validate()}
        val m=Metrics();m.started("a");m.started("a");m.started("b");m.finished("a");m.finished("a")
        assertEquals(1,m.completedCount());assertEquals(2L,m.snapshot()["maxSimultaneousMatches"])
    }
    @Test fun identityLeaseCannotBeSharedAndCloseIsIdempotent(){
        val dir=java.nio.file.Files.createTempDirectory("domino-swarm-lease-")
        val http=java.net.http.HttpClient.newHttpClient()
        try {
            val settings=IdentitySettings(mapOf("DOMINO_SWARM_IDENTITIES_DIR" to dir.toString(),"DOMINO_SWARM_FIREBASE_PROJECT_ID" to "test-project","DOMINO_SWARM_FIREBASE_API_KEY" to "test-key"))
            val first=Identity(settings,Config(),0,http)
            try {assertFails{Identity(settings,Config(),0,http)}} finally {first.close();first.close()}
            Identity(settings,Config(),0,http).close()
        } finally {http.close();java.nio.file.Files.deleteIfExists(dir.resolve("slot-01.lock"));java.nio.file.Files.deleteIfExists(dir)}
    }
    @Test fun monetizationAndDirectMatchCreationRejectedBeforeToken(){
        java.net.http.HttpClient.newHttpClient().use {http->
            val api=Api(http,"http://127.0.0.1:1"){error("TOKEN_MUST_NOT_BE_REQUESTED")}
            for(path in listOf("matches","rewards/consume","wallet","monetization/config")) {
                assertFailsWith<IllegalArgumentException>{kotlinx.coroutines.runBlocking{api.call("POST",path)}}
            }
        }
    }
    @Test fun expiredAuthRetriesOnlyOnce(){val retry=ExpiredAuthRetry();assertTrue(retry.allow("AUTH_TOKEN_EXPIRED"));assertFalse(retry.allow("AUTH_TOKEN_EXPIRED"))}
    @Test fun revokedOrInvalidAuthDoesNotRetry(){for(category in listOf("WS_AUTH_REJECTED","AUTH_TOKEN_REVOKED","HTTP_403"))assertFalse(ExpiredAuthRetry().allow(category))}
    @Test fun defaults(){val c=Config();assertEquals(10,c.clients);assertEquals("DUEL_1V1",c.mode);assertEquals(c,c.validate());assertTrue(c.requeue)}
    @Test fun overrides(){val c=Config.load(arrayOf("--clients=4","--mode=PARTNERS_2V2_ONLINE","--seed=19"),emptyMap());assertEquals(4,c.clients);assertEquals(19,c.seed)}
    @Test fun environmentOverrides(){assertEquals(3,Config.load(emptyArray(),mapOf("DOMINO_SWARM_CLIENTS" to "3")).clients)}
    @Test fun caps(){for(n in listOf(0,21,100))assertFailsWith<IllegalArgumentException>{Config(clients=n).validate()};Config(clients=1).validate();Config(clients=20).validate()}
    @Test fun productionBlockedBeforeAuth(){for(e in listOf("PROD","PRODUCTION","prod",""))assertFailsWith<IllegalArgumentException>{Config(environment=e).validate()}}
    @Test fun remoteHttpBlocked(){for(url in listOf("http://192.168.1.2:8080","http://localhost.evil.com","http://example.com","http://user@localhost","http://127.0.0.1/path"))assertFailsWith<IllegalArgumentException>{Config(baseUrl=url).validate()}}
    @Test fun testRequiresExplicitHttps(){assertFailsWith<IllegalArgumentException>{Config(environment="TEST",baseUrl="https://test.example.com").validate()};Config(environment="TEST",baseUrl="https://test.example.com").validate("https://test.example.com")}
    @Test fun aliases(){val a=Aliases.forSeed(1);assertEquals(20,a.distinct().size);assertEquals(a,Aliases.forSeed(1));assertNotEquals(a,Aliases.forSeed(2))}
    private fun mode(count:Int=4):Map<String,Any> = mapOf("key" to if(count==4)"PARTNERS_2V2_ONLINE" else "DUEL_1V1","active" to true,"executionModesSupported" to listOf("ONLINE"),"playerCount" to count,"botsAllowed" to false,"seatTeams" to if(count==4)listOf(listOf(0,2),listOf(1,3))else emptyList<List<Int>>(),"defaultRuleSetId" to if(count==4)"double-nine-partners"else"double-nine-duel","ruleSet" to mapOf("id" to if(count==4)"double-nine-partners"else"double-nine-duel","version" to 1,"tilesPerPlayer" to 10))
    private fun projection(count:Int=4,seq:Long=1,seat:Int=0):ObjectNode=Json.read(Json.write(mapOf("lastSequence" to seq,"phase" to "PLAYING","publicState" to mapOf("matchId" to "test-match","modeKey" to mode(count)["key"],"lastSequence" to seq,"currentSeat" to seat,"board" to emptyList<Int>(),"participants" to (0 until count).map{mapOf("seat" to it,"controlType" to "REMOTE_HUMAN")}),"privateState" to mapOf("seat" to seat,"lastSequence" to seq,"hand" to listOf(mapOf("sideA" to 2,"sideB" to 4))),"ruleSnapshot" to mapOf("effectiveModeJson" to Json.write(mode(count)))))) as ObjectNode
    @Test fun modeDrivenTopology(){for(n in listOf(2,4,6)){val m=Mode.read(Json.read(Json.write(mode(n))));assertEquals(n,m.requiredPlayers)};assertEquals(0,Mode.read(Json.read(Json.write(mode()))).team(2));assertEquals(1,Mode.read(Json.read(Json.write(mode()))).team(3))}
    @Test fun ruleResolution(){assertEquals("double-nine-duel",Projection(projection(2)).mode.ruleSetId);assertEquals("double-nine-partners",Projection(projection()).mode.ruleSetId)}
    @Test fun firstMove(){val p=Projection(projection());assertEquals("PLAY_TILE",p.intent()!!["type"]);assertEquals("LEFT",p.intent()!!["chainEnd"])}
    @Test fun outOfTurnNoIntent(){val p=projection();(p.path("publicState") as ObjectNode).put("currentSeat",1);assertNull(Projection(p).intent())}
    @Test fun privateHandsRejected(){for(n in listOf(2,4)){val p=projection(n);(p.path("publicState") as ObjectNode).putObject("hands").put("2","secret");assertFailsWith<IllegalArgumentException>{Projection(p)}}}
    @Test fun partnerPrivateEventRejected(){val t=SequenceTracker();t.snapshot(Projection(projection()));val update=Json.read(Json.write(mapOf("matchId" to "test-match","firstSequence" to 2,"snapshot" to projection(seq=2),"events" to listOf(mapOf("sequence" to 2,"event" to mapOf("visibility" to "PLAYER_PRIVATE","targetSeat" to 2))))));assertFailsWith<IllegalArgumentException>{t.update(update)}}
    @Test fun sequenceGapAndResync(){val t=SequenceTracker();t.snapshot(Projection(projection()));val update=Json.read(Json.write(mapOf("matchId" to "test-match","firstSequence" to 3,"snapshot" to projection(seq=3),"events" to emptyList<Int>())));assertFalse(t.update(update));assertEquals(1,t.gaps);assertEquals(1,t.current!!.sequence);t.snapshot(Projection(projection(seq=3)));assertEquals(3,t.current!!.sequence)}
    @Test fun duplicateIgnored(){val t=SequenceTracker();t.snapshot(Projection(projection()));assertFalse(t.update(Json.read(Json.write(mapOf("matchId" to "test-match","snapshot" to projection())))));assertEquals(1,t.current!!.sequence)}
    @Test fun commandRetryUsesSameId(){val p=Projection(projection());val c=LogicalCommand.create(p,p.intent()!!);val retry=c.body;assertEquals(c.id,retry["commandId"]);assertNotEquals(c.id,LogicalCommand.create(p,p.intent()!!).id)}
    @Test fun noClientAuthorities(){val p=Projection(projection());val body=LogicalCommand.create(p,p.intent()!!).body;for(k in listOf("uid","seat","score","coins","rules"))assertFalse(body.containsKey(k))}
    @Test fun metricsCountMatchOnce(){val m=Metrics();repeat(4){m.finished("one")};assertEquals(1L,m.snapshot()["matchesFinished"])}
}
