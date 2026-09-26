# S7-01 — Abandoned match lifecycle

Run `275abc49-45e7-40d0-8019-11531e6c8d48`. Read-only diagnosis; no lifecycle fix implemented.

## Authoritative evidence

Fresh targeted TEST reads of the exact 22 IDs agree between root and runtime: 22 IN_PROGRESS; 20 PLAYING, two ROUND_FINISHED. All 88 persisted participants are ABANDONED, zero CONNECTED/RECONNECTED and zero in the intermediate DISCONNECTED state. Thus 88 are disconnected in the ordinary sense, not 88 stored DISCONNECTED enum values. These are authoritative participant states, not a new live socket census. Earlier Redis evidence found no LOAD connection leases.

All 22 have onlineTurnWork documents, none has a turn deadline, none has History. Each has partial replay events, totaling exactly 1,903, unchanged since the preceding observation. No outcome was inferred solely from Redis.

One exact SERVER-6 comparison, `efe78abb-6582-4984-a433-1bef37ddf497`, is FINISHED / MATCH_FINISHED, has four History documents, 946 events, seven rounds and no onlineTurnWork document. Its archived participant connection fields remain CONNECTED; these do not mean four live connections today.

Evidence: `Generated/SERVER7R/s7-01-outcomes.json` and `s7-01-matches.txt`. The administrative reader was extended only to emit diagnostic metadata and read this one known comparison; gameplay/server source was not changed.

## Concrete root cause

`GameCatalogV4Publisher.canonical()` defines PARTNERS_2V2_ONLINE with `DisconnectPolicy(180,false,false)`. Its shared partners rules have no turn policy. `OnlineEngine.startDeadline()` consequently leaves the deadline null; `OnlineMatchService.timeout()` does nothing without a deadline. Auto-play is not a recovery path for these 22 matches. The earlier generic suggestion that timeouts could finish their rounds does not apply to this mode.

`OnlineEngine.connection()` transitions an expired participant to ABANDONED but does not transition the match to a terminal status. `command()` prohibits further commands by that participant. There is no all-participants-abandoned branch, match abandonment timeout or expiry policy. In PLAYING the match cannot advance without commands; in ROUND_FINISHED, NEXT_ROUND also needs a permitted player. The first divergence from normal completion is the absence of an aggregate match transition after the last participant becomes ABANDONED.

Tests in `OnlineTurnTests` explicitly verify the 179/180-second seat boundary, late reconnect rejection and preservation of IN_PROGRESS after individual abandonment. Those tests establish a seat contract, not an all-four-abandoned terminal contract. Duel auto-play tests must not be applied to partners mode.

## Worker and TTL chain

`OnlineTurnWorker.tick()` is a shared scheduled worker, default fixed delay five seconds, not one thread per match. Its due-index claims temporarily re-schedule IDs by 30 seconds. `processDue()` reads the state and calls `SocialPresenceRuntime.observeMatch()` before its terminal-status check. For nonterminal states it checks connections, tries timeout, then calls `refreshDiscovery()`.

`FirestoreOnlineRepository.refreshDiscovery()` deletes durable work only for missing/FINISHED/CANCELLED matches. Otherwise it writes the next presence check at now+30 seconds (or an earlier applicable deadline). Therefore these 22 durable entries perpetually feed the shared worker even though no human can continue them. Exact live executor/queued-task counts were not inspected; the measured count is 22 durable work entries, zero turn deadlines, and 22 members in the prior Redis due-index inventory.

`SocialPresenceRuntime.observeMatch()` queues a per-player observation. ABANDONED participants receive **active=false**; this is not proof of an online user. `maintain()` drains observations on a one-second default schedule (up to 100 per pass, five-second write retry backoff). `RedisPresenceStore.matchActivity()` unconditionally HSETs the projection and PEXPIREs it to 120,000 ms, even when active=false and even at equal version. Recurring discovery normally supplies observations approximately every 30 seconds plus scheduler/storage delay. These are code defaults, not measured per-key intervals.

The previous 150.649-second window already exceeded the 120-second maximum finite projection TTL plus 30-second grace. The same 123 fingerprints remained, with TTLs replenished. No further passive wait is necessary to establish renewal. Of these, 92 activity keys match LOAD identity hashes; 88 participants belong to the registered matches. Attribution of the other four LOAD projections to individual matches remains unresolved; no unrelated-match scan was performed. The renewal implementation is identified, but no per-key live stack trace was collected.

This is a self-sustaining **durable discovery/observation loop**, not a circular dependency in which Redis makes the match active. Firestore's nonterminal state drives work, work drives activity writes, and Redis TTL renewal is a consequence. Expiring or deleting Redis keys would not terminate the durable matches.

## Replay, History and resource classification

Normal `OnlineEngine.finish()` reaches the score target, emits MatchFinished and marks FINISHED. `build()` then produces History; `OnlineRepository.transact()` persists events and History and removes durable work transactionally. The worker forgets terminal due-index hints. Social activity may receive a final false projection, then expire absent further observation. Shared worker threads and global Redis indexes remain intentionally alive.

Replay is assembled from persisted events/rounds; `ReplayService.authorized()` requires FINISHED. There is no separate mandatory finalization job established by this code. All 1,903 events here are incomplete, retained match event streams; no abandonment-specific retention, deletion, expiry or incomplete-finalization policy was found. **Policy: undefined.** No events were modified.

Zero History is consistent with the current FINISHED-only builder. Whether an abandoned match should receive a cancelled History entry is a missing product/lifecycle contract, not justification to fabricate History manually.

Classification: **UNDEFINED_LIFECYCLE** for permanently nonterminal all-abandoned matches and their recurring work. Partial durable events and shared indexes are persistent by implementation. The activity TTL renewal is mechanically expected under that implementation, but its continued lifetime with no eligible participant has no defined terminal contract. Do not label all 92 keys a confirmed storage leak. The resource-retention path is demonstrated; intended abandonment retention is undefined.

## Minimal correction proposal — NOT implemented

Define the all-abandoned terminal policy first, preferably a transactional CANCELLED outcome using existing terminal-state support, without awarding a gameplay winner or silently extending reconnect grace. At the last grace expiry, re-read authoritative state and ensure every human is irreversibly abandoned before applying an idempotent terminal event/revision and removing onlineTurnWork in the same transaction. Preserve a within-grace reconnect. Competing instances must converge on one terminal transition.

Explicitly approve the History and partial Replay policy for that cancellation. Retaining the audit events while labeling the match cancelled is a proposal, not an existing contract. Publish the terminal projection once, remove the match's due-index member, and allow false activity projections to expire. Do not delete global/shared presence keys or remove a user's activity for another active match. Existing 22 matches require a separately authorized migration/cleanup after the behavior is tested; no cleanup is authorized by this diagnosis.

## Next test design — not executed

| Scenario | Required assertions |
|---|---|
| One disconnect/reconnect before grace | Same seat/state, no terminal event, existing deadline policy preserved |
| One beyond grace | One seat-abandoned event, late commands denied, other players unaffected |
| Four disconnect/reconnect within grace | No premature terminal transition; returning eligible participants continue |
| Four beyond grace | Exactly one approved terminal outcome; no score/winner fabrication; test PLAYING and ROUND_FINISHED |
| Grace boundary race | Reconnect versus abandonment at 179/180 seconds uses authoritative transaction and approved boundary |
| Worker termination | No durable work/deadline for terminal match; repeated shared-worker ticks do not renew that match's work |
| Redis cleanup | Final projection expires after 120s+grace; unrelated/live-match activity and shared indexes preserved |
| Partial Replay | Events unchanged or transformed only according to approved policy; cancelled replay availability explicit |
| History | Exactly the approved cancelled/absent History behavior, idempotent under retry |
| Multi-instance/restart | Two workers, stale hints, duplicate callbacks and crash after durable commit yield one outcome and eventual index removal |

## Required result

```text
S7-01 ABANDONED MATCH LIFECYCLE
RUN_MATCHES=22
MATCHES_ACTIVE=22
MATCHES_COMPLETED=0
MATCHES_ABANDONED=0_TERMINAL_MATCH_STATUS
MATCHES_EXPIRED=0
MATCHES_FAILED=0
CONNECTED_PARTICIPANTS=0_PERSISTED_STATE
DISCONNECTED_PARTICIPANTS=88_ALL_ABANDONED_ENUM
RECONNECT_GRACE_PERIOD=180_SECONDS_CODE_CONTRACT
MATCH_ABANDON_TIMEOUT=UNDEFINED
MATCH_EXPIRY_POLICY=UNDEFINED
ALL_PLAYERS_DISCONNECTED_BEHAVIOR=SEAT_ABANDONMENT_WITHOUT_MATCH_TERMINATION
ACTIVE_MATCH_WORKERS=SHARED_WORKER_22_DURABLE_WORK_ITEMS_LIVE_THREAD_COUNT_NOT_MEASURED
ACTIVE_MATCH_TIMERS=0_TURN_DEADLINES
ACTIVE_MATCH_SCHEDULED_TASKS=22_DURABLE_DISCOVERY_ENTRIES_NOT_22_THREADS
TTL_RENEWAL_OWNER=OnlineTurnWorker_TO_SocialPresenceRuntime_TO_RedisPresenceStore.matchActivity
TTL_RENEWAL_INTERVAL=APPROX_30S_DISCOVERY_1S_WRITER_DEFAULTS_NOT_PER_KEY_MEASURED
TTL_RENEWAL_CONDITION=OBSERVED_NONTERMINAL_STATE_EVEN_ACTIVITY_FALSE
TTL_RENEWAL_EXPECTED_WITH_ZERO_CONNECTED_PLAYERS=YES_BY_IMPLEMENTATION_TERMINAL_POLICY_UNDEFINED
SELF_SUSTAINING_ACTIVE_MATCH_LOOP=YES_DURABLE_DISCOVERY_NOT_REDIS_CAUSING_ACTIVE
PARTIAL_REPLAY_MATCH_COUNT=22
PARTIAL_REPLAY_EVENT_COUNT=1903
PARTIAL_REPLAY_ABANDONMENT_POLICY=UNDEFINED
ABANDONED_MATCH_HISTORY_EXPECTED=UNDEFINED_CURRENT_IMPLEMENTATION_FINISHED_ONLY
RUN_HISTORY_RECORDS=0
SERVER6_SERVER7_LIFECYCLE_DIVERGENCE=LAST_SEAT_ABANDONMENT_HAS_NO_TERMINAL_MATCH_TRANSITION
RESOURCE_STATE_CLASSIFICATION=UNDEFINED_LIFECYCLE
ROOT_CAUSE=ALL_ABANDONED_TERMINAL_TRANSITION_MISSING_NONTERMINAL_DISCOVERY_RENEWS_ACTIVITY
LIFECYCLE_CONTRACT_DEFINED=NO_FOR_ALL_ABANDONED_MATCH
SOURCE_CHANGE_REQUIRED=YES_AFTER_POLICY_APPROVAL
PROPOSED_MINIMAL_FIX=IDEMPOTENT_TRANSACTIONAL_TERMINATION_PLUS_APPROVED_HISTORY_REPLAY_POLICY
NEW_LOAD_EXECUTED=NO
REDIS_DATA_DELETED=NO
FIRESTORE_DATA_MUTATED=NO_BY_THIS_TASK
API_RESTARTED=NO
REDIS_RESTARTED=NO
COMMIT=NONE
PUSH=NONE
S7_01_SUCCESS=YES_ROOT_CAUSE_IDENTIFIED_WITH_OBSERVABILITY_LIMITS_DOCUMENTED
SERVER7_100_RERUN_ALLOWED=NO
NEXT=S7-01 REVIEW
```

## S7-01R correction candidate (local implementation; not deployed)

The original diagnosis above is preserved. The authorized correction adopts CANCELLED with the existing CANCELLED finish reason, null winner and unchanged scores. It preserves the current absence of completed-match History for cancellation. Partial events/rounds are retained, one existing-schema MatchFinished event carries the CANCELLED result, and the existing Replay API continues to deny a full replay for non-FINISHED matches. Its legacy denial code remains ACTIVE_MATCH even for CANCELLED; no client protocol or replay schema was changed.

Seat state machine remains CONNECTED → DISCONNECTED (180-second grace) → CONNECTED before expiry, or ABANDONED at/after expiry. Existing RECONNECTED enum support is unchanged. Match states remain CREATED / STARTING / IN_PROGRESS / FINISHED / CANCELLED. The missing transition is now a nonterminal match with a nonempty set of participants all definitively ABANDONED → CANCELLED / MATCH_FINISHED phase. A mere lack of sockets does not satisfy it.

Implementation files:

- `server/domino/src/main/kotlin/com/teamfho/domino/online/OnlineEngine.kt`: the last seat's abandonment and cancellation share one transition; clears current seat and turn times, preserves private state/audit, and emits exactly one terminal event. An idempotent evaluator also handles already-all-abandoned states.
- `server/domino/src/main/kotlin/com/teamfho/domino/online/OnlineMatchService.kt`: server-only evaluation uses the existing sequence check, transaction and deterministic receipt; no endpoint or direct administrative mutation was added.
- `server/domino/src/main/kotlin/com/teamfho/domino/online/OnlineTurnWorker.kt`: stops observing stale terminal hints, evaluates abandonment after normal connection processing, emits a final inactive projection when applicable and forgets the due-index member. Existing repository transaction semantics remove onlineTurnWork for CANCELLED atomically with the event/state. The shared scheduler remains alive for unrelated matches.
- `server/domino/src/test/kotlin/com/teamfho/domino/online/AllAbandonedTests.kt`: single/staggered/all-seat grace, actual play after reconnection, exact boundary, duplicate evaluation, old all-abandoned states in PLAYING/ROUND_FINISHED, timer clearing and partial Replay/History policy.
- `server/domino/src/test/kotlin/com/teamfho/domino/online/AllAbandonedEmulatorTests.kt`: two competing service instances, one committed cancellation/receipt, unchanged prior events, zero History, persisted terminal state and work deletion; concurrent near-boundary reconnect/expiry has a coherent transaction winner.
- `server/domino/src/test/kotlin/com/teamfho/domino/online/AllAbandonedRedisTests.kt`: actual worker, SocialPresenceRuntime writer and RedisPresenceStore against isolated loopback Redis. Verifies active renewal, final false projection, zero renewals from repeated terminal hints, and natural 120-second expiration without deleting keys.

The terminal condition is checked within the same versioned transaction as the last abandonment; another instance cannot commit against an obsolete sequence. Repeated calls either return the prior receipt or no transition. A reconnect before expiry can win and preserve a nonterminal match; expiry winning leaves an abandoned participant, never a revived seat in a cancelled match. Grace remains 180 seconds.

Validation evidence is under ignored `Generated/S701R/`. The complete unit suite passed: 649 discovered, 625 executed, 24 conditionally skipped, zero failures/errors. Redis was separately enabled and passed its real 120-second natural-expiration test. Unit Replay and normal completion regressions run without real Firebase access. Firestore tests use explicit demo project, loopback emulator and EmulatorCredentials. Initial test setup exposed cross-test residue; owned emulator process cleanup and test fixture cleanup were corrected, without changing production logic to satisfy tests.

The original 22 TEST matches and real SERVER-6 archives were not read or modified during implementation validation; there was no server deployment. Existing user files were hash-checked against a 123-file pre-task baseline and preserved, except this requested report append. Whitespace validation passed for the three production files. No capacity load, settings changes, commit or push occurred.

Controlled migration proposal, **not executed**: separately authorize a deployment and an allowlisted run of the same idempotent evaluator against the exact 22 IDs after rechecking state, all-seat abandonment and TEST scope. Record before/after event sequences and cancellation results, verify work removal and TTL convergence, preserve partial audit data and SERVER-6 archives, and stop on any identity/state mismatch. Deploying this worker would itself discover existing all-abandoned work, so deployment approval must explicitly include that effect; no deployment is part of S7-01R.

Final emulator rerun: **8 tests executed, zero failures/errors/skips**, including the existing normal-completion persistence regression and empty-feed 60-second idle check. Pending emulator work after the suite: zero. The separately enabled Redis test passed (one test, zero failures). No remote service was used for these validations.

```text
SERVER-7 S7-01R ABANDONED MATCH FIX
ROOT_CAUSE=ALL_SEATS_ABANDON_WITHOUT_MATCH_TERMINATION
FILES_CHANGED=3_PRODUCTION_FILES_3_NEW_TEST_FILES_1_REPORT_APPEND
RECONNECT_GRACE_PERIOD=180_SECONDS
RECONNECT_GRACE_CHANGED=NO
ALL_ABANDONED_MATCH_TERMINAL_STATE=CANCELLED
ALL_ABANDONED_COMPLETION_REASON=CANCELLED
ALL_ABANDONED_HISTORY_POLICY=NO_NORMAL_COMPLETED_HISTORY
PARTIAL_REPLAY_ABANDONMENT_POLICY=RETAIN_AUDIT_EVENTS_CANCELLED_TERMINAL_EVENT_FULL_REPLAY_UNAVAILABLE
ALL_ABANDONED_TERMINATION=PASS
TERMINATION_AFTER_LAST_ABANDONMENT=YES
ABANDON_TERMINATION_IDEMPOTENT=YES
SINGLE_DISCONNECT_RECONNECT=PASS
SINGLE_ABANDONMENT=PASS
ALL_DISCONNECT_RECONNECT_WITHIN_GRACE=PASS
ALL_ABANDONED_AFTER_GRACE=PASS
STAGGERED_ABANDONMENT=PASS
RECONNECT_BOUNDARY_RACE=PASS
MULTI_INSTANCE_ABANDON_TERMINATION=PASS
ABANDONED_MATCH_WORKER_TERMINATES=YES_PER_MATCH_WORK_SHARED_WORKER_REMAINS
ABANDONED_MATCH_TURN_TIMERS=0
ABANDONED_MATCH_REDIS_RENEWAL_STOPS=YES
REDIS_ABANDONED_MATCH_CONVERGENCE=PASS_NATURAL_120_SECOND_TTL
NORMAL_MATCH_COMPLETION_REGRESSION=PASS
SERVER6_HISTORY_REPLAY_REGRESSION=PASS_AUTOMATED_NO_REAL_MATCH_MUTATION
ORIGINAL_22_MATCHES_MUTATED=NO
REAL_PRODUCTION_FIRESTORE_CALLS=0
NEW_LOAD_EXECUTED=NO
SERVER7_100_RERUN_STARTED=NO
SERVER7_250_STARTED=NO
PREEXISTING_USER_FILES_PRESERVED=YES_EXCEPT_AUTHORIZED_REPORT_APPEND
UNEXPECTED_FILES=NONE_IDENTIFIED
COMMIT=NONE
PUSH=NONE
S7_01R_SUCCESS=YES
SERVER7_100_RERUN_ALLOWED=NO
NEXT=S7-01R REVIEW
```
