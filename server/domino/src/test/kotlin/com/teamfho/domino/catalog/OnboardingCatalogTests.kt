package com.teamfho.domino.catalog

import org.junit.jupiter.api.Test
import kotlin.test.*

internal class MemoryOnboardingCatalog: OnboardingCatalogRepository {
    val documents = mutableMapOf<Int,OnboardingCatalogPublication>()
    var current: Int? = null
    var reads = 0
    var writes = 0
    override fun currentVersion() = current
    override fun read(version: Int) = documents[version].also { reads++ }
    override fun publish(publication: OnboardingCatalogPublication) {
        OnboardingCatalogValidation.validate(publication)
        OnboardingPublicationChecks.check(publication,GameCatalogCodec.map(publication),documents[publication.catalogVersion]?.let(GameCatalogCodec::map),current?.let{mapOf("publishedVersion" to it)})
        if(!documents.containsKey(publication.catalogVersion)) { documents[publication.catalogVersion]=publication; writes++ }
        if(current == null || current!! < publication.catalogVersion) current=publication.catalogVersion
    }
}
class OnboardingCatalogTests {
    private val seed = OnboardingCatalogSeed.canonical()
    @Test fun `v1 seed is idempotent and history cannot be rewritten or pointer rewound`() {
        val repo=MemoryOnboardingCatalog(); OnboardingCatalogSeed.run(repo); OnboardingCatalogSeed.run(repo)
        assertEquals(1,repo.writes);assertEquals(1,repo.current)
        repo.publish(seed.copy(catalogVersion=2));OnboardingCatalogSeed.run(repo)
        assertEquals(2,repo.current);assertEquals(seed,repo.read(1))
        assertFails {repo.publish(seed.copy(publishedAt="2026-10-01T00:00:00Z"))}
    }
    @Test fun `localized experience preserves exact Spanish and equivalent English stable keys`() {
        val es=OnboardingCatalogLocalization.localize(seed,"es");val en=OnboardingCatalogLocalization.localize(seed,"en")
        assertEquals(4,es.steps.size)
        val a=es.steps.first().questions.single();val b=en.steps.first().questions.single()
        assertEquals("DOMINO_EXPERIENCE",a.key)
        assertEquals(listOf("BEGINNER","RULES_KNOWN","STRATEGY","COMPETITIVE"),a.options.map{it.key})
        assertEquals(a.options.map{it.key},b.options.map{it.key})
        assertEquals("No sé jugar",a.options.first().title)
        assertEquals("I do not know how to play",b.options.first().title)
        assertEquals("Quiero aprender desde el principio",a.options.first().description)
        fun semantics(r:OnboardingCatalogResponse)=r.copy(locale="",steps=r.steps.map{s->s.copy(title="",description="",questions=s.questions.map{q->q.copy(title="",description="",options=q.options.map{it.copy(title="",description="")})})})
        assertEquals(semantics(es),semantics(en))
    }
    @Test fun `regional locales normalize and unsupported locales fallback`() {
        listOf("es","es-US","es-MX").forEach {assertEquals("es",OnboardingCatalogLocalization.localize(seed,it).locale)}
        listOf("en","en-US","en-GB","fr","invalid",null).forEach {assertEquals("en",OnboardingCatalogLocalization.localize(seed,it).locale)}
    }
    @Test fun `missing translation falls back for entire response but cannot be published`() {
        val p=seed.copy(translations=seed.translations + ("es" to seed.translations.getValue("es").minus("BEGINNER")))
        assertFails {OnboardingCatalogValidation.validate(p)}
        val result=OnboardingCatalogLocalization.localize(p,"es")
        assertEquals(OnboardingCatalogLocalization.localize(seed,"en"),result)
        assertFails {OnboardingCatalogLocalization.localize(p.copy(translations=emptyMap()),"es")}
    }
    @Test fun `ordering is explicit and inactive elements are filtered without deleting history`() {
        val p=seed.copy(steps=seed.steps.reversed(),questions=seed.questions.reversed(),options=seed.options.reversed().map{if(it.key=="BEGINNER")it.copy(active=false)else it})
        val result=OnboardingCatalogLocalization.localize(p,"en")
        assertEquals("EXPERIENCE_STEP",result.steps.first().key)
        assertEquals(listOf("RULES_KNOWN","STRATEGY","COMPETITIVE"),result.steps.first().questions.single().options.map{it.key})
        assertEquals(4,p.options.size)
        val inactive=p.copy(questions=p.questions.map{it.copy(active=false)},steps=p.steps.map{if(it.stage=="CONTACTS")it.copy(active=false)else it})
        val filtered=OnboardingCatalogLocalization.localize(inactive,"es")
        assertEquals(3,filtered.steps.size);assertTrue(filtered.steps.all{it.questions.isEmpty()})
    }
    @Test fun `unknown question type binding and version are rejected`() {
        val json=GameCatalogCodec.json(seed)
        assertFails {GameCatalogCodec.mapper.readValue(json.replace("SINGLE_SELECT","UNKNOWN"),OnboardingCatalogPublication::class.java)}
        assertFails {GameCatalogCodec.mapper.readValue(json.replace("EXPERIENCE_LEVEL","players/arbitrary"),OnboardingCatalogPublication::class.java)}
        assertFails {OnboardingCatalogValidation.validate(seed.copy(questions=seed.questions.map{it.copy(typeVersion=2)}))}
    }
    @Test fun `invalid references duplicates missing default translation and oversize are rejected`() {
        assertFails {OnboardingCatalogValidation.validate(seed.copy(options=seed.options+seed.options.first()))}
        assertFails {OnboardingCatalogValidation.validate(seed.copy(questions=seed.questions.drop(1)))}
        assertFails {OnboardingCatalogValidation.validate(seed.copy(translations=seed.translations.minus("en")))}
        val es=seed.translations.getValue("es") + ("BEGINNER" to OnboardingCopy("x".repeat(257)))
        assertFails {OnboardingCatalogValidation.validate(seed.copy(translations=seed.translations+("es" to es)))}
    }
    @Test fun `DTO excludes persistence and personal metadata and deferred capabilities remain unavailable`() {
        val response=OnboardingCatalogLocalization.localize(seed,"en")
        val map=GameCatalogCodec.map(response)
        assertEquals(setOf("catalogVersion","locale","requiredCapabilities","coachCatalogVersion","membershipCatalogVersion","steps"),map.keys)
        val json=GameCatalogCodec.json(response)
        listOf("publishedAt","translations","binding","uid","email","phone","token","playerId").forEach{assertFalse(json.contains("\"$it\""))}
        assertTrue(response.steps.drop(1).all{it.availability=="UNAVAILABLE"})
        assertEquals(2,response.steps.sumOf{it.questions.size})
    }
    @Test fun `reads cache immutable versions without locale contamination or writes`() {
        val repo=MemoryOnboardingCatalog();repo.publish(seed);val service=OnboardingCatalogService(repo)
        val writes=repo.writes
        assertEquals("es",service.read("es",null).locale);assertEquals("en",service.read("en",1).locale)
        assertEquals(1,repo.reads);assertEquals(writes,repo.writes)
        assertFailsWith<OnboardingCatalogFailure>{service.read("en",99)}.also{assertEquals(404,it.status)}
        assertFailsWith<OnboardingCatalogFailure>{service.read("en",0)}.also{assertEquals(400,it.status)}
    }
}
