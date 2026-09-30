# ONB-BE-02 — Onboarding catalog and localization

Base: 455c9d358e54511583f9ed85b550b9d0e5f1a3f9. Implementation date: 2026-09-30. No commit, push, deployment or live seed. Rollout stays disabled.

## Mock inventory audited before seed

Source: Assets/_Domino/AppShellMock/MockShellState.cs (MockPage, MockCatalog.Experience); MockShellView.cs (ExperienceScreen, CoachSelection, Contacts, MembershipScreen). All 40 enum routes classified below. The catalog subset contains four stages/four steps, two questions and four experience options. No generic conversion of routes to questions.

| Classification | Exact Mock routes | Production mapping |
|---|---|---|
| ONBOARDING_STEP | Experience | EXPERIENCE_STEP; stage EXPERIENCE; question DOMINO_EXPERIENCE |
| ONBOARDING_STEP | CoachSelection | COACH_STEP; stage COACH; question COACH_SELECTION |
| ONBOARDING_STEP | Contacts | CONTACTS_STEP; stage CONTACTS; optional action, UNAVAILABLE |
| ONBOARDING_STEP | Trial | MEMBERSHIP_STEP; stage MEMBERSHIP; optional presentation action |
| ONBOARDING_SUPPORT_VIEW | ContactResults, Plans | No extra steps/questions; fictional contacts and plan detail omitted |
| PREVIEW_ONLY | Provider, OtherDevice, Placeholder | No catalog entries; simulated provider/device/generic placeholder |
| NON_ONBOARDING | Welcome, SignIn, EmailRegister, EmailSignIn, ExistingEmail, SendEmail, CheckEmail, LoadAccount, Phone, Otp, ProviderConflict, SecureAccount, Home, Puzzles, Learn, Watch, Menu, Friends, Messages, Conversation, Stats, Coach, Theme, Membership, Settings, Support, Play, Bots, CoachGame, Profile, ProfileHistory | No catalog entries |
| OTHER | None | None |

Experience Spanish labels/supporting copy exactly match the Mock, including the title line break. English translations preserve the same four semantics: BEGINNER, RULES_KNOWN, STRATEGY, COMPETITIVE. The shared stable key is never localized. Coach title comes from the Mock; portraits, names and greeting are not duplicated. Contacts title comes from the Mock; its development-only fictional-list explanation is not production copy. Membership uses neutral navigation metadata, not the Mock's fixed promotional headline: a promise of a free week would incorrectly bypass later policy/catalog authority. No plan/price/trial claims are seeded.

No basic-profile questions are activated: final ONB-00 explicitly excludes TEXT/COUNTRY_SELECT/LOCALE_SELECT from V1 onboarding. No DOB, country default, names inferred from alias, visible timezone question or language question. Timezone remains optional system initialization/preferences foundation, not a new BE-02 write.

## Persistence, lifecycle and explicit seed

- Immutable bounded publication: onboardingCatalogs/{catalogVersion}; initial resource onboarding-catalog-v1.json, schemaVersion=1/catalogVersion=1.
- Current pointer: systemConfig/onboardingCatalog, publishedVersion. Monotonic publication; old documents retained. Re-running v1 after a newer version validates existing v1 without rewinding the pointer.
- Publication owns steps, questions, options, translations and dependency metadata in one document. IDs are stable keys, sortOrder/key sorting is explicit. Stage is a stable step field, not a speculative collection.
- Publisher validates reference integrity, graph order, enum/typeVersion/binding allowlists, unique keys, es/en completeness, string limits, count bounds and 512 KiB serialized limit before transaction.
- All transaction reads precede writes; historical content conflicts abort. Existing identical version does not write again. Reference versions, if supplied, must exist in coachCatalogs/membershipCatalogs. No implementation of those catalogs is included.
- BE-02's explicitly permitted future-capability references use null coachCatalogVersion/membershipCatalogVersion and UNAVAILABLE presentation. This is not a complete executable onboarding graph: the required Coach step cannot be completed or skipped through this endpoint. Future catalogs require a new immutable publication with real pins, not an edit of v1. Rollout remains disabled.
- OnboardingCatalogSeed.run is explicit, with standalone guarded CLI main using the existing real-Firestore opt-in guard and --seed-if-absent. No startup runner or read-time seed. It was tested locally, never executed against live Firestore.
- publishedAt is fixed release metadata in the canonical seed, not a semantic version or timestamp of a claimed live deployment.
- No indexes: all lookups use exact document paths.

## Read contract and security

GET /api/v1/onboarding/catalog?locale=es&version=1 (version optional for current pointer). Existing Firebase /api authentication applies; additional read-only ACTIVE Player lookup occurs before serving even a conditional 304. It does not bootstrap or create public Social profiles. Missing Player=404, inactive Player=403, missing catalog=404, invalid nonpositive version=400, unavailable dependency/invalid stored publication=503. Errors use code/message/requestId and no-store; no raw exception content is returned.

Explicit OnboardingCatalogResponse contains catalogVersion, resolved locale, requiredCapabilities, optional dependency versions, and ordered localized steps/questions/options. Persistence translation maps, domain bindings and publication/admin metadata are excluded. Response contains no account/session/personal data. Question types are SINGLE_SELECT and COACH_SELECT, typeVersion1 only. No generic field paths or arbitrary controls.

Locale policy: case-insensitive es/en and two-letter regional variants normalize to base; unsupported/missing values fall back to en (as explicitly requested for BE-02). Publication requires complete es/en. Defensive read of an older incomplete non-default translation falls back for the entire response to en; incomplete en fails closed. This intentionally resolves the older matrix's LANGUAGE_UNSUPPORTED entry in favor of the current explicit fallback instruction.

Private ETag includes immutable version and resolved locale. Current-pointer cache 300 seconds; explicit immutable-version cache 86400 seconds. Server publication cache is bounded to 32 versions and keyed by version; localization is applied independently per request. No shared localized response cache. HTTP URLs include locale/version, and ETag differs across resolved locales. Historical versions remain individually readable with active filtering based on that version's flags. Inactive entities remain in the stored immutable publication; current response excludes them and derives the next active step. Reads never publish or mutate Player, progress, preferences or entitlements.

## Scope and files

Production additions only under server/domino:
- src/main/kotlin/com/teamfho/domino/catalog/OnboardingCatalog.kt: models, publication validation, explicit localized DTO projection.
- src/main/kotlin/com/teamfho/domino/catalog/OnboardingCatalogRepository.kt: Firestore adapter, publication checks, explicit seed.
- src/main/kotlin/com/teamfho/domino/catalog/OnboardingCatalogHttp.kt: bounded cache, read-only access, endpoint/configuration.
- src/main/resources/onboarding-catalog-v1.json: single source of bilingual catalog copy.

Tests: OnboardingCatalogTests.kt, OnboardingCatalogHttpTests.kt, OnboardingCatalogPersistenceTests.kt under the corresponding test catalog package. This report is the only other new artifact.

No Unity, Player creation, answer persistence, completion, routing, trial, Social lifecycle, Coach/Membership catalog, FCM or configuration-value changes. No schema expansion outside the approved catalog publication and pointer. No real Firebase or Unity authentication retest.

## Validation

Initial compilation caught nullable ETag handling in test code; corrected before running tests. First 13 focused cases passed. Then added SDK transaction retry/idempotency and cache-expiry tests plus graph validation before the full regression run. SDK doubles verify adapter operations and transaction ordering; they are not real Firestore contention tests. Existing Netty/Redis loopback shutdown warnings are recorded separately from test failures.

## Final results

Full root backend :test: BUILD SUCCESSFUL. 727 XML cases: 703 PASS, 0 FAIL, 24 SKIPPED. New catalog tests 15/15 (9 domain/localization, 4 HTTP/security, 2 persistence/cache). PlayerController 33/33; Firebase verifier/security 45/45; Entitlements 17/17; History/Replay 13/13; Social lifecycle 4/4. The 24 skipped cases remain environment-gated Redis/parity integrations, not passing live tests. REAL_FIRESTORE/EMULATOR tags excluded. AUTH evidence is local backend regression only.

Source/report secret pattern scan plus fixture/content review: PASS, no credential or real account data added. Protected inventory hash comparison: 102 checked, zero changed. No existing production source file was edited; all eight task files are new. Current HEAD still equals base.

```text
BASE_SHA=455c9d358e54511583f9ed85b550b9d0e5f1a3f9
CATALOG_VERSION=1
CATALOG_MODEL_IMPLEMENTED=YES
ONBOARDING_CATALOG_VERSIONED=YES
EXISTING_MOCK_ROUTES_AUDITED=40
PRODUCTION_CATALOG_STEP_COUNT=4
MOCK_TO_CATALOG_MAPPING=EXPERIENCE_COACH_CONTACTS_TRIAL_AS_DOCUMENTED
SUPPORTED_LOCALES=es,en
DEFAULT_LOCALE=en
LOCALE_FALLBACK_POLICY=REGIONAL_TO_BASE_UNSUPPORTED_TO_EN_MISSING_TRANSLATION_WHOLE_RESPONSE_EN
QUESTION_TYPES_IMPLEMENTED=SINGLE_SELECT,COACH_SELECT
UNKNOWN_QUESTION_TYPE_REJECTED=YES
DOMINO_EXPERIENCE_QUESTION_KEY=DOMINO_EXPERIENCE
DOMINO_EXPERIENCE_OPTION_KEYS=BEGINNER,RULES_KNOWN,STRATEGY,COMPETITIVE
EXPERIENCE_OPTION_PARITY_ES_EN=YES
DOB_QUESTION_SEEDED=NO
DEFAULT_COUNTRY=NONE
TIMEZONE_ONBOARDING_PRESENTATION=NOT_A_VISIBLE_QUESTION
COACH_DATA_DUPLICATED_IN_ONBOARDING_OPTIONS=NO
MEMBERSHIP_DATA_DUPLICATED_IN_ONBOARDING_OPTIONS=NO
CATALOG_READ_ENDPOINT=GET /api/v1/onboarding/catalog
CATALOG_RESPONSE_DTO=OnboardingCatalogResponse
PERSISTENCE_ENTITY_EXPOSED_DIRECTLY=NO
PLAYER_DATA_IN_CATALOG_RESPONSE=0
CATALOG_SEED_IDEMPOTENT=YES
CATALOG_SEMANTIC_PARITY_ES_EN=PASS
COLLECTIONS_ADDED=onboardingCatalogs
DOCUMENT_STRUCTURE=onboardingCatalogs/{version};systemConfig/onboardingCatalog
INDEXES_ADDED=0
SPECULATIVE_INDEXES_ADDED=0
CATALOG_READ_SIDE_EFFECTS=0
PLAYER_ONBOARDING_ANSWER_WRITE_IMPLEMENTED=NO
SAVE_STEP_ENDPOINT_IMPLEMENTED=NO
COMPLETE_ENDPOINT_IMPLEMENTED=NO
EXISTING_PLAYER_STATE_CHANGED_BY_CATALOG=NO
BOOTSTRAP_AUTO_GRANTS_TRIAL_BEHAVIOR_CHANGED=NO
TRIAL_ACTIVATION_ENDPOINT_IMPLEMENTED=NO
ONBOARDING_ROLLOUT_ENABLED=NO
AUTH_01_REGRESSION=PASS_BACKEND_LOCAL
AUTH_02_REGRESSION=PASS_BACKEND_LOCAL
PLAYER_BOOTSTRAP_REGRESSION=PASS
ENTITLEMENT_REGRESSION=PASS
SOCIAL_REGRESSION=PASS_EXECUTED_LOCAL_TESTS_EXTERNAL_CASES_SKIPPED
HISTORY_REPLAY_REGRESSION=PASS
SOCIAL_INVALIDATION_LIFECYCLE_REGRESSION=PASS
TESTS_RUN=727_REPORTED_CASES
TESTS_PASS=703
TESTS_FAIL=0
TESTS_SKIPPED=24
COMPILER_ERRORS=0
PREEXISTING_PROTECTED_FILES_MODIFIED=0/102
SECRET_SCAN=PASS
COMMIT=NONE
PUSH=NONE
DEPLOY=NO
ONB_BE_02_SUCCESS=YES
NEXT=ONB-BE-02 IMPLEMENTATION REVIEW
```

## Final checkpoint review

Source unchanged since the passing run; SUITE_REPEATED=NO. This section records checkpoint preparation; previous COMMIT=NONE/PUSH=NONE fields describe the implementation review stage.

| Step key | Stage | Order | Required / skippable | Question / type | Options | Mock |
|---|---|---:|---|---|---|---|
| EXPERIENCE_STEP | EXPERIENCE | 10 | true / false | DOMINO_EXPERIENCE / SINGLE_SELECT | BEGINNER, RULES_KNOWN, STRATEGY, COMPETITIVE | Experience |
| COACH_STEP | COACH | 20 | true / false | COACH_SELECTION / COACH_SELECT | None; future capability | CoachSelection |
| CONTACTS_STEP | CONTACTS | 30 | false / true | None; unavailable action metadata | None | Contacts |
| MEMBERSHIP_STEP | MEMBERSHIP | 40 | false / true | None; unavailable navigation metadata | None | Trial |

All four are active metadata entries; only experience is presently available. No plan/trial presentation was implemented. Both locales share the exact structural records; only translation values differ. Locale is the optional query parameter locale; version is optional positive integer. Endpoint requires Firebase authentication and read-only ACTIVE Player verification. DTO is OnboardingCatalogResponse, never the persistence entity. Seed is immutable/create-only for publication content, idempotent, and never runs on read or startup.

File classification (eight explicit candidates): OnboardingCatalog.kt=CATALOG_DOMAIN/LOCALIZATION; OnboardingCatalogRepository.kt=CATALOG_PERSISTENCE/CATALOG_SEED; OnboardingCatalogHttp.kt=CATALOG_API; onboarding-catalog-v1.json=CATALOG_SEED/LOCALIZATION; three OnboardingCatalog*Tests.kt=TEST; this report=VALIDATION. The 102 protected pending files and four previous audit/design reports are excluded. ONB-BE-01 and lifecycle changes are already committed and have no pending diff.

UNAPPROVED_CATALOG_STEPS=0
UNCLASSIFIED_FILES=0
UNRELATED_FILES_FOR_BE02=0
ONB_BE_01_PENDING=0
PREEXISTING_LIFECYCLE_FIX_PENDING=0
COACH_CATALOG_IMPLEMENTED=NO
COACH_RECORDS_SEEDED=0
MEMBERSHIP_CATALOG_IMPLEMENTED=NO
MEMBERSHIP_PLAN_RECORDS_SEEDED=0
MEMBERSHIP_FEATURE_RECORDS_SEEDED=0
ADMIN_ONLY_METADATA_EXPOSED=NO
NEW_PLAYER_CREATION_BEHAVIOR_CHANGED=NO
ONBOARDING_ROUTING_IMPLEMENTED=NO
REAL_FIRESTORE_SEED_EXECUTED=NO
RAW_UIDS_IN_REPORT=0
REAL_EMAILS_IN_REPORT=0
TOKENS_IN_REPORT=0
SECRETS_IN_REPORT=0
REAL_FIREBASE_AUTH_RETESTED=NO
UNITY_AUTH_RETESTED=NO
DEPLOY=NO
ONBOARDING_ROLLOUT_ENABLED=NO
NEXT=ONB-BE-03 AUTHORIZATION
