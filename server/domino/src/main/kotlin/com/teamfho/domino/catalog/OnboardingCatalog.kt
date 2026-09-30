package com.teamfho.domino.catalog

enum class OnboardingQuestionType { SINGLE_SELECT, COACH_SELECT }
enum class OnboardingStepKind { QUESTION, CONTACTS, MEMBERSHIP }
enum class OnboardingBinding { EXPERIENCE_LEVEL, PREFERRED_COACH }
data class OnboardingCopy(val title: String, val description: String = "")
data class CatalogStep(val key: String, val stage: String, val sortOrder: Int, val active: Boolean,
    val required: Boolean, val skippable: Boolean, val kind: OnboardingStepKind,
    val questionKeys: List<String>, val nextStepKey: String?)
data class CatalogQuestion(val key: String, val stepKey: String, val type: OnboardingQuestionType,
    val typeVersion: Int, val sortOrder: Int, val active: Boolean, val required: Boolean,
    val binding: OnboardingBinding, val optionKeys: List<String>)
data class CatalogOption(val key: String, val questionKey: String, val sortOrder: Int, val active: Boolean)
data class OnboardingCatalogPublication(val schemaVersion: Int, val catalogVersion: Int,
    val defaultLocale: String, val supportedLocales: List<String>, val requiredCapabilities: List<String>,
    val coachCatalogVersion: Int?, val membershipCatalogVersion: Int?, val steps: List<CatalogStep>,
    val questions: List<CatalogQuestion>, val options: List<CatalogOption>,
    val translations: Map<String, Map<String, OnboardingCopy>>, val publishedAt: String)

object OnboardingCatalogValidation {
    fun validate(p: OnboardingCatalogPublication, publishing: Boolean = true) {
        require(p.schemaVersion == 1 && p.catalogVersion > 0)
        require(p.defaultLocale == "en" && p.supportedLocales.toSet() == setOf("es","en"))
        require(p.coachCatalogVersion == null || p.coachCatalogVersion > 0)
        require(p.membershipCatalogVersion == null || p.membershipCatalogVersion > 0)
        java.time.Instant.parse(p.publishedAt)
        require(p.steps.size in 1..32 && p.questions.size <= 128 && p.options.size <= 512)
        val keys = p.steps.map { it.key } + p.questions.map { it.key } + p.options.map { it.key }
        require(keys.distinct().size == keys.size && keys.all { Regex("[A-Z][A-Z0-9_]{0,63}").matches(it) })
        require(p.requiredCapabilities.distinct().size == p.requiredCapabilities.size)
        require(p.requiredCapabilities.all { it in setOf("SINGLE_SELECT_V1","COACH_SELECT_V1","CONTACTS_UNAVAILABLE","MEMBERSHIP_PRESENTATION") })
        val ordered = p.steps.sortedWith(compareBy<CatalogStep>{it.sortOrder}.thenBy{it.key})
        ordered.forEachIndexed { i,s -> require(s.nextStepKey == ordered.getOrNull(i+1)?.key) }
        p.steps.forEach { s ->
            require(s.stage in setOf("EXPERIENCE","COACH","CONTACTS","MEMBERSHIP") && s.sortOrder >= 0)
            require(s.required != s.skippable)
            require(s.questionKeys.distinct().size == s.questionKeys.size)
            require(s.questionKeys.all { k -> p.questions.any { it.key == k && it.stepKey == s.key } })
            require(s.nextStepKey == null || p.steps.any { it.key == s.nextStepKey && it.sortOrder > s.sortOrder })
            require(if(s.kind == OnboardingStepKind.QUESTION) s.questionKeys.isNotEmpty() else s.questionKeys.isEmpty() && s.skippable)
        }
        p.questions.forEach { q ->
            require(q.typeVersion == 1 && q.sortOrder >= 0 && p.steps.any { q.key in it.questionKeys && it.key == q.stepKey })
            require(q.optionKeys.distinct().size == q.optionKeys.size)
            require(q.optionKeys.all { k -> p.options.any { it.key == k && it.questionKey == q.key } })
            when(q.type) {
                OnboardingQuestionType.SINGLE_SELECT -> {
                    require("SINGLE_SELECT_V1" in p.requiredCapabilities)
                    require(q.binding == OnboardingBinding.EXPERIENCE_LEVEL)
                    require(q.optionKeys.toSet() == setOf("BEGINNER","RULES_KNOWN","STRATEGY","COMPETITIVE"))
                    require(!q.active || p.options.any { it.key in q.optionKeys && it.active })
                }
                OnboardingQuestionType.COACH_SELECT -> require(q.binding == OnboardingBinding.PREFERRED_COACH && q.optionKeys.isEmpty() && "COACH_SELECT_V1" in p.requiredCapabilities)
            }
        }
        require(p.options.all { o -> o.sortOrder >= 0 && p.questions.any { it.key == o.questionKey && o.key in it.optionKeys } })
        require(p.translations.keys.all { it in p.supportedLocales })
        require(p.translations.values.all { t -> t.keys.all { it in keys } && t.values.all { it.title.isNotBlank() && it.title.length <= 256 && it.description.length <= 1024 } })
        require(p.translations["en"]?.keys == keys.toSet())
        if(publishing) require(p.translations["es"]?.keys == keys.toSet())
        require(GameCatalogCodec.json(p).toByteArray(Charsets.UTF_8).size <= 512*1024)
    }
}

data class LocalizedOption(val key: String, val sortOrder: Int, val title: String, val description: String)
data class LocalizedQuestion(val key: String, val type: OnboardingQuestionType, val typeVersion: Int,
    val sortOrder: Int, val required: Boolean, val title: String, val description: String, val options: List<LocalizedOption>)
data class LocalizedStep(val key: String, val stage: String, val sortOrder: Int, val required: Boolean,
    val skippable: Boolean, val kind: OnboardingStepKind, val availability: String, val nextStepKey: String?,
    val title: String, val description: String, val questions: List<LocalizedQuestion>)
data class OnboardingCatalogResponse(val catalogVersion: Int, val locale: String,
    val requiredCapabilities: List<String>, val coachCatalogVersion: Int?, val membershipCatalogVersion: Int?,
    val steps: List<LocalizedStep>)

object OnboardingCatalogLocalization {
    fun locale(value: String?): String = value?.lowercase(java.util.Locale.ROOT)?.takeIf {
        Regex("(?:es|en)(?:-[a-z]{2})?").matches(it)
    }?.substringBefore('-') ?: "en"
    fun localize(p: OnboardingCatalogPublication, requested: String?): OnboardingCatalogResponse {
        OnboardingCatalogValidation.validate(p, false)
        val wanted = locale(requested)
        val allKeys = p.translations.getValue("en").keys
        // Whole-response fallback prevents silent mixed-language output.
        val resolved = if(p.translations[wanted]?.keys == allKeys) wanted else "en"
        val copy = p.translations.getValue(resolved)
        val steps = p.steps.filter { it.active }.sortedWith(compareBy<CatalogStep> { it.sortOrder }.thenBy { it.key })
        return OnboardingCatalogResponse(p.catalogVersion,resolved,p.requiredCapabilities,p.coachCatalogVersion,p.membershipCatalogVersion,
            steps.mapIndexed { i,s -> LocalizedStep(s.key,s.stage,s.sortOrder,s.required,s.skippable,s.kind,
                when { s.stage == "CONTACTS" -> "UNAVAILABLE"; s.stage == "COACH" && p.coachCatalogVersion == null -> "UNAVAILABLE"
                    s.stage == "MEMBERSHIP" && p.membershipCatalogVersion == null -> "UNAVAILABLE"; else -> "AVAILABLE" },
                steps.getOrNull(i+1)?.key,copy.getValue(s.key).title,copy.getValue(s.key).description,
                p.questions.filter { it.active && it.key in s.questionKeys }.sortedWith(compareBy<CatalogQuestion>{it.sortOrder}.thenBy{it.key}).map { q ->
                    LocalizedQuestion(q.key,q.type,q.typeVersion,q.sortOrder,q.required,copy.getValue(q.key).title,copy.getValue(q.key).description,
                        p.options.filter{it.active && it.key in q.optionKeys}.sortedWith(compareBy<CatalogOption>{it.sortOrder}.thenBy{it.key}).map { o ->
                            LocalizedOption(o.key,o.sortOrder,copy.getValue(o.key).title,copy.getValue(o.key).description) }) }) })
    }
}
