# Domino I3.1 development bot swarm

External Kotlin/JDK 21 coroutine clients for `DUEL_1V1` and
`PARTNERS_2V2_ONLINE`. This Gradle application is not a Spring Boot dependency.
Each slot is a real Firebase identity, bootstraps through REST and joins the
normal matchmaking queue. All seats remain `REMOTE_HUMAN`.

## Emulator validation (preferred)

The resumed I3.1 validation supports a separate all-local route. It requires ALL of:

```text
DOMINO_SWARM_EMULATOR=true
DOMINO_SWARM_FIREBASE_PROJECT_ID=demo-domino-swarm
DOMINO_SWARM_FIREBASE_API_KEY=emulator-only
FIREBASE_AUTH_EMULATOR_HOST=127.0.0.1:19099
FIRESTORE_EMULATOR_HOST=127.0.0.1:18085
baseUrl=http://127.0.0.1:18086
environment=LOCAL
```

Missing/mixed emulator settings, a real project ID or a non-loopback endpoint fail before authentication. Without emulator mode the original explicit `DOMINO_REAL_FIRESTORE_TESTS=true` guard remains required. Real identity files and emulator identity files must never be mixed. `RunEmulatorCase.ps1` uses a separate temporary directory for emulator slots.

Use Firebase CLI 15.30.2 with `emulator.firebase.json` for Auth, Firestore emulator JAR 1.22.0 for database `demo-domino-swarm`, and a dedicated Redis bound to `127.0.0.1:16379`. These versions were used for this validation; installation lives only under ignored `build/swarm-emulators/`. The test-classpath `swarmEmulatorBackend` task starts port 18086, seeds the unchanged canonical V4 catalog before scheduled consumers start, and uses the production Firebase Admin verifier against Auth Emulator. No fake verifier or UID input is added to the backend product. [Firebase's Auth Emulator contract](https://firebase.google.com/docs/emulator-suite/connect_auth#admin_sdks) explains its unsigned test tokens; never set emulator variables in production.

Provision the emulator slots with `:bot-swarm:provision --args="--clients=20 --baseUrl=http://127.0.0.1:18086"` using those environment values and `DOMINO_SWARM_IDENTITIES_DIR` pointing outside Git. Then run `tools/bot-swarm/RunEmulatorCase.ps1` from `server/domino`:

```powershell
./tools/bot-swarm/RunEmulatorCase.ps1 -Clients 10 -TargetMatches 10 -Name duel10
./tools/bot-swarm/RunEmulatorCase.ps1 -Clients 20 -TargetMatches 20 -Name duel20
./tools/bot-swarm/RunEmulatorCase.ps1 -Mode PARTNERS_2V2_ONLINE -Clients 4 -TargetMatches 1 -Name partners4
./tools/bot-swarm/RunEmulatorCase.ps1 -Mode PARTNERS_2V2_ONLINE -Clients 10 -TargetMatches 4 -Name partners10
./tools/bot-swarm/RunEmulatorCase.ps1 -Mode PARTNERS_2V2_ONLINE -Clients 20 -TargetMatches 10 -Name partners20
```

The validation script deliberately uses 100–150 ms thinking; normal swarm defaults remain 800–2500 ms. This is a protocol/correctness exercise, not a performance benchmark. Optional `targetMatches` stops new requeues when the global target is reached and lets existing matches finish; completed totals may exceed the target. Zero retains the previous duration-driven behavior. A missing target before duration expiry is NOT a successful completion: inspect the final summary.

`DOMINO_SWARM_EMULATOR_DROP_SOCKET_ONCE=true` injects one socket interruption after slot 1 sends ten commands, only with the exact emulator configuration. It exercises normal refresh/reconnect/resync without changing identity or gameplay. It is never used for real clients.

`inspectSwarmEmulator` reads only the emulator, verifies unique participants, non-overlapping assignments, sequence continuity, teams, histories and zero rewards, and retains complete match/event/round/history fixtures under `build/swarm-emulator/retained-matches/`. Cost counters are attempted SDK document operations; queries are reported separately, and listener reads/retries are not a billing estimate. Inspection/export reads are outside the load intervals. Stop the owned validation backend by creating `build/swarm-emulator/stop`; remove that single signal before a later restart. Stop only owned emulator processes and the dedicated Redis after exporting data. Never delete completed matches as part of shutdown.

## Local prerequisites

Run Redis using the repository's `docker compose up -d redis`, and start the
normal backend with its Firebase configuration. Redis stays bound to loopback.
Do not stop unrelated services to free a port: select a backend port and pass
the same `--baseUrl` to the swarm instead.

Set these environment variables in the shell without committing their values:

- `DOMINO_SWARM_FIREBASE_PROJECT_ID`: approved Firebase project.
- `DOMINO_SWARM_FIREBASE_API_KEY`: corresponding Firebase client API key.
- `DOMINO_SWARM_IDENTITIES_DIR`: private directory outside any Git repository.
- Administrative provisioning only: normal Google Application Default Credentials.

From `server/domino`, provision stable slots once:

```powershell
.\gradlew.bat :bot-swarm:provision --args="--clients=2"
```

Provisioning creates anonymous Firebase accounts only for missing slots. Existing
slot credentials are reused. Refresh tokens remain outside Git. Each running
slot obtains an exclusive local file lease, preventing two processes from using
the same slot. Do not copy or share these files.

The administrative provisioner creates an immutable marker at
`developmentTestAccounts/{uid}` with `isTestAccount=true`,
`testSource=BOT_SWARM`, environment and slot. This is test metadata, not Player,
Wallet or gameplay data. The normal server reads the marker and propagates the
existing `validationData` flag to paired matches and normal history. The
marker is not accepted from any client request. Firebase custom claims were not
used because the local administrative identity lacks permission to update them.
Firebase verification and backend security remain unchanged.

## Run

```powershell
.\gradlew.bat :bot-swarm:run --args="--clients=2 --mode=DUEL_1V1 --requeue=false --durationSeconds=900"
.\gradlew.bat :bot-swarm:run --args="--clients=4 --mode=PARTNERS_2V2_ONLINE --durationSeconds=900"
```

Provision enough slots before increasing the client count. Default count is 10;
the hard limit is 20. Use `--baseUrl=http://127.0.0.1:8081` if necessary.
`--slotOffset=1 --clients=3` uses slots 2–4, useful alongside a human client.
Human clients retain their own Firebase identity; never enter or spoof a UID.

Configuration precedence: defaults, `bot-swarm.yml` (or `--config=...`), supported
environment overrides, then command-line `--key=value`. Environment overrides
are `DOMINO_SWARM_CLIENTS`, `DOMINO_SWARM_MODE`, `DOMINO_SWARM_ENVIRONMENT` and
`DOMINO_SWARM_SEED`. Names are a seeded, unique selection from 40 natural aliases.
Default thinking delay is 800–2500 ms, requeue delay 1000–5000 ms.

Only `LOCAL` and `TEST` are accepted. LOCAL requires exact loopback hosts.
TEST requires HTTPS and an exact `DOMINO_SWARM_TEST_BASE_URL` match. PROD and
unknown environments fail before credentials are read or connections opened.
This is development tooling, never a public product launcher.

## Protocol and isolation

The catalog and immutable match rule snapshot provide player counts, rules and
teams. Only the current client's private projection is available to its strategy.
The first legal tile is selected using public chain ends; scores and rules are
never computed authoritatively. Both current modes require an explicit PASS.
Starter selection follows the published starter state. Seat 0 sends NEXT_ROUND
to avoid simultaneous continuation commands.

REST access is allowlisted to catalog, bootstrap, display name, queue, own match
snapshot/events and own history. There is no direct match-create call or
monetization endpoint. The running swarm has no Firebase Admin dependency;
administrative provisioning uses a separate source set.

WebSocket authentication uses the current Firebase token. Heartbeat follows the
server interval. Recovery checks queue/active assignment before rejoining.
Sequence gaps trigger a snapshot resync; acknowledged commands retain their
logical command ID across retries. Expired authentication permits one fresh-token
retry; invalid/revoked authentication stops. Transport retries use jitter and a
15-second cap with a configurable failure ceiling.

Ctrl+C or the duration limit cancels clients, attempts to leave pending queues,
closes sockets and releases identity leases. Active matches remain under the
server's normal disconnect lifecycle; the tool does not delete matches or force
results. Dashboard output contains aliases, match IDs, states and counts, never
tokens, authorization headers or response dumps.

For supervised load validation, optionally set `DOMINO_SWARM_STOP_FILE` to a new
local file path before launching. Creating that file requests the same graceful
shutdown within 250 ms. Use a different path for each run. No network polling is
introduced. If any client enters FAILED, the whole load stops rather than
leaving other participants retrying. This permits a supervisor to stop a load
on the first dependency error and then diagnose its safe server category.

## Validation and retained data

```powershell
.\gradlew.bat :test :bot-swarm:test :bot-swarm:provisionClasses :bootJar
```

Unit checks do not establish real multiplayer success. Record real runs separately
for 2/10/20 duel clients, 4/10/20 partners clients, a human versus one swarm client,
a human plus three swarm clients, reconnect and graceful shutdown. Completed
matches must appear in normal history and remain available for future replay
validation. No cleanup runs automatically.

Future cleanup must be an explicitly approved administrative operation: identify
the exact marker-owned account set and match IDs, generate a dry-run inventory,
exclude human accounts and unmarked matches, and preserve any completed matches
selected for I4. Never infer test ownership from an alias, age or prefix. Firebase
account deletion and economic documents require separate explicit authorization;
this tool performs neither.

## Current limitations

Real validation depends on local Redis, backend and approved Firebase access.
The TEST URL allowlist must be supplied by the operator; it cannot independently
prove a remote deployment is non-production. A finished match is advanced by
seat 0; its disconnection uses the existing server lifecycle. No engine bots,
matchmaking changes, reward activity, I4 replay or I5 spectator are implemented
by the client module.
