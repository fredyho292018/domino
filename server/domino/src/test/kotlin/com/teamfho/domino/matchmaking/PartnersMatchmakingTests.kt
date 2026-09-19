package com.teamfho.domino.matchmaking

import com.teamfho.domino.catalog.*
import com.teamfho.domino.match.*
import com.teamfho.domino.online.*
import org.junit.jupiter.api.Test
import org.junit.jupiter.api.condition.EnabledIfEnvironmentVariable
import java.util.concurrent.Executors
import kotlin.test.*

@EnabledIfEnvironmentVariable(named="DOMINO_REDIS_TESTS",matches="true")
class PartnersMatchmakingTests {
    val catalog=GameCatalogService(GameCatalogRepository{GameCatalogV4Publisher.canonical()})
    val key=MatchmakingKey.resolve(catalog.resolve()!!,GameCatalogV4Publisher.KEY)
    val rules=MatchRuleSnapshot.freeze(catalog.resolve()!!,catalog.resolve()!!.modes.single{it.key==key.modeKey})
    @Test fun `three wait fourth creates one atomic match no duel cross pairing private hands and four notifications`()=RedisMatchmakingTests.Fixture().use{f->
        val memory=MemoryOnlineRepository();val writes=mutableListOf<OnlineWrite>()
        val repo=object:OnlineRepository by memory {
            @Synchronized override fun createPaired(write:OnlineWrite):OnlineState {
                memory.read(write.state.match.matchId)?.let{return it};writes.add(write);memory.create(write.state);return write.state
            }
            override fun activeFor(uid:String)=memory.states.values.filter{s->s.match.participants.any{it.playerUid==uid}}.map{it.match.matchId}
        }
        val service=MatchmakingService(f.store,catalog,OnlineMatchService(catalog,repo));val found=mutableListOf<Pair<String,QueueStatus>>()
        service.notify={uid,type,status->if(type=="MATCH_FOUND")found.add(uid to status)}
        val duel=f.user();service.join(duel,"DUEL_1V1");val users=(1..4).map{f.user()}
        users.take(3).forEach{service.join(it,key.modeKey);service.tick();assertTrue(writes.isEmpty());assertTrue(found.isEmpty())}
        service.join(users.last(),key.modeKey);service.tick();service.tick()
        assertEquals(1,writes.size);assertEquals(4,found.size);assertEquals(users.toSet(),found.map{it.first}.toSet())
        assertEquals(setOf(0,1,2,3),found.map{it.second.match!!.seat}.toSet());assertEquals(1,found.map{it.second.match!!.matchId}.distinct().size)
        val state=writes.single().state;assertEquals(OnlinePhase.PLAYING,state.phase);assertEquals(40,state.hands.values.sumOf{it.size});assertEquals(15,state.reserve.size)
        assertTrue(state.match.participants.all{it.controlType==ControlType.REMOTE_HUMAN});assertEquals(QueueState.QUEUED,f.store.status(duel).state)
        assertEquals(rules,state.match.ruleSnapshot);assertEquals(listOf(0,1,0,1),state.match.participants.map{it.teamId})
        for(uid in users)assertEquals(10,OnlineMatchService.snapshot(state,uid).privateState.hand.size)
    }
    @Test fun `100 queued humans concurrently reserve 25 disjoint four player matches`()=RedisMatchmakingTests.Fixture().use{f->
        val users=(1..100).map{f.user()};users.forEach{f.store.join(it,key)};val pool=Executors.newFixedThreadPool(8)
        try {
            val reservations=pool.invokeAll((1..100).map{java.util.concurrent.Callable{f.worker().reserve(key,rules)}}).mapNotNull{it.get()}
            assertEquals(25,reservations.size);assertEquals(100,reservations.flatMap{it.uids}.distinct().size)
            assertEquals(users.toSet(),reservations.flatMap{it.uids}.toSet());assertTrue(reservations.all{it.uids.size==4})
            reservations.forEach{assertTrue(f.store.complete(it))};users.forEach{assertEquals(QueueState.MATCHED,f.store.status(it).state)}
        }finally{pool.shutdownNow()}
    }
    @Test fun `four person lease recovery and failure clean every member`()=RedisMatchmakingTests.Fixture().use{f->
        val users=(1..4).map{f.user()};val store=f.worker(80);users.forEach{store.join(it,key)}
        val old=store.reserve(key,rules)!!;Thread.sleep(120);val recovered=store.recover()!!
        assertEquals(old.id,recovered.id);assertEquals(users,recovered.uids);store.failed(old)
        users.forEach{assertEquals(QueueState.RESERVED,store.status(it).state)};store.failed(recovered)
        users.forEach{assertEquals(QueueState.FAILED,store.status(it).state);assertEquals(QueueState.QUEUED,store.join(it,key).state)}
    }
}
