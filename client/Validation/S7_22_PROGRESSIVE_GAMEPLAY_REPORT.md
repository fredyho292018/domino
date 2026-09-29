# SERVER-7 S7-22 — 200 connected / progressive gameplay

**The load experiment remains INCOMPLETE; post-reboot recovery verification is complete.** Run `ffdc097c-c99a-441f-ad1e-f7b23f274927`. The initial 200 authenticated connections passed the 60-second baseline. Scheduled holds at 40/80/120/160 activation finished; the final nominal-200 hold lasted only **4.004 seconds**. Peak actual gameplay was **188 players / 47 simultaneous matches**, not 200 / 50. Early completions were not replaced. All 200 sockets remained connected during load, without abnormal closes, reconnects or heartbeat failures.

## Recovery after the user-reported server reboot

SSH returned. API and Redis are healthy, with the same container identities and CPU/RAM limits, but **both StartedAt values changed**. The legacy gate is closed. The former S7-22 observer processes no longer exist. No new load, restart or configuration change was performed by this continuation.

Final authoritative results: **3 COMPLETED, 47 CANCELLED, 0 ACTIVE, 0 FAILED, 0 UNKNOWN**. Discovery reports baselineReady with no pending LOAD work. An exact-run Redis inventory found **zero run-owned keys and zero run members**, without deleting keys or modifying other runs. A transactionally consistent root/runtime audit and full bounded event audit of all 50 matches found zero correctness violations. All **3 completed Replays passed**, reproducing **1,744 events**. SERVER-6 passed: its five History records and five snapshots remain available, with all three currently eligible Replay manifests accessible.

Before access was restored, reads beyond the nominal recovery interval still showed 47 ACTIVE. Therefore **recovery without restart is not validated**. The final clean state was verified after the user-reported server reboot. Cancellation is a normal lifecycle outcome, not gameplay corruption, but the reboot prevents using this result as proof of uninterrupted recovery. Guest uptime, journal boot timestamps and container wall-clock timestamps did not align cleanly; precise reboot timing or clock adjustment is not inferred from one clock.

## Why the load stopped

The validation coordinator incorrectly converted any `ValueError` from discovery into `CONFIRMED_AUTHORITY_SCOPE_OR_STATE_CORRUPTION`, without a confirming read. This did not satisfy the user's confirmed-corruption-only stop criterion. The discovery tool fetches roots, then scans work/assignments, then separately reads runtime documents. A scan beginning 21:43:08.638 UTC flagged two matches which completed at 21:43:11.257 and 21:43:11.461 UTC. Both classified correctly on fresh reads. A mixed-time root/runtime read is the likely explanation; intermediate excluded document values were not emitted, so that race is an inference rather than directly captured proof. Subsequent transactional reads and event audits found no durable corruption.

Clients stopped at 21:43:41.895 UTC. Later, SSH timed out and an authenticated SERVER-6 check failed. VirtualBox still reported the VM running. The cause of that availability loss remains unresolved; it is not attributed to workload, `vmwgfx`, OOM or a crash from temporal proximity alone. No automatic retry or replacement load was launched.

## Stage measurements

Latency in ms; API CPU as percent of its four-CPU quota. Nominal activation is not concurrent gameplay. Stage 160 reached 160 but lost active population as matches finished; stage 200 is interrupted. The complete single table including all requested resource, latency, throughput and error columns is [stage-table.csv](D:/Fredy/development/2026/domino/client/Validation/Generated/S722/stage-table.csv).

| Activated | Gameplay peak | Gameplay end | Idle end | WS | Match peak | ACK p50 | ACK p95 | ACK p99 | Matchmaking p95 | CPU avg | CPU peak | Hold seconds |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 40 | 40 | 40 | 160 | 200 | 10 | 614 | 1188 | 1561 | 12623 | 23.856 | 61.331 | 120.14 |
| 80 | 80 | 80 | 120 | 200 | 20 | 1257 | 2032 | 3553 | 27220 | 46.059 | 79.499 | 120.13 |
| 120 | 120 | 120 | 80 | 200 | 30 | 1585 | 2983 | 6337 | 42095 | 48.332 | 67.358 | 120.121 |
| 160 | 160 | 156 | 44 | 200 | 40 | 2045 | 3629 | 5002 | 48177 | 49.968 | 82.713 | 120.116 |
| 200 | 188 | 188 | 12 | 200 | 47 | 2421 | 5280 | 5802 | 50201 | 52.369 | 75.261 | 4.004 |

| Activated | Server samples | Server p95 | Prior runtime p95 | Receipt p95 | Get-all p95 | SDK finalization p95 | Tx attempts | Tx retries |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 40 | 548 | 1016.809 | 192.822 | 176.178 | 204.801 | 229.712 | 549 | 1 |
| 80 | 977 | 1823.126 | 375.21 | 333.894 | 446.954 | 422.738 | 977 | 0 |
| 120 | 1452 | 2708.022 | 485.261 | 462.462 | 550.81 | 623.714 | 1452 | 0 |
| 160 | 1789 | 3331.501 | 575.694 | 507.339 | 655.912 | 862.597 | 1789 | 0 |
| 200 | 641 | 4909.836 | 800.034 | 668.871 | 740.531 | 1451.594 | 641 | 0 |

Recovered phase data correlate **5406/5406** client ACKs; server timing drops: **0**. The metrics files each ended in a zero-filled trailing record after the interruption/reboot (2,280 and 1,534 bytes). Original files were preserved; only those invalid trailing records were excluded, with damage recorded in `analysis.json`. Valid phase records cover the executed load. Missing records were not fabricated. SDK finalization is the existing boundary, not pure remote commit latency. Transaction counts refer to measured commands, not all bootstrap/observer Firestore operations. No S7-18B instrumentation was added.

ACK p95 rose **1,188 → 2,032 → 2,983 → 3,629 → 5,280 ms**; the last stage is partial. Matchmaking p95 rose **12,623 → 27,220 → 42,095 → 48,177 → 50,201 ms**. This supports progressive latency degradation. It does not establish an exact queueing knee, technical saturation point, or stability during a 300-second 200-gameplay hold. First/last-minute comparisons at 200 are unavailable. CPU did not cross 95% in the recorded samples. Home references 364.7 Mb/s down / 245.8 Mb/s up are contextual only; network counters are host-wide.

## Capacity interpretation and scope

Connection capacity: 200 during this load. No measured stage satisfied the full previous gameplay SLO. 40/80/120 showed degraded but connection-stable gameplay through their scheduled holds; 80/120 exceeded the 3-second ACK p99 threshold. Nominal 160 also had severe latency but completed players reduced its actual population. Peak 188 is an observation, **not a validated sustainable capacity**. Technical saturation and an unstable gameplay boundary remain unestablished. The later server availability failure cannot be assigned a specific concurrency threshold.

The harness enforced deterministic numeric allocation, a single matchmaking submission per identity and a maximum of 50 unique matches. Nine local tests passed before launch. All 661 product source hashes remain unchanged. Only generated validation artifacts/reporting changed; no product optimization, server configuration change, commit, push or 250-user test. The generator exited and an independent Windows query found zero swarm processes. No repeat load is authorized by this report.

Evidence is retained under `client/Validation/Generated/S722`: raw client/server metrics, discovery before/after reboot, authoritative outcomes, Redis inventory, Replay results, SERVER-6 preservation and `server-after-reboot.json`. The pre-reboot report is preserved as `report-before-reboot.md`.

## Requested output

```text
SERVER-7 S7-22 — 200 CONNECTED / PROGRESSIVE GAMEPLAY
======================================================
RUN_ID=ffdc097c-c99a-441f-ad1e-f7b23f274927
TARGET_CONNECTED_USERS=200
PEAK_CONNECTED_WS=200
CONNECTION_BASELINE_200=PASS
STAGE_40_GAMEPLAY=HOLD_FINISHED;PEAK=40;END=40;HOLD_SECONDS=120.140
STAGE_80_GAMEPLAY=HOLD_FINISHED;PEAK=80;END=80;HOLD_SECONDS=120.130
STAGE_120_GAMEPLAY=HOLD_FINISHED;PEAK=120;END=120;HOLD_SECONDS=120.121
STAGE_160_GAMEPLAY=HOLD_FINISHED;PEAK=160;END=156;HOLD_SECONDS=120.116
STAGE_200_GAMEPLAY=INTERRUPTED;PEAK=188;END=188;HOLD_SECONDS=4.004
PEAK_GAMEPLAY_USERS=188
PEAK_ACTIVE_MATCHES=47
ACK_P95_1S_FIRST_CROSSED_AT=40_ACTIVATED
ACK_P99_3S_FIRST_CROSSED_AT=80_ACTIVATED
MATCHMAKING_P95_10S_FIRST_CROSSED_AT=40_ACTIVATED
CPU_95_FIRST_CROSSED_AT=NOT_OBSERVED_IN_AVAILABLE_SAMPLES
API_RAM_90_FIRST_CROSSED_AT=NOT_OBSERVED_IN_AVAILABLE_SAMPLES
WS_STABILITY_DEGRADATION_FIRST_AT=NOT_OBSERVED_IN_AVAILABLE_SAMPLES
PEAK_CPU_QUOTA=82.713
PEAK_API_RAM=1059586048
PEAK_JVM_HEAP=460824192
PEAK_REDIS_CPU=6.381
PEAK_REDIS_RAM=2189360
PEAK_HOST_RX_MBIT=5.537
PEAK_HOST_TX_MBIT=9.913
RX_DROPS_DELTA=540
TX_DROPS_DELTA=0
LOAD_GENERATOR_CPU_PEAK=56.485
LOAD_GENERATOR_RAM_PEAK=33521582080
WS_ABNORMAL_CLOSES=0
RECONNECTS=0
HEARTBEAT_FAILURES=0
TRANSACTION_ATTEMPTS=5408
TRANSACTION_RETRIES=1
HTTP_5XX=0
HTTP_429=0
SEQUENCE_REGRESSIONS=0
DUPLICATE_COMMAND_APPLICATIONS=0
INVALID_TURN_ACCEPTANCES=0
UNAUTHORIZED_DELIVERIES=0
MATCH_CORRUPTIONS=0_DETECTED_IN_TRANSACTIONAL_STATE_AND_EVENT_AUDIT
MATCHES_STARTED=50
MATCHES_COMPLETED=3
MATCHES_CANCELLED_DURING_LOAD=0
MATCHES_CANCELLED_DURING_RECOVERY=47
MATCHES_FAILED=0
MATCHES_UNKNOWN=0
ACK_P95_200_FIRST_MINUTE=NOT_AVAILABLE_INCOMPLETE_HOLD
ACK_P95_200_LAST_MINUTE=NOT_AVAILABLE_INCOMPLETE_HOLD
CPU_200_FIRST_MINUTE=NOT_AVAILABLE_INCOMPLETE_HOLD
CPU_200_LAST_MINUTE=NOT_AVAILABLE_INCOMPLETE_HOLD
MEMORY_200_FIRST_MINUTE=NOT_AVAILABLE_INCOMPLETE_HOLD
MEMORY_200_LAST_MINUTE=NOT_AVAILABLE_INCOMPLETE_HOLD
ACTIVE_WS_200_FIRST_MINUTE=NOT_AVAILABLE_INCOMPLETE_HOLD
ACTIVE_WS_200_LAST_MINUTE=NOT_AVAILABLE_INCOMPLETE_HOLD
SATURATION_SHAPE=OTHER_PARTIAL_MONOTONIC_LATENCY_DEGRADATION
CONNECTION_CAPACITY_VALIDATED=200
GAMEPLAY_SLO_VALIDATED_BAND=NONE_AT_TESTED_STAGES
GAMEPLAY_DEGRADED_BUT_STABLE_BAND=40_80_120_DURING_COMPLETED_LOAD_HOLDS_ONLY
GAMEPLAY_SEVERELY_DEGRADED_BAND=80_120_ACK_P99_GT_3S;160_NOMINAL_STAGE_EARLY_COMPLETIONS
GAMEPLAY_TECHNICAL_SATURATION_POINT=NOT_ESTABLISHED
GAMEPLAY_UNSTABLE_POINT=NOT_ESTABLISHED;POST_STOP_SERVER_UNREACHABLE
POST_RECOVERY_ACTIVE_LOAD_MATCHES=0
POST_RECOVERY_PENDING_WORK=0
UNCLASSIFIED_LOAD_MATCHES=0
ORPHAN_SWARM_PROCESSES=0
REPLAY_SAMPLE_SIZE=3
REPLAY_SAMPLE_REQUIRED=3
REPLAY_SAMPLE_RESULT=PASS_1744_EVENTS
SERVER6_REGRESSION=PASS
EMERGENCY_STOP_TRIGGERED=YES_BY_COORDINATOR
EMERGENCY_STOP_REASON=CONFIRMED_AUTHORITY_SCOPE_OR_STATE_CORRUPTION
STOP_REASON_CONFIRMED=NO_OBSERVER_TREATED_UNCONFIRMED_DISCOVERY_AS_CORRUPTION
200_GAMEPLAY_USERS_REACHED=NO
200_GAMEPLAY_USERS_VALIDATED_FOR_SLO=NO
SOURCE_CHANGE=NO
SERVER_CONFIGURATION_CHANGED=NO
COMMIT=NONE
PUSH=NONE
S7_22_COMPLETE=NO
NEXT=S7-22 INTERPRETATION
USER_REPORTED_SERVER_REBOOT=YES
API_AND_REDIS_START_CHANGED=YES
RECOVERY_WITHOUT_RESTART_VALIDATED=NO
POST_REBOOT_RECOVERY_VALIDATED=YES
RUN_OWNED_REDIS_KEYS=0
RUN_REDIS_MEMBERS=0
SERVER_OBSERVERS_RUNNING=NO
POST_RUN_VALIDATION_COMPLETE=YES
```
