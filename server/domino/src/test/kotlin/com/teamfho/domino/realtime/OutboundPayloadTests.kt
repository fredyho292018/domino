package com.teamfho.domino.realtime

import com.teamfho.domino.online.*
import com.teamfho.domino.match.*
import com.teamfho.domino.matchmaking.*
import org.junit.jupiter.api.Test
import org.mockito.Mockito.*
import org.springframework.web.socket.*
import java.time.Instant
import java.util.concurrent.CopyOnWriteArrayList
import kotlin.test.*

/** Complete deterministic in-memory matches; no Firebase, Redis, Firestore, or Swarm. */
class OutboundPayloadTests {
    @Test fun `complete duel and partners frames fit limits and retain pre migration payload contract`() {
        val maximum=sortedMapOf<String,Int>();var frames=0
        val socket=mock(WebSocketSession::class.java);val output=CopyOnWriteArrayList<String>()
        doAnswer {output.add((it.arguments[0] as TextMessage).payload);null}.`when`(socket).sendMessage(any())
        val writer=ConnectionOutbound(socket)
        try {
            writer.offerControl("AUTHENTICATED",emptyMap());assertTrue(writer.awaitIdle());output.clear()
            fun measure(label:String,type:String,payload:Map<String,Any>) {
                val encoded=com.teamfho.domino.catalog.GameCatalogCodec.mapper.writeValueAsString(payload)
                val size=encoded.toByteArray(Charsets.UTF_8).size
                maximum[label]=maxOf(maximum[label]?:0,size)
                assertTrue(size<=128*1024,"STOP: legitimate $label=$size exceeds provisional frame limit")
                assertEquals(OfferResult.ENQUEUED,writer.offerCritical(type,payload));assertTrue(writer.awaitIdle())
                val actual=com.teamfho.domino.catalog.GameCatalogCodec.mapper.readTree(output.removeAt(0))
                assertEquals(com.teamfho.domino.catalog.GameCatalogCodec.mapper.readTree(encoded),actual["payload"])
                assertEquals(type,actual["type"].asString());frames++
            }
            fun inspect(mode:String,w:OnlineWrite) {
                w.state.match.participants.forEach {p->
                    val snapshot=MatchCodec.map(OnlineMatchService.snapshot(w.state,p.playerUid!!))
                    val size=com.teamfho.domino.catalog.GameCatalogCodec.mapper.writeValueAsBytes(snapshot).size
                    maximum["${mode}_SNAPSHOT"]=maxOf(maximum["${mode}_SNAPSHOT"]?:0,size)
                    val payload=mapOf("matchId" to w.state.match.matchId,"firstSequence" to w.events.first().sequence,
                        "events" to w.events.map{MatchCodec.map(OnlineMatchService.authorized(it,p.seatIndex))},"snapshot" to snapshot)
                    measure("${mode}_MATCH_UPDATE","MATCH_UPDATE",payload)
                    if(w.state.phase==OnlinePhase.MATCH_FINISHED) measure("${mode}_TERMINAL","MATCH_UPDATE",payload)
                }
            }
            for(seed in 1L..3L) {
                val f=OnlineFixture(seed);f.service.committed={inspect("DUEL",it)};f.start()
                var steps=0
                while(f.state().phase!=OnlinePhase.MATCH_FINISHED && steps++<3000) f.move()
                assertEquals(OnlinePhase.MATCH_FINISHED,f.state().phase);assertEquals(2,f.repo.histories.size)
                assertEquals(f.state().match.lastSequence,f.repo.stream[f.id]!!.last().sequence)
                val random=java.util.Random(seed);val engine=OnlineEngine(OnlineRandom{random.nextInt(it)})
                var w=engine.join(PartnersOnlineTests.before(),PartnersOnlineTests.participant(3),"start",Instant.EPOCH)
                inspect("PARTNERS",w);steps=0
                while(w.state.phase!=OnlinePhase.MATCH_FINISHED && steps++<3000) {
                    val s=w.state;val seat=s.match.currentSeat?:0
                    val move=s.hands.getValue(seat.toString()).flatMap{t->ChainEnd.entries.filter{OnlineEngine.fits(s,t,it)}.map{t to it}}.firstOrNull()
                    val next=s.phase==OnlinePhase.ROUND_FINISHED
                    val command=OnlineCommand(1,"step-$steps",s.match.matchId,if(next)OnlineCommandType.NEXT_ROUND else if(move==null)OnlineCommandType.PASS else OnlineCommandType.PLAY_TILE,
                        if(next)null else move?.first,if(next)null else move?.second)
                    w=engine.command(s,"m5-p$seat",command,Instant.EPOCH.plusSeconds(steps.toLong()));inspect("PARTNERS",w)
                }
                assertEquals(OnlinePhase.MATCH_FINISHED,w.state.phase);assertEquals(4,w.histories.size)
            }
            measure("MATCH_FOUND","MATCH_FOUND",MatchCodec.map(QueueStatus(QueueState.MATCHED,MatchFound("test",0,"Opponent","DUEL_1V1","double-nine-duel",1))))
            measure("MATCHMAKING_STATUS","MATCHMAKING_STATUS",MatchCodec.map(QueueStatus(QueueState.QUEUED)))
            println("OUTBOUND_PAYLOAD_MAX_UTF8_BYTES=$maximum WIRE_PARITY_FRAMES=$frames")
        } finally {writer.close();assertTrue(writer.awaitClosed())}
    }
}
