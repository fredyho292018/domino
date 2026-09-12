package com.teamfho.domino.player

import java.time.Instant

// A committed server transform has no resolved value in the transaction callback.
// Keep that fact explicit instead of inventing a time or reading documents again.
sealed interface FoundationTimestamp {
    data class Recorded(val instant: Instant) : FoundationTimestamp
    data object ServerAssigned : FoundationTimestamp
}
