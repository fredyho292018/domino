# AUTH-LOGOUT-01 — Production logout flow

BASE_SHA=823e5c679388ac783bbba5ecb9acacd3dc8e6146

## Audit and policy

- Firebase: `FirebaseSdkClient.InitializeApp/Auth` uses DefaultInstance, `CurrentUser` supplies the SDK identity. SignOut is local; no delete/unlink operation. Retired SDK clients reject future access.
- Auth: `FirebaseAuthService` stores Current, initialization and Email operation tasks. `ProductionAuthRouter.StopAsync` cancels its lifetime, suppresses late routing and drains its current operation before SignOut.
- Player: `PlayerService.Dispose` cancels its requests and clears Player, Wallet, Entitlements, owner and events. Late completions check cancellation/disposal before applying snapshots.
- Tokens/HTTP: `FirebaseIdTokens` reads tokens per request and verifies identity before/after acquisition. `UnityApiTransport` uses a per-request Authorization header. New `SessionApiTransport` binds all composition-root APIs to the old session lifetime, aborting in-flight requests and preventing reuse after account switch. Firebase internal persistence is left to SDK SignOut.
- WebSocket: `RealtimeConnectionService.Dispose` sets stopped, cancels active/lifetime, clears activity. Start and foreground reconnect refuse stopped instances. ApplicationServices creates a new service generation after logout.
- Matchmaking: `MatchmakingView.Client.CancelAsync` uses existing DELETE matchmaking/queue, waits a pending join, and accepts only IDLE without an assignment. The production gate also GETs remote matchmaking/queue for a bootstrapped Player and cancels QUEUED/RESERVED using that same existing endpoint. MATCHED, unknown states, cancellation failure and network failure block logout before teardown. No invented leave or forfeit operation.
- Games: any OnlineMatchController, non-null local DominoClientController.Session or development OnlineEntryView blocks logout. Close/finish through existing game UI first. Current production root shell does not launch those legacy gameplay screens, but the global gate still checks them, including inactive objects. No separate pending-challenge implementation was found in the inspected Social client.
- Social: SocialClient captures its owner, checks SessionValid around API responses and owns presence subscriptions; SocialView owns cancellation/disposal. Session teardown removes private Social views. Friendships/follows/blocks are preserved remotely.
- History/Replay: ReplayClient is stateless GET access; HistoryReplayView owns history/manifest/timeline and a cancellation source. Teardown removes its view; session-bound HTTP rejects stale completions. No saved private History cache was found in that runtime path.
- Membership/rewards: Player entitlements clear on disposal; composition-root reward/monetization services and their lifetime are discarded and rebuilt. Purchase records are not deleted.
- FCM: no FirebaseMessaging/device-token association was found in inspected runtime client/server source; logout action NONE.
- Backend: Firebase bearer authentication; SecurityConfiguration disables framework logout. No server logout endpoint is required or added. AUTH-07 is not implemented; preparation delegate provides a future integration point.
- Persistence: language (DominoLocalization) and tile style (TileStyles) are device preferences, PRESERVE_ACROSS_USERS. game-catalog-v1.json is catalog configuration, NOT_AUTH_RELATED, preserved. Firebase session storage is SDK-owned. No PlayerPrefs.DeleteAll.
- UI: ProductionAuthHost rebinds after ApplicationServices replaces the session generation. The old retained AppShell and its navigation are discarded; normal router RestoreAsync opens Welcome without creating a Guest. Static demo providers remain unchanged.

## Operation order

Confirm intended identity → mark LoggingOut and replace interactive content → guard gameplay → cancel local and recovered remote queue → recheck identity/game → stop/drain AuthRouter → dispose session services/cancel HTTP and WS/remove private views/clear Player → Firebase SignOut → rebuild services → existing router RestoreAsync → Welcome. Menu rows never call Firebase directly. Unverified production exit enters the same coordinator; isolated legacy cancellation contract tests remain available.

Network cleanup is mandatory when queue absence needs confirmation: fail closed with safe fixed text, retain authentication, permit retry. No server data is deleted. The UI blocks duplicate submission and distinguishes Guest loss-of-access warning from registered confirmation.

## Validation status

Implementation compiles in local Unity-reference compilation. Dedicated isolated tests: 22 PASS. Auth-01 14 PASS, Auth-02A 50 PASS, Guest 43 PASS, matchmaking 75 PASS, Replay 1595 PASS, Social 89 PASS. Player/realtime expanded regression: 490 PASS, including stopped-connection reconnect suppression. Fresh Unity Logout geometry is pending import; existing suites produced new artifacts but cannot certify the final source until import is confirmed. Do not treat prior Unity artifacts as current.

REAL_GUEST_LOGOUT_EXECUTED=NO
ACCOUNTS_CREATED=0
EMAILS_SENT=0
BACKEND_CHANGED=NO
SCHEMA_CHANGED=NO
FIREBASE_CONFIG_CHANGED=NO
COMMIT=NONE
PUSH=NONE
DEPLOY=NO
AUTH_02B_STARTED=NO
AUTH_03_STARTED=NO
AUTH_LOGOUT_01_SUCCESS=PENDING_UNITY_VALIDATION

## Current gate result

```text
LOGOUT_ORCHESTRATOR=ProductionLogoutService
LOGOUT_ORCHESTRATOR_COUNT=1
LOGOUT_ENTRY=Menu / APP / Sign Out
GUEST_WARNING=IMPLEMENTED_TEXTUAL_LOSS_OF_ACCESS
REGISTERED_CONFIRMATION=IMPLEMENTED_NO_GUEST_WARNING
ACTIVE_GAME_LOGOUT_POLICY=BLOCK_UNTIL_CLOSED_OR_FINISHED
MATCHMAKING_LOGOUT_ACTION=EXISTING_CANCEL_REQUIRE_NOT_QUEUED
WEBSOCKET_LOGOUT_ACTION=DISPOSE_CANCEL_RECONNECT
FCM_LOGOUT_ACTION=NONE
SERVER_LOGOUT_REQUIRED=NO
PLAYER_SESSION_CLEAR=PASS_ISOLATED
AUTH_TOKEN_STATE_CLEAR=IMPLEMENTED_SDK_SIGNOUT_AND_SESSION_TRANSPORT_INVALIDATION
FIREBASE_SIGN_OUT=FAKE_VALIDATED_REAL_NOT_EXECUTED
AFTER_LOGOUT_AUTH_STATE=NO_SESSION_ISOLATED
AFTER_LOGOUT_ROUTE=WELCOME_ISOLATED
STALE_APP_SHELL_VISIBLE_AFTER_LOGOUT=PENDING_UNITY
PLAYERPREFS_DELETE_ALL=NO
USER_SCOPED_CACHE_CLEAR=IMPLEMENTED
SETTINGS_PRESERVED=YES_NO_PREFERENCE_WRITES
NETWORK_CLEANUP_FAILURE_POLICY=FAIL_CLOSED_BEFORE_SIGNOUT
LOGOUT_DOUBLE_SUBMIT_PREVENTED=PASS
LOGOUT_WHEN_NO_SESSION=NO_OP_SAFE
BOOTSTRAP_AFTER_LOGOUT_RACE_PREVENTED=PASS_ISOLATED
GUEST_ISOLATED_TEST=PASS
REGISTERED_ISOLATED_TEST=PASS
UNVERIFIED_ISOLATED_TEST=PASS
NO_SESSION_ISOLATED_TEST=PASS
DOUBLE_TAP_TEST=PASS
IN_FLIGHT_BOOTSTRAP_TEST=PASS
ACCOUNT_SWITCH_TEST=PASS
LOGOUT_UI_RESPONSIVE=PENDING_UNITY_IMPORT
AUTH_01_REGRESSION=14_PASS_LOCAL
AUTH_02A_REGRESSION=50_PASS_LOCAL
GUEST_REGRESSION=43_PASS_LOCAL
PLAYER_REALTIME_REGRESSION=490_PASS_LOCAL
SHELL_REGRESSION=PENDING_FINAL_IMPORTED_SOURCE
MATCHMAKING_REGRESSION=75_PASS_LOCAL
HISTORY_REPLAY_REGRESSION=1595_PASS_LOCAL
SOCIAL_REGRESSION=89_PASS_LOCAL
LOGOUT_TESTS=22_PASS_LOCAL
REAL_GUEST_LOGOUT_EXECUTED=NO
COMPILER_ERRORS=0_LOCAL_UNITY_REFERENCE_BUILD
CURRENT_BLOCKING_EXCEPTIONS=NOT_CONFIRMED_IN_UNITY
PREEXISTING_PROTECTED_FILES_MODIFIED=0/102
SECRET_SCAN=PASS
AUTH_LOGOUT_01_SUCCESS=PENDING_UNITY_VALIDATION
NEXT=UNITY_REFRESH_AND_ISOLATED_VALIDATION_THEN_MANUAL_LOGOUT_UI_REVIEW
```

No new logging of identity, credentials, email or exceptions was introduced. Secret-pattern scan covers the scoped source/test changes; it does not certify unrelated historical logs. Compilation uses installed Unity references; it is not a substitute for current Editor compilation. Windows control timed out on activation and on fresh state capture. The requested manual Refresh remains pending; do not run real logout or resume the real registration test.
