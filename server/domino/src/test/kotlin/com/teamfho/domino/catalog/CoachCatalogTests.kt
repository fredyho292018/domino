package com.teamfho.domino.catalog

import org.junit.jupiter.api.Test
import kotlin.test.*

class MemoryCoachCatalog:CoachCatalogRepository {
    val versions=mutableMapOf<Int,CoachCatalogPublication>()
    var current:Int?=null
    var writes=0
    override fun currentVersion()=current
    override fun read(version:Int)=versions[version]
    override fun publish(publication:CoachCatalogPublication) {
        CoachCatalogValidation.validate(publication)
        CoachPublicationChecks.check(publication,versions[publication.catalogVersion]?.let{GameCatalogCodec.map(it)},current?.let{mapOf("publishedVersion" to it)})
        if(versions.putIfAbsent(publication.catalogVersion,publication)==null)writes++
        if(current==null || current!!<publication.catalogVersion){current=publication.catalogVersion;writes++}
    }
}
class CoachCatalogTests {
    private val seed=CoachCatalogSeed.canonical()
    @Test fun `all ten audited identities and portraits are mapped`() {
        CoachCatalogValidation.validate(seed)
        val keys=listOf("LUCIA","ELENA","AMARA","MEI","SOFIA","DAVID","MATEO","GABRIEL","LEO","OMAR")
        assertEquals(keys,seed.coaches.map{it.key})
        assertEquals(10,seed.coaches.map{it.key}.distinct().size)
        seed.coaches.forEach{assertEquals("COACH_${it.key}",it.avatarKey);assertEquals("AppShellMockCoaches/coach_${it.key.lowercase()}.png",it.storagePath);assertEquals(1,it.assetVersion)}
    }
    @Test fun `seed twice is identical with no version increment or duplicate translations`() {
        val repo=MemoryCoachCatalog();CoachCatalogSeed.run(repo);val writes=repo.writes;CoachCatalogSeed.run(repo)
        assertEquals(writes,repo.writes);assertEquals(1,repo.current);assertEquals(1,repo.versions.size)
        assertEquals(20,repo.read(1)!!.translations.values.sumOf{it.size})
    }
    @Test fun `es en preserve proper names and semantic identity`() {
        val es=CoachCatalogLocalization.localize(seed,"es");val en=CoachCatalogLocalization.localize(seed,"en")
        assertEquals("es",es.resolvedLocale);assertEquals("en",en.resolvedLocale)
        assertEquals(listOf("Lucía","Elena","Amara","Mei","Sofía","David","Mateo","Gabriel","Leo","Omar"),es.items.map{it.name})
        assertEquals(es.items.map{listOf(it.key,it.name,it.avatar,it.sortOrder,it.selectable)},en.items.map{listOf(it.key,it.name,it.avatar,it.sortOrder,it.selectable)})
        assertTrue(es.items.first().description.contains("dominó"));assertTrue(en.items.first().description.contains("dominoes"))
    }
    @Test fun `locale reuses onboarding normalization and fallback`() {
        listOf("es-US","en-US","ES","fr",null,"invalid/").forEach{assertEquals(OnboardingCatalogLocalization.locale(it),CoachCatalogLocalization.localize(seed,it).resolvedLocale)}
        val partial=seed.copy(translations=seed.translations+("es" to seed.translations.getValue("es").minus("OMAR")))
        assertEquals("en",CoachCatalogLocalization.localize(partial,"es").resolvedLocale)
    }
    @Test fun `deterministic sort uses stable key to break ties`() {
        val p=seed.copy(coaches=seed.coaches.reversed().map{it.copy(sortOrder=1)})
        assertEquals(seed.coaches.map{it.key}.sorted(),CoachCatalogLocalization.localize(p,"en").items.map{it.key})
    }
    @Test fun `normal discovery filters inactive and explicit history preserves presentation`() {
        val p=seed.copy(coaches=seed.coaches.map{if(it.key=="LUCIA")it.copy(active=false)else it})
        val repo=MemoryCoachCatalog();repo.publish(p);val service=CoachCatalogService(repo)
        assertEquals(9,service.read("es",null).items.size)
        val historical=service.read("es",1).items.single{it.key=="LUCIA"}
        assertFalse(historical.selectable);assertEquals("Lucía",historical.name)
    }
    @Test fun `duplicate keys and arbitrary asset references fail publication`() {
        assertFails{CoachCatalogValidation.validate(seed.copy(coaches=seed.coaches+seed.coaches.first()))}
        listOf("https://untrusted.invalid/portrait.png","../portrait.png","data:image/png;base64,AA").forEach { path ->
            assertFails{CoachCatalogValidation.validate(seed.copy(coaches=seed.coaches.map{it.copy(storagePath=path)}))}
        }
    }
    @Test fun `immutable content conflict fails without changing pointer`() {
        val repo=MemoryCoachCatalog();repo.publish(seed)
        assertFails{repo.publish(seed.copy(coaches=seed.coaches.map{it.copy(active=false)}))}
        assertEquals(seed,repo.read(1));assertEquals(1,repo.current)
    }
    @Test fun `new publication preserves historical identity and seed never rewinds pointer`() {
        val repo=MemoryCoachCatalog();repo.publish(seed)
        val newer=seed.copy(catalogVersion=2,coaches=seed.coaches.map{if(it.key=="LUCIA")it.copy(active=false,assetVersion=2)else it})
        repo.publish(newer);CoachCatalogSeed.run(repo)
        assertEquals(2,repo.current);assertEquals(seed,repo.read(1));assertEquals(seed.coaches.map{it.key},repo.read(2)!!.coaches.map{it.key})
    }
    @Test fun `DTO does not leak persistence or player metadata`() {
        val json=GameCatalogCodec.json(CoachCatalogLocalization.localize(seed,"es"))
        listOf("uid","playerId","email","phone","token","entitlements","translations","publishedAt","schemaVersion").forEach{assertFalse(json.contains("\"$it\""))}
    }
    @Test fun `missing invalid and unavailable versions fail safely`() {
        val repo=MemoryCoachCatalog();val s=CoachCatalogService(repo)
        assertEquals(404,assertFailsWith<CoachCatalogFailure>{s.read("en",null)}.status)
        assertEquals(400,assertFailsWith<CoachCatalogFailure>{s.read("en",0)}.status)
        repo.publish(seed);assertEquals(404,assertFailsWith<CoachCatalogFailure>{s.read("en",9)}.status)
        repo.versions[1]=seed.copy(schemaVersion=99)
        assertEquals(503,assertFailsWith<CoachCatalogFailure>{s.read("en",1)}.status)
    }
    @Test fun `onboarding v2 pins coach v1 and leaves v1 unchanged`() {
        val old=OnboardingCatalogSeed.canonical();val linked=CoachCatalogSeed.compatibleOnboarding()
        assertNull(old.coachCatalogVersion);assertEquals(1,old.catalogVersion)
        assertEquals(2,linked.catalogVersion);assertEquals(1,linked.coachCatalogVersion)
        assertEquals(old.steps,linked.steps);assertEquals(old.questions,linked.questions);assertEquals(old.options,linked.options)
        OnboardingCatalogValidation.validate(linked)
    }
}
