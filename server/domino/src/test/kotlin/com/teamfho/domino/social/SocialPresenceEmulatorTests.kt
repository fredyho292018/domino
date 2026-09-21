package com.teamfho.domino.social

import com.google.cloud.firestore.*
import com.teamfho.domino.player.FirestorePlayerFoundationRepository
import com.teamfho.domino.security.FirebaseIdentity
import org.junit.jupiter.api.Tag
import org.junit.jupiter.api.Test
import java.time.Clock
import java.util.UUID
import kotlin.test.*

@Tag("EMULATOR")
class SocialPresenceEmulatorTests {
    private fun db():Firestore {
        check(System.getenv("FIRESTORE_EMULATOR_HOST")=="127.0.0.1:18085")
        return FirestoreOptions.newBuilder().setProjectId("demo-domino-f0").setEmulatorHost("127.0.0.1:18085")
            .setCredentials(FirestoreOptions.EmulatorCredentials()).build().service
    }
    private fun player(db:Firestore):PublicPlayerIdentity {
        val uid="s14b-${UUID.randomUUID()}"
        FirestorePlayerFoundationRepository(db,Clock.systemUTC()).ensure(FirebaseIdentity(uid,true),"en","Guest-ABCDEFGH")
        return PublicPlayerIdentityService(FirestoreSocialRepository(db)).ensure(uid)
    }
    @Test fun `follow never authorizes friends presence and activity privacy is independent`()=db().use { db->
        val a=player(db);val b=player(db);val reader=SocialPresenceAuthorization{db}
        assertFalse(reader.read(a.uid,b.uid).allowed)
        FollowService(FirestoreFriendships(db),SocialCursor()).set(a.uid,b.publicPlayerId,true)
        assertFalse(reader.read(a.uid,b.uid).allowed)
        val friends=FriendshipService(FirestoreFriendships(db),SocialCursor())
        val request=friends.send(a.uid,b.publicPlayerId).outgoingRequestId!!
        friends.resolve(b.uid,request,FriendRequestStatus.ACCEPTED)
        assertTrue(reader.read(a.uid,b.uid).allowed)
        val repo=FirestoreSocialRepository(db)
        repo.patchPrivacy(b.uid,PrivacyPatch(revision=1,matchActivityVisibility=SocialVisibility.NO_ONE))
        val permission=reader.read(a.uid,b.uid)
        assertTrue(permission.allowed);assertFalse(permission.matchActivityAllowed)
        assertEquals(SocialPresenceState.ONLINE,projectPresence(SocialPresenceState.IN_MATCH,permission.allowed,permission.matchActivityAllowed))
        repo.patchPrivacy(b.uid,PrivacyPatch(revision=2,presenceVisibility=SocialVisibility.NO_ONE,matchActivityVisibility=SocialVisibility.EVERYONE))
        assertFalse(reader.read(a.uid,b.uid).allowed)
    }
    @Test fun `both block directions test accounts and missing players are denied`()=db().use { db->
        val a=player(db);val b=player(db);val repo=FirestoreSocialRepository(db);val reader=SocialPresenceAuthorization{db}
        repo.patchPrivacy(b.uid,PrivacyPatch(revision=1,presenceVisibility=SocialVisibility.EVERYONE))
        assertTrue(reader.read(a.uid,b.uid).allowed)
        fun candidate(p:PublicPlayerIdentity)=SocialCandidate(p.uid,PublicPlayerProfile(p.publicPlayerId,p.friendCode,"Guest"),"")
        repo.block(a.uid,candidate(b),true);assertFalse(reader.read(a.uid,b.uid).allowed)
        repo.block(a.uid,candidate(b),false);assertTrue(reader.read(a.uid,b.uid).allowed)
        repo.block(b.uid,candidate(a),true);assertFalse(reader.read(a.uid,b.uid).allowed)
        repo.block(b.uid,candidate(a),false)
        db.document("developmentTestAccounts/${b.uid}").set(mapOf("isTestAccount" to true)).get()
        assertFalse(reader.read(a.uid,b.uid).allowed)
        assertFalse(reader.read(a.uid,a.uid).allowed)
        assertFalse(reader.read(a.uid,"missing-${UUID.randomUUID()}").allowed)
    }
    @Test fun `authorization read cost has zero writes`()=db().use { raw->
        val a=player(raw);val b=player(raw)
        val measured=SocialMeasurements.measured(raw) as Firestore
        SocialMeasurements.counts.clear()
        SocialPresenceAuthorization{measured}.read(a.uid,b.uid)
        val reads=SocialMeasurements.counts["reads"]?.get()?:0
        val writes=SocialMeasurements.counts["writes"]?.get()?:0
        println("S14B_AUTHORIZATION_READS=$reads WRITES=$writes")
        assertEquals(9,reads.toInt());assertEquals(0,writes.toInt())
    }
    @org.junit.jupiter.params.ParameterizedTest
    @org.junit.jupiter.params.provider.ValueSource(ints=[1,20,50])
    fun `measure public resolution and consistent permissions for bounded desired sets`(count:Int)=db().use { raw->
        val viewer=player(raw);val targets=(1..count).map{player(raw)}
        val measured=SocialMeasurements.measured(raw) as Firestore;val reader=SocialPresenceAuthorization{measured}
        SocialMeasurements.counts.clear()
        targets.forEach{p->assertEquals(p.uid,reader.resolve(p.publicPlayerId));reader.read(viewer.uid,p.uid)}
        val reads=SocialMeasurements.counts["reads"]?.get()?:0
        val writes=SocialMeasurements.counts["writes"]?.get()?:0
        println("S14B_SUBSCRIBE_$count READS=$reads WRITES=$writes")
        assertEquals(12*count,reads.toInt());assertEquals(0,writes.toInt())
    }
}
