# ONB-BE-04 — Coach catalog and authoritative selection

Base: `05aeaa57e1a752be36d339acafb575616881924a`. Implementation/test only. No live Firestore seed, rollout, Unity changes, commit, push or deployment.

## Audited inventory and asset mapping

Source: `client/DominoGame/Assets/_Domino/AppShellMock/MockShellState.cs`, `MockCatalog.Coaches`. The rendering path in `MockShellView.cs` loads `AppShellMockCoaches/coach_` plus the lowercase ID. All ten existing PNG assets were found under the mock Resources directory; no portraits were generated or modified.

| Stable key | Existing ID | Name in es/en | Avatar key | Existing resource reference |
|---|---|---|---|---|
| LUCIA | lucia | Lucía | COACH_LUCIA | AppShellMockCoaches/coach_lucia.png |
| ELENA | elena | Elena | COACH_ELENA | AppShellMockCoaches/coach_elena.png |
| AMARA | amara | Amara | COACH_AMARA | AppShellMockCoaches/coach_amara.png |
| MEI | mei | Mei | COACH_MEI | AppShellMockCoaches/coach_mei.png |
| SOFIA | sofia | Sofía | COACH_SOFIA | AppShellMockCoaches/coach_sofia.png |
| DAVID | david | David | COACH_DAVID | AppShellMockCoaches/coach_david.png |
| MATEO | mateo | Mateo | COACH_MATEO | AppShellMockCoaches/coach_mateo.png |
| GABRIEL | gabriel | Gabriel | COACH_GABRIEL | AppShellMockCoaches/coach_gabriel.png |
| LEO | leo | Leo | COACH_LEO | AppShellMockCoaches/coach_leo.png |
| OMAR | omar | Omar | COACH_OMAR | AppShellMockCoaches/coach_omar.png |

Stable identity is independent of display name, order and portrait reference. Initial assetVersion is 1. storagePath is a trusted relative reference to the audited bundled resource, not an uploaded/cloud-hosted asset or URL. A future publication may change the asset reference/version without changing the coach key or existing player reference. No Player avatar functionality is included.

Spanish copy preserves the existing greeting split into shortDescription `Hola, soy {name}.` and description `Te enseñaré a jugar dominó.` English uses `Hi, I am {name}.` and `I will teach you to play dominoes.` Names and semantic identity are identical across locales; no invented gameplay/personality fields.

## Persistence and immutable publication

New structure: `coachCatalogs/{version}` and pointer `systemConfig/coachCatalog`. No indexes added. Bounded publication fields: schemaVersion, catalogVersion, defaultLocale, supportedLocales, coaches, translations, publishedAt. Coach fields: key, active, sortOrder, avatarKey, assetVersion, storagePath. Translation fields: name, shortDescription, description. No image bytes, secrets, user data or mutable player data in publications.

`CoachCatalogSeed.canonical()` loads the version-1 resource. `run(repository)` publishes through a transaction: read publication and pointer before writes, create absent publication, reject different content for an existing version, advance pointer monotonically, and preserve a newer pointer on historical re-seed. There is no startup seed or player-facing publish endpoint. Seed entry points are administrative Kotlin functions accepting a repository; no real seed was executed.

Coach catalog versions are independent of onboarding versions, linked through onboarding.coachCatalogVersion. Existing onboarding v1 is immutable and unlinked; it remains blocked at Coach. `compatibleOnboarding()` constructs onboarding v2 with Coach v1 pinned and otherwise the same steps/questions/options. `publishCompatibleOnboarding(repository)` is a separate explicit publication operation; the existing Firestore onboarding publisher checks that Coach v1 exists. Neither publication operation was run against real storage. Existing IN_PROGRESS v1 sessions are not migrated or reinterpreted. Future authorization must separately publish the compatible version before expecting new real starts to complete; rollout remains disabled.

## Read API and presentation DTO

`GET /api/v1/coaches?locale=es&version=1` uses existing Firebase authentication and the shared active-player access check. No request-supplied player identity. Version is optional. CoachCatalogResponse: catalogVersion, resolvedLocale, items. Each item: key, name, shortDescription, description, avatar {key,assetVersion,storagePath}, selectable, sortOrder. Persistence entities and admin publication metadata are not returned.

Locale normalization delegates to OnboardingCatalogLocalization.locale: es-US to es, en-US to en, unsupported/missing to en. Missing locale translations trigger whole-response English fallback. Results sort by sortOrder then stable key. Current discovery (no version) excludes inactive coaches. Explicit-version presentation includes inactive historical entries with selectable=false so existing selection can still be displayed. Selection is always server-validated regardless of presentation. Private cache headers are 300 seconds for current and 86400 for versioned reads; ETags distinguish locale, publication and active/historical projection. Server-side reads currently access persistence directly; no additional in-process cache was introduced.

Invalid version returns 400; missing publication 404; malformed/unavailable storage 503; inactive/missing player retains existing 403/404 policy. Error responses contain safe code/message/requestId and no-store.

## Single selection owner and completion

Existing `PUT /api/v1/player/onboarding/steps/COACH_STEP` is the sole selection write path. Request remains SaveStepRequest: operationId, expectedRevision, catalogVersion (onboarding version), domainRevisions, action and typed answers containing the stable Coach key. Identity comes from the authenticated principal; unknown request fields are rejected by existing strict parsing.

Spring wires AuthoritativeCoachValidation into BE-03. The validator reads the pinned Coach publication through the SAME OnboardingProgressTransaction before any buffered writes. Missing/unpinned/malformed publication fails closed. New selection requires an existing active key. Unknown, localized, lowercase or arbitrary identity is rejected. Only preferredCoachKey and selectedCoachCatalogVersion are persisted in DominoProfile; no presentation copy is stored there.

Historical policy: a matching existing key/version can be retained even when inactive in its authoritative historical publication. Completion uses the same existence validation; missing keys or mismatched versions still fail. A later publication making a coach inactive does not invalidate an earlier immutable pin. Moving away from an inactive choice does not allow selecting it again as a new choice. No silent substitution or migration. Test-only in-memory changes also exercise historical inactive documents; production publications remain immutable.

Existing revision checks and account/endpoint/payload receipts remain authoritative (30-day idempotency window). Back/change is supported while in progress. Experience code is unchanged. Completion still independently requires all required steps and valid experience/coach; optional contacts/membership can be skipped or unvisited per BE-03. No Entitlements or trial writes, no required purchase, no new routing. Legacy completed exemption stays intact.

## Exact changed files

New:
- server/domino/src/main/kotlin/com/teamfho/domino/catalog/CoachCatalog.kt
- server/domino/src/main/kotlin/com/teamfho/domino/catalog/CoachCatalogRepository.kt
- server/domino/src/main/kotlin/com/teamfho/domino/catalog/CoachCatalogHttp.kt
- server/domino/src/main/resources/coach-catalog-v1.json
- server/domino/src/test/kotlin/com/teamfho/domino/catalog/CoachCatalogTests.kt
- server/domino/src/test/kotlin/com/teamfho/domino/catalog/CoachCatalogHttpTests.kt
- server/domino/src/test/kotlin/com/teamfho/domino/catalog/CoachCatalogPersistenceTests.kt
- server/domino/src/test/kotlin/com/teamfho/domino/player/CoachSelectionTests.kt
- this report

Modified:
- OnboardingProgressModels.kt: default historical-validation method on the existing Coach seam.
- OnboardingProgressService.kt: retain existing key/version and independently validate historical selection on completion.
- OnboardingProgressHttp.kt: production wiring of the authoritative validator.

No BE-02 source/resource, existing test, Unity file, bootstrap, Entitlement, deployment or configuration file was changed. Four pre-existing design reports and 102 protected files remain outside this task.

## Validation scope and limitations

31 new tests: 12 catalog, 14 selection/state, 4 HTTP/wiring/security, 1 Firestore adapter. Focused run also includes all 31 BE-03 tests. The first run found two fixture errors (reinitialized pointer and invalid zero revision in an IN_PROGRESS state); corrected only fixtures. Subsequent focused run passes. Persistence tests execute the actual Firestore adapter against SDK doubles, not remote Firestore. HTTP tests use MockMvc, authenticated fixture principals and production Spring wiring. No real Firebase authentication, Unity validation or remote concurrency test is claimed.

The local suite emits known Redis loopback connection/shutdown messages, JVM class-sharing warning and Gradle deprecation warnings. These are not reported as zero warnings or as new Coach test failures.

## Final result

Full backend suite: BUILD SUCCESSFUL; 789 XML cases = 765 PASS, 0 FAIL, 24 SKIPPED. New BE-04: 31 PASS. BE-02 catalog: 15 PASS. BE-03 progress: 31 PASS. PlayerController: 33 PASS. Firebase security: 23 PASS. Entitlements: 17 PASS. Match History/Replay: 13 PASS. SocialInvalidationLifecycle: 4 PASS. Other executed Auth/Social regressions pass within the full suite. Existing environment-gated cases remain skipped; no real Firebase/Unity claim.

```text
BASE_SHA=05aeaa57e1a752be36d339acafb575616881924a
COACH_COUNT_AUDITED=10
COACH_KEYS_AUDITED=LUCIA,ELENA,AMARA,MEI,SOFIA,DAVID,MATEO,GABRIEL,LEO,OMAR
COACH_CATALOG_VERSION=1
COACH_MODEL_IMPLEMENTED=YES
COACH_TRANSLATIONS_IMPLEMENTED=es,en
COACH_SEMANTIC_PARITY_ES_EN=YES
COACH_MOJIBAKE_MARKERS=0
COACH_AVATAR_STRATEGY=VERSIONED_BUNDLED_RESOURCE_REFERENCE
COACH_AVATAR_MAPPING=10/10_EXISTING_ASSETS_CONFIRMED
COACH_AVATAR_BINARY_IN_DATABASE=NO
COACH_SEED_IDEMPOTENT=YES
REAL_FIRESTORE_COACH_SEED_EXECUTED=NO
COACH_READ_ENDPOINT=GET /api/v1/coaches?locale=<locale>&version=<optional>
COACH_RESPONSE_DTO=CoachCatalogResponse
COACH_LOCALE_POLICY_REUSES_ONBOARDING_POLICY=YES
PLAYER_COACH_SELECTION_MODEL=DominoProfile.preferredCoachKey+selectedCoachCatalogVersion
COACH_SELECTION_DOMAIN_OWNER_COUNT=1
COACH_STEP_AUTHORITATIVE_VALIDATION=YES
ARBITRARY_COACH_SELECTION_ACCEPTED=NO
COACH_STEP_IDEMPOTENT=YES
COACH_STEP_STALE_REVISION_REJECTED=YES
COACH_CHANGE_DURING_IN_PROGRESS_SUPPORTED=YES
ONBOARDING_COMPLETION_COACH_VALIDATION=AUTHORITATIVE
ONBOARDING_COMPLETABLE_WITHOUT_COACH=NO
FIRST_REAL_ONBOARDING_COMPLETION_PATH=PASS_BACKEND_TEST
EXPERIENCE_REGRESSION=PASS
CONTACTS_INTEGRATION_IMPLEMENTED=NO
MEMBERSHIP_CATALOG_IMPLEMENTED=NO
MEMBERSHIP_PURCHASE_REQUIRED=NO
BOOTSTRAP_AUTO_GRANTS_TRIAL_BEHAVIOR_CHANGED=NO
TRIAL_ACTIVATION_ENDPOINT_IMPLEMENTED=NO
NOT_NOW_CONSUMES_TRIAL=NO
ONBOARDING_ROLLOUT_ENABLED=NO
PLAYER_AVATAR_IMPLEMENTED=NO
LEGACY_PLAYER_ONBOARDING_STATE_CHANGED=NO
PLAYER_ID_FROM_AUTH_CONTEXT=YES
PLAYER_DATA_IN_COACH_CATALOG_RESPONSE=0
COACH_COLLECTIONS_ADDED=coachCatalogs;systemConfig/coachCatalog_POINTER
COACH_INDEXES_ADDED=0
NEW_BE04_TESTS=31_PASS
FULL_SUITE=765_PASS_0_FAIL_24_SKIPPED
COMPILER_ERRORS=0
PREEXISTING_PROTECTED_FILES_MODIFIED=0/102
SECRET_SCAN=PASS
FILES_IN_SCOPE=12
COMMIT=NONE
PUSH=NONE
DEPLOY=NO
ONB_BE_04_SUCCESS=YES
ONB_BE_05_STARTED=NO
NEXT=ONB-BE-04 IMPLEMENTATION REVIEW
```

## Authorized final checkpoint review

No production/test source changed in this review. Retained evidence: NEW_BE04_TESTS=31_PASS; FULL_SUITE=765_PASS_0_FAIL_24_SKIPPED; SUITE_REPEATED=NO. Earlier COMMIT/PUSH=NONE fields record the implementation-stage result; the subsequent authorized checkpoint SHA is reported separately.

Prepared v2 lives only in `server/domino/src/main/kotlin/com/teamfho/domino/catalog/CoachCatalogRepository.kt`, in `CoachCatalogSeed.compatibleOnboarding()` and its explicit publication helper. Model: OnboardingCatalogPublication. Differences from canonical v1 are exactly catalogVersion=2, coachCatalogVersion=1, publishedAt=2026-09-30T00:00:00Z. No standalone v2 JSON resource, startup publication or migration. The v1 resource matches its base Git blob. Onboarding version and Coach version are coupled by explicit reference, never numeric equality.

Inventory: 118 pending paths classified as 102 PROTECTED, 4 UNRELATED prior design reports and 12 BE-04 candidates. Candidate classification: CoachCatalog.kt = COACH_DOMAIN; CoachCatalogHttp.kt = COACH_API; CoachCatalogRepository.kt = COACH_PERSISTENCE/COACH_SEED/ONBOARDING_V2_PREPARATION; coach-catalog-v1.json = COACH_TRANSLATION/COACH_SEED; the three OnboardingProgress files = ONBOARDING_INTEGRATION; four new test files = TEST; this report = VALIDATION. UNCLASSIFIED_FILES=0; UNRELATED_FILES_FOR_BE04=0. Only these 12 candidates are eligible for staging.

Final contract review: GET is read-only; explicit DTO has no player data. Selection has one domain owner, reuses authenticated identity, revisions, receipts and pinning. New inactive selections are rejected; existing inactive key/version may be retained and presented from explicit historical version without substitution. Completion without Experience, without Coach or with an invalid Coach is rejected. Contacts, Membership, purchase and trial are not required. No seed/publication/live Auth/Unity retest or rollout occurred.

Current protected hash comparison remains 0/102. Content review and secret/identity scan found no real emails, raw user IDs, tokens, private credentials or mojibake. Fixtures are synthetic. Commit scope excludes all protected/unrelated/BE-05 files. Final remote SHA and post-commit status are recorded in the checkpoint response.
