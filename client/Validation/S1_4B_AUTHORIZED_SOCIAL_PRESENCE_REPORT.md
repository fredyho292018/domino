# CUBAN DOMINO CLUB â€” S1.4B AUTHORIZED SOCIAL PRESENCE REPORT

## Result and scope

Implemented authorized ONLINE / OFFLINE / IN_MATCH / UNKNOWN presence, using the existing authenticated WebSocket, Redis connection leases, A.3 authorization handles and A4-01 transmission capabilities. UNKNOWN has its own localized presentation. No last-seen, presence history, match details, Party or Spectator functionality was added.

Branch: `main`. Before/after source commit: `cd7744d07f8e8de6768e919968d3d72a806914b3`. Changes remain uncommitted.

Functional validation passed with the limitations below. This is a review candidate, **not a production release**. The existing durable invalidation retention blocker remains unresolved.

## Implementation map

Paths below are relative to the repository root.

| Responsibility | Source |
|---|---|
| Typed state and privacy projection | `server/domino/src/main/kotlin/com/teamfho/domino/social/SocialPresenceModel.kt` |
| Durable permission reader | `server/domino/src/main/kotlin/com/teamfho/domino/social/SocialPresenceAuthorization.kt` |
| Desired set, stream fences, snapshots/deltas | `server/domino/src/main/kotlin/com/teamfho/domino/social/SocialPresenceSubscriptions.kt` |
| Bounded preparation, target fanout, Redis reconciliation | `server/domino/src/main/kotlin/com/teamfho/domino/social/SocialPresenceRuntime.kt` |
| Ephemeral cross-instance signals | `server/domino/src/main/kotlin/com/teamfho/domino/social/SocialPresencePubSub.kt` |
| Existing leases and new activity projection | `server/domino/src/main/kotlin/com/teamfho/domino/realtime/PresenceStore.kt` |
| Existing socket authentication / heartbeat / subscription dispatch | `server/domino/src/main/kotlin/com/teamfho/domino/realtime/RealtimeHandler.kt` |
| Reused authorization generation and final capability | `server/domino/src/main/kotlin/com/teamfho/domino/social/LocalSocialAuthorizationIndex.kt` |
| Server match lifecycle observations | `OnlineMatchService.kt`, `OnlineTurnWorker.kt`, `RealtimeHandler.kt` |
| Client state and generation guards | `client/DominoGame/Assets/_Domino/Scripts/Social/SocialPresenceStore.cs` |
| Existing realtime channel integration | `RealtimeConnectionService.cs`, `SocialClient.cs`, `DominoClientController.cs` |
| Friends/profile/follow lists, visibility settings | `SocialView.cs` |
| EN/ES source and imported Unity tables | `Translations.json`, `Domino UI Shared Data.asset`, `Domino UI_en.asset`, `Domino UI_es.asset` |

## Authorities and privacy

Firebase continues to authenticate the socket. The client supplies public player IDs, never an authoritative UID or presence value. Spring resolves IDs and reads permissions from Firestore. Redis stores ephemeral leases/activity only. Unity presents authorized projections.

The permission transaction reads the pair revision, target privacy revision, both block directions, friendship, both active player records and both test-account markers. FOLLOW is deliberately absent from the permission predicate. A general presence denial also denies activity. An authorized viewer without activity permission sees IN_MATCH projected to ONLINE. Block, missing/ineligible account, self-target and other denials produce no reason-bearing presence payload.

A.3 marks a handle invalid before reauthorization. A reliable CONTROL invalidation advances the stream revision and clears client state; queued snapshots/deltas still carry the existing immutable A4-01 capability. Successful reauthorization cannot resurrect a candidate bound to the previous generation. Authorization failure stays closed.

The existing durable feed reconciles while handles exist. A caught-up feed now renews unchanged local authorization leases without periodically rereading the social graph. Invalidated, expired-after-a-recovery-gap or failed handles still require a durable read. This distinction is covered by the no-polling regression; it does not remove A.3 durable missed-event recovery.

## Redis model and lifecycle

Existing keys are retained:

- `domino:v1:{presence}:players`: unique-player lease expiry index.
- `domino:v1:{presence}:player:<sha256(uid)>`: per-connection expiry scores.
- `domino:v1:{presence}:connection:<server-generated connection ID>`: TTL lease.
- Added `domino:v1:{presence}:activity:<sha256(uid)>`: version and active flag, TTL 120 seconds.

Lease TTL is the existing configurable 60 seconds; existing heartbeat is 20 seconds and timeout 45 seconds. Redis TIME determines expiry. There is no second heartbeat. Removing one of multiple connections preserves ONLINE. Expiry of the final lease yields OFFLINE only when Redis is readable.

The activity version is a fixed-width encoding of the authoritative match updatedAt and lastSequence. Older observations cannot overwrite newer ones while the ephemeral version key exists. Observations come from committed writes, persisted paired-match creation and the existing turn worker's already-loaded state. No additional Firestore gameplay query was introduced. The turn worker continues to recover active matches; activity has a bounded TTL rather than durable presence history.

Channel: `domino:v1:social:presence-changed`, schema 1, internal UID hint only. Payload is never forwarded to Unity. Subscription delivery and preparation queues are bounded. Pub/Sub reconnect uses 1â€“30 second exponential backoff. A targeted Redis reconciliation every second covers missed hints and lease expiry; it is not a Firestore authorization poll and does not scan sockets or social graphs. Redis snapshots are shared per unique local target across subscriptions.

Redis read/write failures are isolated with five-second retry suppression after a failure. A failed batch projects UNKNOWN. Further batches in that pass avoid repeating the failing network call. Activity writes stop after the first failure and retry later. Realtime authentication remains Firebase-authenticated, PONG continues, and Redis presence/global-counter failure no longer closes gameplay sockets.

## Subscription and wire contract

Existing version-1 envelope and sequence rules remain. New message types:

```text
client -> server: SOCIAL_PRESENCE_SUBSCRIBE
payload: { generation, publicPlayerIds: [...] }

server -> client (CONTROL):
SOCIAL_PRESENCE_SUBSCRIBED { generation, count }
SOCIAL_PRESENCE_INVALIDATED { generation, revision }
SOCIAL_PRESENCE_ERROR { code, generation? }

server -> client (SOCIAL_EPHEMERAL):
SOCIAL_PRESENCE_SNAPSHOT { generation, revision, publicPlayerId, state }
SOCIAL_PRESENCE_UPDATED  { generation, revision, publicPlayerId, state }
```

A snapshot is a **per-target projection following a reliable stream clear**, not an unbounded array or all-or-nothing multipart payload. Denied/unresolved entries remain UNKNOWN after that clear. No chunk assembly is needed. Every state frame has its own revocable capability, remains below the existing 2 KB frame bound, and coalesces by public target ID. The existing 50-target / 16 KB social queue limits remain unchanged. No second writer exists.

The desired set is replaced atomically after validation: up to 50 distinct IDs, raw input bounded to 100, IDs exactly 22 base64url characters, positive safe-integer generation, monotonic request generations. Fifty-one distinct targets are rejected without altering the existing set. Empty unsubscribes. Nonempty replacement is limited to one per 250 ms; Unity debounces it for 300 ms.

After writer saturation drains, the existing recovery acknowledgement now creates a new clear/revision and a complete bounded resnapshot. This correction prevents unchanged targets from being omitted during recovery. The regression forces queue exhaustion and validates the renewed snapshot.

Unity rejects stale subscription generations and stale stream revisions. Reconnect keeps the desired IDs but advances the generation. Disconnect clears state. Social close/account invalidation clears data and releases listeners. Friends, following/followers and public profile use presence; list subscriptions follow the visible viewport with a small buffer. Following does not confer permission. Privacy controls independently cycle EVERYONE / FRIENDS / NO_ONE for presence and activity.

## Validation evidence

All infrastructure used loopback: backend A `18141`, backend B `18142`, Firestore Emulator `18085` / project `demo-domino-f0`, task-owned Redis `16379`. Fake authentication exists only in the pre-existing test-classpath server, which checks the explicit local environment. No production verifier bypass, production credentials or real Firebase users were used.

| Validation | Result |
|---|---|
| Final normal backend suite | **620 PASS / 23 SKIPPED / 0 FAILED**, 643 discovered |
| Transport subset of that suite | **35 PASS**; not an additional 35 tests |
| S1.4B foundation tests | **8 PASS**; includes explicit 1/20/50, atomic rejection of 51, generation barrier and pressure recovery |
| Full relevant Emulator run | **42 PASS / 1 SKIPPED / 0 FAILED** |
| Final S1.4B Emulator focused run | **6 PASS**: 3 repeated cases plus 3 added parameterized cost cases |
| Distinct Emulator tests validated across those runs | **45 PASS / 1 SKIPPED**; not 48 unique passes |
| Dedicated local Redis tests | **5 PASS**, including real expiry and batch sizes 1/20/50 |
| Social client harness | **89 PASS** |
| Unity Edit Mode localization harness | **138 checks PASS** |
| Unity Play Mode presence harness | **60 checks PASS**, EN/ES Ã— 1080Ã—1920, 1080Ã—2400, 1536Ã—2048 |
| Unity compilation | PASS (actual isolated Unity Editor import/Play Mode, plus static compilation earlier) |
| Unity validation Console errors | **0** |

The Unity figures are harness assertions, not NUnit test-case counts. Play Mode used an isolated presentation fixture, not real Firebase. Backend integration separately used actual WebSocket clients against two independent JVM processes. The Spanish IN_MATCH screenshot was visually reviewed; physical Android/iOS/manual user testing was not performed.

Local evidence is under `client/Validation/Generated/S14B/` (ignored, not intended for staging):

- `matrix.json`: 25 completed alternating Block/privacy iterations, 0 iteration failures. A later unsubscribe/resubscribe step originally hit the intended request cadence gate; the follow-up respects its 250 ms interval.
- `outage.json`, `outage-frames.json`: UNKNOWN while Redis is stopped, PONG on the same socket, recovery to ONLINE, final lease removal to OFFLINE; no UID in presence frames.
- `relations.json`: cross-instance FriendAccept, Unfriend, missed Presence Pub/Sub convergence, Pub/Sub recovery, missed authorization Pub/Sub recovered durably, Firestore failure clear and recovery.
- `restart.json`: target-backend restart and viewer-backend restart/resubscription.
- `match.json`: completed real-engine DUEL, 66 accepted commands, IN_MATCH then ONLINE, no match details in presence.
- `partners.json`: completed real-engine four-human PARTNERS match, 302 accepted commands in the final retained run, IN_MATCH then ONLINE, no match details in presence. The preceding 224-command run also passed. No Bot Swarm was started.
- `unity-presence-result.txt`, screenshots, `editmode-result.txt`, `backend-counts.json`, `cleanup-before.json`.

The initial raw-socket validation helper had a blocking close and discarded some state events while waiting for PONG. Those harness defects were corrected. Expected rate-limit responses were retained rather than weakening server limits. No production privacy leak was observed or asserted.

## Cost measurements and estimates

Measured using `SocialMeasurements` against Emulator, with fixtures created before counters were reset:

| Operation | Firestore document reads | Writes |
|---|---:|---:|
| One consistent authorization read | 9 | 0 |
| Resolve + authorize 1 target | 12 | 0 |
| Resolve + authorize 20 targets | 240 | 0 |
| Resolve + authorize 50 targets | 600 | 0 |

These are bounded, controlled reader costs, **not a guarantee for an entire live subscription during concurrent invalidation or recovery**. Recovery retries may add reads. Shared viewer/target data is still read inside each consistent permission transaction; there is no invented cross-target Firestore cache. This is a cost limitation to optimize only with equivalent privacy guarantees.

Redis batch tests measure one Redis script invocation for each 1/20/50-target lookup. Lua internally performs TIME and per-target lease pruning/count/activity operations; one network invocation does not mean one Redis internal operation. A normal steady heartbeat uses the existing lease renewal script and does not publish a state-change event unless effective connectivity changes. No extra Firestore heartbeat write, authorization read per heartbeat, gameplay social read, or network lookup at the final transmission barrier occurs.

Reconciliation worst case: `ceil(unique local subscribed targets / 50)` Redis script invocations per one-second pass, plus pending coalesced activity writes and existing heartbeats. Pub/Sub is a hint; reconciliation intentionally remains the missed-event/TTL safety path.

Memory planning estimate, not a heap benchmark: allow approximately 64 KiB per connection at the maximum 50 handles including indexes, IDs, desired/cached states and queue bookkeeping. This gives ~6.25 MiB / 62.5 MiB / 625 MiB for 100 / 1,000 / 10,000 fully subscribed connections, excluding JVM, socket buffers, shared infrastructure and object-layout variance. Actual use depends heavily on shared targets. Preparation pool: 2 threads, queue 128. Pub/Sub delivery: 1 thread, queue 128. Pending activity observations: at most 10,000 UID entries. No production capacity claim is made.

## Gates

Each PASS below maps to the implementation and validation above; these do not imply a production load or mobile-device certification.

```text
SECURITY_GATE_1=PASS
SECURITY_GATE_2=PASS
SECURITY_GATE_3=PASS
SECURITY_GATE_4=PASS
SECURITY_GATE_5=PASS
SECURITY_GATE_6=PASS
SECURITY_GATE_7=PASS
SECURITY_GATE_8=PASS
SECURITY_GATE_9=PASS
SECURITY_GATE_10=PASS
PRESENCE_GATE_1=PASS
PRESENCE_GATE_2=PASS
PRESENCE_GATE_3=PASS
PRESENCE_GATE_4=PASS
PRESENCE_GATE_5=PASS
PRESENCE_GATE_6=PASS
PRESENCE_GATE_7=PASS
PRESENCE_GATE_8=PASS
PRESENCE_GATE_9=PASS
PRESENCE_GATE_10=PASS
SUBSCRIPTION_GATE_1=PASS
SUBSCRIPTION_GATE_2=PASS
SUBSCRIPTION_GATE_3=PASS
SUBSCRIPTION_GATE_4=PASS
SUBSCRIPTION_GATE_5=PASS
SUBSCRIPTION_GATE_6=PASS
SUBSCRIPTION_GATE_7=PASS
SUBSCRIPTION_GATE_8=PASS
SUBSCRIPTION_GATE_9=PASS
SUBSCRIPTION_GATE_10=PASS
MULTI_GATE_1=PASS
MULTI_GATE_2=PASS
MULTI_GATE_3=PASS
MULTI_GATE_4=PASS
MULTI_GATE_5=PASS
MULTI_GATE_6=PASS
MULTI_GATE_7=PASS
MULTI_GATE_8=PASS
MULTI_GATE_9=PASS
MULTI_GATE_10=PASS
TRANSPORT_GATE_1=PASS
TRANSPORT_GATE_2=PASS
TRANSPORT_GATE_3=PASS
TRANSPORT_GATE_4=PASS
TRANSPORT_GATE_5=PASS
TRANSPORT_GATE_6=PASS
TRANSPORT_GATE_7=PASS
TRANSPORT_GATE_8=PASS
TRANSPORT_GATE_9=PASS
TRANSPORT_GATE_10=PASS
UNITY_GATE_1=PASS
UNITY_GATE_2=PASS
UNITY_GATE_3=PASS
UNITY_GATE_4=PASS
UNITY_GATE_5=PASS
UNITY_GATE_6=PASS
UNITY_GATE_7=PASS
UNITY_GATE_8=PASS
UNITY_GATE_9=PASS
UNITY_GATE_10=PASS
UNITY_GATE_11=PASS
UNITY_GATE_12=PASS
```

## Regression and limitations

Normal backend tests cover Auth, Profile, Entitlements, S1.1/S1.2/S1.3, Match/Replay, matchmaking, monetization, F0/F0.1, A.1 writer, A.2 rate isolation and A.3/A4-01 authorization composition. Their opt-in skipped tests are not claimed as executed. Existing A.4 synthetic slow-socket/queue/generation regressions passed in the transport suite; a new selected **presence** frame test verifies rejection after revoke/reauthorize, and a new pressure test verifies complete resnapshot.

The two-process exercises are controlled correctness checks, not a 100/1,000/10,000 connection load test or exhaustive network-chaos certification. Slow transport and saturation are deterministic writer fixtures; the entire earlier A.4 multi-process suite was not repeated. Unity Play Mode uses a presentation fixture; there was no physical-device or human end-to-end session. Memory figures are estimates. Activity versions are ephemeral and have bounded retention; they are not a durable cross-match activity history. Activity recovery relies on the existing authoritative turn discovery path. These limits must remain visible during review.

No rules, match/replay event sequence semantics, economic authority, purchase flow or direct Firestore client access were changed. The existing G3 behavior intentionally changed only in its Redis outage policy: authenticated gameplay sockets survive presence/counter dependency failure.

```text
SAFE_RETENTION_POLICY=DEFER_CLEANUP_UNTIL_PRODUCTION_HARDENING
RETENTION_CLEANUP_READY=NO
PRODUCTION_RELEASE_BLOCKED_BY_RETENTION_POLICY=YES
PRODUCTION_READY=NO
REAL_FIRESTORE_CALLS=0
REAL_REDIS_PRODUCTION_CALLS=0
BOT_SWARM_STARTED=NO
FIRESTORE_RULES_DEPLOYED=NO
FIRESTORE_INDEXES_DEPLOYED=NO
BACKEND_DEPLOYED=NO
UNITY_DEPLOYED=NO
GOOGLE_PLAY_UPLOAD=NO
S2_STARTED=NO
COMMIT=NONE
PUSH=NONE
DEPLOY=NONE
```

## Protected work and file audit

The initial 97 dirty/untracked user files were recorded with SHA-256 in the ignored `Generated/S14B/baseline.json`. All 97 still match. This includes AdsSettings, ApiSettings, Localization Settings, Google services files, Android/Google Play preparation and prior reports. No staging or restoration was performed.

The complete per-file classification and isolated source diff are in `Generated/S14B/file-classification.json` and `Generated/S14B/S1_4B.diff`. Generated test output, local logs, process argument files and screenshots remain ignored. They are not credentials and must not be staged as source. Exact source classification follows below.

## Exact file classification

### S1_4B_INTENTIONAL (30)

- `client/DominoGame/Assets/_Domino/Editor/Localization/Translations.json`
- `client/DominoGame/Assets/_Domino/Localization/Tables/Domino UI Shared Data.asset`
- `client/DominoGame/Assets/_Domino/Localization/Tables/Domino UI_en.asset`
- `client/DominoGame/Assets/_Domino/Localization/Tables/Domino UI_es.asset`
- `client/DominoGame/Assets/_Domino/Scripts/Client/DominoClientController.cs`
- `client/DominoGame/Assets/_Domino/Scripts/Realtime/RealtimeConnectionService.cs`
- `client/DominoGame/Assets/_Domino/Scripts/Social/Editor/SocialPresenceVisualValidation.cs`
- `client/DominoGame/Assets/_Domino/Scripts/Social/Editor/SocialPresenceVisualValidation.cs.meta`
- `client/DominoGame/Assets/_Domino/Scripts/Social/SocialClient.cs`
- `client/DominoGame/Assets/_Domino/Scripts/Social/SocialPresenceStore.cs`
- `client/DominoGame/Assets/_Domino/Scripts/Social/SocialPresenceStore.cs.meta`
- `client/DominoGame/Assets/_Domino/Scripts/Social/SocialView.cs`
- `client/Validation/RunSocialClientTests.ps1`
- `client/Validation/S1_4B_AUTHORIZED_SOCIAL_PRESENCE_REPORT.md`
- `client/Validation/SocialClientTests.cs`
- `server/domino/src/main/kotlin/com/teamfho/domino/online/OnlineMatchService.kt`
- `server/domino/src/main/kotlin/com/teamfho/domino/online/OnlineTurnWorker.kt`
- `server/domino/src/main/kotlin/com/teamfho/domino/realtime/PresenceStore.kt`
- `server/domino/src/main/kotlin/com/teamfho/domino/realtime/RealtimeHandler.kt`
- `server/domino/src/main/kotlin/com/teamfho/domino/social/LocalSocialAuthorizationIndex.kt`
- `server/domino/src/main/kotlin/com/teamfho/domino/social/SocialPresenceAuthorization.kt`
- `server/domino/src/main/kotlin/com/teamfho/domino/social/SocialPresenceModel.kt`
- `server/domino/src/main/kotlin/com/teamfho/domino/social/SocialPresencePubSub.kt`
- `server/domino/src/main/kotlin/com/teamfho/domino/social/SocialPresenceRuntime.kt`
- `server/domino/src/main/kotlin/com/teamfho/domino/social/SocialPresenceSubscriptions.kt`
- `server/domino/src/test/kotlin/com/teamfho/domino/realtime/RealtimeHandlerTests.kt`
- `server/domino/src/test/kotlin/com/teamfho/domino/social/SocialChaosValidationServer.kt`
- `server/domino/src/test/kotlin/com/teamfho/domino/social/SocialPresenceEmulatorTests.kt`
- `server/domino/src/test/kotlin/com/teamfho/domino/social/SocialPresenceFoundationTests.kt`
- `server/domino/src/test/kotlin/com/teamfho/domino/social/SocialPresenceRedisTests.kt`

### PRE_EXISTING_USER_CHANGE (97)

- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/query/client-agp/cache-v2`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/query/client-agp/cmakeFiles-v1`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/query/client-agp/codemodel-v2`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/cache-v2-2c0909d0b4389f2443c3.json`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/cmakeFiles-v1-2afea77556dece6ed3b6.json`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/codemodel-v2-56ef99f20c5d90a856eb.json`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/directory-.-RelWithDebInfo-d0094a50bb2071803777.json`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/directory-FramePacing-RelWithDebInfo-7f9c8865fd027a154c90.json`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/index-2026-09-20T07-54-05-0123.json`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/target-swappywrapper-RelWithDebInfo-de42165ac0b744ec5a6b.json`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.ninja_deps`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.ninja_log`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeCache.txt`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeCCompiler.cmake`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeCXXCompiler.cmake`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeDetermineCompilerABI_C.bin`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeDetermineCompilerABI_CXX.bin`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeSystem.cmake`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CompilerIdC/CMakeCCompilerId.c`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CompilerIdC/CMakeCCompilerId.o`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CompilerIdCXX/CMakeCXXCompilerId.cpp`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CompilerIdCXX/CMakeCXXCompilerId.o`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/TargetDirectories.txt`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/cmake.check_cache`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/rules.ninja`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/FramePacing/CMakeFiles/swappywrapper.dir/UnitySwappyWrapper.cpp.o`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/FramePacing/cmake_install.cmake`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/additional_project_files.txt`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/android_gradle_build.json`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/android_gradle_build_mini.json`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/build.ninja`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/build_file_index.txt`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/cmake_install.cmake`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/compile_commands.json`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/compile_commands.json.bin`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/configure_fingerprint.bin`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/metadata_generation_command.txt`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/prefab_config.json`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/symbol_folder_index.txt`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/hash_key.txt`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/prefab/arm64-v8a/prefab/lib/aarch64-linux-android/cmake/games-frame-pacing/games-frame-pacingConfig.cmake`
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/prefab/arm64-v8a/prefab/lib/aarch64-linux-android/cmake/games-frame-pacing/games-frame-pacingConfigVersion.cmake`
- `client/DominoGame/.utmp/tools/release/arm64-v8a/compile_commands.json`
- `client/DominoGame/Assets/AddressableAssetsData/Android.meta`
- `client/DominoGame/Assets/AddressableAssetsData/Android/addressables_content_state.bin`
- `client/DominoGame/Assets/AddressableAssetsData/Android/addressables_content_state.bin.meta`
- `client/DominoGame/Assets/AddressableAssetsData/ProfileDataSourceSettings.asset`
- `client/DominoGame/Assets/AddressableAssetsData/ProfileDataSourceSettings.asset.meta`
- `client/DominoGame/Assets/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0/firebase-app-unity-13.16.0.pom`
- `client/DominoGame/Assets/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0/firebase-auth-unity-13.16.0.pom`
- `client/DominoGame/Assets/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0/firebase-firestore-unity-13.16.0.pom`
- `client/DominoGame/Assets/GeneratedLocalRepo.meta`
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase.meta`
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository.meta`
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com.meta`
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google.meta`
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase.meta`
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity.meta`
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0.meta`
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0/firebase-app-unity-13.16.0.aar`
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0/firebase-app-unity-13.16.0.aar.meta`
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0/firebase-app-unity-13.16.0.pom`
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0/firebase-app-unity-13.16.0.pom.meta`
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity.meta`
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0.meta`
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0/firebase-auth-unity-13.16.0.aar`
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0/firebase-auth-unity-13.16.0.aar.meta`
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0/firebase-auth-unity-13.16.0.pom`
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0/firebase-auth-unity-13.16.0.pom.meta`
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity.meta`
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0.meta`
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0/firebase-firestore-unity-13.16.0.aar`
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0/firebase-firestore-unity-13.16.0.aar.meta`
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0/firebase-firestore-unity-13.16.0.pom`
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0/firebase-firestore-unity-13.16.0.pom.meta`
- `client/DominoGame/Assets/Plugins/Android/FirebaseApp.androidlib.meta`
- `client/DominoGame/Assets/Plugins/Android/FirebaseApp.androidlib/AndroidManifest.xml`
- `client/DominoGame/Assets/Plugins/Android/FirebaseApp.androidlib/project.properties`
- `client/DominoGame/Assets/Plugins/Android/FirebaseApp.androidlib/res/values/google-services.xml`
- `client/DominoGame/Assets/Plugins/Android/GoogleMobileAdsPlugin.androidlib/AndroidManifest.xml`
- `client/DominoGame/Assets/Plugins/Android/gradleTemplate.properties`
- `client/DominoGame/Assets/Plugins/Android/gradleTemplate.properties.meta`
- `client/DominoGame/Assets/Plugins/Android/mainTemplate.gradle`
- `client/DominoGame/Assets/Plugins/Android/mainTemplate.gradle.meta`
- `client/DominoGame/Assets/Plugins/Android/settingsTemplate.gradle`
- `client/DominoGame/Assets/Plugins/Android/settingsTemplate.gradle.meta`
- `client/DominoGame/Assets/StreamingAssets/google-services-desktop.json`
- `client/DominoGame/Assets/StreamingAssets/google-services-desktop.json.meta`
- `client/DominoGame/Assets/_Domino/Localization/Localization Settings.asset`
- `client/DominoGame/Assets/_Domino/Resources/AdsSettings.asset`
- `client/DominoGame/Assets/_Domino/Resources/ApiSettings.asset`
- `client/DominoGame/Assets/google-services.json`
- `client/DominoGame/ProjectSettings/AndroidResolverDependencies.xml`
- `client/DominoGame/ProjectSettings/GvhProjectSettings.xml`
- `client/DominoGame/ProjectSettings/ProjectSettings.asset`
- `client/DominoGame/ProjectSettings/ScriptableBuildPipeline.json`
- `client/Validation/S1_4_SOCIAL_PRESENCE_REPORT.md`

GENERATED_BY_VALIDATION: ignored `client/Validation/Generated/S14B/` scripts, logs, argument files, screenshots, measurements and diff; existing ignored G1Project Unity import/cache. No generated runtime output is staged.

UNEXPECTED_FILES=0. This classification was compared with the 97-file initial hash inventory.

## Cleanup

Both owned backend processes and the owned Firestore Emulator were identified by PID and command line, stopped, and verified absent. The task-only `domino-s14b-redis` container was stopped and removed. The user’s `domino-redis`, existing Unity Editor and protected configuration files were left intact. Prior to shutdown both authorization indexes were empty and Redis reported zero nonexpired player leases.

## Checkpoint candidate

```text
S1.4B CHECKPOINT CANDIDATE
=========================
BRANCH=main
SOURCE_SHA_BEFORE=cd7744d07f8e8de6768e919968d3d72a806914b3
SOURCE_SHA_AFTER=cd7744d07f8e8de6768e919968d3d72a806914b3
S1_4B_IMPLEMENTED=YES
S1_4B_SUCCESS=YES_WITH_DOCUMENTED_VALIDATION_LIMITS
S1_4B_READY_FOR_CHECKPOINT=YES_WITH_DOCUMENTED_VALIDATION_LIMITS
ONLINE_IMPLEMENTED=YES
OFFLINE_IMPLEMENTED=YES
IN_MATCH_IMPLEMENTED=YES
UNKNOWN_IMPLEMENTED=YES
UNKNOWN_EQUALS_OFFLINE=NO
LAST_SEEN_IMPLEMENTED=NO
PRESENCE_HISTORY_IMPLEMENTED=NO
PRESENCE_LEASE_MODEL=EXISTING_PER_CONNECTION_REDIS_TIME_EXPIRY
PRESENCE_LEASE_TTL=60_SECONDS
MULTI_CONNECTION_LEASES=PASS
MULTI_INSTANCE_LEASES=PASS
SECOND_HEARTBEAT_CREATED=NO
PRESENCE_PUBSUB_IMPLEMENTED=YES
PRESENCE_FIRESTORE_OUTBOX=NO
A3_AUTHORIZATION_HANDLE_REUSED=YES
A4_01_TRANSMISSION_BARRIER_REUSED=YES
SECOND_SOCIAL_AUTHORIZATION_SYSTEM_CREATED=NO
MAX_SOCIAL_PRESENCE_SUBSCRIPTIONS=50
SUBSCRIPTION_GENERATION_IMPLEMENTED=YES
RESNAPSHOT_IMPLEMENTED=YES
SOCIAL_EPHEMERAL_QUEUE_REUSED=YES
BLOCK_PRESENCE_REVOCATION=PASS
PRIVACY_PRESENCE_REVOCATION=PASS
UNFRIEND_PRESENCE_REAUTHORIZATION=PASS
FRIEND_ACCEPT_PRESENCE_REAUTHORIZATION=PASS
FOLLOW_GRANTS_PRESENCE=NO
MUTUAL_FOLLOW_GRANTS_PRESENCE=NO
MATCH_HIDDEN_PROJECTION=ONLINE
MATCH_DETAILS_EXPOSED=NO
REDIS_PRESENCE_DOWN_STATE=UNKNOWN
GAMEPLAY_SOCKET_CLOSED_ON_PRESENCE_REDIS_FAILURE=NO
FIRESTORE_PRESENCE_FALLBACK=NO
POST_REVOCATION_PRESENCE_DELIVERIES=0_IN_CONTROLLED_TESTS
PRESENCE_UID_EXPOSURE=0
MATCH_DETAIL_EXPOSURE=0
BLOCK_REASON_EXPOSURE=0
LAST_SEEN_EXPOSURE=0
PRESENCE_HISTORY_EXPOSURE=0
FIRESTORE_HEARTBEAT_WRITES=0
FIRESTORE_AUTH_READS_PER_HEARTBEAT=0
FIRESTORE_READS_PER_EPHEMERAL_SEND=0
REDIS_OPS_PER_EPHEMERAL_SEND=0
SOCIAL_READS_PER_GAMEPLAY_COMMAND=0
TWO_REAL_BACKEND_PROCESSES=YES
CROSS_INSTANCE_ONLINE=PASS
CROSS_INSTANCE_OFFLINE=PASS
CROSS_INSTANCE_IN_MATCH=PASS
CROSS_INSTANCE_UNKNOWN=PASS
CROSS_INSTANCE_BLOCK=PASS
CROSS_INSTANCE_PRIVACY=PASS
CROSS_INSTANCE_UNFRIEND=PASS
CROSS_INSTANCE_FRIEND_ACCEPT=PASS
PRESENCE_CHAOS_RUNS=25
PRESENCE_CHAOS_PASS=25
PRESENCE_CHAOS_FAIL=0
PRESENCE_LEASE_LEAKS=0_AT_FINAL_LOCAL_CHECK
PRESENCE_HANDLE_LEAKS=0_AT_FINAL_LOCAL_CHECK
PRESENCE_INDEX_LEAKS=0_AT_FINAL_LOCAL_CHECK
PRESENCE_WORKER_LEAKS=0_AFTER_OWNED_PROCESS_CLEANUP
BACKEND_PASS=620
BACKEND_SKIP=23
BACKEND_FAIL=0
FIRESTORE_EMULATOR_TESTS=45_PASS_1_SKIPPED_DISTINCT_ACROSS_RUNS
EMULATOR_FAIL=0
TRANSPORT_TESTS=35_PASS_INCLUDED_IN_BACKEND
TRANSPORT_FAIL=0
SOCIAL_CLIENT_TESTS=89_PASS
SOCIAL_CLIENT_FAIL=0
UNITY_STATIC_COMPILATION=PASS
UNITY_EDITMODE_TESTS=138_HARNESS_CHECKS_PASS
UNITY_EDITMODE_FAIL=0
UNITY_PLAYMODE_TESTS=60_HARNESS_CHECKS_PASS
UNITY_PLAYMODE_FAIL=0
UNITY_MANUAL_CHECKS=1_CAPTURE_REVIEWED_NO_PHYSICAL_DEVICE_RUN
CONSOLE_ERRORS=0
S1_4A_1_REGRESSION=PASS
S1_4A_2_REGRESSION=PASS
S1_4A_3_REGRESSION=PASS
S1_4A_4_REGRESSION=PASS_RELEVANT_AUTOMATED_SUBSET
A4_01_REGRESSION=PASS
S1_1_REGRESSION=PASS
S1_2_REGRESSION=PASS
S1_3_REGRESSION=PASS
MATCH_EVENT_SEQUENCE_CHANGED=NO
REPLAY_SEQUENCE_CHANGED=NO
SAFE_RETENTION_POLICY=DEFER_CLEANUP_UNTIL_PRODUCTION_HARDENING
RETENTION_CLEANUP_READY=NO
PRODUCTION_RELEASE_BLOCKED_BY_RETENTION_POLICY=YES
PRODUCTION_READY=NO
REAL_FIRESTORE_CALLS=0
REAL_REDIS_PRODUCTION_CALLS=0
BOT_SWARM_STARTED=NO
S1_4B_FILES_MODIFIED=30_INCLUDING_NEW_FILES_AND_REPORT
PREEXISTING_USER_FILES=97
GENERATED_BY_VALIDATION_FILES=IGNORED_GENERATED_S14B_DIRECTORY_AND_EXISTING_ISOLATED_UNITY_PROJECT
UNEXPECTED_FILES=0
PREEXISTING_USER_FILES_PRESERVED=YES_SHA256_97_OF_97
UNRELATED_USER_FILES_MODIFIED_BY_S1_4B=NO
COMMIT=NONE
PUSH=NONE
DEPLOY=NONE
S1_4_COMPLETE=YES_WITH_DOCUMENTED_VALIDATION_LIMITS
S1_FUNCTIONALLY_COMPLETE=YES_WITH_DOCUMENTED_VALIDATION_LIMITS
S2_STARTED=NO
NEXT=S1.4B_REVIEW_CHECKPOINT
```
