package com.teamfho.domino.player

import com.google.cloud.firestore.Firestore
import com.teamfho.domino.security.FirebaseIdentity
import java.time.Clock

/** Independent emulator setup: no dependency on another test or real TEST migration. */
fun ensureEmulatorPlayer(db: Firestore, identity: FirebaseIdentity, clock: Clock): BootstrapResult {
    check(System.getenv("FIRESTORE_EMULATOR_HOST") == "127.0.0.1:18085")
    check(db.options.projectId == "demo-domino-f0")
    db.document(PlayerAliasReservations.rolloutPath)
        .set(mapOf("status" to "READY", "normalizationVersion" to 1L)).get()
    return FirestorePlayerFoundationRepository(db, clock)
        .ensure(identity, "en", GuestDisplayNames.generate())
}
