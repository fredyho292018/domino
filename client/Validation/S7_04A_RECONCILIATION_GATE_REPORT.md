# S7-04A — temporary legacy reconciliation gate

Scope: implementation and local validation only. No checkpoint, push, deployment, remote load, real Firestore mutation or change to the real 22 matches. Base remains `1d0c3bf3fc5b1aba427b5ab0861372895a7c9e38`.

## Decision and legacy detection

`AbandonedMatchReconciliationGate` is the single legacy authorization abstraction. Both `OnlineEngine.connection` and `OnlineEngine.abandon` reach the same `Builder.cancelIfAllAbandoned` decision. No allowlist logic was added independently to the worker. Audit of the online engine found these two abandonment-cancellation entry points; the one terminal builder is the only place that constructs the all-abandoned CANCELLED result.

New matches created by either application creation path receive `abandonmentLifecycleVersion=1`. Existing matches with no marker decode as version 0. The marker is server-owned, assigned only at creation and invariant under domain transactions. Unknown marker versions fail closed. This is an explicit persisted lifecycle contract, not a timestamp heuristic, process-start time or membership in LOAD users. Normal version-1 lifecycle never consults the legacy file, so missing, empty or malformed legacy configuration does not disable new-match cancellation after the existing 180-second grace.

The marker is a sibling field on `matches/{id}/runtime/authoritative`, outside `stateJson`. Old images already read only stateJson, so the added metadata does not introduce an unknown JSON property into their decoder. An old image writing that document may remove the marker; on later upgrade absence is conservatively legacy/closed. Mixed-version writers must not be left running during the controlled migration; this is part of the deployment checklist, not a claim of cross-version feature compatibility.

## Operational file and fail-closed behavior

Spring configuration properties:

* `domino.online.legacy-reconciliation.file`: absolute container path to an externally mounted JSON file; blank means closed.
* `domino.online.legacy-reconciliation.run-id`: exact approved run UUID.
* `domino.online.legacy-reconciliation.registry-sha256`: expected SHA-256 of sorted canonical match UUIDs, joined by LF with one trailing LF, encoded UTF-8.

File schema: version=1, runId=expected run, enabled=boolean, matchIds=array of exactly 22 distinct canonical UUID strings. IDs are supplied operationally and are not hardcoded in source. Unit tests use synthetic IDs. Every read checks count, uniqueness, canonical UUID format, expected run and registry hash. An empty allowlist closes legacy handling. Absent/unreadable/nonregular/oversized/malformed/unknown-field configuration closes it too. No file path, contents, UID, credentials or list of match IDs is logged.

Mount the same **directory** read-only into every API replica, backed by the same authoritative host filesystem on the current single-server deployment. Do not bind-mount only the file: replacing its inode may leave a container reading an old copy. Root owns the directory/file and publishes complete configurations by atomic rename. Give the API only the minimum read permission. Preflight all replicas' image, run ID, registry digest and mount source; differing configuration is a deployment failure. A multi-host fleet without a coherent shared source is not validated by this file-based design and must not be treated as supported automatically.

The policy caches no open decision: each preliminary and transactional evaluation rereads the source. Two independent instances and a reconstructed instance see open/closed changes from the common file. Closing between preliminary evaluation and transaction prevents commit in the test. A transaction already authorized and in flight can finish after a file change; closing is not retroactive revocation of a committed/in-flight transaction. After the exact 22 are confirmed terminal, close and drain/observe before declaring migration finished. Idempotent receipts prevent repeat terminal events.

## Behavior and unaffected processing

For a legacy state already entirely ABANDONED, an unauthorized worker evaluation produces no domain write. For the connection update that would make the last legacy seat ABANDONED and cancel the match, a denied gate returns no write for that combined transition: the pre-update authoritative state remains unchanged. This intentionally defers that boundary for protected legacy matches; earlier ordinary disconnect/reconnect processing remains intact. Existing scheduler discovery bookkeeping can still refresh due hints; “unchanged” here is the authoritative match/events, not a claim that the entire shared scheduler stops writing.

An authorized legacy state uses the unchanged S7-01R domain transaction: CANCELLED, null winner, existing score, one terminal event, no normal completed History, retained partial events and removed pending work. The gate does not patch Firestore directly. New version-1 matches behave normally regardless of legacy gate state. Completed matches remain terminal and do not consult the gate.

Safe log event names are `LEGACY_RECONCILIATION_ALLOWED`, `LEGACY_RECONCILIATION_BLOCKED` and `LEGACY_RECONCILIATION_COMPLETED`. Allowed/blocked events count evaluations, including retries/preflight, not distinct matches. Completed is emitted only after a committed write, never for a receipt-only duplicate. These logs are operational evidence, not a durable exactly-once audit ledger; per-match terminal/event verification remains mandatory in S7-04.

## Required deployment sequence — prepared, not executed

1. Deploy the reviewed image with the file missing or `enabled=false`, identical pinned run/digest configuration and the same mounted directory for every instance. New match creation stamps version 1; legacy evaluation is closed.
2. Wait for healthy API. Verify exact old-22 and unrelated shared-index authoritative snapshots have not unexpectedly transitioned. Check no ungated/mixed-version instance remains.
3. Prepare the exact 22-ID registry file outside Git. Validate its digest against the authorized registry and keep `enabled=false` while checking configuration. No global match query defines migration membership.
4. Atomically publish `enabled=true` only after explicit reconciliation authorization. Existing durable-work processing then invokes the normal domain transaction on eligible allowlisted legacy matches. Other legacy IDs remain denied; normal new matches remain functional.
5. Verify all 22 CANCELLED, original 1,903 events preserved, 22 appended terminal events, no normal History, no full Replay, no durable old-run work and Redis convergence through natural TTL. Do not count global key changes as proof.
6. Atomically set `enabled=false` (or remove the file), verify every replica observes closed state, and drain any prior in-flight evaluation. Leave the legacy gate closed permanently after migration.
7. Perform the separately authorized one-new-match lifecycle proof under S7-04. Its version-1 behavior does not require adding its ID to the legacy allowlist. No such match was created remotely in S7-04A.

Rollback still cannot undo already CANCELLED matches. Preserve previous API image/config and Redis, but inspect lifecycle marker preservation when an older writer has been used. Do not silently reclassify a legacy match as new to make cancellation happen.

## Files changed in S7-04A

1. `server/domino/src/main/kotlin/com/teamfho/domino/online/AbandonedMatchReconciliationGate.kt`
2. `server/domino/src/main/kotlin/com/teamfho/domino/online/OnlineConfiguration.kt`
3. `server/domino/src/main/kotlin/com/teamfho/domino/online/OnlineEngine.kt`
4. `server/domino/src/main/kotlin/com/teamfho/domino/online/OnlineModels.kt`
5. `server/domino/src/main/kotlin/com/teamfho/domino/online/OnlineMatchService.kt`
6. `server/domino/src/main/kotlin/com/teamfho/domino/online/OnlineRepository.kt`
7. `server/domino/src/test/kotlin/com/teamfho/domino/online/AbandonedReconciliationGateTests.kt`
8. `server/domino/src/test/kotlin/com/teamfho/domino/online/AllAbandonedTests.kt` (new-runtime fixture explicitly marked version 1)
9. `server/domino/src/test/kotlin/com/teamfho/domino/online/AllAbandonedEmulatorTests.kt`
10. This report.

Prior candidate hashes for touched files are intentionally superseded; refresh the exact checkpoint inventory during S7-04 review. No unrelated Unity/Firebase changes are included.

## Validation

Final test results are recorded after completion in `Generated/S704/final-tests.txt` and `Generated/S704/s704a-verification.json`. Coverage includes exact synthetic 22/22 policy decisions, actual worker, last-seat connection path, missing/empty/disabled/malformed config, wrong digest, duplicate IDs, unknown fields/version, new match at grace, reconnect before grace, immutable marker, two instances/reconstruction, gate closure before transaction, concurrent emulator transactions and backward-readable stateJson. Existing gameplay/History/Replay regressions run in the backend suite. SERVER-6 regression means local contract regression here; the five remote SERVER-6 matches were not accessed or mutated during this task.

Final results: backend 656 discovered, 632 executed, 24 conditionally skipped, zero failures/errors; emulator 3 executed, zero failures/errors. The owned emulator was stopped after validation. Secret/known-identity scan passed and none of the real 22 match IDs occurs in the changed source/report. No real data was accessed or changed by these tests.

```text
SERVER-7 S7-04A RECONCILIATION GATE
FILES_CHANGED=10
RECONCILIATION_GATE_IMPLEMENTED=YES
RECONCILIATION_GATE_TYPE=SHARED_READ_ONLY_FILE_EXACT_UUIDS_PINNED_REGISTRY_HASH
LEGACY_RECONCILIATION_DETECTION=PERSISTED_SERVER_LIFECYCLE_VERSION_ABSENT_IS_LEGACY
RECONCILIATION_ALLOWLIST_SUPPORTED=YES_EXACTLY_22
ALLOWLIST_NOT_HARDCODED=YES
RECONCILIATION_GATE_FAIL_CLOSED=YES
ABANDON_CANCELLATION_ENTRY_POINTS=WORKER_ABANDON_AND_CONNECTION_SHARED_ENGINE_PREDICATE
WORKER_PATH_GATE=PASS
CONNECTION_PATH_GATE=PASS
LEGACY_VS_NORMAL_LIFECYCLE_SEPARATED=YES
AUTHORIZED_LEGACY_MATCH=PASS
UNAUTHORIZED_LEGACY_MATCH_UNCHANGED=PASS_AUTHORITATIVE_STATE
NEW_MATCH_ALL_ABANDONED=PASS
NEW_MATCH_RECONNECT_WITHIN_GRACE=PASS
EMPTY_ALLOWLIST_TEST=PASS
MALFORMED_CONFIG_TEST=PASS
MULTI_INSTANCE_GATE=PASS_WITH_SHARED_MOUNT_AND_IDENTICAL_PINNED_CONFIGURATION
SERVER6_REGRESSION=PASS_LOCAL_CONTRACT_SUITE
LEGACY_RECONCILIATION_ALLOWED_METRIC=SAFE_LOG_IMPLEMENTED
LEGACY_RECONCILIATION_BLOCKED_METRIC=SAFE_LOG_IMPLEMENTED
LEGACY_RECONCILIATION_COMPLETED_METRIC=POST_COMMIT_SAFE_LOG_IMPLEMENTED
DEPLOYMENT_SEQUENCE_READY=YES
LEGACY_RECONCILIATION_ENABLED_AFTER_MIGRATION=NO_REQUIRED_BY_SEQUENCE
REAL_22_MATCHES_MUTATED=0
NEW_LOAD_EXECUTED=NO
SERVER7_100_RERUN_STARTED=NO
SERVER_CONFIGURATION_CHANGED=NO
FIRESTORE_DATA_MUTATED=NO_REAL_DATA_EMULATOR_ONLY
REDIS_DATA_DELETED=NO
COMMIT=NONE
PUSH=NONE
DEPLOY=NONE
S7_04A_SUCCESS=YES
NEXT=S7-04A REVIEW
```
