package com.teamfho.swarm
import org.junit.jupiter.api.Test
import java.time.Instant
import java.util.UUID
import kotlin.test.*

class AuthoritativeRunRegistryTests {
    private val start=Instant.parse("2026-01-01T00:00:00Z")
    private val population=(1..100).map{"load-$it"}.toSet()
    private val id=UUID(0,1).toString()
    private fun scope(client:Set<String> = emptySet(),through:Instant=start.plusSeconds(1000))=RunScope(UUID(1,1).toString(),start,through,population,client)
    private fun match(created:Instant=start.plusSeconds(10),status:String="FINISHED")=AuthorityMatch(id,created,population.take(4).toSet(),4,status,status,true,status=="IN_PROGRESS")
    @Test fun `backend match missed by client is discovered`() {val r=AuthoritativeRunRegistry.discover(scope(),listOf(match()));assertEquals(listOf(id),r.matchIds);assertEquals(1,r.clientMissedMatches);assertEquals(0,r.unclassifiedLoadMatches)}
    @Test fun `normal observation deduplicates multiple sources`() {val r=AuthoritativeRunRegistry.discover(scope(setOf(id)),listOf(match(),match()));assertEquals(1,r.registryUnionMatches);assertEquals(0,r.clientMissedMatches)}
    @Test fun `foreign population window and partial membership excluded`() {
        for(m in listOf(match().copy(players=setOf("foreign1","foreign2","foreign3","foreign4")),match(start.minusSeconds(1)),match(start.plusSeconds(1001)),match().copy(players=setOf("load-1","foreign1","foreign2","foreign3")))) {
            val r=AuthoritativeRunRegistry.discover(scope(),listOf(m));assertTrue(r.matchIds.isEmpty())
        }
        assertEquals(1,AuthoritativeRunRegistry.discover(scope(setOf(id)),listOf(match(start.minusSeconds(1)))).unclassifiedLoadMatches)
    }
    @Test fun `global stop race discovers later committed match before finalization`() {
        val stop=start.plusSeconds(100);val f=RegistryFinalization(stop)
        assertFalse(f.observe(stop,scope(through=stop),emptyList()).second)
        val createdDuringStop=match(stop.minusSeconds(1))
        val first=f.observe(stop.plusSeconds(360),scope(through=stop.plusSeconds(360)),listOf(createdDuringStop))
        assertEquals(listOf(id),first.first.matchIds);assertFalse(first.second)
        assertTrue(f.observe(stop.plusSeconds(362),scope(through=stop.plusSeconds(362)),listOf(createdDuringStop)).second)
    }
    @Test fun `baseline finds old active load matches outside run and client registry`() {
        val r=AuthoritativeRunRegistry.discover(scope(),listOf(match(start.minusSeconds(1000),"IN_PROGRESS")))
        assertEquals(1,r.activeLoadMatches);assertFalse(r.baselineReady);assertTrue(r.matchIds.isEmpty())
        assertTrue(AuthoritativeRunRegistry.discover(scope(),listOf(match(start.minusSeconds(1000)))).baselineReady)
    }
    @Test fun `inconsistent authority and missing client match prevent completeness`() {
        assertFalse(AuthoritativeRunRegistry.discover(scope(setOf(id)),emptyList()).baselineReady)
        assertEquals(1,AuthoritativeRunRegistry.discover(scope(),listOf(match().copy(runtimeStatus="IN_PROGRESS"))).unclassifiedLoadMatches)
    }
    @Test fun `twenty sixth match rejected`() {assertFailsWith<IllegalArgumentException>{AuthoritativeRunRegistry.discover(scope(),(1L..26).map{match().copy(id=UUID(0,it).toString())})}}
}
