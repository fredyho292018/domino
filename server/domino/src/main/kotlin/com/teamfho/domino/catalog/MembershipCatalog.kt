package com.teamfho.domino.catalog

enum class MembershipFeatureKind { BOOLEAN_CAPABILITY, QUOTA, PRESENTATION_ONLY }
enum class MembershipPlatform { APPLE_APP_STORE, GOOGLE_PLAY }
enum class MembershipBillingPeriod { MONTHLY, YEARLY }
data class MembershipPlan(val key:String,val iconKey:String,val sortOrder:Int,val active:Boolean,val productKind:String)
data class MembershipPlanTranslation(val name:String,val description:String)
data class MembershipFeature(val key:String,val kind:MembershipFeatureKind,val iconKey:String,val sortOrder:Int)
data class MembershipFeatureTranslation(val name:String,val description:String)
data class MembershipQuota(val unlimited:Boolean,val maximum:Int?)
data class MembershipPlanFeature(val planKey:String,val featureKey:String,val included:Boolean,val quota:MembershipQuota?=null)
data class MembershipBillingProduct(val platform:MembershipPlatform,val planKey:String,val billingPeriod:MembershipBillingPeriod,
    val storeProductId:String?,val active:Boolean)
data class MembershipTargetPlan(val capabilities:Set<String>,val limits:Map<String,MembershipQuota>)
// Not SubscriptionPolicy: this document must never be loaded by the runtime entitlement resolver.
data class MembershipTargetPolicy(val version:Int,val active:Boolean,val plans:Map<String,MembershipTargetPlan>)
data class MembershipFamilyPolicy(val minPlayers:Int=2,val maxPlayers:Int=5,val ownerIncluded:Boolean=true,
    val invitationExpirationDays:Int=7,val effectivePlanKey:String="DIAMOND",val limitsPerPlayer:Boolean=true)
data class MembershipTrialReference(val version:Int,val product:String="PREMIUM_LEGACY",val commercialTrialEnabled:Boolean=false,
    val familyTrialEnabled:Boolean=false)
data class MembershipCatalogPublication(val schemaVersion:Int,val catalogVersion:Int,val defaultLocale:String,val supportedLocales:List<String>,
    val hierarchy:List<String>,val plans:List<MembershipPlan>,val features:List<MembershipFeature>,val planFeatures:List<MembershipPlanFeature>,
    val planTranslations:Map<String,Map<String,MembershipPlanTranslation>>,val featureTranslations:Map<String,Map<String,MembershipFeatureTranslation>>,
    val billingProducts:List<MembershipBillingProduct>,val targetPolicy:MembershipTargetPolicy,val trialPolicy:MembershipTrialReference,
    val familyPolicy:MembershipFamilyPolicy,val publishedAt:String)

data class BillingProductMetadataResponse(val platform:MembershipPlatform,val planKey:String,val billingPeriod:MembershipBillingPeriod,
    val storeProductId:String?,val active:Boolean,val purchasable:Boolean)
data class MembershipPlanFeatureResponse(val featureKey:String,val included:Boolean,val quota:MembershipQuota?)
data class MembershipPlanResponse(val key:String,val name:String,val description:String,val iconKey:String,val sortOrder:Int,val active:Boolean,
    val productKind:String,val features:List<MembershipPlanFeatureResponse>,val billingProducts:List<BillingProductMetadataResponse>)
data class MembershipFeatureResponse(val key:String,val name:String,val description:String,val iconKey:String,val sortOrder:Int,val kind:MembershipFeatureKind)
data class MembershipTrialPresentation(val policyVersion:Int,val product:String,val commercialTrialEnabled:Boolean,val familyTrialEnabled:Boolean)
data class MembershipFamilyPresentation(val minPlayers:Int,val maxPlayers:Int,val ownerIncluded:Boolean,val effectivePlanKey:String,val limitsPerPlayer:Boolean)
data class MembershipCatalogResponse(val schemaVersion:Int,val catalogVersion:Int,val resolvedLocale:String,val defaultLocale:String,
    val supportedLocales:List<String>,val hierarchy:List<String>,val entitlementPolicyVersion:Int,val trialPolicyVersion:Int,
    val plans:List<MembershipPlanResponse>,val features:List<MembershipFeatureResponse>,val trialPresentation:MembershipTrialPresentation,
    val familyPresentation:MembershipFamilyPresentation,val priceAuthority:String="APPLE_GOOGLE_STORE")

object MembershipCatalogValidation {
    private val key=Regex("[A-Z][A-Z0-9_]{0,63}")
    fun validate(p:MembershipCatalogPublication,publishing:Boolean=true) {
        require(p.schemaVersion==1 && p.catalogVersion>0 && p.defaultLocale=="en" && p.supportedLocales==listOf("es","en"))
        java.time.Instant.parse(p.publishedAt)
        require(p.hierarchy==listOf("FREE","GOLD","PLATINUM","DIAMOND"))
        val plans=p.plans.map{it.key}.toSet();val features=p.features.map{it.key}.toSet()
        require(plans==p.hierarchy.toSet()+"FRIENDS_AND_FAMILY" && plans.size==p.plans.size)
        require(features==MembershipCatalogSeed.featureKeys.toSet() && features.size==p.features.size)
        p.plans.forEach { require(key.matches(it.key) && it.iconKey.isNotBlank() && it.iconKey.length<=128 && it.sortOrder>=0)
            require(it.productKind==if(it.key=="FRIENDS_AND_FAMILY")"MULTI_PLAYER" else "INDIVIDUAL") }
        p.features.forEach { require(key.matches(it.key) && it.iconKey.isNotBlank() && it.iconKey.length<=128 && it.sortOrder>=0) }
        require(p.planFeatures.size==plans.size*features.size)
        require(p.planFeatures.map{it.planKey to it.featureKey}.toSet().size==p.planFeatures.size)
        p.planFeatures.forEach {
            require(it.planKey in plans && it.featureKey in features)
            val kind=p.features.single{f->f.key==it.featureKey}.kind
            require((it.quota!=null)==(kind==MembershipFeatureKind.QUOTA && it.included))
            it.quota?.let(::quota)
        }
        for(locale in listOf("en","es")) {
            val pc=p.planTranslations[locale];val fc=p.featureTranslations[locale]
            if(publishing || locale=="en")require(pc?.keys==plans && fc?.keys==features)
            pc?.let { require(it.keys.all{ k->k in plans });it.values.forEach{v->copy(v.name,v.description)} }
            fc?.let { require(it.keys.all{ k->k in features });it.values.forEach{v->copy(v.name,v.description)} }
        }
        require(p.planTranslations.keys.all{it in p.supportedLocales} && p.featureTranslations.keys.all{it in p.supportedLocales})
        require(p.billingProducts.size<=16)
        require(p.billingProducts.map{Triple(it.platform,it.planKey,it.billingPeriod)}.toSet().size==p.billingProducts.size)
        require(p.billingProducts.filter{it.storeProductId!=null}.map{it.platform to it.storeProductId}.toSet().size==p.billingProducts.count{it.storeProductId!=null})
        p.billingProducts.forEach {
            require(it.planKey in plans && it.planKey!="FREE")
            require(it.storeProductId==null || Regex("[A-Za-z0-9._-]{1,200}").matches(it.storeProductId))
            require(!it.active || it.storeProductId!=null)
        }
        require(p.targetPolicy.version>0 && !p.targetPolicy.active && p.targetPolicy.plans.keys==plans)
        val allowed=MembershipCatalogSeed.backendKeys
        p.targetPolicy.plans.values.forEach { target ->
            require(target.capabilities.all{it in allowed})
            require(target.limits.keys==setOf("FRIENDS_MAX","HISTORY_MAX","REPLAY_MAX"))
            target.limits.values.forEach(::quota)
            require(target.limits.getValue("HISTORY_MAX").unlimited==("FULL_HISTORY" in target.capabilities))
            require(target.limits.getValue("REPLAY_MAX").unlimited==("FULL_REPLAY" in target.capabilities))
        }
        require(p.trialPolicy.version>0 && p.trialPolicy.product=="PREMIUM_LEGACY" && !p.trialPolicy.commercialTrialEnabled && !p.trialPolicy.familyTrialEnabled)
        require(p.familyPolicy.minPlayers==2 && p.familyPolicy.maxPlayers==5 && p.familyPolicy.ownerIncluded && p.familyPolicy.limitsPerPlayer)
        require(p.familyPolicy.effectivePlanKey=="DIAMOND" && p.familyPolicy.invitationExpirationDays in 1..30)
        require(p.targetPolicy.plans.getValue("FRIENDS_AND_FAMILY")==p.targetPolicy.plans.getValue("DIAMOND"))
        fun commercial(plan:String)=p.planFeatures.filter{it.planKey==plan && it.included}.map{it.featureKey}.toSet()
        require(commercial("FREE").isEmpty())
        p.hierarchy.zipWithNext().forEach{(a,b)->require(commercial(b).containsAll(commercial(a)))
            require(p.targetPolicy.plans.getValue(b).capabilities.containsAll(p.targetPolicy.plans.getValue(a).capabilities))}
        require(commercial("FRIENDS_AND_FAMILY")==commercial("DIAMOND"))
        require(GameCatalogCodec.json(p).toByteArray(Charsets.UTF_8).size<=512*1024)
    }
    private fun quota(q:MembershipQuota){require(if(q.unlimited)q.maximum==null else q.maximum!=null && q.maximum>=0)}
    private fun copy(name:String,description:String){require(name.isNotBlank() && name.length<=128 && description.isNotBlank() && description.length<=2048)}
    fun decode(data:Map<String,Any>,version:Int)=GameCatalogCodec.decode(data,MembershipCatalogPublication::class.java).also{
        require(it.catalogVersion==version);validate(it,false)
    }
}

object MembershipCatalogLocalization {
    fun localize(p:MembershipCatalogPublication,locale:String?,historical:Boolean=false):MembershipCatalogResponse {
        MembershipCatalogValidation.validate(p,false)
        val wanted=OnboardingCatalogLocalization.locale(locale)
        val resolved=if(p.planTranslations[wanted]?.keys==p.plans.map{it.key}.toSet() &&
            p.featureTranslations[wanted]?.keys==p.features.map{it.key}.toSet())wanted else "en"
        val features=p.features.sortedWith(compareBy<MembershipFeature>{it.sortOrder}.thenBy{it.key})
        return MembershipCatalogResponse(p.schemaVersion,p.catalogVersion,resolved,p.defaultLocale,p.supportedLocales,p.hierarchy,
            p.targetPolicy.version,p.trialPolicy.version,p.plans.filter{historical || it.active}.sortedWith(compareBy<MembershipPlan>{it.sortOrder}.thenBy{it.key}).map { plan ->
                val c=p.planTranslations.getValue(resolved).getValue(plan.key)
                MembershipPlanResponse(plan.key,c.name,c.description,plan.iconKey,plan.sortOrder,plan.active,plan.productKind,
                    features.map{f->p.planFeatures.single{it.planKey==plan.key && it.featureKey==f.key}.let{MembershipPlanFeatureResponse(it.featureKey,it.included,it.quota)}},
                    p.billingProducts.filter{it.planKey==plan.key}.sortedWith(compareBy<MembershipBillingProduct>{it.platform.name}.thenBy{it.billingPeriod.name}).map{
                        BillingProductMetadataResponse(it.platform,it.planKey,it.billingPeriod,it.storeProductId,it.active,plan.active && it.active && it.storeProductId!=null)})
            },features.map{f->p.featureTranslations.getValue(resolved).getValue(f.key).let{MembershipFeatureResponse(f.key,it.name,it.description,f.iconKey,f.sortOrder,f.kind)}},
            MembershipTrialPresentation(p.trialPolicy.version,p.trialPolicy.product,false,false),
            MembershipFamilyPresentation(p.familyPolicy.minPlayers,p.familyPolicy.maxPlayers,true,p.familyPolicy.effectivePlanKey,true))
    }
}
