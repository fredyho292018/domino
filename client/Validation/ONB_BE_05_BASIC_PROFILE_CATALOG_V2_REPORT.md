# ONB-BE-05 — Basic Profile and onboarding catalog v2

Base: `01bf70821f5739cf8e2e841eed5d114bbf1371dd`. Implementation/test only. No live Firestore, seed, migration, rollout, Unity change, commit, push or deployment.

## Version compatibility

The approved v1 resource is untouched. Its Git blob SHA-1 is `0a7a0632f7ff23f70081ee14e569d5913cef2ff7`; a regression test checks the normalized resource bytes against that exact blob. V1 retains Experience, Coach, Contacts and Membership. Existing v1 progress remains pinned; completed FLOW and LEGACY_EXEMPT are not reopened. V1's absent Coach pin remains absent; this change does not silently repair/migrate it.

`OnboardingCatalogV2.canonical()` now constructs the authorized full v2. It supersedes the BE-04 *unpublished preparation*, not a historical published version. `CoachCatalogSeed.compatibleOnboarding()` delegates to it so there is only one production v2 definition. An existing conflicting version-2 document would still fail immutable publication checks; there is no overwrite/migration escape hatch.

| Step | Order | Required |
|---|---:|---|
| BASIC_PROFILE_STEP | 5 | Yes |
| EXPERIENCE_STEP | 10 | Yes |
| COACH_STEP | 20 | Yes |
| CONTACTS_STEP | 30 | No |
| MEMBERSHIP_STEP | 40 | No |

V2 pins Coach catalog 1 explicitly. Existing step/question/option definitions are reused unchanged. Additional capability BASIC_PROFILE_V1 declares TEXT, COUNTRY_SELECT and LOCALE_SELECT version-1 types for Basic Profile; these are rejected on catalog v1. All five profile questions must be active/required and bound to the approved domains. No DOB, avatar, timezone question or hundreds of country option documents. Presentation translations es/en do not change semantic keys or catalog version.

## API and domain bindings

The existing authenticated `PUT /api/v1/player/onboarding/steps/BASIC_PROFILE_STEP` is the API integration; no independent duplicate profile state/store or unrelated endpoint is introduced. Request uses SaveStepRequest with the existing operationId/expectedRevision/catalogVersion and domainRevisions containing exactly profile and preferences.

| Question | Type / answer | Canonical field |
|---|---|---|
| FIRST_NAME | TEXT / textValue | Player.firstName |
| LAST_NAME | TEXT / textValue | Player.lastName |
| DISPLAY_NAME | TEXT / textValue | Player.displayName |
| COUNTRY | COUNTRY_SELECT / optionKey | Player.countryCode |
| PREFERRED_LANGUAGE | LOCALE_SELECT / optionKey | PlayerPreferences.preferredLocale |

OnboardingAnswer adds nullable textValue; selectors use optionKey and reject textValue, TEXT rejects optionKey. HTTP parsing checks JSON string types explicitly; numeric-to-string coercion is rejected. Unknown fields, including client-selected playerId, remain rejected. Existing selector request representation is preserved; added optional null metadata is excluded from serialization.

SaveStepRequest additionally accepts optional detectedTimeZone on Basic Profile only. Own OnboardingResponse adds optional basicProfile {firstName,lastName,displayName,countryCode,preferredLocale,timeZone} for catalogs containing profile questions. Derived typed answers are returned from canonical domains, including prefilled existing displayName. No duplicate answer collection. Existing displayName alone never completes Basic Profile. Responses are authenticated self-only, no-store; catalog responses include presentation questions, not private player values. No UID/email/token is added to these DTOs.

## Validation and timezone policy

First/last names reuse PlayerProfileRules.name: trim, Unicode NFC, 1–80 grapheme clusters and rejection of control/bidi formatting controls. Names are mandatory in v2 onboarding despite remaining nullable in the legacy domain. No ASCII constraint on private names; no inference from email/alias and no composition of the public alias.

Display name uses the unchanged DisplayNameRules policy: 3–16 ASCII letters/digits/underscore/hyphen, protected-name rejection, no new uniqueness rule or normalization. This preserves existing product compatibility; it is intentionally distinct from Unicode private names.

Country reuses the server ISO alpha-2 whitelist with trim/uppercase. The explicit answer is required; no default or inference from locale, timezone or location. Preferred language requires explicit es or en; unsupported values (including a regional tag in this explicit answer) return LANGUAGE_UNSUPPORTED. Catalog locale queries continue using BE-02 normalization/fallback.

Detected timezone is optional system metadata. A valid IANA zone (or UTC) initializes a currently null preference. Missing/invalid string retains null/unknown and never blocks completion. Existing non-null preference wins, preserving an explicit Settings override; this automatic suggestion path deliberately does not overwrite it. There is no provenance field added to guess whether a prior timezone was automatic. Explicit Settings timezone replacement remains outside this onboarding action. No GPS, country-based timezone inference or visible timezone step. This task does not introduce an automatic bootstrap write.

## Atomicity, revisions and public projection

Existing BE-03 transaction/receipt owner remains in control. Snapshot retains the raw Player map so root writes preserve metadata not represented in the typed Player (test-account flags, account/lifecycle data and other fields). A successful Basic Profile operation atomically buffers Player, preferences/current, onboarding/current, and the operation receipt. Domino is not touched by Basic Profile.

Player.profileRevision and preferences.revision each increment only when their domain changes; onboarding revision advances once for the accepted operation. Both expected domain revisions and expected onboarding revision must match. Player.language remains the compatibility mirror of canonical preferredLocale, changed in the same transaction. Retried receipts return the stored semantic response within the BE-03 30-day window without duplicate advancement or new startedAt.

If a public identity already exists and alias changes, its existing publicPlayerProfiles document is read and merged within the SAME transaction using only displayName, normalizedDisplayName and updatedAt. No Social profile is created and no first/last/country/locale/timezone is copied into the public projection. Missing/inconsistent referenced projection fails closed before commit. Reads precede actual SDK writes; the Firestore adapter retains buffered-write ordering.

Back/edit preserves later outcomes/answers and catalog pin. Saving edits returns to the first unfinished step per the existing BE-03 frontier rule; locale does not reset progress or restart onboarding. Reload reads canonical prefill and resumes EXPERIENCE_STEP after the first successful Basic Profile save.

## Completion and boundaries

V2 independently requires Basic Profile outcome and all five valid domain values, Experience outcome/value, Coach outcome and authoritative pinned Coach validation. Missing domain data or invalid/corrupt preferences fail closed; malformed persisted preference documents are storage/state errors, not accepted defaults. Optional contacts/membership may be skipped or unvisited under existing BE-03 policy. Timezone is not inspected for completion.

No trial consumption/grant/activation changes, purchase requirement, Membership catalog, Contacts integration, FCM, Player avatar or Unity routing. No new Firestore collections/indexes. Existing private root/preferences paths and public alias projection are reused. No live catalog seed or v2 publication.

## Exact change inventory

New:
- server/domino/src/main/kotlin/com/teamfho/domino/catalog/OnboardingCatalogV2.kt
- server/domino/src/main/kotlin/com/teamfho/domino/player/OnboardingProfileBinding.kt
- server/domino/src/test/kotlin/com/teamfho/domino/catalog/OnboardingCatalogV2Tests.kt
- server/domino/src/test/kotlin/com/teamfho/domino/player/BasicProfileOnboardingTests.kt
- server/domino/src/test/kotlin/com/teamfho/domino/player/BasicProfileFirestoreTests.kt
- server/domino/src/test/kotlin/com/teamfho/domino/player/BasicProfileHttpTests.kt
- this report

Modified:
- OnboardingCatalog.kt: profile question types, bindings, capability and validation.
- CoachCatalogRepository.kt: single prepared-v2 definition delegation.
- OnboardingProgressModels.kt: typed text answers, optional timezone metadata and private prefill projection.
- OnboardingProgressService.kt: canonical profile domain save/prefill/completion, revisions and atomic root/preferences/public-alias writes.
- OnboardingProgressHttp.kt: strict JSON answer/metadata type checks.
- CoachCatalogTests.kt: v2 expectations now account for Basic Profile while verifying old definitions remain unchanged.
- CoachCatalogHttpTests.kt and CoachSelectionTests.kt: explicitly construct their isolated pre-profile Coach fixtures, preserving their Coach-focused regression scenarios rather than silently running the new Basic Profile flow.

Total: 15 scoped files. Unrelated prior design reports and the 102 protected files are excluded.

## Validation evidence

Initial domain/Coach/onboarding tests compiled and passed. Expanded negative HTTP test exposed numeric-to-string coercion; production request validation was tightened, not weakened. A completed-v1 test sent revision zero for revision-four state and was corrected to exercise the completed-state guard after the proper revision check. Final focused run: 121 tests, no failures, including HTTP, SDK adapter, catalog, foundation, BE-03 and Coach regressions.

New tests cover the exact v1 resource hash, v2 seed idempotency, es/en parity, fields/types/normalization, no default country, private prefill, stale revisions, operation retry, Back/language changes, timezone semantics, atomic buffering/retry, root metadata preservation, public alias privacy and failure atomicity, completion negatives and v1 compatibility. SDK tests use doubles; HTTP uses MockMvc and synthetic authenticated principals. No real Firebase/Unity test or remote Firestore contention claim.

## Final validation result

Full backend `:test`: BUILD SUCCESSFUL, 821 XML cases = 797 PASS, 0 FAIL, 24 SKIPPED. New BE-05: 32 PASS (24 domain/state, 5 catalog, 2 HTTP, 1 SDK atomic persistence). Existing BE-01/BE-02/BE-03/BE-04, Auth, Player bootstrap, Entitlements, Social, History/Replay and lifecycle executed regressions pass. Existing environment-gated tests remain skipped; no real Firebase/Unity retest. Known Redis loopback/shutdown warnings, JVM class-sharing and Gradle deprecation messages remain visible and are not counted as test failures.

Scoped security/identity/mojibake scan: 15 files, zero matches. Protected hash check: 0/102 modified. No staged files; base SHA unchanged.

```text
BASE_SHA=01bf70821f5739cf8e2e841eed5d114bbf1371dd
ONBOARDING_V1_MUTATED=NO
ONBOARDING_V2_IMPLEMENTED=YES
ONBOARDING_V2_STEP_COUNT=5
V2_STEP_KEYS=BASIC_PROFILE_STEP,EXPERIENCE_STEP,COACH_STEP,CONTACTS_STEP,MEMBERSHIP_STEP
BASIC_PROFILE_STEP_REQUIRED=YES
FIRST_NAME_DOMAIN_BOUND=YES
LAST_NAME_DOMAIN_BOUND=YES
DISPLAY_NAME_DOMAIN_BOUND=YES
COUNTRY_DOMAIN_BOUND=YES
PREFERRED_LOCALE_DOMAIN_BOUND=YES
DOB_IMPLEMENTED=NO
PLAYER_AVATAR_IMPLEMENTED=NO
COUNTRY_USER_SELECTED=YES
DEFAULT_COUNTRY_ASSIGNED=NO
PREFERRED_LOCALE_VALUES=es,en
LOCALE_CHANGE_PRESERVES_PROGRESS=YES
CATALOG_VERSION_LANGUAGE_INDEPENDENT=YES
TIMEZONE_VISIBLE_ONBOARDING_STEP=NO
TIMEZONE_GPS_REQUIRED=NO
TIMEZONE_BLOCKS_COMPLETION=NO
BASIC_PROFILE_ATOMIC_WRITE=YES
BASIC_PROFILE_STALE_REVISION_REJECTED=YES
BASIC_PROFILE_SAVE_IDEMPOTENT=YES
BASIC_PROFILE_PREFILL=PASS
BASIC_PROFILE_RESUME_NEXT=EXPERIENCE_STEP
V1_IN_PROGRESS_AUTO_MIGRATED_TO_V2=NO
V1_COMPLETED_REOPENED=NO
ONBOARDING_V2_COACH_CATALOG_VERSION=1
V2_COMPLETION_REQUIRES_BASIC_PROFILE=YES
V2_COMPLETION_REQUIRES_EXPERIENCE=YES
V2_COMPLETION_REQUIRES_COACH=YES
CONTACTS_INTEGRATION_IMPLEMENTED=NO
MEMBERSHIP_CATALOG_IMPLEMENTED=NO
BOOTSTRAP_AUTO_GRANTS_TRIAL_BEHAVIOR_CHANGED=NO
TRIAL_ACTIVATION_ENDPOINT_IMPLEMENTED=NO
NOT_NOW_CONSUMES_TRIAL=NO
ONBOARDING_ROLLOUT_ENABLED=NO
REAL_FIRESTORE_V2_PUBLISHED=NO
NEW_BE05_TESTS=32_PASS
FULL_SUITE=797_PASS_0_FAIL_24_SKIPPED
COMPILER_ERRORS=0
PREEXISTING_PROTECTED_FILES_MODIFIED=0/102
SECRET_SCAN=PASS
COMMIT=NONE
PUSH=NONE
DEPLOY=NO
ONB_BE_05_SUCCESS=YES
ONB_BE_06_STARTED=NO
NEXT=ONB-BE-05 IMPLEMENTATION REVIEW
```

## Authorized final checkpoint review

Review base: 01bf70821f5739cf8e2e841eed5d114bbf1371dd. No product/test source changes during final review; retained 32 new PASS and full 797 PASS / 0 FAIL / 24 SKIPPED. SUITE_REPEATED=NO. Earlier COMMIT/PUSH=NONE fields describe the implementation gate; final commit/remote SHA are recorded in the checkpoint response.

All 121 pending paths classified: 102 PROTECTED, 4 UNRELATED prior design reports, 15 BE-05 candidates. New OnboardingProfileBinding is BASIC_PROFILE_DOMAIN/DOMAIN_BINDING/VALIDATION/TIMEZONE; OnboardingProgressService changes are PLAYER_PREFERENCES/ATOMICITY/DOMAIN_BINDING; models and HTTP are domain DTO/VALIDATION; OnboardingCatalog and OnboardingCatalogV2 plus CoachCatalogRepository delegation are CATALOG_V2; seven test files (four new, three adjusted fixtures) are TEST; this report is VALIDATION_REPORT. UNCLASSIFIED_FILES=0, UNRELATED_FILES_FOR_BE05=0. BE-04 has no leftover unpublished checkpoint changes; deltas to its tracked files are the documented BE-05 integration/fixture updates, not restaging the previous commit.

Contracts confirmed: names trim/NFC, 1-80 grapheme clusters, no controls/bidi formatting controls; alias unchanged 3-16 ASCII letters/digits/underscore/hyphen and reserved-name exclusions; explicit country normalized to ISO alpha-2; explicit locale exactly es/en. Existing alias is preserved until explicitly changed and available for prefill, never sufficient by itself to complete Basic Profile. No country inference/default. detectedTimeZone initializes null only, validates IANA/UTC with max128 length, ignores invalid metadata, never blocks completion or overwrites existing timezone.

Locale edits preserve pin and later answers/outcomes; accepted save uses the existing server frontier cursor rule, not an onboarding restart. Root/preferences/progress/public-alias projection/receipt are one buffered Firestore transaction. Reads precede writes, domain revisions guard stale updates and receipts retain 30-day semantics. Pinned/completed v1 is unchanged; v2 explicitly references Coach v1. No live Firebase/Unity retest, catalog publication, rollout, trial change, DOB, Player avatar or BE-06 work.

Scoped content/security review found zero real emails, raw user identifiers, tokens, secrets or mojibake. Synthetic fixture identifiers only. Protected SHA-256 comparison is 0/102. Only the 15 explicitly listed files may enter this checkpoint.
