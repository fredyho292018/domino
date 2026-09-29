# SERVER-7 S7-20 — 200-user concurrency gap review

## Conclusion

S7-19 launched 200 clients, but only 163 distinct identities received the application's `AUTHENTICATED` confirmation. All 163 were counted as connected together at the observed peak. **The missing 37 never reached authenticated WebSocket admission; earlier disconnections did not create this gap.** This does not establish a server capacity limit of 163.

The evidence establishes incomplete admission, cancellation of entire generator groups after individual fatal failures, and a ramp/hold clock that did not wait for successful admission. Historical tunnel logs localize hundreds of canceled requests to the REST bootstrap preceding WebSocket connection. They do not establish why those requests took too long, or prove a Cloudflare or Firebase quota. The source contains a plausible shared-lock bottleneck in entitlement bootstrap, but the run did not measure lock waiting. Calling that the demonstrated root cause would exceed the evidence.

Review only: no new load, optimization, deployment, configuration change, commit or push. The helper scripts and this report are analysis artifacts. All 241 source files in the existing S7-19 hash manifest still match.

## Evidence and definitions

Primary evidence: `Generated/S719/timeline.jsonl`, ten `group-*.log` files, exact isolated `harness` source, `RunS719.py`, `stages.json`, `analysis.json`, `registry-confirm.json`, `server-resources.jsonl`, `phases.jsonl`, and final recovery evidence. Additional read-only historical API/tunnel log summaries are in `Generated/S720/historical-*.json`. No identities, tokens or credential contents are included in the output.

Reproducible accounting: `Generated/S720/analyze.py`, `accounting-summary.json`, and **`identity-accounting.csv` (200 rows, group/slot references only)**. Alias mapping uses the original deterministic alias algorithm offline, not credentials. Every slot has either a connection record or a failure record. Authoritative participant slots establish match membership.

“Connected” below means application-authenticated WS, not TCP establishment or HTTP 101 alone. The coordinator samples cumulative client counters; timestamps are observations, not exact network-event timestamps or an atomic server-side connection registry snapshot. Counter snapshots can lag their underlying events.

## Exclusive accounting of all 200 identities

| Category | Count | Meaning and precedence |
|---|---:|---|
| CONNECTED_AT_LEAST_ONCE | 163 | Takes precedence over later disconnect or group failure |
| AUTH_FAILED | 1 | Group 09 / slot 03: `WS_AUTH_REJECTED`; application authentication confirmation failed |
| WS_CONNECT_FAILED | 1 | Group 10 / slot 10: `WS_CLOSED_1008` before application authentication completed |
| DISCONNECTED_BEFORE_PEAK | 0 | No previously authenticated client missing at first peak |
| NOT_STARTED | 0 | All 200 client jobs started and produced evidence |
| PROCESS_FAILED | 32 | Remaining 13 never-connected slots in group 09 and 19 in group 10; their group stopped on `CLIENT_FAILED` |
| OTHER | 3 | Group 08 never completed admission before the global stop |
| **TOTAL** | **200** | Mutually exclusive |

These categories describe observed outcomes, not independently proven underlying causes. `PROCESS_FAILED` denotes group cancellation, not an OS crash or proof those users would otherwise have connected. The three OTHER slots have generic failures but no sufficiently specific terminal admission cause.

The harness maps any first WS message other than `AUTHENTICATED` to `WS_AUTH_REJECTED` except its token-expired special case. Therefore the one AUTH_FAILED category is **not proof of invalid Firebase credentials or an explicit server AUTH_FAILED response**. The 1008 category occurs after transport establishment; it is not proof of HTTP-upgrade rejection. Neither should be mislabeled as a proven Firebase authentication failure.

### Authentication and transport counts

- Firebase refresh attempted: **200 distinct identities**, based on run ordering and all-slot evidence; retry-attempt total not recorded.
- Firebase refresh succeeded: **at least 165 distinct identities**. The 163 authenticated clients and two fatal WS-establishment clients passed refresh and the preceding REST steps. Exact total is unknown. The evidence cannot say all 200 authenticated with Firebase.
- Explicitly identified Firebase refresh failures: none in retained categories; actual failures remain unknown because 323 exceptions were flattened to `CLIENT_FAILURE`.
- WS transport attempted/succeeded: **at least 165 distinct identities, at least 166 successful transport establishments including the reconnect**; exact transport counts and failed-upgrade count unknown.
- Application-authenticated WS: **163 identities / 164 connection events** including one reconnect.
- Identified application WS-establishment failures: **2 events**, one unexpected authentication response and one policy close 1008. Additional generic failures cannot be assigned a phase.
- Total recorded errors: **326 = 323 CLIENT_FAILURE + 1 HTTP_502 + 1 WS_AUTH_REJECTED + 1 WS_CLOSED_1008**. Error events are not distinct users.

## Group accounting

AUTH below is confirmed application authentication. Firebase totals beyond these confirmations have the lower bounds described above. Early closes are authenticated-socket close events before global shutdown, including a repeatedly closed socket; they are not all abnormal closes.

| Group | Started | AUTH / distinct WS | Peak simultaneous | Early close events | Matched slots | Stop cause |
|---|---:|---:|---:|---:|---:|---|
| 1 | 20 | 20 | 20 | 5 (4 distinct users) | 20 | Global signal |
| 2 | 20 | 20 | 20 | 0 | 20 | Global signal |
| 3 | 20 | 20 | 20 | 0 | 20 | Global signal |
| 4 | 20 | 20 | 20 | 4 | 20 | Global signal |
| 5 | 20 | 20 | 20 | 0 | 20 | Global signal |
| 6 | 20 | 20 | 20 | 0 | 20 | Global signal |
| 7 | 20 | 20 | 20 | 0 | 20 | Global signal |
| 8 | 20 | 17 | 17 | 0 | 14 | Global signal |
| 9 | 20 | 6 | 6 | 6 | 6 | One client fatal → cancel group |
| 10 | 20 | 0 | 0 | 0 | 0 | One client fatal → cancel group |

At peak the group vector was `[20,20,20,20,20,20,20,17,6,0]`, for both active and cumulative distinct connections. Group 09's failed slot had seven generic failures before its fatal classification; group 10's had five. The failed-group behavior was not a 100-failure exhaustion requirement: `Main.kt` stops a group when **any** client becomes FAILED.

## Timeline, ramp and churn

All times UTC on 2026-09-27.

| Event | Observed time / bound |
|---|---|
| First group process launch | 19:30:14.292831 |
| All first-group jobs observed started | 19:30:20.480919 |
| Last group process launch | 19:39:28.500584 |
| All last-group jobs observed started | 19:39:40.269607 |
| Last new identity connects / first observed peak 163 | 19:40:15.139098, requested stage 200 |
| First four authenticated closes, group 1 | 19:41:04.274177 |
| One successful reconnect, group 1 | 19:41:11.055884 |
| One more close, group 1 | 19:41:45.715834 |
| Four closes, group 4 | 19:42:35.554261 |
| Six closes, group 9 | 19:43:00.820089 |
| Global stop signal | 19:44:35.331538 |
| Process exit confirmation | 19:45:05.364011 |

Individual job start instants were not timestamped. First-client start is bounded by the first-group launch and its first started snapshot; last-client start by the last-group launch and its first started snapshot. Do not replace these bounds with exact fabricated job-start times.

Launch ramp duration: **554.207753 seconds (9m14.208s)**. Stages advance after approximately 60 seconds from launch, not after 20 new authentication confirmations. The final stage lasts approximately 306.8 seconds from its launch, not five minutes with 200 connections. No admission barrier enforces the target.

Before all 200 jobs started: **0 observed authenticated closes**. Before the last *initial* successful connection: **0**. Before the last connection event *including reconnect*: **4**. Thus FIRST_DISCONNECT_BEFORE_LAST_CONNECT is NO for initial admission, YES if the later reconnect is included. The total number of transport attempts is unknown, so a “last transport attempt” timestamp cannot be reconstructed.

Before global shutdown, 15 close events affected 14 distinct previously connected users, with one reconnect: **163 + 1 − 15 = 149** remaining. That explains the end-stage decline, not the initial 37-user gap. The harness's `disconnects=324` is misleading as a socket count: its retry catch increments that counter even before a WS ever existed. Only one explicit abnormal policy close 1008 is classified; the total abnormal-close count is unknown.

## Client lifetime and completion

The actual isolated run used `durationSeconds=1800`, `maxFailures=100`, `requeue=false`, `targetMatches=0`, a hold deadline 1800 seconds after run start, and two-second summaries. A 60-second stage transition does not terminate an old group. None of the 1800-second deadlines explains the observed run end.

After MATCH_FINISHED, the client **awaits GET players/me/matches before entering its heartbeat hold**. A slow/failing History request can therefore enter retry/recovery before hold starts; the hold is not an unconditional connection-retention guarantee. `CancellationException` also escapes the retry handler, including potential coroutine timeout cancellation. These are source mechanisms, not a count of observed timeout causes.

The first completed match finished at 19:40:23.915841160; its group lost four connections about 40 seconds later. The next completed match finished at 19:41:53.980541931 and its group lost four around 42 seconds later. Tunnel logs independently contain 13 canceled History requests beginning 19:40:57. This supports post-completion REST involvement; without per-request correlation it does not prove the exception for each client. The third completed match finished at 19:44:12.632200199, shortly before global stop.

Group-level fail-fast cancellation, rather than an intended stage timeout, stopped groups 9 and 10. Groups 1–8 stopped on the coordinator's global signal. Later shutdown/recovery produced 37 normal CANCELLED matches; those cancellations did not suppress ten already-created matches before the peak.

## Host, server and network evidence

### Windows generator

CPU peak **53.7649%**; physical RAM used peak **33,588,256,768 bytes (31.282 GiB)**; minimum available **575,639,552 bytes (549 MiB)**; minimum commit headroom **925,167,616 bytes (882 MiB)**; peak system process count **449**. Summed swarm RSS peaked at **1,916,710,912 bytes (1.785 GiB)**.

This is measured memory pressure and a possible contributor to scheduling/timeout behavior. It is not proof of allocation failure, paging stalls or a 163-socket limit. Per-generator JVM GC, thread counts, page faults and ephemeral-port occupancy were not captured. There is no classified port-exhaustion exception; generic errors prevent exclusion. CPU alone does not identify a generator hardware ceiling. The proven generator contribution is its lifecycle and group cancellation behavior.

### Server near the peak

Nearest resource sample: 19:40:14.166896, about 0.97 seconds before the coordinator's peak observation; resource collection is not instantaneous.

| Metric | Near peak |
|---|---:|
| API CPU / configured 4-CPU quota | 54.0738% (2.163 cores) |
| API memory / 4-GiB cap | 979,243,008 bytes (~933.9 MiB) |
| JVM heap used | 290,158,336 bytes (~276.7 MiB) |
| JVM heap committed / maximum | 512 MiB / 2 GiB |
| JVM non-heap | 225,126,280 bytes |
| JVM GC lifetime cumulative | 56 collections / 3064 ms; not a pause at peak |
| JVM live / peak threads, nearby agent sample | 196 / 196 |
| Linux host CPU | 74.087% |
| Linux available memory | 10,206,605,312 bytes |
| Redis CPU | 1.1864% |
| Redis used memory / connections | 2,120,272 bytes / 4 |
| Authenticated WS, client counters | 163 |

JVM live threads are not “active request workers.” Server connection-registry count and blocked-thread counts were not sampled. Over the run API CPU peaked at **85.5606%**, maximum 30-second mean **55.6309%**. No hard CPU/memory saturation is established. The code's connection cap is 1000; the evidence does not show it being reached. Healthy status, zero restart/OOM evidence and Redis rejection/eviction counts do not exclude a software queue or lock bottleneck.

### Network / Cloudflare / Firebase

Measured host RX peak **3.5771 Mb/s**, TX **9.4887 Mb/s**. TX is about **3.8604%** of the earlier 245.8-Mb/s upload reference; measured traffic, not the reference alone, supports the finding. During launch→stop-signal there were **428 RX drops**, zero TX drops and zero RX/TX error deltas. Drops are real but not correlated to individual failed clients; bandwidth exhaustion is not demonstrated. A longer exit-confirmation window has different totals and should not be mixed with this window.

Read-only tunnel-log window: 19:30:14–19:45:06. **702 error lines correspond to 351 paired request errors**, not 702 failed requests:

- Bootstrap: **337 context-canceled requests**, 19:34:51–19:44:50.
- Bootstrap: **1 origin EOF**, 19:31:18, consistent in timing with the one client HTTP 502.
- History: **13 context-canceled requests**, 19:40:57–19:44:43.

The 350 cancellations do not prove Cloudflare imposed a timeout or connection quota: canceled callers and shutdown can cause these messages. No WS-route request failure was identified in this filtered log window. API logs had three outbound `send_failure` warnings, not a complete WS-rejection audit. Exact server WS rejection/HTTP-handshake failure count remains unknown. The 1008 client close can arise from multiple policies (including authentication timing); it does not identify which one fired.

Firebase refresh has no specific failure category in the captured output; “all 200 authenticated” and “Firebase limited the run” are both unproven. Most localized request errors are after Firebase refresh, at REST bootstrap. The 323 generic exceptions cannot be individually joined to tunnel requests because the retained evidence lacks the required sanitized request/phase correlation.

### Source-supported backend candidate, not established causality

`PlayerController.bootstrap` calls `PlayerBootstrapService.bootstrap` then `EntitlementService.bootstrap`. The latter is `@Synchronized`. Its synchronized `state(uid)` cache-miss path calls `repository.read(uid)` while holding the shared service monitor. `FirestoreEntitlements.read` waits on a document read with a five-second bound. Policy refresh is also synchronized and may read Firestore. History resolves entitlement limits through the same service. Thus serial waiting across users is possible even with spare CPU, and the candidate is relevant to the two failing REST routes.

No per-request monitor acquisition timing, thread dump or lock profiling from the failing interval establishes whether this candidate dominated. The unchanged source supports a hypothesis to measure, **not an optimization authorization or a definitive backend root cause**.

## Forty matches and the absent ten

The authoritative registry contains **40 unique matches and 160 distinct participant slots**. Groups 1–7 supplied 140 seats, group 8 supplied 14 and group 9 supplied 6. The remaining authenticated clients were **group-08/slot-08, slot-13, slot-14**, in SEARCHING/queue rather than a match.

Therefore **163 = 160 matched + 3 queued**. The absent ten four-player matches correspond to **37 clients never admitted + 3 admitted but unmatched = 40 seats**. There was no fourth unmatched admitted player with which to form match 41. Summing per-process `matchesStarted` would double-count cross-group matches. The evidence does not show ten matches being created and lost.

Final retained S7-19 checks: three FINISHED, 37 CANCELLED, zero active/pending/unclassified run matches and zero orphan generators; replay validation for the three completed matches passed (1813 events), SERVER-6 preservation passed. This review did not replay or re-run those tests.

## Saturation shape and preserved SLO

The following uses the coordinator's per-stage histograms and **end-of-stage** WS count, not peak count. Requested users are not confirmed concurrency.

| Requested users | WS at stage end | Client ACK p95, ms |
|---:|---:|---:|
| 20 | 20 | 582 |
| 40 | 40 | 1280 |
| 60 | 60 | 1438 |
| 80 | 80 | 1531 |
| 100 | 100 | 2235 |
| 120 | 118 | 2847 |
| 140 | 132 | 2685 |
| 160 | 149 | 3355 |
| 180 | 159 | 3712 |
| 200 | 149 (peak 163) | 4860 |

ACK p95 exceeds one second beginning around the **40-user stage**, independently of the connection-capacity question. Different correlation-filtered analysis populations should not be substituted for the coordinator histogram without labeling them.

Classification: **MULTIFACTOR** — increasing request latency/admission backlog, generator failure propagation, insufficient connection-aware hold, and later completion-related churn. Memory pressure is an additional candidate. A hard resource cap, Firebase quota, Cloudflare cap or maximum server capacity of 163 is **NOT_DEMONSTRATED**. It is also incorrect to call the observed run stable at 200 connected users.

## One proposed next test — not executed

**One instrumented 200-connected hold test**, with an admission diagnostic phase in the same run, after separate authorization and preparation:

1. Retain the same API resources, deployed semantics, TEST identity ceiling 200 and no automatic requeue. Add validation-only per-slot monotonic timestamps and sanitized phase/error codes for Firebase refresh, each REST step, WS upgrade, application AUTH, retries, close code and exit reason. Record generator GC/thread/port/memory data and server request/queue/lock-wait evidence for bootstrap/entitlement resolution. Do not tune that subsystem first.
2. Eliminate whole-group cancellation for an isolated client admission failure in the validation harness. Keep heartbeat servicing independent of post-match History fetch, so completion cannot silently defeat the hold. Validate these harness behaviors offline before the one remote run. These are prerequisites, **not changes made in S7-20**.
3. Launch in bounded groups of 20, advancing on confirmed authenticated connections rather than elapsed launch time. Give each group a bounded admission deadline and bounded retries; if it cannot complete, stop admission and report its exact failing subsystem. Do not label that run “200 connected.” Preserve safety/correctness stops and prevent replacement identities from exceeding 200.
4. Start a five-minute hold only after all 200 distinct application confirmations are simultaneously retained. No extra matches/requeue after the initial maximum 50. Collect server/client connection agreement and independent ACK SLO results; 200 connections does not mean acceptable gameplay latency.
5. If a backend policy rejection occurs, record its exact server reason and stop escalation rather than bypass it. End normally, verify lifecycle/Redis convergence and preservation of unrelated matches. No second load run is proposed here.

## Required final fields

```text
SERVER-7 S7-20 200-USER CONCURRENCY GAP
=======================================
TARGET_IDENTITIES=200
TOTAL_CLASSIFIED=200
AUTH_ATTEMPTED=200_DISTINCT_FIREBASE_REFRESH; TOTAL_ATTEMPTS_NOT_MEASURED
AUTH_SUCCEEDED=AT_LEAST_165_DISTINCT_FIREBASE; EXACT_UNKNOWN
AUTH_FAILED=FIREBASE_UNKNOWN; APPLICATION_WS_CONFIRMATION_FAILURE_OBSERVED_1
ALL_200_AUTHENTICATED=NOT_DEMONSTRATED
WS_CONNECT_ATTEMPTED=AT_LEAST_165_DISTINCT; EXACT_UNKNOWN
WS_CONNECT_SUCCEEDED=TRANSPORT_AT_LEAST_165_DISTINCT; APPLICATION_AUTH_163_DISTINCT_164_EVENTS
WS_CONNECT_FAILED=APPLICATION_ESTABLISHMENT_2_OBSERVED; TRANSPORT_FAILURE_TOTAL_UNKNOWN
CONNECTED_AT_LEAST_ONCE=163
TOTAL_USERS_CONNECTED_AT_LEAST_ONCE=163
PEAK_ACTIVE_WS=163
PEAK_UTC=2026-09-27T19:40:15.139098Z
PEAK_REQUESTED_STAGE=200
DISCONNECTS_BEFORE_200_ATTEMPT_COMPLETE=0_BEFORE_ALL_JOBS_STARTED; LAST_TRANSPORT_ATTEMPT_UNKNOWN
DISCONNECTS_BEFORE_LAST_CONNECT=0_BEFORE_LAST_INITIAL; 4_BEFORE_LAST_RECONNECT
ABNORMAL_CLOSES=1_EXPLICIT_1008; TOTAL_UNKNOWN
RECONNECTS=1
RAMP_TOTAL_DURATION=554.207753_SECONDS_LAUNCH_TO_LAUNCH
FIRST_DISCONNECT_BEFORE_LAST_CONNECT=NO_INITIAL_CONNECTIONS; YES_INCLUDING_RECONNECT
GROUP_1=STARTED20_AUTH_WS20_PEAK20_CLOSE_EVENTS5
GROUP_2=STARTED20_AUTH_WS20_PEAK20_CLOSE_EVENTS0
GROUP_3=STARTED20_AUTH_WS20_PEAK20_CLOSE_EVENTS0
GROUP_4=STARTED20_AUTH_WS20_PEAK20_CLOSE_EVENTS4
GROUP_5=STARTED20_AUTH_WS20_PEAK20_CLOSE_EVENTS0
GROUP_6=STARTED20_AUTH_WS20_PEAK20_CLOSE_EVENTS0
GROUP_7=STARTED20_AUTH_WS20_PEAK20_CLOSE_EVENTS0
GROUP_8=STARTED20_AUTH_WS17_PEAK17_CLOSE_EVENTS0
GROUP_9=STARTED20_AUTH_WS6_PEAK6_CLOSE_EVENTS6
GROUP_10=STARTED20_AUTH_WS0_PEAK0_CLOSE_EVENTS0
LOAD_GENERATOR_CPU_PEAK=53.7649_PERCENT
LOAD_GENERATOR_RAM_PEAK=33588256768_BYTES_SYSTEM_USED
LOAD_GENERATOR_BOTTLENECK_EVIDENCE=GROUP_FAIL_FAST_AND_HOLD_DESIGN_PROVEN; MEMORY_PRESSURE_MEASURED; HARDWARE_CAUSALITY_UNPROVEN
SERVER_CPU_PEAK=85.6_PERCENT
SERVER_RESOURCE_LIMIT_EVIDENCE=NOT_DEMONSTRATED
NETWORK_CAPACITY_LIMIT_EVIDENCE=NOT_DEMONSTRATED; RX_DROPS_428
CLOUDFLARE_LIMIT_EVIDENCE=NOT_DEMONSTRATED; REST_CANCELLATIONS_LOCALIZED
FIREBASE_AUTH_LIMIT_EVIDENCE=NOT_DEMONSTRATED
SERVER_WS_REJECTION_COUNT=UNKNOWN; CLIENT_POLICY_CLOSE_1008_OBSERVED_1
SERVER_WS_ACCEPTANCE_LIMIT_EVIDENCE=NOT_DEMONSTRATED
CLIENT_LIFETIME_POLICY=GLOBAL_STOP_OR_GROUP_FATAL_OR_CLIENT_EXIT; HOLD_AFTER_HISTORY; NO_STAGE_EXIT
MATCHES_STARTED=40
MATCH_SEATS=160
MATCH_SEATS_VS_WS_INTERPRETATION=160_MATCHED_PLUS_3_QUEUED
MISSING_10_MATCH_CAUSE=37_NEVER_ADMITTED_PLUS_3_UNMATCHED; UNDERLYING_ADMISSION_DELAY_CAUSE_PARTIAL
163_IS_SERVER_CAPACITY_LIMIT=NOT_DEMONSTRATED
SLO_DEGRADATION_START=AROUND_40_USER_STAGE_ACK_P95_OVER_1S
TECHNICAL_SATURATION_EVIDENCE=REQUEST_LATENCY_AND_ADMISSION_BACKLOG; NO_PROVEN_HARD_CAP
SATURATION_SHAPE=MULTIFACTOR
PROPOSED_NEXT_TEST=ONE_INSTRUMENTED_200_CONNECTED_HOLD_WITH_ADMISSION_BARRIER
NEW_LOAD_EXECUTED=NO
SOURCE_CHANGE=NO
SERVER_CONFIGURATION_CHANGED=NO
COMMIT=NONE
PUSH=NONE
S7_20_SUCCESS=YES_REVIEW_COMPLETE_WITH_EXPLICIT_CAUSAL_LIMITATIONS
NEXT=S7-20 REVIEW
```
