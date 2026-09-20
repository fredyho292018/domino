# CUBAN DOMINO CLUB — S1.3 FOLLOW + PRIVACY REPORT

Implementation for review. No commit, push or deployment. Base and current HEAD: `54a658e3927483391bb6e9a0eecb05b9d0ee8ab8`, branch `main`.

## Domain and authority

`FollowService` in `server/domino/src/main/kotlin/com/teamfho/domino/social/Follows.kt` reuses S1.2 `FriendshipRepository` / `SocialTransaction`, `SocialCounters` and S1.1 public identities/privacy. It does not depend on Entitlements, billing, gameplay or matchmaking. Follow is free, directional and independent of requests/friendships. Mutual Follow consists of two independent edges.

The authenticated principal supplies the actor. A public target ID resolves server-side to its internal UID. Creation transaction reads current eligibility/test markers, both block directions, target Follow privacy, actor public identity and both counters. Existing edges are idempotent and survive a later NO_ONE setting. Unfollow removes only its direction and can remove an existing edge without requiring current target eligibility or entitlement availability. No commercial Follow limit exists.

## Durable model and transactions

| Document | Fields / meaning |
|---|---|
| `players/{sourceUid}/following/{targetUid}` | publicPlayerId of target, followedSince (server Instant), sortTime (epoch milliseconds) |
| `players/{targetUid}/followers/{sourceUid}` | publicPlayerId of source, same followedSince/sortTime |
| `players/{uid}/socialCounters/current` | existing friend/pending counts plus followerCount and followingCount |

The ordered pair of document paths is the directional identity; no extra canonical Follow document or socialPairs mutation is needed. Source following and target follower projections plus their counters commit together. Duplicate Follow/Unfollow writes zero documents. Reads precede writes; there is no external side effect inside the retryable callback. Shared counter transactions serialize different users following one target and one user following different targets. Legacy counter documents default the new fields to zero.

Existing Block invokes `FriendshipService.removeInTransaction`. It now reads both Follow directions before writing, combines friend/request and Follow counter deltas, and writes each changed counter once. Friendship removal, pending cancellation, both Follow directions and block/inverse-block records share the original Firestore transaction. Block does not depend on Follow rate limits or Entitlements. Unblock restores none of these relations. Unfriend/decline/cancel do not remove Follow.

Friendship and a pending request for the same pair cannot normally coexist. Emulator cost tests therefore cover friend+mutual-Follow and pending+mutual-Follow separately, rather than fabricate invalid production state.

No copied display name exists in Follow projections. Rename resolves at list-read time with no fan-out writes. Future account deletion can page owned following/followers projections, remove opposite projections and adjust surviving counterpart counters atomically per edge; no deletion job is implemented.

## Privacy

The existing `SocialPrivacySettings` enums remain authoritative. `PrivacyPatch` accepts a nonempty subset of the five approved fields plus a required revision; unknown fields, null/invalid enum values and invalid revisions are rejected. Missing fields are preserved. A stale revision fails; a no-op retains the revision. Firestore decoding now honors all previously persisted settings rather than returning defaults for unimplemented fields.

| Field | Supported values | Behavior |
|---|---|---|
| discoverableByName | boolean | Existing search behavior; only its change updates searchEligible projection |
| friendRequests | EVERYONE / NO_ONE | Existing S1.2 creation/accept checks consume the setting |
| follow | EVERYONE / NO_ONE | Default EVERYONE; NO_ONE prevents new edges without deleting existing followers |
| presenceVisibility | EVERYONE / FRIENDS / NO_ONE | Persisted with default FRIENDS; no presence delivery |
| matchActivityVisibility | EVERYONE / FRIENDS / NO_ONE | Persisted with default FRIENDS; no activity or Match IDs delivered |

Followers/Following lists and both counts are owner-only. Public profile relationship exposes only the viewing pair's following/followedBy alongside existing friendship/request state. It exposes neither another player's counters/plan nor blockedByOther. Hidden/blocked/ineligible targets return generic denial. Following conveys no future spectator/party permission.

## API

| Method | Route | Access |
|---|---|---|
| POST | `/api/v1/players/{publicPlayerId}/follow` | Verified principal follows target; idempotent |
| DELETE | same | Verified principal unfollows target; idempotent |
| GET | `/api/v1/player/followers` | Owner only, cursor + limit |
| GET | `/api/v1/player/following` | Owner only, cursor + limit |
| GET/PATCH | `/api/v1/player/social-settings` | Existing endpoint, completed fields and revision |
| GET | `/api/v1/player/social-summary` | Adds own `follows: {followerCount, followingCount}` via one counter read |
| GET | `/api/v1/players/{id}/profile` | Adds pair-specific following/followedBy inside relationship |

Follow mutation responses are `{success:true}`; subsequent profile fetch provides authoritative relationship state. List items contain `profile: {publicPlayerId, displayName, friendCode, avatarKey}` plus `followedSince`, matching existing relationship-card DTO conventions. No client source UID is authoritative; a forged body cannot select another actor. There is no arbitrary third-party graph endpoint.

Pagination uses sortTime DESC + document ID DESC, default20/max50, one look-ahead document. Encrypted cursors bind to UID and list direction and expire after the existing 15-minute period. Deterministic point reads provide relationship state (one read per direction, no graph scan). List profile resolution uses two getAll batches after the page query: profiles, then eligibility/test/block checks. Automatic subcollection indexes suffice; no indexes or rules deployed or added.

## Technical abuse limits

Follow and Unfollow share one UID-scoped Redis sorted-set limiter across devices/instances: 30 operations in a rolling minute and 200 in a rolling24h window. Both limits are checked atomically by Lua against Redis TIME. TTL24h; random command members prevent same-millisecond collapse. This is independent of plan and runs outside Firestore transactions. Retries count as HTTP attempts even when they produce no Firestore mutation. Redis failure returns SOCIAL_SERVICE_UNAVAILABLE; Block bypasses this limiter. Redis restart can reset these ephemeral abuse windows; it cannot change durable graph/counters or grant Premium authority.

## Unity

Existing Social navigation adds Following/Followers. Cards resolve current public name/code, show a default avatar, local presentation date and Profile action; Following adds Unfollow. Public profile offers Follow / Follow back / Unfollow independently of friend/request actions. Text labels carry state rather than color alone. Busy guards prevent duplicate UI mutations; account-bound client discards stale results and the view closes/clears on identity switch. Lists refresh on entry and successful mutation only, with no polling.

Privacy UI exposes discoverable-by-name, friend-request permission and Follow permission. Presence/activity controls are deliberately deferred to S1.4 under section62's option: their backend fields are configurable now, but showing unavailable status controls would imply an implemented feature. No simulated online state is shown. EN/ES use the existing official Localization tables; protected Localization Settings.asset remains untouched.

Validation uses the isolated `client/Validation/Generated/G1Project` and test-classpath-only loopback server at127.0.0.1:18089, Firebase disabled and in-memory data. S1.3 uses two separate local identities to avoid consuming S1.2 friend-request rate windows. The harness now keeps a stable cursor key per fixture across requests, matching production bean lifetime. It uses normal Follow API calls for its bounded21-edge pagination fixture; no swarm or real Firebase identity is created.

## Validation evidence

Backend XML: `server/domino/build/test-results/test/`. Emulator XML: `server/domino/build/test-results/emulatorTest/`. Unity screenshots/results/logs: `client/Validation/Generated/S13/`.

- Backend: 531 PASS /14 SKIPPED (545 total), including12 S1.3 tests:9 grouped domain tests,1 HTTP scenario,1 Redis-unavailable test and1 real LOCAL Redis rate test. This run opts in only to S1.3 Redis validation; the14 preexisting optional Redis tests remain skipped.
- Firestore Emulator:25 PASS, including4 S1.3 tests; existing direct-client rules tests extend to following/followers and counters. Emulator process/port cleanup PASS.
- Social client:66 checks PASS. Static Unity compilation PASS.
- Unity EditMode130 checks PASS; PlayMode291 checks PASS, final Console errors0. Interactive actions are automated in the real Editor, screenshots reviewed by the agent, not a claim of human-operated clicks or physical-device testing. Portrait sizes1080x1920,1080x2400 and1536x2048 were checked in EN/ES; Follow-specific flow uses1080x1920.
- Regressions: Guest Auth43; Player Foundation/Realtime469; I4 Replay1595 over62 matches/31061 events; I3/M5 client75; Monetization26; F0 isolation16, all PASS. Backend covers Profile/Entitlements/S1.1/S1.2/I4/M5/Matchmaking/Monetization/F0/F0.1.

Domain and Emulator tests assert directional duplicate operations, mutual edges, one-sided removal, no friendship auto-follow, unfriend/decline/cancel independence, existing edge after NO_ONE, fresh denial, self/inactive/test-account rejection, current names, UID-preserving account linking, cursor tampering/owner/direction binding and no private DTO fields. Concurrency covers Follow/Follow, Follow/Unfollow, reverse Follow, shared source/target counters, Follow/Block, Unfollow/Block and privacy/Follow. Emulator reconciliation compares both stored counters with durable projection counts. Block races end with no forbidden edges.

The local Redis test uses two limiter instances sharing one unique test UID, proves30th allowed/31st denied and200th allowed/201st denied, ages only test-owned fixture entries relative to Redis time, and deletes only that unique key. No FLUSHDB or live-user mutation.

## Measured Firestore operation costs

Emulator document-operation counts, not billing guarantees. These measure service/repository operations; existing HTTP identity guards and target resolution add their own reads. Transaction contention can increase reads/retries.

| Operation | Reads | Writes |
|---|---:|---:|
| Follow |12|4|
| Duplicate Follow |10|0|
| Unfollow |4|4|
| Duplicate Unfollow |2|0|
| Mutual Follow (both) |24|8|
| Follow relationship lookup (one direction) |1|0|
| Followers page20, with look-ahead21st |121|0|
| Following page20, with look-ahead21st |121|0|
| Block stranger |8|2|
| Block one-way Follow |8|6|
| Block mutual Follow |8|8|
| Block friendship + mutual Follow |8|11|
| Block pending request + mutual Follow |9|10|
| Privacy update without search visibility change |4|1|

Follow list read cost is21 page documents +20 current public profiles +80 eligibility/test/block documents, delivered with query+two batches. A full page without look-ahead costs120 document reads. Existing name-search20 measurement remains141 reads and is a future optimization candidate, not modified here. Counts use a point-read, never a collection scan in bootstrap. Social reads per gameplay command=0; Firestore heartbeat writes=0; Follow polling=0; real Firestore calls=0.

## Limitations

No social Presence, Party, invitations, Spectator, Chat, follower FCM, Report Player, account-deletion workflow, production deployment or real Firebase validation. Physical Android and native screen-reader validation NOT_RUN. Presence/activity privacy UI deferred as explained above. S1.2 request auto-expiration remains unimplemented. Search cost remains an optimization candidate.

The repository's preexisting global error handler returns500 INTERNAL_ERROR for nonexistent third-party graph routes instead of404; no graph or identity data is returned and no such route is implemented. Tests verify non-success/no list data. This unrelated generic routing behavior was not changed.

## Safety and inventory

The initial96 preexisting user files are the preservation baseline, including AdsSettings, ApiSettings, Localization Settings, google-services files, Android ProjectSettings/API36, Firebase resolver/generated work and Gradle/Play preparation. SHA256 comparison and exact file classification are recorded at finalization below. No staging, commit, push, reset/restore/stash/clean or deployment was performed.

## Final machine-readable report

```text
BRANCH=main
SOURCE_SHA_BEFORE=54a658e3927483391bb6e9a0eecb05b9d0ee8ab8
SOURCE_SHA_AFTER=54a658e3927483391bb6e9a0eecb05b9d0ee8ab8
FOLLOW_IMPLEMENTED=YES
UNFOLLOW_IMPLEMENTED=YES
FOLLOW_DIRECTIONAL=YES
FOLLOW_FREE=YES
FOLLOW_DEPENDS_ON_ENTITLEMENTS=NO
FOLLOW_DEPENDS_ON_GOOGLE_PLAY=NO
FOLLOW=PASS
DUPLICATE_FOLLOW=PASS
UNFOLLOW=PASS
DUPLICATE_UNFOLLOW=PASS
MUTUAL_FOLLOW=PASS
FOLLOW_BACK=PASS
FOLLOW_FRIENDSHIP_INDEPENDENCE=PASS
FOLLOW_REQUEST_INDEPENDENCE=PASS
FOLLOWERS_IMPLEMENTED=YES
FOLLOWING_IMPLEMENTED=YES
FOLLOWERS_PAGINATION=PASS
FOLLOWING_PAGINATION=PASS
FOLLOWER_LIST_VISIBILITY=OWNER_ONLY
FOLLOWING_LIST_VISIBILITY=OWNER_ONLY
FOLLOWER_COUNT_IMPLEMENTED=YES
FOLLOWING_COUNT_IMPLEMENTED=YES
FOLLOW_COUNTER_CONSISTENCY=PASS
FOLLOW_CONCURRENCY=PASS
PUBLIC_FOLLOWER_COUNT=NO
PUBLIC_FOLLOWING_COUNT=NO
DISCOVERABLE_BY_NAME=PASS
FRIEND_REQUEST_PRIVACY=PASS
FOLLOW_PRIVACY=PASS
FOLLOW_PRIVACY_EVERYONE=PASS
FOLLOW_PRIVACY_NO_ONE=PASS
FOLLOW_PRIVACY_DEFAULT=EVERYONE
PRESENCE_VISIBILITY_SETTING=PERSISTED_AND_API_CONFIGURABLE; UI_DEFERRED
MATCH_ACTIVITY_VISIBILITY_SETTING=PERSISTED_AND_API_CONFIGURABLE; UI_DEFERRED
EXISTING_FOLLOW_SURVIVES_PRIVACY_CHANGE=PASS
SHOW_LAST_SEEN=NO
BLOCK_REMOVES_ONE_WAY_FOLLOW=PASS
BLOCK_REMOVES_MUTUAL_FOLLOW=PASS
BLOCK_COMPLEX_SOCIAL_STATE=PASS
UNBLOCK_RESTORES_FOLLOW=NO
BLOCK_PRECEDENCE=PASS
FOLLOW_ENDPOINT=POST /api/v1/players/{publicPlayerId}/follow
UNFOLLOW_ENDPOINT=DELETE /api/v1/players/{publicPlayerId}/follow
FOLLOWERS_ENDPOINT=GET /api/v1/player/followers
FOLLOWING_ENDPOINT=GET /api/v1/player/following
SOCIAL_SETTINGS_ENDPOINTS=GET/PATCH /api/v1/player/social-settings
AUTH_REQUIRED=YES
SOURCE_UID_CLIENT_CONTROLLED=NO
FOLLOW_UI=PASS
UNFOLLOW_UI=PASS
FOLLOW_BACK_UI=PASS
FOLLOWERS_UI=PASS
FOLLOWING_UI=PASS
PRIVACY_UI=PASS
FRIEND_AND_FOLLOW_COMBINATIONS=PASS
EN_LOCALIZATION=PASS
ES_LOCALIZATION=PASS
ACCOUNT_SWITCH_CACHE_CLEARING=PASS
UID_EXPOSED_PUBLICLY=NO
BLOCK_PRIVACY_LEAK=NO
OTHER_USER_FOLLOWER_LIST_ACCESS=DENIED
OTHER_USER_FOLLOWING_LIST_ACCESS=DENIED
DIRECT_FIRESTORE_FOLLOW_ACCESS=DENIED
TEST_ACCOUNTS_FOLLOWABLE=NO
BACKEND_TESTS=531 PASS / 14 SKIPPED (545 TOTAL)
S1_3_BACKEND_TESTS=12 PASS
FIRESTORE_EMULATOR_TESTS=25 PASS
S1_3_EMULATOR_TESTS=4 PASS
SOCIAL_CLIENT_TESTS=66 PASS
UNITY_EDITMODE_TESTS=130 CHECKS PASS
UNITY_PLAYMODE_TESTS=291 CHECKS PASS
UNITY_MANUAL_CHECKS=SCREENSHOTS_REVIEWED; REAL_EDITOR_AUTOMATED_INTERACTIONS
UNITY_COMPILATION=PASS
CONSOLE_ERRORS=0_IN_FINAL_VALIDATION
ANDROID_PHYSICAL_TEST=NOT_RUN
NATIVE_SCREEN_READER_TEST=NOT_RUN
FOLLOW_READS=12
FOLLOW_WRITES=4
DUPLICATE_FOLLOW_READS=10
DUPLICATE_FOLLOW_WRITES=0
UNFOLLOW_READS=4
UNFOLLOW_WRITES=4
DUPLICATE_UNFOLLOW_READS=2
DUPLICATE_UNFOLLOW_WRITES=0
MUTUAL_FOLLOW_TOTAL_WRITES=8
FOLLOW_RELATIONSHIP_LOOKUP_READS=1_PER_DIRECTION
FOLLOWERS_PAGE_20_READS=121_WITH_LOOKAHEAD
FOLLOWING_PAGE_20_READS=121_WITH_LOOKAHEAD
BLOCK_ONE_WAY_FOLLOW_READS=8
BLOCK_ONE_WAY_FOLLOW_WRITES=6
BLOCK_MUTUAL_FOLLOW_READS=8
BLOCK_MUTUAL_FOLLOW_WRITES=8
BLOCK_COMPLEX_SOCIAL_STATE_READS=8_FRIEND / 9_PENDING
BLOCK_COMPLEX_SOCIAL_STATE_WRITES=11_FRIEND / 10_PENDING
PRIVACY_UPDATE_READS=4
PRIVACY_UPDATE_WRITES=1_WITHOUT_DISCOVERABILITY_CHANGE
FIRESTORE_HEARTBEAT_WRITES=0
FOLLOW_BACKGROUND_POLLING=0
SOCIAL_READS_PER_GAMEPLAY_COMMAND=0
NAME_SEARCH_20_READS=141
SEARCH_COST_FUTURE_OPTIMIZATION=YES
AUTH_REGRESSION=PASS
PROFILE_REGRESSION=PASS
ENTITLEMENTS_REGRESSION=PASS
S1_1_REGRESSION=PASS
S1_2_REGRESSION=PASS
I4_REGRESSION=PASS
M5_REGRESSION=PASS
MATCHMAKING_REGRESSION=PASS
MONETIZATION_REGRESSION=PASS
F0_F01_REGRESSION=PASS
ANDROID_USER_WORK_PRESERVED=YES
ADS_SETTINGS_PRESERVED=YES
API_SETTINGS_PRESERVED=YES
LOCALIZATION_SETTINGS_PRESERVED=YES
GOOGLE_SERVICES_JSON_PRESERVED=YES
ANDROID_PROJECT_SETTINGS_PRESERVED=YES
ANDROID_RESOLVER_WORK_PRESERVED=YES
GOOGLE_PLAY_PREPARATION_PRESERVED=YES
SOCIAL_PRESENCE_IMPLEMENTED=NO
PARTY_IMPLEMENTED=NO
FRIEND_INVITATIONS_IMPLEMENTED=NO
SPECTATOR_IMPLEMENTED=NO
CHAT_IMPLEMENTED=NO
FCM_FOLLOW_NOTIFICATIONS_IMPLEMENTED=NO
REPORT_PLAYER_IMPLEMENTED=NO
FILES_MODIFIED=119_DIRTY_OR_UNTRACKED_FILES_TOTAL
S1_3_FILES_MODIFIED=23
PREEXISTING_USER_FILES=96
UNEXPECTED_FILES=0
UNRELATED_USER_FILES_MODIFIED_BY_S1_3=NO
REAL_FIRESTORE_CALLS=0
BOT_SWARM_STARTED=NO
VALIDATION_BACKEND_CLEANUP=PASS
EMULATOR_CLEANUP=PASS
COMMIT=NONE
PUSH=NONE
DEPLOY=NONE
S1_4_STARTED=NO
NEXT=S1.4 (WAITING_FOR_EXPLICIT_APPROVAL)
```

## Exact changed-file classification

All 96 baseline SHA256 hashes match. The 23 S1.3 entries below are intentional; unexpected entries=0. No protected file was staged, normalized or restored.

| Path | Classification |
|---|---|
| client/DominoGame/Assets/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0/firebase-app-unity-13.16.0.pom | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0/firebase-auth-unity-13.16.0.pom | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0/firebase-firestore-unity-13.16.0.pom | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/Plugins/Android/GoogleMobileAdsPlugin.androidlib/AndroidManifest.xml | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/StreamingAssets/google-services-desktop.json | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/StreamingAssets/google-services-desktop.json.meta | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/_Domino/Editor/Localization/Translations.json | S1_3_INTENTIONAL |
| client/DominoGame/Assets/_Domino/Localization/Localization Settings.asset | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/_Domino/Localization/Tables/Domino UI Shared Data.asset | S1_3_INTENTIONAL |
| client/DominoGame/Assets/_Domino/Localization/Tables/Domino UI_en.asset | S1_3_INTENTIONAL |
| client/DominoGame/Assets/_Domino/Localization/Tables/Domino UI_es.asset | S1_3_INTENTIONAL |
| client/DominoGame/Assets/_Domino/Resources/AdsSettings.asset | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/_Domino/Resources/ApiSettings.asset | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/_Domino/Scripts/Social/Editor/SocialVisualValidation.cs | S1_3_INTENTIONAL |
| client/DominoGame/Assets/_Domino/Scripts/Social/SocialClient.cs | S1_3_INTENTIONAL |
| client/DominoGame/Assets/_Domino/Scripts/Social/SocialView.cs | S1_3_INTENTIONAL |
| client/DominoGame/Assets/google-services.json | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/ProjectSettings/GvhProjectSettings.xml | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/ProjectSettings/ProjectSettings.asset | PRE_EXISTING_USER_CHANGE |
| client/Validation/SocialClientTests.cs | S1_3_INTENTIONAL |
| server/domino/src/main/kotlin/com/teamfho/domino/social/FirestoreFriendships.kt | S1_3_INTENTIONAL |
| server/domino/src/main/kotlin/com/teamfho/domino/social/FirestoreSocialRepository.kt | S1_3_INTENTIONAL |
| server/domino/src/main/kotlin/com/teamfho/domino/social/Friendships.kt | S1_3_INTENTIONAL |
| server/domino/src/main/kotlin/com/teamfho/domino/social/SocialController.kt | S1_3_INTENTIONAL |
| server/domino/src/main/kotlin/com/teamfho/domino/social/SocialModel.kt | S1_3_INTENTIONAL |
| server/domino/src/test/kotlin/com/teamfho/domino/social/SocialHttpTests.kt | S1_3_INTENTIONAL |
| server/domino/src/test/kotlin/com/teamfho/domino/social/SocialRulesEmulatorTests.kt | S1_3_INTENTIONAL |
| server/domino/src/test/kotlin/com/teamfho/domino/social/SocialTests.kt | S1_3_INTENTIONAL |
| server/domino/src/test/kotlin/com/teamfho/domino/social/SocialValidationServer.kt | S1_3_INTENTIONAL |
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
| client/Validation/S1_3_FOLLOW_PRIVACY_REPORT.md | S1_3_INTENTIONAL |
| server/domino/src/main/kotlin/com/teamfho/domino/social/FollowController.kt | S1_3_INTENTIONAL |
| server/domino/src/main/kotlin/com/teamfho/domino/social/Follows.kt | S1_3_INTENTIONAL |
| server/domino/src/test/kotlin/com/teamfho/domino/social/FollowEmulatorTests.kt | S1_3_INTENTIONAL |
| server/domino/src/test/kotlin/com/teamfho/domino/social/FollowRateTests.kt | S1_3_INTENTIONAL |
| server/domino/src/test/kotlin/com/teamfho/domino/social/FollowTests.kt | S1_3_INTENTIONAL |

GENERATED_BY_VALIDATION (ignored, retained as evidence): `client/Validation/Generated/S13/` screenshots/logs/results; isolated `Generated/G1Project`; existing per-suite generated test outputs; backend `build/test-results`, reports and emulator logs. Owned temporary backend stop files were removed after graceful shutdown. Emulator port18085 and validation backend port18089 are closed. The user's original Unity process was preserved.
