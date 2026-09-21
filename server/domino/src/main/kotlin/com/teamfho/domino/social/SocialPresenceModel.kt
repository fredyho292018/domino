package com.teamfho.domino.social

enum class SocialPresenceState { ONLINE, OFFLINE, IN_MATCH, UNKNOWN }

/** Projection is deliberately independent of identity resolution and transport. */
fun projectPresence(actual:SocialPresenceState, general:Boolean, match:Boolean):SocialPresenceState = when {
    !general -> SocialPresenceState.UNKNOWN
    actual==SocialPresenceState.IN_MATCH && !match -> SocialPresenceState.ONLINE
    else -> actual
}
