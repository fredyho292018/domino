# SERVER-7B-R2 — controlled capacity retest

**Not passed.** The run stopped at the 60-user stage because command ACK p95 was 1,247 ms, above the predeclared 1,000 ms stage gate. All three launched groups were stopped; groups 4–5 were never started. No automatic retry, tuning, deployment, service restart, legacy reconciliation, or 250-user run followed.

## Execution and scope

New run `78c7d326-f40c-4ec4-a4f9-0a96b9273350` used the exact deployed SHA `a4a2542363d93301bbede958b18bbce9fb3e8d1f`. The 100 dedicated LOAD identities were validated; only 60 authenticated before STOP. No functional TEST/FHO identity participated in matchmaking. Clients were built from the clean checkpoint source, used existing think times (800–2,500 ms), and had requeue disabled. The initial authority reader found zero active LOAD matches and zero unclassified matches. API/Redis health, gates closed, four-CPU quota, 4 GiB memory limit and JVM availableProcessors=4 passed. A 60-second idle resource baseline was captured. Source code and server configuration were not modified; generated operational scripts and evidence are confined to the validation artifacts.

20 users were reached at 22.828 s and held 141.765 s; 40 users at 190.484 s and held 141.266 s. Sixty users were reached at 361.562 s. The latency stage gate failed after its minimum 120-second hold. Total execution through owned-client exit was 487.859 s. The 80/100 stages, including the intended 300-second final hold, were not reached because of global STOP. Five groups were not silently substituted with fewer clients for a success claim.

The coordinator used direct cgroup quota CPU deltas; Docker CPU was not used for stopping. It monitored every newly collected resource sample, applied the 95%/30 s CPU threshold and 90%/30 s API-memory threshold, and retained the 1,536 MiB host-available-memory floor. No CPU or memory stop occurred. The final command ACK distribution has 1,901 samples; p95=1,247 ms, p99=1,725 ms. Latency thresholds were not changed.

## Authority and recovery

Client registry and fresh authoritative discovery both contain exactly 15 IDs; no client-missed or unclassified match exists. A first post-stop read observed one COMPLETED and 14 ACTIVE. After normal abandonment grace and Redis TTL, two stable authoritative reads observed one COMPLETED, 14 CANCELLED, zero ACTIVE/FAILED/UNKNOWN, and zero pending LOAD work. Cancellation is not counted as completed gameplay. The terminal outcomes/event audit covered all 15 IDs and found zero sequence gaps/regressions, duplicate command applications, invalid turn acceptances, unauthorized deliveries or idempotency violations; unverified-turn count was also zero.

Exact run Redis inventory after recovery found zero run-owned keys or shared-index members. API and Redis remained healthy, with unchanged container identities and zero restarts/OOM. Both reconciliation gates remained closed. Local process inspection found no Swarm orphans. The run's resource observer and sampler were stopped after archiving; only their own temporary JVM counter file and verified agent copy were removed. No Redis data was deleted. Confirmed resource leaks=0 is a bounded observation, not a claim about indefinite memory behavior.

SERVER-6 retained five History entries, five authenticated snapshots and the same three available replay manifests. No SERVER-6 gameplay mutation occurred. The required five-completed-match Replay sample was **not run** because only one match completed. No extra match was created to fill the sample; Replay reconstruction PASS is not claimed.

## Metric interpretation

All CPU values are percentages of the API's four-CPU quota unless labeled host CPU. Maximum sustained 30-second CPU is the maximum of window minima over complete sampled windows (14.862%); maximum rolling 30-second mean was 38.912%. Peak API cores used was 2.9778 cores. Redis CPU peak is reported in cores; Redis RAM uses INFO used_memory. Redis connection snapshots were retained at the 20/60 stages and after recovery (final=5); no continuous connection-peak claim is made.

WS latency is CLIENT_FRAME_TO_HANDLER, measured at millisecond resolution; its zero-millisecond percentiles are not zero network latency. REST samples are naturally occurring Swarm operations, including normal shutdown cleanup; no artificial REST load was added. HTTP/auth/socket error counts are observations from the owned clients and their safe failure logs, not a server-wide census. Firestore operation totals were not instrumented and remain NOT_MEASURED. All Firestore tools were pinned to the TEST project and no production calls were made.

## Requested output

```makefile
SERVER-7B-R2 — 100 CONCURRENT USERS
===================================
DEPLOYED_SHA=a4a2542363d93301bbede958b18bbce9fb3e8d1f
RUN_ID=78c7d326-f40c-4ec4-a4f9-0a96b9273350
TARGET_USERS=100
AUTHENTICATED_USERS=60
PEAK_ACTIVE_WS=60
TIME_TO_20=22.828 s
TIME_TO_40=190.484 s
TIME_TO_60=361.562 s
TIME_TO_80=NOT_REACHED
TIME_TO_100=NOT_REACHED
TARGET_MATCHES=25
MATCHES_STARTED=15
MATCHES_COMPLETED=1
MATCHES_CANCELLED=14
MATCHES_FAILED=0
MATCHES_ACTIVE_AFTER=0
MATCHES_UNKNOWN=0
CLIENT_OBSERVED_MATCHES=15
AUTHORITATIVE_DISCOVERED_MATCHES=15
CLIENT_MISSED_MATCHES=0
REGISTRY_UNION_MATCHES=15
UNCLASSIFIED_LOAD_MATCHES=0
BASELINE_CPU_QUOTA_PERCENT=1.994
AVG_CPU_QUOTA_PERCENT=19.76
PEAK_CPU_QUOTA_PERCENT=74.444
MAX_SUSTAINED_CPU_30S=14.862
BASELINE_HOST_CPU=5.289
AVG_HOST_CPU=26.177
PEAK_HOST_CPU=78.205
BASELINE_API_RAM=741.07 MiB
PEAK_API_RAM=871.04 MiB
HOST_RAM_MIN_AVAILABLE=9802.48 MiB
JVM_AVAILABLE_PROCESSORS=4
JVM_HEAP_PEAK=378.52 MiB
JVM_GC_COUNT_DELTA=21
JVM_GC_TIME_DELTA=662 ms
REDIS_CPU_PEAK=0.3974 cores
REDIS_RAM_PEAK=1.74 MiB
REDIS_EVICTIONS=0
REDIS_MEMORY_REJECTIONS=0
COMMAND_ACK_SAMPLES=1901
COMMAND_ACK_P50=570 ms
COMMAND_ACK_P95=1247 ms
COMMAND_ACK_P99=1725 ms
COMMAND_ACK_MAX=2247 ms
WS_EVENT_METRIC=CLIENT_FRAME_TO_HANDLER
WS_EVENT_SAMPLES=7604
WS_EVENT_P50=0 ms
WS_EVENT_P95=0 ms
WS_EVENT_P99=0 ms
WS_EVENT_MAX=3 ms
MATCHMAKING_SAMPLES=60
MATCHMAKING_P50=3726 ms
MATCHMAKING_P95=5421 ms
MATCHMAKING_P99=6103 ms
MATCHMAKING_MAX=6103 ms
REST_SAMPLES=364
REST_P50=507 ms
REST_P95=13280 ms
REST_P99=18955 ms
REST_MAX=21067 ms
HTTP_5XX=0
HTTP_429=0
AUTH_FAILURES=0
WS_CONNECT_FAILURES=0
WS_ABNORMAL_CLOSES=0
RECONNECTS=0
COMMAND_FAILURES=0
SEQUENCE_REGRESSIONS=0
DUPLICATE_COMMAND_APPLICATIONS=0
INVALID_TURN_ACCEPTANCES=0
UNAUTHORIZED_DELIVERIES=0
MATCH_CORRUPTIONS=0
REPLAY_SAMPLE_SIZE=0
REPLAY_SAMPLE_RESULT=NOT_RUN_ONLY_1_COMPLETED_MATCH_5_REQUIRED
SERVER6_REGRESSION=PASS_5_HISTORY_5_SNAPSHOTS_3_AVAILABLE_REPLAY_MANIFESTS
REAL_TEST_FIRESTORE_READS=NOT_MEASURED
REAL_TEST_FIRESTORE_WRITES=NOT_MEASURED
REAL_PRODUCTION_FIRESTORE_CALLS=0
CPU_STOP_TRIGGERED=NO
MEMORY_STOP_TRIGGERED=NO
CRITICAL_ERROR_STOP_TRIGGERED=NO
GLOBAL_STOP_TRIGGERED=YES
GLOBAL_STOP_REASON=COMMAND_ACK_P95_STAGE_GATE_1247_MS_GT_1000_MS_AT_60_USERS
POST_LOAD_ACTIVE_WS=0
POST_LOAD_PENDING_MATCH_WORK=0
POST_LOAD_ACTIVE_RENEWAL=0
POST_LOAD_REDIS_CONVERGENCE=PASS
ORPHAN_SWARM_PROCESSES=0
CONFIRMED_RESOURCE_LEAKS=0
RECOVERY_DURATION=542.4 s
SOURCE_CHANGE=NO
SERVER_CONFIGURATION_CHANGED=NO
COMMIT=NONE
PUSH=NONE
SERVER_7B_R2_SUCCESS=NO
ADVANCE_TO_250=NO_PENDING_REVIEW
NEXT=SERVER-7B-R2 REVIEW

```

Evidence directory: `Generated/SERVER7B-R2-78c7d326/`. Key files: `run.json`, `stages.json`, `timeline.jsonl`, `baseline-resources.json`, `server-resources.jsonl`, `metrics-summary.json`, `load-result.json`, `client-registry.json`, `recovery-final-discovery.txt`, `recovery-confirm-discovery.txt`, `final-registry-audited.json`, `authoritative-outcomes.txt`, `redis-recovery-final.jsonl`, `server-final.json`, `server6-after.json`, `recovery-validated.json` and per-group logs. Earlier R1 and blocked-R2 artifacts remain untouched.
