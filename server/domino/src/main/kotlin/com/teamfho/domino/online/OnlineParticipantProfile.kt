package com.teamfho.domino.online

// Server-owned profile projection for immutable participant names.
data class OnlineParticipantProfile(val displayName:String?)

fun interface OnlineParticipantProfiles {
    fun get(uid:String):OnlineParticipantProfile
}
