package com.teamfho.domino.player

import com.teamfho.domino.catalog.OnboardingBinding
import com.teamfho.domino.catalog.OnboardingQuestionType

/** Shared field rules; legal names never become the public alias automatically. */
object OnboardingProfileBinding {
    val bindings=setOf(OnboardingBinding.FIRST_NAME,OnboardingBinding.LAST_NAME,OnboardingBinding.DISPLAY_NAME,OnboardingBinding.COUNTRY_CODE,OnboardingBinding.PREFERRED_LOCALE)
    fun value(binding:OnboardingBinding,player:Player,preferences:PlayerPreferences):String?=when(binding) {
        OnboardingBinding.FIRST_NAME->player.firstName
        OnboardingBinding.LAST_NAME->player.lastName
        OnboardingBinding.DISPLAY_NAME->player.displayName
        OnboardingBinding.COUNTRY_CODE->player.countryCode
        OnboardingBinding.PREFERRED_LOCALE->preferences.preferredLocale
        else->error("NOT_A_PROFILE_BINDING")
    }
    fun validated(binding:OnboardingBinding,value:String?):String {
        onboardingCheck(value!=null,"ONBOARDING_REQUIRED_FIELD_MISSING",400)
        try {
            return when(binding) {
                OnboardingBinding.FIRST_NAME,OnboardingBinding.LAST_NAME->requireNotNull(PlayerProfileRules.name(value))
                OnboardingBinding.DISPLAY_NAME->DisplayNameRules.validate(value)
                OnboardingBinding.COUNTRY_CODE->requireNotNull(PlayerProfileRules.country(value))
                OnboardingBinding.PREFERRED_LOCALE->{onboardingCheck(value in setOf("es","en"),"LANGUAGE_UNSUPPORTED",400);value!!}
                else->error("NOT_A_PROFILE_BINDING")
            }
        } catch(e:ProfileValidationException){throw OnboardingFailure(e.code,400)}
        catch(e:DisplayNameException){throw OnboardingFailure(if(e.reserved)"DISPLAY_NAME_RESERVED" else "DISPLAY_NAME_INVALID",400)}
    }
    fun answer(binding:OnboardingBinding,type:OnboardingQuestionType,answer:OnboardingAnswer):String {
        if(type==OnboardingQuestionType.TEXT) {
            onboardingCheck(answer.optionKey==null,"ONBOARDING_INVALID_ANSWER",400)
            return validated(binding,answer.textValue)
        }
        onboardingCheck(answer.textValue==null,"ONBOARDING_INVALID_ANSWER",400)
        return validated(binding,answer.optionKey)
    }
    // Detected metadata is a suggestion, never a replacement for an existing Settings choice.
    fun initializeTimeZone(existing:String?,detected:String?):String? = existing ?: runCatching{PlayerProfileRules.timeZone(detected)}.getOrNull()
}
