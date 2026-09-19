# F0 — Firestore cost and test isolation

Source checkpoint: `82c1a985ee31a631ede0b233f1f1a25adf84bd4d`.
Validated locally on 2026-09-19. No commit, push, deploy, real Firestore validation or swarm session was performed.
Pre-existing M5/I3.1 work remains in the working tree. F0 does not optimize participant persistence or change game rules.

Checkpoint scope: only F0 source/tests/docs are staged. The uncommitted M5 catalog, partners tools, swarm module, UI/localization changes, and both user settings assets are excluded. Their already-applied F0 guard additions remain with that pending work. The guard regression checks the nine published tools and additionally checks the two M5 tools when available, so the F0 checkpoint does not depend on unpublished M5 classes. The 465-test result below describes the original combined working tree; the isolated checkpoint is validated separately before publication.

## Test categories and safe commands

Run commands from `server/domino` unless indicated otherwise.

| Category | Scope | Command / entry |
|---|---|---|
| UNIT | Pure policy, codecs, guards, mocked SDK adapters | `./gradlew test` |
| IN_MEMORY | Match engine, rewards, bootstrap, catalog, history, HTTP/security tests using fake stores and test profile | `./gradlew test` |
| LOCAL_REDIS | Existing presence/matchmaking tests plus isolated turn-index Lua tests | PowerShell: `$env:DOMINO_REDIS_TESTS='true'; ./gradlew test` |
| EMULATOR | Real Firestore SDK/transactions/listener, synthetic `demo-domino-f0` data, loopback only | `./tools/f0/RunFirestoreEmulatorTests.ps1` |
| REAL_FIRESTORE | Explicit final policy validation; excluded from normal `test` and `check` | `$env:DOMINO_REAL_FIRESTORE_TESTS='true'; $env:FIREBASE_PROJECT_ID='<explicit-project>'; ./gradlew realFirestoreTest` |

Remove opt-in variables after an intentional validation session. The real command above was **not executed with opt-in enabled** during F0.

Normal `test` excludes both REAL_FIRESTORE and EMULATOR tags, regardless of the real opt-in flag. It also sets `FIRESTORE_EMULATOR_HOST=127.0.0.1:1` and `GOOGLE_CLOUD_PROJECT=demo-domino-unit` inside the test JVM, so an accidentally constructed default SDK client fails locally instead of reaching a real project. Existing Spring tests use `application-test.yaml` with Firebase disabled; explicit Firebase configuration tests mock ADC/Firestore. The normal suite has no emulator dependency.

The emulator runner downloads the pinned official 1.22.0 JAR to ignored `build/`, verifies SHA-256, binds `127.0.0.1:18085`, and terminates its own process in `finally` on success or failure. It refuses an occupied port. It asserts both process and port cleanup. No ADC is used: the client explicitly uses `FirestoreOptions.EmulatorCredentials`, a demo project and loopback channel. This SDK's legacy `NoCredentials` path omits listener routing headers; the emulator credentials path preserves them. No production SDK configuration was altered.

Official emulator setup: https://firebase.google.com/docs/emulator-suite/connect_firestore

## Explicit real-network entry points

`validation-common/RealFirestoreGuard.kt` is shared by backend and swarm tooling. It requires the exact value `DOMINO_REAL_FIRESTORE_TESTS=true` before services, credentials, sockets or SDK calls.

Protected JVM tools:

- `validateMatchM4Firestore`, `validateOnlineI1Real`, `validateOnlineUnityI11`, `validateOnlineTurnI2`.
- `inspectPartnersMatch`, `inspectTwoUnityMatch`.
- `seedMonetizationPolicy`, `seedGameCatalog`, `publishGameCatalogV2/V3/V4`.
- Real policy JUnit test (separate opt-in task).
- Swarm runner, provisioning and match inspection. LOCAL/TEST restrictions remain; PROD remains prohibited.

Nine published backend entry points (plus two optional M5 entry points when present) are invoked with missing opt-in by `RealFirestoreGuardTests` and must reject immediately. The separate real test Gradle task was also verified to fail with `REAL_FIRESTORE_DISABLED` before tests run. No swarm was started.

## Unity isolation

`ValidationNetworkPolicy` is consulted before `ApplicationServices` starts identity/bootstrap. Batch runs default to isolated; visual entry points call `BeginIsolated` before entering Play Mode. Their API configuration is disabled in memory, not by rewriting assets. The real Firebase adapter stops before SDK initialization; REST and socket adapters independently refuse network access. Fake test factories remain usable.

Real validation entry points call `AuthorizeReal` and require the global environment flag. Development standalone flags `--i21-client-b` and `-m5Role` are recognized before the composition root; missing authorization isolates startup and rejects the driver. `--i21-visual` always isolates. Ordinary interactive gameplay is unaffected. Editor session authorization is cleared after Play Mode, and the two-pass guest validation reauthorizes each pass.

Safe client checks, from repository root:

```powershell
./client/Validation/RunFirestoreIsolationTests.ps1
./client/Validation/Compile.ps1
./client/Validation/RunOnlineMatchTests.ps1
./client/Validation/RunMatchmakingTests.ps1
./client/Validation/RunGuestAuthTests.ps1
./client/Validation/RunPlayerFoundationClientTests.ps1
```

Play Mode probe: `Domino.Editor.FirestoreIsolationValidation.Run`, via an isolated batch project under `client/Validation/Generated`. It tests startup with the real source API asset still enabled, waits ten seconds, checks zero network entry points, and exercises REST/socket rejection.

The actual Play Mode probe passed using the existing Unity 6000.0.41f1 Personal license after the user confirmed it was active. It reported `VISUAL_TEST_REAL_BACKEND_CALLS=0`, `FIREBASE_SDK_CALLS=0`, `REALTIME_CALLS=0`, and `BOOTSTRAP_ISOLATION=PASS`. No license activation was performed by F0.

Source `ApiSettings.asset` and `AdsSettings.asset` were not changed by F0 (their pre-existing user changes remain).

## Durable turn scheduling

`OnlineTurnWorker` no longer calls `OnlineRepository.due()` on every tick. Redis provides the due hints; Firestore remains the authority for match revision, current turn, deadline and exactly-once command receipts.

- `RedisTurnDueIndex`: sorted due set, atomic Lua claim, 30-second processing lease. A crashed worker's claimed item becomes due again.
- `TurnIndexBridge`: one Redis-elected listener owner, lease renewed on worker ticks; owner-fenced updates prevent stale listeners from publishing.
- `FirestoreTurnWorkFeed`: initial snapshot and committed changes of the existing durable `onlineTurnWork` collection. That collection is written in the same transaction as match state, so a crash between commit and Redis publication cannot lose a deadline.
- Backend restart: listener initial snapshot reconstructs persisted deadlines.
- Redis restart: loss of leader lease forces listener reopening and a fresh snapshot. Tests simulate loss only in a unique Redis namespace, never FLUSHDB.
- Listener/index failure: bounded retry backoff from 5 to 300 seconds. No rapid Firestore recovery polling.
- Redis is never economic/game authority. Competing/replayed due hints still pass through the original authoritative Firestore transaction.
- Firebase-disabled test contexts receive an inert index and cannot claim a running application's Redis work.

The five-second scheduler tick still exists **against Redis**. There is no recurring Firestore turn query at idle. The 60-second emulator test observed one initial watch and zero changes; the in-memory test also covers 720 ticks without reopening. Existing active-match presence discovery at 30 seconds remains unchanged.

Cost trade-off: this avoids approximately 17,280 empty turn queries/day/instance. A healthy elected listener adds document reads for committed work changes and initial/recovery snapshots; this is not a claim of zero total Firestore usage or zero billing. Other subsystems' catalog/policy reads are outside this turn-worker change. Network reconnects can cause new listener snapshots.

## Controlled DUEL operation measurement

One seeded, full DUEL using the production `FirestoreOnlineRepository` against the emulator:

| Metric | Measured |
|---|---:|
| Commands after join | 124 |
| Rounds | 7 |
| Document reads attempted | 500 |
| Document writes attempted, including deletes | 1,148 |
| Event document writes | 378 |
| Participant document writes | 251 |
| Participant writes identical to previous values | 249 |
| Player history document writes | 2 |

Instrumentation lives only in `FirestoreEmulatorTests`: it delegates to real SDK transactions and counts adapter operations. The measurement excludes bootstrap/catalog loading, socket clients, active worker presence checks and listener reads; it is not a production billing estimate. Transaction contention may add retries/read attempts in other workloads.

Every accepted command currently rewrites every participant, even unchanged participants. Recommendation: later compare each participant against the transaction's authoritative `before` state and skip equal writes. Preserve join creation, disconnect/reconnect/abandonment, aliases frozen in snapshots, event ordering, idempotency and transaction retry correctness. This optimization was **not implemented** in F0.

## Validation evidence

- Normal backend: 452 PASS, 14 skipped (13 optional Redis cases plus existing optional parity case), 0 failures; real/emulator suites excluded.
- Backend with local Redis: 465 PASS, 1 existing optional case skipped, 0 failures.
- Final isolated F0 staged snapshot (without pending M5/I3.1): 456 PASS, 0 skipped, 0 failures with local Redis. The counts above describe the combined working tree.
- Swarm unit tests: 23 PASS, no running swarm clients.
- Firestore emulator: 4 PASS: full DUEL accounting, change feed, persisted expired turn/concurrent timeout recovery, 60-second idle observation.
- Redis tests cover atomic claims, leader fencing, namespace loss/rebuild and processing lease retry. In-memory tests cover backend recreation, leader failover and backoff.
- Temporary Spring server failure path closes its port; emulator cleanup verifies owned process termination and port closure. Validation ports 8081, 18083 and 18085 were closed after tests. Gradle daemons are not validation backends.
- Unity static runtime/editor compilation PASS.
- Unity isolation policy: 16 PASS, zero network calls in pure harness.
- Online client: 48 checks PASS; matchmaking client: 75 PASS.
- Guest Auth: 43 PASS; Player Foundation/realtime client: 469 PASS, real network calls zero.
- Actual Unity compilation and Play Mode: PASS; zero Console errors during the isolation probe.

Local evidence: `build/f0-normal-tests.log`, `build/f0-tests.log`, `build/f0-full-test-results/`, `build/test-results/emulatorTest/`, `build/f0-emulator-tests.log`, `build/f0-real-guard.log`, and `client/Validation/Generated/f0-*.log`. Generated evidence stays out of Git.

Final checkpoint includes only F0 source, tests and documentation; pending M5/I3.1 changes and user AdsSettings/ApiSettings assets are excluded.
DEPLOY=NONE
