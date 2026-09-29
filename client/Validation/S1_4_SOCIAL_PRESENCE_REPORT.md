# CUBAN DOMINO CLUB — S1.4 SOCIAL PRESENCE REPORT

Status: STOPPED_AT_SAFETY_REVIEW. S1.4 is not implemented or validated.

BRANCH=main
SOURCE_SHA_BEFORE=87b785b1276eb25397f80439a123bb3bb59e9a72
SOURCE_SHA_AFTER=87b785b1276eb25397f80439a123bb3bb59e9a72

## Scope and stop condition

The task section 107 requires stopping if shared WebSocket changes risk gameplay ordering/correctness without a safe design. Repository inspection identifies a shared synchronous delivery path with no application-level bounded output queue or priority isolation. This is a risk established by source inspection, not a measured latency failure. It is not a claim that safe implementation is impossible. No runtime implementation has been attempted.

A minimal addition to the current synchronous fanout is not sufficient to meet the requested backpressure, distributed revocation, and outage requirements. The prerequisite design below remains unimplemented and untested.

## Existing foundation and evidence

- `server/domino/src/main/kotlin/com/teamfho/domino/realtime/PresenceStore.kt`: RedisPresenceStore uses Redis TIME and sorted-set connection leases. Keys are `domino:v1:{presence}:players`, `domino:v1:{presence}:player:{sha256(uid)}`, and `domino:v1:{presence}:connection:{connectionId}`. Multiple connections are retained separately, while onlinePlayers counts unique users. connectionCount removes expired leases before counting. No Firestore heartbeat is present here.
- `realtime/RealtimeConfiguration.kt`: heartbeat interval 20 seconds, timeout 45 seconds, presence TTL 60 seconds. These should be reused without a second heartbeat.
- `realtime/RealtimeHandler.kt:52-70`: matchmaking notifications and committed Match updates synchronously acquire the connection monitor and send messages.
- `realtime/RealtimeHandler.kt:162-164`: send directly invokes WebSocketSession.sendMessage while assigning the shared outgoing sequence. There is no separate bounded social delivery path in this handler.
- `realtime/RealtimeHandler.kt:177-211`: cleanup, heartbeat timeout, and global activity delivery also use connection monitors. Social fanout must not add slow authorization or unconstrained socket work under these monitors.
- No Redis pub/sub listener or invalidation bus was found in current main Kotlin sources by searches for RedisMessage, convertAndSend, and addMessageListener. Instance-local subscription authorization alone would not satisfy distributed privacy revocation.
- `online/OnlineMatchService.kt:69-81`: ordinary mutations invoke committed only after repository acknowledgement; transport errors cannot roll back gameplay. This is a potential activity transition integration point, not a durable activity notification guarantee.
- `online/OnlineMatchService.kt:30`: createPaired returns repository.createPaired directly, without invoking committed. Listening only to committed would miss paired Match activation.
- `online/OnlineTurnWorker.kt`: reconnect queues affected UIDs; authoritative match state is read by the existing worker. This is a potential reconciliation point. Existing connection heartbeats must never renew match activity directly.
- `online/TurnDueIndex.kt` and `FirestoreTurnWorkFeed.kt`: existing Redis leader plus durable Firestore change feed recovers turn scheduling without idle query polling. These remain unchanged.
- `social/SocialModel.kt`: presenceVisibility and matchActivityVisibility already support EVERYONE/FRIENDS/NO_ONE and default to FRIENDS independently.
- `social/SocialController.kt:31-42`: RedisSocialRateLimiter exempts Block only. Redis failure rejects other actions with SOCIAL_SERVICE_UNAVAILABLE. Thus the requested availability of Friends/Requests/Follow/Privacy during Redis outage is not currently provided. Removing limits silently would weaken existing protections and is not an acceptable workaround.
- `client/DominoGame/Assets/_Domino/Scripts/Realtime/RealtimeConnectionService.cs`: one session sender serializes outgoing protocol sequence; unknown incoming message types fail the protocol. Social message dispatch must be added separately from Match dispatch and tested for sequence and reconnect compatibility.
- `client/DominoGame/Assets/_Domino/Scripts/Social/SocialView.cs`: social privacy exposes discovery, request and follow controls; presence and match activity controls/indicators are not implemented yet.

## Prerequisite design to resolve before integrating fanout

1. Define and test bounded per-connection delivery: preserve one transport sequence, coalesce replaceable social state, keep authorization revocation ahead of subsequent social state, and prevent slow clients from blocking gameplay/heartbeat workers. Define overload/close behavior. Do not renumber Match event sequences.
2. Define distributed authorization invalidation and its commit/crash boundary for Block, unfriend, privacy and account eligibility. A best-effort publication after commit alone is insufficient: a process can die between the durable commit and publication. Cover lost messages, Redis reconnect and in-flight delivery. Do not use Firestore polling or cache TTL alone for revocation.
3. Define the Redis outage strategy for existing social rate limits without silently failing open. Keep Block independent. Validate the requested durable-social availability separately from presence UNKNOWN.
4. Integrate activity with all authoritative creation/terminal/abandon/recovery paths. Bound marker validity and fence stale versions; connection heartbeats may renew ONLINE only. Use existing authoritative worker reads where applicable, not extra social reads for every tile.
5. Only then expose viewer-filtered public-ID snapshots/deltas through the existing WebSocket; bound subscriptions to 50, clean them on screen/account/session changes, and add Unity UI/localization.

These are engineering prerequisites, not requests to implement Party, Spectator, a second heartbeat, or a new presence authority.

## Validation and cost

Only filesystem/source/Git inspection was performed. No test suite, backend, Redis client, Unity process, Bot Swarm or external service was started by this task. Existing historical test results have not been relabeled as S1.4 evidence.

BACKEND_TESTS=NOT_RUN
S1_4_BACKEND_TESTS=NOT_RUN
FIRESTORE_EMULATOR_TESTS=NOT_RUN
REDIS_TESTS=NOT_RUN
SOCIAL_CLIENT_TESTS=NOT_RUN
UNITY_EDITMODE_TESTS=NOT_RUN
UNITY_PLAYMODE_TESTS=NOT_RUN
UNITY_MANUAL_CHECKS=NOT_RUN
CONSOLE_ERRORS=NOT_MEASURED
REGRESSIONS=NOT_RUN
OPERATION_MEASUREMENTS=NOT_RUN
REAL_FIRESTORE_CALLS=0
REAL_REDIS_PRODUCTION_CALLS=0
BOT_SWARM_STARTED=NO

ONLINE/OFFLINE/IN_MATCH/UNKNOWN, privacy revocation, fanout, reconnect, activity recovery, and Unity indicators are NOT_VALIDATED_FOR_S1_4. Physical Android and native screen-reader checks were not run. S1.1/S1.3 list read costs were not changed or remeasured.

## File classification and preserved work

Initial inventory: 96 modified/untracked user files, recorded with SHA256 in ignored `client/Validation/Generated/S14/baseline.json`. The exact inventory is appended below after final verification.

S1_4_INTENTIONAL: this report only.
GENERATED_BY_VALIDATION: ignored baseline.json, created by inspection (no validation suite ran).
PRE_EXISTING_USER_CHANGE: all 96 initial files, compared byte-for-byte by SHA256.
UNEXPECTED: none at final audit.

No protected asset, Android/Firebase generated work, Gradle template, runtime code, test or localization file was changed.

## Final status

PRESENCE_IMPLEMENTED=NO
PRESENCE_STORE=EXISTING_REDIS
HEARTBEAT_SECONDS=20
HEARTBEAT_TIMEOUT_SECONDS=45
TTL_SECONDS=60
SECOND_HEARTBEAT_CREATED=NO
S1_4_FILES_MODIFIED=client/Validation/S1_4_SOCIAL_PRESENCE_REPORT.md (new documentation)
PREEXISTING_USER_FILES=96
UNEXPECTED_FILES=0
UNRELATED_USER_FILES_MODIFIED_BY_S1_4=NO
S1_FUNCTIONALLY_COMPLETE=NO
S2_STARTED=NO
COMMIT=NONE
PUSH=NONE
DEPLOY=NONE

## Exact pre-existing file inventory (all hashes preserved)

| Classification | Path | Initial and final SHA256 |
|---|---|---|
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/query/client-agp/cache-v2` | `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/query/client-agp/cmakeFiles-v1` | `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/query/client-agp/codemodel-v2` | `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/cache-v2-2c0909d0b4389f2443c3.json` | `573d98b6a1258a6a496cf173e3f2f1b7bcb78d001b053da51a9155cc4570933d` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/cmakeFiles-v1-2afea77556dece6ed3b6.json` | `bdc29e905db05c56a04d770d30ae3590b3142e81f4b6825e3ca0e24cfec8bf61` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/codemodel-v2-56ef99f20c5d90a856eb.json` | `81f186923b2c3a3fdbb183753bebc1b9e65ca401cc8a467a9c457c907d5d76bb` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/directory-.-RelWithDebInfo-d0094a50bb2071803777.json` | `7934b0ebd3c2643b221b784bf8c5ad209d92cde7491e640a65ffe2138a34e8f2` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/directory-FramePacing-RelWithDebInfo-7f9c8865fd027a154c90.json` | `f86047a699d671386c6c16f512f01ba61a0656a54268e7961d4b3f131d89343b` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/index-2026-09-20T07-54-05-0123.json` | `8392b1c83739a8016324e9b568da2dd276d42b7ab32c742f390a0816e4c01878` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/target-swappywrapper-RelWithDebInfo-de42165ac0b744ec5a6b.json` | `31e0103820d5e47080e52cf86fada3d211eb2647ac3811d0a2531e599a45c24d` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.ninja_deps` | `c34d9364af15e776d7f06a6ba29887ff728409eaed91798e2164a24d462ecc87` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.ninja_log` | `3fdf983f08f780ca896735167a01da5d1aaf581591fd190d8714dcf562a81ff3` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeCache.txt` | `2c1394501766a73c42300e70d04b83cb4d9da6f2ad467ad48816f71ccd632859` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeCCompiler.cmake` | `8898e291c2cd225068e5530b9af8a02dcb7fca304512477cce0071a5a25910ac` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeCXXCompiler.cmake` | `74451168cd5893b9b6fa4d3bd2ee5b8c47669b41f7063834d942c68dfceba01f` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeDetermineCompilerABI_C.bin` | `cd589b710cd713772375bb43ea1cb3b9d5b3fc76c9d916fc0b62525ee26d8610` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeDetermineCompilerABI_CXX.bin` | `32901c7d99e031a1f14c4d27885ee9755887e41d2a694d77ad3652bfebf574fc` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeSystem.cmake` | `9b145fa847d33296f64a277d5936746bea088d6f6c84047cbe1017b4e7397774` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CompilerIdC/CMakeCCompilerId.c` | `a5bc5d659352bb93e28ce0ec49b3b97490ecc65afce5456f43d04795d0ad627f` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CompilerIdC/CMakeCCompilerId.o` | `416ac4c4c9de9af176f83167442a9495a9a39db95e9770ff5a31d54506bd78d1` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CompilerIdCXX/CMakeCXXCompilerId.cpp` | `6ee4286c9b42405ee114ccf00a8ae3093b7c2ff079cae1a946af11d96a5898ab` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CompilerIdCXX/CMakeCXXCompilerId.o` | `e9b3bdf87883ab4c2507b72bf072d7fe19b4a74bb69f970221e72bb41ee3f3f6` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/TargetDirectories.txt` | `70ea7a44133476454b52650a0dfe8d94a124c658faf672f46d2e8fd792148f38` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/cmake.check_cache` | `95079b44bcef481d601432c2fd3c654d7fe85b9c8386c355f2874502b47441d7` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/rules.ninja` | `6a79a98995c8de47fd6cc016f079d70fbe534e3f4bcbea1939981d6f20f1050b` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/FramePacing/CMakeFiles/swappywrapper.dir/UnitySwappyWrapper.cpp.o` | `7f6183535cab49afde55d622ad73a0cd9f3242f2ff5b5dcd6bd66e6f25c26a9d` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/FramePacing/cmake_install.cmake` | `b52599606d3a3808246fc2ddd1c8510f4f1894ea4bc6b7711e51b21350df6e0d` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/additional_project_files.txt` | `9ac8c981c6b052aac13081c73ead9b4b57597985b80187f8ac20128c4b946795` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/android_gradle_build.json` | `1fe9ed3cd176b891f01159f992a79d8079550276347097f7756af64ed9441aba` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/android_gradle_build_mini.json` | `7061373638f9f885bbc26b866406a3b7f3a0aa8707685bfd2833f5efc13e5f34` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/build.ninja` | `04f2856e1237fcb5fe845ff73c7df7cd738897abc57f5e55875b301f3c885252` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/build_file_index.txt` | `4196ca0d598ccb9c41b058677b3d234c506535804565f3d6b0b1a107475cd907` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/cmake_install.cmake` | `c7d674a5e634fc2c5317cd1010deea3286aa657ef38ed4cd4812d73b0561a170` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/compile_commands.json` | `01a89e3d7b893a727b804082fad93e0fa5bf224b688d02ade0ba2f394de6c48f` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/compile_commands.json.bin` | `2e709e30ac4df82492398a506c2c067366a8b810275751d5b8d6947c5076e959` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/configure_fingerprint.bin` | `224df562b6ecb9b1a633f294d22d0f2f0726c6656c764a8a98eefcbd58455d99` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/metadata_generation_command.txt` | `ea8117f5531bff270f589254ceefae822ba410209e5208fb96e807226dddc010` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/prefab_config.json` | `bb571977cce17618859997620c1f5c887321108f9fd1e07b5d3cba0280f75b1e` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/symbol_folder_index.txt` | `0587c83c642906aa69a97ae8aa10890723cc0a87ba6d672eb81b55f74c88b19d` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/hash_key.txt` | `32ab30f238bb6f823e8ea6e8c951dbc294a778315bbe48a491b041e0d49614ce` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/prefab/arm64-v8a/prefab/lib/aarch64-linux-android/cmake/games-frame-pacing/games-frame-pacingConfig.cmake` | `c52af1921a38615b47d05621d91644d1a7c524c09fd760fcb231a64eb4d3516b` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/prefab/arm64-v8a/prefab/lib/aarch64-linux-android/cmake/games-frame-pacing/games-frame-pacingConfigVersion.cmake` | `ac9df1467a1321c233c4978fd14887c9b46fa422f6fb13b4e0fa87b165ca952d` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/.utmp/tools/release/arm64-v8a/compile_commands.json` | `01a89e3d7b893a727b804082fad93e0fa5bf224b688d02ade0ba2f394de6c48f` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/Assets/AddressableAssetsData/Android.meta` | `e13381d436151de7f762ce30969d1b07cead20084554b92994f3bf70e0ce2cd8` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/Assets/AddressableAssetsData/Android/addressables_content_state.bin` | `c954894fe5afa590d348d75baf4b7278fdd109a000ac609b92affdbf9d5523e3` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/Assets/AddressableAssetsData/Android/addressables_content_state.bin.meta` | `39059f07d24f84a63a7be96f9f36ad419bf05f3281f95d188678366d1eb5c01f` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/Assets/AddressableAssetsData/ProfileDataSourceSettings.asset` | `9ef795dc81990733c43b2430133ce87a30a34d370b340503649e36ad2aa189cc` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/Assets/AddressableAssetsData/ProfileDataSourceSettings.asset.meta` | `515ae53452f6bed33f04cd594cc263ba8336e601d43a993a375f5ef8c4fbb9e5` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/Assets/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0/firebase-app-unity-13.16.0.pom` | `5c438c2959a44418f03290c7c3df2fc96a9e01c09e17803dcf853f1ea4928c3f` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/Assets/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0/firebase-auth-unity-13.16.0.pom` | `33c73a8d2e676773e2f76c25461c7b581535426d82d8f51067893d380e27bc95` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/Assets/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0/firebase-firestore-unity-13.16.0.pom` | `a636604a3d4ef9ec6d49df9d65b64e5a9f139610417a01b407513e2d422ca462` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/Assets/GeneratedLocalRepo.meta` | `6f2a8e6b8cefe1be555577dd68c0570eea8ea3720d3d0091685d8b51f0db0b98` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase.meta` | `922b2d825805123a6d53d918e60ce2cca767dac544d8457c23ecbf6e654fea71` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository.meta` | `7319953f96ddd1811a9e32c3adb368b807e228a2325387c2b4c1735ba7a8d41b` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com.meta` | `5dbcea7ccc6b31ab89c1bd857b069de0700ecce20df8ded9d27bd73d99c555e2` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google.meta` | `0cbdb8433a88d97cc4437e6dce2e58aa924ca25dc03a57d6e9917763f2e11e06` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase.meta` | `82e85deed480119c9c82098331fd9f37c300b727d36f582b5af00e5dcb3d0f0c` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity.meta` | `e7766a4feb479ad37e63f677884806663ed62d206a20a3dfa4ae88c70d393c5c` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0.meta` | `278fcbc5b4a7ea268272c1db0cfc3ff6450f58e3967ee46e9c629a45441a3fd1` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0/firebase-app-unity-13.16.0.aar` | `dde5250bff66e878dfbf6982e205146af9a7594f344b4e9e95036dd93dc8f139` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0/firebase-app-unity-13.16.0.aar.meta` | `5ba85c7b6bef4212859b04fbe96f1044da4a0149dcce28825e0738b223a426c5` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0/firebase-app-unity-13.16.0.pom` | `9257c828e4d88c4ebc513742cce149a5217f2e3cb2c8df54a470540c569a4702` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0/firebase-app-unity-13.16.0.pom.meta` | `c1b155327802a1bacc64f61ed21542d6e2feb56a52282521ee595d418851598a` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity.meta` | `36a4d300ea58db95632937d07a5439ce2e5cb5bc18fbb3e3797ac3ac101ad0df` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0.meta` | `2e29f7873a47dc9d470f0706bb2816d31a7360a63e8c00f4117fe0dc0bf164b3` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0/firebase-auth-unity-13.16.0.aar` | `ce2f9ce799b1986829758722be446d6aa8d006b5b29c94527d6e92df217a3469` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0/firebase-auth-unity-13.16.0.aar.meta` | `d3bd10fff0c75cf9780ffd2a5ac44bd4cb0bb921926d42c57712b7d33af253ab` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0/firebase-auth-unity-13.16.0.pom` | `7b9a0d4e46f38a68a0a2a89ebf5e0cc4611ecc538182bbf4fa0701cdc44f86ee` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0/firebase-auth-unity-13.16.0.pom.meta` | `c21ccc8a087a0cea4ea5a672380864fd3379c0d0c78a169978e47b5fa6491902` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity.meta` | `62439f3c0003d1f1c41a2c5d3cb712ea810df496b530b92ff5d9e91d07eb5c14` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0.meta` | `53eac06a770fcf4ef1325ea61d8c0a3dd31dda37ec3d454707f26ffd52cfe78e` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0/firebase-firestore-unity-13.16.0.aar` | `a6f6607450ded9e8407384c5efbb22b12a45f68192a555a139c19d064cd8e5cb` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0/firebase-firestore-unity-13.16.0.aar.meta` | `655be2247e715d8fe91a17d3273c67647949df39a9f1e5ad919426ea3df4ceb0` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0/firebase-firestore-unity-13.16.0.pom` | `382418f1b33348965d5da8d858ecffeea370f08a0ca517e4b509392e977d2ae7` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0/firebase-firestore-unity-13.16.0.pom.meta` | `0e76cb720c6ac47ba0dc43fbadabab239640138af204427ea26dc671a57f1075` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/Assets/Plugins/Android/FirebaseApp.androidlib.meta` | `e08238d6d680b5b9fc8fb033ee94bf740b38b968fd9eb02e265c7b06a4ba0835` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/Assets/Plugins/Android/FirebaseApp.androidlib/AndroidManifest.xml` | `40c97b6449f961d187d8dfdcefea0cd2b65495e1740f3bda43bc547133e599e6` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/Assets/Plugins/Android/FirebaseApp.androidlib/project.properties` | `e2be7ada90395ffe241c91268eeb958375f8a71f2f8f2d5bae0f524b2546840e` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/Assets/Plugins/Android/FirebaseApp.androidlib/res/values/google-services.xml` | `9527796cae3b9bcd0e242dcf8565e75be0caf652835c39adc1e7ed24354a1e3f` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/Assets/Plugins/Android/GoogleMobileAdsPlugin.androidlib/AndroidManifest.xml` | `49bd03053036e2e446513088ef04af44cc570975c7b425ec9d81b74bcd20e703` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/Assets/Plugins/Android/gradleTemplate.properties` | `70e1d2b9c796272c6c4304cbef3219981a2611df45ec18dcb4094a9963021c3b` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/Assets/Plugins/Android/gradleTemplate.properties.meta` | `ecf5b0055ed68139c426fba5082e1c10fe9326feb21c2f09f376ba4af2a719e3` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/Assets/Plugins/Android/mainTemplate.gradle` | `86b4a1006c191c9385916a69f003f5163d55f4b04f312c8aeb3945528543055c` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/Assets/Plugins/Android/mainTemplate.gradle.meta` | `9071d08934ee8a761b528be7fe40b09db39f73bc2cf70f9b5ab1021c695ba9fb` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/Assets/Plugins/Android/settingsTemplate.gradle` | `f686a7fb92e2827ad29d737f4ceb3b28c207d736237f2731f5a0e72466edec24` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/Assets/Plugins/Android/settingsTemplate.gradle.meta` | `8f5b19d7c74aa10ff4fb0d976acc129b101a1fdd112922c28fb2ca9f23783c62` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/Assets/StreamingAssets/google-services-desktop.json` | `cf80ae7733259133065967769b642aebca7dbab320e4510de115e95b0dc81279` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/Assets/StreamingAssets/google-services-desktop.json.meta` | `cc2689a16204610b03c4f7fc86bc0cf4d51ad0d520558a12d608d79b1d1c9bb1` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/Assets/_Domino/Localization/Localization Settings.asset` | `7f07a7c99327ec2a82a924e651e5a626995848f3014ea2af562aeaa11b4b65ae` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/Assets/_Domino/Resources/AdsSettings.asset` | `c377416e334727264806761518a4b5edf381a837a927a1c3f3d523a1acbdb5d6` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/Assets/_Domino/Resources/ApiSettings.asset` | `ac6b0ed6d30240bc531b235e43e8b9b55d3b48dcbcccf365adec62d3ee6bcb37` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/Assets/google-services.json` | `cf80ae7733259133065967769b642aebca7dbab320e4510de115e95b0dc81279` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/ProjectSettings/AndroidResolverDependencies.xml` | `c5073b91d619a2e5858a017383301ec9fbeccd861a5cc64f9110106e8d3f06c2` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/ProjectSettings/GvhProjectSettings.xml` | `7da074bfc89ecb0f3e1b5adacaeb4d8da5581863759a3e07713b12f175403970` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/ProjectSettings/ProjectSettings.asset` | `6f5e8d2b8dd94ff3270d2e090ab48e18c634c5410167ffa3eff8bf75d427a537` |
| PRE_EXISTING_USER_CHANGE | `client/DominoGame/ProjectSettings/ScriptableBuildPipeline.json` | `4532c5dc1530d7742b5625618bab6908074006d390a68f46a34183a11bbc7e0f` |
