# S7-05R — singleton gate and authoritative registry (review only)

Base: `639fd73399106cd4fc542d09f1e113f6b1fc185c`. No commit, push, deployment, real-data mutation, service restart or load execution. Existing unrelated Unity/Firebase/provisioning edits were preserved. The prior exact-22 migration remains requested=22, succeeded=22. Historical residual attribution remains `R1_GROUP_05_CORRELATED`, not a persisted run ID.

## Singleton authorization

`FileAbandonedMatchReconciliationGate` retains default `EXACT_22` and the old four-field file format. New `SINGLE_MATCH` requires all independent pins to agree: file mode, configured mode, canonical operation UUID, exact configured match ID, one-entry file list and SHA-256 of the sorted list with trailing LF. Shared read-only directory and atomic file replacement remain required across instances. Missing/invalid/disabled config fails closed on every evaluation; there is no cached open decision.

The reviewed residual is `815ea96e-fdfe-4e4d-b986-60eced10b72b`; singleton hash is `198676e03362fc4a49614e3cb94c80c05c9ec616b7bf6c7906963beece71e02c`. Disabled local examples are in `Generated/S705R/gate-disabled.example.json` and `operation-pins.example.json`; they were NOT installed remotely. These examples reserve a separate operation UUID, not the old migration/run ID.

New operational properties (future reviewed deployment only):

```properties
domino.online.legacy-reconciliation.mode=SINGLE_MATCH
domino.online.legacy-reconciliation.match-id=815ea96e-fdfe-4e4d-b986-60eced10b72b
# file, run-id and registry-sha256 must match the separately reviewed operation pins
```

Worker and connection paths still use the same `OnlineEngine` cancellation predicate and `OnlineMatchService` repository transaction. Singleton policy additionally restricts legacy state to IN_PROGRESS. The server-only `reconcileLegacySingleton` method reuses the same transaction helper and rechecks inside the transaction; it has no HTTP endpoint. It rejects current/unknown versions and terminal/wrong-state matches. All seats must be ABANDONED, so connected/disconnected seats cannot be migrated. Version 1 normal lifecycle cancellation bypasses legacy authorization as before. The persisted marker is never rewritten.

Deterministic existing command receipts make concurrent/repeated calls idempotent. Completion audit occurs only after an acknowledged write, and reports operation UUID plus SHA-256 match fingerprint. Participant UIDs and raw Match IDs are not included in these migration audit messages. ALLOWED is a decision, not proof of a committed write; use COMPLETED for that distinction. Closing a file does not revoke an already-authorized transaction in flight.

## Authoritative discovery

New read-only administrative Gradle task: `:bot-swarm:capacityDiscover`. It is isolated from the simulated-client runtime and requires TEST project `teamfho-domino`, real-read opt-in, explicit matching operation/run ID and the existing 100 distinct LOAD identities from their external directory. It performs no queue/gameplay/write calls.

Manifest fields: `runId`, `startedAt`, `through`, `loadDirectory`, `clientMatchIds`. Set `DOMINO_SWARM_FIREBASE_PROJECT_ID=teamfho-domino`, `DOMINO_CAPACITY_READ_RUN_ID` to that manifest run ID and `DOMINO_REAL_FIRESTORE_TESTS=true` only in a separately authorized real-read execution. No real invocation occurred in S7-05R.

Sources are unioned before ownership filtering:

- All match roots created in the bounded run window, including terminal matches. Paginated createdAt query with a one-second boundary margin; exact Instant comparisons enforce scope.
- All active root statuses, regardless of run age, to detect residual LOAD matches missing from the client registry.
- Pending work selected by each dedicated LOAD UID and current authoritative assignments.
- Exact client-observed IDs, which must also pass authoritative membership and window checks.

The policy requires four distinct participants, all in the dedicated 100-identity set, validationData=true, in-window creation, and consistent root/runtime status. Mixed/unknown/missing or inconsistent scope prevents completeness; foreign/out-of-window IDs do not enter the run registry. Active LOAD baseline count covers any touched LOAD match, even outside the new run window. Query bounds and missing documents fail closed rather than silently producing an incomplete PASS. Discovery is not a global atomic snapshot; repeated reconciliation is required as state changes.

Output includes client-observed, authoritative-discovered, union, client-missed and unclassified counts plus safe slot references. `CapacityRegistry.reconcile_discovery` atomically merges the authoritative output into the existing coordinator registry; client-only records cannot erase missing authority evidence. `RegistryFinalization` requires client exit, at least 360 seconds after STOP and two consistent terminal/clean observations at least two seconds apart. The query window extends through observation time, so a backend commit appearing after initial STOP is still discovered. Arbitrarily delayed external writes cannot be excluded by any finite observation; LOAD identities must remain exclusively owned by that run until finalization.

Future R2 must invoke discovery at baseline, at stage checks and repeatedly after STOP, ingest output into the registry, and require zero unclassified matches before finalization. Baseline must use `activeLoadMatches`/`baselineReady`, not the run-local match count. The historical R1 launcher and evidence were not rewritten or rerun. These are reusable tooling components, not authorization to start R2.

## Validation and boundaries

Focused singleton/legacy/abandonment tests cover exact ID, wrong ID, incorrect state/version, malformed/empty config, normal version-1 bypass, old format compatibility, shared-file reload, closing between precheck and commit, idempotency and safe audit correlation. Firestore emulator tests verify concurrent singleton transactions produce one terminal event, preserve legacy marker, remove work and create no History. Existing exact-22 and normal lifecycle emulator regressions remain included.

Registry unit tests cover missed assignment, deduplication, foreign/window exclusion, inconsistent/missing authority, match-26 rejection, old active baseline and STOP race. The actual Firestore adapter is exercised against the local emulator, including terminal matches never observed by clients and a late commit after the first STOP read. Python tests cover merging into the existing registry without partial updates.

Only emulator fixture documents were written by tests. Real residual, Redis and SERVER-6 were untouched. SERVER-6 validation here means local History/Replay regressions; remote replay consumption was not repeated. Initial test setup failures (wrong Gradle task filter, an independently regenerated fixture comparison, and emulator credential import) were corrected before final passing runs.

Final validation: **98 tests passed, zero failures/errors/skips**: 38 focused backend/History/Replay tests, four backend emulator tests, 44 Swarm unit tests, one actual-reader emulator test and 11 Python tests. Test counts and evidence are stored in `Generated/S705R/test-results.json`. Emulator process and port cleanup passed. S7_05R_SUCCESS=YES for local implementation and validation only. STOP for review. Next separately authorized operation is S7-05B real singleton reconciliation; R2 remains disallowed.
