package com.teamfho.domino.catalog

/** Unpublished v2 extends immutable v1; language remains presentation, never a version. */
object OnboardingCatalogV2 {
    fun canonical():OnboardingCatalogPublication {
        val v1=OnboardingCatalogSeed.canonical()
        val keys=listOf("FIRST_NAME","LAST_NAME","DISPLAY_NAME","COUNTRY","PREFERRED_LANGUAGE")
        val bindings=listOf(OnboardingBinding.FIRST_NAME,OnboardingBinding.LAST_NAME,OnboardingBinding.DISPLAY_NAME,OnboardingBinding.COUNTRY_CODE,OnboardingBinding.PREFERRED_LOCALE)
        val types=listOf(OnboardingQuestionType.TEXT,OnboardingQuestionType.TEXT,OnboardingQuestionType.TEXT,OnboardingQuestionType.COUNTRY_SELECT,OnboardingQuestionType.LOCALE_SELECT)
        val step=CatalogStep("BASIC_PROFILE_STEP","BASIC_PROFILE",5,true,true,false,OnboardingStepKind.QUESTION,keys,"EXPERIENCE_STEP")
        val questions=keys.mapIndexed{i,key->CatalogQuestion(key,step.key,types[i],1,(i+1)*10,true,true,bindings[i],emptyList())}
        val es=listOf("Tu perfil","Nombre","Apellidos","Nombre público","País","Idioma preferido")
        val en=listOf("Your profile","First name","Last name","Display name","Country","Preferred language")
        val titles=listOf(step.key)+keys
        val translations=v1.translations.mapValues { (locale,copy)-> copy+titles.mapIndexed{i,key->key to OnboardingCopy((if(locale=="es")es else en)[i])}.toMap() }
        return v1.copy(catalogVersion=2,coachCatalogVersion=1,publishedAt="2026-09-30T00:00:00Z",
            requiredCapabilities=v1.requiredCapabilities+"BASIC_PROFILE_V1",steps=listOf(step)+v1.steps,questions=questions+v1.questions,translations=translations)
    }
    fun run(repository:OnboardingCatalogRepository)=repository.publish(canonical())
}
