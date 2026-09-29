# S7-05 — residual LOAD match diagnosis (read-only)

## Finding

`815ea96e-fdfe-4e4d-b986-60eced10b72b` is a legacy match predating the S7-04B deployment. It is **not** S7-04B's new-match smoke (`dce4cd74-fa32-4b4d-8a33-7eab10526779`). The latter was authoritatively verified CANCELLED with lifecycleVersion=1. No evidence contradicts that validation.

The residual match was created at `2026-09-26T19:06:50.612395457Z`. Its participants map privately to LOAD group-05 slots 06, 07, 12 and 13. R1 (`275abc49-45e7-40d0-8019-11531e6c8d48`) started at 19:04:38.352195 UTC and ran approximately 136 seconds. Creation therefore occurred about 132 seconds into R1, near shutdown. Its ID is absent from the final R1 client-derived registry, whose group-05 log reports only two matches found. The last coordinator timeline was at elapsed 122.734 seconds. This supports an R1 assignment created near shutdown that was not captured by client match-found logs. **The match has no persisted runId/testRunId and no retained client log names this ID; run attribution is a temporal/participant correlation, not a persisted run-ID proof.**

The exact-22 migration correctly excluded this unregistered match. The defect is incomplete run ownership discovery/reporting around shutdown; extending that exact migration silently would have violated its authorization.

## Authoritative state and lifecycle

Root/runtime IN_PROGRESS, no result/finishedAt, four ABANDONED seats, zero CONNECTED seats. Disconnect timestamps span 19:07:57–19:07:58 UTC; reconnect deadlines span 19:10:57–19:10:58. Runtime last changed at 19:11:22.362449 UTC, before S7-04B deployment.

Persisted abandonmentLifecycleVersion is absent. `OnlineRepository.decode` maps absence to 0 (legacy), recognizes 1 as current and other values as invalid. `OnlineMatchService` writes version 1 on both new creation paths. The target is therefore legitimately subject to the closed legacy gate; it is not a current match misclassified by that gate.

The nonterminal/nonempty/all-ABANDONED cancellation predicate is satisfied. `OnlineEngine.Builder.cancelIfAllAbandoned` requires gate authorization for version 0, and returns BLOCKED with a closed gate. Version 1 bypasses that gate; the S7-04B real smoke validated this behavior.

## Worker and connection paths

`OnlineTurnWorker.processDue` loads state, checks each participant through `service.connection`, calls `service.abandon`, observes social presence, and refreshes discovery for nonterminal state. Already-ABANDONED seats return immediately in `OnlineEngine.connection`; there is no new connection transition to commit. The standalone abandon path reaches the legacy gate and returns no write when blocked.

The exact durable work document changed between two samples: updatedAt 23:06:50.902661 → 23:07:22.582348 UTC; dueAt/presenceCheckAt 23:07:14.327267 → 23:07:46.030781 UTC. This proves ongoing scheduling refresh rather than a never-scheduled or abandoned work item. Attempts and lastResult are not persisted by this work schema and are NOT_MEASURED. Per-match method invocation is inferred from the deployed code and changing work/index/presence observations; no per-match invocation trace exists.

## Redis and bounded logs

Root observer verified gate enabled=false, count=0, matchAllowed=false. Two exact-participant Redis samples span 40.015 seconds. All four activity hashes remain; TTL falls only from approximately 117.6 s to 109.5 s, proving renewal within that interval. All four player connection keys are absent. The match's shared due-index score advances from 1790464355646 to 1790464387524. No keys were deleted.

Bounded current-container log read: last six hours, at most 20,000 lines; 2,673 returned. Counts: BLOCKED=2,489, ALLOWED=44, COMPLETED=22, ONLINE_WORK_UNAVAILABLE=0, exact match-ID mentions=0. Audit messages omit match IDs. These are aggregate counts, **not proof that each blocked message belongs to this match**. Current-container logs cannot reconstruct the old container's creation/disconnect decisions. No transaction failure was observed in this bounded window; absence is not historical proof.

## Events, History and Replay

15 partial events: MATCH_STARTED=1, ROUND_STARTED=1, HAND_DEALT=4, TURN_STARTED=1, PLAYER_DISCONNECTED=4, PLAYER_ABANDONED=4. No MATCH_FINISHED/cancellation event. Normal History count for all four participants=0. Authenticated GET using one existing LOAD participant confirms IN_PROGRESS, absence from History and Replay HTTP 409. No gameplay/queue requests or data writes were made.

## Proposed minimum correction — not implemented

1. Preserve this evidence and explicitly authorize this single legacy ID and four expected LOAD participants as a separate migration scope. Do not reopen the historical exact-22 allowlist or change the lifecycle marker to disguise it as new.
2. The current file gate hardcodes count=22 plus the original pinned registry hash; a singleton configuration is intentionally rejected. If reusing this mechanism, make the expected count explicitly configurable with default 22 and strict positive bound, retaining exact pinned run/hash/IDs and fail-closed validation. Review/test singleton and unchanged 22-ID behavior before any deployment. A separately reviewed singleton domain tool with an exact injected gate is an alternative; neither is implemented here.
3. Only after separate approval, reconcile the singleton through the normal domain transaction, close its gate, preserve 15 original events, verify one cancellation event/zero History/no full Replay and observe work/Redis convergence. Do not delete Redis or write a terminal state directly.
4. Correct R2 ownership discovery to reconcile the complete 100-identity set against authoritative pending work after shutdown, including assignments not seen in client logs. Keep observed client assignments distinct from authoritative run attribution. This prevents another residual match from escaping a client-only registry.

R2 remains blocked. No source fix, operational configuration change, reconciliation, restart, commit or push was performed. Diagnostic artifacts were added locally and a read-only observer was copied to the server.

## Evidence

`Generated/S705/authoritative.json`, `participant-slots.json`, `server-read.jsonl`, `api-result.json`; historical `Generated/SERVER7B/run.json`, `result.json`, `timeline.jsonl`, `group-05.log`, final registry; S7-04B exact-ID snapshots and smoke result. Historical evidence was not overwritten.
