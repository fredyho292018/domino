package com.teamfho.domino.catalog

import org.junit.jupiter.api.Test
import kotlin.test.*
import com.teamfho.domino.entitlement.*
import java.time.Instant

class MemoryMembershipCatalog:MembershipCatalogRepository {
    val docs=linkedMapOf<Int,MembershipCatalogPublication>()
    val policies=linkedMapOf<Int,Map<String,Any>>()
    val trials=linkedMapOf<Int,Map<String,Any>>()
    var pointer:Int?=null
    override fun currentVersion()=pointer
    override fun read(version:Int)=docs[version]
    override fun publish(publication:MembershipCatalogPublication) {
        MembershipCatalogValidation.validate(publication)
        MembershipPublicationChecks.check(publication,docs[publication.catalogVersion]?.let{GameCatalogCodec.map(it)},pointer?.let{mapOf("publishedVersion" to it)})
        MembershipPublicationChecks.policy(publication.targetPolicy,policies[publication.targetPolicy.version])
        MembershipPublicationChecks.policy(publication.trialPolicy,trials[publication.trialPolicy.version])
        docs.putIfAbsent(publication.catalogVersion,publication)
        policies.putIfAbsent(publication.targetPolicy.version,GameCatalogCodec.map(publication.targetPolicy))
        trials.putIfAbsent(publication.trialPolicy.version,GameCatalogCodec.map(publication.trialPolicy))
        pointer=maxOf(pointer?:0,publication.catalogVersion)
    }
}
class MembershipCatalogTests {
    private val p=MembershipCatalogSeed.canonical()
    private fun included(key:String)=p.planFeatures.filter{it.planKey==key && it.included}.map{it.featureKey}.toSet()
    @Test fun `five plans eight features forty explicit relations`() {MembershipCatalogValidation.validate(p);assertEquals(5,p.plans.size);assertEquals(8,p.features.size);assertEquals(40,p.planFeatures.size)}
    @Test fun `gold exact commercial set`() {assertEquals(setOf("PUZZLES","LESSONS","COACH_GAMES","BOTS","NO_ADS"),included("GOLD"))}
    @Test fun `platinum exact cumulative set`() {assertEquals(included("GOLD")+"GAME_REVIEW",included("PLATINUM"))}
    @Test fun `diamond exact cumulative set`() {assertEquals(included("PLATINUM")+setOf("MOVE_EXPLANATIONS","ADVANCED_STATS"),included("DIAMOND"))}
    @Test fun `free commercial set empty family equals diamond`() {assertTrue(included("FREE").isEmpty());assertEquals(included("DIAMOND"),included("FRIENDS_AND_FAMILY"))}
    @Test fun `spanish and english semantic parity`() {
        val es=MembershipCatalogLocalization.localize(p,"es-US");val en=MembershipCatalogLocalization.localize(p,"en-US")
        assertEquals("es",es.resolvedLocale);assertEquals("en",en.resolvedLocale)
        assertEquals(es.plans.map{it.key to it.features},en.plans.map{it.key to it.features});assertEquals(es.hierarchy,en.hierarchy)
        assertNotEquals(es.features.first().name,en.features.first().name)
    }
    @Test fun `locale normalization and fallback reuse existing rules`() {
        for(locale in listOf(null,"fr","es-US","en-US"))assertEquals(OnboardingCatalogLocalization.locale(locale),MembershipCatalogLocalization.localize(p,locale).resolvedLocale)
        assertEquals("en",MembershipCatalogLocalization.localize(p.copy(featureTranslations=p.featureTranslations-"es"),"es").resolvedLocale)
    }
    @Test fun `display ordering independent of hierarchy`() {
        val r=MembershipCatalogLocalization.localize(p,"en")
        assertEquals(listOf("DIAMOND","PLATINUM","GOLD","FRIENDS_AND_FAMILY","FREE"),r.plans.map{it.key})
        assertEquals(listOf("FREE","GOLD","PLATINUM","DIAMOND"),r.hierarchy)
        assertEquals("MULTI_PLAYER",r.plans.single{it.key=="FRIENDS_AND_FAMILY"}.productKind)
    }
    @Test fun `family metadata only trial disabled`() {assertEquals(MembershipFamilyPolicy(),p.familyPolicy);assertFalse(p.trialPolicy.familyTrialEnabled);assertEquals("PREMIUM_LEGACY",p.trialPolicy.product)}
    @Test fun `invitation expiry configurable and bounded`() {MembershipCatalogValidation.validate(p.copy(familyPolicy=p.familyPolicy.copy(invitationExpirationDays=14)));assertFails{MembershipCatalogValidation.validate(p.copy(familyPolicy=p.familyPolicy.copy(invitationExpirationDays=0)))}}
    @Test fun `seed idempotent no version increment`() {val r=MemoryMembershipCatalog();repeat(3){MembershipCatalogSeed.run(r)};assertEquals(1,r.docs.size);assertEquals(1,r.currentVersion());assertEquals(1,r.policies.size);assertEquals(1,r.trials.size)}
    @Test fun `current and historical versions and monotonic reseed`() {val r=MemoryMembershipCatalog();r.publish(p);r.publish(p.copy(catalogVersion=2));r.publish(p);assertEquals(2,MembershipCatalogService(r).read(null,null).catalogVersion);assertEquals(1,MembershipCatalogService(r).read(null,1).catalogVersion)}
    @Test fun `unknown and invalid version`() {val s=MembershipCatalogService(MemoryMembershipCatalog());assertEquals(404,assertFailsWith<MembershipCatalogFailure>{s.read(null,99)}.status);assertEquals(400,assertFailsWith<MembershipCatalogFailure>{s.read(null,0)}.status)}
    @Test fun `inactive hidden current retained historical nonpurchasable`() {val changed=p.copy(plans=p.plans.map{it.copy(active=it.key!="GOLD")});assertEquals(4,MembershipCatalogLocalization.localize(changed,null).plans.size);val gold=MembershipCatalogLocalization.localize(changed,null,true).plans.single{it.key=="GOLD"};assertFalse(gold.active);assertTrue(gold.billingProducts.none{it.purchasable})}
    @Test fun `immutable publication rejects changed content`() {val r=MemoryMembershipCatalog();r.publish(p);assertFails{r.publish(p.copy(publishedAt="2026-10-01T00:00:00Z"))}}
    @Test fun `policy version cannot silently change with presentation version`() {val r=MemoryMembershipCatalog();r.publish(p);val target=p.targetPolicy.copy(plans=p.targetPolicy.plans.mapValues{(_,v)->v.copy(limits=v.limits+("FRIENDS_MAX" to MembershipQuota(false,101))) });assertFails{r.publish(p.copy(catalogVersion=2,targetPolicy=target))}}
    @Test fun `target matrix exact independent serialized configuration`() {
        val decoded=MembershipCatalogValidation.decode(GameCatalogCodec.map(p),1);assertFalse(decoded.targetPolicy.active)
        val free=setOf("PUBLIC_DUEL","PUBLIC_PARTNERS","FOLLOW_PLAYER","FRIENDS","FRIEND_REQUESTS")
        val gold=free+setOf("PARTY_CREATE","PARTY_INVITE","PRIVATE_DUEL","PRIVATE_PARTNERS","CHOOSE_2V2_PARTNER","PARTY_MATCHMAKING","PREMIUM_THEMES")
        val platinum=gold+setOf("FULL_HISTORY","FULL_REPLAY")
        val expected=mapOf("FREE" to free,"GOLD" to gold,"PLATINUM" to platinum,"DIAMOND" to platinum+"ADVANCED_STATS","FRIENDS_AND_FAMILY" to platinum+"ADVANCED_STATS")
        expected.forEach{(key,features)->val t=decoded.targetPolicy.plans.getValue(key);assertEquals(features,t.capabilities)
            assertEquals(if(key=="FREE")5 else 100,t.limits.getValue("FRIENDS_MAX").maximum)
            val full=key !in setOf("FREE","GOLD");assertEquals(MembershipQuota(full,if(full)null else 10),t.limits.getValue("HISTORY_MAX"));assertEquals(MembershipQuota(full,if(full)null else 3),t.limits.getValue("REPLAY_MAX"))}
    }
    @Test fun `sixteen unconfigured mappings no prices or fake ids`() {assertEquals(16,p.billingProducts.size);assertTrue(p.billingProducts.all{!it.active && it.storeProductId==null});val r=MembershipCatalogLocalization.localize(p,null);assertTrue(r.plans.flatMap{it.billingProducts}.none{it.purchasable});assertFalse(GameCatalogCodec.json(r).contains("3.99"));assertEquals("APPLE_GOOGLE_STORE",r.priceAuthority)}
    @Test fun `billing periods share plan feature set`() {val r=MembershipCatalogLocalization.localize(p,null);r.plans.filter{it.key!="FREE"}.forEach{assertEquals(MembershipBillingPeriod.entries.toSet(),it.billingProducts.map{b->b.billingPeriod}.toSet());assertTrue(it.billingProducts.all{b->b.planKey==it.key})}}
    @Test fun `active mapping requires configured id`() {assertFails{MembershipCatalogValidation.validate(p.copy(billingProducts=p.billingProducts.map{it.copy(active=true)}))}}
    @Test fun `reject duplicate matrix missing matrix unknown features and unresolved kind`() {
        assertFails{MembershipCatalogValidation.validate(p.copy(planFeatures=p.planFeatures.dropLast(1)))}
        assertFails{MembershipCatalogValidation.validate(p.copy(planFeatures=p.planFeatures.dropLast(1)+p.planFeatures.first()))}
        assertFails{MembershipCatalogValidation.validate(p.copy(features=p.features+MembershipFeature("FUTURE",MembershipFeatureKind.BOOLEAN_CAPABILITY,"icon",99)))}
        assertFails{GameCatalogCodec.mapper.readValue(GameCatalogCodec.json(p).replace("BOOLEAN_CAPABILITY","UNRESOLVED"),MembershipCatalogPublication::class.java)}
    }
    @Test fun `reject active target and missing translations`() {assertFails{MembershipCatalogValidation.validate(p.copy(targetPolicy=p.targetPolicy.copy(active=true)))};assertFails{MembershipCatalogValidation.validate(p.copy(planTranslations=p.planTranslations-"es"))}}
    @Test fun `quota validation rejects contradictory values`() {assertFails{MembershipCatalogValidation.validate(p.copy(targetPolicy=p.targetPolicy.copy(plans=p.targetPolicy.plans+("FREE" to p.targetPolicy.plans.getValue("FREE").copy(limits=mapOf("FRIENDS_MAX" to MembershipQuota(true,5)))))))}}
    @Test fun `legacy resolver unchanged by catalog reads`() {
        val now=Instant.parse("2026-09-30T00:00:00Z");val policy=SubscriptionPolicy();val states=listOf(EntitlementState(),EntitlementState(grants=listOf(EntitlementGrant("fixture",EntitlementSource.ADMIN_GRANT,validFrom=now.minusSeconds(1),validUntil=now.plusSeconds(600),createdAt=now,policyVersion=1,reason="fixture",grantedBy="fixture"))))
        val before=states.map{EntitlementResolver.resolve(it,policy,now)};val r=MemoryMembershipCatalog();MembershipCatalogSeed.run(r);MembershipCatalogService(r).read("es",null)
        assertEquals(before,states.map{EntitlementResolver.resolve(it,policy,now)});assertEquals(listOf(Plan.FREE,Plan.PREMIUM),before.map{it.plan})
    }
}
