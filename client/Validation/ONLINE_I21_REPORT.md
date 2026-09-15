# DOMINO ONLINE — PHASE I2.1 REPORT

```text
BRANCH=main
SOURCE_SHA_BEFORE=0e6b3691ac2174d3ce7c00fe7dce9203cfc6d0d2
SOURCE_SHA_AFTER=0e6b3691ac2174d3ce7c00fe7dce9203cfc6d0d2
I2_COMMIT=65a4cb70b880f72f961543dcf035e8f9b8d9cfe0
STARTER_VISUAL_COMMIT=0e6b3691ac2174d3ce7c00fe7dce9203cfc6d0d2

NORMAL SELECTOR
FIRESTORE_CATALOG_VERSION=2 before; 3 after immutable publication
FIRESTORE_PUBLISHED_VERSION=2 before; 3 after
SPRING_CATALOG_VERSION=2 before; 3 after
SPRING_MODE_KEYS=PARTNERS_2V2,DUEL_1V1
API_CATALOG_VERSION=2 before; 3 after
API_MODE_KEYS=PARTNERS_2V2,DUEL_1V1
UNITY_RAW_CATALOG_VERSION=2 before; 3 after
UNITY_RAW_MODE_KEYS=PARTNERS_2V2,DUEL_1V1
UNITY_VALIDATED_CATALOG_VERSION=2 before; 3 after
UNITY_VALIDATED_MODE_KEYS=PARTNERS_2V2,DUEL_1V1
UNITY_CATALOG_VERSION=3
UNITY_MODE_KEYS=PARTNERS_2V2,DUEL_1V1
UNITY_CATALOG_SOURCE=REMOTE
DUEL_1V1_VISIBLE=PASS
FIRST_POINT_DUEL_PREVIOUSLY_DISAPPEARED=NOT_REPRODUCED_IN_CURRENT_SOURCE
ROOT_CAUSE=Current API/cache/validated catalogue contain DUEL. Its normal button still called local StartMatch.
FIX_IMPLEMENTED=Normal DUEL routes to localized Create/Join. Local StartMatch retained for development/tests.

DUEL EXECUTION
MODE_KEY=DUEL_1V1
RULESET=double-nine-duel
RULESET_VERSION=1
EXECUTION_MODES_BEFORE=LOCAL
EXECUTION_MODES_AFTER=LOCAL,ONLINE
ONLINE_ENTRY_VISIBLE=PASS
SHARED_DEVICE_STILL_AVAILABLE_FOR_DEV=YES

TWO UNITY CLIENTS
REAL_TWO_UNITY_CLIENTS=PASS
CLIENT_A_TYPE=Unity Editor (isolated project)
CLIENT_B_TYPE=Windows Standalone Development Build
UID_A_DIFFERENT_UID_B=PASS
CREATE_MATCH=PASS
JOIN_MATCH=PASS
BACKEND_URL_A=http://127.0.0.1:8080
BACKEND_URL_B=http://127.0.0.1:8080
WEBSOCKET_URL_A=ws://127.0.0.1:8080/ws/v1/realtime
WEBSOCKET_URL_B=ws://127.0.0.1:8080/ws/v1/realtime
MATCH_ID=7bad37eb-ee0f-4c73-9dfd-a8b7003fe5ca
JVM_PARTICIPANT=NO

REALTIME
REAL_WEBSOCKET_A_TO_B=PASS
REAL_WEBSOCKET_B_TO_A=PASS
SERVER_AUTHORITATIVE=YES
CLIENT_A_STATE_AUTHORITY=NO
CLIENT_B_STATE_AUTHORITY=NO
PRIVATE_HAND_A=PASS
PRIVATE_HAND_B=PASS
OPPONENT_HAND_LEAK=NO
CLIENT_A_RECEIVED_UPDATES=188
CLIENT_B_RECEIVED_UPDATES=193
CLIENT_A_PLAY_ACTIONS=68
CLIENT_B_PLAY_ACTIONS=63

GAMEPLAY
STARTER_SELECTION=PASS
ONLINE_HIGH_TILE_PRESENTATION=PASS
REAL_TWO_UNITY_ROUND=PASS
REAL_TWO_UNITY_FULL_MATCH=PASS
ROUNDS_COMPLETED=9
FINAL_SCORE=97,184
SCORE_SYNC=PASS

I2 RUNTIME
TURN_TIMER_60S=PASS
REAL_TIMEOUT=PASS
REAL_AUTOPLAY=PASS
REAL_DISCONNECT=PASS
REAL_RECONNECT=PASS
DEADLINE_RESET_ON_RECONNECT=NO
RECONNECT_WINDOW_SECONDS=180

RESYNC
CLIENT_A_LAST_SEQUENCE=547
CLIENT_B_LAST_SEQUENCE=547
SERVER_LAST_SEQUENCE=547
SEQUENCE_CONVERGENCE=PASS
GAP_DETECTION=PASS
RESYNC=PASS

SECURITY
OUT_OF_TURN_REJECTED=PASS
INVALID_ACTION_STATE_UNCHANGED=PASS
SAME_UID_SECOND_SEAT_REJECTED=PASS
DOUBLE_INPUT_SINGLE_TRANSITION=PASS

FIRESTORE
REAL_TWO_UNITY_MATCH_PERSISTED=PASS
HISTORY_AFTER_FINISH=PASS
PARTICIPANTS_PERSISTED=2
HISTORY_PROJECTIONS=2
EVENTS_PERSISTED=547
WALLET_CHANGED=NO
LEDGER_CHANGED=NO
MONETIZATION_CHANGED=NO

REGRESSION
BACKEND_TESTS=435 PASS; 2 optional tests skipped; 437 total
LOCAL_DUEL=PASS (14723 model checks; 200 rounds; 261 Unity UI checks)
STARTER_TABLE_VISUAL=PASS (311 checks; 9 sizes; 3 themes)
PARTNERS_2V2=PASS (251685 checks; 250 differential rounds; 100 golden matches)
I1=PASS (41 client checks including safe entry error mappings)
I2=PASS (real timeout/disconnect/reconnect plus unchanged engine tests)
PLAYER_FOUNDATION_AND_REALTIME_G3=PASS (469 checks)
CATALOG_CACHE=PASS (42 checks)
PORTRAIT=PASS (321 checks; 9 sizes; both seats)
ENTRY_EN_ES=PASS (96 checks; 5 sizes; back/error flow; server-driven high tile)
UNITY_COMPILATION=PASS
UNITY_PLAY_MODE=PASS
WINDOWS_DEVELOPMENT_BUILD=PASS
WINDOWS_VISIBLE_SELECTOR_AND_ENTRY=PASS
CONSOLE_ERRORS=0

LIMITATIONS
WEBSOCKET_MULTI_INSTANCE_DISTRIBUTION=NOT_VALIDATED
MATCHMAKING=NO
SPECTATOR_RUNTIME=NO
REPLAY_UI=NO
H7_STARTED=NO

LOCAL USER FILES
ADS_SETTINGS_USER_CHANGE_PRESERVED=YES
API_SETTINGS_USER_CHANGE_PRESERVED=YES
ADS_SETTINGS_SHA256=C377416E334727264806761518A4B5EDF381A837A927A1C3F3D523A1ACBDB5D6
API_SETTINGS_SHA256=AC6B0ED6D30240BC531B235E43E8B9B55D3B48DCBCCCF365ADEC62D3EE6BCB37

GIT
COMMIT=NONE
PUSH=NONE
STAGED_FILES=NONE
CLOUD_RUN_DEPLOYED=NO
```

## Audit and implementation evidence

The pre-edit authenticated Unity SDK audit is saved in `client/Validation/Generated/i21-catalog-audit.txt`. The running API returned version 2 and both keys. `GameCatalogController.catalog()` returns `GameCatalogService.resolve()` directly, so this is also the Spring resolved snapshot, without a mode-filtering intermediary. Unity's persisted cache and validated `GameCatalogService.Current` also contained both modes. A historical disappearance was not reproducible; no speculative Firestore repair was made.

The confirmed entry issue was `DominoClientController.StartRequested -> StartMatch`, which always started the shared-device local engine. `StartFromMenu` now routes only DUEL to `OnlineEntryView`; the partners route remains local. The public local `StartMatch` API remains unchanged, with the additional Editor menu **Domino > Online > Start local shared-device DUEL (development)**. The existing online Editor controls remain available.

`OnlineEntryView` uses the existing authenticated `OnlineMatchApi` and single realtime connection. Create and Join are single-flight; failures map to allowlisted localization keys, never raw responses. Back cancels pending client work, disposes subscriptions/presentation, and returns to the selector. An already-created backend Match may remain CREATED if its creator leaves; no deletion/expiry/lobby lifecycle was added. These development records require a future administrative retention/cleanup policy.

`OnlineMatchController` reuses `BoardView.GetStarterView()` and actual themed tiles for HIGH_TILE_SELECTION. Choices send `SELECT_STARTER_TILE` intentions. Values come exclusively from the server's private `setup.chosenTiles` after resolution. Candidate availability is server-projected; no local RNG/StarterSelection engine runs online. Resync uses the confirmed snapshot instead of replaying a past selection. The new entry and error strings live in the existing official Unity Localization tables, not a parallel localization system.

## Catalogue publication

`GameCatalogV3Publisher` derives an immutable v3 aggregate from canonical v2 and changes only DUEL's execution list to `[LOCAL, ONLINE]`. Its transaction reads and checks historical v2, existing v3, and the expected published pointer before writing. It creates v3 only if absent and refuses conflicting existing content; it never edits v2. The published aggregate is authoritative (`FirestoreGameCatalogRepository` reads `systemConfig/gameCatalog` and then `gameCatalogs/{version}`); legacy registry/index documents were not rewritten. RuleSet versions, content, scoring, timers, and target 150 are unchanged. The bundled v2 remains a valid offline fallback. Both real Unity clients downloaded v3 from the running backend.

## Real runtime validation and identity isolation

`TwoUnityValidation` is compiled only for Editor/Development and is inactive without an explicit isolated test invocation. It drives the real normal-menu buttons, production API, Firebase SDK, WebSocket service, online client, controller and board. The controlled driver chooses legal intentions from each client's own visible hand; Spring alone resolves legality, turns, RNG, scoring and persisted events. No JVM player and no score fixture were used for the full match.

The Editor and Windows build share company/product persistent-data settings, so default storage cannot be assumed independent. The validation uses named Firebase apps **Domino-I21-A** and **Domino-I21-B**, reusing each app's real persisted guest session. Both obtain real ID tokens with native Firebase SDK; the backend derives UID only from the verified token. Hash comparisons prove different UIDs; a second-seat join with the creator's token is rejected. No default user sign-out, token files, UID spoof, or production identity/configuration change was introduced. Named development sessions are retained for reconnect testing.

At the first two-client attempt, the existing Spring JVM had started at 20:06, before the I2 worker class was compiled at 20:25. It exposed a deadline but did not perform the expected worker transitions. After identifying that exact workspace JVM, it was restarted on the same 8080 endpoint with current code. No timeout-rule fix or security change was needed. The updated backend was left running.

During the successful match, A disconnected for approximately 70 seconds and reconnected using the same UID. B received the live timeout/autoplay/disconnect events; A restored their resulting board/hand/score by authoritative resync after reconnect. Thus A's live timeout-event counter is zero, while B's is one; this is expected for the deliberately disconnected socket. Both subsequently exchanged real WebSocket moves and converged at sequence 547. A deliberately malformed sequence gap was then injected locally into each receiver and recovered using a real authenticated snapshot request; no server state was modified for this gap test.

`TwoUnityMatchInspection` performs read-only, paginated Firestore inspection. It verified all 547 contiguous events, 9 finished rounds, two distinct participants, two histories, final scores 97–184, timeout/autoplay, and disconnect/reconnect timestamps. The timeout deadline equals the preceding server turn start +60s. The persisted reconnect deadline equals disconnect +180s, and the reconnect occurred before it. There is at most one TilePlayed event per causative command. This inspector never simulates a participant or writes data.

Earlier unsuccessful development matches remain as validation artifacts; no destructive cloud cleanup was performed. No Wallet/Ledger endpoint is called by these drivers or inspectors. Ads/API resources are disabled only in the isolated validation copy; the two user source assets retain the exact initial hashes above.

## Windows visual validation

The hidden Windows run produced black ScreenCapture images, so those images are not visual evidence. A separate explicitly visible Development run (`--i21-visual`) successfully rendered and captured the normal selector and online entry at 540x960 (`windows-selector.png`, `windows-entry.png`). Editor captures independently verify the online board, both seats, and high-tile reveal. The visible probe creates no match and requests no ads.

## Evidence and reproduction

Generated evidence is ignored by Git under `client/Validation/Generated/I21/`: `A-result.txt`, `B-result.txt`, per-client public state/counters, safe identity hashes, logs, and EN/ES/high-tile captures. Raw tokens and opponent private hands are not written to coordination files. Backend logs/runtime outputs and the executable are not staged.

The isolated project is `client/Validation/Generated/M2Unity` (historical directory name). Use `TwoUnityValidationEditor.Build` to generate Windows Development, `TwoUnityValidationEditor.Run` for Editor A, and the resulting executable with `--i21-client-b` for B. Start A before B; use a fresh coordination directory or archive previous run files after both processes exit. Both require the current localhost backend and Redis. The user source ApiSettings/AdsSettings must not be changed to prepare that copy. For source UI checks use `OnlineEntryValidation.Run`, `OnlinePlayModeValidation.Run`, `StarterTileValidation.Run`, and `DuelPlayModeValidation.Run` in the isolated project only.

The readonly Firestore inspection command is `gradlew inspectTwoUnityMatch -PmatchId=7bad37eb-ee0f-4c73-9dfd-a8b7003fe5ca`. Normal entry now displays the development Match ID, while I3 will replace this with search -> MATCH_FOUND and retain manual Create/Join as development-only tooling.

## Next

Manual review, then an explicitly authorized checkpoint commit/push. Afterwards: I3 Redis Matchmaking, compatible-mode queues, atomic pairing and MATCH_FOUND. None of I3 is implemented here.
