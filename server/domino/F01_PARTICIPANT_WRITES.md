# F0.1 — Participant write optimization

Base: `8e3a174b31683b17fa0600eb2c3451a3fee16528`.

## Persistence change

Only `FirestoreOnlineRepository.transact` changes production behavior: compare complete `MatchCodec.map` participant documents, indexed by seat, from the authoritative BEFORE and AFTER states. Write new or changed documents only. Structural map equality includes every persisted field, null and timestamp; it does not use object identity or client-provided dirty flags. The same codec creates the comparison and the written document, so future persisted fields automatically participate.

Comparison runs inside each Firestore transaction attempt, after its authoritative read and validation. No additional document reads, external cache, out-of-transaction writes, or changes to receipts, revisions, events, rounds, history, root/runtime writes or turn scheduling. Initial `create` and `createPaired` still create all participants. Join creates the new seat without rewriting existing seats. Removal is not supported by existing match invariants and no new removal semantics are introduced.

## Persisted model audit

`match/MatchModels.kt`, `MatchParticipant`; serialized through `MatchCodec.map`.

| Field | Classification | Change behavior |
|---|---|---|
| seatIndex | IMMUTABLE | Assigned at creation/join |
| playerUid | IMMUTABLE | Verified identity, frozen in match |
| displayNameSnapshot | IMMUTABLE | Frozen display name; subsequent profile changes do not rewrite match |
| teamId | IMMUTABLE | Assigned topology, preserved |
| controlType | IMMUTABLE | Online participant control type; autoplay does not change it |
| connectionState | CONNECTION_STATE | CONNECTED / DISCONNECTED / ABANDONED |
| joinedAt | TIMESTAMP, immutable | Creation/join time |
| disconnectedAt | TIMESTAMP, connection | Set on disconnect, cleared on reconnect |
| reconnectDeadlineAt | TIMESTAMP, connection | Set on disconnect, cleared on reconnect |
| abandonedAt | TIMESTAMP, connection | Set at expiry/abandonment |

There are no participant MUTABLE_GAME_STATE or DERIVED fields in the current persisted model. Hands, score, round and turn data live elsewhere.

| Transition | Participant fields changed |
|---|---|
| PLAY_TILE / PASS / AUTO_PASS | None |
| TIMEOUT / AUTOPLAY | None; independent presence expiry can produce an abandonment transition |
| DISCONNECT | connectionState, disconnectedAt, reconnectDeadlineAt of affected seat |
| RECONNECT | connectionState, disconnectedAt=null, reconnectDeadlineAt=null of affected seat |
| Abandonment | connectionState, abandonedAt of affected seat; other timestamps retained |
| ROUND_FINISHED / NEXT_ROUND / MATCH_FINISHED | None in current model |

The optimization does not hardcode these transition assumptions: any future effective document difference is still written.

## Differential validation

`FirestoreEmulatorTests` runs the unchanged F0 controlled DUEL algorithm twice, seed 2, canonical catalog V2, clock starting 2026-01-01 UTC with six-second steps. A test-only SDK wrapper restores omitted participant writes for the legacy run, reproducing F0's 500 reads / 1148 writes baseline. There is no production legacy switch.

The test compares complete final authoritative state (including scores, winner, rounds and hands), all events in sequence, and both stored history documents. Only the independently generated match ID is normalized. It also reloads runtime and participant documents through a fresh repository.

Counts are attempted SDK document operations, not a production billing estimate. Listener/query minimums and background bootstrap/catalog reads are outside this controlled scenario. Contention can retry transactions and increase attempts.

Additional tests cover detached instance equality, all ten fields, nanosecond timestamp changes, timestamp clearing, creation/join, disconnect/reconnect/abandonment and unchanged other seats. Emulator races cover same-seat and cross-player commands, durable duplicate receipts and command-ID conflicts. Existing I2 regressions cover timeout-vs-command and multi-worker timeout; the emulator also exercises concurrent timeout workers with a recreated service.

Recovery tests recreate backend services and rebuild Redis indexes/namespace. They do not claim a physical backend or Docker process restart. Firestore remains authoritative; Redis remains a scheduling index. No real Firestore calls or running Bot Swarm clients are required.

## Scope and review

F0.1 files: this document, the isolated participant-write hunk in `OnlineRepository.kt`, `FirestoreEmulatorTests.kt`, and new `ParticipantPersistenceTests.kt`. Existing M5 edits elsewhere in `OnlineRepository.kt` remain untouched and are not part of F0.1. Other M5/I3.1 files, Unity and both user settings assets are preserved. No stage, commit, push or deploy.

## F0.1 PARTICIPANT WRITE OPTIMIZATION REPORT

```text
BASE_COMMIT=8e3a174b31683b17fa0600eb2c3451a3fee16528
IMPLEMENTED=YES

READS_BEFORE=500
WRITES_BEFORE=1148
EVENT_WRITES_BEFORE=378
PARTICIPANT_WRITES_BEFORE=251
UNCHANGED_PARTICIPANT_WRITES_BEFORE=249
HISTORY_WRITES_BEFORE=2

READS_AFTER=500
WRITES_AFTER=899
EVENT_WRITES_AFTER=378
PARTICIPANT_WRITES_AFTER=2
UNCHANGED_PARTICIPANT_WRITES_AFTER=0
HISTORY_WRITES_AFTER=2
PARTICIPANT_WRITES_REDUCTION_PERCENT=99.20
TOTAL_WRITES_REDUCTION_PERCENT=21.69

FINAL_SCORE_IDENTICAL=YES
WINNER_IDENTICAL=YES
ROUND_COUNT_IDENTICAL=YES
GAMEPLAY_DIFFERENTIAL=PASS
EVENT_SEQUENCE_DIFFERENTIAL=PASS
HISTORY_DIFFERENTIAL=PASS

DUPLICATE_COMMAND=PASS
COMMAND_ID_CONFLICT=PASS
DOUBLE_PLAY_RACE=PASS
CROSS_PLAYER_RACE=PASS
TIMEOUT_COMMAND_RACE=PASS
MULTI_WORKER_TIMEOUT_DEDUPE=PASS

BACKEND_RESTART_RECOVERY=PASS (service recreation)
REDIS_RESTART_RECOVERY=PASS (index namespace loss/rebuild)
MATCH_RESYNC=PASS

NORMAL_BACKEND_TESTS=455 PASS / 14 SKIPPED
BACKEND_WITH_LOCAL_REDIS=468 PASS / 1 SKIPPED
EMULATOR_TESTS=6 PASS
UNITY_CHANGED=NO
REAL_FIRESTORE_CALLS=0
BOT_SWARM_STARTED=NO

RULESETS_CHANGED=NO
GAMEPLAY_CHANGED=NO
MATCH_EVENTS_CHANGED=NO
HISTORY_SEMANTICS_CHANGED=NO
MONETIZATION_CHANGED=NO
M5_USER_CHANGES_PRESERVED=YES
I3_1_USER_CHANGES_PRESERVED=YES
ADS_SETTINGS_PRESERVED=YES
API_SETTINGS_PRESERVED=YES

COMMIT=NONE
PUSH=NONE
DEPLOY=NONE
```

Evidence (ignored generated output): `build/f01-normal.log`, `build/f01-normal-results/`, `build/f01-redis.log`, `build/f01-emulator.log`, `build/test-results/emulatorTest/`. Emulator-owned process and port cleanup both passed. Existing optional parity test is skipped; Redis tests are opt-in and skipped in the normal suite. Counts refer to the existing combined working tree with preserved M5/I3.1 pending code. No new Unity validation is necessary for this server-only change.
