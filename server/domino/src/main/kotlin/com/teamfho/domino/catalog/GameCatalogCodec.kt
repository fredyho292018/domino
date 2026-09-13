package com.teamfho.domino.catalog

import tools.jackson.databind.json.JsonMapper
import tools.jackson.module.kotlin.KotlinModule
import tools.jackson.databind.DeserializationFeature
import tools.jackson.databind.MapperFeature
import java.security.MessageDigest

object GameCatalogCodec {
    val mapper = JsonMapper.builder().addModule(KotlinModule.Builder().build())
        .disable(MapperFeature.ALLOW_COERCION_OF_SCALARS)
        .enable(DeserializationFeature.FAIL_ON_NULL_FOR_PRIMITIVES)
        .disable(DeserializationFeature.ACCEPT_FLOAT_AS_INT)
        .enable(DeserializationFeature.FAIL_ON_UNKNOWN_PROPERTIES).build()
    fun json(value: Any?): String = mapper.writeValueAsString(value)
    fun <T> decode(value: Any, type: Class<T>): T = mapper.readValue(json(value), type)
    @Suppress("UNCHECKED_CAST")
    fun map(value: Any): Map<String, Any> = mapper.readValue(json(value), Map::class.java) as Map<String, Any>
    // Ordinal key sort, UTF-8 compact JSON, array order preserved; metadata excluded.
    private fun sorted(value: Any?): Any? = when (value) {
        is Map<*, *> -> value.entries.associate { it.key.toString() to sorted(it.value) }.toSortedMap()
        is List<*> -> value.map(::sorted)
        else -> value
    }
    fun semantic(value: Any): String = json(sorted(map(value).filterKeys { it !in setOf("createdAt", "updatedAt", "contentHash") }))
    fun documentContent(value: Any): String = json(sorted(map(value).filterKeys { it !in setOf("createdAt", "updatedAt") }))
    fun hash(rule: RuleSetVersion): String = MessageDigest.getInstance("SHA-256")
        .digest(semantic(rule).toByteArray(Charsets.UTF_8)).joinToString("") { "%02x".format(it) }
    // Firestore does not support arrays directly inside arrays. API retains seatTeams[][];
    // persisted seatTeams is an array of {members:[...]}, including embedded publication modes.
    fun storage(value: Any?): Any? = when (value) {
        is Map<*, *> -> value.entries.associate { (k,v) -> k.toString() to
            if (k == "seatTeams") (v as List<*>).map { mapOf("members" to it) } else storage(v) }
        is List<*> -> value.map(::storage)
        else -> value
    }
    fun restored(value: Any?): Any? = when (value) {
        is Map<*, *> -> value.entries.associate { (k,v) -> k.toString() to
            if (k == "seatTeams") (v as List<*>).map { (it as Map<*, *>)["members"] } else restored(v) }
        is List<*> -> value.map(::restored)
        else -> value
    }
}
