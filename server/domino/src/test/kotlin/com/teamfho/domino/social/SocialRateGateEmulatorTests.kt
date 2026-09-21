package com.teamfho.domino.social

import com.google.cloud.firestore.*
import com.teamfho.domino.player.FirestorePlayerFoundationRepository
import com.teamfho.domino.security.FirebaseIdentity
import com.teamfho.domino.catalog.GameCatalogCodec
import org.junit.jupiter.api.Tag
import org.junit.jupiter.api.Test
import org.springframework.beans.factory.support.DefaultListableBeanFactory
import org.springframework.core.MethodParameter
import org.springframework.web.context.request.NativeWebRequest
import org.springframework.web.method.support.*
import org.springframework.web.bind.support.WebDataBinderFactory
import org.springframework.test.web.servlet.setup.MockMvcBuilders
import org.springframework.test.web.servlet.request.MockMvcRequestBuilders.*
import java.time.*
import java.util.UUID
import java.util.concurrent.*
import kotlin.test.*

@Tag("EMULATOR")
class SocialRateGateEmulatorTests {
    private fun db():Firestore {
        check(System.getenv("FIRESTORE_EMULATOR_HOST")=="127.0.0.1:18085")
        return FirestoreOptions.newBuilder().setProjectId("demo-domino-f0").setEmulatorHost("127.0.0.1:18085").setCredentials(FirestoreOptions.EmulatorCredentials()).build().service
    }
    private fun player(db:Firestore):PublicPlayerIdentity {
        val uid="a2-${UUID.randomUUID()}"
        FirestorePlayerFoundationRepository(db,Clock.systemUTC()).ensure(FirebaseIdentity(uid,true),"en","Guest-ABCDEFGH")
        return PublicPlayerIdentityService(FirestoreSocialRepository(db)).ensure(uid)
    }
    private class Http(db:Firestore) {
        val clock=SocialTestClock()
        val gate=ResilientSocialRateGate(DistributedSocialGate{_,_->throw java.net.ConnectException("test-only outage")},LocalSocialLimiter(clock),SocialRedisCircuit(clock))
        private val cursor=SocialCursor()
        private val social=SocialServices({FirestoreSocialRepository(db)},cursor)
        private val friends=FriendshipServices{FriendshipService(FirestoreFriendships(db),cursor)}
        private val follows=FollowServices{FollowService(FirestoreFriendships(db),cursor)}
        private val provider=DefaultListableBeanFactory().apply{registerSingleton("friends",friends)}.getBeanProvider(FriendshipServices::class.java)
        private val resolver=object:HandlerMethodArgumentResolver {
            override fun supportsParameter(p:MethodParameter)=p.parameterType==FirebaseIdentity::class.java
            override fun resolveArgument(p:MethodParameter,m:ModelAndViewContainer?,r:NativeWebRequest,b:WebDataBinderFactory?)=FirebaseIdentity(r.getHeader("Test-Actor")!!,true)
        }
        val mvc=MockMvcBuilders.standaloneSetup(SocialController(social,gate,provider,follows),FriendshipController(friends,social,gate),FollowController(follows,social,gate))
            .setControllerAdvice(SocialErrors()).setCustomArgumentResolvers(resolver).build()
        fun request(uid:String,method:String,path:String,status:Int=200,body:String?=null):tools.jackson.databind.JsonNode {
            // Simulated passage of limiter time ONLY. Firestore domain time/quotas remain real.
            clock.seconds(60)
            val request=when(method){"POST"->post(path);"DELETE"->delete(path);"PATCH"->patch(path);else->get(path)}.header("Test-Actor",uid)
            if(body!=null)request.contentType("application/json").content(body)
            val response=mvc.perform(request).andReturn().response
            assertEquals(status,response.status,"$method $path: ${response.contentAsString}")
            if(status==503)assertEquals("SOCIAL_SERVICE_UNAVAILABLE",GameCatalogCodec.mapper.readTree(response.contentAsString)["code"].asString())
            return GameCatalogCodec.mapper.readTree(response.contentAsString)
        }
    }
    @Test fun `Redis down HTTP full lifecycle keeps durable graph and rejects discovery follow`()=db().use {raw->
        val a=player(raw);val b=player(raw);val c=player(raw)
        val measured=SocialMeasurements.measured(raw) as Firestore;val h=Http(measured)
        val fs=FollowService(FirestoreFriendships(raw),SocialCursor())
        fs.set(a.uid,b.publicPlayerId,true);fs.set(b.uid,a.publicPlayerId,true)
        for(query in listOf("mode=NAME&q=gue","mode=FRIEND_CODE&friendCode=${b.friendCode}")) {
            SocialMeasurements.counts.clear();h.request(a.uid,"GET","/api/v1/players/search?$query",503)
            assertEquals(0,SocialMeasurements.counts["reads"]?.get()?:0);assertEquals(0,SocialMeasurements.counts["writes"]?.get()?:0)
        }
        h.request(a.uid,"POST","/api/v1/players/${c.publicPlayerId}/follow",503);assertFalse(fs.isFollowing(a.uid,c.uid))
        val profile=h.request(a.uid,"GET","/api/v1/players/${b.publicPlayerId}/profile");assertFalse(profile.has("uid"))
        for(path in listOf("social-settings","social-summary","blocks","friends","followers","following","friend-requests?direction=INCOMING","friend-requests?direction=OUTGOING"))
            h.request(a.uid,"GET","/api/v1/player/$path")
        var id=h.request(a.uid,"POST","/api/v1/players/${b.publicPlayerId}/friend-request")["outgoingRequestId"].asString()
        h.request(b.uid,"POST","/api/v1/friend-requests/$id/accept")
        h.request(a.uid,"DELETE","/api/v1/player/friends/${b.publicPlayerId}")
        assertTrue(fs.isFollowing(a.uid,b.uid))
        id=h.request(a.uid,"POST","/api/v1/players/${b.publicPlayerId}/friend-request")["outgoingRequestId"].asString()
        h.request(a.uid,"DELETE","/api/v1/friend-requests/$id")
        id=h.request(c.uid,"POST","/api/v1/players/${b.publicPlayerId}/friend-request")["outgoingRequestId"].asString()
        h.request(b.uid,"POST","/api/v1/friend-requests/$id/decline")
        id=h.request(a.uid,"POST","/api/v1/players/${b.publicPlayerId}/friend-request")["outgoingRequestId"].asString()
        h.request(b.uid,"POST","/api/v1/friend-requests/$id/accept")
        h.request(a.uid,"POST","/api/v1/players/${b.publicPlayerId}/block")
        assertFalse(fs.isFollowing(a.uid,b.uid));assertFalse(fs.isFollowing(b.uid,a.uid))
        assertEquals(0,fs.counts(a.uid).followingCount);assertEquals(0,FriendshipService(FirestoreFriendships(raw),SocialCursor()).summary(a.uid).friendCount)
        h.request(a.uid,"GET","/api/v1/players/${b.publicPlayerId}/profile",404)
        h.request(a.uid,"DELETE","/api/v1/players/${b.publicPlayerId}/block");assertFalse(fs.isFollowing(a.uid,b.uid))
        fs.set(a.uid,b.publicPlayerId,true)
        repeat(2){h.request(a.uid,"DELETE","/api/v1/players/${b.publicPlayerId}/follow")}
        assertEquals(0,fs.counts(a.uid).followingCount)
    }
    @Test fun `Redis down privacy direction no partial writes noop and revision race`()=db().use {raw->
        val a=player(raw);val r=FirestoreSocialRepository(raw)
        r.patchPrivacy(a.uid,PrivacyPatch(true,1,presenceVisibility=SocialVisibility.EVERYONE,matchActivityVisibility=SocialVisibility.EVERYONE))
        val measured=SocialMeasurements.measured(raw) as Firestore;val h=Http(measured)
        for(fields in listOf("\"discoverableByName\":false","\"friendRequests\":\"NO_ONE\"","\"follow\":\"NO_ONE\"",
            "\"presenceVisibility\":\"FRIENDS\"","\"presenceVisibility\":\"NO_ONE\"","\"matchActivityVisibility\":\"FRIENDS\"","\"matchActivityVisibility\":\"NO_ONE\"")) {
            val before=r.privacy(a.uid)
            h.request(a.uid,"PATCH","/api/v1/player/social-settings",body="{\"revision\":${before.revision},$fields}")
            assertEquals(before.revision+1,r.privacy(a.uid).revision)
        }
        val current=r.privacy(a.uid)
        for(fields in listOf("\"discoverableByName\":true","\"friendRequests\":\"EVERYONE\"","\"follow\":\"EVERYONE\"","\"presenceVisibility\":\"FRIENDS\"","\"presenceVisibility\":\"EVERYONE\"","\"matchActivityVisibility\":\"FRIENDS\"","\"matchActivityVisibility\":\"EVERYONE\"")) {
            SocialMeasurements.counts.clear()
            h.request(a.uid,"PATCH","/api/v1/player/social-settings",503,"{\"revision\":${current.revision},$fields}")
            assertEquals(current,r.privacy(a.uid));assertEquals(0,SocialMeasurements.counts["writes"]?.get()?:0)
        }
        SocialMeasurements.counts.clear()
        h.request(a.uid,"PATCH","/api/v1/player/social-settings",body="{\"revision\":${current.revision},\"follow\":\"NO_ONE\"}")
        assertEquals(0,SocialMeasurements.counts["writes"]?.get()?:0)
        val less=r.patchPrivacy(a.uid,PrivacyPatch(revision=current.revision,friendRequests=ContactPermission.EVERYONE))
        h.request(a.uid,"PATCH","/api/v1/player/social-settings",503,"{\"revision\":${less.revision},\"friendRequests\":\"NO_ONE\",\"follow\":\"EVERYONE\"}")
        assertEquals(less,r.privacy(a.uid))
        h.request(a.uid,"PATCH","/api/v1/player/social-settings",409,"{\"revision\":${current.revision},\"follow\":\"NO_ONE\"}")
        Unit
    }
    @Test fun `Redis down acceptance retains atomic free final slot and durable send quota`()=db().use {db->
        val a=player(db);val b=player(db);val c=player(db)
        db.document("players/${a.uid}/socialCounters/current").set(com.teamfho.domino.match.MatchCodec.map(SocialCounters(friendCount=4))).get()
        val s=FriendshipService(FirestoreFriendships(db),SocialCursor())
        val requests=listOf(b,c).map{s.send(it.uid,a.publicPlayerId).outgoingRequestId!!}
        val gate=ResilientSocialRateGate(DistributedSocialGate{_,_->error("test down")});val barrier=CyclicBarrier(2);val pool=Executors.newFixedThreadPool(2)
        try {
            val results=pool.invokeAll(requests.map{r->Callable {barrier.await();runCatching{gate.check(a.uid,SocialOperation.ACCEPT);s.resolve(a.uid,r,FriendRequestStatus.ACCEPTED)}.isSuccess}}).map{it.get()}
            assertEquals(1,results.count{it});assertEquals(5,s.summary(a.uid).friendCount)
        } finally {pool.shutdownNow()}
        val sender=player(db);val recipient=player(db)
        val now=Instant.now();val quota=db.document("players/${sender.uid}/socialRate/friendRequests")
        for(times in listOf(List(5){now.minusSeconds(it.toLong())},List(20){now.minusSeconds(120+it.toLong())})) {
            quota.set(mapOf("sentAt" to times.map{it.toString()})).get()
            gate.check(sender.uid,SocialOperation.SEND)
            val failure=assertFailsWith<ExecutionException>{s.send(sender.uid,recipient.publicPlayerId)}
            assertEquals(429,assertIs<SocialFailure>(failure.cause).status)
            assertEquals(times.map{it.toString()},quota.get().get().get("sentAt"))
        }
    }
}
