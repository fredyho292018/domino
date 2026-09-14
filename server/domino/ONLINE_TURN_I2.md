# Online DUEL turn lifecycle — I2

Base: `d3af125a3b6cccb23af2a290d91102d6e812997b`. No catalogue, scoring,
wallet, matchmaking, spectator, or advertising policy changes.

## Authority and transactions

`OnlineEngine.Builder.startDeadline` reads the frozen `MatchRuleSnapshot`:
`turnPolicy.timeLimitSeconds=60`. It persists `turnStartedAt` and
`turnDeadlineAt` with `TURN_STARTED`. Starter selection has no timer.
Human PLAY/PASS is rejected at or beyond the server deadline, even before
the worker runs. Client commands have no deadline or timeout authority.

`OnlineMatchService.timeout` uses a reserved server command ID
`sys_timeout_{round}_{turn}`. `FirestoreOnlineRepository.transact` checks the
current revision and deadline, writes state, round, contiguous events,
receipt, discovery index, and any terminal history in one transaction.
Competing workers or player commands cannot apply a second move. Delivery
occurs after commit; WebSocket delivery failure cannot roll it back.

Autoplay uses authoritative hand order, first matching tile, LEFT before
RIGHT; otherwise it passes. `TURN_TIMEOUT` and `AUTO_PLAYED` are audit events;
the regular `TILE_PLAYED`/`PLAYER_PASSED` transition performs the action and
normal round scoring exactly once. Replay does not apply the audit twice.

## Durable discovery and recovery

`onlineTurnWork/{matchId}` is a server-only Firestore discovery document,
written atomically with new online matches and subsequent transitions.
It contains `dueAt`, `presenceCheckAt`, and participant UIDs (never exposed
in public match events). `dueAt` is the earliest turn deadline, reconnect
deadline, or presence reconciliation deadline.

`OnlineTurnWorker` queries up to 100 due documents every five seconds and
reconciles connection leases every 30 seconds. It does not poll every match
each second. Redis socket notifications add UIDs to an in-memory hint set;
the persistent index remains sufficient after a backend restart.
Timeout application can follow its deadline by a scheduling interval plus
storage/backlog latency; the deadline itself is never extended.

Online work has its own `onlineTurnScheduler`. G3 lifecycle jobs retain a
separate scheduler, so synchronous Firestore work cannot stall heartbeat
processing. These are initial operational limits, not a load benchmark.

For production scale, partition the due index by a stable match-ID shard,
assign shard ranges to bounded worker pools, and measure queue lag and
Firestore contention before increasing concurrency. Keep the same atomic
revision/deadline/receipt checks even if delivery uses a durable task queue.
Shard ownership, distributed fan-out, and load benchmarking are future work.

## Disconnect, reconnect, abandonment

`RedisPresenceStore.connectionCount` counts live per-UID connection leases
using Redis server time and existing lease expiration. Closing one of two
sockets does not disconnect the participant. Notifications do no Firestore
work while holding a G3 socket lock. Periodic reconciliation covers missing
close notifications and expired leases.

The last observed valid connection disappearing writes `disconnectedAt`
and a frozen-policy reconnect deadline of 180 seconds. Turn time continues.
Reconnect before that deadline emits `PLAYER_RECONNECTED` and clears the
disconnect timestamps; it does not undo autoplay or restart turn time.
At or beyond the deadline the seat becomes `ABANDONED`, including when a
late reconnect races the worker. Human commands are rejected after expiry;
normal timeout autoplay can still operate that seat. No automatic match
loss, coin penalty, ranking penalty, or bot identity replacement is added.

Redis errors mean unknown presence, not proof of a disconnect. Existing
persisted reconnect deadlines and turn deadlines still apply. Restoring
Redis and reconnecting rebuilds leases; REST state remains authoritative.
Presence detection is eventual (lease lifetime plus reconciliation after
unclean disconnect), not an atomic transaction across Redis and Firestore.

## Unity presentation

`OnlineTurnClock` estimates remaining time from `serverNow` and the deadline
using a monotonic Stopwatch anchor; changing the device wall clock grants
no time or move authority. Network latency can affect the displayed estimate.
`OnlineMatchController` displays a localized EN/ES countdown and lifecycle
feedback. At zero input stops while the server resolves the timeout.

Connection loss retains the board, blocks input, and clears pending command
state. A restored G3 connection resynchronizes through authenticated REST
with the same Firebase identity; it does not recreate a guest or local engine.

Events remain schema version 1 with additive optional fields. The legacy
`turnDeadline` alias remains available alongside `turnDeadlineAt`.

## Compatibility limits

- Pre-I2 records lacking an index/deadline are not retroactively migrated.
  A subsequent I2 transition installs the index, and the next turn installs
  its deadline. Restart recovery is validated for persisted I2 matches.
- Waiting/starter and round-finished matches still receive presence checks;
  there is no starter timeout, automatic next-round timer, or retention job.
- WebSocket fan-out between separate backend instances is not validated.
  Transactional timeout deduplication is covered independently of transport.
- No matchmaking, spectator runtime, replay UI, or H7 implementation.

## Reproduction and diagnostics

- Backend: `gradlew test exportOnlineFixtures`.
- Client: `client/Validation/RunOnlineMatchTests.ps1`.
- Portrait: isolated Unity batch `Domino.Online.Editor.OnlinePlayModeValidation.Run`.
- Real integration: `gradlew validateOnlineTurnI2` and isolated Unity batch
  `Domino.Online.Editor.OnlineTurnNetworkValidation.Run`. Requires existing
  local Firebase configuration, Firestore access, and loopback Redis.
- The real harness coordinates via ignored `client/Validation/Generated/I2`:
  at `REDIS_OFF_REQUESTED`, stop the local test container and write
  `redis-off.txt`; at `REDIS_RESTORE_REQUESTED`, restore it and write
  `redis-restored.txt`. It waits actual 60-second deadlines and a 65-second
  backend outage, without changing production rule durations.
- The JVM companion uses one temporary Firebase account and deletes it on
  exit; Unity reuses its existing identity. No tokens/hands are written to
  coordination files. Match records created through the normal authenticated
  endpoint remain in Firestore; their IDs are identified in the report as
  validation records (the endpoint's validationData flag is unchanged).
  No wallet or economic credit is performed.
- Safe server logs: `TURN_DEADLINE_CREATED`, `TURN_TIMEOUT_CLAIMED`,
  `AUTO_PLAY_APPLIED`, `MATCH_PLAYER_DISCONNECTED`, `MATCH_PLAYER_RECONNECTED`,
  `MATCH_PLAYER_ABANDONED`; no token logging.

See `ONLINE_TURN_I2_REPORT.txt` for measured results and real test evidence.
