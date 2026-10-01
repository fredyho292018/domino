# ONB-CLIENT-01 Production onboarding API foundation

BASE_SHA=672ab16b4472ef021e4c9e7432b89ca3e7539d07
MODE=IMPLEMENT_AND_ISOLATED_TEST

## Existing infrastructure

Reuses DominoApiConfiguration for environment/base URL/timeout policy, IAuthTokenProvider (the existing Firebase ID token provider), IApiTransport / UnityApiTransport, SessionApiTransport, CancellableTask and installed Newtonsoft JSON. Errors use existing DominoApiException / ApiFailure, with allowlisted server codes and sanitized request IDs. No alternative HTTP implementation, dependency installation or persistent credential storage. Bootstrap remains DominoApiClient; its existing strict Player/wallet mapping remains intact.

OnboardingApiSession is a session-scoped request helper over that infrastructure, not a global store. It must be constructed with the production session cancellation token and current identity accessor, then disposed on session end. It checks identity and cancellation before token acquisition, before sending and after receiving. The cancellation generation protects logout/relogin even to the same identity. No services are instantiated by ApplicationServices or bound to production routes in this phase.

## Public DTO mapping

Backend source is authoritative: OnboardingCatalog.kt, CoachCatalog.kt, MembershipCatalog.kt, OnboardingProgressModels.kt, OnboardingFoundation.kt and TrialActivation.kt plus their controllers/services. Contract tests compare 22 public DTO field sets directly against Kotlin declarations. Synthetic responses contain fictional data only; no real Firebase fixtures were exported.

Onboarding catalog uses `locale` (not `resolvedLocale`); Coach and Membership use `resolvedLocale`. Steps contain nested localized questions/options with stable keys, ordering, requirements, availability and capabilities. Catalog loading requires loaded Player onboarding state and uses its pinned catalogVersion, including v1 and v2; locale never selects a version. Locale normalization matches backend es/en, es-US/en-US and unsupported fallback to en. Switching locale does not alter progress. Public DTOs omit persistence publication/translations/audit metadata.

Basic Profile supports FIRST_NAME, LAST_NAME, DISPLAY_NAME text answers, COUNTRY with COUNTRY_SELECT and PREFERRED_LANGUAGE with LOCALE_SELECT. No DOB. Experience helper only accepts BEGINNER/RULES_KNOWN/STRATEGY/COMPETITIVE. Coach keys remain server-authoritative, including future keys; the ten-entry avatar allowlist controls bundled resource lookup only. Unknown/missing assets resolve to the supplied neutral fallback, never an arbitrary server path or a Player avatar. Resources are reused without editing AppShellMock.

Membership DTOs support five plans, feature/quota data, billing metadata, catalog/policy versions, legacy trial and Family presentation. A product is purchasable only if the plan and mapping are active, server purchasable is true and a product ID is present. Catalog clients have no entitlement owner reference and cannot grant features. No store or Family runtime is implemented.

## Clients and mutations

OnboardingApiClient: GET catalog/state; prepare and execute start, step save, cursor and complete. State is session-local and exposed as a defensive copy. Only successful server state replaces revision; stale/replayed older revisions cannot roll back state. Conflicts leave the prior state intact. Prepared operations freeze request JSON, method/path and UUID operationId and are bound to their originating client. Explicit retries reuse the same operation, including after timeout/lost response; no transparent retry with a fresh ID. Operations remain in memory only and are not recovered across process restart.

Backend response distinction preserved: start/cursor/complete return bare OnboardingResponse; step save returns OnboardingMutationResponse. The client normalizes these into a mutation result without changing the wire contract. Domain revision maps are exactly profile/preferences for Basic Profile, domino for Experience/Coach, and empty for SKIP. Request bodies never add UID, entitlement, trial dates, duration or tier.

CoachCatalogApiClient and MembershipCatalogApiClient take the versions from the onboarding catalog at future composition time. They validate returned versions; no fixed current version is assumed.

TrialActivationApiClient prepares POST /api/v1/player/trial/activate with only operationId and expectedPolicyVersion. ACTIVATED and ALREADY_ACTIVE are validated. After success, GET player/entitlements refreshes the sole existing owner, PlayerService.ReceiveEntitlements. Activation receipt snapshots are never treated as current access authority because a retry may replay an old response. A failed refresh remains a failure and can be retried with the same operation. Server controls dates and PREMIUM_LEGACY; no client trial timer or grant owner is created.

## Compatibility header

UnityApiTransport sets exactly one X-Trial-Activation-Contract: 1 on POST /api/v1/player/bootstrap and POST /api/v1/player/trial/activate. It does not attach it to other methods/resources. It uses the shared TrialContractHeader policy tested with fake requests. Existing bootstrap and new clients map CLIENT_UPDATE_REQUIRED. Bootstrap DTO/codec also retain additive trialEligibility and capabilities without changing routing.

This phase does not complete a user-facing activation flow or authorize deployment. Backend rollout remains blocked pending the compatible full client flow and separate deployment authorization.

## Validation

NEW_CLIENT_TESTS=114_PASS

Coverage: backend DTO shape comparisons; nested catalog/locale/v1/v2 fixtures; exact request fields and domain revisions; immutable save/trial retries after response loss; server revision replacement and conflict preservation; state envelopes; future Coach keys and neutral assets; unconfigured membership products; unchanged PlayerService entitlements after catalog read; post-trial current entitlement refresh; one forced token refresh on 401; sanitized update-required error; malformed/duplicate/type-invalid responses; session A response discarded and B state untouched; cross-client operation rejection; existing History read/pagination contract.

Regressions executed locally, with no real authentication:
- AUTH-01: 14 passed.
- AUTH-02A: 53 passed.
- AUTH-02B: 55 passed.
- Logout: 22 passed.
- Player/bootstrap/realtime/correlation: 490 passed (including codec cases).
- Social: 89 passed.
- Online: 48 passed.
- Replay: 1595 checks passed against 62 existing retained fixtures / 31061 events; no remote calls.
- App Shell navigation: 85; toolbar: 17; contacts: 11; membership: 31; profile: 18; Back routing: 66 passed.

Runtime compilation uses current production scripts against installed Unity 6000.0.41f1 assemblies. An old generated Auth01 compile project initially failed because it omitted newer existing Auth files. Regenerating its source list in Generated/OnboardingClient resolved those harness-only errors. No product change was made to fix that harness. Final runtime build has zero compiler errors; five preexisting unused/unassigned-field warnings. Auth regression fixtures also produce preexisting unassigned-field warnings. This is external compilation, not a claim that the open Unity Editor was refreshed or that a visual/runtime UI gate ran. No new UI is introduced and no eight-size gate was run.

## Changed files

Modified:
- client/DominoGame/Assets/_Domino/Scripts/Infrastructure/Api/IDominoApiClient.cs
- client/DominoGame/Assets/_Domino/Scripts/Infrastructure/Api/DominoApiClient.cs
- client/DominoGame/Assets/_Domino/Scripts/Infrastructure/Api/PlayerBootstrapDtos.cs
- client/DominoGame/Assets/_Domino/Scripts/Infrastructure/Api/UnityApiJsonCodec.cs
- client/DominoGame/Assets/_Domino/Scripts/Infrastructure/Api/UnityApiTransport.cs

Added (each production source has its own .meta):
- client/DominoGame/Assets/_Domino/Scripts/Infrastructure/Api/OnboardingDtos.cs
- client/DominoGame/Assets/_Domino/Scripts/Infrastructure/Api/OnboardingApiSession.cs
- client/DominoGame/Assets/_Domino/Scripts/Infrastructure/Api/OnboardingApiClients.cs
- client/DominoGame/Assets/_Domino/Scripts/Infrastructure/Api/CoachAvatarResources.cs
- client/Validation/OnboardingClientTests.cs
- client/Validation/RunOnboardingClientTests.ps1
- client/Validation/ONB_CLIENT_01_API_FOUNDATION_REPORT.md

Generated projects/results remain ignored. Prior protected and unrelated files are excluded from this scope.

## Final gates

CLIENT_ENTITLEMENT_OWNER_COUNT=1
CATALOG_RESPONSE_ENTITLEMENT_SIDE_EFFECTS=0
APP_SHELL_MOCK_CHANGED=NO
PRODUCTION_ONBOARDING_UI_CHANGED=NO
PRODUCTION_ROUTING_CHANGED=NO
BACKEND_CHANGED=NO
REAL_TRIAL_ACTIVATED=NO
REAL_FIREBASE_MUTATIONS=0
REAL_GUEST_LOGOUT_EXECUTED=NO
PREEXISTING_PROTECTED_FILES_MODIFIED=0/102
RAW_UIDS_IN_REPORT=0
REAL_EMAILS_IN_REPORT=0
TOKENS_IN_REPORT=0
PASSWORDS_IN_REPORT=0
SECRET_SCAN=PASS
COMMIT=NONE
PUSH=NONE
DEPLOY=NO
CLIENT_02_STARTED=NO
NEXT=ONB-CLIENT-01 IMPLEMENTATION REVIEW

## Unity import and authorized final checkpoint review

Unity Editor 6000.0.41f1 was inspected directly. Play was initially active; the user confirmed stopping Play and completing Assets > Refresh. Play OFF was then visually verified, and Assets > Refresh was invoked again from the observed menu. Editor.log records completed refresh cfa85d3efb4e11d4296b1af0ebc33c57 and subsequent 5ae7baa779650bc44837c543f44165a8. Assembly-CSharp.dll (2026-09-30 22:56:24 local) is newer than the final production sources and contains OnboardingApiSession, OnboardingApiClient and TrialActivationApiClient. The current Editor is idle with zero Console errors and no import/compilation progress. No production source was changed in this review.

Current Console warnings: 2, both from the preceding Play session: missing Theme Style Sheet on PanelSettings and Firebase Database URL not configured. Console also retains a sanitized bootstrap Timeout log from that session. These are not CLIENT-01 compiler diagnostics and are not hidden or reclassified as passing tests. NEW_WARNINGS_FROM_CLIENT_01=0. CURRENT_BLOCKING_EXCEPTIONS=0 for import/checkpoint. The five prior external compiler warnings remain documented separately. No real auth or trial action was used for contract validation; no Guest Sign Out was invoked.

Added OnboardingCheckpointTests.cs and a -Checkpoint option to RunOnboardingClientTests.ps1. This isolated gate exercises the production bootstrap API/codec against BE07 additive capabilities and trialEligibility fields, optional null metadata and unlimited limits. BE07_BOOTSTRAP_CLIENT_CONTRACT=PASS, 31 checks, zero real network calls. The original 114 checks and all regression evidence are retained without rerunning the full suites. CLIENT_TESTS_REPEATED=NO (original suites); ADDITIONAL_BOOTSTRAP_CONTRACT_CHECKS=31_PASS.

Re-reviewed endpoints, exact X-Trial-Activation-Contract: 1 scope, operation snapshots, server revisions, session cancellation, catalog pinning, stable experience keys, asset-only Coach mapping and read-only Membership clients. No production wiring/routing was added. A future composition layer must dispose the client/session on logout; no automatic registration of these new clients is claimed here.

Final candidate inventory: 17 files = 9 production C# files, 4 required Unity .meta files, 2 test C# files, 1 test runner, 1 validation report. API_CLIENT/ONBOARDING/COACH/MEMBERSHIP/TRIAL are covered by the new clients; DTO by OnboardingDtos and bootstrap DTO; AUTH_HTTP_INTEGRATION by the existing transport/codec/header updates; SESSION_ISOLATION by OnboardingApiSession; TEST and VALIDATION by the listed files. The 102 protected files and four unrelated prior design reports are excluded. Required asset metadata is not generated noise. UNCLASSIFIED_FILES=0, UNRELATED_FILES_FOR_CLIENT01=0.

UNITY_ASSETS_REFRESH=PASS
UNITY_COMPILER_ERRORS=0
UNITY_WARNINGS_CURRENT=2
UNITY_WARNINGS_NEW=0
NEW_WARNINGS_FROM_CLIENT_01=0
CURRENT_BLOCKING_EXCEPTIONS=0
BE07_BOOTSTRAP_CLIENT_CONTRACT=PASS
PREEXISTING_PROTECTED_FILES_MODIFIED=0/102
DEPLOY=NO
BE07_DEPLOY_BLOCKED_BY=COORDINATED_COMPATIBLE_UNITY_RELEASE
NEXT=ONB-CLIENT-02 AUTHORIZATION
