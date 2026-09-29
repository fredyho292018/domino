# S7-08 — Firestore ACK critical path

## Scope and evidence

Separate diagnostic run `3b8d0890-a7a3-49fb-9dec-3746199f426b`, 20 then 40 users, at least 120 seconds per ready stage, with existing safety and latency gates. Image `cuban-domino-api:s708-acfafd501ec5`, base SHA `a4a2542363d93301bbede958b18bbce9fb3e8d1f` plus the recorded instrumented source manifest. No optimization, resource tuning, transaction policy change, commit or push. Only the API image changed; CPU 4, memory 4 GiB and JVM processors 4 remained fixed. Redis was not restarted. All 78 S7-07 artifact hashes are unchanged (`Generated/S708/s707-preserved.json`).

## Verified execution order

Source: `OnlineMatchService.command` → `FirestoreOnlineRepository.read/transact` → `OnlineMatchService.publish` → `RealtimeHandler` committed callback / ACK → `ConnectionOutbound`.

| Order | Operation | Classification | Logical remote interaction |
|---|---|---|---|
| 1 | Authoritative runtime pre-read before seat authorization | READ | BatchGetDocuments |
| 2 | Begin read-write transaction, existing 8-attempt policy | TRANSACTION_BEGIN | BeginTransaction |
| 3 | Read command receipt | TRANSACTION_READ | BatchGetDocuments |
| 4 | Read authoritative runtime | TRANSACTION_READ | BatchGetDocuments |
| 5 | Read turn-work presenceCheckAt | TRANSACTION_READ | BatchGetDocuments |
| 6 | Domain transition, validation, DTO conversion and serialization | OTHER | None |
| 7 | Buffer match/runtime, changed players, round/event/history, work update/delete and receipt writes | TRANSACTION_WRITE_BUFFER | None per local write |
| 8 | Commit buffered writes; await transaction future | TRANSACTION_COMMIT | Commit |
| 9 | Update local social-presence map, queue authorized updates, then ACK | OTHER | No synchronous Firestore call |

Normal new successful commands have **6 logical remote interactions**, not six measured network packets. PLAY_TILE, PASS and NEXT_ROUND use this same repository path; local write counts can differ. A duplicate receipt short-circuits after its read. Retries can repeat calls and add Rollback; transport retries may add further network attempts. All 961 measured commands had one callback attempt and three transactional reads. Exact wire exchanges and internal RPC retry counts are NOT_MEASURED. No local write-buffer operation is counted as a remote call.

The installed SDK bytecode is archived in `Generated/S708/sdk-*.txt`: FirestoreImpl.getAll invokes BatchGetDocuments; ServerSideTransaction.begin invokes BeginTransaction; ServerSideTransactionRunner invokes commit after the user callback. No transaction boundaries, writes, retry behavior, gameplay, lifecycle, History, Replay or ACK ordering changed.

## Timing semantics

Monotonic clocks; bounded opt-in traces; no synchronous per-phase logging.

- **FS_PRE_TRANSACTION_WAIT / preReadNanos:** existing document get invocation through future return. DTO conversion is excluded. Local work between pre-read and transaction invocation is not counted as Firestore time.
- **FS_TRANSACTION_TOTAL:** transaction invocation through successful future return, inclusive of retry attempts.
- **Acquisition-to-callback:** invocation through first callback entry; includes BeginTransaction and scheduling, not pure RPC time.
- **FS_TRANSACTION_READ:** sum of existing document future waits across attempts. Data extraction, DTO decoding and timestamp conversion are outside these timers.
- **FS_DOMAIN_INSIDE_TRANSACTION:** engine transition. Callback local-other separately contains validation, conversion, encoding and write buffering.
- **Between-attempts:** callback exit through next callback entry, including failed completion, rollback, backoff and new begin as applicable.
- **FS_COMMIT_OR_REMOTE_COMPLETION:** final callback exit through transaction future return; includes Commit and SDK/local completion scheduling. Pure Commit RPC duration is **not separately measured**.
- **FS_POST_COMMIT=0, count=0:** source-proven absence of synchronous post-commit Firestore operations in the normal command path. General publication time remains an outer phase. Background listeners are outside the synchronous ACK path.
- **FS_TOTAL = pre-read + transaction total**. Acquisition + all callback reads/domain/local-other + retry gaps + final completion exactly partition transaction total. Nested values must not be added again to FS_TOTAL.
- **FS_CLIENT_QUEUE_WAIT=NOT_MEASURED:** public options do not expose an independent executor/channel queue timer. No invasive SDK changes were made.

Each command preserves client ACK, server total, FS total and subphases with SHA-256 correlation; allowed metric fields contain no UID/token/password/credential values. The diagnostic residual is computed per paired sample before aggregation. It is not Cloudflare latency: Swarm → Internet → Cloudflare → API and API → Google Firestore are separate paths.

Percentiles use **NEAREST_RANK_MERGED_1MS_BUCKETS**: floor durations to milliseconds, merge buckets, select nearest rank, preserve millisecond max and explicit overflow above 60000 ms. Raw nanoseconds remain available for consistency and shares. No independent percentiles are added together. Stage distributions are separate and include ramp; readiness and hold duration are reported independently.

## Configuration and tests

Authoritative database metadata returned **us-central1**, FIRESTORE_NATIVE, for TEST teamfho-domino/(default). Only selected metadata was saved. Live client implementation, credential mechanism, channel pool and public retry settings are included below; no credential values were emitted.

Six Firestore timing tests cover order, retry aggregation, missing phases, nesting, total consistency, clock regression and output safety. Existing ACK/hash tests passed. Full backend: 677 discovered, 653 executed, 24 conditional skips, zero failures/errors. Seven histogram and safe-output tests pass. The added Firestore trace microbenchmark averaged **3112 ns** over 10000 iterations after warmup; this is not a production A/B overhead estimate. Preparation encountered an overbroad test selector and an unavailable SDK accessor; both were corrected and validation passed before deployment/load.

Raw timing, resource records, analysis, source manifest, authoritative registry, outcome audit and recovery evidence are retained under `Generated/S708/`.

## Experiment results

All durations below are milliseconds; full count/p50/p95/p99/max and per-command-type breakdowns are in `Generated/S708/analysis.json`.

| Users | Samples | Client p95 | Server p95 | FS total p95 | Transaction reads p95 | Completion tail p95 | Residual p95 | CPU average / peak % |
|---|---|---|---|---|---|---|---|---|
| 20 | 347 | 887 | 815 | 736 | 344 | 160 | 103 | 17.69 / 65.23 |
| 40 | 614 | 1244 | 1079 | 978 | 465 | 223 | 164 | 24.26 / 60.47 |

Stop reason: `LATENCY_STAGE_GATE_commandAckLatency`. Correlation: 961/961, no dropped or incomplete samples; one flagged negative residual (-25.126477 ms), retained in evidence and excluded from the nonnegative residual histogram. No stage beyond 40. No new capacity limit is certified.

Measured classification: **READ_WAIT**, read share 61.24% of summed FS time; Firestore dominance under the required growth criterion: **YES**. These are client-observed SDK waits, not proof of Google server execution time. Acquisition/completion also contain SDK scheduling. 30-second correlations are descriptive and cannot establish CPU or GC causality. Fresh API/JVM state and instrumentation prevent treating differences from S7-07 as a controlled performance comparison.

Normal lifecycle recovery: `{'COMPLETED': 0, 'CANCELLED': 10, 'FAILED': 0, 'ACTIVE': 0, 'UNKNOWN': 0}`; run-owned Redis inventory converged without cleanup, no orphan processes, gate closed, Redis unchanged, SERVER-6 preserved.

Exactly one proposed next action: **FIRESTORE_TRANSACTION_READ_REVIEW** — review why the three sequential transactional document reads are needed and their measured waits; retain all reads and transaction semantics until separately authorized. This review was not executed.

The residual at 40 users increased (p95 103 → 164 ms), so it is not stable. One negative residual demonstrates that the two measurement intervals are not strictly nested: server timing ends after transport.send returns, whereas client timing ends when its ACK is processed. The exact cause of this sample is unproven. No phase duration itself is negative. An observer timeout record occurred after the load; it is retained and excluded from stage statistics, with its preceding valid timestamp checked to be after client shutdown.

Configuration note: the live public service retry settings are not necessarily the effective per-RPC stub settings. The installed SDK default-branch reconstruction preserved in S7-07 reports Begin/Commit RPC timeout 60 s and BatchGet 300 s, maxAttempts 5; these were not obtained by inspecting the live private RPC stub. Application transaction attempts remain 8, pre-read wait 15 s, and transaction future wait has no additional explicit timeout. Credentials mechanism is ADC backed by ServiceAccountCredentials, with no credential values recorded.

## Required final output

```text
SERVER-7 S7-08 FIRESTORE CRITICAL PATH
======================================
FILES_CHANGED=["OnlineRepository.kt","AckPhaseTiming.kt","FirestorePhaseTiming.kt","FirestorePhaseTimingTest.kt","S708TimingAgent.java","S708TimingAnalysis.py","S708TimingAnalysisTests.py","S7_08_FIRESTORE_CRITICAL_PATH_REPORT.md","Generated/S708/*"]
FIRESTORE_COMMAND_PATH=PREREAD > BEGIN > RECEIPT_READ > RUNTIME_READ > WORK_READ > DOMAIN > LOCAL_WRITE_BUFFER > COMMIT_COMPLETION > LOCAL_PUBLISH > ACK
FIRESTORE_REMOTE_ROUND_TRIPS_PER_COMMAND=6_LOGICAL_CALLS_NORMAL_ONE_ATTEMPT; WIRE_RETRIES_NOT_MEASURED; LOCAL_WRITES_NOT_COUNTED
FIRESTORE_DATABASE_LOCATION=us-central1_AUTHORITATIVE_DATABASE_METADATA
FIRESTORE_CLIENT_CONFIGURATION={"channelProvider":"com.google.api.gax.grpc.InstantiatingGrpcChannelProvider","channelPoolSettings":"ChannelPoolSettings{minRpcsPerChannel=0, maxRpcsPerChannel=2147483647, minChannelCount=1, maxChannelCount=1, maxResizeDelta=2, initialChannelCount=1, preemptiveRefreshEnabled=false}","serviceRetrySettings":"RetrySettings{totalTimeoutDuration=PT50S, initialRetryDelayDuration=PT1S, retryDelayMultiplier=2.0, maxRetryDelayDuration=PT32S, maxAttempts=6, jittered=true, initialRpcTimeoutDuration=PT50S, rpcTimeoutMultiplier=1.0, maxRpcTimeoutDuration=PT50S}","transactionAttempts":8,"preReadTimeoutSeconds":15,"transactionFutureTimeout":"SDK_MANAGED_NO_EXPLICIT_FUTURE_TIMEOUT","availableProcessors":4,"project":"teamfho-domino","database":"(default)","clientImplementation":"com.google.cloud.firestore.FirestoreImpl","credentialsMechanism":"com.google.auth.oauth2.ServiceAccountCredentials","executorProvider":"NOT_EXPOSED_BY_FIRESTORE_OPTIONS"}
FS_COMMIT_SEPARATELY_MEASURABLE=NO_CALLBACK_EXIT_TO_FUTURE_RETURN_IS_COMPLETION_TAIL
TRANSACTION_ATTEMPT_MEASURABLE=YES_CALLBACK_INVOCATIONS
TRANSACTION_RETRY_MEASURABLE=YES_CALLBACK_REEXECUTIONS_RPC_RETRIES_NOT_MEASURED
S7_08_TIMING_OVERHEAD_ACCEPTABLE=YES_LOCAL_MEAN_3112_NS_NOT_PRODUCTION_AB
INSTRUMENTATION_TESTS=PASS_6_FS_7_HISTOGRAM_AND_SAFE_OUTPUT_FULL_BACKEND_653_EXECUTED_24_SKIPPED
PERCENTILE_ALGORITHM=NEAREST_RANK_MERGED_1MS_BUCKETS
CORRELATED_SAMPLES=961/961
NEGATIVE_DIAGNOSTIC_RESIDUALS=[{"correlation":"882d67f6fa537b63da45a6371f10bc85b12298444bf1e2dfbfeed30ec75d75c9","commandType":"PLAY_TILE","clientAckMs":864,"serverMs":889.126477,"outsideServerMs":-25.126477000000023}]
RESIDUAL_POLICY=NEGATIVE_VALUES_RETAINED_AND_FLAGGED_EXCLUDED_FROM_NONNEGATIVE_HISTOGRAM_NOT_CLAMPED
POST_LOAD_RESOURCE_INVALID_RECORDS=1
COMMAND_TYPES_MEASURED=["NEXT_ROUND","PASS","PLAY_TILE"]
COMMAND_TYPE_SAMPLE_COUNTS={"20":{"NEXT_ROUND":6,"PASS":66,"PLAY_TILE":275},"40":{"NEXT_ROUND":11,"PASS":126,"PLAY_TILE":477}}

20_USERS:
STAGE_DURATION={"observationSeconds":167.625,"readyToStageEndSeconds":143.211,"requiredHoldSeconds":120}
CLIENT_ACK_SAMPLES=347
CLIENT_ACK_P50=470_MS
CLIENT_ACK_P95=887_MS
CLIENT_ACK_P99=1125_MS
CLIENT_ACK_MAX=1303_MS
SERVER_TOTAL_SAMPLES=347
SERVER_TOTAL_P50=428_MS
SERVER_TOTAL_P95=815_MS
SERVER_TOTAL_P99=1059_MS
SERVER_TOTAL_MAX=1286_MS
FS_PRE_TRANSACTION_WAIT_SAMPLES=347
FS_PRE_TRANSACTION_WAIT_P50=60_MS
FS_PRE_TRANSACTION_WAIT_P95=129_MS
FS_PRE_TRANSACTION_WAIT_P99=174_MS
FS_PRE_TRANSACTION_WAIT_MAX=236_MS
FS_TRANSACTION_TOTAL_SAMPLES=347
FS_TRANSACTION_TOTAL_P50=342_MS
FS_TRANSACTION_TOTAL_P95=648_MS
FS_TRANSACTION_TOTAL_P99=775_MS
FS_TRANSACTION_TOTAL_MAX=1225_MS
FS_TRANSACTION_READ_SAMPLES=347
FS_TRANSACTION_READ_P50=180_MS
FS_TRANSACTION_READ_P95=344_MS
FS_TRANSACTION_READ_P99=420_MS
FS_TRANSACTION_READ_MAX=501_MS
FS_DOMAIN_INSIDE_TRANSACTION_SAMPLES=347
FS_DOMAIN_INSIDE_TRANSACTION_P50=0_MS
FS_DOMAIN_INSIDE_TRANSACTION_P95=7_MS
FS_DOMAIN_INSIDE_TRANSACTION_P99=20_MS
FS_DOMAIN_INSIDE_TRANSACTION_MAX=40_MS
FS_COMMIT_OR_REMOTE_COMPLETION_SAMPLES=347
FS_COMMIT_OR_REMOTE_COMPLETION_P50=85_MS
FS_COMMIT_OR_REMOTE_COMPLETION_P95=160_MS
FS_COMMIT_OR_REMOTE_COMPLETION_P99=220_MS
FS_COMMIT_OR_REMOTE_COMPLETION_MAX=991_MS
FS_POST_COMMIT_SAMPLES=347
FS_POST_COMMIT_P50=0_MS
FS_POST_COMMIT_P95=0_MS
FS_POST_COMMIT_P99=0_MS
FS_POST_COMMIT_MAX=0_MS
FS_TOTAL_SAMPLES=347
FS_TOTAL_P50=407_MS
FS_TOTAL_P95=736_MS
FS_TOTAL_P99=918_MS
FS_TOTAL_MAX=1279_MS
FS_ACQUISITION_TO_CALLBACK_SAMPLES=347
FS_ACQUISITION_TO_CALLBACK_P50=57_MS
FS_ACQUISITION_TO_CALLBACK_P95=125_MS
FS_ACQUISITION_TO_CALLBACK_P99=153_MS
FS_ACQUISITION_TO_CALLBACK_MAX=199_MS
FS_CALLBACK_LOCAL_OTHER_SAMPLES=347
FS_CALLBACK_LOCAL_OTHER_P50=2_MS
FS_CALLBACK_LOCAL_OTHER_P95=28_MS
FS_CALLBACK_LOCAL_OTHER_P99=65_MS
FS_CALLBACK_LOCAL_OTHER_MAX=103_MS
FS_BETWEEN_ATTEMPTS_SAMPLES=347
FS_BETWEEN_ATTEMPTS_P50=0_MS
FS_BETWEEN_ATTEMPTS_P95=0_MS
FS_BETWEEN_ATTEMPTS_P99=0_MS
FS_BETWEEN_ATTEMPTS_MAX=0_MS
FS_CLIENT_QUEUE_WAIT=NOT_MEASURED
TRANSACTION_ATTEMPTS={"1":347}
TRANSACTION_RETRIES=0
TRANSACTION_READ_COUNT_DISTRIBUTION={"3":347}
CPU_QUOTA_AVG=17.694569
CPU_QUOTA_PEAK=65.231708
JVM_GC_COUNT_DELTA=5
JVM_GC_TIME_DELTA=249

40_USERS:
STAGE_DURATION={"observationSeconds":151.442,"readyToStageEndSeconds":122.58,"requiredHoldSeconds":120}
CLIENT_ACK_SAMPLES=614
CLIENT_ACK_P50=601_MS
CLIENT_ACK_P95=1244_MS
CLIENT_ACK_P99=1412_MS
CLIENT_ACK_MAX=1812_MS
SERVER_TOTAL_SAMPLES=614
SERVER_TOTAL_P50=533_MS
SERVER_TOTAL_P95=1079_MS
SERVER_TOTAL_P99=1273_MS
SERVER_TOTAL_MAX=1771_MS
FS_PRE_TRANSACTION_WAIT_SAMPLES=614
FS_PRE_TRANSACTION_WAIT_P50=76_MS
FS_PRE_TRANSACTION_WAIT_P95=172_MS
FS_PRE_TRANSACTION_WAIT_P99=226_MS
FS_PRE_TRANSACTION_WAIT_MAX=531_MS
FS_TRANSACTION_TOTAL_SAMPLES=614
FS_TRANSACTION_TOTAL_P50=423_MS
FS_TRANSACTION_TOTAL_P95=847_MS
FS_TRANSACTION_TOTAL_P99=1025_MS
FS_TRANSACTION_TOTAL_MAX=1668_MS
FS_TRANSACTION_READ_SAMPLES=614
FS_TRANSACTION_READ_P50=227_MS
FS_TRANSACTION_READ_P95=465_MS
FS_TRANSACTION_READ_P99=615_MS
FS_TRANSACTION_READ_MAX=945_MS
FS_DOMAIN_INSIDE_TRANSACTION_SAMPLES=614
FS_DOMAIN_INSIDE_TRANSACTION_P50=0_MS
FS_DOMAIN_INSIDE_TRANSACTION_P95=9_MS
FS_DOMAIN_INSIDE_TRANSACTION_P99=23_MS
FS_DOMAIN_INSIDE_TRANSACTION_MAX=47_MS
FS_COMMIT_OR_REMOTE_COMPLETION_SAMPLES=614
FS_COMMIT_OR_REMOTE_COMPLETION_P50=102_MS
FS_COMMIT_OR_REMOTE_COMPLETION_P95=223_MS
FS_COMMIT_OR_REMOTE_COMPLETION_P99=307_MS
FS_COMMIT_OR_REMOTE_COMPLETION_MAX=665_MS
FS_POST_COMMIT_SAMPLES=614
FS_POST_COMMIT_P50=0_MS
FS_POST_COMMIT_P95=0_MS
FS_POST_COMMIT_P99=0_MS
FS_POST_COMMIT_MAX=0_MS
FS_TOTAL_SAMPLES=614
FS_TOTAL_P50=504_MS
FS_TOTAL_P95=978_MS
FS_TOTAL_P99=1173_MS
FS_TOTAL_MAX=1760_MS
FS_ACQUISITION_TO_CALLBACK_SAMPLES=614
FS_ACQUISITION_TO_CALLBACK_P50=73_MS
FS_ACQUISITION_TO_CALLBACK_P95=179_MS
FS_ACQUISITION_TO_CALLBACK_P99=230_MS
FS_ACQUISITION_TO_CALLBACK_MAX=530_MS
FS_CALLBACK_LOCAL_OTHER_SAMPLES=614
FS_CALLBACK_LOCAL_OTHER_P50=2_MS
FS_CALLBACK_LOCAL_OTHER_P95=32_MS
FS_CALLBACK_LOCAL_OTHER_P99=67_MS
FS_CALLBACK_LOCAL_OTHER_MAX=124_MS
FS_BETWEEN_ATTEMPTS_SAMPLES=614
FS_BETWEEN_ATTEMPTS_P50=0_MS
FS_BETWEEN_ATTEMPTS_P95=0_MS
FS_BETWEEN_ATTEMPTS_P99=0_MS
FS_BETWEEN_ATTEMPTS_MAX=0_MS
FS_CLIENT_QUEUE_WAIT=NOT_MEASURED
TRANSACTION_ATTEMPTS={"1":614}
TRANSACTION_RETRIES=0
TRANSACTION_READ_COUNT_DISTRIBUTION={"3":614}
CPU_QUOTA_AVG=24.263234
CPU_QUOTA_PEAK=60.472683
JVM_GC_COUNT_DELTA=4
JVM_GC_TIME_DELTA=186

FIRESTORE_SHARE_OF_SERVER_P95_20=0.903067
TRANSACTION_SHARE_OF_SERVER_P95_20=0.795092
FIRESTORE_SHARE_OF_SUMMED_SERVER_20=0.932818
OUTSIDE_SERVER_RESIDUAL_20={"count":347,"p50":34,"p95":103,"p99":138,"max":166,"overflow":0}
FIRESTORE_SHARE_OF_SERVER_P95_40=0.906395
TRANSACTION_SHARE_OF_SERVER_P95_40=0.784986
FIRESTORE_SHARE_OF_SUMMED_SERVER_40=0.93917
OUTSIDE_SERVER_RESIDUAL_40={"count":613,"p50":53,"p95":164,"p99":235,"max":539,"overflow":0}
FIRESTORE_DOMINANT=YES
PRIMARY_FIRESTORE_SUBCOMPONENT=READ_WAIT
COMBINED_READ_SHARE_OF_FS_TIME=0.612429
FIRESTORE_LATENCY_CPU_RELATION={"firestoreP95":0.6103100364732708,"transactionP95":0.5824270815076378,"completionP95":0.6737763330443809}
FIRESTORE_LATENCY_GC_RELATION={"firestoreP95":0.2971797074510581,"transactionP95":0.26667757111973867,"completionP95":0.40140411795147657}
CORRELATION_INTERPRETATION=DESCRIPTIVE_30_SECOND_WINDOWS_NOT_CAUSAL
PRIMARY_BOTTLENECK_CLASSIFICATION=READ_WAIT
CONFIDENCE=HIGH_FOR_MEASURED_BOUNDARIES_NOT_REMOTE_SERVICE_CAUSALITY
POST_TEST_ACTIVE_LOAD_MATCHES=0
POST_TEST_PENDING_WORK=0
UNCLASSIFIED_LOAD_MATCHES=0
ORPHAN_SWARM_PROCESSES=0
RECOVERY_MATCH_COUNTS={"COMPLETED":0,"CANCELLED":10,"FAILED":0,"ACTIVE":0,"UNKNOWN":0}
SERVER6_REGRESSION=PASS
PROPOSED_NEXT_ACTION=FIRESTORE_TRANSACTION_READ_REVIEW
GAMEPLAY_SEMANTICS_CHANGED=NO
FIRESTORE_TRANSACTION_SEMANTICS_CHANGED=NO
SERVER7_100_RERUN_STARTED=NO
SERVER7_250_STARTED=NO
SERVER_CONFIGURATION_CHANGED=NO
API_IMAGE_CHANGED=YES_INSTRUMENTATION_ONLY
COMMIT=NONE
PUSH=NONE
S7_07_EVIDENCE_PRESERVED=YES
S7_08_SUCCESS=YES
NEXT=S7-08 REVIEW
```
