# CUBAN DOMINO CLUB — S1.2 FRIENDS REPORT

Implementation for review. No commit, push, production service access or deployment.

```text
BRANCH=main
SOURCE_SHA_BEFORE=8934cbf000652b2a5b659ac92096ebe2650f4d86
SOURCE_SHA_AFTER=8934cbf000652b2a5b659ac92096ebe2650f4d86
FRIEND_REQUESTS_IMPLEMENTED=YES
SEND_REQUEST=PASS
DUPLICATE_SEND=PASS
CROSS_REQUEST=PASS_EXPLICIT_ACCEPT_NO_AUTO_ACCEPT
ACCEPT=PASS
DECLINE=PASS
CANCEL=PASS
REQUEST_GENERATION_PROTECTION=PASS
REQUEST_PAGINATION=PASS
FRIENDSHIP_IMPLEMENTED=YES
FRIENDSHIP_SYMMETRIC=PASS
CANONICAL_PAIR=SHA256_LENGTH_PREFIXED_SORTED_UID_PAIR
FRIEND_PROJECTIONS=ATOMIC_BOTH_USERS
FRIEND_LIST=OWNER_ONLY
UNFRIEND=PASS_IDEMPOTENT
FREE_FRIENDS_MAX=5
PREMIUM_FRIENDS_MAX=100
TRIAL_FRIENDS_MAX=100
FREE_LIMIT_5=PASS
PREMIUM_LIMIT_100=PASS
TRIAL_LIMIT_100=PASS
DOWNGRADE_DELETES_FRIENDS=NO
DOWNGRADE_PRESERVES_FRIENDS=PASS
ATOMIC_LIMIT_CONCURRENCY=PASS
BOTH_USERS_LIMIT_CHECKED=YES
ENTITLEMENT_ATOMICITY_DESIGN=POLICY_AND_BOTH_GRANT_PROJECTIONS_READ_IN_FRIENDSHIP_TRANSACTION
BLOCK_PRECEDENCE=PASS
BLOCK_PENDING_REQUEST=PASS
BLOCK_EXISTING_FRIEND=PASS
BLOCK_REQUIRES_ENTITLEMENTS=NO
UNBLOCK_RESTORES_FRIENDSHIP=NO
ACCEPT_BLOCK_RACE=PASS
COUNTERS_AFTER_BLOCK=CONSISTENT_NO_NEGATIVES
UID_EXPOSED_PUBLICLY=NO
OTHER_USER_PLAN_EXPOSED=NO
OTHER_USER_FRIEND_COUNT_EXPOSED=NO
BLOCK_PRIVACY_LEAK=NO
OTHER_USER_FRIEND_LIST_ACCESS=DENIED
OTHER_USER_REQUEST_LIST_ACCESS=DENIED
DIRECT_FIRESTORE_SOCIAL_ACCESS=DENIED_EMULATOR_VERIFIED
TEST_ACCOUNTS_SOCIAL_REQUESTABLE=NO
```

## Architecture and atomicity

`social/Friendships.kt` implements typed requests, canonical friendship, counters, pair generation and the shared service. `FriendshipRepository` exposes an atomic callback, point reads and bounded ordered pages. `FirestoreFriendships` adapts Firestore; the test-only `MemoryFriendships` uses rollback-on-failure and enforces reads-before-writes. Controllers contain routing/authentication, not transitions.

The existing `EntitlementService` has a 30-second grant cache and `SubscriptionPolicyService` has a 60-second policy cache. These caches are intentionally **not** used for friendship formation. Instead, the transaction reads `systemConfig/subscriptionPolicy` and each participant's `players/{uid}/entitlementState/current`, then calls the existing pure `EntitlementResolver` with server time for that transaction attempt. No billing/provider-specific rule is introduced into Social. Missing policy uses the same injected `SubscriptionPolicy` fallback as P0.1; storage errors abort instead of granting capacity. Missing entitlement state resolves FREE, while read failure is unavailable.

Acceptance reads request, pair generation, canonical friendship, both counters, both active/test-account states, both block directions, recipient request privacy, policy, both grant projections and both public identity mappings before writing. It writes seven documents: canonical friendship, both projections, both counters, terminal request and cleared pair pointer. Transaction conflicts on counters prevent two acceptances from consuming the final slot. Grant/policy changes participate in the same read set; retries re-evaluate server time. Capacity is evaluated at the successful transaction attempt's authorization boundary, not from client time or cached Premium flags. Natural expiration does not delete friendships.

Both limits are checked on acceptance, including the sender whose capacity may have changed since sending. Own limit returns `FRIEND_LIMIT_REACHED`; another player's capacity returns generic `SOCIAL_ACTION_NOT_ALLOWED`, without their count or plan. Failed acceptance leaves PENDING intact. Own summary reports unavailable rather than interpreting an entitlement failure as a valid FREE plan. Friends/requests reads, decline, cancel, unfriend and block do not resolve entitlements.

## Pair/request semantics

- Pair IDs hash a sorted pair of length-prefixed UTF-8 UIDs; concatenation cannot confuse `a+bc` with `ab+c`. Pair IDs remain internal.
- Request IDs are server-generated UUIDs; the pair stores an increasing generation and a single pending request pointer.
- Same-direction retry returns the existing request. Reverse-direction send returns that incoming request; it never accepts automatically.
- Accept/decline are recipient-only; cancel is sender-only. Terminal retries are stable. Commands for an older generation fail without touching the current generation.
- Removing a friendship deletes the canonical document and both projections, decrementing both counts once. Repeating it is a no-op.
- S1.1 block now calls the same removal transition inside its existing transaction, before block writes. A pending request becomes CANCELED and its counters are decremented. An accepted friendship and both projections are removed. Whichever transaction wins the initial accept/block race, the final successful block leaves no friendship. Unblock only deletes block references.
- Request expiration is deliberately deferred: PENDING requests do not automatically expire. There is no TTL cleanup or background polling. A seven-day pair cooldown after DECLINED is implemented using server time. Cancel does not impose this cooldown.

## Durable storage

| Path | Purpose / fields |
|---|---|
| `socialPairs/{pairId}` | lowerUid, upperUid, generation, pendingRequestId, declinedUntil |
| `friendRequests/{requestId}` | requestId, pairId, generation, senderUid, recipientUid, status, createdAt, resolvedAt, sortTime |
| `friendships/{pairId}` | canonical UIDs, createdAt, sourceRequestId, generation |
| `players/{uid}/friends/{otherUid}` | pairId, friendPublicPlayerId, friendsSince, sortTime; no copied displayName |
| `players/{uid}/socialCounters/current` | friendCount, pendingIncomingCount, pendingOutgoingCount |
| `players/{uid}/socialRate/friendRequests` | bounded rolling sentAt timestamps, maximum 20 |

All mutations are server-authoritative. Counter documents are lazy defaults, not a migration. No existing users are scanned. Lists resolve current public profile fields, so alias changes do not fan out across friendships or requests. Eligibility and both block directions are filtered before delivering list profiles.

Requests query the authenticated sender/recipient plus PENDING, ordered by epoch-millisecond `sortTime DESC` and document ID DESC. Friends use the owner's collection with the same ordering. Time ordering does not depend on variable-length ISO fractional seconds. Default page 20, maximum 50, one look-ahead row; encrypted S1.1 cursors bind to UID and list/direction. No offsets or full-graph bootstrap. Two composite indexes are added to `server/domino/firestore.indexes.json` for the actual request queries. Existing deny-all client rules already cover the new paths and were tested; no rules change or deployment.

## API and security

| Method | Endpoint | Authority |
|---|---|---|
| POST | `/api/v1/players/{publicPlayerId}/friend-request` | authenticated sender |
| GET | `/api/v1/player/friend-requests?direction=INCOMING\|OUTGOING&cursor=&limit=` | own lists only |
| POST | `/api/v1/friend-requests/{requestId}/accept` | recipient |
| POST | `/api/v1/friend-requests/{requestId}/decline` | recipient |
| DELETE | `/api/v1/friend-requests/{requestId}` | sender cancellation |
| GET | `/api/v1/player/friends?cursor=&limit=` | own list only |
| DELETE | `/api/v1/player/friends/{publicPlayerId}` | either participant, own relationship |

S1.1 profile now includes `relationship` with friendship and incoming/outgoing request IDs. Own social summary adds `friends` containing own count, effective limit, canAddFriend, incoming count and availability. Public profile contains neither another user's limit/count/plan nor Firebase UID. Arbitrary sourceUid body/query fields do not select the actor. Controllers derive UID from the verified Firebase principal. No endpoint permits enumerating another player's lists. Removing a nonexistent own relationship returns success without affecting another pair.

New sends enforce a transactional rolling five-per-minute and twenty-per-24-hours limit and twenty outgoing pending requests, independent of Premium. Duplicate/cross sends do not consume another durable send allowance. Existing S1.1 Redis token buckets also bound HTTP actions; accepted operations use one script per rate-limited request. Block continues to bypass Redis/Entitlements. No social presence, heartbeat, FCM or generalized event platform is added.

## Unity

The existing Social screen adds Friends and Requests with incoming/outgoing selection, profile relationship actions, removal confirmation, count/limit, loading/error/retry and existing paging. All mutations refresh affected views/own summary without restarting the application. The UI prevents duplicate in-flight actions; backend idempotency remains authoritative. Account changes cancel/discard in-flight results and close/clear the social view.

FREE over-limit users retain their list and removal/block actions, see the `20 / 5` explanation and cannot use Add Friend. Incoming acceptance is still authoritatively rejected when capacity is unavailable. Generic other-player failure never displays their plan/count. Dates are presentation-only local dates using the selected EN/ES culture. Official localization tables are imported in the isolated project; only the three table files are copied back. Protected `Localization Settings.asset` is untouched. Existing native accessibility labels are reused.

Real Editor validation uses `Generated/G1Project` and the test-classpath-only loopback server at 127.0.0.1:18089. Two distinct local authenticated identities exercise send, explicit accept, both friends lists, remove, cancel, reverse request, decline, block existing friendship and block pending request. A separate controlled account with twenty coherent friendship projections exercises the downgrade display. Tokens are local test verifier strings, not Firebase credentials. Production code has no test-auth bypass.

## Tests and evidence

```text
BACKEND_TESTS=519 PASS / 14 SKIPPED; TOTAL=533
S1_2_BACKEND_TESTS=14 PASS (13 service tests + 1 HTTP lifecycle test)
FIRESTORE_EMULATOR_TESTS=21 PASS
S1_2_EMULATOR_TESTS=4 PASS
SOCIAL_CLIENT_TESTS=47 PASS
UNITY_EDITMODE_TESTS=92 LOCALIZATION CHECKS PASS
UNITY_PLAYMODE_TESTS=221 CHECKS PASS
UNITY_COMPILATION=PASS
UNITY_MANUAL_CHECKS=SCREENSHOTS_REVIEWED; INTERACTIONS_AUTOMATED_IN_REAL_EDITOR
CONSOLE_ERRORS=0_IN_FINAL_VALIDATION
ANDROID_PHYSICAL_TEST=NOT_RUN
NATIVE_SCREEN_READER_PHYSICAL_TEST=NOT_RUN
```

Evidence: backend `build/test-results/test`, `build/test-results/emulatorTest`; Unity `client/Validation/Generated/S12/unity-result.txt`, `editmode-result.txt`, logs and screenshots. Validation does not claim human-operated clicks or a physical Android test.

Concurrency tests use barriers, not sleeps: simultaneous cross send; duplicate accept; accept/decline and accept/cancel; last FREE slot 4→5, last PREMIUM slot 99→100; accept/block; send/block; unfriend/block. Unit tests cover trial 99→100, expiry downgrade, both sides' capacity, policy/revocation changes, stale generations, terminal retries, test-account filtering, private DTOs, cursor identity/direction, rate and pending limits. Emulator tests independently exercise final-slot transactions, accept/block final graph invariants, lifecycle and costs. HTTP tests exercise two verified principals, reject sender acceptance/recipient cancellation, and prove forged sourceUid cannot change authority. Emulator rules tests verify authenticated direct read/write denial for pairs, requests, friendships, projections and counters.

Existing regressions run: Guest Auth 43 checks; Player Foundation/Realtime 469; I4 Replay 1595 checks over 62 matches and 31061 events; I3/M5 75; Monetization 26; F0 isolation 16. Backend suite covers Profile, Entitlements, Social S1.1, I4, M5, matchmaking, Wallet/Monetization and F0/F0.1. Fourteen optional real-Redis tests remain skipped, not claimed as executed: PartnersMatchmaking 3, RedisMatchmaking 8, PartnersOnline 1, RedisTurnIndex 1, RedisPresence 1. No swarm load run.

## Measured operation cost

Uncontended emulator document operations measured by `SocialMeasurements`; not billing guarantees. These are service/repository costs. HTTP identity readiness adds its existing three reads for an initialized actor; profile/summary have their existing additional work.

| Operation | Reads | Writes |
|---|---:|---:|
| Send request | 15 | 5 |
| Duplicate send | 11 | 0 |
| Accept | 17 | 7 |
| Decline | 5 | 4 |
| Cancel | 5 | 4 |
| Unfriend | 5 | 5 |
| Block existing friend | 6 | 7 |
| Friends page of 20 | 120 | 0 |
| Incoming requests page of 20 | 140 | 0 |
| Outgoing requests page of 20 | 140 | 0 |

Block measurement starts with a resolved target; S1.1 controller/target-resolution costs are additional. Query reads include returned documents. Retry conflicts can increase actual operations. Friendship formation writes seven documents, well below transaction limits. Profile resolution is bounded per page but intentionally not optimized in S1.2. S1.1 name-search measurement remains 141 reads for its measured 20-result fixture; future optimization remains recommended.

```text
REAL_FIRESTORE_CALLS=0
FIRESTORE_HEARTBEAT_WRITES=0
SOCIAL_READS_PER_GAMEPLAY_COMMAND=0
FRIENDSHIP_BACKGROUND_POLLING=0
REQUEST_BACKGROUND_POLLING=0
NAME_SEARCH_20_READS=141
SEARCH_COST_FUTURE_OPTIMIZATION=YES
```

## Limitations and scope

No request auto-expiration; seven-day decline cooldown is implemented. No Follow, social Presence, Party, friend match invitations, spectator, chat, FCM or reporting workflow. No billing/provider changes. No real Firebase, physical-device or native screen-reader validation. No production index/rules deployment. The controlled downgrade fixture deliberately seeds retained friendships; backend expiration behavior is separately covered with server-clock unit tests. Cached policy/grant resolution for other P0.1 features remains unchanged; only friendship formation uses transactional reads.

```text
FOLLOW_IMPLEMENTED=NO
SOCIAL_PRESENCE_IMPLEMENTED=NO
PARTY_IMPLEMENTED=NO
FRIEND_GAME_INVITATIONS_IMPLEMENTED=NO
CHAT_IMPLEMENTED=NO
SPECTATOR_IMPLEMENTED=NO
REPORT_PLAYER_IMPLEMENTED=NO
BOT_SWARM_STARTED=NO
FIRESTORE_RULES_DEPLOYED=NO
FIRESTORE_INDEXES_DEPLOYED=NO
BACKEND_DEPLOYED=NO
UNITY_DEPLOYED=NO
GOOGLE_PLAY_UPLOAD=NO
S1_3_STARTED=NO
COMMIT=NONE
PUSH=NONE
DEPLOY=NONE
```

## Final changed-file classification

All 96 initial dirty paths were hashed before implementation and rechecked unchanged. The following exact Git inventory includes untracked files. No staging occurred. New or changed S1.2 files were individually reviewed by their implementation role; classification is not based solely on a broad directory match.

| Path (repository-relative) | Classification |
|---|---|
| client/DominoGame/Assets/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0/firebase-app-unity-13.16.0.pom | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0/firebase-auth-unity-13.16.0.pom | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0/firebase-firestore-unity-13.16.0.pom | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/Plugins/Android/GoogleMobileAdsPlugin.androidlib/AndroidManifest.xml | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/StreamingAssets/google-services-desktop.json | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/StreamingAssets/google-services-desktop.json.meta | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/_Domino/Editor/Localization/Translations.json | S1_2_INTENTIONAL |
| client/DominoGame/Assets/_Domino/Localization/Localization Settings.asset | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/_Domino/Localization/Tables/Domino UI Shared Data.asset | S1_2_INTENTIONAL |
| client/DominoGame/Assets/_Domino/Localization/Tables/Domino UI_en.asset | S1_2_INTENTIONAL |
| client/DominoGame/Assets/_Domino/Localization/Tables/Domino UI_es.asset | S1_2_INTENTIONAL |
| client/DominoGame/Assets/_Domino/Resources/AdsSettings.asset | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/_Domino/Resources/ApiSettings.asset | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/_Domino/Scripts/Social/Editor/SocialVisualValidation.cs | S1_2_INTENTIONAL |
| client/DominoGame/Assets/_Domino/Scripts/Social/SocialClient.cs | S1_2_INTENTIONAL |
| client/DominoGame/Assets/_Domino/Scripts/Social/SocialView.cs | S1_2_INTENTIONAL |
| client/DominoGame/Assets/google-services.json | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/ProjectSettings/GvhProjectSettings.xml | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/ProjectSettings/ProjectSettings.asset | PRE_EXISTING_USER_CHANGE |
| client/Validation/SocialClientTests.cs | S1_2_INTENTIONAL |
| server/domino/firestore.indexes.json | S1_2_INTENTIONAL |
| server/domino/src/main/kotlin/com/teamfho/domino/social/FirestoreSocialRepository.kt | S1_2_INTENTIONAL |
| server/domino/src/main/kotlin/com/teamfho/domino/social/SocialController.kt | S1_2_INTENTIONAL |
| server/domino/src/test/kotlin/com/teamfho/domino/social/SocialHttpTests.kt | S1_2_INTENTIONAL |
| server/domino/src/test/kotlin/com/teamfho/domino/social/SocialRulesEmulatorTests.kt | S1_2_INTENTIONAL |
| server/domino/src/test/kotlin/com/teamfho/domino/social/SocialTests.kt | S1_2_INTENTIONAL |
| server/domino/src/test/kotlin/com/teamfho/domino/social/SocialValidationServer.kt | S1_2_INTENTIONAL |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/query/client-agp/cache-v2 | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/query/client-agp/cmakeFiles-v1 | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/query/client-agp/codemodel-v2 | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/cache-v2-2c0909d0b4389f2443c3.json | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/cmakeFiles-v1-2afea77556dece6ed3b6.json | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/codemodel-v2-56ef99f20c5d90a856eb.json | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/directory-.-RelWithDebInfo-d0094a50bb2071803777.json | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/directory-FramePacing-RelWithDebInfo-7f9c8865fd027a154c90.json | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/index-2026-09-20T07-54-05-0123.json | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/target-swappywrapper-RelWithDebInfo-de42165ac0b744ec5a6b.json | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.ninja_deps | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.ninja_log | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeCache.txt | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeCCompiler.cmake | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeCXXCompiler.cmake | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeDetermineCompilerABI_C.bin | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeDetermineCompilerABI_CXX.bin | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeSystem.cmake | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CompilerIdC/CMakeCCompilerId.c | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CompilerIdC/CMakeCCompilerId.o | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CompilerIdCXX/CMakeCXXCompilerId.cpp | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CompilerIdCXX/CMakeCXXCompilerId.o | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/TargetDirectories.txt | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/cmake.check_cache | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/rules.ninja | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/FramePacing/CMakeFiles/swappywrapper.dir/UnitySwappyWrapper.cpp.o | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/FramePacing/cmake_install.cmake | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/additional_project_files.txt | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/android_gradle_build.json | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/android_gradle_build_mini.json | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/build.ninja | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/build_file_index.txt | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/cmake_install.cmake | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/compile_commands.json | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/compile_commands.json.bin | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/configure_fingerprint.bin | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/metadata_generation_command.txt | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/prefab_config.json | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/symbol_folder_index.txt | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/hash_key.txt | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/prefab/arm64-v8a/prefab/lib/aarch64-linux-android/cmake/games-frame-pacing/games-frame-pacingConfig.cmake | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/prefab/arm64-v8a/prefab/lib/aarch64-linux-android/cmake/games-frame-pacing/games-frame-pacingConfigVersion.cmake | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/.utmp/tools/release/arm64-v8a/compile_commands.json | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/AddressableAssetsData/Android.meta | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/AddressableAssetsData/Android/addressables_content_state.bin | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/AddressableAssetsData/Android/addressables_content_state.bin.meta | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/AddressableAssetsData/ProfileDataSourceSettings.asset | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/AddressableAssetsData/ProfileDataSourceSettings.asset.meta | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/GeneratedLocalRepo.meta | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase.meta | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository.meta | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com.meta | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google.meta | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase.meta | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity.meta | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0.meta | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0/firebase-app-unity-13.16.0.aar | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0/firebase-app-unity-13.16.0.aar.meta | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0/firebase-app-unity-13.16.0.pom | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0/firebase-app-unity-13.16.0.pom.meta | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity.meta | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0.meta | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0/firebase-auth-unity-13.16.0.aar | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0/firebase-auth-unity-13.16.0.aar.meta | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0/firebase-auth-unity-13.16.0.pom | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0/firebase-auth-unity-13.16.0.pom.meta | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity.meta | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0.meta | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0/firebase-firestore-unity-13.16.0.aar | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0/firebase-firestore-unity-13.16.0.aar.meta | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0/firebase-firestore-unity-13.16.0.pom | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0/firebase-firestore-unity-13.16.0.pom.meta | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/Plugins/Android/FirebaseApp.androidlib.meta | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/Plugins/Android/FirebaseApp.androidlib/AndroidManifest.xml | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/Plugins/Android/FirebaseApp.androidlib/project.properties | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/Plugins/Android/FirebaseApp.androidlib/res/values/google-services.xml | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/Plugins/Android/gradleTemplate.properties | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/Plugins/Android/gradleTemplate.properties.meta | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/Plugins/Android/mainTemplate.gradle | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/Plugins/Android/mainTemplate.gradle.meta | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/Plugins/Android/settingsTemplate.gradle | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/Plugins/Android/settingsTemplate.gradle.meta | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/ProjectSettings/AndroidResolverDependencies.xml | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/ProjectSettings/ScriptableBuildPipeline.json | PRE_EXISTING_USER_CHANGE |
| client/Validation/S1_2_FRIENDS_REPORT.md | S1_2_INTENTIONAL |
| server/domino/src/main/kotlin/com/teamfho/domino/social/FirestoreFriendships.kt | S1_2_INTENTIONAL |
| server/domino/src/main/kotlin/com/teamfho/domino/social/FriendshipController.kt | S1_2_INTENTIONAL |
| server/domino/src/main/kotlin/com/teamfho/domino/social/Friendships.kt | S1_2_INTENTIONAL |
| server/domino/src/test/kotlin/com/teamfho/domino/social/FriendshipEmulatorTests.kt | S1_2_INTENTIONAL |
| server/domino/src/test/kotlin/com/teamfho/domino/social/FriendshipTests.kt | S1_2_INTENTIONAL |

```text
S1_2_FILES_MODIFIED=21
PREEXISTING_USER_FILES=96
UNEXPECTED_FILES=0
UNRELATED_USER_FILES_MODIFIED_BY_S1_2=NO
ANDROID_USER_WORK_PRESERVED=YES
ADS_SETTINGS_PRESERVED=YES
API_SETTINGS_PRESERVED=YES
LOCALIZATION_SETTINGS_PRESERVED=YES
GOOGLE_SERVICES_JSON_PRESERVED=YES
ANDROID_PROJECT_SETTINGS_PRESERVED=YES
ANDROID_RESOLVER_WORK_PRESERVED=YES
GOOGLE_PLAY_PREPARATION_PRESERVED=YES
```

GENERATED_BY_VALIDATION: ignored S12 screenshots/results/logs, the isolated G1Project copy, existing client test output folders and backend build/XML reports. Kept as validation evidence. No pre-existing generated Android/Firebase files were deleted. Temporary process-stop files are removed after the owned loopback validation process exits; the emulator runner cleans its own process. No production backend or user's original Unity process was stopped.
