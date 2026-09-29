# SERVER-7 S7-21 — 200 connected users hold

## Result

Connection-capacity result: **YES**. Peak authenticated/stable WS: **200**. Longest continuous sampled 200-client stable interval: **300.327 seconds**. No matchmaking or gameplay commands were sent. This does **not** validate 200 concurrent gameplay users; S7-19's ACK degradation around 40 users remains a separate finding.

## Harness changes and local tests

The reviewable entry point is `S721ConnectOnly.kt`; deterministic tests are `S721ConnectOnlyTest.kt`. They are isolated validation files, copied into a separate generated harness build. Existing backend, Unity and gameplay swarm sources were not edited. `S721JvmAgent.java` is a bounded metrics-only JVM agent: no class transformation, request behavior, JVM flag or server resource change.

Each client owns its request futures, identity lease, socket and child coroutine. A supervisor isolates exceptions; normal individual failure cannot cancel the other clients. A global stop cancels all children. Identity-scope violations or unexpected match delivery trigger the integrity stop. Shared HTTP transport is closed only after all clients terminate.

Three deterministic tests passed: client 7 bootstrap timeout with 19 surviving/heartbeating clients and at most two attempts; global stop closing all 20; bounded phase-specific failure classification. Compilation and tests completed before the remote run. No credentials were used by these tests.

Fixed experiment policy: one JVM, ten groups of 20; group starts every 30 seconds, slot starts staggered 1.5 seconds inside each group. Maximum two total establishment attempts per identity, five-second retry delay, no infinite retries; fatal policy/auth failures may stop after one. Firebase refresh and bootstrap have 30-second request deadlines, WS upgrade 15 seconds, application AUTH 15 seconds. Heartbeat every ten seconds with a 30-second reply deadline. A client becomes WS_STABLE only after AUTHENTICATED and a successful PONG. No History fetch, queue lookup, queue join, gameplay command or display-name update is in this entry point.

The hold clock begins at 200 stable connections; the continuous-200 clock resets if the stable count drops. The bounded fallback retains achieved connections for 300 seconds if admission cannot complete; latency alone is not an emergency. Configured emergency checks cover OOM/host instability, crash loops, Redis OOM/integrity failure, authorization violation and host unavailability. No pacing or resource tuning occurred during the run.

## S7-19 cancellation audit

**Root cause: INCONCLUSIVE at individual-request level.** The old `await()` request futures participate in their client's coroutine cancellation. Old group orchestration cancels all children when any client becomes FAILED; global stop also cancels them. HTTP request timeouts and coroutine timeouts provide additional cancellation paths. S7-20 found bootstrap cancellations before the late group-failure stops, so group cascade cannot explain every canceled request. The retained old logs lack request/phase correlation and must not be retrospectively relabeled as proven Firebase, Cloudflare or backend-lock failures.

The new mode separates client lifecycles, logs slot/attempt/phase/state with timestamps, and distinguishes bootstrap timeout, HTTP, authentication, DNS/TCP/TLS, handshake, WS authentication, server close and other exceptions. Intentional shutdown produces STOPPED/CANCELLED_BY_GLOBAL_STOP control records, not failed-admission counts. All reports use numeric slots, never UIDs or tokens.

## Admission and hold

All 200 existing dedicated LOAD identity files were scope-validated and checked for uniqueness without provisioning. Functional TEST users were excluded from load; bounded SERVER-6 history/snapshot/replay reads ran separately before and after.

| Stable authenticated clients | Seconds from client-run clock |
|---:|---:|
| 20 | 30.441 |
| 40 | 60.484 |
| 60 | 90.521 |
| 80 | 120.555 |
| 100 | 150.597 |
| 120 | 180.631 |
| 140 | 210.667 |
| 160 | 241.705 |
| 180 | 270.745 |
| 200 | 300.777 |

Firebase AUTH success: 200; bootstrap success: 200; distinct WS connected: 200. Bootstrap latency from paired start/success timestamps: p50 **902 ms**, p95 **1272 ms**, maximum **1897 ms**. Successful heartbeat responses: **9051**; p95 **147 ms**. Heartbeat failures: **0**, abnormal closes: **0**, reconnects: **0**. Failure counts in the final block count observed failure events; distinct failed identities are retained separately in `identity-outcomes.csv`.

The one-second client summary measures simultaneous application-authenticated connections with active heartbeat processing. It is not an atomic backend registry dump. `server-tcp.jsonl` provides separate server-network-namespace established TCP observations on port 8080; these include HTTP and are explicitly **not** mislabeled as authenticated WS counts. No finite polling proves absence of an unobserved sub-sample network interruption; no such disconnect/reconnect was recorded by the clients.

## Resources and evidence limits

| Measure | Result |
|---|---:|
| API peak CPU, normalized to unchanged four-CPU quota | 29.6620% |
| API memory peak | 982.71 MiB of 4096 MiB |
| JVM heap peak | 428.74 MiB |
| JVM thread peak | 71 |
| JVM GC during sampled run | 3 collections / 147 ms |
| Redis peak CPU / used memory | 5.0230% / 2.02 MiB |
| Linux minimum available RAM | 9641.46 MiB |
| Host peak RX / TX | 1.1569 / 0.2537 Mb/s |
| RX / TX drop deltas | 300 / 0 |
| RX / TX error deltas | 0 / 0 |
| Windows CPU peak | 44.6087% |
| Windows physical RAM used peak | 31225.38 MiB |
| Windows minimum available / commit headroom | 1355.86 / 4440.66 MiB |
| Windows process / system-thread peaks | 434 / 9322 |
| Generator JVM thread peak | 13 |
| Generator GC | 7 collections / 23 ms |
| System unique high TCP local ports peak | 302 |

The locally queried IPv4 and IPv6 dynamic TCP range was 49152–65535 (16384 ports). The high-port observation counts unique local TCP ports system-wide, not exact allocator occupancy or all possible per-destination tuples. It is supporting evidence, not a proof excluding every transient socket issue. Windows host CPU/RAM includes other applications. Server CPU/GC and network samples were read at approximately two-second cadence; 301 fresh JVM samples / 301 resource rows covered the analyzed window. Peaks are sample peaks. No game-command ACK SLO applies to CONNECT_ONLY.

Tunnel historical request-error counts: `{}`. API outbound send-failure log count: 0. These counts do not establish a Cloudflare quota. No runtime instrumentation changed product behavior or Firestore transaction logic.

## Baseline, shutdown and preservation

Before connection load: healthy API/Redis, zero active/unclassified LOAD matches, no pending LOAD work according to authoritative discovery, zero orphan swarm processes, and SERVER-6 PASS. The discovery window was explicitly anchored to this test rather than inheriting S7-19 timestamps.

Shutdown reason: **HOLD_200_COMPLETE**. Final client active count **0**, orphan generator processes **0**. Post-run authoritative discovery confirms zero newly created LOAD matches and no active/pending/unclassified LOAD matches. No match cleanup was needed or performed. API/Redis container IDs, start times, restart counts and resources remained unchanged; legacy gate stayed closed. The temporary observer and metrics agent were stopped. SERVER-6 after-check: **PASS**, five history matches and five snapshots preserved, 3 eligible replay manifests verified.

Source verification compared 661 files against the pre-run manifest; differences: **0**. No product commit/push, server deployment, resource adjustment, Cloudflare or DNS change occurred. No gameplay follow-up was started.

## Artifacts

- `Generated/S721/clients.jsonl`: per-slot lifecycle, attempts, heartbeat, one-second summaries.
- `Generated/S721/identity-outcomes.csv`: exactly 200 classified slots, first/last failure and final outcome.
- `Generated/S721/analysis.json`, `timeline.jsonl`, `resources.jsonl`, `server-tcp.jsonl`: metrics and accounting.
- `Generated/S721/before-authority.json`, `after-authority.json`, `server6-before.json`, `server6-after.json`, `server-final.json`: bounded preservation evidence.
- `Generated/S721/harness/build/test-results/test/TEST-com.teamfho.swarm.S721ConnectOnlyTest.xml`: local tests.

## Required final output

```text
SERVER-7 S7-21 200 CONNECTED USERS HOLD
=======================================
HARNESS_FILES_CHANGED=S721ConnectOnly.kt; S721ConnectOnlyTest.kt; S721JvmAgent.java; Generated/S721 operational and analysis helpers
HARNESS_TESTS=3_PASS_0_FAILURES
CLIENT_FAILURE_ISOLATION=YES
GROUP_FAILURE_CASCADE_TEST=PASS
S7_19_BOOTSTRAP_CANCELLATION_ROOT_CAUSE=INCONCLUSIVE; harness group cancellation and request timeout mechanisms identified, per-request causal correlation absent
MODE=CONNECT_ONLY
TARGET_IDENTITIES=200
BOOTSTRAP_ATTEMPTED=200
BOOTSTRAP_SUCCEEDED=200
BOOTSTRAP_FAILED=0
AUTH_SUCCEEDED=200
AUTH_FAILED=0
WS_CONNECT_ATTEMPTED=200
WS_CONNECTED_AT_LEAST_ONCE=200
WS_FAILED=0
PEAK_ACTIVE_WS=200
PEAK_STABLE_AUTHENTICATED_WS=200
TIME_TO_20=30441_MS
TIME_TO_40=60484_MS
TIME_TO_60=90521_MS
TIME_TO_80=120555_MS
TIME_TO_100=150597_MS
TIME_TO_120=180631_MS
TIME_TO_140=210667_MS
TIME_TO_160=241705_MS
TIME_TO_180=270745_MS
TIME_TO_200=300777_MS
WS_STABLE_200_DURATION=300327_MS
HEARTBEAT_FAILURES=0
WS_ABNORMAL_CLOSES=0
RECONNECTS=0
FAIL_BOOTSTRAP_TIMEOUT=0
FAIL_BOOTSTRAP_HTTP=0
FAIL_AUTH=0
FAIL_TOKEN=0
FAIL_DNS=0
FAIL_TCP=0
FAIL_TLS=0
FAIL_WS_HANDSHAKE=0
FAIL_WS_AUTH=0
FAIL_SERVER_CLOSE=0
FAIL_CLIENT_EXCEPTION=0
FAIL_OTHER=0
UNCLASSIFIED_IDENTITIES=0
PEAK_API_CPU=29.662_PERCENT_OF_4_CPU_QUOTA
PEAK_API_RAM=1030447104_BYTES
PEAK_JVM_HEAP=449567064_BYTES
PEAK_REDIS_CPU=5.023_PERCENT
PEAK_REDIS_RAM=2118568_BYTES
MIN_HOST_AVAILABLE_RAM=10109808640_BYTES
PEAK_HOST_RX_MBIT=1.1569
PEAK_HOST_TX_MBIT=0.2537
RX_DROPS_DELTA=300
TX_DROPS_DELTA=0
LOAD_GENERATOR_CPU_PEAK=44.6087
LOAD_GENERATOR_RAM_PEAK=32742178816_BYTES_SYSTEM_USED
LOAD_GENERATOR_BOTTLENECK_EVIDENCE=NOT_DEMONSTRATED_IN_THIS_RUN
CLOUDFLARE_LIMIT_EVIDENCE=NOT_DEMONSTRATED; see historical log counts
API_WS_LIMIT_EVIDENCE=NOT_OBSERVED_AT_200
MATCHMAKING_STARTED=NO
MATCHES_STARTED=0
POST_TEST_ACTIVE_WS=0
ORPHAN_SWARM_PROCESSES=0
SERVER6_REGRESSION=PASS
200_CONNECTION_CAPACITY_VALIDATED=YES
200_GAMEPLAY_USERS_VALIDATED=NO
EMERGENCY_STOP_TRIGGERED=NO
EMERGENCY_STOP_REASON=NONE
SOURCE_CHANGE_PRODUCT=NO
SERVER_CONFIGURATION_CHANGED=NO
COMMIT=NONE
PUSH=NONE
S7_21_SUCCESS=YES
NEXT=S7-21 REVIEW
```
