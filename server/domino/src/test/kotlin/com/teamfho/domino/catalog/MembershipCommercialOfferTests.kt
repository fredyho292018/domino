package com.teamfho.domino.catalog

import org.junit.jupiter.api.Test
import org.junit.jupiter.params.ParameterizedTest
import org.junit.jupiter.params.provider.CsvSource
import kotlin.test.*

class MembershipCommercialOfferTests {
    private val catalog=MembershipCatalogSeed.canonical()
    private val seed=MembershipCommercialOfferSeed.approvedUs()
    @ParameterizedTest
    @CsvSource("GOLD,MONTHLY,699","GOLD,YEARLY,4999","PLATINUM,MONTHLY,1099","PLATINUM,YEARLY,7999",
        "DIAMOND,MONTHLY,1699","DIAMOND,YEARLY,11999","FRIENDS_AND_FAMILY,MONTHLY,2799","FRIENDS_AND_FAMILY,YEARLY,19900")
    fun `approved eight offers exact`(plan:String,period:String,amount:Long) {
        MembershipCommercialOfferValidation.validateCatalog(seed,catalog)
        val resolved=MembershipCommercialOfferResolution.resolve(seed,catalog,null)
        val offer=resolved.offers.single{it.planKey==plan && it.billingPeriod.name==period}
        assertEquals(amount,offer.amountMinorUnits);assertEquals("USD",offer.currencyCode);assertEquals("US",offer.market)
        assertEquals(8,resolved.offers.size);assertFalse(resolved.purchasesAvailable)
    }
    @Test fun `null unpublished pricing never seeds zero or mock`() {
        val result=MembershipCommercialOfferResolution.resolve(null,catalog,null)
        assertEquals("UNAVAILABLE",result.status);assertTrue(result.offers.isEmpty());assertNull(result.offerVersion)
    }
    @Test fun `missing inactive and unsupported offers cannot resolve`() {
        assertEquals(7,MembershipCommercialOfferResolution.resolve(seed.copy(offers=seed.offers.drop(1)),catalog,null).offers.size)
        assertEquals(7,MembershipCommercialOfferResolution.resolve(seed.copy(offers=seed.offers.mapIndexed{i,o->if(i==0)o.copy(active=false)else o}),catalog,null).offers.size)
        val unsupported=MembershipCommercialOfferResolution.resolve(seed,catalog,"CA")
        assertEquals("UNSUPPORTED_MARKET",unsupported.status);assertTrue(unsupported.offers.isEmpty())
    }
    @Test fun `catalog version mismatch and invalid linked plan rejected`() {
        assertTrue(MembershipCommercialOfferResolution.resolve(seed.copy(catalogVersion=2),catalog,null).offers.isEmpty())
        assertFails { MembershipCommercialOfferValidation.validateCatalog(seed.copy(offers=listOf(seed.offers[0].copy(planKey="UNKNOWN"))),catalog) }
        assertFails { MembershipCommercialOfferValidation.decode(GameCatalogCodec.map(seed),2) }
    }
    @Test fun `duplicate lookup currency negative fractional and malformed values rejected`() {
        assertFails { MembershipCommercialOfferValidation.validate(seed.copy(offers=seed.offers+seed.offers[0])) }
        for(amount in listOf(0L,-1L,Long.MAX_VALUE))assertFails { MembershipCommercialOfferValidation.validate(seed.copy(offers=listOf(seed.offers[0].copy(amountMinorUnits=amount)))) }
        assertFails { MembershipCommercialOfferValidation.validate(seed.copy(offers=listOf(seed.offers[0].copy(currencyCode="BAD")))) }
        for(value in listOf<Any>(6.99,"699","invalid")) {
            val data=GameCatalogCodec.map(seed).toMutableMap()
            data["offers"]=listOf(GameCatalogCodec.map(seed.offers[0]).toMutableMap().also{it["amountMinorUnits"]=value})
            assertFails { MembershipCommercialOfferValidation.decode(data,1) }
        }
    }
    @Test fun `immutable versions and no mapping duplication`() {
        MembershipCommercialOfferValidation.immutable(seed,GameCatalogCodec.map(seed),1)
        assertFails { MembershipCommercialOfferValidation.immutable(seed.copy(offers=emptyList()),GameCatalogCodec.map(seed),1) }
        assertFails { MembershipCommercialOfferValidation.immutable(seed,null,2) }
        assertEquals(16,catalog.billingProducts.size);assertTrue(catalog.billingProducts.all{!it.active && it.storeProductId==null})
        assertFalse(GameCatalogCodec.json(seed).contains("storeProductId"))
    }
    @Test fun `explicit multi market policy independent from locale`() {
        val ca=seed.copy(marketPolicy=MembershipMarketPolicy("CA",listOf("US","CA")),offers=seed.offers+seed.offers.map{it.copy(market="CA",currencyCode="CAD")})
        val repo=MemoryMembershipCatalog().also{it.publish(catalog)}
        val service=MembershipCatalogService(repo,object:MembershipCommercialOfferRepository { override fun current()=ca })
        for(locale in listOf("en","es"))assertEquals("CAD",service.read(locale,null).pricing!!.offers.first().currencyCode)
        assertEquals("USD",service.read("es",null,"US").pricing!!.offers.first().currencyCode)
    }
    @Test fun `future store precedence metadata grants neither billing nor trial`() {
        val pricing=MembershipCommercialOfferResolution.resolve(seed,catalog,"US")
        assertEquals(listOf("STORE_PRICE_IF_AUTHORITATIVE","CONFIGURED_COMMERCIAL_OFFER"),pricing.displayPricePrecedence)
        assertEquals("CONFIGURED_COMMERCIAL_OFFER",pricing.authority);assertFalse(pricing.purchasesAvailable)
    }
    @Test fun `repository failure preserves catalog and makes prices unavailable`() {
        val repo=MemoryMembershipCatalog().also{it.publish(catalog)}
        val response=MembershipCatalogService(repo,object:MembershipCommercialOfferRepository{override fun current():MembershipCommercialOfferPublication?=error("unavailable")}).read("es",null)
        assertEquals(5,response.plans.size);assertEquals("UNAVAILABLE",response.pricing!!.status)
    }
    @Test fun `existing read API carries offer version market and cache identity`() {
        var current=seed
        val repo=MemoryMembershipCatalog().also{it.publish(catalog)}
        val controller=MembershipCatalogController(MembershipCatalogService(repo,object:MembershipCommercialOfferRepository {override fun current()=current}),OnboardingCatalogAccess{})
        val identity=com.teamfho.domino.security.FirebaseIdentity("fixture",true)
        val request=org.springframework.mock.web.MockHttpServletRequest()
        val first=controller.catalog(identity,"es",null,request)
        assertEquals(200,first.statusCode.value());assertEquals(8,(first.body as MembershipCatalogResponse).pricing!!.offers.size)
        request.addHeader("If-None-Match",first.headers.getFirst("ETag")!!)
        assertEquals(304,controller.catalog(identity,"es",null,request).statusCode.value())
        current=seed.copy(offerVersion=2)
        assertEquals(200,controller.catalog(identity,"es",null,request).statusCode.value())
        request.setParameter("market","CA")
        val unsupported=controller.catalog(identity,"es",null,request)
        assertEquals(200,unsupported.statusCode.value());assertEquals("UNSUPPORTED_MARKET",(unsupported.body as MembershipCatalogResponse).pricing!!.status)
        request.setParameter("market","invalid")
        assertEquals(400,controller.catalog(identity,"es",null,request).statusCode.value())
    }
}
