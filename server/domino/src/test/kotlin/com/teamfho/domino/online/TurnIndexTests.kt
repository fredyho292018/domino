package com.teamfho.domino.online

import org.junit.jupiter.api.Test
import org.junit.jupiter.api.Tag
import java.time.Instant
import kotlin.test.*

class MemoryTurnDueIndex:TurnDueIndex {
    var owner:String?=null
    val entries=java.util.concurrent.ConcurrentHashMap<String,Instant>()
    override fun lead(owner:String):Int=if(this.owner==owner)1 else if(this.owner==null){this.owner=owner;2}else 0
    override fun release(owner:String){if(this.owner==owner)this.owner=null}
    override fun offer(owner:String,id:String,due:Instant?):Boolean {
        if(this.owner!=owner)return false
        if(due==null)entries.remove(id)else entries[id]=due
        return true
    }
    @Synchronized override fun claim(now:Instant)=entries.filterValues{it<=now}.keys.toList().also {ids->ids.forEach{entries[it]=now.plusSeconds(30)}}
    override fun forget(id:String){entries.remove(id)}
}

@Tag("IN_MEMORY")
class TurnIndexTests {
    @Test fun `idle startup snapshot once and no polling across 720 five second ticks`() {
        val index=MemoryTurnDueIndex();var snapshots=0;var closes=0
        val feed=TurnWorkFeed {_,_->snapshots++;AutoCloseable{closes++}}
        TurnIndexBridge(index,feed).use {bridge->repeat(720){bridge.tick();assertTrue(index.claim(Instant.now()).isEmpty())}}
        assertEquals(1,snapshots);assertEquals(1,closes)
    }
    @Test fun `backend restart and redis loss rebuild persisted expired deadline`() {
        val index=MemoryTurnDueIndex();val now=Instant.parse("2026-01-01T00:00:00Z");var snapshots=0
        val feed=TurnWorkFeed {change,_->snapshots++;change("match",now.minusSeconds(60));AutoCloseable{}}
        TurnIndexBridge(index,feed).use {it.tick()}
        TurnIndexBridge(index,feed).use {bridge->
            bridge.tick();assertEquals(listOf("match"),index.claim(now))
            index.owner=null;index.entries.clear();bridge.tick();bridge.tick()
            assertEquals(listOf("match"),index.claim(now))
        }
        assertEquals(3,snapshots)
    }
    @Test fun `competing bridges only one durable subscription and failover`() {
        val index=MemoryTurnDueIndex();var snapshots=0
        val feed=TurnWorkFeed {_,_->snapshots++;AutoCloseable{}}
        val a=TurnIndexBridge(index,feed);val b=TurnIndexBridge(index,feed)
        try {a.tick();b.tick();assertEquals(1,snapshots);a.close();b.tick();assertEquals(2,snapshots)}finally{a.close();b.close()}
    }
    @Test fun `expired processing lease requeues without losing work`() {
        val index=MemoryTurnDueIndex();val now=Instant.now();index.entries["match"]=now
        assertEquals(listOf("match"),index.claim(now));assertTrue(index.claim(now.plusSeconds(29)).isEmpty())
        assertEquals(listOf("match"),index.claim(now.plusSeconds(30)))
    }
    @Test fun `watch failures back off rather than retry every tick`() {
        val index=MemoryTurnDueIndex();val clock=OnlineTurnTests.Time(Instant.now());var attempts=0
        val feed=TurnWorkFeed {_,fail->attempts++;fail();AutoCloseable{}}
        TurnIndexBridge(index,feed,clock).use {bridge->
            repeat(720){bridge.tick();clock.value=clock.value.plusSeconds(5)}
        }
        assertTrue(attempts<25,"attempts=$attempts")
    }
}
