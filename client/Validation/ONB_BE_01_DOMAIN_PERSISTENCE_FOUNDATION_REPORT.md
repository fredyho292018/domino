# ONB-BE-01 — Domain and persistence foundation

Base: `bbc22664f3344c0e180cdeb778286a6b00c59d1c`. Date: 2026-09-30.

**Final technical gate: PASS.** The preexisting lifecycle blocker was resolved separately. Current retained evidence: 688 passed, zero failed, 24 skipped. Earlier sections retain the diagnostic history; the final review below supersedes their pending status. No live migration or deployment.

## Exact changed files

Under `server/domino/src/main/kotlin/com/teamfho/domino/`:

1. `player/Player.kt` — optional private firstName, lastName, countryCode; profileRevision with legacy default zero and validation.
2. `player/PlayerProfileRules.kt` (new) — NFC private names, trim, grapheme limit80, control/bidi-control rejection; explicit ISO country; supported normalized locale; IANA region/UTC timezone validation.
3. `player/OnboardingFoundation.kt` (new) — preferences/notification-choice structure, DominoProfile, stable experience enum, onboarding structural invariants and start transition, explicit rollout boundary and legacy classification.
4. `player/FirestoreFoundationMapping.kt` — backward-compatible reading of absent optional fields/revision; new-document encoding preserves optional values and timestamps.
5. `player/FirestoreOnboardingFoundation.kt` (new) — Firestore timestamp codec; create-only transactional initialization/backfill for an explicitly selected authenticated identity and boundary; validates existing documents without overwriting them.
6. `player/FirestorePlayerFoundationRepository.kt` — optional boundary-controlled initialization in the existing bootstrap transaction, all reads before writes; display-name changes increment profileRevision and retain public projection behavior.
7. `player/PlayerFoundationConfiguration.kt` — optional `domino.onboarding.created-at-cutoff` wiring. Blank default disables initialization. No runtime configuration file/value was changed.
8. `security/FirebaseIdentity.kt` — request-scoped isEmailVerified, signInProvider, hasPasswordProvider, with safe compatibility defaults.
9. `security/FirebaseAdminTokenVerifier.kt` — facts obtained from already-fetched Firebase UserRecord and verified token claims; no new lookup, no persistence, no new authorization gate in BE-01.

Under `server/domino/src/test/kotlin/com/teamfho/domino/`:

10. `player/OnboardingFoundationTests.kt` (new) — 8 tests for defaults, legacy exemption, state guards, profile/locale/timezone/experience validation, Firestore serialization and DTO boundary.
11. `player/OnboardingPersistenceTests.kt` (new) — 4 tests for create-only/repeated initialization, existing preferences, bootstrap atomic initialization and retry with existing root preservation.
12. `player/PlayerDisplayNameTests.kt` — explicitly checks the approved revision increment while retaining other-field preservation and no-op checks.
13. `security/FirebaseAdminTokenVerifierTests.kt` — current user verification and verified active-provider facts, separate from linked password-provider presence.

This report is the only additional artifact created by BE-01. Earlier ONB design evidence remains unchanged. No Unity production files changed.

## Implemented behavior and activation boundary

Player retains its existing displayName/createdAt and identity. New private values default null, not manufactured names/country. Email, verification status, phone and membership are not Player fields. Experience remains null until selected and is not rating. Avatar/DOB/ruleset/game-mode/target-score persistence remains excluded.

Preferences own normalized es/en locale, nullable timezone and typed sparse notification choices. Initialization copies existing Player.language and never infers country. No preferences mutation endpoint or locale mirror update path is introduced here; these belong to the later API phase. Notification structure does not implement delivery or preference-policy enforcement APIs.

The foundation models NOT_STARTED, IN_PROGRESS and COMPLETED, including pinned version/cursor/substep, completed/skipped keys, timestamps, revision and completion origin. Constructors reject contradictory structural state; start cannot restart in-progress state. There is no public completion mutator or endpoint. Catalog-aware completeness and required-answer validation remain in the later catalog/onboarding phase; this foundation does not pretend to validate nonexistent coach/catalog references.

When an explicit boundary is supplied, existing persisted createdAt before the cutoff yields COMPLETED/LEGACY_EXEMPT with migration time in completedAt and no fabricated startedAt/answers. New creation at/after boundary yields NOT_STARTED. Existing subdocuments are retained. Repeated initialization/backfill creates only missing documents. Player root creation time and alias are not regenerated. No scans, scheduler, startup-wide migration or caller-selectable UID endpoint were added.

The initializer accepts FirebaseIdentity in the repository layer, like existing foundation operations; no mobile API accepts a Player identifier. There is no operational backfill CLI or cohort selection in this phase. Approved cohort/cutoff and real migration execution are still required before enabling the configuration. The default boundary is absent, so current runtime initialization/routing remains unchanged.

## Firestore structure

- `players/{uid}`: existing fields plus optional firstName/lastName/countryCode and profileRevision. Existing documents with absent new fields remain readable.
- `players/{uid}/preferences/current`: preferredLocale,timeZone,notificationPreferences,revision,updatedAt.
- `players/{uid}/dominoProfile/current`: experienceLevel,preferredCoachKey,selectedCoachCatalogVersion,revision,updatedAt.
- `players/{uid}/onboarding/current`: approved progress fields, revision and completionOrigin.

All initial subdocument writes join the existing Player/wallet transaction when enabled. Explicit per-account initialization is transactional and create-only. Firestore timestamps used for state timestamps. No speculative collections/indexes/catalogs/receipts are added: save-step/activation APIs are not yet implemented. No live Firestore calls were used during validation.

## Validation evidence and limits

Commands ran the root Gradle `:test` task, whose configured tests exclude REAL_FIRESTORE and EMULATOR and point any accidentally created Firestore client at a disabled loopback endpoint. No load tests.

1. Initial filtered invocation failed because the multi-project test selection included an unrelated subproject and a shell-expanded filter; not a product test result.
2. First full run found an old display-name test expecting only name/timestamp changes. Test updated to require the approved profileRevision increment; product behavior was not weakened.
3. Next full run failed initializing PlayerControllerTests: `socialInvalidationRuntime` attempted to schedule on an already terminated executor (`RejectedExecutionException`).
4. Repeated full run failed with the same lifecycle category during PlayerDisplayNameTests initialization. No Social source modified. This suggests shared-context lifecycle interference, but this task does not claim a proved preexisting root cause.
5. PlayerDisplayNameTests run alone: BUILD SUCCESSFUL, 39 tests, zero failures/skips. The isolated pass does not erase the broad-suite failure.

Latest full-run XML aggregate captured before isolated rerun: 670 reported entries, 645 passed, 1 failure, 24 skipped. Gradle console counted 669 completed/23 skipped; XML includes an additional suite-initialization/report entry. Counts below use the explicit XML aggregate and include the initialization failure. Do not add isolated rerun counts to claim unique coverage.

Selected full-run groups:

| Group | Reported entries | Failures | Skipped |
|---|---:|---:|---:|
| Entitlements | 17 | 0 | 0 |
| Matchmaking | 22 | 0 | 11 |
| History/Replay | 13 | 0 | 0 |
| Social | 120 | 0 | 9 |

The 12 new foundation tests passed. FirebaseAdminTokenVerifierTests passed 22 cases. Current compilation passed; existing warnings include deprecated test API usage, JVM class-sharing notice and Redis shutdown warnings. Firestore contention/emulator tests were not run; transaction tests use existing SDK transaction doubles and do not prove real emulator contention behavior.

AUTH-01/AUTH-02 live Unity Guest/register/verification/login/reset/logout journeys were not re-executed. Client and endpoint response code are unchanged; existing backend security/bootstrap regressions provide partial evidence, not a fresh live Auth PASS. Historical manual approvals remain historical. The broad-suite startup failure prevents declaring the requested overall regression gate complete.

## Compatibility and security

PlayerResponse and PlayerBootstrapResponse remain unchanged; the test asserts their existing public field boundary. Existing self profile/social response construction was not extended to emit private values. No new endpoint exists. EntitlementService.bootstrap, EntitlementRepository.trial and FirestoreEntitlements.trial are unchanged; automatic trial behavior remains the current conditional behavior. No activation endpoint, catalogs, FCM or routing changes.

Protected inventory SHA-256 comparison: 102 checked, zero modified by this work. Source/report scan excludes unrelated protected files; report contains no real identifiers, email addresses, tokens or credentials. Fixture identifiers in unit tests are synthetic and are not copied into this report.

## Final status

```text
PLAYER_PROFILE_FIELDS_IMPLEMENTED=firstName,lastName,countryCode,profileRevision;existing displayName/createdAt preserved
EMAIL_VERIFIED_AUTH_CONTEXT=IMPLEMENTED_REQUEST_SCOPED
EMAIL_VERIFIED_PERSISTED_IN_PLAYER=NO
PLAYER_PREFERENCES_IMPLEMENTED=DOMAIN_AND_INITIALIZATION
DOMINO_PROFILE_IMPLEMENTED=DOMAIN_AND_INITIALIZATION
ONBOARDING_STATE_MODEL_IMPLEMENTED=YES_STRUCTURAL_FOUNDATION
EXISTING_PLAYER_MIGRATION=CREATE_ONLY_BOUNDARY_CONTROLLED_CODE;NOT_EXECUTED_ON_REAL_DATA
EXISTING_PLAYERS_FORCED_INTO_ONBOARDING=NO
NEW_PLAYER_INITIAL_ONBOARDING_STATE=NOT_STARTED_WHEN_BOUNDARY_ENABLED
DISPLAY_NAME_PRESERVED=YES
CREATED_AT_PRESERVED=YES
FIRESTORE_STRUCTURE_IMPLEMENTED=PLAYER_PLUS_THREE_CURRENT_SUBDOCUMENTS
INDEXES_ADDED=0
BOOTSTRAP_RESPONSE_COMPATIBLE=YES_UNCHANGED_DTO
BOOTSTRAP_AUTO_GRANTS_TRIAL_BEHAVIOR_CHANGED=NO
ENTITLEMENT_REGRESSION=PASS_17_LOCAL_TESTS
MEMBERSHIP_CATALOG_IMPLEMENTED=NO
COACH_CATALOG_IMPLEMENTED=NO
ONBOARDING_CATALOG_IMPLEMENTED=NO
TRIAL_ACTIVATION_ENDPOINT_IMPLEMENTED=NO
PUSH_DELIVERY_IMPLEMENTED=NO
AUTH_01_REGRESSION=PARTIAL_BACKEND_EVIDENCE_NO_NEW_LIVE_CLIENT_RUN
AUTH_02_REGRESSION=PARTIAL_BACKEND_EVIDENCE_NO_NEW_LIVE_CLIENT_RUN
PLAYER_BOOTSTRAP_REGRESSION=PARTIAL_BROAD_CONTEXT_GATE_FAILED
MATCHMAKING_REGRESSION=PARTIAL_11_SKIPPED
HISTORY_REPLAY_REGRESSION=PASS_13_LOCAL_TESTS
SOCIAL_REGRESSION=PARTIAL_9_SKIPPED_AND_CONTEXT_STARTUP_FAILURE
TESTS_RUN=670_FULL_RUN_XML_ENTRIES
TESTS_PASS=645
TESTS_FAIL=1
TESTS_SKIPPED=24
COMPILER_ERRORS=0
PLAYER_ID_FROM_AUTH_CONTEXT=YES
PREEXISTING_PROTECTED_FILES_MODIFIED=0/102
SECRET_SCAN=PASS_SCOPED_PATTERN_SCAN_AND_REVIEW
COMMIT=NONE
PUSH=NONE
DEPLOY=NO
ONB_BE_01_SUCCESS=NO_VALIDATION_GATE_INCOMPLETE
NEXT=ONB-BE-01_IMPLEMENTATION_REVIEW_AND_TEST_LIFECYCLE_DIAGNOSIS
```

## Follow-up: implementation review and lifecycle diagnosis (2026-09-30)

This section supersedes the earlier uncertainty about the startup failure. Diagnosis only: no application/test source or configuration was changed during this follow-up. No fix has been applied. Earlier full-run failure evidence is retained above, not converted into a passing result.

### Changed-file scope review

All 13 source/test files listed above were reviewed, including new files not present in git diff. Classification:

| Files (same relative prefixes as inventory above) | Classification |
|---|---|
| Player.kt, PlayerProfileRules.kt | PLAYER_PROFILE; shared validation for PLAYER_PREFERENCES |
| OnboardingFoundation.kt | PLAYER_PREFERENCES, DOMINO_PROFILE, ONBOARDING_STATE, MIGRATION |
| FirestoreFoundationMapping.kt | FIRESTORE, PLAYER_PROFILE |
| FirestoreOnboardingFoundation.kt | FIRESTORE, MIGRATION |
| FirestorePlayerFoundationRepository.kt | FIRESTORE, MIGRATION, PLAYER_PROFILE |
| PlayerFoundationConfiguration.kt | CONFIG wiring only, default disabled |
| FirebaseIdentity.kt, FirebaseAdminTokenVerifier.kt | AUTH_CONTEXT |
| Four test files in inventory | TEST |
| This report | VALIDATION |

ONB_BE_01_FILES_CHANGED=13 source/test files + 1 report = 14. UNEXPECTED_FILES_CHANGED=0 within BE-01 scope. Protected preexisting edits and prior design reports are not BE-01 changes. Temporary Gradle output and the isolated baseline live under ignored build/, not production source.

Approved private fields, nullable country/experience, locale/timezone and notification structure are present. Firebase verification is request-scoped only. No email/phone/membership duplication. Existing alias and creation timestamp are preserved. Current response DTOs/public Social projections remain unchanged. No membership/free fallback was added. Coach catalog, question catalog, final completion validation/API and post-onboarding mutation APIs remain deferred, as required by BE-01 scope. Structural state validation must not be mistaken for the later catalog-aware completion gate.

Rollout mechanism is precisely the optional `domino.onboarding.created-at-cutoff` property. Blank default becomes null boundary and skips initialization in the existing repository. Nonblank parses an explicit Instant. No value was configured here. With boundary enabled, createdAt before cutoff initializes COMPLETED/LEGACY_EXEMPT; at/after cutoff initializes NOT_STARTED. Existing subdocuments are validated and preserved; no production migration run occurred. This is boundary-based lazy/per-account initialization, not an implemented cohort-management tool. Any exception cohort still needs later approved operational selection. Routing remains unchanged.

The runtime trial path remains byte-for-byte unchanged in controller/Entitlements sources: POST bootstrap → EntitlementService.bootstrap → conditional repository.trial. No trial activation route exists. The only default-active backend changes are private model compatibility, profileRevision handling and additional authenticated-context facts; onboarding persistence is gated.

### Exact reproduction and baseline

Authoritative baseline was exported with git archive from the exact base SHA, limited to server/domino, into `server/domino/build/onb-diagnosis/baseline/server/domino`. This is an equivalent isolated baseline checkout; the working source was never reset/stashed/replaced. Gradle compiled baseline sources separately. No temporary commit or push was used.

| Execution, performed in this follow-up | Current BE-01 | BASE_SHA |
|---|---|---|
| PlayerControllerTests alone, runtime started by Spring | PASS, 33 tests | PASS, 33 tests |
| PlayerControllerTests + PlayerDisplayNameTests without intervening context | PASS | PASS |
| Controlled three-class order described below | FAIL, same runtime restart | FAIL, same runtime restart |

Minimal reproduced sequence is **PlayerDisplayNameTests (context A) → FirebaseSecurityTests (context B) → PlayerControllerTests (cached context A)**. Current and baseline each produced 39 passing display-name cases, 23 passing security cases, then one PlayerControllerTests initialization failure before any of its 33 test bodies ran. Three classes are needed to leave and return to the same context; the two-class same-context control passes.

Reproduction uses root Gradle `:test`, selecting these exact three classes, with process-scoped JAVA_TOOL_OPTIONS:

```text
-Djunit.jupiter.testclass.order.default=org.junit.jupiter.api.ClassOrderer$Random
-Djunit.jupiter.execution.order.random.seed=1
```

Observed XML timestamps confirm the actual order; it is not inferred from argument order. The previous environment value was restored by finally; no persistent environment or source configuration override was introduced. One PowerShell wrapper returned shell exit0 despite Gradle's failure; classification uses Gradle BUILD FAILED and XML failure/stack, not that wrapper exit. Baseline wrapper explicitly propagated Gradle exit1.

Evidence snapshots retained locally under ignored build/onb-diagnosis/current-sequence and base-sequence (three TEST XML files each); isolated final results remain in each build/test-results/test. These generated logs are not proposed checkpoint files.

### Failure record (sanitized)

```text
FAILURE_TEST=PlayerControllerTests.initializationError
FAILURE_PHASE=SpringExtension.beforeAll / TestContextManager.beforeTestClass / cached context restart
FAILURE_EXCEPTION_TYPE=ApplicationContextException caused by RejectedExecutionException
FAILURE_MESSAGE_SANITIZED=Failed to start bean socialInvalidationRuntime; scheduled task rejected by terminated ScheduledThreadPoolExecutor
FAILURE_FIRST_PROJECT_FRAME=SocialInvalidationRuntime.start(SocialInvalidationRuntime.kt:83)
FAILURE_BEFORE_TEST_BODY=YES
FAILURE_DURING_TEST_BODY=NO
FAILURE_DURING_TEARDOWN=NO (earlier pause performs the destructive stop)
```

Current and baseline causal stack: DefaultContextCache.get → restartContextIfNecessary → AbstractApplicationContext.restart → DefaultLifecycleProcessor.onRestart/startBeans → SocialInvalidationRuntime.start → scheduler.scheduleWithFixedDelay → RejectedExecutionException. No BE-01 Player persistence/Auth-context frame participates in this failure path.

### Dependency/lifecycle chain

SpringBootTest + test profile + MockMvc imports FakeAuthConfiguration/FakePlayerFoundationConfiguration. These two Player classes share the same cached context. FirebaseSecurityTests imports an additional ProbeController and uses a different context. HTTP is MockMvc, no embedded listening HTTP port is required by these tests.

SocialInvalidationConfiguration always registers SmartLifecycle SocialInvalidationRuntime (autoStartup=true), with ObjectProvider references to Firestore, RedisConnectionFactory, StringRedisTemplate and MeterRegistry. Runtime owns one scheduled executor (social-invalidation), a two-thread worker pool (social-authorization), index, optional Firestore feed/listener, and optional Redis Pub/Sub bus. start schedules tick. stop delegates to close; close calls scheduler.shutdownNow and worker.shutdownNow. Those final executors are never recreated.

The installed Spring test cache source documents default `spring.test.context.cache.pause=on_context_switch`. DefaultContextCache pauses unused context A when switching to B, then restarts A on reuse. That legitimate pause calls the runtime stop/close, irreversibly shutting its scheduler. Reuse tries to schedule on that same terminated instance. The cached Spring context is intentional shared state, not a leaked external server.

Firestore is disabled in test profile (`firebase.enabled=false`); root test task excludes REAL_FIRESTORE/EMULATOR and supplies loopback FIRESTORE_EMULATOR_HOST=127.0.0.1:1 plus a demo project. Runtime's database supplier can return null and tick exits before installing its Firestore listener. No emulator/container was started for this diagnosis.

Redis auto-configuration is still present and other application lifecycle components can attempt loopback 6379, producing connection/shutdown warnings. SocialInvalidationPubSub would own bounded delivery/subscription executors and a Redis listener if instantiated. However the reproduced fatal call fails immediately on the local terminated scheduler before dependency recovery work. It is not a bind/address-in-use error, a missing Firestore listener, or exhaustion caused by a prior open port. No remote server/config changes, container restarts or filesystem cleanup were used.

PREVIOUS_TEST_RESOURCE_LEAK=NO_EVIDENCE_OF_EXTERNAL_LEAK; observed mechanism is premature shutdown of a resource inside an intentionally cached context. Context pause/return is the order-sensitive trigger. This does not certify every unrelated background worker leak-free; none is needed to explain or reproduce this failure.

### Classification and stop

Primary classification (one): **PREEXISTING_FAILURE**.

Evidence: exact base SHA, before all BE-01 edits, reproduces the same minimal sequence, same exception type, same SocialInvalidationRuntime.start line and terminated scheduler. Isolated and no-intervening-context controls pass on both trees. Mechanism is a non-restartable SmartLifecycle resource exposed by Spring test-context pause/restart. This also merits lifecycle review beyond tests; simply disabling tests would not demonstrate runtime restart correctness. No fix choice is implemented or approved here.

```text
ONB_BE_01_FILES_CHANGED=14_INCLUDING_REPORT
UNEXPECTED_FILES_CHANGED=0
PLAYER_PROFILE_IMPLEMENTATION=FOUNDATION_SCOPE_CONFIRMED
PLAYER_PREFERENCES_IMPLEMENTATION=FOUNDATION_SCOPE_CONFIRMED
DOMINO_PROFILE_IMPLEMENTATION=FOUNDATION_SCOPE_CONFIRMED
ONBOARDING_STATE_IMPLEMENTATION=STRUCTURAL_FOUNDATION_CONFIRMED
EMAIL_VERIFIED_AUTH_CONTEXT=REQUEST_SCOPED_CONFIRMED
EMAIL_VERIFIED_PERSISTED_IN_PLAYER=NO
DEFAULT_COUNTRY_ASSIGNED=NO
DEFAULT_EXPERIENCE_LEVEL_ASSIGNED=NO
MISSING_ENTITLEMENT_ASSUMED_FREE=NO
ONBOARDING_ROLLOUT_FLAG=domino.onboarding.created-at-cutoff
ONBOARDING_ROLLOUT_DEFAULT=DISABLED
EXISTING_PLAYER_INITIAL_STATE=COMPLETED_LEGACY_EXEMPT_BEFORE_CONFIGURED_CUTOFF
EXISTING_PLAYERS_FORCED_INTO_ONBOARDING=NO
NEW_PLAYER_INITIAL_ONBOARDING_STATE=NOT_STARTED_WHEN_INITIALIZATION_ENABLED
BOOTSTRAP_AUTO_GRANTS_TRIAL_BEHAVIOR_CHANGED=NO
TRIAL_ACTIVATION_ENDPOINT_IMPLEMENTED=NO
SOCIAL_INVALIDATION_ISOLATED=PASS_VIA_33_PLAYER_CONTROLLER_TESTS
FAILURE_ORDER_DEPENDENT=YES_A_B_A_CONTEXT_SEQUENCE
PREVIOUS_TEST_RESOURCE_LEAK=NO_EXTERNAL_LEAK_IDENTIFIED
BASE_SHA_SOCIAL_INVALIDATION_ISOLATED=PASS_33_TESTS
BASE_SHA_REPRODUCES_FAILURE=YES_SAME_MINIMAL_SEQUENCE
FAILURE_CLASSIFICATION=PREEXISTING_FAILURE
CLASSIFICATION_EVIDENCE=IDENTICAL_BASELINE_AND_CURRENT_RESTART_FAILURE
SOURCE_CHANGED_DURING_DIAGNOSIS=NO
COMPILER_ERRORS=0
PREEXISTING_PROTECTED_FILES_MODIFIED=0/102
SECRET_SCAN=PASS_SANITIZED_REPORT
COMMIT=NONE
PUSH=NONE
DEPLOY=NO
ONB_BE_01_SUCCESS=NO_PENDING_FAILURE_RESOLUTION
NEXT=ONB-BE-01_FAILURE_RESOLUTION_REVIEW
```

## PREEXISTING_LIFECYCLE_FIX — completed 2026-09-30

This section supersedes the pending lifecycle blocker above; it does not change the ONB-BE-01 domain implementation.

Ownership: SocialInvalidationRuntime implements SmartLifecycle and AutoCloseable. It owns the scheduler, periodic future and authorization worker; Spring may stop and restart the same instance when switching cached test contexts. Previously close/stop permanently shut down constructor-owned executors, while start reused them. Both executors now support fresh ownership after termination. The authorization index keeps its identity through a delegating executor.

Start/stop are serialized. Double start returns without another registration. Stop cancels the future, interrupts the scheduler, waits for the active tick boundary, removes listener/bus/feed state, fails authorization closed, shuts down the worker and waits for termination. A generation guard rejects stale scheduled callbacks. Restart refuses overlap if an old executor has not terminated. Invalidation logic and its timing constants are unchanged; no test-only production branch was added.

Files changed for this fix:
- server/domino/src/main/kotlin/com/teamfho/domino/social/SocialInvalidationRuntime.kt
- server/domino/src/test/kotlin/com/teamfho/domino/social/SocialInvalidationLifecycleTests.kt
- this validation report

Current evidence:
- Focused lifecycle + PlayerController execution passed: 4 + 33 tests.
- Exact seeded context sequence passed: PlayerDisplayName 39, FirebaseSecurity 23, PlayerController 33 (95 total). XML timestamps confirm that order. Evidence: server/domino/build/onb-diagnosis/fixed-sequence.
- Full root backend :test XML: 712 cases, 688 passed, 0 failures, 0 errors, 24 skipped. Results timestamp 2026-09-30 17:15:27 local. The lifecycle startup failure is resolved.
- Lifecycle tests exercise four restart cycles, repeated start/stop, concurrent lifecycle calls, an interrupted in-flight callback, and stop before start. Captured scheduler/worker instances terminate and futures cancel.
- Entitlement regression: 17 passed. History/Replay: 13 passed. ONB foundation/persistence: 12 passed. Firebase verifier/security: 45 passed.
- Redis-dependent skipped tests are not claimed as live integration validation. AUTH-01/02 coverage here is backend regression only; no new Unity or live Firebase Auth test was performed. Existing manual evidence is retained.
- Protected inventory SHA-256 comparison: 102 checked, 0 changed.

```text
CLASSIFICATION=PREEXISTING_LIFECYCLE_FIX
ROOT_CAUSE_CONFIRMED=YES
LIFECYCLE_BEFORE=STOP_DESTROYS_EXECUTORS_START_REUSES_TERMINATED_SCHEDULER
LIFECYCLE_AFTER=RESTART_CREATES_FRESH_OWNED_EXECUTORS
SCHEDULER_RECREATED_AFTER_STOP=YES
DOUBLE_START_DUPLICATE_TASKS=0
DOUBLE_STOP_SAFE=YES
RESTART_SAFE=YES
MULTI_CYCLE_RESTART=PASS
SOCIAL_INVALIDATION_FUNCTIONAL_BEHAVIOR_CHANGED=NO
SOCIAL_SCHEDULER_RESOURCE_LEAK=NO_IN_TESTED_LIFECYCLES
SOCIAL_INVALIDATION_ISOLATED=PASS_33_PLAYER_CONTROLLER_PLUS_4_LIFECYCLE
MINIMAL_SEQUENCE=PASS_95
FULL_SUITE_SOCIAL_INVALIDATION_STARTUP=PASS
FULL_SUITE_CASES=712
FULL_SUITE_PASS=688
FULL_SUITE_FAIL=0
FULL_SUITE_SKIPPED=24
AUTH_01_REGRESSION=PASS_BACKEND_LOCAL_ONLY
AUTH_02_REGRESSION=PASS_BACKEND_LOCAL_ONLY
ENTITLEMENT_REGRESSION=PASS
SOCIAL_REGRESSION=PASS_EXECUTED_TESTS_REDIS_TESTS_SKIPPED
PREEXISTING_FIX_FILES_CHANGED=2_SOURCE_TEST_FILES_PLUS_REPORT
ONB_BE_01_DOMAIN_FILES_CHANGED_BY_FIX=0
COMPILER_ERRORS=0
PREEXISTING_PROTECTED_FILES_MODIFIED=0/102
SECRET_SCAN=PASS_SCOPED_SOURCE_TEST_REPORT
COMMIT=NONE
PUSH=NONE
DEPLOY=NO
NEXT=ONB-BE-01 FINAL GATE
```


## Final contract review and checkpoint boundaries

Source unchanged since the passing suite; full suite not repeated. This checkpoint implements only the approved foundation subset of ONB-00, not its future APIs or catalogs.

PLAYER_PROFILE_FIELDS_IMPLEMENTED=YES (optional firstName/lastName/countryCode, profileRevision)
PLAYER_PREFERENCES_IMPLEMENTED=YES_FOUNDATION
DOMINO_PROFILE_IMPLEMENTED=YES_FOUNDATION
ONBOARDING_STATE_MODEL_IMPLEMENTED=YES_FOUNDATION
EMAIL_VERIFIED_AUTH_CONTEXT=FIREBASE_CURRENT_USER_RECORD
EMAIL_VERIFIED_PERSISTED_IN_PLAYER=NO
EXISTING_PLAYER_MIGRATION=CREATE_ONLY_TRANSACTIONAL_INITIALIZER_NOT_EXECUTED_LIVE
EXISTING_PLAYERS_FORCED_INTO_ONBOARDING=NO
DISPLAY_NAME_PRESERVED=YES
CREATED_AT_PRESERVED=YES
IDENTITY_AND_ENTITLEMENTS_PRESERVED=YES
NEW_PLAYER_INITIAL_ONBOARDING_STATE=NOT_STARTED_WHEN_INITIALIZATION_ENABLED
NEW_PLAYER_ROUTING_CHANGED=NO
ONBOARDING_ROLLOUT_DEFAULT=DISABLED
ROLLOUT_MECHANISM=OPTIONAL_domino.onboarding.created-at-cutoff_BLANK_BY_DEFAULT
DEFAULT_COUNTRY_ASSIGNED=NO
DEFAULT_FIRST_NAME_FROM_DISPLAY_NAME=NO
DEFAULT_LAST_NAME_FROM_DISPLAY_NAME=NO
DEFAULT_EXPERIENCE_LEVEL_ASSIGNED=NO
MISSING_ENTITLEMENT_ASSUMED_FREE=NO
FIRESTORE_STRUCTURE_MATCHES_CONTRACT=YES_FOUNDATION_SUBSET
NEW_COLLECTIONS=players/{uid}/preferences;players/{uid}/dominoProfile;players/{uid}/onboarding
EXTENDED_DOCUMENTS=players/{uid}
INDEXES_ADDED=0
PLAYER_BOOTSTRAP_RESPONSE_COMPATIBLE=YES_EXISTING_DTO_UNCHANGED
AUTH_02_CLIENT_COMPATIBLE=YES_UNCHANGED_RESPONSE_AND_ROUTING
BOOTSTRAP_AUTO_GRANTS_TRIAL_BEHAVIOR_CHANGED=NO
TRIAL_ACTIVATION_ENDPOINT_IMPLEMENTED=NO
MEMBERSHIP_CATALOG_IMPLEMENTED=NO
COACH_CATALOG_IMPLEMENTED=NO
ONBOARDING_CATALOG_IMPLEMENTED=NO
PUSH_DELIVERY_IMPLEMENTED=NO
AUTH_BACKEND_REGRESSION=PASS
REAL_FIREBASE_AUTH_RETESTED=NO
UNITY_AUTH_RETESTED=NO
ONB_BE_01_DOMAIN_FILES_CHANGED_BY_LIFECYCLE_FIX=0
PREEXISTING_PROTECTED_FILES_MODIFIED=0/102
UNCLASSIFIED_FILES=0

The 24 skipped XML cases are: PartnersMatchmaking 3; RedisMatchmaking 8; AllAbandonedRedis 1; PartnersOnline 1; RedisTurnIndex 1; RedisPresence 1; FollowRedisRate 1; SocialInvalidationRedis 3; SocialPresenceRedis 3; SocialRateGateRedis 2. Their environment-gated external Redis/parity scenarios were not enabled. They are not PASS. REAL_FIRESTORE/EMULATOR tags excluded by task configuration are also not live validation.

Commit 1 contains only SocialInvalidationRuntime.kt and SocialInvalidationLifecycleTests.kt, with message `fix: make social invalidation runtime restart-safe`.
Commit 2 contains the 13 ONB files enumerated above and this report, with message `feat: add onboarding domain persistence foundation`. The report describes both test evidence and the prerequisite but contains no lifecycle source change.

All 122 pending files were classified: 102 protected preexisting files; 2 lifecycle files; 9 ONB production files; 4 ONB tests; this report; 4 unrelated prior design/audit documents. Unrelated documents retained unstaged: FUNCTIONAL_00_AUTH_PLAYER_BACKEND_AUDIT.md, ONB_00A_BACKEND_DOMAIN_API_DESIGN.txt, ONB_00_BACKEND_CONTRACT_FINAL_REVIEW.md, ONB_00_ONBOARDING_PLAYER_COACH_MEMBERSHIP_DESIGN.txt (all under client/Validation). Protected paths and hashes remain in APP_SHELL_01_PREWORK_REVIEW_INVENTORY.csv. No unrelated or protected file belongs in either commit.

RAW_UIDS_IN_REPORT=0
REAL_EMAILS_IN_REPORT=0
TOKENS_IN_REPORT=0
SECRETS_IN_REPORT=0
SECRET_SCAN=PASS
DEPLOY=NO
ONBOARDING_ROLLOUT_ENABLED=NO
ONB_BE_01_TECHNICAL_GATE=PASS
NEXT=ONB-BE-02 AUTHORIZATION
