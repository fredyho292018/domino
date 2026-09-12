package com.teamfho.domino.player

import java.security.SecureRandom

object GuestDisplayNames {
    private val random = SecureRandom()
    private const val alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"
    // The caller creates one candidate before ensure(), never inside a transaction callback.
    fun generate(): String = "Guest-" + CharArray(8) { alphabet[random.nextInt(alphabet.length)] }.concatToString()
}
