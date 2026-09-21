# CUBAN DOMINO CLUB — S1.4A.4 MULTI-INSTANCE CHAOS REPORT

Current result: **A4-01 corrected; resumed A.4 passed with the documented validation limits below.** Original failure retained in the historical appendix. No independent A4-01 checkpoint.

## Correction and boundary

A4-01 was an admission-only authorization check: a Social payload already queued/selected was not tied to the A.3 authorization generation. The writer could transmit it after invalidation. The original deterministic failure and source are retained below; no real user data was exposed.

`Handle.capability()` now captures its A.3 generation. Revocation marks it invalid and increments generation under the same local index monitor used by `tryCommit()`. A successful final `tryCommit()` immediately before transport is the logical transmission commit point. The monitor is released before socket I/O. If commit linearizes first, that already-committed frame can finish; if invalidation linearizes first, the frame is dropped. This is not a claim that a remote process learns revocation before Pub/Sub/durable recovery reaches it.

Revocation callbacks remove queued candidates; a selected candidate is checked at the final barrier. Callback removal never takes the authority monitor while holding the writer queue lock. Coalescing, close and completion release registrations. Reauthorization issues a new generation and cannot revive old candidates. Missing capabilities are denied by default. Consequently the **unchanged original test now passes through default denial**; positive capability admission/selection tests and real-process probes demonstrate the actual corrected race, rather than relying on that old test alone.

The three production files do not add Firestore/Redis operations, protocol fields, Presence, queue limits, Match/replay rules or a second authorization service.

## Evidence and counts

- Final backend: **617 passed, 15 skipped, 0 failed**. Skips retain explicit opt-ins; ordinary tests force loopback emulator/invalid port. No Swarm process started; any module task is unit validation only.
- Firestore Emulator: **40 passed**, including S1.1/S1.2/S1.3/A.2R/A.3 and 50 concurrent Follow/Unfollow–Block races. Transaction lock retries during this stress are expected; final invariants pass.
- A4-01: **11 new JUnit tests + original unchanged test = 12 passed**. One test contains 100 selected/invalidation/connection-close races. A1 transport has 23 tests. A2/A3 dedicated Redis tests: 5 passed; distributed Emulator: 1 passed. These are subsets, not extra tests to add to the backend total.
- Client Social: **78 passed**. Static Unity runtime/editor compilation passed with existing unused-member warnings. No Unity Editor/Play Mode run; console errors **NOT_MEASURED**.
- Real independent Spring JVMs A/B shared only loopback Emulator/Redis. Twenty-five reproducible queue-chaos iterations (fault every fifth iteration), four privacy/Unfriend/reauth cases, three Redis restart cycles and saturation/close/account-switch checks all rejected stale candidates. No stale frame transmitted after local invalidation linearized.
- Ten cross-process Follow/Block races passed. During Redis outage, two concurrent accepts at friend count 4 returned 200/409 and final count 5.
- A/B/both restarts, dispatcher pause/failure and 100-event backlog passed. Feed pages were 20/20/20/20/20/0; continuation across B restart had 100 unique events. An audit at that point found 205 durable events and zero missing pair-revision events. Later Emulator tests use their own fixtures; this is a scoped audit, not a permanent database invariant scan.
- Three connections for the same viewer, split over A/B, rejected their old selected frames. Thirty-eight duplicate/out-of-order and four invalid Pub/Sub publications did not restore authorization.
- Five Pub/Sub disconnect/reconnect cycles left **2 subscribers**, **5 Redis client connections before/after**. No local handles or probe writers remained. A 15s idle sample used A=0.15625 CPU seconds, B=0.078125; native handles/threads were stable or decreased. This is bounded observation, not a heap profiler or long-duration leak certification.

### Actual Match and replay

A normal authenticated `/ws/v1/realtime` DUEL completed while Social Pub/Sub was disabled and a 20-event dispatcher backlog existed: **66 commands, 203 events, 4 rounds**, including client reconnection. Transport sequences were consecutive. Replay manifest reconstruction and event paging matched persisted board, score, winner, round and sequence.

A separate total-Redis-outage run confirmed an **existing G3 behavior**: its Redis-backed realtime presence failure closes connections with retryable UNAVAILABLE (`RealtimeHandler.broadcastActivity`). The first probe correctly exposed this; uninterrupted WebSocket service under total Redis loss is not claimed. With restoration and reconnection of the same test identities, retrying the same command ID preserved idempotency and the Match finished: **153 commands, 465 events, 8 rounds**, replay parity PASS over two pages. A.4 did not redesign this behavior.

The Match fixtures use canonical catalog v2 through the existing development DUEL endpoint. Matchmaking emits its existing unavailable warning because this fixture is not an online matchmaking publication; no matchmaking queue was started. This fixture warning is not attributed to A4-01 and no logging/Matchmaking production change was made.

### Cost and performance

Measured Emulator document operation costs (reads/writes): Block stranger 8/4; Unblock 3/4; Accept 17/8; Unfriend 5/7; Privacy 4/2; Block friend+mutual Follow 8/13. Feed empty 1/0, one 1/0, twenty 20/0; dispatch one 1/0, twenty 20/0; ACK 0/1. Reauthorization for 1/20/50 identical viewer-target-reader handles: 5/0 each. These are method-scoped measurements, not total chaos billing.

Final barrier microbench: 100 decisions average 1087ns; 1000 average 486ns; 10000 average 145ns. This is a local warmup-sensitive test, not a capacity benchmark. Reader invocation count is unchanged through the barrier benchmark. Dedicated rapid-generation test records 3 decisions / 2 rejections / 1 allowed commit; these are not global production metrics.

Memory remains bounded by the existing 50 queued social targets per connection plus the selected candidate. Each queued item adds a capability/registration and atomic cleanup state. Conservative **estimate**, not heap measurement: 256–512 bytes extra per candidate gives about 1.3–2.6MiB for 100 fully occupied connections, 13–26MiB for 1000, 130–260MiB for 10000, excluding existing queue payloads/index/runtime overhead. Allocation is per offered candidate, not upfront.

## Limitations / retention / failure domains

- No production Firestore/Redis, Android device or Unity Play Mode used. All persistence calls target `demo-domino-f0` Emulator. No deployment or real Firebase account creation.
- Exact whole-process startup reads, Pub/Sub wire-command totals and long-duration heap/listener retention were not instrumented; method costs and bounded resource samples are reported without substituting estimates for measurements.
- No unsafe feed/outbox cleanup added. Retain events; cleanup and a production recovery-window proof remain required before production Presence release. S1.4B may be reviewed as the next development phase; it was not started here.
- Redis command failure preserves A2 status policy: Search/Follow fail 503; Profile/Block/Unblock/Unfollow/Send Request/tightening succeed; loosening fails 503. Concurrent Accept retains atomic limits. Rate-limit 429 is not a circuit failure (regression tests).
- Pub/Sub or dispatcher loss retains durable recovery; uncertain authorization fails closed. Firestore failure before mutation returns 503 without an event/mutation commit. Failed reauthorization issues no usable capability. Restart loses local capabilities and derives new authorization from durable state.
- No full database social graph scan was introduced in production. The test-only audit enumerates its isolated Emulator fixture; the production feed uses bounded cursor pages.

## Exact file boundary

### A.4 production correction (3)

- `server/domino/src/main/kotlin/com/teamfho/domino/realtime/ConnectionOutbound.kt`
- `server/domino/src/main/kotlin/com/teamfho/domino/realtime/EphemeralAuthorization.kt`
- `server/domino/src/main/kotlin/com/teamfho/domino/social/LocalSocialAuthorizationIndex.kt`

### A.4 tests (4)

- `server/domino/src/test/kotlin/com/teamfho/domino/realtime/ConnectionOutboundTests.kt`
- `server/domino/src/test/kotlin/com/teamfho/domino/social/EphemeralBarrierTests.kt`
- `server/domino/src/test/kotlin/com/teamfho/domino/social/SocialChaosValidationServer.kt`
- `server/domino/src/test/kotlin/com/teamfho/domino/social/SocialOutboundAuthorizationTests.kt` — original failing test, byte-for-byte preserved

### Report (1)

- `client/Validation/S1_4A_4_MULTI_INSTANCE_CHAOS_REPORT.md`

### Protected prior work (97)

All SHA256 hashes match the initial S14A4 baseline. Includes Android/Google Play, Ads/API/Localization, Firebase/resolver files and prior Social report. Exact list:

```text
client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/query/client-agp/cache-v2
client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/query/client-agp/cmakeFiles-v1
client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/query/client-agp/codemodel-v2
client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/cache-v2-2c0909d0b4389f2443c3.json
client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/cmakeFiles-v1-2afea77556dece6ed3b6.json
client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/codemodel-v2-56ef99f20c5d90a856eb.json
client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/directory-.-RelWithDebInfo-d0094a50bb2071803777.json
client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/directory-FramePacing-RelWithDebInfo-7f9c8865fd027a154c90.json
client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/index-2026-09-20T07-54-05-0123.json
client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/target-swappywrapper-RelWithDebInfo-de42165ac0b744ec5a6b.json
client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.ninja_deps
client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.ninja_log
client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeCache.txt
client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeCCompiler.cmake
client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeCXXCompiler.cmake
client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeDetermineCompilerABI_C.bin
client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeDetermineCompilerABI_CXX.bin
client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeSystem.cmake
client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CompilerIdC/CMakeCCompilerId.c
client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CompilerIdC/CMakeCCompilerId.o
client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CompilerIdCXX/CMakeCXXCompilerId.cpp
client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CompilerIdCXX/CMakeCXXCompilerId.o
client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/TargetDirectories.txt
client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/cmake.check_cache
client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/rules.ninja
client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/FramePacing/CMakeFiles/swappywrapper.dir/UnitySwappyWrapper.cpp.o
client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/FramePacing/cmake_install.cmake
client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/additional_project_files.txt
client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/android_gradle_build.json
client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/android_gradle_build_mini.json
client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/build.ninja
client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/build_file_index.txt
client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/cmake_install.cmake
client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/compile_commands.json
client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/compile_commands.json.bin
client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/configure_fingerprint.bin
client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/metadata_generation_command.txt
client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/prefab_config.json
client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/symbol_folder_index.txt
client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/hash_key.txt
client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/prefab/arm64-v8a/prefab/lib/aarch64-linux-android/cmake/games-frame-pacing/games-frame-pacingConfig.cmake
client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/prefab/arm64-v8a/prefab/lib/aarch64-linux-android/cmake/games-frame-pacing/games-frame-pacingConfigVersion.cmake
client/DominoGame/.utmp/tools/release/arm64-v8a/compile_commands.json
client/DominoGame/Assets/AddressableAssetsData/Android.meta
client/DominoGame/Assets/AddressableAssetsData/Android/addressables_content_state.bin
client/DominoGame/Assets/AddressableAssetsData/Android/addressables_content_state.bin.meta
client/DominoGame/Assets/AddressableAssetsData/ProfileDataSourceSettings.asset
client/DominoGame/Assets/AddressableAssetsData/ProfileDataSourceSettings.asset.meta
client/DominoGame/Assets/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0/firebase-app-unity-13.16.0.pom
client/DominoGame/Assets/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0/firebase-auth-unity-13.16.0.pom
client/DominoGame/Assets/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0/firebase-firestore-unity-13.16.0.pom
client/DominoGame/Assets/GeneratedLocalRepo.meta
client/DominoGame/Assets/GeneratedLocalRepo/Firebase.meta
client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository.meta
client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com.meta
client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google.meta
client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase.meta
client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity.meta
client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0.meta
client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0/firebase-app-unity-13.16.0.aar
client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0/firebase-app-unity-13.16.0.aar.meta
client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0/firebase-app-unity-13.16.0.pom
client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0/firebase-app-unity-13.16.0.pom.meta
client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity.meta
client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0.meta
client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0/firebase-auth-unity-13.16.0.aar
client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0/firebase-auth-unity-13.16.0.aar.meta
client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0/firebase-auth-unity-13.16.0.pom
client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0/firebase-auth-unity-13.16.0.pom.meta
client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity.meta
client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0.meta
client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0/firebase-firestore-unity-13.16.0.aar
client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0/firebase-firestore-unity-13.16.0.aar.meta
client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0/firebase-firestore-unity-13.16.0.pom
client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0/firebase-firestore-unity-13.16.0.pom.meta
client/DominoGame/Assets/Plugins/Android/FirebaseApp.androidlib.meta
client/DominoGame/Assets/Plugins/Android/FirebaseApp.androidlib/AndroidManifest.xml
client/DominoGame/Assets/Plugins/Android/FirebaseApp.androidlib/project.properties
client/DominoGame/Assets/Plugins/Android/FirebaseApp.androidlib/res/values/google-services.xml
client/DominoGame/Assets/Plugins/Android/GoogleMobileAdsPlugin.androidlib/AndroidManifest.xml
client/DominoGame/Assets/Plugins/Android/gradleTemplate.properties
client/DominoGame/Assets/Plugins/Android/gradleTemplate.properties.meta
client/DominoGame/Assets/Plugins/Android/mainTemplate.gradle
client/DominoGame/Assets/Plugins/Android/mainTemplate.gradle.meta
client/DominoGame/Assets/Plugins/Android/settingsTemplate.gradle
client/DominoGame/Assets/Plugins/Android/settingsTemplate.gradle.meta
client/DominoGame/Assets/StreamingAssets/google-services-desktop.json
client/DominoGame/Assets/StreamingAssets/google-services-desktop.json.meta
client/DominoGame/Assets/_Domino/Localization/Localization Settings.asset
client/DominoGame/Assets/_Domino/Resources/AdsSettings.asset
client/DominoGame/Assets/_Domino/Resources/ApiSettings.asset
client/DominoGame/Assets/google-services.json
client/DominoGame/ProjectSettings/AndroidResolverDependencies.xml
client/DominoGame/ProjectSettings/GvhProjectSettings.xml
client/DominoGame/ProjectSettings/ProjectSettings.asset
client/DominoGame/ProjectSettings/ScriptableBuildPipeline.json
client/Validation/S1_4_SOCIAL_PRESENCE_REPORT.md
```

Generated ignored evidence/scripts/logs: `Generated/A401/classification.json`. Unexpected tracked/untracked files: **0**. No pre-existing user change was staged, reverted or edited.

## Required A.4 report fields

```text
BRANCH=main
SOURCE_SHA_BEFORE=324ae674186cc8ccf465c4f6779e31f826adeb9a
SOURCE_SHA_AFTER=324ae674186cc8ccf465c4f6779e31f826adeb9a
INSTANCE_A_PROCESS=JVM; original 52500; final 29424; stopped
INSTANCE_A_PORT=18141
INSTANCE_B_PROCESS=JVM; original 44584; final 53928; stopped
INSTANCE_B_PORT=18142
PROCESSES_SHARE_MEMORY=NO
FIRESTORE_EMULATOR_HOST=127.0.0.1:18085; demo-domino-f0
REDIS_LOCAL_HOST=127.0.0.1:16379; domino-a4-redis
SHARED_FIRESTORE_EMULATOR=YES
SHARED_LOCAL_REDIS=YES
PRODUCTION_FIRESTORE_CONFIGURED=NO
PRODUCTION_REDIS_CONFIGURED=NO
INSTANCE_A_READY=PASS
INSTANCE_B_READY=PASS
FIRESTORE_EMULATOR_READY=PASS
REDIS_READY=PASS
PUBSUB_A_READY=PASS
PUBSUB_B_READY=PASS
A1_BASELINE=PASS
A2_BASELINE=PASS
A3_BASELINE=PASS
CROSS_INSTANCE_BLOCK=PASS
CROSS_INSTANCE_PRIVACY=PASS
CROSS_INSTANCE_UNFRIEND=PASS
CROSS_INSTANCE_FRIEND_ACCEPT=PASS
FOLLOW_BLOCK_CROSS_INSTANCE=PASS
CONCURRENT_ACCEPT_REDIS_DOWN=PASS
MISSED_PUBSUB_EVENT=PASS
PUBSUB_DISCONNECT_RECOVERY=PASS
DUPLICATE_PUBLICATION=PASS
OUT_OF_ORDER_PUBLICATION=PASS
DISPATCHER_PAUSE_RECOVERY=PASS
DISPATCHER_FAILURE_RECOVERY=PASS
FEED_PAGINATION=PASS
WATERMARK_RESTART=PASS
REDIS_FULL_OUTAGE=PASS
REDIS_RECOVERY=PASS
REDIS_RESTART_CYCLES=3 mandatory queue cycles + 3 additional outage/recovery exercises
BLOCK_WHILE_REDIS_DOWN=PASS
BLOCK_REDIS_DOWN_DURABLE_COMMIT=PASS
BLOCK_REDIS_DOWN_LOCAL_INVALIDATION=PASS
BLOCK_REDIS_DOWN_REMOTE_SAFETY=PASS
BLOCK_REDIS_DOWN_PRIVACY_LEAK=NO
SOCIAL_REDIS_CIRCUIT_RECOVERY=PASS
INSTANCE_A_RESTART=PASS
INSTANCE_B_RESTART=PASS
BOTH_INSTANCES_RESTART=PASS
INSTANCE_STARTUP_RECOVERY=PASS
FIRESTORE_REAUTH_FAILURE=PASS
FIRESTORE_MUTATION_FAILURE=PASS
FIRESTORE_RECOVERY=PASS
REAUTH_FAILURE_BEHAVIOR=FAIL_CLOSED
SLOW_SOCKET_INVALIDATION=PASS
IN_FLIGHT_FRAME_BOUNDARY=PASS
QUEUE_SATURATION_INVALIDATION=PASS
CRITICAL_GAMEPLAY_UNDER_PRESSURE=PASS
CONTROL_FAIRNESS_UNDER_PRESSURE=PASS
CONNECTION_CLOSE_INVALIDATION_RACE=PASS
POST_INVALIDATION_UNAUTHORIZED_DELIVERIES=0
COMMITTED_MUTATIONS_WITHOUT_DURABLE_INVALIDATION=0
REVISION_REGRESSIONS=0
MISSED_PUBSUB_PRIVACY_LEAK=NO
FIRESTORE_REAUTH_FAILURE_PRIVACY_LEAK=NO
PUBSUB_SUBSCRIBER_LEAKS=0
OUTBOX_WORKER_LEAKS=0
WRITER_TASK_LEAKS=0
STALE_LOCAL_AUTH_HANDLES=0
REDIS_CONNECTION_LEAK_OBSERVED=NO
FEED_WORKER_LEAK_OBSERVED=NO
BUSY_LOOP_OBSERVED=NO
BLOCK_REMOTE_INVALIDATION_LATENCY_MS=baseline 5062; healthy queued samples 344..484
PRIVACY_REMOTE_INVALIDATION_LATENCY_MS=516 baseline
UNFRIEND_REMOTE_INVALIDATION_LATENCY_MS=265 baseline
MISSED_PUBSUB_RECOVERY_LATENCY_MS=4984 baseline; queued fault samples 906..2922
REDIS_RECOVERY_TO_PUBSUB_READY_MS=NOT_SEPARATELY_MEASURED; command recovery 4813,344,5562 ms
INSTANCE_RESTART_TO_RECOVERY_READY_MS=readiness checked within 45s; exact latency NOT_MEASURED
OUTBOX_BACKLOG_SIZE=100
OUTBOX_DRAIN_TIME_MS=2500 after final dispatcher release; B restart may already drain some pending events
OUTBOX_BATCHES=feed pages 20,20,20,20,20,0; dispatch batch executions not counted
MISSED_EVENT_RECOVERY_READS=measured page costs: empty=1, one=1, twenty=20; plus authorization transaction=5 per viewer/target/reader group
MISSED_EVENT_RECOVERY_WRITES=0 for feed and reauthorization; publication ACK=1/event
INSTANCE_STARTUP_RECOVERY_READS=fresh fence query + current authorization; whole-startup total NOT_MEASURED
INSTANCE_STARTUP_RECOVERY_WRITES=0 authorization reconstruction; pending outbox ACK writes may occur
IDLE_FEED_READS_PER_MINUTE=0 scheduled feed polls with 0 handles (15s stable recovery counters + source); initial listener/fence costs excluded
PUBSUB_RECONNECT_OPS=5 explicit reconnect cycles, 2 final subscribers; exact command total NOT_MEASURED
INVALIDATION_PUBLISH_OPS=1 PUBLISH/event attempt; 38 explicit duplicate/out-of-order + 4 invalid publications tested
INVALIDATION_RECEIVE_OPS=0 receiver Redis commands per notification; callback processing local
FIRESTORE_AUTH_READS_PER_HEARTBEAT=0
FIRESTORE_HEARTBEAT_WRITES=0
SOCIAL_INVALIDATION_POLLING_PER_HEARTBEAT=0
SOCIAL_READS_PER_GAMEPLAY_COMMAND=0
MAX_OBSERVED_FEED_LAG=100 queued events; no gap across restart
MAX_OBSERVED_RECOVERY_WINDOW=~5s measured revocation; not a retention guarantee
SAFE_RETENTION_POLICY=DEFER_CLEANUP_UNTIL_PRODUCTION_HARDENING
RETENTION_CLEANUP_READY=NO
RETENTION_CLEANUP_DEFERRED_REASON=No proof of safe production offline recovery window; no deletion/TTL introduced
S1_4A_4_FOCUSED_TESTS=12 composition JUnit PASS; 100 race iterations inside one test
MULTI_PROCESS_TESTS=11 baseline; 25 queued chaos; 4 privacy/reauth; 3 Redis queue cycles; 10 Follow/Block races; 1 concurrent Accept; 8 recovery/audit; 4 publication/resource; 3 pressure/close/account; 11 final local checks; 2 completed real Matches + 2 replay parity checks
CHAOS_RUNS=25
CHAOS_PASS=25
CHAOS_FAIL=0
BACKEND_TESTS=617 PASS / 15 SKIPPED / 0 FAILED (632 discovered)
FIRESTORE_EMULATOR_TESTS=40 PASS / 0 SKIPPED / 0 FAILED
REDIS_TESTS=5 PASS (A2=2, A3=3); plus 1 distributed Emulator test
TRANSPORT_TESTS=23 A1 PASS + 12 composition PASS (overlapping backend count)
SOCIAL_CLIENT_TESTS=78 PASS
UNITY_STATIC_COMPILATION=PASS; existing warnings only
UNITY_EDITMODE_TESTS=NOT_RUN_BACKEND_ONLY_WIRE_UNCHANGED
UNITY_PLAYMODE_TESTS=NOT_RUN_BACKEND_ONLY_WIRE_UNCHANGED
UNITY_MANUAL_CHECKS=NOT_RUN_BACKEND_ONLY_WIRE_UNCHANGED
CONSOLE_ERRORS=NOT_MEASURED
S1_4A_1_REGRESSION=PASS
S1_4A_2_REGRESSION=PASS
S1_4A_3_REGRESSION=PASS
AUTH_REGRESSION=PASS
PROFILE_REGRESSION=PASS
ENTITLEMENTS_REGRESSION=PASS
S1_1_REGRESSION=PASS
S1_2_REGRESSION=PASS
S1_3_REGRESSION=PASS
I4_REGRESSION=PASS
M5_REGRESSION=PASS
MATCHMAKING_REGRESSION=PASS
MONETIZATION_REGRESSION=PASS
F0_F01_REGRESSION=PASS
HEARTBEAT_REGRESSION=PASS
RECONNECT_REGRESSION=PASS
MATCH_WEBSOCKET_REGRESSION=PASS
MATCH_EVENT_SEQUENCE_CHANGED=NO
REPLAY_SEQUENCE_CHANGED=NO
DEFECTS_FOUND=A4-01 corrected; existing G3 Redis outage close policy documented
PRODUCTION_FILES_CHANGED=3
TEST_FILES_CHANGED=4 A4 files (original failing test preserved unchanged)
REPORT_FILES_CHANGED=1
REAL_FIRESTORE_CALLS=0
REAL_REDIS_PRODUCTION_CALLS=0
BOT_SWARM_STARTED=NO
FIRESTORE_RULES_DEPLOYED=NO
FIRESTORE_INDEXES_DEPLOYED=NO
BACKEND_DEPLOYED=NO
UNITY_DEPLOYED=NO
GOOGLE_PLAY_UPLOAD=NO
PREEXISTING_USER_FILES_PRESERVED=YES
ADS_SETTINGS_PRESERVED=YES
API_SETTINGS_PRESERVED=YES
LOCALIZATION_SETTINGS_PRESERVED=YES
GOOGLE_SERVICES_JSON_PRESERVED=YES
ANDROID_PROJECT_SETTINGS_PRESERVED=YES
ANDROID_RESOLVER_WORK_PRESERVED=YES
GOOGLE_PLAY_PREPARATION_PRESERVED=YES
PREVIOUS_S1_4_REPORTS_PRESERVED=YES
PRESENCE_IMPLEMENTED=NO
ONLINE_IMPLEMENTED=NO
OFFLINE_IMPLEMENTED=NO
IN_MATCH_IMPLEMENTED=NO
PRESENCE_REDIS_STATE_IMPLEMENTED=NO
PRESENCE_WEBSOCKET_PROTOCOL_IMPLEMENTED=NO
UNITY_PRESENCE_UI_IMPLEMENTED=NO
S1_4B_STARTED=NO
S2_STARTED=NO
S1_4A_4_FILES_MODIFIED=8 unique files
PREEXISTING_USER_FILES=97 SHA256 verified
GENERATED_BY_VALIDATION_FILES=Generated/A401/classification.json
UNEXPECTED_FILES=0
UNRELATED_USER_FILES_MODIFIED_BY_S1_4A_4=NO
COMMIT=NONE
PUSH=NONE
DEPLOY=NONE
```

## A4-01 AUTHORIZATION / OUTBOUND COMPOSITION FIX

```text
DEFECT_ID=A4-01
ORIGINAL_RESULT=FAIL
ORIGINAL_FAILURE=queued synthetic Social ephemeral frame transmitted after authorization invalidation
REAL_USER_DATA_EXPOSED=NO
ROOT_CAUSE=admission check not bound to queued candidate generation
CORRECTION=A3 generation capability plus final process-local transmission barrier and queue cancellation
TRANSMISSION_COMMIT_POINT=successful tryCommit under A3 index monitor before socket IO
AUTHORIZATION_CAPABILITY_MODEL=immutable captured Handle generation; not durable and not on wire
INVALIDATION_LINEARIZATION_POINT=revoke marks invalid and increments generation under same monitor
FIRESTORE_READS_PER_EPHEMERAL_SEND=0
REDIS_OPS_PER_EPHEMERAL_SEND=0
SECOND_SOCIAL_AUTHORIZATION_SYSTEM_CREATED=NO
EXISTING_DETERMINISTIC_TEST=PASS
QUEUED_REVOCATION_TEST=PASS
SELECTED_REVOCATION_TEST=PASS
COMMIT_POINT_RACE_TEST=PASS
REAUTHORIZE_TEST=PASS
COALESCE_REVOKE_TEST=PASS
BLOCK_TEST=PASS
PRIVACY_TEST=PASS
UNFRIEND_TEST=PASS
FAIL_CLOSED_TEST=PASS
SLOW_SOCKET_TEST=PASS
SATURATION_TEST=PASS
DEADLOCK_TEST=PASS
CROSS_INSTANCE_COMPOSITION_TEST=PASS
MISSED_PUBSUB_COMPOSITION_TEST=PASS
POST_INVALIDATION_UNAUTHORIZED_DELIVERIES=0
UNSENT_REVOKED_EPHEMERAL_CONSUMES_SEQUENCE=NO
CROSS_INSTANCE_STALE_QUEUED_FRAME_TRANSMITTED=NO
MISSED_PUBSUB_STALE_QUEUED_FRAME_TRANSMITTED=NO
REDIS_DOWN_STALE_QUEUED_FRAME_TRANSMITTED=NO
OLD_PRIVACY_GENERATION_FRAME_TRANSMITTED=NO
OLD_CANDIDATE_REUSED_AFTER_REAUTH=NO
EPHEMERAL_AUTH_CHECKS=3 in dedicated generation decision test
EPHEMERAL_AUTH_REJECTIONS=2 in dedicated generation decision test
EPHEMERAL_TRANSMISSION_COMMITS=1 in dedicated generation decision test
STALE_GENERATION_REJECTIONS=2 in dedicated generation decision test
TRANSPORT_SEQUENCE_MONOTONIC=PASS
CRITICAL_LIMIT_CHANGED=NO
CONTROL_LIMIT_CHANGED=NO
SOCIAL_LIMIT_CHANGED=NO
STALE_AUTH_CAPABILITY_REFERENCES=0 registered callbacks after deterministic cleanup; heap-GC proof not claimed
A1_REGRESSION=PASS
A2_REGRESSION=PASS
A3_REGRESSION=PASS
PRODUCTION_FILES_CHANGED=3
TEST_FILES_CHANGED=4 A4 files (original failing test preserved unchanged)
A4_01_CORRECTION_COMPLETE=YES
A4_01_SUCCESS=YES
A4_FULL_VALIDATION_RESUMED=YES
```

## Original A.4 failure record (historical, superseded by results above)

<details>
<summary>Retained original report and FAIL evidence</summary>

# CUBAN DOMINO CLUB â€” S1.4A.4 MULTI-INSTANCE CHAOS REPORT

## Outcome: STOPPED â€” integration gate failed

Base/current SHA: `324ae674186cc8ccf465c4f6779e31f826adeb9a`, branch `main`.
No production source was changed. A.4 is incomplete and not ready for checkpoint.
The mandatory stop condition in task section 112 was reached: queued synthetic Social
state was transmitted after its authorization handle became invalid. The focused
reproduction intentionally remains failing for review; no full regression PASS is claimed.

## Actual topology and isolation

- Backend A: real Spring Boot JVM PID 18408, HTTP/WS loopback port 18141.
- Backend B: real Spring Boot JVM PID 10076, HTTP/WS loopback port 18142.
- Separate JVM heaps, local indexes, writers, rate circuits and fallback limiters.
- Shared Firestore Emulator PID 52088: `127.0.0.1:18085`, project `demo-domino-f0`, explicit EmulatorCredentials.
- Shared dedicated Redis 7 container `domino-a4-redis`: `127.0.0.1:16379` only.
- Existing `domino-redis` on 6379 was not stopped or modified.
- Test-only Firebase verifier in an explicitly launched test-classpath host, synthetic `a4-*` identities. Firebase disabled. Environment sanitized before launch.
- Production controllers/repositories, SocialRateGate and SocialInvalidationRuntime were used by HTTP tests.
- The diagnostic controller only exists on the test classpath and `a4-local` profile. It is not included in production artifacts.
- No Bot Swarm, production services, Presence implementation or Unity runs.
- All three owned JVM processes and dedicated Redis were stopped; ports 18085/18141/18142/16379 verified closed.

## Observed cross-process scenarios

| Scenario | Result | Local elapsed ms |
|---|---|---:|
| BASELINE_AUTHORIZATION | PASS | 32 |
| CROSS_INSTANCE_BLOCK | PASS | 593 |
| CROSS_INSTANCE_UNBLOCK | PASS | 157 |
| CROSS_INSTANCE_PRIVACY | PASS | 828 |
| CROSS_INSTANCE_FRIEND_ACCEPT | PASS | 203 |
| CROSS_INSTANCE_UNFRIEND | PASS | 797 |
| PUBSUB_DOWN_COMMAND_PATH_AVAILABLE | PASS | 62 |
| MISSED_PUBSUB_BLOCK_DURABLE_RECOVERY | PASS | 5062 |
| PUBSUB_RECONNECT | PASS | 2063 |
| FIRESTORE_REAUTH_FAILURE_FAIL_CLOSED | PASS | 797 |
| FIRESTORE_REAUTH_RECOVERY | PASS | 156 |

Times include HTTP mutation and bounded observation, not a production latency SLA or
an exact network-only Pub/Sub measurement. Fast-path publication and durable recovery
both run in the healthy baseline; latency alone does not attribute a particular delivery.
Pub/Sub fault closes B's subscriber and suspends its publisher using test-only reflection;
the production durable recovery loop remains running. Search on B still returned 200.
Reauthorization failure uses the test reader throwing before its durable read, not a full
Emulator outage. Redis-down scenarios actually stopped the isolated container.

## Redis outage matrix

| Operation | Observed |
|---|---|
| Search | 503 SOCIAL_SERVICE_UNAVAILABLE |
| Follow | 503 SOCIAL_SERVICE_UNAVAILABLE |
| Profile | 200 |
| Block | 200; remote handle denied after 5563 ms measured from response |
| Unblock | 200 |
| Unfollow | 200 |
| Privacy tightening | 200 |
| Privacy loosening | 503 SOCIAL_SERVICE_UNAVAILABLE |
| Concurrent Friend Accept | 200 + 409 FRIEND_LIMIT_REACHED; final FREE count 5 |
| Send/Decline/Cancel/Unfriend during outage | NOT_RUN |

Two outage/restoration cycles ran before stop. Command circuit HALF_OPEN -> CLOSED
was observed after restoration. Three-cycle resource-leak/recovery validation is NOT_RUN.
No unexpected 500 occurred in this outage matrix. An earlier harness request used the
wrong Unfriend URL and got 500; corrected to the existing `/api/v1/player/friends/{id}`
route before the recorded scenario run. This is not reported as a Redis failure defect.

## Cross-instance concurrency

Ten Follow-vs-Block races used independent backend processes and real Emulator
transactions. All ten finished with successful Block, Follow either 200 or 404,
and both users' follower/following counters zero. No escaped lock-timeout was observed.
A FREE fixture was built using four actual friendships. During a real Redis outage,
two requests were accepted concurrently through A and B: one 200, one 409, count 5.
This proves the tested cross-process limit boundary, not the entire friend-quota matrix.

## Blocking integration finding A4-01

Classification: **INTEGRATION_FIX required; not applied because the explicit stop gate was reached.**
Severity: high for future private ephemeral delivery. No real private user data was sent.

Reproduction (`SocialOutboundAuthorizationTests`):

1. Register and authorize an A.3 local handle.
2. Authenticate an A.1 ConnectionOutbound writer.
3. Block a synthetic socket while it sends a HOLD control frame.
4. Enqueue TEST_STATE only after checking `handle.canDeliver()`.
5. Invalidate the pair; assert `handle.canDeliver() == false`.
6. Release the held control frame.
7. Observe TEST_STATE transmitted even though it was still queued at invalidation.

Actual JUnit result: **1 executed, 1 failed** at the assertion that TEST_STATE must be absent.
Evidence: `Generated/S14A4/revocation-failure.xml`.
The fake socket is deliberate for deterministic transmission control. This failing case
is an in-process A.1+A.3 composition test, not a real two-process WebSocket leak test.

Root cause: `LocalSocialAuthorizationIndex.invalidate` revokes handles, while
`ConnectionOutbound` independently retains encoded pending messages. Its explicit
`invalidate(key)` operation is not connected to handle invalidation. A one-time
admission check cannot revoke a queued item. No automatic bridge or transmission-time
authorization guard exists in the tested composition. The two primitives preserve their
individual documented behavior; this finding identifies the missing integration contract,
not a demonstrated leak in an existing Presence feature (there is no such feature).

Recommended smallest correction to review: provide an explicit, race-safe connection
between authorization invalidation and ephemeral transmission, or a fail-closed guard at
the writer's transmission commit boundary. It must cover durable recovery and lease
expiration, queue invalidation, concurrent admission, and rejected/failed reauthorization.
Preserve Critical FIFO, Control fairness, bounded queues and the one-writer rule.
Do not introduce Presence just to close this gap.

The physically in-flight limitation is different: the HOLD frame was in flight;
TEST_STATE was not. The reproduction does not demand cancellation of transmitted bytes.
No claim is made that observing canDeliver=false alone guarantees an end-to-end
absence of unauthorized deliveries. The failed test demonstrates why it does not.

## Failure-domain matrix (observed only)

| Failure | Gameplay | Durable Social | Invalidation | Privacy safety | Recovery |
|---|---|---|---|---|---|
| Redis command path separately down | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN |
| B Pub/Sub down | NOT_RUN | Search 200 | durable Block revoke PASS | handle denied; delivery gate incomplete | reconnect observed |
| Redis fully down | NOT_RUN | tested A.2 matrix PASS | remote Block handle denied | queued-delivery safety FAIL separately | command circuit recovered |
| Firestore reader throws | NOT_RUN | NOT_RUN | remote handle remains denied | handle fail-closed PASS; transport gate incomplete | reader restored PASS |
| Backend A down | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN |
| Backend B down | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN |
| Both restart | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN |
| Dispatcher independently down | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN |
| Slow synthetic WebSocket | NOT_RUN | unchanged | handle revoked | queued TEST_STATE delivered: FAIL | not approved |
| Missed Pub/Sub event | NOT_RUN | Block 200 | durable recovery PASS | handle denied | 5062 ms scenario |
| Duplicate invalidation | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN |
| Out-of-order invalidation | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN |

## Counts, regressions and limitations

- HTTP scenario assertions: 31 PASS (11 baseline/fault, 9 outage including remote safety,
  10 Follow/Block race iterations, 1 concurrent acceptance).
- New focused JUnit composition test: 0 PASS / 1 FAIL.
- These are separate counts; no combined 'all tests PASS' claim.
- Recommended seeded combined chaos loop: NOT_RUN, stopped at correctness gate.
- Backend full regression suite, Emulator JUnit suite, Redis suite, transport full suite,
  Social Client suite, Unity static/EditMode/PlayMode/manual: NOT_RUN during A.4.
- Test Kotlin compilation: PASS. No production Kotlin sources changed.
- Earlier A.3 test counts are not reused as current A.4 results.
- Console errors: NOT_MEASURED (Unity not run).
- Worker leaks, reconnect storm, backlog drain, feed pagination, backend restart,
  operation/billing counters and maximum recovery window: NOT_MEASURED/NOT_RUN.
- POST_INVALIDATION_UNAUTHORIZED_DELIVERIES=1 synthetic frame in deterministic reproduction.
- Committed-mutation/outbox loss and revision-regression comprehensive detectors: NOT_RUN.
- No end-to-end privacy PASS is claimed despite passing handle-revocation checks.
- The normal test task will include the new failing regression until the integration
  contract is fixed. No commit/push is requested or performed.

## Retention and scope

SAFE_RETENTION_POLICY=DEFER_CLEANUP_UNTIL_PRODUCTION_HARDENING
RETENTION_CLEANUP_READY=NO
Retention cleanup is unchanged. No outbox/feed events deleted. Recovery-window and
watermark evidence is insufficient for a safe production deletion policy.
PRODUCTION_RELEASE_BLOCKED_BY_RETENTION_POLICY=YES
S1.4B is blocked by A4-01 independently of retention.
No Presence/ONLINE/OFFLINE/IN_MATCH, Party, Spectator, protocol or Unity UI added.
No gameplay/replay source or sequence semantics modified.

## Files and protected work

Intentional A.4 files (all new, untracked):
- `client/Validation/S1_4A_4_MULTI_INSTANCE_CHAOS_REPORT.md`
- `server/domino/src/test/kotlin/com/teamfho/domino/social/SocialChaosValidationServer.kt`
- `server/domino/src/test/kotlin/com/teamfho/domino/social/SocialOutboundAuthorizationTests.kt`

PRODUCTION_FILES_CHANGED=0
TEST_FILES_CHANGED=2
REPORT_FILES_CHANGED=1
PREEXISTING_USER_FILES=97
PREEXISTING_USER_FILES_PRESERVED=YES (all SHA256 values checked)
UNEXPECTED_FILES=0
UNRELATED_USER_FILES_MODIFIED_BY_S1_4A_4=NO

Exact pre-existing inventory and hashes: `Generated/S14A4/baseline.json`.
Exact generated artifact inventory: `Generated/S14A4/classification.json`.
Protected AdsSettings, ApiSettings, Localization Settings, Firebase/Android settings,
resolver work, Google Play preparation and previous reports remain untouched.
Temporary startup wiring fixes were confined to the new harness: explicitly supply the
Emulator Player repository and avoid duplicate controller registration. No production fix.
Generated logs redact Spring's automatically generated unused test password.

## Final checkpoint candidate

```text
BRANCH=main
SOURCE_SHA_BEFORE=324ae674186cc8ccf465c4f6779e31f826adeb9a
SOURCE_SHA_AFTER=324ae674186cc8ccf465c4f6779e31f826adeb9a
S1_4A_4_IMPLEMENTED=PARTIAL
S1_4A_4_SUCCESS=NO
S1_4A_4_READY_FOR_CHECKPOINT=NO
TWO_REAL_BACKEND_PROCESSES=YES
CROSS_INSTANCE_BLOCK=PASS
CROSS_INSTANCE_PRIVACY=PASS
CROSS_INSTANCE_UNFRIEND=PASS
CROSS_INSTANCE_FRIEND_ACCEPT=PASS
MISSED_PUBSUB_EVENT_SAFETY=PASS_AT_HANDLE_LEVEL_ONLY
PUBSUB_DISCONNECT_RECOVERY=PASS_AT_HANDLE_LEVEL_ONLY
REDIS_FULL_OUTAGE=PARTIAL_MATRIX_PASS
BLOCK_WHILE_REDIS_DOWN=PASS_AT_HANDLE_LEVEL_ONLY
REDIS_RECOVERY=COMMAND_CIRCUIT_OBSERVED; FULL_GATE_NOT_RUN
INSTANCE_RESTART_RECOVERY=NOT_RUN
DISPATCHER_PAUSE_RECOVERY=NOT_RUN
DISPATCHER_FAILURE_RECOVERY=NOT_RUN
DUPLICATE_EVENT_SAFETY=NOT_RUN
OUT_OF_ORDER_EVENT_SAFETY=NOT_RUN
FIRESTORE_REAUTH_FAILURE_FAIL_CLOSED=PASS_AT_HANDLE_LEVEL_ONLY
SLOW_SOCKET_INVALIDATION=FAIL
QUEUE_SATURATION_INVALIDATION=NOT_RUN
CONCURRENT_ACCEPT_REDIS_DOWN=PASS
FOLLOW_BLOCK_CROSS_INSTANCE=10_PASS
POST_INVALIDATION_UNAUTHORIZED_DELIVERIES=1_SYNTHETIC_FRAME
COMMITTED_MUTATIONS_WITHOUT_DURABLE_INVALIDATION=NOT_MEASURED
REVISION_REGRESSIONS=NOT_MEASURED
PUBSUB_SUBSCRIBER_LEAKS=NOT_MEASURED
OUTBOX_WORKER_LEAKS=NOT_MEASURED
WRITER_TASK_LEAKS=NOT_MEASURED
STALE_LOCAL_AUTH_HANDLES=NOT_MEASURED
REDIS_CONNECTION_LEAK_OBSERVED=NOT_MEASURED
FEED_WORKER_LEAK_OBSERVED=NOT_MEASURED
BUSY_LOOP_OBSERVED=NOT_MEASURED
FIRESTORE_AUTH_READS_PER_HEARTBEAT=NOT_MEASURED; NO_SOURCE_CHANGE
FIRESTORE_HEARTBEAT_WRITES=NOT_MEASURED; NO_SOURCE_CHANGE
SOCIAL_INVALIDATION_POLLING_PER_HEARTBEAT=NOT_MEASURED; NO_SOURCE_CHANGE
SOCIAL_READS_PER_GAMEPLAY_COMMAND=NOT_MEASURED; NO_SOURCE_CHANGE
CHAOS_RUNS=NOT_RUN_COMBINED_LOOP
CHAOS_PASS=NOT_RUN_COMBINED_LOOP
CHAOS_FAIL=NOT_RUN_COMBINED_LOOP
S1_4A_4_FOCUSED_TESTS=1_FAIL
MULTI_PROCESS_TESTS=31_HTTP_SCENARIO_ASSERTIONS_PASS
BACKEND_TESTS=FULL_SUITE_NOT_RUN
FIRESTORE_EMULATOR_TESTS=JUNIT_SUITE_NOT_RUN; HTTP_SCENARIOS_USED_EMULATOR
REDIS_TESTS=SUITE_NOT_RUN; LOCAL_OUTAGE_SCENARIOS_EXECUTED
TRANSPORT_TESTS=1_COMPOSITION_TEST_FAIL; FULL_SUITE_NOT_RUN
SOCIAL_CLIENT_TESTS=NOT_RUN
UNITY_STATIC_COMPILATION=NOT_RUN
UNITY_EDITMODE_TESTS=NOT_RUN
UNITY_PLAYMODE_TESTS=NOT_RUN
UNITY_MANUAL_CHECKS=NOT_RUN
CONSOLE_ERRORS=NOT_MEASURED
S1_4A_1_REGRESSION=FULL_SUITE_NOT_RUN
S1_4A_2_REGRESSION=PARTIAL_HTTP_MATRIX_PASS
S1_4A_3_REGRESSION=PARTIAL_HTTP_SCENARIOS_PASS
S1_1_REGRESSION=FULL_SUITE_NOT_RUN
S1_2_REGRESSION=FULL_SUITE_NOT_RUN
S1_3_REGRESSION=FULL_SUITE_NOT_RUN
HEARTBEAT_REGRESSION=NOT_RUN
RECONNECT_REGRESSION=NOT_RUN
MATCH_WEBSOCKET_REGRESSION=NOT_RUN
I4_REPLAY_REGRESSION=NOT_RUN
MATCH_EVENT_SEQUENCE_CHANGED=NO
REPLAY_SEQUENCE_CHANGED=NO
DEFECTS_FOUND=1_INTEGRATION_GAP
PRODUCTION_FILES_CHANGED=0
TEST_FILES_CHANGED=2
REPORT_FILES_CHANGED=1
SAFE_RETENTION_POLICY=DEFER_CLEANUP_UNTIL_PRODUCTION_HARDENING
RETENTION_CLEANUP_READY=NO
RETENTION_CLEANUP_DEFERRED_REASON=INSUFFICIENT_RECOVERY_WINDOW_EVIDENCE
REAL_FIRESTORE_CALLS=0
REAL_REDIS_PRODUCTION_CALLS=0
BOT_SWARM_STARTED=NO
PRESENCE_IMPLEMENTED=NO
ONLINE_IMPLEMENTED=NO
OFFLINE_IMPLEMENTED=NO
IN_MATCH_IMPLEMENTED=NO
S1_4A_4_FILES_MODIFIED=3_NEW_FILES
PREEXISTING_USER_FILES=97
GENERATED_BY_VALIDATION_FILES=SEE_CLASSIFICATION_JSON
UNEXPECTED_FILES=0
PREEXISTING_USER_FILES_PRESERVED=YES
UNRELATED_USER_FILES_MODIFIED_BY_S1_4A_4=NO
VALIDATION_BACKEND_CLEANUP=PASS
COMMIT=NONE
PUSH=NONE
DEPLOY=NONE
S1_4A_COMPLETE=NO
S1_4B_UNBLOCKED=NO
S1_4B_STARTED=NO
S2_STARTED=NO
NEXT=A4-01_REVIEW_AND_CORRECTION_BEFORE_RESUMING_A4
```

</details>

## S1.4A.4 CHECKPOINT CANDIDATE

```text
BRANCH=main
SOURCE_SHA_BEFORE=324ae674186cc8ccf465c4f6779e31f826adeb9a
SOURCE_SHA_AFTER=324ae674186cc8ccf465c4f6779e31f826adeb9a
A4_01_SUCCESS=YES
S1_4A_4_SUCCESS=YES_WITH_DOCUMENTED_VALIDATION_LIMITS
S1_4A_4_READY_FOR_CHECKPOINT=YES_WITH_DOCUMENTED_VALIDATION_LIMITS
TWO_REAL_BACKEND_PROCESSES=YES
POST_INVALIDATION_UNAUTHORIZED_DELIVERIES=0
BACKEND_TESTS=617 PASS / 15 SKIPPED / 0 FAILED (632 discovered)
BACKEND_SKIPPED=15
BACKEND_FAILED=0
FIRESTORE_EMULATOR_TESTS=40 PASS / 0 SKIPPED / 0 FAILED
FIRESTORE_EMULATOR_FAILED=0
SOCIAL_CLIENT_TESTS=78 PASS
UNITY_STATIC_COMPILATION=PASS; existing warnings only
CONSOLE_ERRORS=NOT_MEASURED
PREEXISTING_USER_FILES_PRESERVED=YES
UNEXPECTED_FILES=0
REAL_FIRESTORE_CALLS=0
REAL_REDIS_PRODUCTION_CALLS=0
BOT_SWARM_STARTED=NO
S1_4A_COMPLETE=YES
S1_4B_UNBLOCKED=YES
S1_4B_STARTED=NO
S2_STARTED=NO
COMMIT=NONE
PUSH=NONE
DEPLOY=NONE
NEXT=S1.4A.4 REVIEW/CHECKPOINT
```
