package com.teamfho.domino.player

import com.google.cloud.Timestamp
import com.google.cloud.firestore.Firestore
import com.google.cloud.firestore.Transaction
import com.teamfho.domino.match.MatchCodec
import com.teamfho.domino.security.FirebaseIdentity
import java.time.Instant
import java.util.concurrent.TimeUnit

internal object FoundationDocumentCodec {
    fun encode(value: Any): Map<String, Any> = MatchCodec.map(value).mapValues { (key, v) ->
        if (key in setOf("startedAt", "completedAt", "updatedAt") && v is String) {
            val time = Instant.parse(v); Timestamp.ofTimeSecondsAndNanos(time.epochSecond, time.nano)
        } else v
    }
    fun <T> decode(data: Map<String, Any>, type: Class<T>): T = MatchCodec.read(data.mapValues { (_, v) ->
        if (v is Timestamp) Instant.ofEpochSecond(v.seconds, v.nanos.toLong()).toString() else v
    }, type)
}

// Explicit boundary required; no scheduler, public endpoint, broad scan or automatic real backfill.
class FirestoreOnboardingFoundation(private val db: Firestore, private val boundary: OnboardingRolloutBoundary) {
    internal fun prepare(tx: Transaction, player: Player, at: Instant): () -> Unit {
        val root = "players/${player.uid}"
        val pref = db.document("$root/preferences/current")
        val domino = db.document("$root/dominoProfile/current")
        val onboarding = db.document("$root/onboarding/current")
        val p = tx.get(pref).get(); val d = tx.get(domino).get(); val o = tx.get(onboarding).get()
        if (p.exists()) FoundationDocumentCodec.decode(p.data!!, PlayerPreferences::class.java)
        if (d.exists()) FoundationDocumentCodec.decode(d.data!!, DominoProfile::class.java)
        if (o.exists()) FoundationDocumentCodec.decode(o.data!!, PlayerOnboarding::class.java)
        val created = when (val time = player.createdAt) {
            is FoundationTimestamp.Recorded -> time.instant
            FoundationTimestamp.ServerAssigned -> at
        }
        val state = OnboardingInitialization.state(created, boundary, at)
        return {
            if (!p.exists()) tx.create(pref, FoundationDocumentCodec.encode(PlayerPreferences(player.language, updatedAt = at)))
            if (!d.exists()) tx.create(domino, FoundationDocumentCodec.encode(DominoProfile(updatedAt = at)))
            if (!o.exists()) tx.create(onboarding, FoundationDocumentCodec.encode(state))
        }
    }

    fun initialize(identity: FirebaseIdentity, at: Instant) {
        require(identity.uid.isNotBlank() && identity.uid.length <= 128 && '/' !in identity.uid && identity.uid !in setOf(".", ".."))
        db.runTransaction { tx ->
            val doc = tx.get(db.document("players/${identity.uid}")).get()
            check(doc.exists())
            val player = FirestoreFoundationMapping.player(doc.data!!, identity.uid)
            prepare(tx, player, at).invoke()
            null
        }.get(30, TimeUnit.SECONDS)
    }
}
