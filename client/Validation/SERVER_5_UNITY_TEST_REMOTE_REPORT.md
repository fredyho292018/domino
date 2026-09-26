# CUBAN DOMINO CLUB — SERVER-5 UNITY → TEST

Status: PASS — bounded Unity Editor TEST end-to-end validation completed after the existing license became available.
Branch: main. Source: 20cabe0b400498db3575f4f5c7bf252a15a0e53e.

## Configuration audit and change

Current REST configuration: Resources/ApiSettings.asset -> DominoApiSettings -> DominoApiConfiguration.
Current environment was LOCAL, URL http://127.0.0.1:8080. The pre-existing asset is unchanged.
RealtimeConfiguration derives /ws/v1/realtime and ws/wss from the validated REST configuration.
No independent WebSocket setting or second hostname was introduced.

DominoApiSettings now recognizes the TEST preset https://domino-api-test.teamfho.com.
Editor menu Domino / Environment / TEST server selects TEST via a project-scoped EditorPrefs key;
Configured asset (LOCAL) clears the override. Select while outside Play Mode; restart Play Mode to apply.
Player builds use the existing serialized environment field: TEST selects the same preset.
No PROD endpoint was added. The project-scoped Editor TEST override is now selected. No serialized asset was changed and no player build was generated.
Both google-services.json and google-services-desktop.json identify teamfho-domino.

## Bounded validation executed

Editor-only Server5Validation.RunEditMode contains three configuration checks.
Server5Validation.RunReal requires the existing DOMINO_REAL_FIRESTORE_TESTS=true guard, checks the
Firebase project and enters the actual client scene. It uses the normal Firebase SDK and client flow.
It checks bootstrap UID parity internally without printing UID/token, then reads own profile, social
summary, friends, following, history and queue status. It does not join matchmaking or create matches.
It requests a supported Firebase token refresh, instruments the existing RealtimeConnectionService
through an IRealtimeSocket wrapper, observes three PONG cycles, aborts only that client socket and
checks new authentication/global activity after normal reconnect. No second heartbeat loop is added.
The application's initial realtime instance is disposed before the instrumented one starts.
No raw API bodies, tokens or exception messages are written by the harness.

Full account logout is intentionally NOT_RUN to preserve the current anonymous account; connection
lifecycle disposal is a separate check and must not be misreported as Firebase logout.
Presence subscriptions and manual History/Social UI inspection remain pending.
The harness compiled in Unity 6000.0.41f1. The real client scene completed the bounded checks successfully.

## Actual validation evidence

- Player foundation/realtime isolated client checks: 469 PASS; REAL_NETWORK_CALLS=0.
- Social isolated client tests: 89 PASS.
- Unity 6000.0.41f1 initial sandbox launch could not access its database/license client.
- Retry outside sandbox reused the installed license client, but exited with:
  `No valid Unity Editor license found. Please activate your license.`
- No license activation attempted. User asked to open Unity Hub and confirm existing license.
- Subsequent retry after the user confirmed the existing Personal license: Unity compilation succeeded; all 3 Edit Mode configuration checks PASS.
- Unity Play Mode: PASS. Bootstrap UID parity PASS; profile, social-summary, friends, following, history and matchmaking queue each returned HTTP 200. Supported SDK token refresh PASS.
- Authenticated WSS PASS; 3 PONG heartbeat cycles; controlled socket interruption followed by fresh authentication and global activity resynchronization PASS. Connection disposal PASS.
- Console errors during the Play Mode validation: 0. Firebase warning: Database URL not set (Realtime Database is not configured; the tested Auth/backend flows passed). Headless Editor startup/shutdown diagnostics are outside this application error count.
- No physical Android/iOS validation or new build performed.
- 224 client text assets scanned for obvious private-key/service-account/cursor secret indicators: none.
  This is a scoped indicator scan, not a complete binary artifact audit; no build exists for this task.
- All 97 pre-existing user file hashes match the initial snapshot.

## Changed-file classification

SERVER_5_INTENTIONAL:
- client/DominoGame/Assets/_Domino/Scripts/Infrastructure/Api/DominoApiSettings.cs
- client/DominoGame/Assets/_Domino/Scripts/Client/Editor/Server5Validation.cs
- client/DominoGame/Assets/_Domino/Scripts/Client/Editor/Server5Validation.cs.meta
- client/Validation/SERVER_5_UNITY_TEST_REMOTE_REPORT.md

PREEXISTING_USER_FILE: 97 paths recorded in the initial temporary hash manifest; all unchanged,
including AdsSettings.asset, ApiSettings.asset, localization settings, Firebase client files and Android work.
GENERATED_VALIDATION: ignored client/Validation/Generated/SERVER5 logs and existing client-test build outputs.
UNEXPECTED: none as of final status audit. No backend or infrastructure file modified.

## Machine-readable report

```text
BRANCH=main
SOURCE_SHA=20cabe0b400498db3575f4f5c7bf252a15a0e53e
UNITY_TEST_ENDPOINT_CONFIGURED=YES
REST_BASE_URL=https://domino-api-test.teamfho.com
WS_BASE_URL=wss://domino-api-test.teamfho.com/ws/v1/realtime
UNITY_FIREBASE_PROJECT_ID=teamfho-domino
AUTHENTICATED_REST=PASS
AUTHENTICATED_WSS=PASS
WEBSOCKET_TRANSPORT=PASS
FIREBASE_WS_AUTH=PASS
HEARTBEAT_OVER_WSS=PASS_3_CYCLES
WSS_RECONNECT=PASS
WSS_RESYNC=PASS
PRESENCE_TEST=NOT_RUN
MATCHMAKING_CONNECTIVITY=PASS_READ_ONLY_QUEUE_STATUS
SOCIAL_REMOTE_CONNECTIVITY=PASS_API_UI_NOT_MANUALLY_VERIFIED
HISTORY_REMOTE_CONNECTIVITY=PASS_API_UI_NOT_MANUALLY_VERIFIED
LOGOUT_REMOTE_BEHAVIOR=NOT_RUN_ACCOUNT_PRESERVED
TOKEN_REFRESH_REMOTE=PASS
CLOUDFLARE_CLIENT_SPECIAL_AUTH_REQUIRED=NO
LOCAL_ENVIRONMENT_PRESERVED=YES
PROD_ENDPOINT_CONFIGURED=NO
UNITY_EDITMODE=3_PASS
UNITY_PLAYMODE=PASS
CONSOLE_ERRORS=0_DURING_PLAYMODE_VALIDATION
PHYSICAL_ANDROID_VALIDATION=NOT_RUN
PHYSICAL_IOS_VALIDATION=NOT_RUN
CLIENT_SERVER_SECRETS=0_INDICATORS_IN_SCANNED_TEXT_ASSETS
BUILT_ASSET_SECRET_AUDIT=NOT_RUN_NO_BUILD
REAL_TEST_FIRESTORE_CALLS=NOT_COUNTED_NORMAL_BOUNDED_APPLICATION_USAGE
REAL_PRODUCTION_FIRESTORE_CALLS=0
BOT_SWARM_STARTED=NO
SERVER_CONFIGURATION_CHANGED=NO
CLOUDFLARE_CONFIGURATION_CHANGED=NO
DNS_CHANGES=0
FILES_MODIFIED=4_INTENTIONAL
PREEXISTING_USER_FILES_PRESERVED=YES_97_OF_97
UNEXPECTED_FILES=0
COMMIT=NONE
PUSH=NONE
SERVER_5_SUCCESS=YES_DESKTOP_PROTOCOL_GATES
NEXT=SERVER-5 REVIEW/CHECKPOINT
```
