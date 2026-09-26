# SERVER-7R — capacity measurement review

Run: `275abc49-45e7-40d0-8019-11531e6c8d48`. Source HEAD remains `1d0c3bf3fc5b1aba427b5ab0861372895a7c9e38`.

## Findings

The current API cgroup has an effective quota of **4 CPU** on an **8-vCPU VM**. Docker NanoCpus is 4000000000; CpuQuota and CpuPeriod are zero because NanoCpus supplies the limit. The effective cgroup value is `400000 100000`, the parent is unlimited, and the effective cpuset is `0-7`. Container-visible processor count is 8. The running JVM's availableProcessors has **not** been directly measured; container-visible count must not be substituted for it.

The original collector used sequential `docker stats --no-stream` commands, parsed CPUPerc and removed `%`. Its nominal five-second loop took longer under load. It did not retain Docker's raw CPU/system counter deltas. Docker's Linux formula is `(container CPU delta / system CPU delta) * online CPU count * 100`; 100% denotes one CPU-equivalent, not the whole quota. See [Docker CLI implementation](https://github.com/docker/cli/blob/master/cli/command/container/stats_helpers.go). This source documents the formula, not the exact installed binary version.

842.04% would nominally mean 8.4204 CPU-equivalents. It is inconsistent with a sustained 4-CPU quota and even exceeds the currently visible eight CPUs. It is **not evidence that the container consumed 210.51% of its quota**. Historical raw counters and concurrent effective quota were not retained, so the cause and historical saturation remain indeterminate. The protective stop executed correctly against its configured input; that does not validate the CPU input.

Independent idle observation succeeded: 30 monotonic intervals, 150.649 seconds overall, average 0.09602 CPU-equivalents, peak 0.20932. Mean quota utilization was approximately 2.4004%, peak 5.2330%. No increase in nr_throttled between first and last sampled counters. Formulas explicitly report cores used, cores*100 (Docker-style CPU-equivalent percentage), cores/hostCPUs*100, and cores/effectiveQuota*100. The host-capacity fraction consumed by this container is not total host utilization. A deterministic four-core fixture and actual idle counter checks passed. No stress or gameplay was run.

## Redis and expiry

Both inventories contain the same 123 key fingerprints; no unknown key prefixes. Metadata only was retained, without raw keys, identities or values.

| Class | Count | TTL policy / observed range | Interpretation |
|---|---:|---|---|
| presence.activity | 116 | 120s renewed; first 91.020–113.328s, second 88.148–119.993s | 92 match dedicated LOAD identity hashes; activity projection, not connected socket proof |
| presence.connection | 1 | 60s; 47.901s then 57.442s | Unrelated live connection |
| presence.player | 1 | lease-derived; 47.898s then 57.437s | Unrelated player lease |
| presence.players | 1 | Persistent shared sorted set | Zero LOAD members |
| mm.order | 1 | Persistent | Shared monotonic queue order |
| mm.queues | 1 | Persistent | Shared queue names |
| turn.due | 1 | Persistent shared sorted set | Contains all 22 registered match IDs |
| turn.leader | 1 | Renewable 30s lease | Healthy service leadership; not expected to disappear on client exit |

The observed maximum finite TTL is 120 seconds. The second observation covered 120 seconds plus 30 seconds grace. The activity keys survived because their TTLs were renewed, not because Redis ignored expiration. No LOAD connection/player lease, matchmaking user entry, reservation or job remained. Authorization handles are in process memory with 30-second validity; their current count cannot be inferred from Redis and was not inspected.

92 keys are attributable to the dedicated LOAD population, plus 22 members of the shared turn index. The pre-load key fingerprints were not recorded, so the arithmetic delta of 92 is consistent with, but does not independently prove, a one-to-one historical creation delta. Four more LOAD activity identities exist than participants in the exact 22 registered matches; their per-match attribution remains bounded but unresolved. The other 31 current keys must not be attributed to this run.

Confirmed Redis storage leaks: **0 confirmed**, not proof of absence. Persistent indexes and renewable activity state have architectural explanations. The continued lifecycle of abandoned matches needs review before any cleanup is proposed as an executable operation.

## Exact registered match outcomes

Targeted administrative TEST Firestore reads checked all 22 registered IDs; no scans of unrelated matches and no writes. Root status agrees with runtime authoritative status for all 22: **ACTIVE**, with 88/88 participants marked ABANDONED. Twenty runtime phases are PLAYING; two are ROUND_FINISHED. All 22 retain onlineTurnWork records. Participant abandonment is not a terminal match status in this model.

History records: **0**. Persisted replay event documents: **1,903** at observation time. These are partial event streams, not completed or validated replay manifests. No match was completed, cancelled, expired or failed according to the stored statuses. Events and state can continue changing through the existing timeout engine without any new client load.

Do not delete the turn index or activity keys: active durable work can recreate them, and that would hide the lifecycle issue. A separate reviewed correction should first define terminal handling for all-abandoned matches, then authorize any targeted cleanup. SERVER-6's prior regression PASS is preserved; its five matches were not modified or re-executed.

## Prepared instruments and remaining gates

`Server7ReadOnlyReview.py` performs allowlisted Redis reads and cgroup delta measurements. Initial per-command Docker process overhead was corrected to one reusable Redis connection; the successful observation used that corrected collector. It writes only sanitized metadata. It is a bounded diagnostic, not yet wired into the load supervisor's stop decision.

`ReviewRun.kt` reads only the exact registry under the TEST project and opt-in guard. It reports authoritative status, participant abandonment, history/event counts and durable work. It deliberately separates match status from participant connection state. Its current registry guard targets this failed run; R2 requires an explicitly reviewed new registry.

The swarm now records bounded 1ms latency histograms, including missing/overflow states: command ACK from first send (including retries), matchmaking wait, and local WS event handler duration. Offline histogram tests and the complete bot-swarm test task passed. **WS handler duration is not network event-delivery latency.** End-to-end event latency, REST timings, aggregate merging across groups and supervisor latency gates are not ready.

A cross-match delivery assertion was added. Existing sequence/private-seat checks remain in place. Independent duplicate command application, invalid-turn and durable replay correctness counters are still incomplete. No readiness or capacity PASS is claimed for those gaps.

Memory observations remain unchanged: API peak 926.1 MiB of 4 GiB, host minimum available 9.544 GiB, Redis peak approximately 9.2 MiB, no observed eviction/rejection/OOM. They are not a capacity conclusion.

## R2 proposal — not authorized to execute

Stages: 20 → 40 → 60 → 80 → 100 users, with at least 120 seconds of stable observation after each stage fully authenticates; maximum 180 seconds to reach the stage target. Hold final target at least five minutes and until the required outcomes are obtained. Do not advance on a timer alone. Never exceed 25 new registered matches or replace failed ones without review.

Before execution, independently approve CPU saturation thresholds in CPU-quota-percent and host-utilization-percent with explicit sustained windows; do not derive numbers from the anomalous failed run. Require fresh cgroup samples and consistent effective quota, target socket count, no reconnect trend, and complete measurement coverage. Provisional review targets for latency: command ACK p95 ≤1s / p99 ≤3s, matchmaking p95 ≤30s; event-delivery gate remains blocked until its measurement definition is implemented. Zero authorization/correctness failures, OOM, restart, Redis evictions or rejected writes; freeze ramp on any such event. Retain prior host available-memory floor and API memory limit guard only after explicit plan review. Require Redis recovery classification and authoritative final outcomes after global stop. **This proposal is not an executable approved threshold configuration.**

## Required result block

```text
SERVER-7R — CAPACITY MEASUREMENT REVIEW
RUN_ID=275abc49-45e7-40d0-8019-11531e6c8d48
API_NANO_CPUS=4000000000
API_CPU_QUOTA=0
API_CPU_PERIOD=0
API_CPU_MAX=400000 100000
API_CPUSET=CONFIG_EMPTY_EFFECTIVE_0-7
EFFECTIVE_API_CPU_LIMIT=4_CURRENT
HOST_LOGICAL_CPUS=8
VM_VCPU=8
CONTAINER_VISIBLE_CPUS=8
JVM_AVAILABLE_PROCESSORS=NOT_VERIFIED
CPU_METRIC_SOURCE=DOCKER_STATS_CPUPERC_ORIGINAL;CGROUP_USAGE_USEC_DELTA_NEW
DOCKER_CPU_PERCENT_SEMANTICS=100_PERCENT_PER_CPU_EQUIVALENT
CPU_NORMALIZATION=EXPLICIT_CORES_HOST_EQUIVALENT_AND_QUOTA
CGROUP_CPU_INSTRUMENTATION_READY=YES_BOUNDED_OBSERVER
CPU_METRIC_VALIDATION=PASS_IDLE
ORIGINAL_AVG_DOCKER_CPU=446.47%
ORIGINAL_PEAK_DOCKER_CPU=842.04%
ORIGINAL_CPU_STOP_VALID=INDETERMINATE
BASELINE_REDIS_KEYS=31
POST_LOAD_REDIS_KEYS=123
REDIS_KEY_DELTA=92
PRESENCE_KEYS=119
MATCH_KEYS=1
MATCHMAKING_KEYS=2
AUTHORIZATION_KEYS=0_REDIS_ONLY_IN_MEMORY_NOT_VERIFIED
LEASE_KEYS=1
OUTBOX_KEYS=0_REDIS_ONLY
OTHER_KEYS=0
UNKNOWN_KEYS=0
RUN_OWNED_REDIS_KEYS=92_LOAD_IDENTITY_ACTIVITY_KEYS_PLUS_22_SHARED_INDEX_MEMBERS
EXPECTED_MAX_TTL=120_SECONDS_RENEWABLE
RECOVERY_OBSERVATION_DURATION=150.649_SECONDS
REDIS_KEYS_AFTER_TTL_WINDOW=123
CONFIRMED_RESOURCE_LEAKS=0_CONFIRMED_LIFECYCLE_REVIEW_REQUIRED
RUN_MATCHES_REGISTERED=22
RUN_MATCHES_COMPLETED=0
RUN_MATCHES_ACTIVE=22
RUN_MATCHES_ABANDONED=0_MATCH_STATUS_88_PARTICIPANTS_ABANDONED
RUN_MATCHES_EXPIRED=0
RUN_MATCHES_FAILED=0
RUN_MATCHES_UNKNOWN=0
RUN_HISTORY_RECORDS=0
RUN_REPLAY_RECORDS=1903_EVENT_DOCUMENTS_NOT_COMPLETED_REPLAYS
MATCH_OUTCOME_INSTRUMENTATION_READY=YES_FOR_EXACT_FAILED_RUN_REGISTRY
LATENCY_INSTRUMENTATION_READY=PARTIAL
CORRECTNESS_INSTRUMENTATION_READY=PARTIAL
SERVER7_R2_RAMP_PLAN=PROPOSAL_PENDING_MEASUREMENT_GATES
NEW_LOAD_EXECUTED=NO
SERVER_CONFIGURATION_CHANGED=NO
REDIS_DATA_DELETED=NO
API_RESTARTED=NO
REDIS_RESTARTED=NO
COMMIT=NONE
PUSH=NONE
SERVER_7R_SUCCESS=NO
ADVANCE_TO_100_RERUN=NO_PENDING_REVIEW
NEXT=SERVER-7R REVIEW
```

Evidence: `Generated/SERVER7R/server-review.jsonl`, `match-outcomes.json`, `matches.txt`, and `tests.txt`. Generated evidence is ignored by Git. No credentials or raw UIDs were included in this report.

## S7-02 — instrumentation implementation and bounded validation

Scope: local instrumentation, deterministic tests and one bounded measurement inside the existing TEST JVM. No capacity workload, backend deployment, server configuration change, restart, old-match mutation, commit or push. Base remains `1d0c3bf3fc5b1aba427b5ab0861372895a7c9e38`; pending S7-01R and preexisting user files are preserved.

### Running JVM evidence

`Generated/S702/jvm-samples.jsonl` contains 30 distinct samples spanning 60.009 seconds. Every sample reports **Runtime.availableProcessors() = 4**. Effective API quota is 4 cores, host has 8 logical CPUs; the JVM therefore already recognizes the quota. No ActiveProcessorCount change is proposed now. Heap maximum is 2,147,483,648 bytes (2 GiB), committed heap 536,870,912 bytes, observed used heap 97,421,936–410,818,024 bytes. GC counters increased by one collection and 10 milliseconds. This is cumulative MXBean collection time, not a measurement of individual stop-the-world pauses or pause percentiles.

The allowlisted agent creates no listener port, transforms no classes and reads no credentials. Its sampling thread exits after the bounded interval. The attached class and JVM attach listener may remain until an ordinary restart; this was runtime instrumentation, not a claim of zero runtime side effects. The default server capture used 31 scheduled samples; polling retrieved 30 distinct samples. Local source now also accepts an explicit 2–1801 sample bound for a reviewed future run; this extended source was compiled locally, not uploaded or activated. Rebuild and hash the reviewed agent before future use. The existing attach wrapper remains a short diagnostic, not the future long-duration launcher.

### CPU, memory and resource collection

`CapacityMetrics.py` uses monotonic deltas: cores = delta usage_usec / 1,000,000 / elapsed seconds; quota percent = cores / effective cores * 100. Four continuously used cores mean 100%, one means 25%. Counter reset, nonpositive intervals and invalid quota fail validation. `S702ResourceSampler.py` reads cgroup v2 directly, checks all ancestor quotas and effective cpuset, and invalidates the first sample and PID/quota transitions. API and Redis have separate samples. Host utilization uses /proc/stat deltas excluding guest double counting; load average is recorded separately.

Cadence is 2 seconds with explicit overruns and stale JVM coverage. API/Redis cgroup RAM, host available memory, Redis INFO error/eviction/rejection counters and JVM heap/nonheap/GC counters are allowlisted. It does not enumerate Redis keys or player identities. The new full host collector was validated locally, not exercised against the live server during S7-02; previous S7-7R idle cgroup evidence remains separate. Socket counts come from authenticated client connections, active-match counts from authoritative registry state, never from presence activity keys. Docker CPU can be retained in separate diagnostic evidence, but is absent from the new STOP decision.

### Latency and correctness

Swarm records monotonic command send-to-accepted-ACK time, retaining the initial timestamp across retries. Rejected commands increment failures instead of successful latency. Matchmaking measures queue acceptance to MATCH_FOUND; immediate accepted MATCHED is zero. REST measures natural authenticated request round trips, without extra traffic. WebSocket metric is complete-frame receipt to handler completion, including client dispatch/processing delay. It is **not server-to-client network latency**; protocol has no suitable end-to-end monotonic correlation, and no protocol changes were made.

Histograms use integer millisecond buckets through 60,000 ms, explicit overflow and true maximum. Global percentiles merge bucket counts, never average child percentiles. Nearest rank is sorted sample at ceil(p*N); empty means not measured, one sample returns that sample. Quantiles with small N are descriptive: proposed ACK gate requires at least 100 observations, matchmaking p95 at least 20; insufficient samples block a capacity conclusion rather than imply success. Histogram storage truncates sub-millisecond measurements to milliseconds.

Wire checks count sequence regressions, cross-match/private/system-private delivery, inconsistent repeated ACKs and unexpected rejection without logging payloads. Client-visible sequence gaps can be legitimate filtered events. `CapacityOutcome.kt` prepares a separately opted-in TEST-only administrative read of exact new-run match IDs and the complete authoritative event stream; it checks gaps, repeated gameplay application and turn seat consistency. It never runs automatically and was compiled only. Raw participant identities and tile payloads are not output. Post-revocation delivery is not exercised by this workload and is **NOT_VALIDATED**; no blanket authorization completeness claim is made. Unknown turn coverage must be reviewed and must not be counted as a passing validation.

### Authoritative registry, History and Replay

`CapacityRegistry.py` stores UUID run/match IDs, group, four distinct participant slot references, start/completion times and authoritative outcome. It prohibits second matches for a slot and caps registration at 25. The outcome importer requires matching run ID/source and consistent root/runtime statuses. FINISHED maps to COMPLETED; CANCELLED is separate; missing/conflicting evidence stays UNKNOWN. FAILED is reserved and is not fabricated from CANCELLED or a disconnected client.

The administrative reader rejects the historical failed run, requires explicit run opt-in, and only reads registered matches marked validationData. `CapacityCoordinator.py` joins authoritative correctness counters with client counters and resources. It cannot launch a process or advance a stage: review approval remains required. For future execution, timestamp and join group snapshots, resource frames and authoritative reads in a new supervised launcher; do not reuse the historical Docker-CPU launcher. A stale/missing producer or an unresolved UNKNOWN/unaudited result must block advancement. Read authoritative metadata at stage boundaries/at most every 30 seconds; full event audits after stopping avoid turning monitoring into excessive Firestore traffic.

Replay selection is the first five lexicographically sorted UUIDs with authoritative COMPLETED status; fewer than five is an explicit failure. CANCELLED is never eligible. Normal completed History/Replay still needs authenticated participant validation during R2. Cancelled all-abandoned matches must have no normal completed History; S7-01R tests cover this contract, but no new live History claims are made here. Do not use the historical replay helper's client-log candidate selection for R2.

### Proposed R2 policy — review required before execution

* CPU: API quota utilization >=95% continuously for 30 seconds, or host utilization >=95% for 30 seconds. A single spike resets no result to failure. This preserves headroom rather than matching the invalid old Docker percentages.
* Memory: host MemAvailable <1.5 GiB immediately; API cgroup RAM >=90% of its 4 GiB limit for 30 seconds. Any OOM or API/Redis restart stops immediately. JVM heap/GC remain diagnostic alongside total cgroup memory; no invented pause threshold.
* Errors: immediate stop for unavailable services, Redis rejection/eviction/OOM error increments, unauthorized delivery, sequence corruption, duplicate application, invalid turn or match/idempotency corruption. Persistent 5xx means positive **new** 5xx increments in each sampled interval for 10 seconds; a lone cumulative error does not remain bad forever. Any correctness failure blocks acceptance even if the run has stopped already.
* Coverage: nonmonotonic time, >6-second sampling gaps or missing required resources stop. Newly restarted/reset sources need a new baseline and cannot silently count as healthy data.
* ACK: proposed p95 <=1000 ms and p99 <=3000 ms; exceedance with >=100 samples sustained for 30 seconds stops. These limits describe turn-based interaction responsiveness, not an adjustment to make 100 pass. Current consumer uses cumulative run histograms; report that scope explicitly and reset stage histograms in the future launcher for stage-specific acceptance.
* Matchmaking: proposed p95 <=10,000 ms after >=20 observations. Smaller samples are insufficient evidence; additionally bound each individual wait to 30 seconds once its full cohort is ready. A gate failure prevents advancement.
* Ramp: 20, 40, 60, 80, 100 authenticated users, holding at least 120 seconds of valid observations at each intermediate level and 300 seconds at 100. Bound cohort establishment to 180 seconds. No next level until resource, correctness and sample coverage gates pass. The exact behavior for finished participants must be reviewed before launch; do not silently call cumulative connections “concurrent.” Use distinct current open authenticated sockets. No automatic second match per slot.

These are prepared policy components and a stage proposal, not an integrated or authorized rerun launcher. R2 integration and preflight remain review tasks. The historical run and its evidence are not overwritten.

### S7-01R deployment and old 22 reconciliation proposal

After review/authorization, establish the final reviewed source revision (the current pending fix must not be labeled with the unchanged base SHA), build/test an immutable image tagged with that SHA and record its digest. Capture the current API image digest and nonsecret deployment settings as the rollback reference. Replace API only, retaining its 127.0.0.1:38080 binding, existing networks, resource limits and credential mounts. Preserve Redis and its data. Check local health, authenticated TEST connectivity and lifecycle behavior before accepting. On failed health restore the recorded previous API image/config; never roll back by deleting Redis or rotating credentials. No part of this plan was executed.

**The fix explicitly evaluates persisted all-abandoned matches in the worker. Deploying it can therefore cancel the old 22 automatically when discovered.** Deployment and old-match reconciliation cannot honestly be treated as independent in this implementation. Before deployment obtain separate authorization for that effect, preserve exact old-run evidence and verify the allowlisted 22 still meet all-abandoned/grace criteria. Proposed reconciliation uses the normal idempotent lifecycle transaction, preserves event evidence, removes due work through the contract, and verifies CANCELLED/no normal completed History. Do not bulk delete Redis keys or write FINISHED to obtain a clean dashboard. If authorization excludes those 22, deployment must remain blocked pending an explicitly reviewed exclusion design. No old match was mutated by S7-02.

### Validation and result

Seven Python tests passed (CPU/quota, known percentiles, STOP windows/recovery, registry, histogram merge, authoritative counter join and isolated versus sustained 5xx). Gradle `:bot-swarm:test :bot-swarm:provisionClasses` passed: 37 tests, zero failures. The bounded Java agent compiled with JDK21. Local baseline hash comparison found only the intended preexisting Swarm files changed; S7-01R production files and unrelated user files were preserved.

```text
SERVER-7 S7-02 CAPACITY OBSERVABILITY
JVM_AVAILABLE_PROCESSORS=4
EFFECTIVE_API_CPU_LIMIT=4
CGROUP_CPU_INSTRUMENTATION_READY=YES
CPU_QUOTA_NORMALIZATION_TESTS=PASS
DOCKER_CPU_STOP_TRIGGER=DISABLED_FOR_R2
HOST_CPU_INSTRUMENTATION_READY=YES
JVM_MEMORY_INSTRUMENTATION_READY=YES
GC_INSTRUMENTATION_READY=YES_COLLECTION_COUNT_AND_TIME
COMMAND_ACK_LATENCY_READY=YES
WS_EVENT_LATENCY_READY=YES_WITH_DOCUMENTED_LIMITATION
WS_EVENT_LATENCY_LIMITATION=CLIENT_FRAME_TO_HANDLER_ONLY_NOT_NETWORK_END_TO_END
MATCHMAKING_LATENCY_READY=YES
REST_LATENCY_READY=YES
PERCENTILE_ALGORITHM=NEAREST_RANK_MERGED_1MS_BUCKETS
PERCENTILE_TESTS=PASS
SEQUENCE_REGRESSION_DETECTION_READY=YES
DUPLICATE_COMMAND_DETECTION_READY=YES_AUTHORITATIVE_AUDIT
INVALID_TURN_DETECTION_READY=YES_AUTHORITATIVE_AUDIT
UNAUTHORIZED_DELIVERY_DETECTION_READY=YES_OBSERVABLE_WIRE_SCOPE
AUTHORITATIVE_MATCH_OUTCOME_TRACKING_READY=YES
RUN_REGISTRY_READY=YES
RESOURCE_SAMPLE_INTERVAL=2_SECONDS
R2_CPU_STOP_POLICY=95_PERCENT_FOR_30_SECONDS
R2_MEMORY_STOP_POLICY=HOST_AVAILABLE_LT_1536_MIB_OR_API_90_PERCENT_30_SECONDS
R2_ERROR_STOP_POLICY=CRITICAL_IMMEDIATE_NEW_5XX_PERSISTENT_10_SECONDS
COMMAND_ACK_P95_LIMIT=1000_MS_PROPOSED
COMMAND_ACK_P99_LIMIT=3000_MS_PROPOSED
MATCHMAKING_P95_LIMIT=10000_MS_PROPOSED
R2_RAMP=20_40_60_80_100_PROPOSED
R2_STAGE_DURATION=120_SECONDS_INTERMEDIATE_300_SECONDS_FINAL_PROPOSED
S7_01R_DEPLOYMENT_PLAN_READY=YES_REVIEW_REQUIRED
OLD_22_MATCH_CLEANUP_PLAN_READY=YES_SEPARATE_AUTHORIZATION_REQUIRED
NEW_LOAD_EXECUTED=NO
SERVER7_100_RERUN_STARTED=NO
SERVER_CONFIGURATION_CHANGED=NO
COMMIT=NONE
PUSH=NONE
DEPLOY=NONE
S7_02_SUCCESS=YES
SERVER7_100_RERUN_ALLOWED=NO
NEXT=S7-02 REVIEW
```
