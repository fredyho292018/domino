# AUTH-01 Production Guest integration

Base: `19904fb45044f6b3250edd5dc25e7b4b4ff794e8`.

## Reused flow and startup

Existing FirebaseBootstrap -> FirebaseSdkClient.GetCurrentUser / SignInAnonymouslyAsync -> FirebaseIdTokens.GetAsync -> DominoApiClient -> PlayerService -> existing POST player/bootstrap remains the sole Guest pipeline. No second anonymous implementation, token store, Player endpoint or bootstrap repository was introduced.

ApplicationServices now composes ProductionAuthRouter in normal application mode without automatically launching Guest, Player bootstrap or realtime. Explicit isolated Editor validation factories retain their legacy validation entry. ProductionAuthEntry installs a single persistent UIDocument host on scene load and suppresses the legacy controller's startup at runtime without editing or deleting legacy UI source. Existing shell pages and their demo sources are unchanged. Session lifecycles (realtime, monetization/rewards) start after confirmed Player bootstrap, not on an unauthenticated application pause/resume callback.

ProductionAuthHost owns Welcome/shell mounting; individual shell pages do not route authentication. A restored Firebase user (anonymous or registered) is reused. No user yields Welcome; Guest tap invokes FirebaseAuthService.ContinueAsGuestAsync. Busy is reserved before notifications and repeated taps share the operation. Shell mounting requires SYNCED, confirmed snapshots and identity equality; task completion alone is not success.

## Explicit retry and continuity

FirebaseAuthService.InitializeAsync retains its prior memoized failure behavior for existing consumers. RestoreAsync / ContinueAsGuestAsync provide explicit retry; dependency failures can be retried. Before another sign-in, the SDK current user is read. An anonymous account surviving a bootstrap failure is therefore reused.

PlayerService.RetryBootstrapFromAuthAsync enables explicit pre-acceptance retries without changing legacy CanRetry semantics. It does not bypass UID, status, DTO or wallet validation and cannot replace an already confirmed Player through this route. Existing operation serialization remains authoritative.

TemporaryAppShellPolicy implements IPostAuthenticationPolicy: authenticated and bootstrapped Player -> AppShell. This is explicitly TEMPORARY_APP_SHELL, not a stored onboarding decision. The interface is the future onboarding routing boundary; no onboardingCompleted field or persistence exists.

## Production Welcome

ProductionWelcomeView reproduces the approved compact rows, bundled provider graphic assets, handset Phone icon, source-sans typography and ModernSocialPremium layout. Asset resource paths retain historical AppShellMockIcons names, but no MockState, MockShellView, MockShellHost or mock preview class is referenced. Google/Facebook brand backgrounds remain brand colors; app surfaces and text consume theme tokens.

Google, Facebook, Email, Phone and Sign In show Coming Soon and perform no authentication. Guest alone calls the router. Busy disables Guest; errors are fixed user-facing text, with no exception details, identity or credential values. The shell still displays intentional Alex/Amara/history/Puzzles/Watch demo data. No demo identity is replaced by a raw Guest identifier.

## Logout

The audit found no existing real logout. Added IFirebaseSessionControl.SignOut implemented by FirebaseSdkClient using Auth.SignOut. ApplicationServices.LogoutGuestAsync requires an explicit confirmedLossOfAccess flag and an anonymous session, drains the router, signs out and rebuilds the service lifetime. Player, sockets and reward services are disposed; the host rebinds to the fresh router and restores to Welcome. Async recovery captures its session-owned services to avoid operating on a replacement session after logout.

No visible destructive Guest logout control was added. GUEST_LOGOUT_RISK=LOSS_OF_ACCESS_TO_UNLINKED_GUEST; future UI must warn and obtain confirmation. No account deletion, recovery, linking or global token revocation is implemented. Isolated tests verify sign-out plus a fresh Player/router has no stale snapshot; a real user was not signed out.

## Validation

- Local compile of current runtime sources and Welcome Editor fixture: PASS.
- AUTH-01 isolated functional tests: 14 PASS, real existing FirebaseAuthService + PlayerService + DominoApiClient with fake SDK/transport boundaries. Cases: no session/no automatic creation, double submit, token/backend-client path, UID correlation, restart same identity/player, bootstrap503 and same-Guest retry, sanitized sign-in failure/retry, response identity mismatch and explicit revalidation, registered restore, logout without stale Player, late response after disposal.
- Existing Guest Auth: 43 PASS. A regression initially found automatic retry of legacy failures; corrected to explicit Auth actions, then rerun successfully.
- Player foundation: 489 PASS, codec9, retry/concurrency/alias/realtime checks included.
- Matchmaking:75 PASS. Replay:1595 PASS,62 retained matches and31061 events, no Firestore calls. Social:89 PASS.
- Unity Welcome:608 PASS, all eight presets (375x667,393x852,412x915,430x932,480x1040,600x960,768x1024,834x1194), centering, bounds/reachability, hidden scrollbar, provider graphics/labels/placeholders, loading and Guest action.
- Unity production shell:4333 PASS in this task; all six view regressions retained. No shell page source changed.
- Unity imported the final status-string correction at 00:45 (source updated 00:42:07). Current Welcome validation completed on 2026-09-30 at 00:51:25 local: 608 checks passed, zero failed. Current compilation has zero errors; nine distinct CS0067 unused-event warnings belong to existing Client/Online Editor validation fixtures. No current blocking exception was found. No live Firebase account creation or remote TEST bootstrap was performed. Functional continuity results are isolated simulations, not live remote evidence.

Editor validation: `Domino/Production Auth/Welcome preview (isolated)`. Its Guest callback is deliberately local to the visual fixture; actual runtime ProductionAuthHost uses the real router. The preview does not log out the user's existing Firebase session. It was opened at 393x852 in isolated NO_SESSION state; the preview request was consumed by the Editor.

## Scope and status

Modified: Infrastructure/ApplicationServices.cs, FirebaseAuthService.cs, FirebaseBootstrap.cs, FirebaseSdkClient.cs, IFirebaseClient.cs, Player/PlayerService.cs.
New: Auth/ProductionAuthRouter.cs; production Welcome, AuthHost and AuthEntry; Editor ProductionWelcomeValidation; corresponding metadata; Auth01Tests.cs; RunAuth01Tests.ps1; this report. Generated compile/test artifacts are local validation tooling.

EXISTING_GUEST_FLOW_REUSED=YES
AUTH_ROUTER_TYPE=ProductionAuthRouter
PRODUCTION_WELCOME=IMPLEMENTED
PRODUCTION_WELCOME_DEPENDS_ON_MOCK=NO
NO_SESSION_ROUTE=PRODUCTION_WELCOME
GUEST_ACTION=EXISTING_ANONYMOUS_AUTH_AND_BOOTSTRAP
DOUBLE_SUBMIT_PREVENTED=YES
POST_GUEST_DESTINATION_POLICY=TEMPORARY_APP_SHELL
ONBOARDING_ROUTING_HOOK_READY=YES
ONBOARDING_PERSISTENCE_IMPLEMENTED=NO
RESTORED_GUEST_REUSED=YES_ISOLATED_TEST
GUEST_IDENTITY_CONTINUITY=PASS_ISOLATED_TEST
PLAYER_CONTINUITY=PASS_ISOLATED_TEST
PLAYER_BOOTSTRAP_DUPLICATES=0_ISOLATED_TEST
BOOTSTRAP_FAILURE_RETRY=PASS
SECOND_ANONYMOUS_ACCOUNT_CREATED=NO
GUEST_LOGOUT=CONFIRMED_INTERNAL_ROUTE
AFTER_LOGOUT=PRODUCTION_WELCOME_ISOLATED_TEST
GOOGLE_REAL_AUTH=NO
FACEBOOK_REAL_AUTH=NO
EMAIL_REAL_AUTH=NO
PHONE_REAL_AUTH=NO
GOOGLE_AUTH_CALLS=0
FACEBOOK_AUTH_CALLS=0
EMAIL_AUTH_CALLS=0
PHONE_AUTH_CALLS=0
WELCOME_CENTERING=8/8_PASS
WELCOME_OVERFLOW=8/8_PASS
WELCOME_REACHABILITY=8/8_PASS
SHELL_REGRESSION=4333_PASS
AUTH_REGRESSION=43_PASS
PLAYER_BOOTSTRAP_REGRESSION=489_PASS
MATCHMAKING_REGRESSION=75_PASS
HISTORY_REPLAY_REGRESSION=1595_PASS
SOCIAL_REGRESSION=89_PASS
RAW_UIDS_IN_LOGS=0_IN_NEW_VALIDATION_OUTPUT
TOKENS_IN_LOGS=0
MOJIBAKE_MARKERS=0
SECRET_SCAN=PASS
LOCAL_COMPILER_ERRORS=0
FINAL_UNITY_IMPORT=PASS
PREEXISTING_PROTECTED_FILES_MODIFIED=0/102
LEGACY_UI_CHANGED=NO_SOURCE_CHANGES
BACKEND_SOURCE_CHANGED=NO
NEW_ENDPOINTS=0
SCHEMA_CHANGED=NO
REDIS_CHANGED=NO
FIREBASE_CONFIG_CHANGED=NO
COMMIT=NONE
PUSH=NONE
DEPLOY=NO
AUTH_01_SUCCESS=YES
AUTH_02_STARTED=NO
NEXT=FINAL_MANUAL_PRODUCTION_WELCOME_AUTH_REVIEW

## Final imported-version validation — 2026-09-30

The final imported source was validated using a new Auth01.request, consumed by Unity only while not compiling, updating or playing. Auth01Welcome.result.txt was rewritten during this execution. All eight logical sizes passed (608/608); completion remounted the isolated Welcome at 393x852. No production Guest authentication was initiated. Exact primary/secondary strings were reviewed in UTF-8 source; the imported view rendered these labels and passed its rendered-label encoding and geometry checks. Existing bundled Google/Facebook, envelope, green handset and user assets resolved successfully.

The last correction was limited to router status text. Earlier isolated functional results and 4333 shell checks are retained, not represented as fresh executions. No live Firebase/TEST authentication or new account creation occurred in this final gate. Manual visual approval remains pending.

Current source/report scan: 14 scoped files, zero detected credential patterns and zero mojibake markers. New validation output contains no raw identities or tokens. Protected inventory SHA-256 comparison: 102 checked, zero modified. Only this report changed during the final gate; no product source was modified.

ASSETS_REFRESH=PASS
COMPILER_ERRORS=0
CURRENT_BLOCKING_EXCEPTIONS=0
WARNINGS=9_CS0067_EXISTING_EDITOR_FIXTURES
FINAL_TEXT_VALIDATION=PASS
WELCOME_VISUAL_CONTRACT=PASS
PRODUCTION_WELCOME_VISIBLE=YES_ISOLATED_PREVIEW
PRODUCTION_SHELL_VISIBLE=NO
WELCOME_CHECKS_RUN=608
WELCOME_CHECKS_PASS=608
WELCOME_CHECKS_FAIL=0
SCROLLBAR_CONSUMES_CONTENT_WIDTH=NO
AUTH_01_SUCCESS=YES
MANUAL_VISUAL_APPROVAL=PENDING

## Footer-only polish — validated in Unity

Sign In now uses a transparent standard Button instead of ThemeButton, whose hover/focus handlers restored a surface. Primary bold typography, a 6px inline gap, zero border/background/padding and a minimum 44x44 logical hit target preserve the placeholder action. No other Welcome element, provider, routing or shared theme changed.

Local compile: zero errors. Fresh isolated Auth01 regression: 14 PASS, no network calls; Guest and Player continuity retained. Six additional footer assertions per preset cover touch size, transparency, centering, overflow, gap and hover. Expected Welcome total: 656, pending current Unity import/execution. A 608-check run consumed before import is explicitly excluded as old code. Protected hashes: 0/102 modified. Changed-source secret/mojibake scans: zero matches. Shared shell source unchanged; prior 4333 checks retained.

FOOTER_POLISH_SUCCESS=YES
COMMIT=NONE
PUSH=NONE
DEPLOY=NO
AUTH_02_STARTED=NO

## Final footer Unity validation

Final Editor assembly imported at 01:04 on 2026-09-30. A new execution passed 680 checks, zero failed, across all eight presets. The total increased from 656 by three assertions per viewport: exact 6px margin, separately measured visual text gap, and primary-color/bold-font binding. No production source changed during this validation.

FOOTER_GAP=6_PX_LAYOUT_MARGIN
FOOTER_VISUAL_TEXT_GAP=9_PX_ALL_8_PRESETS
SIGN_IN_TOUCH_TARGET=44x44_ALL_8_PRESETS
SIGN_IN_INLINE=YES
SIGN_IN_BACKGROUND_VISIBLE=NO
SIGN_IN_BORDER_VISIBLE=NO
FOOTER_CENTERING=8/8_PASS
FOOTER_OVERFLOW=8/8_PASS
SIGN_IN_REACHABILITY=8/8_PASS
WELCOME_CHECKS_RUN=680
WELCOME_CHECKS_PASS=680
WELCOME_CHECKS_FAIL=0
WELCOME_VISUAL_CONTRACT=PASS
PREVIEW=393x852_NO_SESSION
COMPILER_ERRORS=0
CURRENT_BLOCKING_EXCEPTIONS=0
AUTH_01_SUCCESS=YES
AUTH_02_STARTED=NO
NEXT=FINAL_MANUAL_AUTH_01_REVIEW

The 9px visual gap comprises the 6px layout margin plus 3px of transparent inset from centering the measured Sign In text in its 44px hit area. This remains a compact inline action without a visible surface. Current isolated Guest/Player continuity and double-submit evidence (14 PASS) is retained; no auth implementation changed. Shared shell regression evidence is retained. Nine existing CS0067 unused-event warnings are unrelated to this footer. No Firebase account was created. Validation output contains only geometry and counts, no credentials or user identities. Unity's unrelated licensing log is not part of authentication validation evidence.

Current log classification: one transient IOException concerned concurrent access to Library/Auth01.request while the request file was being written. The Editor subsequently consumed the request and completed all 680 checks. This is validation request transport contention, not a Welcome/Auth runtime exception or a remaining blocker. EXCEPTIONS_OBSERVED=1_TRANSIENT_REQUEST_FILE_IO; CURRENT_BLOCKING_EXCEPTIONS=0. It has not been hidden or counted as a product failure.

## Final manual review follow-up

FOOTER_VISUAL=APPROVED_BY_USER
COMING_SOON_SOURCE=ProductionWelcomeView.Unavailable
COMING_SOON_CLASSIFICATION=EXPECTED_PLACEHOLDER_FEEDBACK
INITIAL_NO_SESSION_COMING_SOON_VISIBLE=NO
SIGN_IN_PLACEHOLDER_BEHAVIOR=COMING_SOON_AFTER_ACTIVATION

Source inspection confirms AuthStatus is constructed with empty text and DisplayStyle.None. Only Unavailable sets Coming Soon; it is bound to Sign In and the four inactive provider rows. Thus this feedback is not exclusive to Sign In. The screenshot alone cannot establish which control was activated. SetState also hides empty status. A fresh preview request remounts a new ProductionWelcomeView at index 1 (393x852), without invoking any action. The request was consumed in the current Editor session; no Sign In or Guest tap was sent during this follow-up. Fresh hidden state is verified through that constructor/mount contract; no separate screenshot inspection was performed.

DEVICE_SIMULATOR_EXCEPTION=UNITY_DEVICE_SIMULATOR_EDITOR_EXCEPTION
UNITY_EDITOR_DEVICE_SIMULATOR_EXCEPTIONS=2_LOGGED_OCCURRENCES
AUTH_01_CAUSED_EXCEPTION=NO_EVIDENCE_IN_STACK
AUTH_01_BLOCKING_EXCEPTIONS=0
COMPILER_ERRORS=0

Both observed NullReferenceException stacks originate in UserInterfaceController.StoreSerializedStates, then DeviceSimulatorMain.SerializeSimulatorState and SimulatorWindow.OnBeforeSerialize, followed by UnityEditor layout serialization/loading. Neither contains AUTH-01 or other Domino product frames. No production change was made to suppress the Editor exception. The earlier transient validation request-file IOException remains documented above; the Editor session is not claimed exception-free.

Product and validator source unchanged during follow-up. Retain 680 PASS, footer centering/overflow/reachability 8/8, and isolated Guest/Player continuity and double-submit evidence. Protected files: zero of 102 modified. No credentials or raw identities were added to the report. All real non-Guest providers remain inactive. Footer styling remains unchanged.

AUTH_01_READY_FOR_CHECKPOINT=YES
COMMIT=NONE
PUSH=NONE
DEPLOY=NO
AUTH_02_STARTED=NO

## Authorized final checkpoint

MANUAL_VISUAL_REVIEW=APPROVED
BASE_SHA=19904fb45044f6b3250edd5dc25e7b4b4ff794e8
COMMIT_MESSAGE=feat: integrate production guest authentication
TARGET=origin/main

This section supersedes earlier chronological pending/NO COMMIT statuses. Product source remains exactly the validated and manually approved version. The checkpoint contains 20 explicitly inventoried files: production Welcome/host/entry and their metadata; Auth router and folder metadata; six existing Guest/Player integration sources; isolated Auth tests and runner; Welcome Editor validator and metadata; this report. No new graphic assets are needed. Existing bundled icon resources are reused; there is no dependency on mock behavior/classes. No backend, Firebase configuration, production shell page, protected work, generated files or FUNCTIONAL-00 report is included.

Retained evidence: Welcome 680 PASS; isolated Auth01 14 PASS; GuestAuth 43 PASS; Player 489 PASS; Matchmaking 75 PASS; Replay 1595 PASS; Social 89 PASS; shell 4333 PASS. No live remote identity test is claimed. All current security and staged-content checks must pass before commit.

Commit identity and remote verification: the commit containing this final report is the AUTH-01 checkpoint and AUTH_02_BASE_SHA. Its exact SHA, push result and verified remote SHA are recorded after publication in the local receipt client/DominoGame/Library/Auth01CheckpointReceipt.json and in the final task response. A report cannot embed its own containing commit SHA without changing that SHA; no second commit or amend is used solely for self-reference. AUTH-02 remains unstarted.
