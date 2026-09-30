package com.teamfho.domino.security

data class FirebaseIdentity(val uid: String, val isAnonymous: Boolean,
    val isEmailVerified: Boolean = false, val signInProvider: String = "unknown",
    val hasPasswordProvider: Boolean = false)
