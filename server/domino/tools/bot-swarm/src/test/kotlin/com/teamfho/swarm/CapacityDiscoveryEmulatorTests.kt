package com.teamfho.swarm
import com.google.cloud.firestore.FirestoreOptions
import org.junit.jupiter.api.Tag
import org.junit.jupiter.api.Test
import java.time.Instant
import java.util.UUID
import kotlin.test.*

@Tag("EMULATOR")
class CapacityDiscoveryEmulatorTests {
    @Test fun `real reader finds missed terminal creation and old active baseline then stop race`() {
        require(System.getenv("FIRESTORE_EMULATOR_HOST")=="127.0.0.1:18085")
        FirestoreOptions.newBuilder().setProjectId("demo-domino-f0").setEmulatorHost("127.0.0.1:18085")
            .setChannelProvider(FirestoreOptions.getDefaultTransportChannelProviderBuilder().setEndpoint("127.0.0.1:18085").setChannelConfigurator{it.usePlaintext().proxyDetector{null}}.build())
            .setCredentials(FirestoreOptions.EmulatorCredentials()).build().service.use{db->
            val start=Instant.now();val token=UUID.randomUUID().toString();val players=(1..100).map{"fixture-$token-$it"}.toSet()
            fun fixture(at:Instant,status:String,who:Set<String> = players.take(4).toSet()):String {
                val id=UUID.randomUUID().toString();val root=db.document("matches/$id")
                root.set(mapOf("createdAt" to at.toString(),"status" to status,"validationData" to true,"participants" to who.map{mapOf("playerUid" to it)})).get()
                root.collection("runtime").document("authoritative").set(mapOf("stateJson" to Json.write(mapOf("match" to mapOf("status" to status))))).get()
                return id
            }
            val old=fixture(start.minusSeconds(60),"IN_PROGRESS")
            val missed=fixture(start.plusSeconds(1),"FINISHED")
            val foreign=fixture(start.plusSeconds(1),"IN_PROGRESS",setOf("foreign-a","foreign-b","foreign-c","foreign-d"))
            fun scope(through:Instant)=RunScope(token,start,through,players,setOf(missed))
            val source=FirestoreCapacityDiscovery(db)
            val baseline=AuthoritativeRunRegistry.discover(scope(start.plusSeconds(10)),source.read(scope(start.plusSeconds(10))))
            assertEquals(1,baseline.activeLoadMatches);assertFalse(baseline.baselineReady);assertEquals(listOf(missed),baseline.matchIds);assertFalse(foreign in baseline.matchIds)
            val stop=start.plusSeconds(20);val finish=RegistryFinalization(stop)
            assertFalse(finish.observe(stop,scope(stop),source.read(scope(stop))).second)
            val late=fixture(stop.minusMillis(1),"CANCELLED") // commit happens after first STOP read
            db.document("matches/$old").update("status","CANCELLED").get()
            db.document("matches/$old/runtime/authoritative").update("stateJson",Json.write(mapOf("match" to mapOf("status" to "CANCELLED")))).get()
            val at=stop.plusSeconds(360);val r=finish.observe(at,scope(at),source.read(scope(at)))
            assertEquals(setOf(missed,late),r.first.matchIds.toSet());assertEquals(1,r.first.clientMissedMatches);assertFalse(r.second)
            assertTrue(finish.observe(at.plusSeconds(2),scope(at.plusSeconds(2)),source.read(scope(at.plusSeconds(2)))).second)
        }
    }
}
