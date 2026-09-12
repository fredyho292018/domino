# DOMINO PLAYER FOUNDATION V1 — PHASE G2 REPORT

BRANCH=main
SOURCE_SHA_BEFORE=e17b17d5a3f85d09d223c21ecfd22f820cfaa700
SOURCE_SHA_AFTER=bee3fb17532aca7cefa1ed12f38fe127e7eee5bf
G1_COMMIT_SHA=bee3fb17532aca7cefa1ed12f38fe127e7eee5bf
G1_PUSH=YES
WORKTREE_CLEAN_BEFORE_G2=YES

G1 was inspected and its 341 Player checks, 43 Guest Auth checks, configuration, scoring, rules and compilation were rerun before the requested G1 commit/push. G2 starts at that commit. The original implementation was delivered uncommitted; the subsequent checkpoint packages it without functional changes.

## PLAYER UI

PLAYER_UI_IMPLEMENTED=YES
DISPLAY_NAME_VISIBLE=YES
COINS_VISIBLE=YES
ACCOUNT_TYPE_VISIBLE=YES
ONLINE_STATE_VISIBLE=YES
RETRY_ACTION_VISIBLE=YES (only CanRetry)

Open the compact profile button above Play in the main menu. The panel shows confirmed coins, account type, synchronization status, alias input, Cancel, Save and conditional Retry. Generated Guest names become an invitation to choose an alias. No UID is displayed. Missing wallets display `--`, never invented zero. Offline play remains available without an alias or backend.

All new visible strings use the existing Unity Localization table: 17 English/Spanish entries, persisted as source JSON and imported table assets. No parallel localization system.

## ALIAS

ALIAS_EDIT_IMPLEMENTED=YES
BACKEND_ENDPOINT=PUT /api/v1/player/display-name
MIN_LENGTH=3
MAX_LENGTH=16
GLOBAL_UNIQUENESS=NO
DISPLAY_NAME_UNIQUE=NO
RESERVED_NAMES_VALIDATED=YES
UID_ACCEPTED_FROM_CLIENT=NO

Request: `{"displayName":"Fredy92"}`. Response reuses the bootstrap Player/Wallet model. Only JSON strings are accepted. Allowed characters are ASCII letters, digits, underscore and hyphen. Case is preserved; no trimming. Protected exact names, case-insensitively: admin, administrator, moderator, support, teamfho, system. No registry, uniqueness reservation or profanity platform.

Validation returns 400 DISPLAY_NAME_INVALID / DISPLAY_NAME_RESERVED. Unknown fields, including uid and coins, use the existing invalid-request contract. Missing Player returns 409 PLAYER_STATE_CONFLICT; missing/invalid Wallet fails without creating or repairing it. Existing Firebase authentication determines identity.

## FIRESTORE

DISPLAY_NAME_UPDATED=PASS
PLAYER_CREATED_AT_PRESERVED=PASS
WALLET_PRESERVED=PASS
COINS_PRESERVED=PASS

The transaction reads Player and Wallet before writing. It updates only displayName and updatedAt with serverTimestamp. Identical aliases return 200 without a write. Wallet, lifetime counters, createdAt, lastSeenAt, accountType, status and language remain unchanged. Existing transaction retry and sanitized error classification are reused.

## UNITY

PLAYER_SERVICE_ALIAS_UPDATE=PASS
RESPONSE_UID_VALIDATED=PASS
PLAYER_SNAPSHOT_REFRESHED=PASS
OFFLINE_LAST_KNOWN_PLAYER_VISIBLE=PASS
OFFLINE_LAST_KNOWN_COINS_VISIBLE=PASS

Bootstrap, retry and rename share G1's reserved active Task, cancellation/lifetime and main-context event delivery. Rename requires confirmed snapshots and the same authenticated identity. Basic client validation prevents invalid submissions. A successful response must match the authenticated UID and requested alias; unchanged account fields and coins are validated. The confirmed Player is replaced and the immutable Wallet reference preserved. SnapshotChanged notifies presentation; no polling.

HTTP PUT reuses the existing Unity transport, Bearer token provider, one 401 force-refresh, timeout, cancellation, strict JSON codec and safe error whitelist. No new network library. Validation rejection retains the current snapshots and allows correcting the input. Transport failures keep cached snapshots, mark them stale, and expose Retry. UI errors are localized, never raw responses or exception text.

## REAL TEST

OLD_DISPLAY_NAME=Guest-6DH9C7CA
NEW_DISPLAY_NAME=Fredy92
FIRESTORE_ALIAS_PERSISTED=PASS
RESTART_ALIAS_PERSISTED=PASS
OFFLINE_UI=PASS
RETRY_UI=PASS

On 2026-09-12 an isolated Unity editor reused the existing Firebase Guest, bootstrapped against the local backend and invoked the real profile's Save button with Fredy92. No Guest was created. Before/after authenticated Firestore inspection verified every Player field except displayName/updatedAt and all Wallet fields unchanged, including coins=0. Four read-only inspection requests; the actual rename used the production backend endpoint, not a manual Firestore write.

The test stopped its own backend, issued an explicit refresh through the existing internal validation seam, verified cached alias/coins and disabled Save, restarted the backend and invoked the actual Retry button in the same Play session. A second isolated Unity launch confirmed bootstrap and UI still showed Fredy92. Button actions were exercised automatically, not by a human touch on a device. The user's open editor was not closed. The backend was restored to its original OFF state.

Ignored local evidence under `Validation/Generated/G2Real`: result-1.txt (UI), result-2.txt (real save/offline/retry), result-3.txt (restart), firestore-validation.txt, profile-es.png. Safe comparison artifacts include the UID but no tokens. Editor harness is explicit batch-only; its file signals are test coordination, not production polling.

## TESTS

BACKEND_TESTS=PASS (164)
BACKEND_BUILD=PASS
UNITY_GUEST_AUTH=PASS (43 checks)
UNITY_PLAYER_FOUNDATION=PASS (389 checks, including G1 and G2)
UNITY_RETRY=PASS
UNITY_G2_UI_ALIAS=PASS
PORTRAIT=PASS (139,406 checks; nine sizes and complete round)
UNITY_COMPILATION=PASS
CONSOLE_ERRORS=0 (G2 UI, real save/retry, restart and portrait)

Configuration: 82 checks. Scoring: 32,679 checks with unchanged traces. Rules/geometry: 2,981,089 checks and 1,000 games. Nine profile resolutions tested: 1080x1920, 1080x2160, 1080x2340, 1080x2400, 1080x2520, 1170x2532, 1284x2778, 1600x2560 and 1536x2048. Input and buttons remain inside safe bounds; translated text height is checked. The Spanish rendered profile was visually inspected. Source compilation includes the existing three serialized-field warnings, not errors.

## EXTERNAL

ADS_IMPLEMENTED=NO
MULTIPLAYER_IMPLEMENTED=NO
CLOUD_RUN_DEPLOYED=NO

## FILES CREATED

- Assets/_Domino/Scripts/UI/PlayerProfileView.cs (+ meta)
- Assets/_Domino/Scripts/Player/DisplayNameRules.cs (+ meta)
- Assets/_Domino/Scripts/Client/Editor/PlayerProfileValidation.cs (+ meta)
- client/Validation/PlayerAliasTests.cs
- client/Validation/PLAYER_FOUNDATION_PHASE_G2.md
- server/domino/src/main/kotlin/com/teamfho/domino/player/PlayerDisplayNameService.kt
- server/domino/src/test/kotlin/com/teamfho/domino/player/PlayerDisplayNameTests.kt

## FILES MODIFIED

- Assets/_Domino/Scripts/UI/StartMenuView.cs
- Assets/_Domino/Scripts/Player/PlayerService.cs
- Assets/_Domino/Scripts/Infrastructure/Api/{IDominoApiClient,DominoApiClient,UnityApiTransport,UnityApiJsonCodec}.cs
- Assets/_Domino/Scripts/Client/Editor/PlayerFoundationValidation.cs (transport test adapter)
- Assets/_Domino/Editor/Localization/Translations.json
- Assets/_Domino/Localization/Tables/Domino UI Shared Data.asset, Domino UI_en.asset, Domino UI_es.asset
- client/Validation/PlayerFoundationClientTests.cs and RunPlayerFoundationClientTests.ps1
- server/domino/src/main/kotlin/com/teamfho/domino/player/{PlayerController,PlayerFoundationRepository,FirestorePlayerFoundationRepository}.kt
- server/domino/src/main/kotlin/com/teamfho/domino/common/{ApiErrorResponse,ApiExceptionHandler}.kt
- server/domino/src/test/kotlin/com/teamfho/domino/player/FakePlayerFoundationConfiguration.kt

Assets paths above are relative to client/DominoGame.

## KNOWN LIMITATIONS

- Availability reflects the last request, not instant server monitoring. Backend shutdown becomes visible on the next failed operation. No new polling was added.
- Cached Player/Wallet snapshots are in memory for the current session. A cold start offline displays no fabricated Player/coins; a successful bootstrap restores persisted data.
- Physical iOS/Android keyboard and touch behavior still require device QA. Basic keyboard visibility moves the panel upward and restores it on dismissal. Editor tests do not prove physical keyboard behavior.
- No alias uniqueness or rate limiting beyond single-flight; those policies are deliberately deferred.
- Source ApiSettings remains disabled as received. Real testing enabled only the ignored isolated copy with LOCAL/http://127.0.0.1:8080. Enable the existing local settings when testing the source editor against a running backend.

## GIT

G2 checkpoint commit message: `feat: add player profile and editable alias`.
Target: `origin/main`. The checkpoint commit containing this document identifies the final G2 source; the post-push report records its SHA and clean-worktree result.

## FINAL CHECKPOINT VALIDATION

PLAYER_UI=PASS
ALIAS_EDIT=PASS
ALIAS_PERSISTENCE=PASS
RESTART_PERSISTENCE=PASS
WALLET_PRESERVED=PASS
COINS_PRESERVED=PASS
OFFLINE_LAST_KNOWN_DATA=PASS
RETRY_UI=PASS
PHYSICAL_KEYBOARD_VALIDATION=PENDING

The checkpoint runs only local/fake automated suites. Real alias Fredy92, coins=0 and persistence are documented from the preceding G2 validation; no real-data tests, Firebase changes, IAM updates or deployments are repeated here. Backend Gradle test/build use unchanged, up-to-date validated outputs (164 tests). Guest Auth, Player Foundation including G1/G2, rules, compilation and isolated Unity profile/portrait suites are rerun. Logs are ignored files `Validation/Generated/g2-checkpoint-ui.log` and `g2-checkpoint-portrait.log`.

The commit retains ApiSettings enabled=false, environment=LOCAL, baseUrl=http://127.0.0.1:8080, timeoutSeconds=15. Source and pending-file review excludes generated outputs, credentials and keys. No functional contract or connection-state label is changed by this checkpoint.

## RECOMMENDED NEXT TASK

Phase G2.1: correct connection-state semantics and investigate repetitive AUTH_TOKEN_MISSING traffic, without polling or WebSocket. "En línea" currently reflects the last successful synchronization, not a persistent realtime connection; that wording and the repetitive traffic were intentionally not investigated or changed in this checkpoint. Then Phase G3: Realtime WebSocket Foundation. Physical-device keyboard/touch QA remains pending. No Ads or multiplayer were added.
