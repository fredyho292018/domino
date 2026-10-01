package com.teamfho.domino.catalog

import org.junit.jupiter.api.Test
import kotlin.test.*

class OnboardingCatalogV2Tests {
    @Test fun `v1 resource retains exact approved git blob`() {
        val bytes=javaClass.getResourceAsStream("/onboarding-catalog-v1.json")!!.readBytes()
        val normalized=bytes.toString(Charsets.UTF_8).replace("\r\n","\n").toByteArray(Charsets.UTF_8)
        val digest=java.security.MessageDigest.getInstance("SHA-1")
        digest.update("blob ${normalized.size}\u0000".toByteArray());digest.update(normalized)
        assertEquals("0a7a0632f7ff23f70081ee14e569d5913cef2ff7",digest.digest().joinToString(""){"%02x".format(it)})
    }
    @Test fun `v2 five ordered steps preserves previous definitions and independent coach pin`() {
        val v1=OnboardingCatalogSeed.canonical();val v2=OnboardingCatalogV2.canonical();OnboardingCatalogValidation.validate(v2)
        assertEquals(listOf("BASIC_PROFILE_STEP","EXPERIENCE_STEP","COACH_STEP","CONTACTS_STEP","MEMBERSHIP_STEP"),v2.steps.map{it.key})
        assertEquals(listOf(5,10,20,30,40),v2.steps.map{it.sortOrder})
        assertEquals(v1.steps,v2.steps.drop(1));assertEquals(v1.questions,v2.questions.drop(5));assertEquals(v1.options,v2.options)
        assertEquals(1,v2.coachCatalogVersion);assertTrue(v2.steps.first().required);assertFalse(v2.steps.first().skippable)
    }
    @Test fun `v2 seed idempotency and v1 retained`() {
        val repo=MemoryOnboardingCatalog();OnboardingCatalogSeed.run(repo);OnboardingCatalogV2.run(repo)
        val first=repo.read(2);OnboardingCatalogV2.run(repo)
        assertEquals(first,repo.read(2));assertEquals(OnboardingCatalogSeed.canonical(),repo.read(1));assertEquals(2,repo.currentVersion())
    }
    @Test fun `es en catalog semantic parity and no private answers`() {
        val p=OnboardingCatalogV2.canonical();val es=OnboardingCatalogLocalization.localize(p,"es-US");val en=OnboardingCatalogLocalization.localize(p,"en-US")
        assertEquals("es",es.locale);assertEquals("en",en.locale);assertEquals(2,es.catalogVersion)
        assertEquals(es.steps.map{it.key to it.questions.map{q->q.key to q.type}},en.steps.map{it.key to it.questions.map{q->q.key to q.type}})
        assertEquals("Nombre",es.steps.first().questions.first().title);assertEquals("First name",en.steps.first().questions.first().title)
        assertTrue(p.options.none{it.questionKey in setOf("COUNTRY","PREFERRED_LANGUAGE")})
        val json=GameCatalogCodec.json(es)
        listOf("firstName","lastName","preferredLocale","timeZone","uid","textValue").forEach{assertFalse(json.contains("\"$it\""))}
    }
    @Test fun `basic profile type capability not accepted as v1`() {
        assertFails{OnboardingCatalogValidation.validate(OnboardingCatalogV2.canonical().copy(catalogVersion=1))}
        assertFails{OnboardingCatalogValidation.validate(OnboardingCatalogV2.canonical().copy(requiredCapabilities=OnboardingCatalogSeed.canonical().requiredCapabilities))}
    }
}
