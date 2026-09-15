# DOMINO ONLINE — PHASE I3 REPORT

## Publication split

The I3 checkpoint on main includes the validated normal online entry, catalog v3 support and presentation baseline described by the I2.1/I2.2 reports. Development anonymous identity reset, its ApplicationServices lifecycle changes and the two-client harness requiring that reset are published separately on `codex/development-authentication`. `OnlineDevelopmentAccess` retains the same Editor/Development plus LOCAL/DEVELOPMENT/TEST gate for manual Create/Join tools without depending on the reset feature. AdsSettings.asset and ApiSettings.asset are excluded from both commits and retain their recorded hashes. Generated builds, caches and ScriptableBuildPipeline.json are not part of the checkpoint.

The real Firebase validation below was run with the separately published development harness. After splitting, main is also compiled and UI-tested without that authentication tool. Historical COMMIT=NONE entries below describe the validation run, not the later publication result reported in the checkpoint response.

## Source and scope

```text
BRANCH=main
SOURCE_SHA_BEFORE=0e6b3691ac2174d3ce7c00fe7dce9203cfc6d0d2
SOURCE_SHA_AFTER=0e6b3691ac2174d3ce7c00fe7dce9203cfc6d0d2
WORKTREE_CLEAN_BEFORE=NO
PREEXISTING_I21_DEV_AUTH_I22_CHANGES=PRESERVED
ADS_SETTINGS_PRESERVED=YES
API_SETTINGS_PRESERVED=YES
STAGE=NONE
COMMIT=NONE
PUSH=NONE
DEPLOY=NONE
```

I3 was applied on top of the uncommitted I2.1, development identity, build and I2.2 presentation work. The Git diff from HEAD includes those earlier changes; it must not be treated as an I3-only patch. No settings were reverted or staged.

Settings SHA-256 before/after I3:

- `Assets/_Domino/Resources/ApiSettings.asset`: `AC6B0ED6D30240BC531B235E43E8B9B55D3B48DCBCCCF365ADEC62D3EE6BCB37`.
- `Assets/_Domino/Resources/AdsSettings.asset`: `C377416E334727264806761518A4B5EDF381A837A927A1C3F3D523A1ACBDB5D6`.

## Matchmaking

```text
MATCHMAKING_IMPLEMENTED=YES
MATCHMAKING_STORE=REDIS
MATCHMAKING_KEY=DUEL_1V1:double-nine-duel:1:ONLINE
QUEUE_POLICY=FIFO
JOIN_QUEUE=PASS
LEAVE_QUEUE=PASS
DUPLICATE_JOIN=PASS
SAME_UID_SELF_MATCH=NO
COMPATIBILITY_VALIDATION=PASS
PRESENCE_REQUIRED=YES
DISCONNECTED_QUEUE_CLEANUP=PASS
ATOMIC_PAIRING=PASS
MULTI_WORKER_REDIS=PASS
INDEPENDENT_OS_RESERVATION_WORKERS=PASS
RESERVATION_LEASE_TTL_SECONDS=30
RESERVATION_FAILURE=PASS
CANCEL_PAIR_RACE=PASS
SEATS_ASSIGNED_BY_SERVER=YES
MATCH_FOUND=PASS
MATCH_FOUND_ALL_UID_CONNECTIONS=PASS
ACTIVE_ASSIGNMENT_RECOVERY=PASS
NORMAL_FLOW_REQUIRES_MANUAL_MATCH_ID=NO
MANUAL_ENTRY=EDITOR_AND_DEVELOPMENT_ONLY
WEBSOCKET_MULTI_INSTANCE_DISTRIBUTION=NOT_VALIDATED
```

Authenticated APIs (UID comes only from the verified Firebase principal):

- `POST /api/v1/matchmaking/queue`, body exactly `{ "modeKey": "DUEL_1V1" }`.
- `DELETE /api/v1/matchmaking/queue`.
- `GET /api/v1/matchmaking/queue` returns queue state or the committed assignment.
- `GET /api/v1/matches/active` recovers a committed assignment without Redis.

Typed states: `NOT_QUEUED`, `QUEUED`, `RESERVED`, `MATCHED`, `FAILED`. A reserved response is not cancellation success. Cancellation also checks Firestore so a missing Redis key after restart cannot hide an already committed assignment. Extra UID, opponent, seat, rule and reservation fields are rejected.

Real unauthenticated POST queue, DELETE queue and GET active probes each returned HTTP 401 on the final local backend.

The normal DUEL button now opens the localized search view, shows elapsed time, permits cancellation, and automatically attaches to the existing online controller after `MATCH_FOUND`. There is no client polling: one recovery request runs on opening/reconnection. Aggregate activity updates do not trigger queue requests. If the queue was lost, recovery returns idle and the player explicitly searches again.

Manual Create/Join remains under **DEV • Authentication → Manual DUEL Create / Join**, plus the existing Editor development controls. `OnlineEntryView` is excluded from Release compilation, and development controls require a development context and LOCAL/DEVELOPMENT/TEST configuration. The local shared-device DUEL Editor command remains available.

## Atomicity and storage

`RedisMatchmakingStore` runs the production Lua script for membership, FIFO, presence checks, reservation and cancellation. No JVM queue lock is used. Keys use `domino:v1:{presence}:mm:` and the existing presence hash tag. Entries use SHA-256 UID keys; raw Firebase tokens, hands and wallet information are never stored in Redis.

FIFO ordering uses Redis `INCR` plus a sorted set. The first two compatible, present, distinct UIDs are reserved and removed atomically. Queue entries have a 120-second lease refreshed by the server while G3 presence remains live; cleanup runs every second. Join/cancel protection allows 20 operations per UID per 10 seconds. This is a small operational limiter, not anti-DDoS infrastructure.

The reservation ID is also the future Match ID. A 30-second owner lease can expire, but the recovery job and reserved membership retain that same ID. A replacement worker retries the same creation rather than pairing either user with a third player. Only the current lease owner finalizes Redis and records the completed pairing.

`OnlineMatchService.createPaired` randomizes seats and invokes the existing `OnlineEngine.join`; starter selection, private hand generation, I2 timers, rules and scoring remain in the existing engine. Two `REMOTE_HUMAN` participants are created, with no bot participant.

`FirestoreOnlineRepository.createPaired` atomically persists the existing M4 Match/runtime/participants/events/rounds/work record, the assignment indexes and a creation receipt. `onlinePlayerAssignments/{uid}` is a durable Match lookup, not a queue. `onlineMatchCreationReceipts/{reservationId}` records COMMITTED/ABORTED creation idempotency, not presence.

If creation times out, `settleFailedCreation` first finds the committed Match or atomically writes an ABORTED receipt that fences a late transaction. Only a conclusive failure releases the reservation and sends failure to both users. If Firestore is wholly unavailable, recovery metadata stays pending until it can make that decision safely; the 30 seconds is an owner lease, not a guarantee that all recovery metadata disappears after 30 seconds.

`RealtimeHandler` sends `MATCH_FOUND`/`MATCHMAKING_STATUS` to every locally attached authenticated connection of the affected UID. The payload contains the assignment and public opponent display label, never their UID or hand. Existing public participant labels are reused; no profile/identity or wallet mutation was added to matchmaking.

Metrics use the existing Micrometer registry: queue size, join/cancel requests, completed matches, reservation failures and wait duration. Counters are process operational metrics, not game authority. Global activity now includes actual queued players, including changes when the online count stays constant. No new public metrics endpoint was exposed.

Read-only local diagnostics (counts only):

```powershell
docker exec domino-redis redis-cli ZCARD 'domino:v1:{presence}:mm:q:DUEL_1V1:double-nine-duel:1:ONLINE'
docker exec domino-redis redis-cli ZCARD 'domino:v1:{presence}:mm:jobs'
```

## Validation

```text
BACKEND_TESTS=447 PASS
BACKEND_SKIPPED=1 (optional RealFirestorePolicyTests, unrelated monetization validation)
BACKEND_FAILURES=0
BACKEND_ERRORS=0
I3_CLIENT_TESTS=47 PASS
I3_SEARCH_UI=270 PASS (9 sizes, en/es)
ONLINE_PORTRAIT_I22_REGRESSION=360 PASS (9 sizes, both seats)
I1_I2_CLIENT_REGRESSION=43 PASS
LOCAL_CATALOG_GAMEPLAY=251685 PASS (250 differential rounds, 100 golden matches)
LOCAL_DUEL=14723 PASS (200 rounds)
PLAYER_FOUNDATION_REALTIME_CLIENT=469 PASS
GUEST_AUTH=43 PASS
H2=46 PASS
H3=30 PASS
H4=35 PASS
H5=27 PASS
H6=26 PASS
UNITY_COMPILATION=PASS
UNITY_PLAY_MODE=PASS
WINDOWS_DEVELOPMENT_BUILD=PASS
CONSOLE_ERRORS=0
REAL_AD_REQUESTS=0
REWARDED_AD_REQUESTS_DURING_I3=0
WALLET_COINS_MONETIZATION_CHANGES=NONE
```

The Redis tests use unique namespaces and synthetic test UIDs and never flush the database. They exercise FIFO, incompatible keys, same UID/multiple devices, presence loss, 100 queued users/50 distinct reservations, concurrent Redis connections, 25 cancellation races, expired-owner recovery, forced creation failure, assignment recovery, rate protection and two independent Docker/redis-cli processes executing the production reservation Lua. Firestore failure unit tests use controlled repository doubles; this is not a distributed Firestore chaos benchmark.

The real Unity validation used the normal application services, Firebase authentication, normal menu button, search/cancel view, shared starter presentation and gameplay controls. Files synchronize only test timing, never supply opponents, seats, rules or UIDs. The test uses the existing development identity reset where necessary and normal Player bootstrap/name update to distinguish the clients; no backend verifier bypass or remote user deletion is used.

Final real run evidence: `Generated/I3Final/`:

- Editor A UID fingerprint: `9F6CFAC3147DFF12`.
- Windows B UID fingerprint: `8E77B2B145CE14DF`.
- Same persisted Match: `1e58c098-be20-4a9d-ab1a-7212af0e2608`.
- Seats: A=0, B=1, chosen by the server.
- Both received one `MATCH_FOUND`; A=30 checks, B=37 checks, both PASS with zero Console errors.
- Solo search/cancel, cancelled opponent exclusion, disconnected opponent exclusion, same-UID reconnect, automatic pairing, assignment recovery, 10 own tiles/hidden opponent tiles, starter selection and authoritative gameplay all passed.
- Screenshot: `Generated/I3Final/B-playing.png`.

Real Redis restart evidence: `Generated/I3Restart/R-result.txt`. Redis was stopped and started while an existing real Match was loaded. REST health stayed UP, and the same persisted Match `388f7b08-aeb6-4973-9146-91d396e147fa`, immutable rule snapshot and private projection were recovered; 10 checks PASS, Console errors=0. No Firestore Match was recreated during this test.

The initial real run discovered that `UnityApiTransport` only accepted GET/POST/PUT. I3 adds bodyless DELETE support; the complete cancel/pair/gameplay flow was rerun successfully after that fix. No TLS or authentication policy was relaxed.

## Files and review boundaries

New server package: `server/domino/src/main/kotlin/com/teamfho/domino/matchmaking/` (`MatchmakingModels`, `RedisMatchmakingStore`, `MatchmakingService`, `MatchmakingConfiguration`, `MatchmakingMetrics`). Tests: `server/domino/src/test/kotlin/com/teamfho/domino/matchmaking/`.

Server integration: `online/OnlineRepository.kt`, `online/OnlineMatchService.kt`, `online/OnlineConfiguration.kt`, `realtime/RealtimeHandler.kt`. No `OnlineEngine` rule changes.

New Unity runtime: `Scripts/Online/MatchmakingClient.cs`, `MatchmakingView.cs`. Integrations: `DominoClientController`, `OnlineMatchClient`, `RealtimeConnectionService`, `UnityApiTransport`, development entry/authentication controls and EN/ES tables. Validation: `MatchmakingClientTests.cs`, `RunMatchmakingTests.ps1`, `MatchmakingViewValidation`, `MatchmakingNetworkValidation` and its Editor launcher, with Unity metadata.

Old manual-entry validation harnesses now explicitly invoke the development entry; they no longer assume the normal DUEL button opens manual Create/Join. Historical I2.1/I2.2 reports remain as prior-phase evidence.

## Limits and next review

- Full cross-instance WebSocket delivery remains NOT_VALIDATED, as allowed by I3. Redis pairing does not depend on a JVM lock; cross-instance connected clients may need the authenticated recovery route until a future WS distribution layer exists.
- Redis queues are ephemeral. After loss, an unassigned player explicitly searches again; persisted Matches remain in Firestore. The combined failure case of Redis losing reservations during an unresolved Firestore commit was not chaos-tested. The restart test covers an already persisted Match.
- Indeterminate Firestore outages retain reservation recovery metadata rather than claiming a cancellation or creating a second Match. No liveness guarantee is claimed while Firestore is unavailable.
- No performance benchmark, MMR/ranking, spectator runtime, replay UI, H7, production ads or deploy was added.
- Windows build: `builds/duel-client-b/DominoGame.exe`. Automated clients exited after testing; the local backend and Redis were left available.

Review I3 and the preserved pending earlier work before authorizing any commit or push.

## Addendum 73–100: final acceptance record

The two supplied continuation documents are identical. Their UX requirements are implemented: normal DUEL opens a recovery check, then an explicit Search opponent button (not automatic queue enrollment). A found assignment disables Cancel, displays the localized found message for 650 ms, transitions to ENTERING_MATCH, and loads the existing authoritative snapshot. Existing assignments display a resuming message instead of starting another search. Repeated identical assignments do not repeat entry; conflicting assignments preserve the original assignment and emit a safe diagnostic. Failed entry can retry recovery of the same Match.

```text
BRANCH=main
SOURCE_SHA_BEFORE=0e6b3691ac2174d3ce7c00fe7dce9203cfc6d0d2
SOURCE_SHA_AFTER=0e6b3691ac2174d3ce7c00fe7dce9203cfc6d0d2
MATCHMAKING_IMPLEMENTED=YES
MATCHMAKING_STORE=REDIS
MATCHMAKING_KEY=DUEL_1V1:double-nine-duel:1:ONLINE
QUEUE_POLICY=FIFO
JOIN_QUEUE=PASS
LEAVE_QUEUE=PASS
DUPLICATE_JOIN=PASS
SAME_UID_SELF_MATCH=NO
ATOMIC_PAIRING=PASS
MULTI_WORKER_SAFE=YES
RESERVATION_IMPLEMENTED=YES
RESERVATION_TTL_SECONDS=30 (owner lease; pending recovery metadata retained)
RESERVATION_FAILURE_RECOVERY=PASS
SEAT_ASSIGNMENT_SERVER_AUTHORITY=YES

AUTHORITATIVE_MATCH_CREATED=PASS
MATCH_COUNT_PER_PAIR=1
MODE_KEY=DUEL_1V1
RULESET_ID=double-nine-duel
RULESET_VERSION=1
REMOTE_HUMANS=2
BOTS=0
FIRESTORE_MATCH_AUTHORITY=YES

MATCH_FOUND_IMPLEMENTED=YES
MATCH_FOUND_WEBSOCKET=PASS
PLAYER_A_MATCH_ID=bc908ff7-8a57-4cde-b50f-c7d878b84c2a
PLAYER_B_MATCH_ID=bc908ff7-8a57-4cde-b50f-c7d878b84c2a
SAME_MATCH_ID=PASS
DIFFERENT_SEATS=PASS
OPPONENT_PRIVATE_DATA_LEAK=NO
MISSED_MATCH_FOUND_RECOVERY=PASS
ACTIVE_MATCH_RECOVERY=PASS

NORMAL_DUEL_MATCHMAKING_ENTRY=PASS
SEARCH_UI=PASS
SEARCHING_STATE=PASS
SEARCH_ELAPSED_TIME=PASS
CANCEL_SEARCH=PASS
MATCH_FOUND_AUTO_TRANSITION=PASS
MANUAL_MATCH_ID_REQUIRED_FOR_NORMAL_FLOW=NO
CREATE_JOIN_MATCH_ID_DEV_TOOL_PRESERVED=YES

REAL_TWO_UNITY_CLIENTS=PASS
CLIENT_A_TYPE=UNITY_EDITOR
CLIENT_B_TYPE=WINDOWS_DEVELOPMENT_STANDALONE
UID_A_DIFFERENT_UID_B=PASS
PLAYER_A_JOIN_QUEUE=PASS
PLAYER_B_JOIN_QUEUE=PASS
AUTOMATIC_PAIRING=PASS
REAL_MATCH_FOUND_A=PASS
REAL_MATCH_FOUND_B=PASS
REAL_GAME_START=PASS
STARTER_SELECTION=PASS
REAL_WEBSOCKET=PASS

REAL_CANCEL_SEARCH=PASS
CANCELLED_PLAYER_REMOVED=PASS
CANCELLED_PLAYER_NOT_MATCHED=PASS
DISCONNECTED_PLAYER_REMOVED=PASS
STALE_QUEUE_CLEANUP=PASS
CANCEL_VS_MATCH_RACE=PASS
THREE_PLAYER_CONCURRENT_JOIN=PASS (real Redis, test presence identities)
ONE_PLAYER_PAIRED_ONCE=PASS
REMAINING_PLAYER_STAYS_SEARCHING=PASS
MULTI_WORKER_PAIRING=PASS
DUPLICATE_MATCH_CREATED=NO
REAL_THREE_AUTHENTICATED_CLIENTS=NOT_RUN

REDIS_QUEUE_IMPLEMENTED=YES
REDIS_PERSISTENT_MATCH_AUTHORITY=NO
QUEUE_STALE_TTL_OR_PRESENCE_CLEANUP=PASS
REDIS_RESTART_QUEUE_BEHAVIOR=EPHEMERAL; EXPLICIT_REJOIN_IF_UNASSIGNED
EXISTING_MATCH_CORRUPTED_BY_REDIS_RESTART=NO

UID_FROM_FIREBASE_PRINCIPAL=YES
CLIENT_CAN_SELECT_OPPONENT=NO
CLIENT_CAN_SELECT_SEAT=NO
CLIENT_CAN_CANCEL_OTHER_USER=NO
SAME_UID_SELF_PAIR=NO
MATCH_FOUND_CROSS_USER_LEAK=NO

I1_REGRESSION=PASS
I2_REGRESSION=PASS
I2_1_REGRESSION=PASS
I2_2_REGRESSION=PASS
M4_REGRESSION=PASS
REALTIME_G3_REGRESSION=PASS
DUEL_LOCAL_REGRESSION=PASS
DUEL_ONLINE_REGRESSION=PASS
PARTNERS_2V2_REGRESSION=PASS
MONETIZATION_REGRESSION=PASS
PORTRAIT_REGRESSION=PASS
UNITY_COMPILATION=PASS
UNITY_PLAY_MODE=PASS
CONSOLE_ERRORS=0

WEBSOCKET_MULTI_INSTANCE_DISTRIBUTION=NOT_VALIDATED
MMR_MATCHMAKING=NO
RANKED_MATCHMAKING=NO
GEOGRAPHIC_MATCHMAKING=NO
SPECTATOR_RUNTIME=NO
REPLAY_UI=NO
H7_STARTED=NO
ADS_SETTINGS_USER_CHANGE_PRESERVED=YES
API_SETTINGS_USER_CHANGE_PRESERVED=YES
COMMIT=NONE
PUSH=NONE
CLOUD_RUN_DEPLOYED=NO
```

### Additional requirement evidence

- 74–80, 87, 93–95: `MatchmakingView.Initialize/Recover/Search/Render/Enter/Cancel` and `MatchmakingClient.Apply/Receive/CancelAsync`. The UI has one state at a time and no queue polling. Initial recovery does not POST; retry and cancellation use authenticated API replies. A reserved response is not cancellation success. Unit tests cover late replies, duplicate cancel, duplicate/conflicting assignment and retry after failed entry. Search texts and safe areas pass nine sizes in EN/ES.
- 81: only public participant labels and assignment metadata are sent. No opponent UID, token or private hand is displayed.
- 82–85: elapsed seconds are presentation only. Redis INCR supplies FIFO ordering; the server resolves and freezes the compatibility key on join. Catalog changes never move existing entries into an incompatible key. Old-key entries remain cancellable and subject to presence/TTL cleanup; they are not silently migrated. Pairing workers reserve only the currently supported key. No client timestamp or arbitrary RuleSet is accepted.
- 86: `MatchmakingService.join` returns the existing authenticated assignment with `ACTIVE_MATCH_EXISTS` and performs no Redis join. Unity resumes that assignment. This is an idempotent successful recovery response rather than an HTTP error that would hide the resumable Match.
- 88–90, 96: cleanup affects Redis queue membership only. It never deletes Firestore Match, Round or events. Presence is per UID across all sockets, so closing one of two connections correctly keeps the player eligible. Mobile background cancels the transport; on foreground the client recovers state and requires an explicit search if unassigned. Once assigned, existing I2 Match reconnect semantics apply. Mobile OS suspension timing has not been device-tested.
- 91–92: production Lua tests inspect membership, reservation and finalization in unique Redis namespaces. Real clients load the same Firestore-backed private Match projections. Repository tests assert one creation receipt, one Match and two distinct seats. Redis restarts were tested against an existing real Match as documented above.
- 97: `MatchmakingService` emits MATCHMAKING_JOIN, MATCHMAKING_CANCEL, MATCHMAKING_PAIR, MATCHMAKING_RESERVATION, MATCHMAKING_MATCH_CREATED, MATCHMAKING_MATCH_FOUND, MATCHMAKING_CLEANUP and MATCHMAKING_FAILURE. Reservation/Match IDs and compatibility keys are safe diagnostics; tokens and hands are never logged.
- 98: three concurrent joins and simultaneous workers pass against real Redis, with exactly one pair and one remaining queued identity. This is not claimed as three real Firebase-authenticated application clients; that recommended additional test remains NOT_RUN.
- 99–100: see final real-client run below. The validation driver operates normal menu/search/cancel controls; coordination files contain timing signals only. No manual Match ID or seat/opponent selection is used.

Final addendum run: `Generated/I3AddendumFinal/`. Editor A: 42 checks PASS, UID fingerprint DF50AC1CCCA014CB, seat 0. Windows B: 40 checks PASS, UID fingerprint ED385637EDAC1B63, seat 1. Each received exactly one MATCH_FOUND; both have Console errors=0. Match bc908ff7-8a57-4cde-b50f-c7d878b84c2a. The real cancellation race resolved CANCELLED; A then explicitly searched again and paired with waiting B. Search/cancel, disconnect exclusion, starter, private hands and authoritative gameplay passed. Screenshot: `Generated/I3AddendumFinal/B-playing.png`. The isolated validation harness resets both opted-in test identities through the existing development tool to avoid another open Editor retaining the same UID; no remote Firebase users were deleted. An earlier attempt did not isolate A and failed the last-connection test; it is retained in `Generated/I3Addendum/` and is not counted as PASS. The final build includes the corrected test isolation and cleanup on validation exit.
