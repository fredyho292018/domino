package com.teamfho.domino.entitlement

import com.teamfho.domino.match.*
import org.junit.jupiter.api.Test
import java.time.Instant
import kotlin.test.*

class EntitlementHistoryTests {
    val clock=EntitlementClock();val repo=MemoryEntitlements()
    val service=EntitlementService(SubscriptionPolicyService({SubscriptionPolicy(promotionalTrialEnabled=false)},clock=clock),repo,clock)
    val fixture=ReplayFixture()
    val rows=(0..24).map {n->PlayerMatchHistory("match-$n","DUEL_1V1","double-nine-duel",1,HistoryResult.WIN,listOf(150,12),
        emptyList(),Instant.EPOCH,Instant.EPOCH.minusSeconds(n.toLong()),MatchFinishReason.TARGET_REACHED)}
    var eventReads=0;var maxHistoryRequested=0
    val history=object:PlayerMatchHistoryRepository {
        override fun history(uid:String,limit:Int,afterMatchId:String?):HistoryPage {
            maxHistoryRequested=maxOf(maxHistoryRequested,limit)
            val offset=if(afterMatchId==null)0 else rows.indexOfFirst{it.matchId==afterMatchId}+1
            val result=if(uid=="p0")rows.drop(offset).take(limit) else emptyList()
            return HistoryPage(result,if(offset+result.size<rows.size&&result.isNotEmpty())result.last().matchId else null)
        }
    }
    val source=object:ReplaySource {
        override fun match(id:String)=fixture.state.match.copy(matchId=id)
        override fun events(id:String,after:Long,limit:Int)=fixture.events.filter{it.sequence>after}.take(limit).map{it.copy(matchId=id)}.also{eventReads++}
        override fun round(id:String,number:Int)=fixture.rounds[number]
    }
    val access=EntitlementHistory(service,history,source)
    fun premium()=service.adminGrant("p0",EntitlementGrant("admin",EntitlementSource.ADMIN_GRANT,validFrom=clock.now,
        validUntil=clock.now.plusSeconds(10),createdAt=clock.now,policyVersion=1,reason="TEST",grantedBy="test"))
    @Test fun `free history total window ten cannot be bypassed with cursor or page size`() {
        assertEquals(10,access.page("p0",100,null).items.size)
        val p=access.page("p0",4,null);val q=access.page("p0",4,p.nextCursor);val r=access.page("p0",4,q.nextCursor)
        assertEquals(2,r.items.size);assertNull(r.nextCursor)
        assertFailsWith<EntitlementFailure>{access.page("p0",20,"match-12")}
        assertEquals(10,maxHistoryRequested);assertEquals(25,rows.size)
    }
    @Test fun `free latest three eligible replay denial before event loading premium full access`() {
        for(i in 0..2)access.requireReplay("p0","match-$i")
        assertEquals("FEATURE_NOT_ENTITLED",assertFailsWith<EntitlementFailure>{access.requireReplay("p0","match-3")}.code)
        assertEquals(0,eventReads)
        premium();access.requireReplay("p0","match-24")
        val page=access.page("p0",20,null);assertEquals(20,page.items.size);assertEquals(5,access.page("p0",20,page.nextCursor).items.size)
    }
    @Test fun `manifest session continues across expiration new entry denied and membership still checked`() {
        premium();val replay=ReplayService(source,access::requireReplay,clock)
        val manifest=replay.manifest("p0","match-24");assertTrue(manifest.replayAvailable);assertNotNull(manifest.accessSession)
        clock.now=clock.now.plusSeconds(10)
        assertEquals(Plan.FREE,service.resolve("p0").plan)
        assertTrue(replay.page("p0","match-24",0,5,manifest.accessSession).items.isNotEmpty())
        assertFailsWith<EntitlementFailure>{replay.manifest("p0","match-24")}
        assertFailsWith<EntitlementFailure>{replay.page("p0","match-23",0,5,manifest.accessSession)}
        assertFailsWith<ReplayForbidden>{replay.page("intruder","match-24",0,5,manifest.accessSession)}
        clock.now=clock.now.plusSeconds(1800)
        assertFailsWith<EntitlementFailure>{replay.page("p0","match-24",0,5,manifest.accessSession)}
    }
    @Test fun `active match and cross match privacy still precede entitlement checks`() {
        premium()
        val active=object:ReplaySource by source {override fun match(id:String)=source.match(id)!!.copy(status=MatchStatus.IN_PROGRESS)}
        assertFailsWith<ReplayFailure>{ReplayService(active,access::requireReplay,clock).manifest("p0","match-0")}
        assertFailsWith<ReplayForbidden>{ReplayService(source,access::requireReplay,clock).manifest("intruder","match-0")}
    }
    @Test fun `active authoritative game stays independent of grant expiry and no entitlement reads per command`() {
        // The same engine fixture executes a complete match with a server clock crossing the entitlement boundary.
        premium();val before=repo.reads
        var crossed=false
        val duel=ReplayFixture(beforeCommand={n->if(n==20){clock.now=clock.now.plusSeconds(10);crossed=true}})
        val partners=ReplayFixture(true)
        assertTrue(crossed)
        assertEquals(MatchStatus.FINISHED,duel.state.match.status);assertEquals(MatchStatus.FINISHED,partners.state.match.status)
        assertEquals(before,repo.reads)
        assertTrue(duel.events.any{it.payload is TilePlayed});assertTrue(partners.events.any{it.payload is TilePlayed})
    }
}
