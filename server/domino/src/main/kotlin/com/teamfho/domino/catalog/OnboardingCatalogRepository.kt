package com.teamfho.domino.catalog

import com.google.cloud.firestore.Firestore
import java.util.concurrent.TimeUnit

interface OnboardingCatalogRepository {
    fun currentVersion(): Int?
    fun read(version: Int): OnboardingCatalogPublication?
    fun publish(publication: OnboardingCatalogPublication)
}
class FirestoreOnboardingCatalogRepository(private val db: Firestore): OnboardingCatalogRepository {
    companion object { const val POINTER = "systemConfig/onboardingCatalog" }
    override fun currentVersion(): Int? = db.document(POINTER).get().get(5,TimeUnit.SECONDS).data?.let {
        val n = (it["publishedVersion"] as? Number)?.toLong() ?: error("INVALID_CATALOG_POINTER")
        require(n in 1..Int.MAX_VALUE.toLong()); n.toInt()
    }
    override fun read(version: Int): OnboardingCatalogPublication? {
        require(version > 0)
        return db.document("onboardingCatalogs/$version").get().get(5,TimeUnit.SECONDS).data?.let {
            GameCatalogCodec.decode(it,OnboardingCatalogPublication::class.java).also { p ->
                require(p.catalogVersion == version); OnboardingCatalogValidation.validate(p,false)
            }
        }
    }
    override fun publish(publication: OnboardingCatalogPublication) {
        OnboardingCatalogValidation.validate(publication)
        val data = GameCatalogCodec.map(publication)
        db.runTransaction { tx ->
            val ref = db.document("onboardingCatalogs/${publication.catalogVersion}")
            val pointer = db.document(POINTER)
            val existing = tx.get(ref).get().data
            val current = tx.get(pointer).get().data
            publication.coachCatalogVersion?.let { require(tx.get(db.document("coachCatalogs/$it")).get().exists()) { "COACH_PUBLICATION_MISSING" } }
            publication.membershipCatalogVersion?.let { require(tx.get(db.document("membershipCatalogs/$it")).get().exists()) { "MEMBERSHIP_PUBLICATION_MISSING" } }
            OnboardingPublicationChecks.check(publication,data,existing,current)
            if(existing == null) tx.create(ref,data)
            val old = (current?.get("publishedVersion") as? Number)?.toInt()
            if(old == null || old < publication.catalogVersion) tx.set(pointer,mapOf("publishedVersion" to publication.catalogVersion))
            null
        }.get(30,TimeUnit.SECONDS)
    }
}
object OnboardingPublicationChecks {
    fun check(p: OnboardingCatalogPublication, data: Map<String,Any>, existing: Map<String,Any>?, pointer: Map<String,Any>?) {
        require(existing == null || GameCatalogCodec.documentContent(existing) == GameCatalogCodec.documentContent(data)) { "CATALOG_IMMUTABLE_CONFLICT" }
        val old = pointer?.let { (it["publishedVersion"] as? Number)?.toLong() ?: error("INVALID_CATALOG_POINTER") }
        require(old == null || old > 0)
        // Re-seeding an existing historical version never rewinds the pointer.
        require(old == null || old <= p.catalogVersion || existing != null) { "CATALOG_VERSION_NOT_MONOTONIC" }
    }
}
object OnboardingCatalogSeed {
    fun canonical(): OnboardingCatalogPublication = GameCatalogCodec.mapper.readValue(
        requireNotNull(javaClass.getResourceAsStream("/onboarding-catalog-v1.json")),OnboardingCatalogPublication::class.java)
    fun run(repository: OnboardingCatalogRepository) = repository.publish(canonical())
    @JvmStatic fun main(args: Array<String>) {
        com.teamfho.domino.validation.RealFirestoreGuard.requireOptIn()
        require(args.contains("--seed-if-absent"))
        org.springframework.boot.builder.SpringApplicationBuilder(CatalogSeedConfiguration::class.java)
            .web(org.springframework.boot.WebApplicationType.NONE).logStartupInfo(false)
            .run(*args.filter { it != "--seed-if-absent" }.toTypedArray()).use { context ->
                val project = context.getBean(com.teamfho.domino.config.FirebaseProperties::class.java).projectId
                com.google.cloud.firestore.FirestoreOptions.newBuilder().setProjectId(project)
                    .setCredentials(com.google.auth.oauth2.GoogleCredentials.getApplicationDefault()).build().service.use { db ->
                        run(FirestoreOnboardingCatalogRepository(db))
                    }
            }
    }
}
