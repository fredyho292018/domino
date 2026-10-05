using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Domino.Infrastructure.Api
{
    // Public response contracts only. Enum-like strings preserve unknown values for safe future presentation.
    [Serializable] public sealed class OnboardingCatalogDto {
        public int catalogVersion; public string locale; public string[] requiredCapabilities;
        public int? coachCatalogVersion, membershipCatalogVersion; public OnboardingStepDto[] steps;
    }
    [Serializable] public sealed class OnboardingStepDto {
        public string key, stage, kind, availability, nextStepKey, title, description;
        public int sortOrder; public bool required, skippable; public OnboardingQuestionDto[] questions;
    }
    [Serializable] public sealed class OnboardingQuestionDto {
        public string key, type, title, description; public int typeVersion, sortOrder;
        public bool required; public OnboardingOptionDto[] options;
    }
    [Serializable] public sealed class OnboardingOptionDto { public string key, title, description; public int sortOrder; }
    [Serializable] public sealed class OnboardingAnswerDto { public string questionKey, type, optionKey, textValue; }
    [Serializable] public sealed class BasicProfileDto { public string firstName, lastName, displayName, countryCode, preferredLocale, timeZone; }
    [Serializable] public sealed class OnboardingDomainRevisionsDto { public long profile, preferences, domino; }
    [Serializable] public sealed class OnboardingStateDto {
        public string status, currentStepKey, currentSubstepKey, lastCompletedStepKey, startedAt, completedAt, updatedAt, completionOrigin;
        public int? catalogVersion; public long revision; public string[] completedStepKeys, skippedStepKeys, requiredFieldsMissing;
        public OnboardingDomainRevisionsDto domainRevisions; public OnboardingAnswerDto[] answers; public BasicProfileDto basicProfile;
    }
    [Serializable] public sealed class DominoProfileDto { public string experienceLevel, preferredCoachKey, updatedAt; public int? selectedCoachCatalogVersion; public long revision; }
    [Serializable] public sealed class OnboardingMutationDto { public OnboardingStateDto onboarding; public DominoProfileDto domino; }
    [Serializable] public sealed class OnboardingStartDto { public string operationId; public long expectedRevision; }
    [Serializable] public sealed class OnboardingSaveDto {
        public string operationId; public long expectedRevision; public int catalogVersion; public Dictionary<string,long> domainRevisions;
        public string action; public OnboardingAnswerDto[] answers; public string detectedTimeZone;
    }
    [Serializable] public sealed class OnboardingCursorDto { public string operationId; public long expectedRevision; public int catalogVersion; public string targetStepKey, targetSubstepKey; }
    [Serializable] public sealed class OnboardingCompleteDto { public string operationId; public long expectedRevision; public int catalogVersion; }
    [Serializable] public sealed class CoachCatalogDto { public int catalogVersion; public string resolvedLocale; public CoachDto[] items; }
    [Serializable] public sealed class CoachDto { public string key, name, shortDescription, description; public CoachAvatarDto avatar; public bool selectable; public int sortOrder; }
    [Serializable] public sealed class CoachAvatarDto { public string key, storagePath; public int assetVersion; }
    [Serializable] public sealed class MembershipCatalogDto {
        public MembershipPricingDto pricing;
        public int schemaVersion, catalogVersion, entitlementPolicyVersion, trialPolicyVersion; public string resolvedLocale, defaultLocale, priceAuthority;
        public string[] supportedLocales, hierarchy; public MembershipPlanDto[] plans; public MembershipFeatureDto[] features;
        public MembershipTrialDto trialPresentation; public MembershipFamilyDto familyPresentation;
    }
    [Serializable] public sealed class MembershipPricingDto {
        public int? offerVersion; public int catalogVersion;
        public string market, status, authority; public bool purchasesAvailable;
        public CommercialOfferDto[] offers; public string[] displayPricePrecedence;
    }
    [Serializable] public sealed class CommercialOfferDto {
        public string planKey, billingPeriod, market, currencyCode;
        public long? amountMinorUnits; public bool active;
    }
    [Serializable] public sealed class MembershipPlanDto { public string key, name, description, iconKey, productKind; public int sortOrder; public bool active; public MembershipPlanFeatureDto[] features; public BillingProductDto[] billingProducts; }
    [Serializable] public sealed class MembershipFeatureDto { public string key, name, description, iconKey, kind, implementationStatus; public bool currentlyUsable; public int sortOrder; }
    [Serializable] public sealed class MembershipPlanFeatureDto { public string featureKey; public bool included; public MembershipQuotaDto quota; }
    [Serializable] public sealed class MembershipQuotaDto { public bool unlimited; public int? maximum; }
    [Serializable] public sealed class BillingProductDto {
        public string platform, planKey, billingPeriod, storeProductId; public bool active, purchasable;
        [JsonIgnore] public bool IsConfiguredForPurchase => active && purchasable && !string.IsNullOrWhiteSpace(storeProductId);
    }
    [Serializable] public sealed class MembershipTrialDto { public int policyVersion; public string product; public bool commercialTrialEnabled, familyTrialEnabled; }
    [Serializable] public sealed class MembershipFamilyDto { public int minPlayers, maxPlayers; public bool ownerIncluded, limitsPerPlayer; public string effectivePlanKey; }
    [Serializable] public sealed class TrialActivationRequestDto { public string operationId, plan, billingPeriod; public long expectedPolicyVersion; }
    [Serializable] public sealed class TrialActivationResponseDto { public string operationId, outcome; public EntitlementSummaryDto entitlements; public TrialStateDto trial; }
}
