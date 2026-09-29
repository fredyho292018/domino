# S7-04B — deployment, exact reconciliation and one-match smoke

Checkpoint pushed to origin/main: `639fd73399106cd4fc542d09f1e113f6b1fc185c`.
Image: `cuban-domino-api:639fd73399106cd4fc542d09f1e113f6b1fc185c`, image digest `sha256:6da94f39e1fc0b62ce5ee4c7b843bd5d276d9c5678415d6918b97b206d794a45`.
Built from a clean archive of that commit. Existing unrelated Unity/Firebase changes were preserved.

## Exact migration

API-only deployment with legacy gate closed, followed by a comparison of 49 exact IDs: 22 migration matches, 22 other due-index matches and five SERVER-6 matches. Root and authoritative-runtime hashes remained identical after deployment. Redis container identity, restart count and health were unchanged.

Allowlist count 22; canonical SHA-256 `4f186eea8f8a5c30bb4905ee8b84978c682e01f8d3c0f1bd02b82f44ac9eddc4`. Installed and checked while closed, then explicitly opened through the approved root operator. The normal domain worker reconciled the exact 22:

- 22 CANCELLED, zero nonterminal; result reason CANCELLED.
- All 1,903 original event IDs and content hashes preserved.
- Exactly 22 additional cancellation events; total 1,925 events.
- Zero normal History records; zero durable pending-work documents.
- No root/runtime changes in the 27 controls. SERVER-6 event and History hashes also preserved.
- Operator confirmed legacy gate closed after reconciliation.

Redis observation spanned 152.495 seconds (120-second TTL plus grace). No old-run due members and no activity keys for the exact 88 migrated participants at either endpoint. Four renewing LOAD activity keys belong to other participants; they were not deleted or modified. Absence of active old-run workers is supported indirectly by terminal authoritative state, absent durable work/due entries and no participant activity renewal; no per-match in-flight JVM worker gauge was captured.

Authenticated SERVER-6 reads passed: five History entries, five snapshots and the same three available Replay manifests as before deployment.

## One new match

Match `dce4cd74-fa32-4b4d-8a33-7eab10526779`, four existing LOAD identities, one matchmaking cycle, requeue disabled. Five command ACKs were captured, then all four sockets closed. Initial disconnected snapshot remained IN_PROGRESS with lifecycleVersion=1 and no connected participants. After the normal 180-second grace, authoritative state was CANCELLED, all four seats ABANDONED, one cancellation event and no pending work. No legacy allowlist entry was installed for this match.

Authenticated API verification: CANCELLED snapshot, absent from normal History, Replay endpoint HTTP 409. Other 49 match root/runtime states remained unchanged relative to the post-migration baseline.

## Observability smoke

Nine server samples overlap the active match. JVM available processors=4; cgroup quota=4 CPU. Peak sampled API use 0.982 cores / 24.54% of quota; host CPU peak 20.67%. Heap-used peak 373,473,664 bytes; one GC / 22 ms during sampled match interval. No container restarts or OOM observed.

Five command ACK samples: p50 561 ms, p95/max 1,340 ms. Four matchmaking samples: p50 2,297 ms, p95/max 2,927 ms. Twenty WS event-dispatch samples measured 0 ms at millisecond resolution. This measures client dispatch work, not network WebSocket round-trip latency. These few samples validate instrumentation, not capacity or a latency SLO.

Client correctness counters: zero command failures, duplicate command applications, idempotency violations, invalid turn acceptances, sequence gaps/regressions or unauthorized deliveries. Four authenticated sockets during play, zero after stop; one match started.

Initial JVM observer attempt failed because the packaged agent predated configurable duration. No match was started under that failed observer. Rebuilt the agent from checkpoint source with a distinct class name; second attempt captured live JVM/host/container metrics successfully. The bounded counter observer stops automatically after approximately 20 minutes.

## Evidence and remaining closure

Local evidence directory: `Generated/S704B/`. Main files: `firestore-before.json`, `firestore-deployed-closed.json`, `firestore-reconciled.json`, `firestore-convergence.json`, `redis-convergence.jsonl`, `reconciliation-result.json`, `server6-after.json`, `one-match-result.json`, `smoke-disconnected.json`, `smoke-final.json`, `smoke-api-result.json`, `smoke-observability.json`.

Final root check passed: `FINAL_SERVER=PASS GATE=CLOSED REDIS_UNCHANGED=YES LEGACY_DUE=0`. Downloaded `server-final.json` confirms the expected healthy API image, unchanged Redis container and no due-index entries for either the 22 migrated matches or the new smoke match. `LEGACY_RECONCILIATION_ENABLED_AFTER_MIGRATION=NO`.

S7-04B execution is complete with the measurement limits stated above (worker activity inferred from converged state; WS measurement is client dispatch latency). SERVER-7B-R2 and 100/250-user load were not started. Execution stopped after the single authorized new match; no further matchmaking is scheduled. The bounded diagnostic observer expires automatically as described above.
