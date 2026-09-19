package com.teamfho.domino

import com.teamfho.domino.validation.RealFirestoreGuard
import org.junit.jupiter.api.Test
import org.junit.jupiter.api.Tag
import kotlin.test.*

@Tag("UNIT")
class RealFirestoreGuardTests {
    @Test fun `all real JVM entry points refuse before opening services`() {
        org.junit.jupiter.api.Assumptions.assumeTrue(System.getenv("DOMINO_REAL_FIRESTORE_TESTS")!="true")
        val entries=listOf(
            com.teamfho.domino.match.MatchFirestoreValidation::main,
            com.teamfho.domino.online.OnlineRealValidation::main,
            com.teamfho.domino.online.OnlineUnityValidation::main,
            com.teamfho.domino.online.OnlineTurnRealValidation::main,
            com.teamfho.domino.online.TwoUnityMatchInspection::main,
            com.teamfho.domino.catalog.GameCatalogSeed::main,
            com.teamfho.domino.catalog.GameCatalogV2Publisher::main,
            com.teamfho.domino.catalog.GameCatalogV3Publisher::main,
            com.teamfho.domino.economy.reward.MonetizationPolicySeed::main)
        entries.forEach {entry->assertTrue(assertFailsWith<IllegalStateException>{entry(emptyArray())}.message!!.startsWith("REAL_FIRESTORE_DISABLED"))}
        // Pending M5 tools are not part of the F0 checkpoint. Validate their guards when present,
        // without making this test depend on publishing that unrelated work.
        for(name in listOf("com.teamfho.domino.online.PartnersRealInspection","com.teamfho.domino.catalog.GameCatalogV4Publisher")) {
            val type=try {Class.forName(name)}catch(_:ClassNotFoundException){continue}
            val error=assertFailsWith<java.lang.reflect.InvocationTargetException>{type.getMethod("main",Array<String>::class.java).invoke(null,emptyArray<String>())}
            assertTrue(error.cause is IllegalStateException && error.cause!!.message!!.startsWith("REAL_FIRESTORE_DISABLED"))
        }
    }
    @Test fun `missing or ambiguous opt in is rejected before starting services`() {
        for(value in listOf(null,"","false","TRUE","1"))assertFailsWith<IllegalStateException>{
            RealFirestoreGuard.requireOptIn(value?.let{mapOf("DOMINO_REAL_FIRESTORE_TESTS" to it)}?:emptyMap())
        }
        RealFirestoreGuard.requireOptIn(mapOf("DOMINO_REAL_FIRESTORE_TESTS" to "true"))
    }
}
