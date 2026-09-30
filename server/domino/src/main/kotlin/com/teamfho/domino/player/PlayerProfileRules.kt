package com.teamfho.domino.player

import java.text.Normalizer
import java.time.ZoneId
import java.util.Locale
import java.util.regex.Pattern

class ProfileValidationException(val code: String) : RuntimeException(code)

object PlayerProfileRules {
    private val grapheme = Pattern.compile("\\X")
    fun name(value: String?): String? {
        if (value == null) return null
        val normalized = Normalizer.normalize(value.trim(), Normalizer.Form.NFC)
        val count = grapheme.matcher(normalized).results().count()
        if (count !in 1..80 || normalized.codePoints().anyMatch {
                Character.isISOControl(it) || it in 0x202A..0x202E || it in 0x2066..0x2069 || it == 0x200E || it == 0x200F || it == 0x061C
            }) throw ProfileValidationException("PROFILE_FIELD_INVALID")
        return normalized
    }
    fun country(value: String?): String? {
        if (value == null) return null
        val normalized = value.trim().uppercase(Locale.ROOT)
        if (normalized !in Locale.getISOCountries().toSet()) throw ProfileValidationException("PROFILE_FIELD_INVALID")
        return normalized
    }
    fun locale(value: String): String {
        if (!Regex("[A-Za-z]{2}(?:-[A-Za-z]{2})?").matches(value)) throw UnsupportedPlayerLanguageException()
        return value.substringBefore('-').lowercase(Locale.ROOT).also {
            if (it !in setOf("en", "es")) throw UnsupportedPlayerLanguageException()
        }
    }
    fun timeZone(value: String?): String? {
        if (value != null && (value.length > 128 || (value != "UTC" && '/' !in value) || value !in ZoneId.getAvailableZoneIds()))
            throw ProfileValidationException("TIME_ZONE_INVALID")
        return value
    }
}
