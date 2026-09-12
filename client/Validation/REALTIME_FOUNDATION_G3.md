# DOMINO REALTIME FOUNDATION — G3

G2_1_COMMIT_SHA=7641fcf04bf8f5faac060f359eb8e9ff2f0ba358
WORKTREE_CLEAN_BEFORE_G3=YES
G2_1_PUSH=SUCCESS (origin/main)
IMPLEMENTATION_HANDOFF_COMMIT=NONE (before checkpoint)
IMPLEMENTATION_HANDOFF_PUSH=NONE (before checkpoint)
CHECKPOINT_COMMIT_MESSAGE=feat: add realtime websocket foundation
WEBSOCKET_IMPLEMENTATION=SPRING_NATIVE_WEBSOCKET (TextWebSocketHandler, Spring Boot 4.0.8)
UNITY_WEBSOCKET_IMPLEMENTATION=SYSTEM_NET_WEBSOCKETS_CLIENTWEBSOCKET
REDIS_IMPLEMENTATION=SPRING_DATA_REDIS_LETTUCE
REALTIME_ENDPOINT=/ws/v1/realtime

The supplied task ends mid-sentence in section 54. The same text was supplied again after clarification. Implementation follows its available requirements; no additional missing requirements are claimed complete.

## Architecture and protocol

Spring Boot 4.0.8 native Spring WebSocket (`TextWebSocketHandler`), without STOMP/SockJS. Boot manages Spring Data Redis/Lettuce. See [Boot WebSocket support](https://docs.spring.io/spring-boot/reference/messaging/websockets.html) and [Spring native handlers](https://docs.spring.io/spring-framework/reference/web/websocket/server.html).

Endpoint: `/ws/v1/realtime`. Only its GET upgrade is public; socket commands have no authority until the existing Firebase verifier succeeds. Existing REST security remains in force. Browser same-origin checks remain enabled; native Unity does not send Origin. No token in URL, registry, Redis, logs or snapshots. No new Guest is created by realtime.

Each text envelope has exactly `type`, integer `version=1`, strictly increasing positive `sequence` scoped to one connection/direction, ISO-8601 `timestamp`, and object `payload`. An explicit request ID is unnecessary for G3's ordered AUTH/subscription commands. Client sequence restarts on reconnect. Unknown types, malformed/duplicate fields, unsupported version, binary frames and excessive payloads are rejected. Limit: 32 KiB; JSON depth 8; token length at most 8192; 10 application messages/sec/connection. Basic connection cap: 1000 per server (not a distributed DDoS solution).

Client commands: AUTH (`idToken` only), PING (empty), GLOBAL_ACTIVITY_SUBSCRIBE (empty), GLOBAL_ACTIVITY_UNSUBSCRIBE (empty).
Server messages: AUTHENTICATED (heartbeat configuration), AUTH_FAILED (safe code), PRESENCE_READY (ONLINE), PONG, GLOBAL_ACTIVITY_SNAPSHOT, GLOBAL_ACTIVITY_UPDATED, SYSTEM_ERROR (safe code).

Auth timeout 5s; heartbeat every 20s; heartbeat deadline 45s; presence lease 60s. Server values are externalizable under `realtime.*`. The Unity-compatible .NET ClientWebSocket API does not expose native pong acknowledgments, so G3 uses application PING/PONG for verifiable deadlines at both peers. It is not REST polling. Lease writes are throttled to the heartbeat interval, including when a client sends extra pings.

Unity ApplicationServices owns one RealtimeConnectionService, independent of PlayerService. It derives its endpoint from the existing validated ApiSettings configuration: HTTPS -> WSS; LOCAL + exact localhost/127.0.0.1 + Editor/Development HTTP -> WS. Disabled/invalid API settings leave realtime disconnected. No TLS bypass or CertificateHandler. No WebGL transport is included; mobile IL2CPP/device validation remains distinct from Editor validation.

Reconnect uses bounded exponential backoff with jitter (0.8–1.0 times 1,2,4,8,16,30s). Each attempt gets the current SDK token; expired-token authentication permits one forced refresh, then stops on repeated expiry. Invalid/revoked/disabled/protocol/identity failures stop. Transport/dependency failures retry. Suspension cancels the current connection/backoff; resuming starts one connection after the previous operation drains. Quit/Play exit disposes the service; no Firebase sign-out/cache deletion.

Global activity is server-pushed after subscription, with immediate snapshot and changed counts at most every 2s. OnlinePlayers counts real unique identities; activeMatches/waitingPlayers/openRooms are zero. UI shows realtime connection separately from REST `Synced`, using EN/ES keys. Snapshots are cleared when disconnected rather than shown as current counts.

## Redis model

Docker Compose at repository root: Redis 7.4 Alpine, port bound only to 127.0.0.1:6379, no password or persistent volume, RDB and AOF disabled. It stores no wallet/player/alias data. Redis restart intentionally discards realtime state.

Keys share `domino:v1:{presence}:` (a common hash slot):

- `players`: sorted set, hashed UID -> maximum active connection expiry.
- `player:<sha256-uid>`: sorted set, server-generated connection UUID -> expiry; key TTL to final active expiry.
- `connection:<uuid>`: owning server UUID, TTL 60s.

Atomic Lua scripts use Redis TIME, prune expired leases, and recalculate the UID's maximum expiry. Removing one device preserves the other device; counts are never incremented/decremented per socket. Global count prunes expired scores before ZCARD; no dependency on Redis keyspace notifications or clean socket close. No raw UID/token in Redis. Global payloads contain counts and timestamp only.

Redis unavailable closes realtime with controlled retryable failure; it does not prevent backend startup, REST bootstrap or alias requests. `/actuator/health` excludes Redis as a REST dependency; authenticated `/actuator/health/realtime` retains the actual Redis UP/DOWN status, without details. No DOWN -> UP remapping of the Redis indicator.

## Local operation

From repository root:

```powershell
docker compose up -d redis
docker compose ps
docker exec domino-redis redis-cli PING
docker exec domino-redis redis-cli --scan --pattern 'domino:v1:{presence}:*'
docker exec domino-redis redis-cli ZCARD 'domino:v1:{presence}:players'
```

ZCARD alone can include scores pending expiry pruning for up to the 2s publisher interval; application snapshots prune atomically. `PTTL <key>` and `ZCOUNT <key> <now-ms> +inf` are useful read-only lease diagnostics. No FLUSHDB or manual writes are needed. The optional real Redis test creates UUID-scoped leases and deletes only its own records.

Tests: `server/domino/gradlew.bat test`; opt into real Redis with `DOMINO_REDIS_TESTS=true`. Client: `client/Validation/RunPlayerFoundationClientTests.ps1`. Real Unity harness `Domino.Editor.RealtimeValidation.RunReal` must run only in an isolated batch project with API enabled and an existing Guest. It signals backend/Redis stop/start phases through ignored `Generated/G3/state.txt`; never writes token values. Normal bootstrap can retain its existing lastSeenAt update; same-value alias validation is a no-op.

## Validation

REDIS_DOCKER=PASS (healthy, PONG; loopback binding inspected)
BACKEND_TESTS=PASS (186 tests, zero failures/skips, including real Redis leases)
CLIENT_TESTS=PASS (469 checks, including expired auth, missing heartbeat, background cancellation and reconnect)
GUEST_AUTH=PASS (43 checks)
UNITY_COMPILATION=PASS (three pre-existing serialized-field CS0649 warnings)
UNITY_REAL_PLAY_MODE=PASS
CONSOLE_ERRORS=0 (Unity validation hook; separate editor licensing diagnostics are not gameplay Console errors)
FIREBASE_AUTHENTICATED=PASS (existing Guest, no new identity)
HEARTBEAT=PASS (24-second stable windows before and after recovery)
BACKEND_STOP_RESTORE=PASS (same Play session)
REDIS_STOP_RESTORE=PASS (same backend and Play session)
REST_WITH_REDIS_OFF=PASS (bootstrap and same-value alias PUT; public health remains UP)
UID_ALIAS_COINS_PRESERVED=PASS
PRESENCE_AFTER_UNITY_EXIT=0 (Redis count inspected)
MULTI_CLIENT_SIMULATION=PASS (10/50/100 logical fake-verifier connections; not a throughput benchmark)
GLOBAL_ACTIVITY_UNIQUE_UID=PASS (one UID/two sockets, two UIDs, last disconnect and TTL expiry)
UI_EN_ES=PASS (nine Portrait sizes, runtime locale changes, singular counters)
GAMEPLAY_UNIT_REGRESSION=PASS (82 configuration, 32,679 scoring, 2,981,089 rules/geometry checks; unchanged traces)
PORTRAIT_PLAY_REGRESSION=PASS (139,392 final checkpoint checks, nine sizes, four local seats and complete round; zero Console errors)

Ignored evidence: `client/Validation/Generated/G3/result.txt`, `G3-unity.log`, `G3-ui-final.log`, `G3-portrait.log`, backend test XML and `G2Real/realtime-es.png`. The screenshot uses explicitly simulated UI counts; the separate G3 integration result uses real Firebase/WebSocket/Redis. Real integration ran with enabled API in the isolated copy; the original ApiSettings remains unchanged (`enabled: 0`).

Docker initially failed to stay running; restarting Docker Desktop allowed the configured container to start. No Docker/WSL reinstall, upgrade, system networking change or external service deployment was performed.

Final local state: Redis remains available through Compose. The backend launched for validation was stopped afterward. Original ApiSettings remains disabled; enable it with LOCAL configuration and start the backend when intentionally testing realtime in the original project. No source Unity scene or gameplay settings were changed.

## Main files

- `compose.yaml`: local ephemeral Redis.
- `server/domino/src/main/kotlin/com/teamfho/domino/realtime/`: WebSocket handler/configuration, atomic presence store, independent health groups.
- Backend build dependency, application YAML and security route declarations.
- `client/DominoGame/Assets/_Domino/Scripts/Realtime/`: connection service, transport, protocol/models, mobile lifecycle.
- ApplicationServices composition; StartMenuView and RealtimeStatusView; official EN/ES localization source and tables.
- Backend realtime/Redis tests; RealtimeClientTests; isolated RealtimeValidation and extended profile UI validation.

Remaining limits: Android/iOS device/IL2CPP and mobile OS background behavior require device QA; lifecycle was tested with the service and real networking in Editor. No WebGL adapter. Presence is ephemeral and current connection authentication is reverified on reconnect, not continuously against Firebase. The connection rate limit is a foundation, not a distributed edge limiter.

## Checkpoint validation (2026-09-12)

The checkpoint closes the existing G3 implementation only. Source baseline: `7641fcf04bf8f5faac060f359eb8e9ff2f0ba358`, branch `main`. No runtime functionality or timing values were changed during closure. Only this documentation was extended.

Backend `test build --rerun-tasks`: 186 tests passed, zero skipped, with real local Redis enabled. A separate fresh test run while Redis was OFF passed 185 isolated tests; the single explicitly opt-in real Redis test was skipped. Both sets of XML evidence are retained under ignored `Generated/G3Checkpoint/redis-on-results` and `redis-off-results`. This confirms ordinary tests do not require Redis.

Guest Auth: 43 checks. Player Foundation including G1/G2/G2.1/G3: 469 checks. Configuration: 82 checks. Scoring: 32,679 checks. Rules/geometry: 2,981,089 checks. Static Unity compilation passed with the three existing serialized-field warnings. Backend compilation reports two existing deprecated `asText()` calls in player tests.

The real checkpoint Play Mode run passed Firebase WebSocket authentication, heartbeat, backend stop/restore, Redis stop/restore and both REST operations while Redis was OFF. UID, alias and coins stayed unchanged; no new Guest. After Unity exited, Redis unique presence count returned to zero. The validation backend was stopped; Redis remains running. LuaTools was not running during this validation; no security workaround was added.

Final profile/alias/realtime UI EN/ES validation passed at nine Portrait sizes. The final full-round Portrait run passed 139,392 checks with zero Console errors. The staged review contains exactly 35 G3 files, with no runtime caches, Redis dumps or credential files. The staged secret scan found no credential/token candidates; no ADC or service-account key file is tracked. The commit and remote publication identifiers are supplied in the checkpoint delivery report rather than self-referencing the containing commit.

Android and iOS physical realtime tests remain PENDING and are required before production. API settings stay disabled in the source asset; enabled settings are used only in the ignored validation copy. Redis local may remain running; no cloud environment is configured.

## Future contract

PRESENCE_* and SYSTEM_* already fit the versioned envelope. Later MATCHMAKING_*, LOBBY_* and MATCH_* require explicit schema, authorization and handlers before acceptance. They remain rejected in G3. No matchmaking keys/queues, rooms, online moves, timers, bot takeover, ads, rewards, deployment or Firestore heartbeat writes were added. Cross-instance Redis presence works through shared leases; this phase does not claim a multi-node production load test.

Phase I may introduce waiting counters for DUEL_1V1, FFA_3, FFA_4 and PARTNERS_2V2. These are documentation only: no mode-specific counters, queues or handlers exist in G3. TEST/PROD Redis, Memorystore and cloud deployment are not implemented. Recommended next separately scoped task: Phase H, AdMob + Rewarded Ads + Coins; preserve G3 for future Phase I multiplayer.
