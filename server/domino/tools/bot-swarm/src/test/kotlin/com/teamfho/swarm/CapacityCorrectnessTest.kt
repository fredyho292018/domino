package com.teamfho.swarm
import kotlin.test.*
class CapacityCorrectnessTest {
    @Test fun `sequence duplicate command and invalid turn are independently detected`() {
        val a=CapacityCorrectness()
        a.event(1,"TURN_STARTED",0,null);a.event(2,"TILE_PLAYED",0,"command")
        a.event(3,"TURN_CHANGED",1,null);a.event(4,"TILE_PLAYED",0,"command")
        a.event(4,"TILE_PLAYED",0,"command");a.event(2,"TILE_PLAYED",0,"command")
        a.event(6,"TURN_CHANGED",2,null)
        val c=a.counters();assertEquals(1L,c["duplicateCommandApplications"]);assertEquals(1L,c["invalidTurnAcceptances"])
        assertEquals(1L,c["duplicateEvents"]);assertEquals(1L,c["sequenceRegressions"]);assertEquals(1L,c["sequenceGaps"])
    }
    @Test fun `private cross match system events and inconsistent receipt are counted without payload logs`() {
        val a=CapacityCorrectness();a.authorize("a","a",1,"PLAYER_PRIVATE",1);assertEquals(0L,a.counters()["unauthorizedDeliveries"])
        a.authorize("a","b",1,"PUBLIC",null);a.authorize("a","a",1,"PLAYER_PRIVATE",0);a.authorize("a","a",1,"SYSTEM_PRIVATE",null)
        assertEquals(3L,a.counters()["unauthorizedDeliveries"])
        a.receipt("x",3,true);a.receipt("x",3,true);a.receipt("x",4,true);a.receipt("y",0,false)
        assertEquals(1L,a.counters()["idempotencyViolations"]);assertEquals(1L,a.counters()["commandFailures"])
    }
    @Test fun `cancelled gameplay never increments completed counter`() {
        val m=Metrics();m.started("a");m.cancelled("a");m.cancelled("a")
        assertEquals(1L,m.snapshot()["matchesCancelled"]);assertNull(m.snapshot()["matchesFinished"])
    }
}
