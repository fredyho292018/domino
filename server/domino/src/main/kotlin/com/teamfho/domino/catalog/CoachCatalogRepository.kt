package com.teamfho.domino.catalog

import com.google.cloud.firestore.Firestore
import java.util.concurrent.TimeUnit

interface CoachCatalogRepository {
    fun currentVersion():Int?
    fun read(version:Int):CoachCatalogPublication?
    fun publish(publication:CoachCatalogPublication)
}
object CoachPublicationChecks {
    fun check(p:CoachCatalogPublication,existing:Map<String,Any>?,pointer:Map<String,Any>?) {
        require(existing==null || GameCatalogCodec.documentContent(existing)==GameCatalogCodec.documentContent(GameCatalogCodec.map(p))) { "CATALOG_IMMUTABLE_CONFLICT" }
        val old=pointer?.let{(it["publishedVersion"] as? Number)?.toLong() ?: error("INVALID_CATALOG_POINTER")}
        require(old==null || old in 1..Int.MAX_VALUE.toLong())
        require(old==null || old<=p.catalogVersion || existing!=null) { "CATALOG_VERSION_NOT_MONOTONIC" }
    }
}
class FirestoreCoachCatalogRepository(private val db:Firestore):CoachCatalogRepository {
    override fun currentVersion():Int?=db.document("systemConfig/coachCatalog").get().get(5,TimeUnit.SECONDS).data?.let {
        val v=(it["publishedVersion"] as? Number)?.toLong() ?: error("INVALID_CATALOG_POINTER")
        require(v in 1..Int.MAX_VALUE.toLong());v.toInt()
    }
    override fun read(version:Int):CoachCatalogPublication? {
        require(version>0)
        return db.document("coachCatalogs/$version").get().get(5,TimeUnit.SECONDS).data?.let{CoachCatalogValidation.decode(it,version)}
    }
    override fun publish(publication:CoachCatalogPublication) {
        CoachCatalogValidation.validate(publication)
        db.runTransaction { tx ->
            val ref=db.document("coachCatalogs/${publication.catalogVersion}");val pointer=db.document("systemConfig/coachCatalog")
            val existing=tx.get(ref).get().data;val current=tx.get(pointer).get().data
            CoachPublicationChecks.check(publication,existing,current)
            if(existing==null)tx.create(ref,GameCatalogCodec.map(publication))
            val old=(current?.get("publishedVersion") as? Number)?.toInt()
            if(old==null || old<publication.catalogVersion)tx.set(pointer,mapOf("publishedVersion" to publication.catalogVersion))
            null
        }.get(30,TimeUnit.SECONDS)
    }
}
object CoachCatalogSeed {
    fun canonical():CoachCatalogPublication=GameCatalogCodec.mapper.readValue(
        requireNotNull(javaClass.getResourceAsStream("/coach-catalog-v1.json")),CoachCatalogPublication::class.java)
    fun run(repository:CoachCatalogRepository)=repository.publish(canonical())
    // A separate immutable onboarding publication opts NEW starts into Coach v1. Never rewrite v1 or existing pins.
    fun compatibleOnboarding():OnboardingCatalogPublication=OnboardingCatalogSeed.canonical().copy(
        catalogVersion=2,coachCatalogVersion=1,publishedAt="2026-09-30T00:00:00Z")
    fun publishCompatibleOnboarding(repository:OnboardingCatalogRepository)=repository.publish(compatibleOnboarding())
}
