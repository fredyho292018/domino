# S7-04 — checkpoint/deployment preflight stopped at worker gate

Base verified: branch `main`, HEAD `1d0c3bf3fc5b1aba427b5ab0861372895a7c9e38`. No index entries were staged. No remote operation, commit, push, build deployment, container replacement or reconciliation was executed in S7-04.

## Phase A inventory

The 25 S7-03 checkpoint candidates still match every recorded SHA-256. Inventory of all 144 pending paths: 3 S7_01R_BACKEND, 3 S7_01R_TESTS, 13 S7_02_OBSERVABILITY, 3 S7_02_TESTS, 1 S7_03_RECONCILIATION_PLAN, 2 S7_REPORTS, 46 GENERATED_VALIDATION and 73 PREEXISTING_USER_FILE; no unclassified paths. Generated files and preexisting user files are excluded. This new review report is a separate added artifact after the inventory. Exact paths/status/classification: `Generated/S704/pending-inventory.json`; candidate manifest remains `Generated/S703/checkpoint-inventory.json`.

Candidate scan passed against locally available slot identity values (UID/token/password/private fields), private-key headers and JWT-shaped values. No matched secret or raw slot UID was printed or written into the checkpoint candidates. This targeted scan is not a claim of exhaustive detection of every possible credential format. No candidates were staged. Shared Gradle/Swarm files also contain preexisting tooling hunks and need hunk-level closure before checkpoint: tasks referencing untracked historical entry points must not silently become unusable in the committed tree. The 25-path manifest is therefore a candidate inventory, not proof of an independently buildable clean commit.

S7-01R still uses the existing reconnect window from the frozen mode policy (the reviewed mode is 180 seconds), CANCELLED/null winner/unchanged score, no normal completed History, and retained partial events. S7-02 sources/evidence still report JVM processors=4 and independent cgroup normalization with Docker CPU excluded from STOP. No code or evidence was changed to force those gates to pass.

## Phase B blocking evidence

S7-03 explicitly required designing/testing a worker gate and did not specify or implement an existing operational switch. `OnlineConfiguration` has no reconciliation pause/allowlist setting. `OnlineTurnWorker` claims the shared due index and calls `service.abandon` for each nonterminal match. The shared index was observed with 44 members, only 22 in the authorized registry.

Cancellation has **two entry paths**: `OnlineEngine.connection` invokes `cancelIfAllAbandoned` when a participant becomes abandoned, and `OnlineEngine.abandon` invokes it for legacy all-abandoned state. Suppressing only `worker -> service.abandon` would leave the connection-driven transition enabled. A process-local pause is also insufficient evidence for another instance or restart. Therefore no existing reviewed gate establishes the requested mutation boundary before deployment.

The user task says in phase B: “If this requires a new unreviewed architecture: STOP.” S7-03 approved a strategy, not the operational control design. Implementing an ad hoc flag or remote control path now and calling it reviewed would bypass that condition. Work stops before phase C; current API/Redis remain untouched.

## Concrete gate design for the next review

Review a single cancellation-authorization policy at the shared engine predicate, covering both entry paths while leaving ordinary commands, reconnection/grace bookkeeping and timeout handling unchanged. During the migration window:

1. Automatic cancellation of pre-deployment matches is denied, including the exact 22 until explicit reconciliation. Missing/invalid gate configuration fails closed for this new transition, not by corrupting ordinary unrelated work.
2. An explicit, non-public, one-shot administrative runner loads the exact registry plus hash, validates TEST ownership and expected state, and calls `OnlineMatchService.abandon`/the existing repository transaction with an explicit scope. No Firestore field patching or global ACTIVE query is allowed. It must have an audit ledger and fail on scope drift.
3. The same policy permits automatic abandonment for the separately authorized newly created four-player validation match; its scope/cutover definition must be specified, immutable for the process and validated across all instances. A broad enable switch cannot be used before phase G/H verification.
4. Tests must cover both cancellation paths outside the allowlist, absent/invalid configuration, duplicate/concurrent execution, process restart, exact-22 isolation and the one new match after 180 seconds. Define mixed-version deployment and rollback behavior before use.

This is a proposed control design, not implementation or deployment authorization beyond the task's stop condition. The exact mechanism for passing administrative scope, deployment configuration and multi-instance fencing requires review. No new endpoint or runner was created.

## Status

SOURCE_SHA_AFTER remains the base; COMMIT_SHA=NONE; FILES_COMMITTED=0; PUSH=NO; API_DEPLOY=NOT_ATTEMPTED; RECONCILIATION_WORKER_GATE=NOT_IMPLEMENTED_REVIEW_REQUIRED. UNRELATED_WORK_PROTECTED=NO_DEPLOYMENT_PERFORMED, not a claim that the current candidate image is gated. Reconciliation requested by scope=22, attempted=0, succeeded=0, execution failures=0. Post-deployment/reconciliation/remote-smoke outcomes are NOT_RUN, not successful zeros. Current server health and data were not remeasured in this preflight.

SERVER_CONFIGURATION_CHANGED=NO; FIRESTORE_DATA_MUTATED=NO; REDIS_DATA_DELETED=NO; API_RESTARTED=NO; REDIS_RESTARTED=NO; CLOUDFLARE_CONFIGURATION_CHANGED=NO; DNS_CHANGES=0; SERVER7_100_RERUN_STARTED=NO; SERVER7_250_STARTED=NO.

S7_04_SUCCESS=NO. NEXT=S7-04 WORKER GATE DESIGN REVIEW. Do not advance to capacity review while these prerequisite gates remain unresolved.
