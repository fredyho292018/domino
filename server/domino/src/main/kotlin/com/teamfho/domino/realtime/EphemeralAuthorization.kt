package com.teamfho.domino.realtime

/** A revocable local A.3 generation, never serialized. Construction is module-internal.
 * tryCommit linearizes with its issuing authority's revocation. No I/O is allowed
 * in either callback and the authority lock is released before socket I/O starts. */
class EphemeralAuthorization internal constructor(
    private val commit: () -> Boolean,
    private val subscribe: (() -> Unit) -> AutoCloseable,
) {
    internal fun tryCommit(): Boolean = commit()
    internal fun onRevoked(action: () -> Unit): AutoCloseable = subscribe(action)
}
