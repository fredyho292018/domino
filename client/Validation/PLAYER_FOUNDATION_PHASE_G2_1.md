# DOMINO PLAYER FOUNDATION V1 — PHASE G2.1 REPORT

BRANCH=main
SOURCE_SHA_BEFORE=84ae747036ecf2e3265f580a624a22d786ce0952
SOURCE_SHA_AFTER=84ae747036ecf2e3265f580a624a22d786ce0952
WORKTREE_CLEAN_BEFORE=YES
WORKTREE_CLEAN_AFTER=NO (G2.1 changes for review)

## AUTH TOKEN MISSING DIAGNOSIS

REPETITIVE_AUTH_TOKEN_MISSING_REPRODUCED=YES
INTERVAL_APPROX_SECONDS=1.002 against Spring Boot
TARGET_HTTP_METHOD=GET
TARGET_PATH=/json
AUTH_TOKEN_MISSING_TARGET_PATH=/json
QUERY_STRING=NONE
REMOTE_ADDRESS=127.0.0.1
USER_AGENT=ABSENT
ORIGIN=ABSENT
REFERER=ABSENT
AUTH_HEADER_PRESENT=false
SOURCE_IDENTIFIED=YES
SOURCE=LuaTools.exe 1.3.1.0, PID 27756
SOURCE_PATH=C:/Users/fredy/AppData/Local/LuaTools/current/LuaTools.exe

ROOT_CAUSE: An external local LuaTools process repeatedly probes port 8080 with GET /json without authentication. That port is also used by Domino. /json is not an approved public Domino endpoint. The existing deny-by-default Spring Security policy correctly returns 401 AUTH_TOKEN_MISSING to an unauthenticated caller. There is no reason to add /json to public routes or weaken /api protection.

Evidence collected before source-code edits on 2026-09-12:

1. With the backend initially stopped, a temporary loopback-only HttpListener captured eight requests. While each connection remained open, Get-NetTCPConnection associated the caller's ephemeral port with PID 27756. Every request was GET /json, had no query, User-Agent, Origin, Referer or Authorization header, and came from LuaTools. The diagnostic listener returned 503 and was then closed; its processing adds roughly 0.15 seconds to the measured interval.
2. Executable metadata and the running process confirmed LuaTools 1.3.1.0 at the path above. No inference from a User-Agent was needed.
3. The unchanged Spring Boot backend was started with temporary CLI-only Tomcat access logging of timestamp, method, path, remote address, User-Agent and response status. It reproduced GET /json -> 401 every approximately 1.002 seconds and corresponding AUTH_TOKEN_MISSING warnings. Thirty-two such warnings were recorded before stopping LuaTools.
4. Stopping the verified LuaTools process removed the requests. The installation, registry and startup settings were not changed. The source is proven at process level; no speculative claim is made about the internal LuaTools feature responsible for the probe.

The initial backend launch immediately after the diagnostic listener encountered a transient port-in-use error; a subsequent port inspection found no listener and the next launch succeeded. No alternative port or project configuration was introduced.

## FIX

PROJECT_CODE_CHANGE_REQUIRED=NO for the repetitive requests; YES for profile labels
SECURITY_WEAKENED=NO
POLLING_ADDED=NO
REPETITIVE_REQUEST_REMOVED=PASS for the validated local session
IDLE_30_SECONDS_AUTH_TOKEN_MISSING_COUNT=0

Production client audit: ApplicationServices starts Player initialization once; PlayerService retains G1's single active task and explicit RetryAsync. UnityApiTransport's while loop waits for an already-started request; it does not start another request each iteration. Token refresh permits only the existing single retry after 401. No production InvokeRepeating, periodic HTTP timer or one-second REST loop was found. Test-only editor updates coordinate validation and are not player-build polling.

Backend audit: no scheduled self-call, WebClient, RestTemplate, HttpClient timer or protected health polling in production sources. GET /actuator/health remains the only explicitly public route. Backend source and security configuration are unchanged.

Temporary request capture was deleted after diagnosis. The diagnostic backend was stopped, returning the backend to its original OFF state. Access logging existed only as command-line overrides on that process. Ignored evidence remains under Validation/Generated/G21; no credential values, cookies or request bodies were captured. Future backend launches do not inherit the diagnostic logging.

## CONNECTION SEMANTICS

OLD_LABEL=En línea
NEW_SYNCED_LABEL_ES=Sincronizado
NEW_SYNCED_LABEL_EN=Synced
OFFLINE_LABEL_ES=Sin conexión
OFFLINE_LABEL_EN=Offline
NOT_SYNCED_LABEL=Sin sincronizar / Not synced
SYNCING_LABEL=Sincronizando… / Syncing…
REALTIME_CONNECTION_CLAIMED=NO

PlayerSyncPresentation maps service results to localization keys without affecting PlayerService or its retry rules. Configuration failure says synchronization is unavailable; authentication/authorization failures request authentication; HTTP 5xx indicates service unavailable; other failures indicate sync failed. Transport and timeout indicate offline. Any failed state with confirmed snapshots includes the localized last-known-data qualifier.

BackendAvailability remains an observation from the last request, not a liveness monitor. Existing G1 behavior treats received HTTP rejections as evidence of reachability; these are not presented as a persistent connection or successful player synchronization. The profile's SYNCED label depends on synchronization state, not the availability enum. No automatic refresh, health check, foreground retry or presence is added. Legacy unused translation keys are retained for compatibility; the profile no longer uses profile.online or profile.connecting.

## PLAYER UI

DISPLAY_NAME_PRESERVED=PASS
COINS_PRESERVED=PASS
STALE_DATA_HANDLED=PASS
RETRY_UI_PRESERVED=PASS

## VALIDATION MATRIX

BACKEND_ONLY_30S=PASS (54.7 seconds; zero new warnings)
REQUESTS_WITH_ALL_CLIENTS_CLOSED=NO after stopping LuaTools
REQUESTS_WITH_BROWSER_ONLY=NO (71.2 seconds; blank browser; Unity closed)
REQUESTS_WITH_UNITY_EDITOR_ONLY=NO (31 seconds)
REQUESTS_WITH_UNITY_PLAY=NO repetitive requests
UNITY_API_DISABLED_30S=PASS (31 seconds; zero HTTP requests)
UNITY_API_ENABLED_BOOTSTRAP=PASS (existing Firebase identity; normal REST bootstrap)
UNITY_IDLE_AFTER_BOOTSTRAP_30S=PASS (31 seconds; zero HTTP requests)
INTENTIONAL_NO_TOKEN_401=PASS (one request, one warning)

The existing source editor and browser windows were closed normally for isolation, then idle editor and Play tests ran in an ignored project copy. Remaining browser background processes were closed only after their windows were gone. No Unity force-close or unsaved scene discard was performed.

Unity serialized an existing inspector value Enabled=true on normal exit, although the source asset was false and clean at the initial Git precheck. The first disabled-API fixture therefore correctly rejected that configuration; it was explicitly set to false in the isolated copy and rerun successfully. Source ApiSettings was restored to the initial false value. Enabled=true was used only in the successful isolated enabled test. No alias or coins were edited; normal bootstrap may perform its existing throttled lastSeenAt update.

Timestamped Tomcat access rows were compared to the start/end of each 31-second Unity idle window. All three windows contained zero HTTP requests. Logs distinguished Unity's normal POST bootstrap and User-Agent from LuaTools' GET /json. A separate intentional POST without a token returned 401 AUTH_TOKEN_MISSING and raised the warning total from 32 to 33. No warning suppression or route exception was used.

## TESTS

BACKEND_TESTS=PASS (164; unchanged Gradle inputs, test task up-to-date)
UNITY_TESTS=PASS (400 Player checks including G1/G2 and connection mapping; 43 Guest Auth)
PORTRAIT_TESTS=PASS (nine sizes, all four local seats and complete round)
UNITY_COMPILATION=PASS
CONSOLE_ERRORS=0

Configuration: 82 checks. Scoring: 32,679 checks with unchanged traces. Rules/geometry: 2,981,089 checks, 1,000 games. New tests cover every mapping, ES/EN initial/syncing/synced labels, rendered text after locale changes, stale offline labels, preserved snapshots and the existing Retry button. Physical keyboard validation remains pending from G2. The expanded locale test initially switched locales before asynchronous table preloading settled. The test now waits for each localization initialization and holds its fake request pending while checking SYNCING in both languages. Final profile UI validation passes with zero Console errors; production localization code was not changed.

## EXTERNAL

WEBSOCKET_IMPLEMENTED=NO
REDIS_IMPLEMENTED=NO
ADS_IMPLEMENTED=NO
CLOUD_RUN_DEPLOYED=NO

## FILES CREATED

- Assets/_Domino/Scripts/UI/PlayerSyncPresentation.cs (+ meta)
- Assets/_Domino/Scripts/Client/Editor/PlayerConnectionValidation.cs (+ meta)
- client/Validation/PlayerConnectionTests.cs
- client/Validation/PLAYER_FOUNDATION_PHASE_G2_1.md

## FILES MODIFIED

- Assets/_Domino/Scripts/UI/PlayerProfileView.cs
- Assets/_Domino/Scripts/Client/Editor/PlayerProfileValidation.cs
- Assets/_Domino/Editor/Localization/Translations.json
- Assets/_Domino/Localization/Tables/Domino UI Shared Data.asset, Domino UI_en.asset, Domino UI_es.asset
- client/Validation/PlayerFoundationClientTests.cs
- client/Validation/RunPlayerFoundationClientTests.ps1

Assets paths are relative to client/DominoGame. No backend source, PlayerService, wallet, security or gameplay modifications.

## KNOWN LIMITATIONS

- LuaTools was stopped, not uninstalled or permanently reconfigured. Relaunching it can restore the external /json probe. Keep it stopped while using Domino on port 8080, or separately configure that tool through its supported settings. No unsupported modification of a third-party installation was attempted.
- REST synchronization is not realtime presence. Stale snapshots remain in memory; there is no automatic network monitoring.
- Physical-device keyboard QA remains pending.

## GIT

COMMIT=NONE
PUSH=NONE

## RECOMMENDED NEXT TASK

Review G2.1, then separately scope G3: Realtime WebSocket Foundation, Firebase authentication, heartbeat, reconnect, presence, global activity counters and local Redis Docker. No G3 component is implemented here.
