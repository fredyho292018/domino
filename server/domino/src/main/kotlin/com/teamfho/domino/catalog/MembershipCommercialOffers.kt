package com.teamfho.domino.catalog

import com.google.cloud.firestore.Firestore
import java.util.Currency
import java.util.Locale
import java.util.concurrent.TimeUnit

data class MembershipCommercialOffer(
    val planKey:String, val billingPeriod:MembershipBillingPeriod, val market:String,
    val amountMinorUnits:Long, val currencyCode:String, val active:Boolean
)
// Store product identity remains in the existing platform/plan/period mapping, not in money.
data class MembershipMarketPolicy(val defaultMarket:String, val supportedMarkets:List<String>)
data class MembershipCommercialOfferPublication(
    val schemaVersion:Int, val offerVersion:Int, val catalogVersion:Int,
    val marketPolicy:MembershipMarketPolicy, val offers:List<MembershipCommercialOffer>
)
data class MembershipPricingResponse(
    val offerVersion:Int?, val catalogVersion:Int, val market:String?, val status:String,
    val offers:List<MembershipCommercialOffer> = emptyList(),
    val authority:String="CONFIGURED_COMMERCIAL_OFFER", val purchasesAvailable:Boolean=false,
    val displayPricePrecedence:List<String> = listOf("STORE_PRICE_IF_AUTHORITATIVE","CONFIGURED_COMMERCIAL_OFFER")
)

object MembershipCommercialOfferValidation {
    fun validate(p:MembershipCommercialOfferPublication) {
        require(p.schemaVersion==1 && p.offerVersion>0 && p.catalogVersion>0)
        val markets=p.marketPolicy.supportedMarkets
        require(markets.isNotEmpty() && markets.size<=250 && markets.distinct().size==markets.size)
        require(markets.all { it in Locale.getISOCountries() })
        require(p.marketPolicy.defaultMarket in markets)
        require(p.offers.size<=2000)
        require(p.offers.map{Triple(it.planKey,it.billingPeriod,it.market)}.distinct().size==p.offers.size) { "DUPLICATE_COMMERCIAL_OFFER" }
        p.offers.forEach {
            require(Regex("[A-Z][A-Z0-9_]{0,63}").matches(it.planKey) && it.planKey!="FREE")
            require(it.market in markets && it.amountMinorUnits in 1..1_000_000_000_000L)
            require(Regex("[A-Z]{3}").matches(it.currencyCode))
            require(Currency.getInstance(it.currencyCode).defaultFractionDigits in 0..3)
        }
    }
    fun validateCatalog(p:MembershipCommercialOfferPublication,catalog:MembershipCatalogPublication) {
        validate(p); require(p.catalogVersion==catalog.catalogVersion) { "OFFER_CATALOG_VERSION_MISMATCH" }
        p.offers.forEach { offer ->
            require(catalog.plans.any { it.key==offer.planKey })
            require(catalog.billingProducts.any { it.planKey==offer.planKey && it.billingPeriod==offer.billingPeriod })
        }
    }
    fun decode(data:Map<String,Any>,version:Int)=GameCatalogCodec.decode(data,MembershipCommercialOfferPublication::class.java).also {
        require(it.offerVersion==version);validate(it)
    }
    fun immutable(p:MembershipCommercialOfferPublication,existing:Map<String,Any>?,oldVersion:Int?) {
        require(existing==null || GameCatalogCodec.documentContent(existing)==GameCatalogCodec.documentContent(GameCatalogCodec.map(p))) { "OFFER_IMMUTABLE_CONFLICT" }
        require(oldVersion==null || oldVersion<=p.offerVersion || existing!=null) { "OFFER_VERSION_NOT_MONOTONIC" }
    }
}

// Configuration seed only. No startup publication and no fallback when storage is absent.
object MembershipCommercialOfferSeed {
    fun approvedUs()=MembershipCommercialOfferPublication(1,1,1,MembershipMarketPolicy("US",listOf("US")),
        listOf("GOLD" to (699L to 4999L),"PLATINUM" to (1099L to 7999L),
            "DIAMOND" to (1699L to 11999L),"FRIENDS_AND_FAMILY" to (2799L to 19900L)).flatMap { (plan,amounts) ->
            listOf(MembershipCommercialOffer(plan,MembershipBillingPeriod.MONTHLY,"US",amounts.first,"USD",true),
                MembershipCommercialOffer(plan,MembershipBillingPeriod.YEARLY,"US",amounts.second,"USD",true))
        })
}
interface MembershipCommercialOfferRepository {
    fun current():MembershipCommercialOfferPublication?
}
object MissingMembershipCommercialOffers:MembershipCommercialOfferRepository {
    override fun current():MembershipCommercialOfferPublication?=null
}
class FirestoreMembershipCommercialOfferRepository(private val db:Firestore):MembershipCommercialOfferRepository {
    override fun current():MembershipCommercialOfferPublication? {
        val pointer=db.document("systemConfig/membershipCommercialOffers").get().get(5,TimeUnit.SECONDS).data ?: return null
        val version=GameCatalogCodec.decode(pointer,OfferPointer::class.java).publishedVersion
        require(version>0)
        return db.document("membershipCommercialOffers/$version").get().get(5,TimeUnit.SECONDS).data
            ?.let { MembershipCommercialOfferValidation.decode(it,version) } ?: error("OFFER_PUBLICATION_MISSING")
    }
    // Explicit future configuration operation, never called by reads or application startup.
    fun publish(p:MembershipCommercialOfferPublication) {
        MembershipCommercialOfferValidation.validate(p)
        db.runTransaction { tx ->
            val ref=db.document("membershipCommercialOffers/${p.offerVersion}")
            val pointer=db.document("systemConfig/membershipCommercialOffers")
            val catalog=tx.get(db.document("membershipCatalogs/${p.catalogVersion}")).get().data ?: error("OFFER_CATALOG_MISSING")
            val existing=tx.get(ref).get().data
            val old=tx.get(pointer).get().data?.let{GameCatalogCodec.decode(it,OfferPointer::class.java).publishedVersion}
            require(old==null || old>0)
            MembershipCommercialOfferValidation.validateCatalog(p,MembershipCatalogValidation.decode(catalog,p.catalogVersion))
            MembershipCommercialOfferValidation.immutable(p,existing,old)
            if(existing==null)tx.create(ref,GameCatalogCodec.map(p))
            if(old==null || old<p.offerVersion)tx.set(pointer,mapOf("publishedVersion" to p.offerVersion))
            null
        }.get(30,TimeUnit.SECONDS)
    }
    data class OfferPointer(val publishedVersion:Int)
}
object MembershipCommercialOfferResolution {
    fun resolve(p:MembershipCommercialOfferPublication?,catalog:MembershipCatalogPublication,market:String?):MembershipPricingResponse {
        if(p==null)return MembershipPricingResponse(null,catalog.catalogVersion,market,"UNAVAILABLE")
        MembershipCommercialOfferValidation.validate(p)
        val selected=market ?: p.marketPolicy.defaultMarket
        if(selected !in p.marketPolicy.supportedMarkets)return MembershipPricingResponse(p.offerVersion,catalog.catalogVersion,selected,"UNSUPPORTED_MARKET")
        if(p.catalogVersion!=catalog.catalogVersion)return MembershipPricingResponse(p.offerVersion,catalog.catalogVersion,selected,"UNAVAILABLE")
        MembershipCommercialOfferValidation.validateCatalog(p,catalog)
        val offers=p.offers.filter { it.market==selected && it.active && catalog.plans.any { plan->plan.key==it.planKey && plan.active } }
        return MembershipPricingResponse(p.offerVersion,catalog.catalogVersion,selected,if(offers.isEmpty())"UNAVAILABLE" else "AVAILABLE",offers)
    }
}
