# ONB-BE-03 — Player onboarding progress and answers API

Base: 22d9b9a40335aec67f5ec1c194e8dd554122ef65. Date: 2026-09-30. Implement/test only; no live calls, rollout, seed, commit, push or deployment.

## Contract and endpoints

The authoritative ONB-00 matrix specifies explicit START. A SAVE against NOT_STARTED is rejected; the first successful POST start is the interaction that assigns server UTC startedAt and pins the current publication. GET never initializes state. Missing BE-01 documents return ONBOARDING_ROLLOUT_UNRESOLVED; this capability does not silently migrate existing accounts while initialization is disabled.

| Endpoint | Request | Response |
|---|---|---|
| GET /api/v1/player/onboarding | None | OnboardingResponse |
| POST /api/v1/player/onboarding/start | operationId, expectedRevision | OnboardingResponse |
| PUT /api/v1/player/onboarding/steps/{stepKey} | operationId, expectedRevision, catalogVersion, domainRevisions, action, answers | OnboardingMutationResponse |
| PUT /api/v1/player/onboarding/cursor | operationId, expectedRevision, catalogVersion, targetStepKey, optional targetSubstepKey | OnboardingResponse |
| POST /api/v1/player/onboarding/complete | operationId, expectedRevision, catalogVersion | OnboardingResponse |

All routes use existing Firebase authentication; identity exclusively comes from the principal, not request fields. ACTIVE Player is reread inside the transaction. Writes enforce current verified email for password sign-in. Anonymous Guest is supported. Recognized Google/Facebook/Phone provenance follows the approved policy. Unknown/custom provenance with linked password requires verified email; without it, AUTH_CONTEXT_UNSUPPORTED. No verification fact is persisted to Player.

Requests use strict existing catalog codec parsing (unknown fields, invalid enums, invalid scalar types rejected) with a 16 KiB body cap and canonical UUID operation IDs. Error DTO is code/message/requestId with no-store and X-Request-ID, without submitted values or raw exceptions. GET and successful mutation responses are no-store. No endpoint creates a Social profile.

OnboardingResponse includes all existing progress fields plus requiredFieldsMissing, domainRevisions and derived typed answers. No UID/email/phone/token. OnboardingMutationResponse contains onboarding plus the changed DominoProfile projection (null if unchanged). Canonical answers are derived from domains, not a second generic answer store.

## Transitions and version semantics

- START: NOT_STARTED to IN_PROGRESS, first active step from published immutable catalog; server timestamp once. Repeated START while in progress resumes the pinned state without timestamp/revision changes, after normal revision checking. Completed cannot restart.
- SAVE EXPERIENCE_STEP: validates authoritative pinned step/question/type/active option and required answer; writes ExperienceLevel in DominoProfile. Stable options BEGINNER/RULES_KNOWN/STRATEGY/COMPETITIVE only; never rating/localized labels.
- SAVE requires touched domainRevisions exactly domino, compared alongside onboarding revision. Domain revision increments only on actual answer changes. Progress revision increments once per successful new progress operation.
- Server computes frontier as first active step without a completed/skipped outcome. After experience this is COACH_STEP. When all outcomes are present but completion has not run, cursor remains at the last active step, satisfying the existing IN_PROGRESS model invariant.
- Cursor only permits a target whose preceding active steps have completed/skipped outcomes. V1 has no substep schema; non-null substep input is rejected. It cannot bypass a required Coach step.
- Back/edit updates the canonical answer, preserves unrelated later completed answers, and returns to the first outstanding step. V1 declares no answer dependencies requiring invalidation. Future dependencies need explicit catalog/domain work, not blanket erasure.
- Optional CONTACTS_STEP/MEMBERSHIP_STEP support SKIP only, empty answers and no domain revisions. SAVE actions are unavailable; no contact import, purchase or trial call occurs. Required skip is rejected.
- COMPLETE loads pinned publication, checks every required step outcome, existing display-name validity and required domain answers, then calls Coach validation again. Optional unanswered/skipped steps do not block completion. completedAt is server-owned and never rewritten by repeat completion. Completed/LEGACY_EXEMPT state remains completed; progress writes cannot reopen it.
- A later pointer publication does not change a pinned session. Incompatible submitted versions fail, never migrate implicitly.

## Coach boundary

Production OnboardingProgressService uses UnavailableOnboardingCoach: authoritative validation always returns DEPENDENCY_UNAVAILABLE until BE-04. There is no production coach key list, catalog or arbitrary-coach acceptance. V1 has no coach publication pin, so real completion remains blocked. Tests alone use a clearly synthetic authoritative validator and a test-only catalog pin to exercise optional skip/completion; the source v1 resource was not changed. Test completion under that fixture is not evidence that production can complete before BE-04.

## Atomic persistence, revision and retries

Firestore adapter executes a read/write transaction with five SDK retry attempts. Reads include ACTIVE Player, preferences/domino/progress, scoped receipt, and authoritative catalog/pointer as needed. Writes are buffered until all reads/validation finish. Only onboarding/current, dominoProfile/current when changed, and mutationReceipts/{operationId} may be written. Player root, preferences, catalog and Entitlements are not written.

The receipt stores endpoint/step scope, SHA-256 of canonical typed request, response snapshot and server createdAt/expiresAt. The authenticated account path isolates users. Unexpired identical operations replay their original response before stale-revision evaluation; changed request/scope fails IDEMPOTENCY_CONFLICT. A replay can return an older projection; the client must ignore older revisions. The guarantee is 30 days. Expired receipt no longer bypasses revision checks. Expiry timestamps are stored; no physical Firestore TTL policy/index was deployed.

Firestore transaction contention protects domain/progress/receipt together across instances. Aborted retries never commit partial progress. Tests use SDK doubles and serialized in-memory transactions, not live Firestore contention. No email, billing or other external side-effect action is invoked inside a domain transition. The future Coach validation seam receives the same transaction so BE-04 can perform authoritative reads without independently committing.

## Errors

Implemented applicable codes: REQUEST_INVALID400, PLAYER_NOT_FOUND404, PLAYER_NOT_ACTIVE403, ONBOARDING_ROLLOUT_UNRESOLVED409, ONBOARDING_NOT_STARTED409, EMAIL_VERIFICATION_REQUIRED403, AUTH_CONTEXT_UNSUPPORTED403, ONBOARDING_CATALOG_NOT_FOUND404, ONBOARDING_CATALOG_VERSION_MISMATCH409, ONBOARDING_STEP_NOT_FOUND404, ONBOARDING_STEP_INACTIVE409, ONBOARDING_STEP_NOT_REACHABLE409, ONBOARDING_INVALID_ANSWER400, ONBOARDING_REQUIRED_FIELD_MISSING400, ONBOARDING_REQUIRED_STEP_CANNOT_SKIP400, REVISION_MISMATCH409, IDEMPOTENCY_CONFLICT409, ONBOARDING_INCOMPLETE400, ONBOARDING_ALREADY_COMPLETED409, DEPENDENCY_UNAVAILABLE503, FIRESTORE_CONTENTION_EXHAUSTED503. Complete on already completed with current revision is a no-op; original-operation retry returns its receipt.

## Exact scope

New production files under server/domino/src/main/kotlin/com/teamfho/domino/player:
- OnboardingProgressModels.kt — typed DTOs, errors, write auth policy, future Coach transaction seam/default unavailable.
- OnboardingProgressRepository.kt — transactional Firestore adapter with buffered writes.
- OnboardingProgressService.kt — state machine, domain answers, receipts, cursor and completion validation.
- OnboardingProgressHttp.kt — strict bounded HTTP parsing, routes, no-store/error responses and dependency wiring.

New tests under the corresponding test player package:
- OnboardingProgressTests.kt — domain/auth/idempotency/concurrency tests and in-memory fixture.
- OnboardingProgressHttpTests.kt — authenticated read/start/save, strict identity/unknown/oversized payload rejection and unresolved state.
- OnboardingProgressFirestoreTests.kt — actual SDK adapter callback retry/ordering/atomic write-set verification using doubles.

This report is the eighth new file. No existing source, catalog v1, protected file, trial implementation, production routing or configuration value was changed. No generic answer collection, basic-profile questions, Coach/Membership catalog, FCM, Unity or new initialization path.

## Validation log

Production compiled immediately. The HTTP test fixture initially exposed an internal Kotlin type; adjusted visibility only in test code, then focused tests passed. No production contract was weakened. The final focused suite contains 31 cases. Existing Gradle deprecation/JVM class sharing and Redis loopback shutdown warnings are not hidden as zero warnings.


## Final validation result

Full backend :test BUILD SUCCESSFUL: 758 reported XML cases, 734 PASS, 0 FAIL, 24 SKIPPED. New tests: 31 PASS (25 domain/security/concurrency; 5 HTTP; 1 SDK transaction adapter). Auth verifier/security 45 PASS, PlayerController 33 PASS, Entitlements 17 PASS, History/Replay 13 PASS, Social lifecycle 4 PASS. Redis/parity environment-gated cases remain skipped; REAL_FIRESTORE/EMULATOR tagged tests are excluded. No new live Firebase/Unity or server test is claimed. Resume was tested through service recreation and persisted backend reads, not a real client restart.

Final protected SHA-256 comparison: 0/102 modified. Scoped source/report scan and content review: no secrets, real emails, tokens or raw account IDs; no mojibake matches. Compiler errors zero. Catalog resource and all prior production sources have no diff against base.

```text
BASE_SHA=22d9b9a40335aec67f5ec1c194e8dd554122ef65
BASIC_PROFILE_FIELDS_IN_CATALOG_V1=NO
ONBOARDING_STATE_ENDPOINT=GET /api/v1/player/onboarding
START_ENDPOINT=POST /api/v1/player/onboarding/start
SAVE_STEP_ENDPOINT=PUT /api/v1/player/onboarding/steps/{stepKey}
CURSOR_ENDPOINT=PUT /api/v1/player/onboarding/cursor
COMPLETE_ENDPOINT=POST /api/v1/player/onboarding/complete
PLAYER_ID_FROM_AUTH_CONTEXT=YES
UNVERIFIED_EMAIL_ONBOARDING_WRITE_BLOCKED=YES
GUEST_ONBOARDING_SUPPORTED=YES
GET_ONBOARDING_SIDE_EFFECTS=0
NOT_STARTED_TO_IN_PROGRESS_TRIGGER=EXPLICIT_START_PER_FINAL_CONTRACT
STARTED_AT_SERVER_CONTROLLED=YES
EXPERIENCE_DOMAIN_BOUND=YES
EXPERIENCE_OPTION_KEYS=BEGINNER,RULES_KNOWN,STRATEGY,COMPETITIVE
EXPERIENCE_LEVEL_IS_RATING=NO
HARDCODED_COACH_KEYS_IN_BE03=0_PRODUCTION
COACH_COMPLETION_DEPENDENCY=ONB-BE-04
ONBOARDING_COMPLETABLE_WITHOUT_COACH=NO
CONTACTS_INTEGRATION_IMPLEMENTED=NO
CONTACTS_STEP_SKIPPABLE=YES_WHEN_REACHABLE
MEMBERSHIP_STEP_SKIPPABLE=YES_WHEN_REACHABLE
MEMBERSHIP_PURCHASE_REQUIRED=NO
TRIAL_REQUIRED=NO
NOT_NOW_CONSUMES_TRIAL=NO
REQUIRED_STEP_SKIP_REJECTED=YES
CURRENT_STEP_SERVER_CONTROLLED=YES
RESUME_EXACT_NEXT_STEP=PASS_BACKEND_PERSISTENCE
IN_PROGRESS_CATALOG_VERSION_PINNED=YES
CATALOG_VERSION_MISMATCH_HANDLED=YES
STALE_REVISION_REJECTED=YES
STEP_SAVE_IDEMPOTENT=YES_30_DAY_RECEIPT_WINDOW
SERVER_VALIDATES_ONBOARDING_COMPLETENESS=YES
CLIENT_CAN_FORCE_COMPLETED=NO
OPTIONAL_STEPS_BLOCK_COMPLETION=NO
LEGACY_COMPLETED_STATE_PRESERVED=YES
NEW_PLAYER_ROUTING_CHANGED=NO
ONBOARDING_ROLLOUT_ENABLED=NO
BOOTSTRAP_AUTO_GRANTS_TRIAL_BEHAVIOR_CHANGED=NO
TRIAL_ACTIVATION_ENDPOINT_IMPLEMENTED=NO
COACH_CATALOG_IMPLEMENTED=NO
MEMBERSHIP_CATALOG_IMPLEMENTED=NO
FIRST_NAME_ONBOARDING_IMPLEMENTED=NO
LAST_NAME_ONBOARDING_IMPLEMENTED=NO
COUNTRY_ONBOARDING_IMPLEMENTED=NO
ONBOARDING_MEMBERSHIP_WRITE_ENTITLEMENT_SIDE_EFFECTS=0
CATALOG_MUTATED_BY_PLAYER_WRITE=NO
SENSITIVE_AUTH_DATA_IN_ONBOARDING_DTO=0
NEW_BE03_TESTS=31_PASS
FULL_SUITE=734_PASS_0_FAIL_24_SKIPPED
COMPILER_ERRORS=0
PREEXISTING_PROTECTED_FILES_MODIFIED=0/102
SECRET_SCAN=PASS
COMMIT=NONE
PUSH=NONE
DEPLOY=NO
ONB_BE_03_SUCCESS=YES
NEXT=ONB-BE-03 IMPLEMENTATION REVIEW
```

## Authorized final checkpoint review

Reviewed against base 22d9b9a40335aec67f5ec1c194e8dd554122ef65. The implementation-stage COMMIT/PUSH fields above describe the prior review, not the subsequent authorized checkpoint. No production or test source changed during this review; the approved 31 new passing tests and full 734 pass / 0 fail / 24 skipped evidence are retained without rerunning.

Request DTO names, respectively: none, OnboardingStartRequest, SaveStepRequest, OnboardingCursorRequest, OnboardingCompleteRequest. Every mutation checks expectedRevision and uses an account- and operation-scoped 30-day receipt; step save additionally checks the touched DominoProfile revision. Reads require authentication but have no revision precondition or receipt. No other write endpoint is introduced.

Pending inventory: 114 files = 102 protected + 4 unrelated prior design reports + 8 BE-03 candidates. The eight candidates are exactly the four production files, three test files and this report listed above. Models cover DOMAIN_BINDING/auth/DTOs; service covers IDEMPOTENCY/CONCURRENCY/domain transitions; repository covers ONBOARDING_PERSISTENCE; HTTP covers ONBOARDING_API. All files are classified. The four prior design reports remain excluded. BE-02 has no pending implementation files. Protected hashes remain unchanged (0/102).

Production Coach keys: zero. The synthetic FIXTURE_COACH literal exists only in tests. No authoritative Coach implementation is included; normal production completion remains blocked until BE-04. No Basic Profile, rollout, trial activation, Unity integration or configuration change is included. Existing completed legacy states are preserved. New foundation states remain NOT_STARTED; unresolved foundation documents are not initialized by these endpoints.

Scoped content review found no credential material, real account identifiers, real email addresses or mojibake. Synthetic authentication labels in tests are non-secret fixtures. Final staging must contain exactly these eight reviewed candidates. Commit and remote SHA are recorded in the final checkpoint response, avoiding a self-referential commit hash in this report.
