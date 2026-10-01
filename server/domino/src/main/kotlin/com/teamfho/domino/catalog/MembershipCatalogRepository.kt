package com.teamfho.domino.catalog

import com.google.cloud.firestore.Firestore
import java.util.concurrent.TimeUnit

interface MembershipCatalogRepository {
    fun currentVersion():Int?
    fun read(version:Int):MembershipCatalogPublication?
    fun publish(publication:MembershipCatalogPublication)
}
object MembershipPublicationChecks {
    fun policy(expected:Any,existing:Map<String,Any>?) {
        require(existing==null || GameCatalogCodec.documentContent(existing)==GameCatalogCodec.documentContent(GameCatalogCodec.map(expected))) { "POLICY_IMMUTABLE_CONFLICT" }
    }
    fun check(p:MembershipCatalogPublication,existing:Map<String,Any>?,pointer:Map<String,Any>?) {
        require(existing==null || GameCatalogCodec.documentContent(existing)==GameCatalogCodec.documentContent(GameCatalogCodec.map(p))) { "CATALOG_IMMUTABLE_CONFLICT" }
        val old=pointer?.let{(it["publishedVersion"] as? Number)?.toLong() ?: error("INVALID_CATALOG_POINTER")}
        require(old==null || old in 1..Int.MAX_VALUE.toLong())
        require(old==null || old<=p.catalogVersion || existing!=null) { "CATALOG_VERSION_NOT_MONOTONIC" }
    }
}
class FirestoreMembershipCatalogRepository(private val db:Firestore):MembershipCatalogRepository {
    override fun currentVersion():Int?=db.document("systemConfig/membershipCatalog").get().get(5,TimeUnit.SECONDS).data?.let {
        val v=(it["publishedVersion"] as? Number)?.toLong() ?: error("INVALID_CATALOG_POINTER")
        require(v in 1..Int.MAX_VALUE.toLong());v.toInt()
    }
    override fun read(version:Int):MembershipCatalogPublication? {
        require(version>0)
        return db.document("membershipCatalogs/$version").get().get(5,TimeUnit.SECONDS).data?.let{MembershipCatalogValidation.decode(it,version)}
    }
    override fun publish(publication:MembershipCatalogPublication) {
        MembershipCatalogValidation.validate(publication)
        db.runTransaction { tx ->
            val ref=db.document("membershipCatalogs/${publication.catalogVersion}");val pointer=db.document("systemConfig/membershipCatalog")
            val existing=tx.get(ref).get().data;val current=tx.get(pointer).get().data
            val target=db.document("membershipTargetPolicies/${publication.targetPolicy.version}")
            val trial=db.document("membershipTrialReferences/${publication.trialPolicy.version}")
            val oldTarget=tx.get(target).get().data;val oldTrial=tx.get(trial).get().data
            MembershipPublicationChecks.check(publication,existing,current)
            MembershipPublicationChecks.policy(publication.targetPolicy,oldTarget)
            MembershipPublicationChecks.policy(publication.trialPolicy,oldTrial)
            if(oldTarget==null)tx.create(target,GameCatalogCodec.map(publication.targetPolicy))
            if(oldTrial==null)tx.create(trial,GameCatalogCodec.map(publication.trialPolicy))
            if(existing==null)tx.create(ref,GameCatalogCodec.map(publication))
            val old=(current?.get("publishedVersion") as? Number)?.toInt()
            if(old==null || old<publication.catalogVersion)tx.set(pointer,mapOf("publishedVersion" to publication.catalogVersion))
            null
        }.get(30,TimeUnit.SECONDS)
    }
}

