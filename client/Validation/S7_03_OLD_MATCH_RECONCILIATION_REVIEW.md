# S7-03 — Old abandoned match reconciliation review

Mode: read-only final review and migration proposal. No deployment or reconciliation is authorized by this task. Base: `1d0c3bf3fc5b1aba427b5ab0861372895a7c9e38`. Run: `275abc49-45e7-40d0-8019-11531e6c8d48`.

## Current evidence

Fresh exact-ID Firestore read: 22 ACTIVE, zero CANCELLED/COMPLETED/FAILED, 88 ABANDONED participant records, zero CONNECTED/RECONNECTED participant records, zero History records, 1,903 replay event documents and 22 durable onlineTurnWork documents. All 22 satisfy the nonterminal/nonempty/all-participants-ABANDONED predicate according to these persisted participant records. Connected participant state is not itself proof of live socket count. Current Redis/socket evidence is pending the root-side read-only observer.

Exact registry and per-match snapshot: `Generated/S703/registry.json` and `Generated/S703/snapshot.json`. The 22 unique IDs have zero intersection with all five IDs in `Generated/SERVER7B/server6-before.json`. One SERVER-6 comparison was also read by the existing observer; no SERVER-6 documents were written. All 100 local LOAD slot files remain present; no identity deletion/provisioning action occurred.

The existing diagnostic labels CANCELLED as FAILED; S7-03 normalizes that outdated label when root/runtime both say CANCELLED. Current results are all IN_PROGRESS, so this does not alter current counts. Snapshot reads span time, not one global transaction; a fresh check is mandatory immediately before any future mutation.

## What the deployed worker would do

`OnlineTurnWorker.tick` runs every five seconds by default. `TurnIndexBridge` acquires a Redis lease and subscribes to the durable work feed, including its initial snapshot. This reconstructs the due index after restart or Redis loss. Due claims are bounded to 100 and leased forward 30 seconds; connection dirtiness can also discover work by participant. The 22 durable work documents therefore make these matches automatically discoverable. This is a durable Firestore work feed plus Redis due scheduling, not a periodic query of all ACTIVE match documents. Storage failures/backoff can delay convergence; five seconds is not a guaranteed completion SLA.

For each discovered nonterminal match, worker checks participant connections, calls `service.abandon`, reloads state, publishes the final presence observation and forgets terminal due work. `OnlineEngine.cancelIfAllAbandoned` checks nonterminal state, a nonempty participant list and every participant ABANDONED. Already ABANDONED seats do not require a new grace wait. FINISHED/CANCELLED are no-ops. This implementation **does explicitly reconcile old persisted state automatically**.

Idempotency: deterministic `sys_abandon_<lastSequence>` command ID, command receipt/fingerprint, expected-sequence comparison and a rechecked transition inside the Firestore transaction. One concurrent transaction wins; duplicates return the receipt or become a stale/no-op evaluation. Existing emulator tests cover competing instances, repeated cycles and one terminal event; Redis integration covers no terminal renewal and natural 120-second expiry. These are existing verified S7-01R results, not tests run against the real 22 in S7-03.

## Predicted changes per eligible match

* Root match: CANCELLED, currentSeat=null, finishedAt=transition time, result with winner=null and finishReason=CANCELLED, existing score unchanged; updatedAt and lastSequence advance.
* Authoritative runtime: same terminal match, phase=MATCH_FINISHED, turnStartedAt/turnDeadlineAt=null. Existing partial state is retained.
* One new public MatchFinished event carrying the cancellation result. Existing event documents and rounds are retained. One deterministic command receipt is created.
* onlineTurnWork document is deleted atomically. No normal History is produced. Unchanged participant documents are not rewritten by the repository's diff check. Player assignment/creation receipts are not deleted by this cancellation transaction.
* Redis: remove the exact due member; publish/flush the terminal inactive presence projection, then stop its periodic renewal. Activity keys expire naturally after 120 seconds from the last successful terminal write. Shared scheduler leadership remains; unrelated matchmaking/presence keys remain.

There is no fabricated win/loss or score. ReplayService requires FINISHED, so CANCELLED cannot obtain full Replay. Its current non-FINISHED availability label is ACTIVE_MATCH even for cancellation; unavailable is the correct semantic prediction, not a claim of a dedicated CANCELLED response code.

**Event preservation means the original 1,903 events remain, not that the final count stays 1,903.** If all 22 remain in the captured state, cancellation appends 22 terminal events and yields 1,925 total. Verification must compare original IDs/content fingerprints and validate the appended cancellation events; count alone does not prove preservation.

## Options and recommendation

| Option | Evidence and risk | Assessment |
| --- | --- | --- |
| A: deploy and let the normal worker run | Uses tested idempotent lifecycle path and needs no data migration script. Worker scope is global durable work, with no old-run allowlist. Other eligible matches could transition too. | Technically sufficient, but does not establish an exact-22 mutation boundary. |
| B: controlled reconciliation with automatic handling paused/gated | Can establish an exact-ID boundary, record before/after evidence and invoke the same domain transaction. Requires a reviewed control mechanism and validation before deployment; none is assumed to exist today. | Recommended for this exact-scope request. |
| C: leave the 22 indefinitely | Preserves current bytes but violates the intended lifecycle and retains work/projection renewal. Deploying the current worker would not leave them untouched. | Reject as a permanent strategy. |

Recommend B because scope isolation is mandatory, not because the automatic path is non-idempotent. No direct document patching is needed: the eventual bounded reconciler should call the existing abandonment transition for exactly the registered IDs. Do not add or run it in S7-03. Before an eventual release, design/test an explicit worker gate (there is no claimed existing pause flag), including competing-instance fencing and restart behavior. Merely stopping a thread locally or relying on Redis leadership does not prevent another instance processing work. Keep handling paused until reconciliation and verification complete; restoring ordinary global lifecycle processing is a separately reviewed effect.

## Future execution and verification plan

1. Freeze/review exact registry hash, source/image digest and authorization. Reject any overlap with SERVER-6 or non-LOAD participants. Preserve the five SERVER-6 records/History/Replay fingerprints and original 22 event fingerprints securely without logging private payloads.
2. Immediately re-read exact root/runtime/participant/work state and live presence for each of the 22. Require consistency, correct TEST ownership, all abandoned and no live LOAD participant socket. Stop on drift or unknown evidence. Never mutate a global ACTIVE query result.
3. Establish and verify the reviewed automatic-worker gate across every instance before installing code with reconciliation effects. Obtain deliberate authorization for irreversible data transitions. Preserve the previous API image digest/configuration, Redis and credential mounts.
4. Invoke the normal transactional abandonment path on the exact IDs, with a per-ID audit ledger. A timeout means re-read receipt/state before retry, not invent a second command. Stop on mismatched predicate or unrelated effects.
5. Verify 22 CANCELLED, zero ACTIVE, cancellation reason and null winner, unchanged score, zero normal History; preserve all original 1,903 events and validate one appended terminal event per match. Confirm full Replay remains unavailable.
6. Confirm zero exact-ID onlineTurnWork documents and zero run members in due scheduling. Observe longer than 120 seconds after the last terminal presence write, including multiple worker cycles. Require no old-run activity renewal and natural expiry of its participant projections. Do not assert a fixed global Redis key count: shared and unrelated TEST activity is legitimate.
7. Compare SERVER-6 snapshots/History/Replay and protected functional identity records with preflight fingerprints. Require zero mutations. Keep all 100 LOAD identities. Stop if any protected object changes unexpectedly.
8. Retain before/after evidence and hashes, deployment digest, per-match receipts and timings. Keep 100-user and 250-user load prohibited pending separate review.

Expected Redis reduction is removal of 22 members from the shared due set and expiry of activity keys attributable to the 88 participants, provided no newer activity for those identities legitimately replaces them. Previous observations also included additional LOAD activity outside those 88; do not attribute or delete those automatically. No FLUSH, broad deletion or fixed global key-count target is part of this plan.

## Rollback and checkpoint

Restore the recorded previous API image/config if deployment health fails, preserving Redis, credentials and the worker gate. **Code rollback does not undo CANCELLED records, appended events or receipts.** Do not revert data by replacing saved documents; any data correction requires separate authorization and an audit-preserving design.

Exact candidate file inventory with hashes is `Generated/S703/checkpoint-inventory.json` (24 existing S7-01R/S7-02 paths), plus this report. Review individual hunks in shared Swarm files because they include prior instrumentation changes. Include the three backend fix files, three abandonment test files, S7-02 metrics/registry/coordinator/agent/collector tests, corresponding Swarm changes and reports. Exclude all Generated artifacts, credentials, Unity/Firebase/Android user changes and unrelated historical tooling. The manifest is a review inventory, not staging authorization. No commit, push or deployment was performed.

Completed current Redis observation: 151.705 seconds, completion marker present, 123 total keys at both endpoints; 92 LOAD-owned activity projections renewed; zero LOAD-owned player connection keys/cardinality; the shared due set has 44 members, exactly 22 in this registry. The other 22 members are not assumed to be active matches or inspected/mutated. This is concrete evidence that normal worker discovery is not restricted to this run. Zero registered LOAD sockets is inferred from absence of the corresponding presence connection sets, not from archived participant state alone. Evidence: `Generated/S703/server-review.jsonl`. The 92 projections represent the broader LOAD population, not an assertion that all 92 belong to the 88 matched participants. No global Redis deletion/count target is justified.

```text
SERVER-7 S7-03 OLD MATCH RECONCILIATION
RUN_ID=275abc49-45e7-40d0-8019-11531e6c8d48
RUN_MATCH_REGISTRY_COUNT=22
SERVER6_MATCHES_IN_OLD_RUN_REGISTRY=0
OLD_MATCHES_ACTIVE=22
OLD_MATCHES_CONNECTED_PARTICIPANTS=0
OLD_MATCHES_ABANDONED_PARTICIPANTS=88
OLD_MATCHES_AUTOMATICALLY_REEVALUATED=YES
REEVALUATION_TRIGGER=DURABLE_WORK_FEED_REDIS_DUE_INDEX_WORKER
OLD_MATCHES_ELIGIBLE_FOR_CANCELLATION=22
OLD_MATCH_CANCELLATION_IDEMPOTENT=YES
EXPECTED_FINAL_STATE=CANCELLED
EXPECTED_COMPLETION_REASON=CANCELLED
NORMAL_HISTORY_CREATED_FOR_OLD_CANCELLED_MATCHES=NO
PARTIAL_REPLAY_EVENTS=1903
PARTIAL_REPLAY_EVENTS_PRESERVED=YES
FULL_REPLAY_AVAILABLE_FOR_CANCELLED_MATCH=NO
REDIS_RENEWAL_STOPS_AFTER_CANCELLATION=YES_FOR_TERMINATED_MATCH_ACTIVITY
REDIS_CLEANUP_MECHANISM=EXACT_DUE_MEMBER_REMOVAL_AND_ACTIVITY_TTL_120_SECONDS
EXPECTED_FIRESTORE_MUTATIONS=ROOT_RUNTIME_TERMINAL_EVENT_COMMAND_RECEIPT_WORK_DELETION
OPTION_A_AUTOMATIC_WORKER=FUNCTIONAL_BUT_NOT_EXACT_RUN_SCOPED
OPTION_B_CONTROLLED_RECONCILIATION=RECOMMENDED_REQUIRES_REVIEWED_WORKER_GATE
OPTION_C_LEAVE_UNTOUCHED=NOT_RECOMMENDED
RECOMMENDED_RECONCILIATION_STRATEGY=B_EXACT_ALLOWLIST_USING_DOMAIN_TRANSACTION
RECOMMENDATION_REASON=EXACT_SCOPE_AUDITABILITY_IDEMPOTENCY_GLOBAL_INDEX_HAS_OTHER_MEMBERS
RECONCILIATION_SCOPE=EXACT_22_MATCH_IDS_ONLY
SERVER6_MATCHES_MUTATED=0
SERVER6_HISTORY_MUTATED=0
SERVER6_REPLAYS_MUTATED=0
LOAD_IDENTITIES_PRESERVED=100
DATA_TRANSITION_IRREVERSIBLE_BY_CODE_ROLLBACK=YES
POST_DEPLOY_VERIFICATION_PLAN_READY=YES
S7_CHECKPOINT_PLAN_READY=YES
NEW_LOAD_EXECUTED=NO
SERVER7_100_RERUN_STARTED=NO
SERVER7_250_STARTED=NO
FIRESTORE_DATA_MUTATED=NO
REDIS_DATA_DELETED=NO
API_RESTARTED=NO
REDIS_RESTARTED=NO
COMMIT=NONE
PUSH=NONE
DEPLOY=NONE
S7_03_SUCCESS=YES
SERVER7_100_RERUN_ALLOWED=NO
NEXT=S7-03 REVIEW
```
