# ONB-ROUTING-02 — Production composition integration

Base: `eb73988b3f025a5a6c4be047243e93e286b38ddc`, branch main.

## Before / after and protection gate

Composition root: `ApplicationServices.Start` (BeforeSceneLoad). Previously it created `ProductionAuthRouter` with `TemporaryAppShellPolicy`. `ProductionAuthEntry.Mount` mounts the UIDocument/ProductionAuthHost after scene loading; host Bind calls RestoreAsync. The router bootstrapped PlayerService and decided AppShell itself.

Current consumers audited: ApplicationServices creates, subscribes for session lifecycles, stops/disposes during Logout/shutdown; ProductionAuthHost subscribes/renders and invokes Auth actions; ProductionEmailRuntimeValidation and WelcomeRuntimeFooterProbe inspect the existing facade. Standalone Auth tests still construct its compatibility composition.

None of the required composition, Auth facade, host or PlayerService files belongs to the 102 protected files. The 106 historical pending files remain byte-for-byte identical to their recorded baseline. No protected file was edited.

Production now injects `ProductionRoutingComposition` into ProductionAuthRouter. It owns one `AuthenticatedRoutingOrchestrator`, whose destination is projected into the existing presentation enum. ProductionAuthRouter retains form navigation, provider operations and feedback; its old authenticated policy is not invoked in the injected production path. Compatibility construction without injection is retained for existing isolated tests. It is not a second owner in production.

## Bootstrap, state and activation

The source resolves FirebaseAuthService before choosing a destination. Password sessions reload verification and refresh a verified token through the existing Auth implementation. Anonymous identities require no email verification. Bootstrap uses the existing PlayerService and CLIENT-01 API, preserving snapshots, identity checks, cancellation, error codes and the existing trial-contract header. No second bootstrap client or header implementation is introduced.

PlayerService now retains the already-existing bootstrap `trialEligibility` DTO and clears it on disposal. Membership receives that authoritative value and existing entitlements, never an invented eligibility value. This is client state wiring, not a backend/DTO/schema change.

Each resolution binding owns one OnboardingApiSession, OnboardingApiClient and OnboardingShellController. Routing reads state; the controller owns explicit start/save/complete/trial actions. Route entry loads catalog/state through that controller but does not automatically start onboarding. Pinned v1/v2 and exact server cursor are preserved. Profile completeness, membership and trial do not decide Home versus Onboarding.

Activation mechanism: explicit constructor injection in the production branch of ApplicationServices. No remote configuration infrastructure or rollout flag was added. The code path is connected in source, not released or deployed. Tests inject fake SDK/transport dependencies into the same composition and exercise the actual ProductionAuthHost in Edit Mode. ApplicationServices runtime initialization, network, Firebase SDK and real session operations were not run.

## Route targets

| Authority | Actual target |
|---|---|
| No session | ProductionWelcomeView; no bootstrap/onboarding call |
| Password email unverified | ProductionEmailView VerificationPending; no bootstrap/onboarding call |
| Guest or verified, NOT_STARTED | ProductionOnboardingRoot; controller-owned explicit start |
| Guest or verified, IN_PROGRESS | ProductionOnboardingRoot, exact server step/catalog |
| Guest or verified, COMPLETED | ProductionAppShell Home |
| Legacy completed | Home without reopening onboarding |
| Session/player/state resolving | ProductionRoutingStatusView loading; no stale Welcome/Home |
| Bootstrap/onboarding failure | Safe error and stage-specific retry |
| CLIENT_UPDATE_REQUIRED | Update required; no retry or fallback |

Welcome, Email/verification, Home and approved onboarding view source files are unchanged. New loading/error/update presentation uses existing theme and semantic feedback components. No store update workflow was added.

## Completion and session lifetime

Server-confirmed controller COMPLETED triggers the orchestrator's state reread. Home is selected only after authoritative COMPLETED is read successfully. There is no local completion flag, timer or new CTA.

Completed view production role: the approved CLIENT-02 view remains available to isolated/diagnostic presentation. The live composition transitions directly through controlled routing resolution to Home, avoiding a timed or imperceptible success flash. Initial completed sessions do not load the onboarding UI/catalog. Home content is unchanged.

Logout continues through the existing stop/dispose, Reset/Start, SessionReplaced boundary. That boundary invalidates the old routing binding/controller and creates the next composition. Late responses from a disposed composition cannot affect the next account. No real logout was executed. Tests cover late account A responses versus a replacement account B composition. Arbitrary external Firebase account replacement outside the existing application session replacement boundary is not newly introduced as a supported login mechanism.

Composition notifications are deduplicated by orchestrator route. Host keeps a single root target and reuses it while its route remains active. Session lifecycle/reward recovery startup is guarded once per composition. Repeated retry does not install new subscriptions. Auth form failures remain visible, including a failed explicit Guest authentication.

## Validation

| Test | Result |
|---|---|
| Production composition | 89 PASS |
| ROUTING-01 regression | 106 PASS |
| CLIENT-01 contracts | 114 PASS |
| Bootstrap contract | 31 PASS |
| CLIENT-02 shell | 93 PASS |
| CLIENT-02 full flow | 407 PASS |
| Auth Guest regression | 14 PASS |
| Email register/verification regression | 53 PASS |
| Email sign-in/reset regression | 55 PASS |
| Logout fixture regression | 22 PASS |
| Unity actual host / actual views | 240 PASS, 22 scenarios |
| New status presentation responsive | 8/8 PASS |

The focused runner compiles production composition plus its controllers and existing CLIENT-01 dependencies. Its Suite option reruns existing Auth tests without rewriting those test sources. Every SDK/network dependency is synthetic. The isolated completion test uses the normal controller/API flow against the fake server, then verifies a fresh authoritative state read before Home.

Unity cases: no session, unverified, guest not started, all five guest in-progress steps, guest completed, verified not started/in-progress/completed, legacy completed, pinned v1/v2, bootstrap/state errors, update required, restore, session switch, logout, verification transition. The harness binds the actual ProductionAuthHost to an inactive Editor-only GameObject with fake composition, asserts one root and the concrete production view type, then validates the new status views at eight sizes. Deeper late-response races are covered by focused tests.

During validation the first fake update-required response omitted required requestId; corrected in fixture only. Its failed console executable displayed a Windows exception dialog, subsequently closed. An Editor harness request-file deletion briefly raced the request writer and logged IOException; the harness now retries on IOException and requests are published after closing the writer. These intermediate failures are not hidden or counted as passing runs.

## Final Unity import validation — 2026-10-01

Final imported runtime assembly: 2026-10-01T15:42:02Z. Final imported Editor assembly: 2026-10-01T15:42:04Z. Both postdate the final Auth facade and harness source edits. Unity completed compilation and domain reload successfully.

A new prepared harness run started **2026-10-01T15:46:49.4112379Z**:
- FINAL_UNITY_CHECKS=240_PASS
- FINAL_UNITY_SCENARIOS=22_PASS
- STATUS_RESPONSIVE=8/8_PASS
- FAIL=0
- REAL_OPERATIONS=0
- PLAY_MODE=OFF

This run is distinct from the earlier 15:31:55 run and validates the final imported source. The earlier result was preserved separately in ignored Library evidence. The report's earlier 240-check evidence is not being relabeled as this final run.

Current import/validation log: compiler errors 0; nine distinct preexisting CS0067 unused-event warnings in existing Online/Rewards validators; new ROUTING-02 warnings 0; current blocking exceptions 0. The previous request-file IOException remains historical evidence and did not recur after final import. No claim is made that the entire Editor session has never logged an exception.

Production composition harness uses the same ProductionRoutingComposition constructor injected by ApplicationServices, together with the actual ProductionAuthHost and concrete production views. All 22 cases have exactly one final root. The retained 89 composition checks provide the deeper late-response replacement and authoritative completion-to-Home evidence. Completion mutations in that test were synthetic server operations; the final Unity routing harness issued zero onboarding writes.

SOURCE_CHANGED_DURING_FINAL_IMPORT=NO: all 14 non-report scoped file hashes remained unchanged during this turn. Only this requested report was updated. PREVIOUS_SUITES_REPEATED=NO. Retain 89 composition, 106 ROUTING-01, 114 CLIENT-01, 31 bootstrap, 407 CLIENT-02 full-flow, and 144 Auth/Logout checks. The separately recorded 93 shell checks are also retained.

Protection/security rerun: 102/102 protected hashes match; all 106 historical pending files remain and their hashes match; no unclassified pending files; scoped secret scan passes. Git index remains empty. No live test, rollout, deployment or real operation.

ONB_ROUTING_02_SUCCESS=YES.
NEXT=ONB-ROUTING-02 FINAL REVIEW.

## Changed files

Four existing production files: Auth/ProductionAuthRouter.cs, Infrastructure/ApplicationServices.cs, Player/PlayerService.cs, UI/AppShell/ProductionAuthHost.cs.

New production files (with meta): UI/AppShell/ProductionRoutingComposition.cs and ProductionRoutingStatusView.cs.

New Editor validation files (with meta): UI/AppShell/Editor/RoutingCompositionFixture.cs and ProductionRoutingValidation.cs.

New standalone validation: ProductionRoutingCompositionTests.cs, RunProductionRoutingCompositionTests.ps1, and this report. Total 15 scoped files including four meta files. No historical file is included in this scope.

## Live-test boundary

BE07_DEPLOYED=NO. Production routing is connected in source only. No backend deploy, real onboarding writes, real trial, store purchase, Firebase mutation or Guest logout. No live end-to-end routing test. ONB-LIVE-01 has not started.

COMMIT=NONE. PUSH=NONE. DEPLOY=NO. No staging performed.

PREEXISTING_PROTECTED_FILES_MODIFIED=0/102. HISTORICAL_PENDING_FILES_PRESERVED=YES (106 hashes). SECRET_SCAN=PASS (15 scoped files). ONB_ROUTING_02_SUCCESS=YES.

NEXT=ONB-ROUTING-02 FINAL REVIEW.
