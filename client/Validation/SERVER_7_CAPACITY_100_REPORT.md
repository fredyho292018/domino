# SERVER-7A/7B â€” 100-user capacity preflight (stopped before provisioning)

Source: `1d0c3bf3fc5b1aba427b5ab0861372895a7c9e38`; branch `main`.
Target: `https://domino-api-test.teamfho.com`, `wss://domino-api-test.teamfho.com/ws/v1/realtime`, Firebase TEST project `teamfho-domino`.

## Decision

STOP_BEFORE_ACCOUNT_CREATION. The inspected existing TEST slot directory contains **3 distinct, correctly scoped identity records**, with refresh-token fields present, zero duplicate UIDs and zero invalid records. **97 additional identities are required** to reach 100 using the existing three dedicated Swarm accounts. Token validity was not refreshed or remotely verified during this inventory. The count describes locally provisioned records, not a Firebase-wide account census. No approved 100-identity pool was established by this audit.

No accounts, tokens, UIDs or credential contents were printed. LOCAL emulator identities and the current FHO Unity identity were not used. No Firebase, backend or server request was made. Five SERVER-6 matches remain untouched; preservation is by absence of writes, not a repeated remote History check. Local process inspection found zero running Java Swarm/identity-coordinator processes; this is not a measurement of server-wide WebSocket connections.

## Existing provisioning audit and proposal for review

`server/domino/tools/bot-swarm/src/provision/kotlin/com/teamfho/swarm/Provision.kt` is the separate administrative provisioner. Missing slots create Firebase anonymous accounts; the provisioner verifies the returned token against the intended project, persists refresh credentials outside Git, and registers immutable `developmentTestAccounts` TEST/BOT_SWARM metadata. Existing slots are reused. Normal bots have no Firebase Admin runtime dependency. The server resolves the marker and propagates validationData to matches/history; this flag alone does not distinguish SERVER-7 from earlier validation.

Proposed provisioning, NOT EXECUTED:

1. Reuse the three existing dedicated TEST Swarm accounts, excluding the FHO account. Create only the missing 97 after review authorizes account creation and marker writes. If complete separation from SERVER-6 bot accounts is desired, provision 100 new identities instead; that alternative needs explicit selection and authorization.
2. Keep administrative ADC separate from bot runtime. Use five private shard directories outside Git, at most 20 distinct leased slots each. Move or map the existing slots once under the reviewed provisioning plan; do not copy credentials into simultaneous populations. Compare identity distinctness in memory across all shards and confirm TEST/project guards. Never log UIDs or tokens.
3. Provision missing slots sequentially with stop-on-error/quota behavior, retaining progress so retries do not create duplicate populations. Store credentials with owner-restricted permissions. No Player/Wallet/match/history/replay manual writes are proposed; only Auth accounts and existing test-marker metadata.

## Harness and instrumentation readiness

`Config.kt` enforces clients <=20 and slotOffset+clients <=20. A single --clients=100 invocation is prohibited. Five independently scoped 20-client processes are a possible architecture, but their coordinated ramp, cross-process distinctness, exact 25-match limit and collective stop/recovery are not implemented or validated by this audit. Do not simply launch five uncontrolled runs. The existing normal heartbeat and 800â€“2500 ms think-time defaults should be retained; no 100â€“150 ms emulator pacing.

Proposed aggregate ramp is 0 -> 25 -> 50 -> 75 -> 100, 15 seconds per step, subject to predeclared gates. Authentication ramp and queue admission require separate coordination to avoid counting partial groups as complete matches; 25 users cannot all form four-player matches at intermediate stages. Require requeue=false, exactly 25 total assignments, and enough session lifetime to observe at least 10 minutes with 100 connections, followed by natural completion/recovery. The existing finite-run client lifecycle must be reviewed before claiming it can maintain those connections after early match completion.

No latency percentiles or resource thresholds were invented. Before load, establish numerical stop thresholds and collect a fresh >=60-second baseline with periodic Docker/host/Redis/JVM measurements, followed by equivalent load/recovery sampling. Existing user-stated VM/API/Redis limits are planning inputs, not freshly measured values. Current harness code shows normal heartbeat support; full required REST/ACK/event latency, server resource, leak and correctness coverage remains to be established. Server access for metrics must be arranged without exposing sudo passwords or credentials. No infrastructure tuning is proposed.

## Data identification and future cleanup boundary

LOAD_TEST_DATA_IDENTIFIABLE=NOT_YET_RUN_SCOPED. Existing validationData=true is insufficient: SERVER-6 and other TEST validation can share it. Before execution create a private run manifest binding this run to its exact dedicated identity set, and collect exact match IDs from authenticated assignments across all four seats. Keep a sanitized public report with slot labels, counts and match IDs only. Future cleanup would require an independently reviewed dry-run intersection of explicit run match IDs, expected participants and server test markers, with the five SERVER-6 IDs excluded. Reject mixed/unexpected participants and stop on any FHO involvement. Do not infer ownership from aliases, time range or validationData alone. No cleanup code or deletion was implemented.

## Measurements and limitations

The identity gate failed before baseline/load. Baseline, sustained load, functional checks, sample Replays and recovery were NOT_RUN. No server capacity conclusion is possible. Authentication of the three stored identities was not retried. No 250-user run is authorized or started.

```text
SOURCE_SHA=1d0c3bf3fc5b1aba427b5ab0861372895a7c9e38
TARGET_USERS=100
TARGET_CONCURRENT_MATCHES=25
LOAD_IDENTITIES_REQUIRED=100
ADDITIONAL_LOAD_IDENTITIES_REQUIRED=97
AUTHENTICATED_USERS=0
LOAD_IDENTITIES_AVAILABLE=3_LOCAL_RECORDS_REMOTE_VALIDITY_NOT_RECHECKED
LOAD_IDENTITIES_CREATED=0
RAMP_PATTERN=NOT_RUN
TIME_TO_100_USERS=NOT_RUN
STEADY_STATE_DURATION=NOT_RUN
MATCHES_STARTED=0
MATCHES_COMPLETED=0
MATCHES_FAILED=0
MATCHES_ABANDONED=0
BASELINE_API_CPU=NOT_MEASURED
AVG_API_CPU=NOT_MEASURED
PEAK_API_CPU=NOT_MEASURED
BASELINE_API_RAM=NOT_MEASURED
PEAK_API_RAM=NOT_MEASURED
FINAL_API_RAM=NOT_MEASURED
BASELINE_REDIS_CPU=NOT_MEASURED
PEAK_REDIS_CPU=NOT_MEASURED
BASELINE_REDIS_RAM=NOT_MEASURED
PEAK_REDIS_RAM=NOT_MEASURED
BASELINE_HOST_RAM_AVAILABLE=NOT_MEASURED
MIN_HOST_RAM_AVAILABLE=NOT_MEASURED
BASELINE_HOST_LOAD=NOT_MEASURED
PEAK_HOST_LOAD=NOT_MEASURED
JVM_HEAP_PEAK=NOT_MEASURED
GC_PAUSE=NOT_MEASURED
REST_P50=NOT_MEASURED
REST_P95=NOT_MEASURED
REST_P99=NOT_MEASURED
COMMAND_ACK_P50=NOT_MEASURED
COMMAND_ACK_P95=NOT_MEASURED
COMMAND_ACK_P99=NOT_MEASURED
WS_EVENT_P50=NOT_MEASURED
WS_EVENT_P95=NOT_MEASURED
WS_EVENT_P99=NOT_MEASURED
MATCHMAKING_P50=NOT_MEASURED
MATCHMAKING_P95=NOT_MEASURED
HTTP_5XX=NOT_MEASURED
HTTP_429=NOT_MEASURED
WS_ABNORMAL_CLOSES=NOT_MEASURED
AUTH_FAILURES=NOT_MEASURED
COMMAND_FAILURES=NOT_MEASURED
REDIS_EVICTIONS=NOT_MEASURED
REDIS_MEMORY_REJECTIONS=NOT_MEASURED
FIRESTORE_ERRORS=NOT_MEASURED
UNEXPECTED_EXCEPTIONS=NOT_MEASURED
OOM_EVENTS=NOT_MEASURED
UNAUTHORIZED_DELIVERIES=NOT_MEASURED
SEQUENCE_REGRESSIONS=NOT_MEASURED
MATCH_CORRUPTIONS=NOT_MEASURED
REPLAY_SAMPLE_SIZE=0
REPLAY_SAMPLE_RESULT=NOT_RUN
REAL_TEST_FIRESTORE_READS=0
REAL_TEST_FIRESTORE_WRITES=0
POST_LOAD_ACTIVE_WS=NOT_MEASURED
POST_LOAD_REDIS_STATE=NOT_MEASURED
POST_LOAD_RESOURCE_LEAKS=NOT_MEASURED
RECOVERY_DURATION=NOT_RUN
BASELINE_ACTIVE_WS=NOT_MEASURED
LOAD_TEST_DATA_IDENTIFIABLE=NOT_YET_RUN_SCOPED
REAL_PRODUCTION_FIRESTORE_CALLS=0
SERVER_CONFIGURATION_CHANGED=NO
CLOUDFLARE_CONFIGURATION_CHANGED=NO
DNS_CHANGES=0
COMMIT=NONE
PUSH=NONE
SERVER_7_100_SUCCESS=NO_NOT_RUN_IDENTITY_GATE
ADVANCE_TO_250=NO_PENDING_REVIEW
NEXT=SERVER-7 100-USER REVIEW â€” PROVISIONING_AND_RUN_ISOLATION_PROPOSAL
```


## SERVER-7B actual execution — controlled CPU stop

The earlier identity-preflight STOP above is historical. SERVER-7P subsequently created 100 separate LOAD users; this actual run used only that population. No FHO/functional identity participated in load. The functional account was used only for bounded SERVER-6 preservation reads before and after.

**Result: the 100-user target was not demonstrated.** The predeclared conservative CPU threshold triggered global STOP during group 5. No retry, tuning, replacement match, restart, deployment, commit or push followed. This does not establish the server's maximum capacity.

Before load, API/Redis were healthy with zero restarts/OOM. The fresh baseline covers thirteen samples spanning roughly sixty seconds. The observer runs read-only in the user's root terminal; it collects allowlisted counters, never credentials, key names or log bodies. Container/host data are sampled, not billing telemetry. The collector has a 45-minute automatic lifetime and is not a Swarm process.

Stop thresholds were saved before dispatch: host available memory <1.5 GiB; API container memory >=90%; Docker API CPU >=380% for 30 seconds; metrics older than 25 seconds; any client critical failure, container health/restart/OOM change, Redis eviction or new error reply. The resource observer itself uses Docker stats twice per sample, so sample spacing varied under load. CPU averages below are arithmetic means of observed samples, not continuous integrals.

The VM reports 8 CPUs; API inspection reports NanoCpus=4000000000 and cgroup cpu.max=400000/100000 (four CPUs). Docker nevertheless reported percentages above 400%. Preserve these raw readings but do not equate them with verified CPU consumption or proven four-CPU saturation. CPU instrumentation needs review before selecting another attempt. Global STOP correctly followed the predeclared observed-metric threshold; no post-result threshold was invented.

Five groups launched at 0.0, 22.1, 44.9, 73.8 and 105.2 seconds. The 15-second minimum was extended until the preceding group authenticated. First periodic observations of each connection stage are reported separately; they are not exact handshake timestamps. Ninety-four distinct users authenticated cumulatively, while the highest periodic simultaneous socket observation was 87. Six requested clients did not complete authentication before cancellation. No claim of 94 or 100 simultaneously measured sockets is made.

At the stop decision, 21 matches were registered. Final complete-line reconciliation found one further assignment during shutdown, giving 22 exact match IDs and 88 assigned clients. All IDs are disjoint from the five protected SERVER-6 IDs. No Match 26 or replacement was launched. All five children exited with code 0 after STOP; no forced kill was needed. Their final activeAuthenticatedSockets counts total zero. They submitted 525 normal-cadence commands; no client failure records were observed before the resource stop.

No match completion was observed. Active matches were left to the normal server disconnect lifecycle. Their final server-side abandoned/failed outcomes were not inspected and must not be invented. With no completed sample available, five-match Replay reconstruction was NOT_RUN, not PASS. The GET-only sample downloader was prepared but not invoked. No backend data was deleted or manually rewritten.

SERVER-6 verification found all five history entries and authoritative snapshots both before and after. A first attempt to read the oldest Replay received 403 with the functional account. The bounded check was corrected to respect the three replayAvailable entries advertised by History; all three authorized Replay manifests passed before and after. No entitlement change or authentication bypass occurred. This is bounded preservation evidence, not a repeat reconstruction of all five FHO Replays.

Recovery observed naturally without service restarts. API memory remaining above baseline can reflect retained heap/cache; it is not independently classified as a leak. Redis still has more keys than baseline, with unfinished load matches present at STOP. Per-key ownership/TTL, server-wide WSS, queue/presence/authorization-handle cleanup and JVM workers/heap were not exposed by the available observer. POST_LOAD_RESOURCE_LEAKS therefore remains NOT_VERIFIED. Redis cumulative error replies were already 10 at baseline and did not increase; evictions and observed OOM errors remained zero. Server exception/Firestore operation counters were not collected.

The harness change adds an active-socket gauge and opt-in heartbeat retention for completed clients, without requeue or additional gameplay commands. The intended ten-minute retention never activated because target population was not reached. The Kotlin harness compiled and its existing test suite passed. No production backend was rebuilt/deployed or reconfigured. Earlier pending work is retained; no unrelated file was edited.

Evidence (ignored Generated/SERVER7B): run.json, baseline-resources.json, resources-after.json, timeline.jsonl, client-final-metrics.json, result.json, stop-thresholds.json, server6-before/after.json, recovery.jsonl, match-registry-final.json and separate five group logs. The final registry is authoritative for later reviewed cleanup; the initial registry/result preserve the 21-match stop-time observation. No cleanup is authorized here.

```text
SOURCE_SHA=1d0c3bf3fc5b1aba427b5ab0861372895a7c9e38
RUN_ID=275abc49-45e7-40d0-8019-11531e6c8d48
TARGET_USERS=100
AUTHENTICATED_USERS=94_CUMULATIVE_NOT_100_CONCURRENT
PEAK_ACTIVE_WS_SAMPLED=87
LOAD_GROUPS=5
CLIENTS_PER_GROUP=20
RAMP_PATTERN=0->20->40->60->80; STOP_DURING_GROUP_5
GROUP_START_SECONDS=0.0,22.1,44.9,73.8,105.2
TIME_TO_20=26.11s_FIRST_PERIODIC_OBSERVATION
TIME_TO_40=46.92s_FIRST_PERIODIC_OBSERVATION
TIME_TO_60=75.27s_FIRST_PERIODIC_OBSERVATION
TIME_TO_80=106.78s_FIRST_PERIODIC_OBSERVATION
TIME_TO_100=NOT_REACHED
TIME_TO_100_USERS=NOT_REACHED
STEADY_STATE_DURATION=0_TARGET_NOT_REACHED
TARGET_CONCURRENT_MATCHES=25
MATCHES_STARTED=22
MATCHES_COMPLETED=0
MATCHES_FAILED=NOT_MEASURED_SERVER_FINAL_OUTCOME
MATCHES_ABANDONED=NOT_MEASURED_SERVER_FINAL_OUTCOME
UNASSIGNED_USERS=12_OF_100_TARGET_AT_STOP
BASELINE_API_CPU=3.84%_DOCKER
AVG_API_CPU=446.47%_DOCKER
PEAK_API_CPU=842.04%_DOCKER_ANOMALOUS_VS_QUOTA
BASELINE_API_RAM=847.42 MiB
PEAK_API_RAM=926.1 MiB
FINAL_API_RAM=921.0 MiB
BASELINE_REDIS_CPU=0.71%
PEAK_REDIS_CPU=94.33%
BASELINE_REDIS_RAM=5.09 MiB
PEAK_REDIS_RAM=9.203 MiB
BASELINE_HOST_RAM_AVAILABLE=9.638 GiB
MIN_HOST_RAM_AVAILABLE=9.544 GiB
BASELINE_HOST_LOAD=1.37,0.94,0.56
PEAK_HOST_LOAD=10.8
JVM_HEAP_PEAK=NOT_MEASURED
GC_PAUSE=NOT_MEASURED
REST_P50=NOT_MEASURED
REST_P95=NOT_MEASURED
REST_P99=NOT_MEASURED
COMMAND_ACK_P50=NOT_MEASURED
COMMAND_ACK_P95=NOT_MEASURED
COMMAND_ACK_P99=NOT_MEASURED
WS_EVENT_P50=NOT_MEASURED
WS_EVENT_P95=NOT_MEASURED
WS_EVENT_P99=NOT_MEASURED
MATCHMAKING_P50=NOT_MEASURED
MATCHMAKING_P95=NOT_MEASURED
HTTP_5XX=0_OBSERVED_CLIENT_LOGS
HTTP_429=0_OBSERVED_CLIENT_LOGS
AUTH_FAILURES=0_OBSERVED_CLIENT_LOGS
WS_CONNECT_FAILURES=0_OBSERVED_CLIENT_LOGS
WS_ABNORMAL_CLOSES=0_OBSERVED_CLIENT_LOGS
RECONNECTS=0_OBSERVED_CLIENT_LOGS
COMMAND_FAILURES=0_OBSERVED_CLIENT_LOGS
REDIS_ERRORS=0_NEW_ERROR_REPLIES
FIRESTORE_ERRORS=NOT_MEASURED_SERVER_LOGS
UNEXPECTED_EXCEPTIONS=0_OBSERVED_CLIENT_LOGS
OOM_EVENTS=0
REDIS_EVICTIONS=0
REDIS_MEMORY_REJECTIONS=0_OBSERVED_ERRORSTAT_OOM
UNAUTHORIZED_DELIVERIES=NOT_MEASURED
SEQUENCE_REGRESSIONS=NOT_INDEPENDENTLY_VERIFIED
MATCH_CORRUPTIONS=NOT_INDEPENDENTLY_VERIFIED
DUPLICATE_COMMAND_APPLICATIONS=NOT_INDEPENDENTLY_VERIFIED
INVALID_TURN_ACCEPTANCES=NOT_INDEPENDENTLY_VERIFIED
REPLAY_SAMPLE_SIZE=0
REPLAY_SAMPLE_RESULT=NOT_RUN_NO_COMPLETED_MATCHES_OBSERVED
SERVER6_REGRESSION=PASS_5_HISTORY_5_SNAPSHOTS_3_AUTHORIZED_REPLAY_MANIFESTS
REAL_TEST_FIRESTORE_READS=NOT_MEASURED
REAL_TEST_FIRESTORE_WRITES=NOT_MEASURED
REAL_PRODUCTION_FIRESTORE_CALLS=0
ORPHAN_SWARM_PROCESSES=0
POST_LOAD_ACTIVE_WS=0_OWNED_CLIENTS_SERVER_WIDE_NOT_MEASURED
POST_LOAD_REDIS_STATE=123_KEYS_VS_31_BASELINE
POST_LOAD_RESOURCE_LEAKS=NOT_VERIFIED_RESIDUAL_MATCH_STATE
RECOVERY_DURATION=312.9s_OBSERVED
GLOBAL_STOP_REAL_RUN=PASS_API_CPU_THRESHOLD_ALL_5_EXIT_0
SERVER_CONFIGURATION_CHANGED=NO
CLOUDFLARE_CONFIGURATION_CHANGED=NO
DNS_CHANGES=0
COMMIT=NONE
PUSH=NONE
SERVER_7_100_SUCCESS=NO_STOPPED_BEFORE_TARGET
ADVANCE_TO_250=NO_PENDING_REVIEW
NEXT=SERVER-7 100-USER REVIEW
```


## R2 — controlled retest: stopped at clean-baseline gate

Run `d348e2d3-fd20-491a-939c-b1c5a56e71d6`; source `639fd73399106cd4fc542d09f1e113f6b1fc185c`. R1 sections and artifacts above were preserved. No ramp or new match was started. No commit/push or server configuration change.

Read-only identity inventory: 100 distinct existing LOAD identities in TEST project `teamfho-domino`; no credentials or UIDs emitted. Process inventory: zero Java Swarm MainKt processes. Exact-ID read of the 50 known matches found one preexisting active match with four LOAD participants: `815ea96e-fdfe-4e4d-b986-60eced10b72b`. It is outside the previous exact-22 migration allowlist and is not the S7-04B smoke match.

Blocker metadata: `{"abandoned": 4, "connected": 0, "id": "815ea96e-fdfe-4e4d-b986-60eced10b72b", "lifecycleVersion": null, "loadParticipants": 4, "pendingWork": true, "runtimeStatus": "IN_PROGRESS", "scope": "PREFLIGHT_BLOCKER", "status": "IN_PROGRESS", "validationData": true}`. The fresh read confirms durable pending work. This violates `ACTIVE_LOAD_MATCHES=0`. It was not mutated, reconciled, deleted or added to any allowlist; legacy gate was not opened. The previous S7-04B migration remains scoped to its original 22 matches.

A new exact-scope decision is required for this preexisting legacy match before retrying baseline. No automatic cleanup or retry was attempted. Resource baseline, ramp, latency, replay sampling and post-load recovery were not run; unavailable measurements below are not zero-valued successes.

Evidence: `Generated/SERVER7B-R2/run.json`, `preflight-matches.json`, `preflight-blocker.json`.

```text
SERVER-7B-R2 — 100 CONCURRENT USERS
===================================
RUN_ID=d348e2d3-fd20-491a-939c-b1c5a56e71d6
TARGET_USERS=100
AUTHENTICATED_USERS=0
PEAK_ACTIVE_WS=0
RAMP=20_40_60_80_100_PLANNED_NOT_EXECUTED
STAGE_DURATION_INTERMEDIATE=120s_PLANNED
STAGE_DURATION_FINAL=300s_MINIMUM_PLANNED
TIME_TO_20=NOT_MEASURED_RUN_NOT_STARTED
TIME_TO_40=NOT_MEASURED_RUN_NOT_STARTED
TIME_TO_60=NOT_MEASURED_RUN_NOT_STARTED
TIME_TO_80=NOT_MEASURED_RUN_NOT_STARTED
TIME_TO_100=NOT_MEASURED_RUN_NOT_STARTED
TARGET_MATCHES=25
MATCHES_STARTED=0
MATCHES_COMPLETED=0
MATCHES_CANCELLED=0
MATCHES_FAILED=0
MATCHES_ACTIVE_AFTER=0_R2_NOT_STARTED
MATCHES_UNKNOWN=0_R2_NOT_STARTED
BASELINE_CPU_QUOTA_PERCENT=NOT_MEASURED_RUN_NOT_STARTED
AVG_CPU_QUOTA_PERCENT=NOT_MEASURED_RUN_NOT_STARTED
PEAK_CPU_QUOTA_PERCENT=NOT_MEASURED_RUN_NOT_STARTED
MAX_SUSTAINED_CPU_30S=NOT_MEASURED_RUN_NOT_STARTED
BASELINE_HOST_CPU=NOT_MEASURED_RUN_NOT_STARTED
AVG_HOST_CPU=NOT_MEASURED_RUN_NOT_STARTED
PEAK_HOST_CPU=NOT_MEASURED_RUN_NOT_STARTED
BASELINE_API_RAM=NOT_MEASURED_RUN_NOT_STARTED
PEAK_API_RAM=NOT_MEASURED_RUN_NOT_STARTED
JVM_HEAP_PEAK=NOT_MEASURED_RUN_NOT_STARTED
JVM_GC_COUNT_DELTA=NOT_MEASURED_RUN_NOT_STARTED
JVM_GC_TIME_DELTA=NOT_MEASURED_RUN_NOT_STARTED
REDIS_CPU_PEAK=NOT_MEASURED_RUN_NOT_STARTED
REDIS_RAM_PEAK=NOT_MEASURED_RUN_NOT_STARTED
REDIS_EVICTIONS=NOT_MEASURED_RUN_NOT_STARTED
REDIS_MEMORY_REJECTIONS=NOT_MEASURED_RUN_NOT_STARTED
HOST_RAM_MIN_AVAILABLE=NOT_MEASURED_RUN_NOT_STARTED
COMMAND_ACK_SAMPLES=0
COMMAND_ACK_P50=NOT_MEASURED_RUN_NOT_STARTED
COMMAND_ACK_P95=NOT_MEASURED_RUN_NOT_STARTED
COMMAND_ACK_P99=NOT_MEASURED_RUN_NOT_STARTED
COMMAND_ACK_MAX=NOT_MEASURED_RUN_NOT_STARTED
WS_EVENT_SAMPLES=0
WS_EVENT_P50=NOT_MEASURED_RUN_NOT_STARTED
WS_EVENT_P95=NOT_MEASURED_RUN_NOT_STARTED
WS_EVENT_P99=NOT_MEASURED_RUN_NOT_STARTED
WS_EVENT_MAX=NOT_MEASURED_RUN_NOT_STARTED
WS_EVENT_METRIC=CLIENT_FRAME_TO_HANDLER
MATCHMAKING_SAMPLES=0
MATCHMAKING_P50=NOT_MEASURED_RUN_NOT_STARTED
MATCHMAKING_P95=NOT_MEASURED_RUN_NOT_STARTED
MATCHMAKING_P99=NOT_MEASURED_RUN_NOT_STARTED
MATCHMAKING_MAX=NOT_MEASURED_RUN_NOT_STARTED
REST_SAMPLES=0
REST_P50=NOT_MEASURED_RUN_NOT_STARTED
REST_P95=NOT_MEASURED_RUN_NOT_STARTED
REST_P99=NOT_MEASURED_RUN_NOT_STARTED
HTTP_5XX=NOT_MEASURED_RUN_NOT_STARTED
HTTP_429=NOT_MEASURED_RUN_NOT_STARTED
AUTH_FAILURES=NOT_MEASURED_RUN_NOT_STARTED
WS_CONNECT_FAILURES=NOT_MEASURED_RUN_NOT_STARTED
WS_ABNORMAL_CLOSES=NOT_MEASURED_RUN_NOT_STARTED
RECONNECTS=NOT_MEASURED_RUN_NOT_STARTED
COMMAND_FAILURES=NOT_MEASURED_RUN_NOT_STARTED
SEQUENCE_REGRESSIONS=NOT_MEASURED_RUN_NOT_STARTED
DUPLICATE_COMMAND_APPLICATIONS=NOT_MEASURED_RUN_NOT_STARTED
INVALID_TURN_ACCEPTANCES=NOT_MEASURED_RUN_NOT_STARTED
UNAUTHORIZED_DELIVERIES=NOT_MEASURED_RUN_NOT_STARTED
MATCH_CORRUPTIONS=NOT_MEASURED_RUN_NOT_STARTED
REPLAY_SAMPLE_SIZE=0
REPLAY_SAMPLE_RESULT=NOT_RUN
SERVER6_REGRESSION=NOT_RERUN_R2_BLOCKED_PRIOR_S704B_PASS
REAL_TEST_FIRESTORE_READS=NOT_MEASURED
REAL_TEST_FIRESTORE_WRITES=0_PREFLIGHT_READ_ONLY
REAL_PRODUCTION_FIRESTORE_CALLS=0
CPU_STOP_TRIGGERED=NO_LOAD_NOT_STARTED
MEMORY_STOP_TRIGGERED=NO_LOAD_NOT_STARTED
CRITICAL_ERROR_STOP_TRIGGERED=NO_LOAD_NOT_STARTED
GLOBAL_STOP_TRIGGERED=NO_PREFLIGHT_BLOCKED
GLOBAL_STOP_REASON=CLEAN_BASELINE_FAILED_ACTIVE_LOAD_MATCHES_1
POST_LOAD_ACTIVE_WS=0_R2_OWNED_CLIENTS
POST_LOAD_PENDING_MATCH_WORK=NOT_APPLICABLE_R2_NOT_STARTED_PREEXISTING_LOAD_WORK_1
POST_LOAD_ACTIVE_RENEWAL=NOT_MEASURED_R2_NOT_STARTED
POST_LOAD_REDIS_CONVERGENCE=NOT_APPLICABLE_R2_NOT_STARTED
ORPHAN_SWARM_PROCESSES=0
CONFIRMED_RESOURCE_LEAKS=NOT_ASSESSED_PREEXISTING_LEGACY_WORK
RECOVERY_DURATION=NOT_APPLICABLE
SERVER_CONFIGURATION_CHANGED=NO
CLOUDFLARE_CONFIGURATION_CHANGED=NO
DNS_CHANGES=0
COMMIT=NONE
PUSH=NONE
SERVER_7B_R2_SUCCESS=NO_BASELINE_BLOCKED
ADVANCE_TO_250=NO_PENDING_REVIEW
NEXT=SERVER-7B-R2 REVIEW
```
