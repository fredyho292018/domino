# S7-05B — real singleton reconciliation

The only authorized legacy match was cancelled through the deployed domain worker and repository transaction. No direct Firestore patch, Redis deletion, synthetic History, new match, or load run was performed.

## Checkpoint and deployment

Exact checkpoint includes 14 reviewed S7-05R source, test and report files. The two unrelated pending Gradle tasks were excluded from its staged blob; unrelated Unity/Firebase/provisioning files were preserved. Secret-pattern scanning and index scope checks passed. Checkpoint source was exported from Git and built separately from the working tree. Exact-source validation passed: 38 backend tests, 44 Swarm tests, and the Python suite passed 11 tests. Prior S7-05R emulator evidence covers four backend and one discovery integration tests; these were not represented as new real-server tests.

API image: `cuban-domino-api:a4a2542363d93301bbede958b18bbce9fb3e8d1f`; image digest `sha256:c4b7e6260dae1aa814644dad68cb82fbde3f512fb56a6fed54beae5c720926df`. Only API was replaced. Server operator verified identical Redis container/image/start time/restart count, CPU, memory, mounts, port bindings, security settings and existing environment including JVM options. Operational overlay is `/opt/cuban-domino-club/server/s705b-overlay.json`. The preceding overlay/compose/gate were backed up with root-only permissions. No credentials were printed or copied to the repository.

## Bounded real evidence

Authoritative discovery used all 100 dedicated LOAD identities and the committed reader, unioning active roots, runtime, assignments and pending work. Before reconciliation exactly one active LOAD match existed, matching the authorized ID; unclassified count was zero. The current operation discovery window also included the already cancelled S7-04B lifecycle smoke; it was not attributed to R1. Historical attribution of the residual remains R1_GROUP_05_CORRELATED, not a persisted run ID.

A 49-match exact snapshot included the residual plus 48 controls (prior migration, SERVER-6 and unrelated controls). Root/runtime hashes and captured event/History hashes were identical before versus after deployment with gate closed. After reconciliation all 48 controls remained identical. This is a bounded control comparison, not a claim to have hashed every unrelated document in the project.

Singleton operation `42055285-0657-4942-9c48-b567304cc10a`, pinned list hash `198676e03362fc4a49614e3cb94c80c05c9ec616b7bf6c7906963beece71e02c`. Safe audit match fingerprint `64e275bc13ed3dd80cf4d1cf2afc26c67812a880cbc29e75fb8f4557609c0ecb`. One committed domain completion was observed. The operator closed the shared gate automatically after 7.131 seconds, then observed Redis for at least 150 seconds. It did not deliberately trigger a second mutation.

The 15 original event hashes were preserved; one MATCH_FINISHED/CANCELLED event was added (16 total). Persisted lifecycle marker remains legacy (absent/version 0). All four participant History entries remain absent. Authenticated Replay returns HTTP 409 and does not offer a complete replay. Pending work is absent. Four residual activity keys expired; all eight sampled activity/player keys were absent after the TTL window and the match was absent from the Redis due set. No further completion audit appeared. No active renewal or continuing scheduled work was observed; this conclusion combines work absence, due-set absence and TTL convergence, rather than an invented per-match worker counter.

Final authoritative LOAD discovery: active=0, unclassified=0, baselineReady=true. SERVER-6 retains five History entries, five authenticated snapshots and the same three available replay manifests; no gameplay was initiated. The prior S7-04B normal lifecycle cancellation PASS remains valid; no additional lifecycle-test match was created.

## Requested final output

```makefile
SERVER-7 S7-05B REAL SINGLETON RECONCILIATION
=============================================
SOURCE_SHA_BEFORE=639fd73399106cd4fc542d09f1e113f6b1fc185c
SOURCE_SHA_AFTER=a4a2542363d93301bbede958b18bbce9fb3e8d1f
S7_05R_COMMITTED=YES
S7_05R_DEPLOYED=YES
COMMIT_CREATED=YES
COMMIT_SHA=a4a2542363d93301bbede958b18bbce9fb3e8d1f
PUSH=PASS_ORIGIN_MAIN
API_DEPLOY=PASS
API_HEALTH=healthy
REDIS_RESTARTED=NO
AUTHORITATIVE_ACTIVE_LOAD_MATCHES_BEFORE=1
AUTHORITATIVE_ACTIVE_LOAD_MATCH_IDS_MATCH_EXPECTATION=YES
RESIDUAL_MATCH=815ea96e-fdfe-4e4d-b986-60eced10b72b
MATCH_STATE_BEFORE=IN_PROGRESS
PERSISTED_LIFECYCLE_VERSION=0
CONNECTED_PARTICIPANTS=0
ABANDONED_PARTICIPANTS=4
PARTIAL_EVENTS_BEFORE=15
NORMAL_HISTORY_BEFORE=0
FULL_REPLAY_BEFORE=NO
UNINTENDED_TRANSITIONS_BEFORE_SINGLETON=0
SINGLETON_SCOPE_COUNT=1
SINGLETON_PINNED_HASH_VALID=YES
SINGLETON_GATE_OPEN=YES_DURING_AUTHORIZED_WINDOW_ONLY
SINGLETON_RECONCILIATION_REQUESTED=1
SINGLETON_RECONCILIATION_SUCCEEDED=1
SINGLETON_RECONCILIATION_FAILED=0
MATCH_STATE_AFTER=CANCELLED
COMPLETION_REASON_AFTER=CANCELLED
PARTIAL_EVENTS_PRESERVED=15
CANCELLATION_EVENTS_ADDED=1
TOTAL_EVENTS_AFTER=16
NORMAL_HISTORY_CREATED=0
FULL_REPLAY_AVAILABLE_AFTER=NO
SINGLETON_GATE_ENABLED_AFTER_RECONCILIATION=NO
DUPLICATE_CANCELLATION_EVENTS=0
MATCH_REDIS_RENEWAL_ACTIVE_AFTER=NO
MATCH_EPHEMERAL_REDIS_CONVERGENCE=PASS
MATCH_PENDING_WORK_AFTER=0
AUTHORITATIVE_ACTIVE_LOAD_MATCHES_AFTER=0
UNCLASSIFIED_LOAD_MATCHES=0
SERVER6_REGRESSION=PASS
LEGACY_RECONCILIATION_GATE=CLOSED
SINGLETON_RECONCILIATION_GATE=CLOSED
SERVER7_R2_BASELINE_READY=YES
R2_MATCHES_STARTED=0
SERVER7_100_RERUN_STARTED=NO
SERVER7_250_STARTED=NO
SERVER_CONFIGURATION_CHANGED=YES_API_IMAGE_AND_SINGLETON_OPERATIONAL_OVERLAY_ONLY
CLOUDFLARE_CONFIGURATION_CHANGED=NO
DNS_CHANGES=0
S7_05B_SUCCESS=YES
NEXT=SERVER-7B-R2 FINAL AUTHORIZATION REVIEW
```

Evidence: `Generated/S705B/checkpoint-files.json`, `checkpoint-test-results.json`, `server-precheck.json`, `server-deployed.json`, `discovery-before.txt`, `discovery-after.txt`, `firestore-before.json`, `firestore-deployed-closed.json`, `firestore-after.json`, `api-before.json`, `api-after.json`, `reconciliation.json`, `server6-before.json`, `server6-after.json`, `validated-result.json`. Final report is local evidence and was not included retroactively in the checkpoint SHA.
