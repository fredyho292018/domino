# DOMINO ONLINE — PHASE I1.1 REAL UNITY VALIDATION REPORT

```text
I1_1_ACCEPTANCE=PASS
BRANCH=main
SOURCE_SHA=786073aed2d1c45cab87540dbe8b7ff67b895205
SOURCE=uncommitted I1 implementation
FUNCTIONAL_CODE_CHANGED_FOR_I1_1=NO

REAL_UNITY_PARTICIPANT=YES
UNITY_VERSION=6000.0.41f1
UNITY_RUNTIME=real Editor Play Mode in isolated project copy
UNITY_AUTH=native Firebase SDK, existing authenticated identity
SECOND_INDEPENDENT_CLIENT=YES (JVM process, separate temporary Firebase identity)
DIFFERENT_FIREBASE_UIDS=YES (verified against persisted participants)
TOPOLOGY=Unity A + independent JVM B + Spring Boot LOCAL + Redis Docker LOCAL + real Firestore
LOCAL_BACKEND_PORT=18083
REDIS_BIND=127.0.0.1:6379
MOCK_TRANSPORT=NO
MOCK_AUTH_VERIFIER=NO
GAMEPLAY_FIXTURE=NO

MATCH_ID=0118f5fd-4cb3-4897-8fd2-1a7cc9c9f259
MATCH_CREATED=PASS (Unity OnlineMatchClient.CreateAsync -> authenticated REST)
MATCH_JOINED=PASS (independent JVM authenticated REST)
STARTER=EVEN_ODD_GUESS
UNITY_STARTER_INTENT=PASS (actual controller button -> WebSocket intent)
SERVER_STARTER_RESULT=PASS (seat 1 starts)
CLIENT_RANDOM_AUTHORITY=NO

UNITY_PRIVATE_HAND=PASS
UNITY_OPPONENT_BACKS=PASS
PRIVATE_DTO_VISIBILITY=PASS
PRIVATE_HAND_DEALT_EVENTS=PASS (own seat only, other seat redacted)
JVM_PRIVATE_HAND=PASS (own authorized seat 1 snapshot only)
JVM_GRAPHICAL_UI=NOT_APPLICABLE
TOKENS_OR_PRIVATE_HANDS_IN_DIAGNOSTIC_FILES=NO

REAL_PLAY_TILE=PASS
UNITY_PLAY_TILE_COUNT=10
UNITY_VALID_PASS=NOT_NEEDED_IN_FINAL_ROUND
AUTHORITATIVE_EVENT_RECEIVED=PASS
UNITY_REAL_WEBSOCKET_EVENT=PASS
UNITY_MATCH_UPDATE_COUNT=22
SEQUENCE_SYNC=PASS (server REST, Unity WebSocket-applied snapshot, JVM REST/WebSocket)
FINAL_SEQUENCE=68
BOARD_AND_SCORE_EQUALITY=PASS after every test action

INVALID_COMMAND_REJECTED=PASS
INVALID_COMMAND=Unity PASS out of turn
REJECTION=NOT_YOUR_TURN
INVALID_STATE_MUTATION=NO
INVALID_SEQUENCE_INCREMENT=NO
UNITY_REMAINS_SYNCHRONIZED=PASS

REAL_ROUND_RESULT=PASS
FINISH_TYPE=NORMAL
WINNER_SEAT=0 (Unity)
REMAINING_PIPS=0,7
SCORE_AWARDED=17 (7 + 10)
AUTHORITATIVE_SCORE=17,0
RESULT_EQUALITY=PASS (original JSON from all three views, including kind discriminator)
CLIENT_SCORING_AUTHORITY=NO
EXPLICIT_SNAPSHOT_RESYNC=PASS
MATCH_RECONNECT_RUNTIME_ADDED=NO

FIRESTORE_EVENT_PERSISTENCE=PASS
PERSISTED_MATCH=PASS
PERSISTED_PLAYERS=2
PERSISTED_ROUNDS=1
PERSISTED_EVENTS=68 (contiguous 1..68)
MATCH_HISTORY=MATCH_HISTORY_NOT_COMPLETED (one round; target 150 not reached)
WALLET_WRITES=0
MONETIZATION_CHANGED=NO
REAL_AD_REQUESTS=0
TEST_AD_REQUESTS=0

BACKEND_TESTS=414 PASS
OPTIONAL_SKIPPED=2
UNITY_COMPILATION=PASS
UNITY_PLAY_MODE=PASS
I1_1_LIVE_CHECKS=365 PASS
I1_VISUAL_CHECKS=265 PASS preserved (nine sizes, two perspectives; not unnecessarily repeated)
I1_1_LIVE_VISUAL=1080x1920 PASS, playing and round-result screenshots inspected
CONSOLE_ERRORS=0 (final isolated execution)

WEBSOCKET_MULTI_INSTANCE_DISTRIBUTION=NOT_VALIDATED
ANDROID_PHYSICAL_TEST=NOT_RUN
TWO_UNITY_INSTANCES=NOT_RUN (accepted Unity + JVM topology used)
TIMER=NOT_ADDED
AUTOPLAY_RUNTIME=NOT_ADDED
MATCHMAKING=NOT_ADDED
SPECTATORS=NOT_ADDED

ADS_SETTINGS_PRESERVED=YES
ADS_SETTINGS_SHA256=C377416E334727264806761518A4B5EDF381A837A927A1C3F3D523A1ACBDB5D6
API_SETTINGS_PRESERVED=YES
API_SETTINGS_SHA256=AC6B0ED6D30240BC531B235E43E8B9B55D3B48DCBCCCF365ADEC62D3EE6BCB37
ISOLATED_ADS_ENABLED=false
ISOLATED_GENERAL_API_ENABLED=false
ONLINE_VALIDATION_ENDPOINT=explicit local test service configuration; source assets unchanged
TEMPORARY_JVM_FIREBASE_ACCOUNTS=deleted after each invocation
UNITY_IDENTITY=retained; no sign-out or deletion
TEST_BACKEND=stopped gracefully
USER_UNITY_EDITOR=left open and untouched
REDIS=left running unchanged

COMMIT=NONE
PUSH=NONE
DEPLOY=NONE
```

## What ran

The Editor test driver exercises the real `OnlineMatchController` starter, tile selection and placement buttons. Its test-only sequence chooses legal intents from Unity's own hand; this is not gameplay autoplay. Unity uses native Firebase token acquisition, `OnlineMatchApi`, `UnityApiTransport`, `RealtimeConnectionService`, `ClientRealtimeSocket`, `RealtimeProtocol` and `OnlineMatchClient` unchanged. No manually injected snapshots or socket callbacks participate in this run.

The JVM companion chooses its own intents from its independently authorized snapshot. Files coordinate when each test step runs and carry only public comparison data; they do not carry Firebase tokens, private hands or authoritative mutations. REST reads after each action are validation assertions, not production polling. Unity must receive the matching actual WebSocket sequence before each comparison passes.

The isolated project's generic API is disabled to prevent Player bootstrap/monetization calls. Its online test service alone connects to port 18083. The user's source assets were neither staged nor reverted.

## Evidence

Ignored local evidence directory: `client/Validation/Generated/I11/`.

- `unity-result.txt`: final 365 checks, 22 WebSocket updates and final sequence.
- `sequence-evidence.txt`: three-way sequence checks after representative actions, invalid action and resync.
- `public-round-comparison.json`: identical public round result from Unity, server and JVM.
- `firestore.json`: persisted match/players/round/events inspection and different-UID confirmation.
- `unity-playing.png`, `unity-round.png`: actual rendered Unity Play Mode screenshots.
- `unity.log`: compilation/Play Mode log; no token or private-hand logging was added.

Earlier validation-driver attempts are retained in `I11-attempt1`, `I11-attempt2`, `I11-attempt3`. Their issues were confined to the harness: null versus JSON null comparison, a file replacement race, and loss of Jackson's `kind` field when reserializing a typed result in the companion. The final comparison uses the original server JSON. These were not product defects and did not require changing I1 functionality.

Attempt 3 also completed a real blocked round: match `697401a9-c8cd-42db-aace-aca052cbb2bc`, 62 persisted events, seat 1 wins with 26 versus 51 remaining pips and receives 51. The final acceptance result uses the successful final normal round above, not the intermediate failed harness assertion. Earlier incomplete match IDs are `c7a12823-f1a2-4bf0-9811-6526435b49b6` and `04a1de71-d671-4005-9291-c7b8cb595e71`. All test matches are retained; no destructive Firestore cleanup or validation bypass was used. Because creation went through the real API, these are ordinary match records rather than records created through the previous internal validationData shortcut.

## Files added for I1.1

- `client/DominoGame/Assets/_Domino/Scripts/Online/Editor/OnlineUnityNetworkValidation.cs` and `.meta`: isolated, opt-in Editor test driver.
- `server/domino/src/test/kotlin/com/teamfho/domino/online/OnlineUnityValidation.kt`: real authenticated JVM companion and Firestore read-only final inspection.
- `server/domino/build.gradle.kts`: adds only `validateOnlineUnityI11` verification task relative to I1.
- This report.

## Reproduction

Use an isolated Unity project under `client/Validation/Generated/` containing the current I1 assets/scripts/packages and cached Library. Keep that copy's Ads and general API disabled. The source assets must remain untouched. Archive previous `Generated/I11` evidence and create an empty directory before a new run.

1. With existing ADC and loopback Redis available, run `./gradlew validateOnlineUnityI11` from `server/domino`. It starts only its own backend on 18083 and authenticates its temporary companion identity.
2. Launch the isolated Unity Editor with `-batchmode -projectPath <isolated-project> -executeMethod Domino.Online.Editor.OnlineUnityNetworkValidation.Run -logFile <Generated/I11/unity.log>`. Do not add `-nographics`: rendered UI is part of validation. Native Firebase initializes the Editor identity normally.
3. Inspect `unity-result.txt` and `firestore.json`. A successful run closes the test backend and isolated Editor. Do not treat a JVM-only success as Unity success.

No automatic user-identity cleanup is allowed. The companion deletes only the anonymous Firebase account it created. On harness failure, ensure the companion exits through its cleanup path; its run is bounded to 15 minutes.

## Remaining limitations

Cross-instance WebSocket distribution is not validated and must be addressed before TEST/PROD horizontal scaling. This does not block the requested single-LOCAL-server validation. Match history is deliberately not claimed: the test ended after one round.

The existing round-end controls overlay part of a long chain, reducing the result label's contrast against white tiles. The 17-point score and next-round action remain visible; this presentation polish is documented rather than changing functionality during I1.1.

No commit/push authorization is inferred from passing I1.1. Ready for review.
