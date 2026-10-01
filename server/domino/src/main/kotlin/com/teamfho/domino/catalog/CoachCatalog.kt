package com.teamfho.domino.catalog

import com.teamfho.domino.player.*

data class CoachEntry(val key:String,val active:Boolean,val sortOrder:Int,val avatarKey:String,val assetVersion:Int,val storagePath:String)
data class CoachCopy(val name:String,val shortDescription:String,val description:String)
data class CoachCatalogPublication(val schemaVersion:Int,val catalogVersion:Int,val defaultLocale:String,
    val supportedLocales:List<String>,val coaches:List<CoachEntry>,val translations:Map<String,Map<String,CoachCopy>>,val publishedAt:String)
data class CoachAvatar(val key:String,val assetVersion:Int,val storagePath:String)
data class LocalizedCoach(val key:String,val name:String,val shortDescription:String,val description:String,
    val avatar:CoachAvatar,val selectable:Boolean,val sortOrder:Int)
data class CoachCatalogResponse(val catalogVersion:Int,val resolvedLocale:String,val items:List<LocalizedCoach>)

object CoachCatalogValidation {
    fun validate(p:CoachCatalogPublication,publishing:Boolean=true) {
        require(p.schemaVersion==1 && p.catalogVersion>0 && p.defaultLocale=="en" && p.supportedLocales.toSet()==setOf("es","en"))
        java.time.Instant.parse(p.publishedAt)
        require(p.coaches.size in 1..128)
        val keys=p.coaches.map{it.key}.toSet()
        require(keys.size==p.coaches.size)
        p.coaches.forEach {
            require(Regex("[A-Z][A-Z0-9_]{0,63}").matches(it.key) && it.sortOrder>=0 && it.assetVersion>0)
            require(Regex("[A-Z][A-Z0-9_]{0,63}").matches(it.avatarKey))
            require(Regex("AppShellMockCoaches/coach_[a-z0-9_]+\\.png").matches(it.storagePath))
        }
        require(p.translations.keys.all{it in p.supportedLocales})
        require(p.translations["en"]?.keys==keys)
        if(publishing) require(p.translations["es"]?.keys==keys)
        p.translations.values.forEach { copies ->
            require(copies.keys.all{it in keys})
            copies.values.forEach { require(it.name.isNotBlank() && it.name.length<=128 && it.shortDescription.length<=512 && it.description.length<=2048) }
        }
        require(GameCatalogCodec.json(p).toByteArray(Charsets.UTF_8).size<=512*1024)
    }
    fun decode(data:Map<String,Any>,version:Int):CoachCatalogPublication =
        GameCatalogCodec.decode(data,CoachCatalogPublication::class.java).also{require(it.catalogVersion==version);validate(it,false)}
}
object CoachCatalogLocalization {
    fun localize(p:CoachCatalogPublication,locale:String?,historical:Boolean=false):CoachCatalogResponse {
        CoachCatalogValidation.validate(p,false)
        val wanted=OnboardingCatalogLocalization.locale(locale)
        val resolved=if(p.translations[wanted]?.keys==p.coaches.map{it.key}.toSet())wanted else "en"
        val copy=p.translations.getValue(resolved)
        return CoachCatalogResponse(p.catalogVersion,resolved,p.coaches.filter{historical || it.active}
            .sortedWith(compareBy<CoachEntry>{it.sortOrder}.thenBy{it.key}).map {
                val c=copy.getValue(it.key)
                LocalizedCoach(it.key,c.name,c.shortDescription,c.description,CoachAvatar(it.avatarKey,it.assetVersion,it.storagePath),it.active,it.sortOrder)
            })
    }
}

// All selection checks use the caller's transaction and the onboarding-pinned publication.
class AuthoritativeCoachValidation:OnboardingCoachValidation {
    private fun entry(tx:OnboardingProgressTransaction,version:Int?,key:String?):CoachEntry {
        onboardingCheck(version!=null && version>0,"COACH_CATALOG_VERSION_MISMATCH")
        onboardingCheck(key!=null && Regex("[A-Z][A-Z0-9_]{0,63}").matches(key),"COACH_NOT_FOUND",404)
        val data=tx.read("coachCatalogs/$version") ?: throw OnboardingFailure("COACH_CATALOG_NOT_FOUND",404)
        val publication=try { CoachCatalogValidation.decode(data,version!!) }
            catch(e:Exception){throw OnboardingFailure("DEPENDENCY_UNAVAILABLE",503)}
        return publication.coaches.find{it.key==key} ?: throw OnboardingFailure("COACH_NOT_FOUND",404)
    }
    override fun validate(tx:OnboardingProgressTransaction,catalogVersion:Int?,key:String?) {
        onboardingCheck(entry(tx,catalogVersion,key).active,"COACH_NOT_SELECTABLE")
    }
    override fun validateExisting(tx:OnboardingProgressTransaction,catalogVersion:Int?,key:String?) {
        entry(tx,catalogVersion,key) // Retain historical inactive references, never substitute.
    }
}
