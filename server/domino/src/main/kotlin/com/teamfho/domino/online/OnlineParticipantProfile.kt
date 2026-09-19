package com.teamfho.domino.online

// Server-owned profile projection for immutable participant names and existing history test marker.
data class OnlineParticipantProfile(val displayName:String?,val validationData:Boolean)

fun interface OnlineParticipantProfiles {
    fun get(uid:String):OnlineParticipantProfile
}
