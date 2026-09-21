package com.teamfho.domino.social

import com.google.cloud.firestore.*
import com.teamfho.domino.player.FirestorePlayerFoundationRepository
import com.teamfho.domino.security.FirebaseIdentity
import org.junit.jupiter.api.Tag
import org.junit.jupiter.api.Test
import org.junit.jupiter.api.condition.EnabledIfEnvironmentVariable
import org.springframework.data.redis.connection.lettuce.LettuceConnectionFactory
import org.springframework.data.redis.core.StringRedisTemplate
import java.time.Clock
import java.util.UUID
import java.util.concurrent.TimeUnit
import kotlin.test.*

@Tag("EMULATOR")
@EnabledIfEnvironmentVariable(named="DOMINO_A3_REDIS_TESTS",matches="true")
class SocialInvalidationDistributedEmulatorTests {
    private fun await(expected:()->Boolean){val end=System.nanoTime()+TimeUnit.SECONDS.toNanos(15);while(!expected()&&System.nanoTime()<end)Thread.sleep(25);assertTrue(expected())}
    @Test fun `two instances share emulator and Redis with lost publication durable recovery`() {
        check(System.getenv("FIRESTORE_EMULATOR_HOST")=="127.0.0.1:18085")
        val db=FirestoreOptions.newBuilder().setProjectId("demo-domino-f0").setEmulatorHost("127.0.0.1:18085").setCredentials(FirestoreOptions.EmulatorCredentials()).build().service
        val factory=LettuceConnectionFactory("127.0.0.1",16379);factory.afterPropertiesSet();factory.start();val redis=StringRedisTemplate(factory)
        try {
            fun player():PublicPlayerIdentity {val uid="a3distributed-${UUID.randomUUID()}";FirestorePlayerFoundationRepository(db,Clock.systemUTC()).ensure(FirebaseIdentity(uid,true),"en","Guest-ABCDEFGH");return PublicPlayerIdentityService(FirestoreSocialRepository(db)).ensure(uid)}
            val a=player();val b=player();val pair=SocialPairIdentity.id(a.uid,b.uid)
            val reader=SocialAuthorizationReader {viewer,target->db.runTransaction {tx->
                val revision=tx.get(db.document("socialPairs/$pair")).get().getLong("authorizationRevision")?:0
                val privacy=tx.get(db.document("players/$target/socialSettings/current")).get()
                val ab=tx.get(db.document("players/$viewer/blocks/$target")).get().exists()
                val ba=tx.get(db.document("players/$target/blocks/$viewer")).get().exists()
                val friends=tx.get(db.document("friendships/$pair")).get().exists()
                val allowed=!ab && !ba && (privacy.getString("presenceVisibility")=="EVERYONE" || privacy.getString("presenceVisibility")=="FRIENDS" && friends)
                SocialAuthorizationSnapshot(allowed,revision,privacy.getLong("revision")?:1)
            }.get(5,TimeUnit.SECONDS)}
            val right=SocialInvalidationRuntime({db},{factory},{redis})
            val left=SocialInvalidationRuntime({db},{factory},{redis})
            try {
                left.start();right.start()
                val social=FirestoreSocialRepository(db,left)
                social.patchPrivacy(b.uid,PrivacyPatch(revision=1,presenceVisibility=SocialVisibility.EVERYONE))
                val h=right.index.register("b",a.uid,b.uid,reader);val local=left.index.register("a",a.uid,b.uid,reader)
                await{h.canDeliver()&&local.canDeliver()}
                val candidate=SocialCandidate(b.uid,PublicPlayerProfile(b.publicPlayerId,b.friendCode,"Guest"),"")
                social.block(a.uid,candidate,true);assertFalse(local.canDeliver());await{!h.canDeliver() && h.pairRevision==1L}
                social.block(a.uid,candidate,false);await{h.canDeliver()}
                social.patchPrivacy(b.uid,PrivacyPatch(revision=2,presenceVisibility=SocialVisibility.FRIENDS));await{!h.canDeliver()}
                val friends=FriendshipService(FirestoreFriendships(db,left),SocialCursor())
                val request=friends.send(a.uid,b.publicPlayerId).outgoingRequestId!!;friends.resolve(b.uid,request,FriendRequestStatus.ACCEPTED);await{h.canDeliver()}
                friends.remove(a.uid,b.publicPlayerId);assertFalse(local.canDeliver());await{!h.canDeliver() && h.pairRevision==4L}
                // Disconnect both fast paths. No publish/subscriber dependency for the safety path.
                left.close();right.close()
            } finally {left.close();right.close()}
            val isolated=SocialInvalidationRuntime({db},{null},{null})
            try {
                val social=FirestoreSocialRepository(db)
                social.patchPrivacy(b.uid,PrivacyPatch(revision=3,presenceVisibility=SocialVisibility.EVERYONE))
                isolated.start();val h=isolated.index.register("isolated",a.uid,b.uid,reader);await{h.canDeliver()}
                social.patchPrivacy(b.uid,PrivacyPatch(revision=4,presenceVisibility=SocialVisibility.NO_ONE))
                await{!h.canDeliver() && h.privacyRevision==5L}
            }finally{isolated.close()}
        }finally{factory.destroy();db.close()}
    }
}
