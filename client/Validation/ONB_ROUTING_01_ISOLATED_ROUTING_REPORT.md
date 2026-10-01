# ONB-ROUTING-01 — Isolated authenticated routing

Base: `db4b2da35e0663f3f7475f9ad064fcd23e7bd4c4` (main).

## Architecture audit and activation boundary

Current live owner: `ProductionAuthRouter`, created by `ApplicationServices`, rendered by `ProductionAuthHost` through `ProductionAuthEntry`. It restores/authenticates with FirebaseAuthService, verifies password-provider sessions, initializes PlayerService and uses TemporaryAppShellPolicy to select AppShell. ApplicationServices starts session/realtime lifecycles when that route is selected. None of those files changed.

Target authenticated destination owner: `AuthenticatedRoutingOrchestrator`. It is opt-in and currently instantiated only by isolated tests and an Editor harness. Each composition has one route/state owner; there are no two active routers controlling the live UI. Future rollout must replace/delegate the existing destination decision, not run two competing decisions. Auth form navigation and provider operations remain in the existing Auth flow. No production startup registration or feature flag was enabled.

`IRoutingSessionSource` supplies the existing normalized FirebaseAuthSessionSnapshot. Anonymous identities skip verification. Only password-provider identities with unverified email target VerificationPending. Other registered providers do not implicitly become password accounts; provider implementations are not added.

`RoutingApiBinding` delegates bootstrap to the existing IDominoApiClient and state reads to CLIENT-01 OnboardingApiClient, with an epoch-owned OnboardingApiSession. There is no HTTP implementation, token acquisition or header implementation in the router. Existing TrialContractHeader logic is retained. No real binding factory is registered.

## State model and matrix

States: ResolvingSession, Welcome, VerificationPending, ResolvingPlayer, ResolvingOnboarding, Onboarding, Home, Error and UpdateRequired. The coordinator exposes a defensive copy of server onboarding state, including the exact current step and pinned version, for the future host. It does not mutate profile, start onboarding, activate trial, or select a local first step.

| Session / server state | Destination |
|---|---|
| No session | Welcome; no bootstrap/state call |
| Unverified password identity | VerificationPending; no bootstrap/state call |
| Guest / NOT_STARTED | Onboarding; no automatic start |
| Guest / IN_PROGRESS | Onboarding, exact server cursor/version |
| Guest / COMPLETED | Home |
| Verified password identity / NOT_STARTED | Onboarding |
| Verified password identity / IN_PROGRESS | Onboarding, exact server cursor/version |
| Verified password identity / COMPLETED | Home |
| Legacy exempt completed player | Home, null catalog version preserved |
| Bootstrap/state failure | Error, safe retry |
| CLIENT_UPDATE_REQUIRED | UpdateRequired, no automatic retry |

All five v2 steps and pinned v1 resume are tested. Profile completeness, membership tier, eligibility and trial are not routing inputs. Home contents and all approved onboarding/Auth views remain unchanged.

## Legacy contract correction

Backend OnboardingFoundation explicitly allows COMPLETED / LEGACY_EXEMPT with no catalogVersion or startedAt. CLIENT-01 previously required a positive version for every non-NOT_STARTED response, which prevented this required route even with a correct router.

The only existing production-file change is a narrow exception in OnboardingApiClients.Apply: COMPLETED, LEGACY_EXEMPT, null catalogVersion/currentStepKey/startedAt, nonempty completedAt and empty completed/skipped step lists. Other missing-version completed responses still fail. This changes client acceptance to match the existing backend contract; it does not change backend/schema or infer legacy status from profile fields. Integration tests exercise the actual CLIENT-01 parser/client, including invalid origins, missing timestamp, nonempty steps and cursor/startedAt contradictions.

## Session ownership, completion and errors

ResolveAsync invalidates the previous epoch, cancels/disposes its binding and clears player-specific state before session resolution. The future session-change integration must invoke it for logout, account replacement and verification refresh. Late results at session, bootstrap and onboarding stages cannot publish after replacement. Tests use providers which deliberately ignore cancellation to prove epoch guarding independently.

Restore begins in ResolvingSession, not Welcome. Each resolution stage clears the prior screen target; a failed stage publishes only a generic message, never exception text. Retry repeats the failed stage and downstream work, not earlier successful stages. UpdateRequired is terminal until a new explicit resolution. Bootstrap identity mismatch fails before any onboarding read.

After authoritative controller completion, the future host calls ReevaluateOnboardingAsync: it re-reads server state with CLIENT-01 and only then targets Home. There is no boolean/local completion override. Mutation of the exposed snapshot cannot change the route. Revision rollback and changing an already pinned version fail closed across retries. A previously unpinned NOT_STARTED state may acquire its first authoritative catalog version.

## Validation evidence

| Validation | Result |
|---|---|
| Focused routing tests | 106 PASS, zero real network calls |
| CLIENT-01 regression after legacy parser correction | 114 PASS |
| Existing BE-07 bootstrap contract regression | 31 PASS |
| Current Unity routing harness | 14 checks PASS / 12 scenarios, FAIL=0 |
| Unity import | Completed, compiler errors 0 |
| Unity compiler warnings | 9 existing unused-event warnings, 0 new routing warnings |
| Current blocking exceptions | 0; no exception after the current domain reload |
| Protected baseline | 0/102 hash changes |
| Historical pending files | All 106 hashes unchanged |

Unity harness result: Library/OnboardingRouting01.validation.txt, current run started 2026-10-01T14:35:22.5533002Z. The menu `Domino → Production Onboarding → Routing preview (isolated)` supports all twelve required scenarios. It displays route targets and retry diagnostics, not redesigned product views. Session-switch races are tested more deeply in the focused suite. No production loading/error/update screen was introduced, so a new eight-size product visual matrix is not applicable; approved views remain byte-for-byte unchanged and their evidence is retained.

The routing fixture exposes only bootstrap and state reads; the real-client fake transport rejects any non-bootstrap mutation path. No onboarding start/save/complete/trial/purchase/Auth operation is called during routing resolution. The synthetic bootstrap is POST by existing contract; zero real bootstrap requests were made. Known historical nonblocking Editor validation exceptions are not erased by this current clean import.

## Changed files

- Assets/_Domino/Scripts/Auth/AuthenticatedRoutingOrchestrator.cs and .meta: coordinator, normalized source/binding contracts, existing-client adapter.
- Assets/_Domino/Scripts/Infrastructure/Api/OnboardingApiClients.cs: narrow legacy-completed contract acceptance.
- Assets/_Domino/Scripts/UI/AppShell/Editor/AuthenticatedRoutingPreview.cs and .meta: isolated Editor harness.
- client/Validation/AuthenticatedRoutingTests.cs: focused matrix, races, retry, contract and side-effect tests.
- client/Validation/RunAuthenticatedRoutingTests.ps1: focused runner using installed local dependencies.
- This report.

Eight scoped files total. No protected startup file was required or modified. No stage/commit/push/deploy. Historical worktree files remain untouched. Fixture identifiers are synthetic aliases; no real credentials or personal identity data.

## Final gates

```text
PRODUCTION_ROUTING_OWNER_COUNT=1_PER_COMPOSITION
ROUTING_STATE_OWNER_COUNT=1
BOOTSTRAP_CLIENT_OWNER_COUNT=1
ROUTING_ORCHESTRATOR_IMPLEMENTED=YES_ISOLATED
PRODUCTION_ROUTING_ENABLED=NO
BOOTSTRAP_TRIAL_CONTRACT_HEADER=PASS
GUEST_EMAIL_VERIFICATION_REQUIRED=NO
VERIFIED_EMAIL_CAN_RESOLVE_ONBOARDING=YES
IN_PROGRESS_RESUME_SERVER_STEP=YES
COMPLETION_TO_HOME_REQUIRES_AUTHORITATIVE_STATE=YES
ROUTER_FORCES_V2=NO
LEGACY_COMPLETED_REOPENED=NO
PROFILE_FIELDS_USED_AS_ROUTING_AUTHORITY=NO
MEMBERSHIP_CONTROLS_ONBOARDING_ROUTE=NO
TRIAL_CONTROLS_ROUTING=NO
ROUTER_GET_SIDE_EFFECTS=0
ROUTING_RESOLUTION_SIDE_EFFECTS=0
BOOTSTRAP_FAILURE_FAILS_CLOSED=YES
LOGOUT_ROUTE=WELCOME
CROSS_ACCOUNT_ROUTING_STATE_LEAK=NO
STALE_PLAYER_A_ROUTING_RESPONSE_APPLIED=NO
SESSION_RESTORE_ROUTE_STABLE=YES
STALE_SCREEN_DURING_ROUTE_RESOLUTION=NO
VERIFICATION_SUCCESS_REEVALUATES_ROUTE=YES
REGISTER_UNVERIFIED_STARTS_ONBOARDING=NO
GUEST_ONBOARDING_SUPPORTED=YES
ONBOARDING_UI_CHANGED=NO
AUTH_UI_CHANGED=NO
HOME_CONTENT_CHANGED=NO
APP_SHELL_MOCK_CHANGED=NO
APPROVED_SCREEN_VISUAL_REGRESSIONS=0_SOURCE_UNCHANGED
BE07_DEPLOYED=NO
REAL_OPERATIONS=0
REAL_BACKEND_ONBOARDING_WRITES=0
REAL_TRIAL_ACTIVATED=NO
REAL_STORE_PURCHASES=0
REAL_FIREBASE_MUTATIONS=0
REAL_GUEST_LOGOUT_EXECUTED=NO
PREEXISTING_PROTECTED_FILES_MODIFIED=0/102
HISTORICAL_PENDING_FILES_PRESERVED=YES
SECRET_SCAN=PASS
COMMIT=NONE
PUSH=NONE
DEPLOY=NO
ONB_ROUTING_01_SUCCESS=YES
NEXT=ONB-ROUTING-01 IMPLEMENTATION REVIEW
```
