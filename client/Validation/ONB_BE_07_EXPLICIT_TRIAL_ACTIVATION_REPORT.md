# ONB-BE-07 Explicit promotional trial activation

Base: a34702415740b3e322a08fec884f7189497911a7. Implementation and local tests only. No deploy, real Firebase authentication, Unity operation, real Firestore mutation, catalog publication or commercial policy activation.

## Old and new entrypoints

Previously PlayerController bootstrap called EntitlementService.bootstrap, which could call EntitlementRepository.trial / FirestoreEntitlements.trial and grant automatically. That production repository method is removed. Bootstrap now resolves current Entitlements only, with trialGranted=false. Existing resolver semantics, ADMIN_GRANT handling, grant dates and historical documents are unchanged.

The single normal promotional grant entrypoint is POST /api/v1/player/trial/activate -> EntitlementService.activateTrial -> TrialActivationService.activate -> buffered Firestore transaction. Product remains PREMIUM_LEGACY (persisted Plan.PREMIUM), never a commercial tier. Membership catalog and onboarding services have no activation calls.

## DTO and authentication

TrialActivationRequest: operationId (canonical UUID string), expectedPolicyVersion (positive Long). Strict JSON object with exactly these two typed fields; 16 KiB body limit. Unknown fields, numeric/string coercions and caller-supplied identity/dates/duration/plan are rejected with REQUEST_INVALID400.

TrialActivationResponse: operationId, outcome ACTIVATED|ALREADY_ACTIVE, entitlements: existing EntitlementSummary. Newly granted response trialGranted=true; already-active response false. Authenticated principal is the sole Player identity. Existing OnboardingWriteAuthorization reused: anonymous allowed unless linked-only policy, password requires verified email, supported federated/phone accepted; unsupported context rejected. ACTIVE Player and developmentTestAccounts marker are checked transactionally.

Bootstrap retains player/wallet/entitlements fields and adds:
- capabilities: trialActivationMode=EXPLICIT, trialActivationContractVersion="1".
- trialEligibility: state NOT_STARTED/ACTIVE/CONVERTED/EXPIRED/INELIGIBLE/UNKNOWN, eligible, policyVersion, periodDays, activationMode.

Eligibility is a read-only transaction, not a persisted state machine. Inability to determine eligibility returns UNKNOWN/false. Snapshot Entitlements remain the current access authority. Additive DTO fields do not alter Player identity or wallet. No unrelated onboarding/profile projection implemented.

## Transaction and authority

Reuses FirestoreOnboardingProgressRepository solely as the generic buffered transaction adapter (five conflict attempts), without requiring an onboarding document or current step. Reads all inputs before writes: operation receipt, authoritative systemConfig/subscriptionPolicy (existing configuration fallback when document is absent), Player, development test marker, entitlement state, deterministic promotion marker, immutable grant and prior audit. No cached policy or entitlement state determines eligibility. No external store call inside transaction.

Successful new activation atomically writes five documents:
- players/{alias}/entitlementGrants/initial-premium-trial
- players/{alias}/promotions/initial-premium-trial
- players/{alias}/entitlementState/current
- players/{alias}/entitlementAudit/TRIAL_GRANTED
- players/{alias}/trialActivationReceipts/{operationId}

Server UTC clock is sampled inside each transaction attempt. Backend promotionalTrialDays determines duration (default7, existing policy range1..30). Existing grant codec/string dates preserved; new receipt/audit timestamps use native Firestore Timestamp. State revision increments once; retry/ALREADY_ACTIVE does not increment except authorized reconstruction of a missing projection from consistent historical evidence. Own-process entitlement cache invalidated after successful activation.

Historical grant/marker must agree (identifier, source and original start/end); inconsistent/partial evidence fails closed with DEPENDENCY_UNAVAILABLE503. trialConsumed=true without active trial always prevents regrant. Consistent active historical marker/grant with projection loss restores the same grant, not a new period; existing grant/marker/audit are not rewritten. Expired/revoked/consumed evidence never permits a new trial. Existing active trial remains resolvable even if new trial policy is disabled; new operations still validate current policy version.

## Idempotency

Receipt scoped to authenticated Player + operationId, operationType TRIAL_ACTIVATE, canonical SHA256 request hash, INITIAL_PREMIUM group, grantId, outcome, responseSnapshot/schemaVersion, createdAt/expiresAt. Retention30 days; expiration timestamp is TTL-ready, but no real TTL configuration deployed. Eligibility marker never expires.

Same ID/payload within retention returns exact original successful response before current policy validation (after authentication and current ACTIVE Player check). Different payload returns IDEMPOTENCY_CONFLICT409. Replay after elapsed trial can return the original snapshot: clients MUST refresh GET /api/v1/player/entitlements for current access; an activation receipt is not authority for current access. Expired receipts no longer guarantee original response, but permanent eligibility prevents regrant. Failures do not create success receipts. Firestore conflicts across instances serialize deterministic grant/marker creation; JVM locks are not production authority.

## Errors and coverage

- REQUEST_INVALID400
- EMAIL_VERIFICATION_REQUIRED403 / AUTH_CONTEXT_UNSUPPORTED403
- TRIAL_NOT_ELIGIBLE403
- TRIAL_DISABLED403 (latest BE07 request explicitly distinguishes disabled policy)
- IDEMPOTENCY_CONFLICT409
- TRIAL_POLICY_VERSION_MISMATCH409
- TRIAL_ALREADY_CONSUMED409
- TRIAL_NOT_APPLICABLE409
- DEPENDENCY_UNAVAILABLE503 / FIRESTORE_CONTENTION_EXHAUSTED503

Current active non-promotional grants (ADMIN_GRANT, GOOGLE_PLAY, APPLE_APP_STORE) are incompatible coverage. This honors available backend evidence only. STORE_RECONCILIATION_IMPLEMENTED=CURRENT_CAPABILITY_ONLY: no receipt verification, external store lookup or claim of complete cross-store trial-history reconciliation. Future integration must supply verified evidence and enforce approved non-stacking before rollout. Family metadata has no effect; no Family trial.

## Compatibility and deployment block

TrialClientCompatibilityGuard protects bootstrap and activation before controller execution. Default domino.trial.require-compatible-client=true requires X-Trial-Activation-Contract: 1. Missing/unsupported declaration returns409 CLIENT_UPDATE_REQUIRED without bootstrap/activation mutation. This declares supported contract, not a trusted grant authority or a hardcoded commercial build number. Disabling the guard never restores auto-grant; operational bypass would permit old clients to receive FREE without an activation UI and is not a safe production rollout.

General legacy HTTP tests disable compatibility signaling in application-test.yaml to isolate their original contracts; dedicated TrialActivationHttpTests explicitly enable the guard and test both paths. Production default remains required. No minimum released build invented: none validated yet. Deploy remains prohibited until compatible client, update requirement and coordinated cutover are authorized. Unity code is untouched.

## Scope preservation

No commercial enforcement, Family domain/invitations, store purchases, notification/reminder delivery, trial CTA, onboarding routing or rollout performed. Optional Not Now remains unchanged and is covered by existing onboarding regression. Membership reads remain side-effect-free under BE06 regression. No historical database migration.

## Validation and changed files

New tests cover explicit grant, server duration, policy mismatch, test account exclusion, auth context, consumed/revoked/expired evidence, inconsistent historical dates, lost response, conflicting operation, 30-day receipt, simultaneous service instances, SDK transaction retry/read-before-write, projection recovery, bootstrap compatibility, strict body validation, DTO privacy and cache invalidation. Existing entitlement tests now initialize historical trial fixtures explicitly rather than expecting bootstrap grants. Emulator tests adapted for the new entrypoint; no emulator execution is claimed unless separately recorded.

Implementation iterations corrected fixture compilation and test-policy wiring; first full suite found two old exact-bootstrap-DTO assertions, updated to validate additive fields while retaining exact original player/wallet checks. Final suite evidence is recorded below after completion.

Modified production:
- server/domino/src/main/kotlin/com/teamfho/domino/entitlement/EntitlementConfiguration.kt
- server/domino/src/main/kotlin/com/teamfho/domino/entitlement/EntitlementService.kt
- server/domino/src/main/kotlin/com/teamfho/domino/entitlement/FirestoreEntitlements.kt
- server/domino/src/main/kotlin/com/teamfho/domino/player/PlayerBootstrapResponse.kt
- server/domino/src/main/kotlin/com/teamfho/domino/player/PlayerController.kt

New production:
- server/domino/src/main/kotlin/com/teamfho/domino/entitlement/TrialActivation.kt
- server/domino/src/main/kotlin/com/teamfho/domino/entitlement/TrialActivationHttp.kt

Modified tests/config:
- server/domino/src/test/kotlin/com/teamfho/domino/entitlement/EntitlementTests.kt
- server/domino/src/test/kotlin/com/teamfho/domino/entitlement/EntitlementHttpTests.kt
- server/domino/src/test/kotlin/com/teamfho/domino/entitlement/EntitlementEmulatorTests.kt
- server/domino/src/test/kotlin/com/teamfho/domino/player/PlayerControllerTests.kt
- server/domino/src/test/resources/application-test.yaml

New tests:
- server/domino/src/test/kotlin/com/teamfho/domino/entitlement/TrialActivationTests.kt
- server/domino/src/test/kotlin/com/teamfho/domino/entitlement/TrialActivationHttpTests.kt
- server/domino/src/test/kotlin/com/teamfho/domino/entitlement/TrialActivationFirestoreTests.kt

New report: client/Validation/ONB_BE_07_EXPLICIT_TRIAL_ACTIVATION_REPORT.md.

COMMIT=NONE
PUSH=NONE
DEPLOY=NO

## Final gate

Final full backend run: BUILD SUCCESSFUL, 886 tests discovered, 862 PASS, 0 failures, 0 errors, 24 skipped. New TrialActivation tests: 35 PASS. Compiler errors=0. Existing BE01–06, Auth/bootstrap, Entitlements, Social, History/Replay and lifecycle suites included. No claim that skipped emulator tests ran.

Observed non-test-failing diagnostics: Redis connection-closed/reconnection exceptions during context shutdown, matchmaking unavailable during shutdown, JVM class-sharing warning, Kotlin API deprecations and Gradle deprecations. These diagnostics are retained, not described as zero exceptions.

PREEXISTING_PROTECTED_FILES_MODIFIED=0/102
SECRET_SCAN=PASS
FILES_IN_SCOPE=16
TRIAL_GRANT_ENTRYPOINT_COUNT=1
REAL_FIREBASE_AUTH_RETESTED=NO
UNITY_AUTH_RETESTED=NO
REAL_FIRESTORE_MUTATED=NO
ONB_BE_07_SUCCESS=YES
NEXT=ONB-BE-07 IMPLEMENTATION REVIEW

## Authorized checkpoint review

Reviewed against base a34702415740b3e322a08fec884f7189497911a7. Product/test source unchanged during this review; approved full suite retained, not repeated. Current XML totals corroborate 886 discovered, 862 passed, 0 failures/errors and 24 skipped. Current blocking exceptions: 0; shutdown and deprecation diagnostics above remain documented.

Inventory: 122 pending files = 102 protected baseline files, four unrelated preexisting design reports excluded, and 16 BE07 candidates. Classification: seven production files cover TRIAL_API, TRIAL_SERVICE, ENTITLEMENT, FIRESTORE, IDEMPOTENCY and CLIENT_COMPATIBILITY; eight TEST files (including test-only configuration); one VALIDATION report. UNCLASSIFIED_FILES=0. UNRELATED_FILES_FOR_BE07=0. ONB_BE_06_PENDING=0. Protected SHA256 comparison: 0/102 modified. Scope excludes Unity client, Family and operational configuration.

Single promotional grant constructor/caller path confirmed by production source search. Bootstrap only reads current entitlements and eligibility; no trial grant, consumption, receipt or clock-start write. Membership catalog and Not Now do not activate or consume trial. Activation is independent of onboarding progress. Historical grant records and ADMIN_GRANT behavior remain unchanged. Thirty-day receipt retention is encoded, not deployed as a TTL policy. Concurrency authority is Firestore transaction conflict handling over deterministic persistent marker/grant documents; local concurrency tests do not constitute a real Firestore load test.

SECRET_SCAN=PASS
RAW_UIDS_IN_REPORT=0
REAL_EMAILS_IN_REPORT=0
TOKENS_IN_REPORT=0
SECRETS_IN_REPORT=0
SUITE_REPEATED=NO
LEGACY_PREMIUM_REGRESSION=PASS
ADMIN_GRANT_REGRESSION=PASS
CURRENT_BLOCKING_EXCEPTIONS=0
DEPLOY=NO
BE07_DEPLOY_BLOCKED_BY=COMPATIBLE_UNITY_TRIAL_ACTIVATION_CLIENT
NEXT=ONB-CLIENT-01 AUTHORIZATION
