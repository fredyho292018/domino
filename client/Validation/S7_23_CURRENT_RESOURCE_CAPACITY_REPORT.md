# SERVER-7 S7-23 — Current resource capacity limit

Run `67e67bb6-86f2-4a44-a68d-803ef846ff1d`. **SLO gameplay limit: 40_TESTED**. **Stable gameplay limit: 40_TESTED**. Provisional operating recommendation: **40_TESTED**. These are measured stages, not a theoretical maximum or interpolated boundary.

The connection baseline held 200 authenticated sockets for 60 seconds before gameplay. Resource limits stayed at four API CPUs, 4 GiB and four JVM processors. Only the isolated validation harness changed. No backend/client product optimization, redeploy, server configuration change, commit or push occurred.

## Stage results

ACK/phase/resource figures use the hold interval; matchmaking describes the newly admitted cohort immediately preceding that hold. A missing matchmaking measurement is not assumed to pass. Full-stage and first/middle/last-minute distributions remain in `analysis.json`. Latency in milliseconds; CPU percent of quota; memory values in bytes. Full required single table: [stage-table.csv](D:/Fredy/development/2026/domino/client/Validation/Generated/S723/stage-table.csv).

| Activated | Gameplay peak | End | Hold minimum | WS | ACK p95 | ACK p99 | MM p95 | CPU avg | CPU peak | SLO | Stability | Full target held |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|---|---|
| 40 | 40 | 40 | 40 | 200 | 868 | 1247 | 7818 | 13.679 | 36.466 | SLO_PASS | STABILITY_PASS | True |
| 80 | 80 | 76 | 76 | 200 | 1333 | 2751 | 10274 | 27.253 | 54.954 | SLO_FAIL | STABLE_OBSERVED_POPULATION_TARGET_NOT_HELD | False |
| 120 | 116 | 108 | 108 | 200 | 1357 | 1707 | 15927 | 36.839 | 61.881 | SLO_FAIL | STABLE_OBSERVED_POPULATION_TARGET_NOT_HELD | False |
| 128 | 116 | 108 | 108 | 200 | 1734 | 2209 | 9757 | 40.079 | 72.284 | SLO_FAIL | STABLE_OBSERVED_POPULATION_TARGET_NOT_HELD | False |
| 136 | 116 | 84 | 84 | 200 | 1303 | 1541 | 7270 | 31.494 | 52.173 | SLO_FAIL | STABLE_OBSERVED_POPULATION_TARGET_NOT_HELD | False |
| 144 | 84 | 20 | 20 | 200 | 1052 | 1309 | 6785 | 15.444 | 47.854 | SLO_FAIL | STABLE_OBSERVED_POPULATION_TARGET_NOT_HELD | False |

Natural completions were not replaced. A stage may pass stability for its actual workload but is **not eligible to establish a concurrency limit at its nominal target** unless that population persisted throughout sampled hold observations. This avoids claiming 144 active-player capacity from 144 cumulative activations. Stability here requires healthy containers, no restarts/OOM, intact sockets and correctness, no 5xx/Redis evictions/rejections, and clean final recovery. Thresholds remained ACK p95 1,000 ms, p99 3,000 ms, matchmaking p95 10,000 ms. Latency failures never aborted load.

A concurrency level excluded because matches completed normally is **not an instability threshold**. If the nominal upper stages lose their target populations, the experiment establishes a verified lower operating level but cannot establish the server's practical maximum above it. The `PARTIAL_CONCURRENCY_COVERAGE` status makes that limitation explicit.

The operating recommendation uses a tested SLO-passing stage when available. If none passes SLO, it is explicitly a provisional degraded-service choice from the lowest stable tested workload, favoring lower latency and resource reserve over the highest surviving load; it does not meet the original SLO. CPU headroom uses 100 minus observed peak quota usage; memory headroom uses the configured limit minus peak usage. Network capacity was not measured, so a speed-test reference is not converted into guaranteed headroom.

## Coordinator correction and integrity

Non-atomic discovery mismatches now create a pending suspicion and invoke the existing `CapacityOutcomeKt` transactionally paired root/runtime read and bounded event audit. Only confirmed paired mismatch/event corruption causes the coordinator's corruption stop; failed/missing/malformed confirmation is recorded as inconclusive. The actual two-match S7-22 fixture, confirmed corruption and inconclusive-read cases passed four regression tests; all 50 harness tests passed. No product code was modified.

Live paired-confirmation batches: **2**; consistent batches: **2**. These confirmations prevented unpaired observations from being mislabeled as corruption. Details are preserved in `confirmation-log.jsonl` and bounded paired-read artifacts.

The allocation ceiling was 144 active slots and 36 unique matches with one matchmaking submission per identity; 56 users remained idle. Existing command phase timing was reused; no S7-18B. Client/server phase correlations: 9950/9950; timing drops: 0. SDK finalization is not presented as pure remote commit. Authority reads add bounded measurement traffic. Metrics are sampled, not continuous proofs between samples.

## Recovery and preservation

The clients disconnected without restarting API or Redis. Final discovery after abandonment 180 seconds + Redis TTL 120 seconds + grace was confirmed by a second read and exact-run Redis inventory. Outcomes: {'COMPLETED': 31, 'CANCELLED': 5, 'FAILED': 0, 'ACTIVE': 0, 'UNKNOWN': 0}. Full event audit passed. Replay sample: 5, result PASS. SERVER-6: PASS. Observer cleanup and unchanged server/container/resource evidence are retained in `server-final.json`. Product source differences: [].

## Required output

```text
SERVER-7 S7-23 — CURRENT RESOURCE CAPACITY LIMIT
================================================
API_CPU_LIMIT=4
API_MEMORY_LIMIT=4_GIB
CONNECTED_WS_BASELINE=PASS_200_FOR_60S
CONNECTION_CAPACITY_VALIDATED=200
STAGE_40=SLO_PASS;STABILITY_PASS;FULL_TARGET_HELD=True
STAGE_80=SLO_FAIL;STABLE_OBSERVED_POPULATION_TARGET_NOT_HELD;FULL_TARGET_HELD=False
STAGE_120=SLO_FAIL;STABLE_OBSERVED_POPULATION_TARGET_NOT_HELD;FULL_TARGET_HELD=False
STAGE_128=SLO_FAIL;STABLE_OBSERVED_POPULATION_TARGET_NOT_HELD;FULL_TARGET_HELD=False
STAGE_136=SLO_FAIL;STABLE_OBSERVED_POPULATION_TARGET_NOT_HELD;FULL_TARGET_HELD=False
STAGE_144=SLO_FAIL;STABLE_OBSERVED_POPULATION_TARGET_NOT_HELD;FULL_TARGET_HELD=False
SLO_GAMEPLAY_LIMIT=40_TESTED
STABLE_GAMEPLAY_LIMIT=40_TESTED
RECOMMENDED_OPERATING_LIMIT=40_TESTED
ACK_P95_AT_SLO_LIMIT=868
ACK_P95_AT_STABLE_LIMIT=868
CPU_AVG_AT_STABLE_LIMIT=13.679
CPU_PEAK_AT_STABLE_LIMIT=36.466
CPU_HEADROOM_AT_STABLE_LIMIT=63.534
API_RAM_AT_STABLE_LIMIT=1204674560
MEMORY_HEADROOM_AT_STABLE_LIMIT=3090292736
REDIS_CPU_AT_STABLE_LIMIT=4.336
REDIS_HEADROOM_AT_STABLE_LIMIT=1071727968
NETWORK_TX_AT_STABLE_LIMIT=4.437
NETWORK_HEADROOM_AT_STABLE_LIMIT=NOT_MEASURED;HOME_REFERENCE_ONLY
TRANSACTION_ATTEMPTS=9950
TRANSACTION_RETRIES=0
HTTP_5XX=0
HTTP_429=0
WS_ABNORMAL_CLOSES=0
RECONNECTS=0
HEARTBEAT_FAILURES=0
SEQUENCE_REGRESSIONS=0
DUPLICATE_COMMAND_APPLICATIONS=0
INVALID_TURN_ACCEPTANCES=0
UNAUTHORIZED_DELIVERIES=0
MATCH_CORRUPTIONS=0
MATCHES_STARTED=36
MATCHES_COMPLETED=31
MATCHES_CANCELLED_DURING_LOAD=0
MATCHES_CANCELLED_DURING_RECOVERY=5
MATCHES_FAILED=0
MATCHES_UNKNOWN=0
ACK_P95_144_FIRST_MINUTE=1227
ACK_P95_144_LAST_MINUTE=602
CPU_144_FIRST_MINUTE=27.454
CPU_144_LAST_MINUTE=6.741
MEMORY_144_FIRST_MINUTE=1234890752
MEMORY_144_LAST_MINUTE=1230667776
ACTIVE_GAMEPLAY_144_FIRST_MINUTE={'count': 30, 'min': 68, 'p50': 76, 'p95': 80, 'p99': 80, 'max': 80, 'mean': 74.8}
ACTIVE_GAMEPLAY_144_LAST_MINUTE={'count': 29, 'min': 20, 'p50': 24, 'p95': 24, 'p99': 24, 'max': 24, 'mean': 22.896551724137932}
PERFORMANCE_TREND_AT_144=RECOVERING_WITH_DECLINING_ACTIVE_POPULATION_NOT_FIXED_LOAD
POST_RECOVERY_ACTIVE_MATCHES=0
POST_RECOVERY_PENDING_WORK=0
UNCLASSIFIED_LOAD_MATCHES=0
ORPHAN_SWARM_PROCESSES=0
REPLAY_SAMPLE_RESULT=PASS
SERVER6_REGRESSION=PASS
LATENCY_LIMITING_FACTOR=FIRESTORE_READ_PATH
RESOURCE_LIMITING_FACTOR=NO_MEASURED_RESOURCE_EXHAUSTION
160_GAMEPLAY_STARTED=NO
200_GAMEPLAY_STARTED=NO
250_GAMEPLAY_STARTED=NO
SERVER_CONFIGURATION_CHANGED=NO
COMMIT=NONE
PUSH=NONE
S7_23_SUCCESS=PARTIAL_CONCURRENCY_COVERAGE
NEXT=S7-23 CAPACITY INTERPRETATION
ACTUAL_PEAK_GAMEPLAY=116
144_ACTIVE_HOLD_VALIDATED=NO
STAGE_SCHEDULE_COMPLETED=YES
PAIRED_CONFIRMATION_BATCHES=2
PAIRED_CONFIRMATION_CONSISTENT_BATCHES=2
MIXED_TIME_OBSERVATION_TRIGGERED_STOP=NO
MEAN_FIRESTORE_READ_MS_AT_STABLE_LIMIT=215.201
MEAN_SDK_FINALIZATION_MS_AT_STABLE_LIMIT=100.091
```
