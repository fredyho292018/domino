# I3.1 MULTI-MODE BOT SWARM — resumed validation

Base: `9522bb921585fc3f709fded3ac4aa3cc8df86354`. No commit, push or deploy.

Automated matrix and the small real-Firestore final test passed. Human validation was pending when this historical report was written; it subsequently passed for both modes. See [HUMAN_VALIDATION_REPORT.md](HUMAN_VALIDATION_REPORT.md) and [CHECKPOINT.md](CHECKPOINT.md) for final acceptance evidence.

## Protocol load matrix

All local cases use Auth Emulator + Firestore Emulator + isolated real Redis + the actual Spring HTTP/WebSocket/I3 matchmaking and game services. Firebase Admin's standard emulator support is used, not a mock token verifier. Thinking time is 100–150 ms for bounded validation; the tool's ordinary defaults remain 800–2500 ms. No rules, scores or turn deadlines were shortened.

| Case | Completed | Peak concurrent | Commands | Rejected | Resyncs | Timeouts | Fatal errors | History confirmations |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| DUEL 2 | 1 | 1 | 70 | 1 | 3 | 0 | 0 | 2 |
| DUEL 10 | 14 | 5 | 1677 | 1 | 29 | 0 | 0 | 28 |
| DUEL 20 | 28 | 10 | 3602 | 1 | 57 | 0 | 0 | 56 |
| PARTNERS 4 | 1 | 1 | 301 | 0 | 4 | 0 | 0 | 4 |
| PARTNERS 10 | 5 | 2 | 1247 | 0 | 20 | 0 | 0 | 20 |
| PARTNERS 20 | 12 | 5 | 3176 | 0 | 48 | 0 | 0 | 48 |

PARTNERS 10 explicitly recorded `PLAYING=8, SEARCHING=2`. Target-based draining stopped requeues and allowed already active matches to finish, hence totals above the requested targets. No incomplete or three-player PARTNERS matches were created. The completed-match inspection verified distinct UIDs, REMOTE_HUMAN control, teams [0,2]/[1,3], double-nine-partners v1, continuous event sequences, validation markers and per-player histories. It also checked that a UID's match time windows never overlap.

No sequence gaps, unexpected hand projection fields or fatal errors occurred in the matrix. The single accepted recovery path for a rejected DUEL command resynced and continued; rejected commands are reported, not hidden as zero.

## Firestore operation observations

These are attempted SDK **document operations** within each load interval, including bootstrap/profile operations. Queries are separate counts, not counts of query result documents. Listener deliveries, query minimum billing and SDK retry billing are not included; these are **not total billable reads or production cost estimates**. Inspection and export are outside the measured load intervals.

| Case | Document reads | Writes | Participant writes | Unchanged participant writes | Event writes | History writes | Queries |
|---|---:|---:|---:|---:|---:|---:|---:|
| DUEL 10 | 6918 | 12157 | 28 | 0 | 5104 | 28 | 102 |
| DUEL 20 | 14814 | 26070 | 56 | 0 | 10952 | 56 | 211 |
| PARTNERS 10 | 5163 | 8959 | 20 | 0 | 3826 | 20 | 86 |
| PARTNERS 20 | 13108 | 22801 | 48 | 0 | 9740 | 48 | 184 |

Participant writes equal initial participant creation for these steady-connection scenarios. F0.1 remains active under concurrency. After shutdown, an 88-second idle interval recorded zero additional document reads, writes or Firestore turn queries. Firestore remains durable authority; Redis remains the due index, not economic/game authority.

## Faults, recovery and shutdown

An initial backend interruption exceeded the configured eight-failure retry ceiling: both clients stopped, rather than retry forever. That attempt did **not** pass automatic backend-restart reconnection. The same existing emulator identities subsequently recovered the retained match. A separate emulator-only injected socket drop then produced one disconnect, one automatic reconnect, one expected injected error, zero fatal errors and a completed match with two histories. No identity was recreated. The completed recovered match is `8eee221d-852f-4821-a128-770e17ade1e7`.

A separate single SEARCHING client was stopped using the local stop-file signal, which shares the coroutine cancellation/queue leave/socket cleanup path with the shutdown hook. Redis inspection found no queue keys/entries afterwards. An actual keyboard Ctrl+C was not separately pressed.

The initial emulator startup also exposed a harness ordering error: its catalog was seeded after scheduled consumers had cached an unavailable result. Seeding was moved into test dependency construction before scheduling starts. Production catalog logic was unchanged. The corrected full matrix above is the acceptance evidence.

## Real final validation — deliberately small

Only after the emulator matrix passed, the two previously provisioned `teamfho-domino` identities were checked and reused. No new real Firebase accounts were created. The existing markers were read/validated; no 10/20-client real load was run.

The real backend used loopback port 18087 and a separate Redis on 16380. The run was bounded by one target match, 120 seconds and `maxFailures=1`. Results: real Firebase authentication, real WebSockets, real I3 matchmaking, one completed DUEL, six rounds, 139 commands, one command rejection, three resyncs, two history confirmations, zero fatal errors and zero observed RESOURCE_EXHAUSTED. Retained real match: `98f7c774-e062-4b87-b7dc-c6b4195fbf94`.

The owned real validation backend was stopped after completion, preventing continuing real Firestore work. Real read/write billing was not instrumented and is not claimed to be zero. No production ad, reward intent, reward grant or wallet reward write was requested. Ordinary player bootstrap is allowed; it is distinct from reward credit.

## Regressions

- Normal backend: 455 PASS / 14 SKIPPED.
- Backend with local Redis and explicit M5 differential enabled: **469 PASS / 0 SKIPPED**. Forced test execution avoids a cached normal-suite result.
- Swarm unit tests: 25 PASS.
- Online client: 48 checks PASS; matchmaking client: 75 PASS.
- C# DUEL: 14,723 checks PASS / 200 rounds.
- C# PARTNERS traces: 100 matches / 679 rounds / 27,946 turns. Server comparison: 29,304 complete trace steps PASS, including deals/results.
- F0/F0.1/I1/I2/I3/M5 regressions remain passing. No Unity product or gameplay changes were made in this continuation.

## Retained data and evidence

Under ignored `server/domino/build/swarm-emulator/`:

- Per-case logs and `*-before.json` / `*-after.json` counters.
- `inspection.json`: 44 completed emulator DUELs (including recovery), 18 PARTNERS, 160 histories, 31,061 events, no duplicate pairing, no sequence corruption, zero reward intents/coins.
- `retained-matches/`: 62 complete fixtures, including authoritative runtime, participants, rounds, events and history. These contain test-only private hands for future trusted I4 fixture use; never serve them as public projections.
- `firestore-export/firestore_export/`: full native Firestore Emulator export. Completed matches were not deleted.
- `idle-before.json`, `idle-after.json`: 88-second idle observation.

Real evidence: `build/swarm-real-final-preflight.log`, `build/swarm-real-final.log`, `build/swarm-real-final-backend.log`. Client and regression evidence: `build/swarm-final-regressions.log`, `build/swarm-final-redis-parity.log`, `build/swarm-client-*.log`, `build/swarm-m5-parity.log`. Credentials remain outside Git; none are included in the report.

```text
BASE_COMMIT=9522bb921585fc3f709fded3ac4aa3cc8df86354
SUPPORTED_MODES=DUEL_1V1, PARTNERS_2V2_ONLINE
DEFAULT_CLIENTS=10
MAX_CLIENTS=20
DUEL_2_CLIENTS=PASS
DUEL_10_CLIENTS=PASS
DUEL_20_CLIENTS=PASS
MAX_DUEL_CONCURRENT_MATCHES=10
COMPLETED_DUEL_MATCHES=44 emulator + 1 real
HUMAN_VS_SWARM_DUEL=NOT_RUN
PARTNERS_4_CLIENTS=PASS
PARTNERS_10_CLIENTS=PASS
PARTNERS_20_CLIENTS=PASS
MAX_PARTNERS_CONCURRENT_MATCHES=5
COMPLETED_PARTNERS_MATCHES=18
HUMAN_PLUS_3_SWARM=NOT_RUN
TEAMS_0_2_VS_1_3=PASS
PARTNER_HAND_LEAK=NO
OPPONENT_HAND_LEAK=NO
IDLE_FIRESTORE_TURN_QUERIES=0
UNCHANGED_PARTICIPANT_WRITES=0
F01_OPTIMIZATION_ACTIVE=PASS
REAL_AUTH=PASS (small real final test; emulator auth in local matrix)
REAL_WEBSOCKET=PASS
REAL_I3_MATCHMAKING=PASS
SEQUENCE_CORRUPTION=NO
DUPLICATE_PAIRING=NO
REQUEUE=PASS
RECONNECT=PASS (controlled socket drop; retry-ceiling limitation documented)
GRACEFUL_SHUTDOWN=PASS (local cancellation signal)
ORPHAN_QUEUE_ENTRIES=0
TEST_DATA_MARKED=PASS
HISTORY_GENERATED=PASS
REWARD_INTENTS_CREATED=0
REWARD_COINS_EARNED=0
SWARM_WALLET_REWARD_WRITES=0
EMULATOR_VALIDATION=PASS
REAL_FIRESTORE_VALIDATION=PASS (2 clients only)
RESOURCE_EXHAUSTED_COUNT=0 observed during this continuation
F0=PASS
F0_1=PASS
I1=PASS
I2=PASS
I3=PASS
M5=PASS
PARTNERS_2V2_LOCAL=PASS
M5_CHANGES_PRESERVED=YES
I3_1_CHANGES_PRESERVED=YES (continued incrementally)
ADS_SETTINGS_PRESERVED=YES
API_SETTINGS_PRESERVED=YES
COMMIT=NONE
PUSH=NONE
DEPLOY=NONE
```
