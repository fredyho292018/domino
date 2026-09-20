# CUBAN DOMINO CLUB — S1.1 IMPLEMENTATION REPORT

Date: 2026-09-20. Branch: `main`.

SOURCE_SHA_BEFORE=82a3fd3823750a929c15b6a7c639523b183142c4
SOURCE_SHA_AFTER=82a3fd3823750a929c15b6a7c639523b183142c4

Implementation is uncommitted. No production services, Play Console, real Firebase, Bot Swarm, deployment, commit or push were used.

## Scope and results

```text
PUBLIC_IDENTITY_IMPLEMENTED=YES
PUBLIC_PLAYER_ID_STABLE=PASS
FIREBASE_UID_PUBLICLY_EXPOSED=NO_IN_SOCIAL_DTOS
FRIEND_CODE_IMPLEMENTED=YES
FRIEND_CODE_STABLE=PASS
FRIEND_CODE_UNIQUE=PASS
FRIEND_CODE_FORMAT=FHO-XXXXXXXXXXXX
LAZY_BACKFILL=PASS
CONCURRENT_CREATION_SAFE=PASS
COLLISION_RETRY=PASS; MAX_ATTEMPTS=5

PUBLIC_PROFILE_IMPLEMENTED=YES
DISPLAY_NAME_UNIQUE=NO
DISPLAY_NAME_PROJECTION_SYNC=PASS
AVATAR_IMPLEMENTATION=LOCAL_DEFAULT; avatarKey=null

NAME_PREFIX_SEARCH=PASS
FRIEND_CODE_EXACT_LOOKUP=PASS
SEARCH_MIN_LENGTH=3
SEARCH_MAX_NAME_LENGTH=16
SEARCH_DEFAULT_PAGE=20
SEARCH_MAX_PAGE=50
CURSOR_PAGINATION=PASS
SEARCH_CANDIDATE_BUDGET=min(3*requestedLimit,100)
TEST_ACCOUNTS_DISCOVERABLE=NO
BLOCKED_ACCOUNTS_DISCOVERABLE=NO

SOCIAL_PRIVACY_IMPLEMENTED=YES
FRIEND_REQUEST_PRIVACY_DEFAULT=EVERYONE
FOLLOW_PRIVACY_DEFAULT=EVERYONE
PRESENCE_VISIBILITY_DEFAULT=FRIENDS
MATCH_ACTIVITY_VISIBILITY_DEFAULT=FRIENDS
DISCOVERABLE_BY_NAME_IMPLEMENTED=YES
EXISTING_USER_DISCOVERY_POLICY=FALSE_UNTIL_EXPLICIT_TOGGLE
NEW_PLAYER_DISCOVERY_DEFAULT=TRUE

BLOCK_IMPLEMENTED=YES
UNBLOCK_IMPLEMENTED=YES
BLOCK_LIST_IMPLEMENTED=YES
BLOCK_FREE=YES
BLOCK_DEPENDS_ON_ENTITLEMENTS=NO
BLOCK_REDIS_FAILURE_SAFE=YES
BLOCK_HIDES_SEARCH=PASS
BLOCK_HIDES_PROFILE=PASS
BLOCK_PRIVACY_LEAK=NO
ANONYMOUS_TO_LINKED_IDENTITY_PRESERVED=PASS

BACKEND_TESTS=505 PASS / 14 SKIPPED; TOTAL=519
S1_1_BACKEND_TESTS=21 PASS
FIRESTORE_EMULATOR_TESTS=17 PASS
S1_1_EMULATOR_TESTS=6 PASS
S1_1_CLIENT_TESTS=31 PASS
UNITY_EDITMODE_TESTS=48 CHECKS PASS
UNITY_PLAYMODE_TESTS=174 CHECKS PASS
UNITY_COMPILATION=PASS
CONSOLE_ERRORS=0_IN_FINAL_PLAY_MODE_VALIDATION
UNITY_MANUAL_CHECKS=SCREENSHOTS_REVIEWED; UI_ACTIONS_AUTOMATED_IN_REAL_EDITOR
ANDROID_PHYSICAL_TEST=NOT_RUN
SCREEN_READER_PHYSICAL_TEST=NOT_RUN

DIRECT_CLIENT_SOCIAL_READS=DENIED_IN_EMULATOR
DIRECT_CLIENT_SOCIAL_WRITES=DENIED_IN_EMULATOR
REAL_FIRESTORE_CALLS=0
FIRESTORE_HEARTBEAT_WRITES=0
SOCIAL_READS_PER_GAMEPLAY_COMMAND=0
BOT_SWARM_STARTED=NO
VALIDATION_BACKEND_CLEANUP=PASS
EMULATOR_CLEANUP=PASS
COMMIT=NONE
PUSH=NONE
DEPLOY=NONE
```

The 14 normal-suite skips are pre-existing optional Redis tests: PartnersMatchmakingTests (3), RedisMatchmakingTests (8), PartnersOnlineTests (1), RedisTurnIndexTests (1), RedisPresenceTests (1). They are **not claimed as executed passes**. No swarm load matrix was run.

## Architecture

`social/SocialModel.kt` defines separate public identity/profile, privacy and block types and repository contracts. `PublicPlayerIdentityService`, `PublicPlayerProfileService`, `PlayerDiscoveryService`, `SocialPrivacyService`, `BlockService` and `SocialAccess` separate responsibilities. `FirestoreSocialRepository` implements the contracts with server-side transactions. Controllers derive the acting UID exclusively from the verified Firebase principal.

The public ID is 128 random bits, URL-safe Base64 without padding (22 characters), independent of UID. The friend code uses 12 cryptographically random characters from Crockford's 32-character alphabet `0123456789ABCDEFGHJKMNPQRSTVWXYZ`. O/I/L/U are excluded. Lookup accepts surrounding whitespace and case differences; the hyphen is required. Codes are immutable and non-secret.

Identity creation atomically reads/reserves the owner mapping, public profile and code index and creates settings if absent. Collisions retry with a fresh candidate, maximum five attempts. Concurrent calls for the same UID return the already-created identity. No bulk migration occurs.

New Player documents receive an internal `socialDefaultDiscoverable=true` marker. Existing documents without that marker backfill with name discovery disabled. Opening Social does not opt legacy users into name discovery; the explicit privacy toggle does. Exact code/profile access remains available subject to account eligibility and blocks. Linking an anonymous account while preserving UID retains all identity and privacy records.

The authoritative display-name transaction updates the projection only when the alias changes and a public identity exists. Unchanged aliases retain the original two reads and zero writes. A changed alias adds an owner-identity read; with an existing public identity it updates Player and its single public projection. There is no fan-out.

Search normalizes with NFKC, trim and Locale.ROOT lowercase, then validates the existing ASCII name alphabet. Display-name validation itself is unchanged. Prefix queries use normalized name plus document ID for deterministic ordering. A bounded candidate query can produce a partial/empty page with a continuation cursor. No filtered counts or total counts are exposed.

Current Player status and the existing `developmentTestAccounts/{uid}.isTestAccount` marker are rechecked before delivery, so an old search projection cannot expose a subsequently marked test account. Privacy and blocks are checked again for each delivered candidate. There is no background eligibility scan. Current Player status has only ACTIVE; hypothetical inactive/deleted states fail the eligibility comparison.

## Firestore model, indexes and rules

| Path | Contents / authority |
|---|---|
| `players/{uid}/publicIdentity/current` | Stable public ID, code, creation time; backend only |
| `publicPlayerProfiles/{publicId}` | Internal UID, public ID, code, name, normalized name, eligibility and timestamps; API serializes a separate safe DTO |
| `publicPlayerCodes/{canonicalCode}` | Public ID exact index; backend only |
| `players/{uid}/socialSettings/current` | Approved defaults, discoverableByName and revision |
| `players/{uid}/blocks/{targetUid}` | Public target identity/name snapshot and timestamp |
| `players/{targetUid}/blockedBy/{uid}` | Minimal inverse reference, never exposed to client |

Block and inverse references are written/deleted atomically. Repeating an unchanged block produces zero writes. Unblock resolves the owner's retained block record, so it still succeeds when a target later becomes ineligible; repeated deletion succeeds. Block does not consult Entitlements or Redis. Future S1.2 acceptance must read both block documents inside its authoritative transaction; no speculative socialPairs/friendship/request collection is created now.

The new `server/domino/firestore.indexes.json` contains only the collection query index actually used by search: `searchEligible ASC, normalizedDisplayName ASC, __name__ ASC`. Block lists use a single-field publicPlayerId order and opaque cursor. The existing deny-all client `firestore.rules` was unchanged. Authenticated emulator probes prove direct profile/code/identity/settings/block/inverse reads and writes are denied; profile enumeration is also denied. These tests first validate their emulator token against a temporary allowed probe, then restore the original rules.

No index/rules deployment was performed. Emulator validates query behavior and access rules, but does not establish that a composite index is deployed in production. Deploying the intended index through the normal reviewed release process remains necessary before enabling this feature against a real database.

Future account deletion can locate all identity/projection/code mappings by UID and both block directions by their subcollections. Cleanup workflow and retention jobs are not implemented in S1.1.

## API

All paths below are prefixed `/api/v1`, require Firebase authentication, and have no client source-UID authority.

| Method | Path | Behavior |
|---|---|---|
| GET | `/player/social-summary` | Lazily creates own identity; safe profile and own privacy |
| GET | `/players/{publicId}/profile` | Safe public profile, generic 404 if invisible |
| GET | `/players/search?mode=NAME&q=...&limit=20&cursor=...` | Prefix search |
| GET | `/players/search?mode=FRIEND_CODE&friendCode=...` | Exact code; no name/cursor parameters accepted |
| GET | `/player/social-settings` | Own settings |
| PATCH | `/player/social-settings` | Exactly discoverableByName boolean and positive revision; unknown fields rejected |
| POST | `/players/{publicId}/block` | Idempotent durable safety action |
| DELETE | `/players/{publicId}/block` | Idempotent removal of own block |
| GET | `/player/blocks?limit=20&cursor=...` | Own paginated minimal block records |

Typed errors cover invalid query/code, missing/invisible player, self action, unauthorized social action, rate limit, identity/service unavailable and revision mismatch. An existing reverse block never produces `blockedByOther` or a special outward denial reason. No public DTO includes Firebase UID, token, email, billing, grant internals, test markers, presence or Match data.

Cursors use AES-GCM, bind to account and query/list, and expire after 15 minutes. Set `DOMINO_SOCIAL_CURSOR_KEY` to a shared Base64-encoded 32-byte secret for multi-instance continuity. Without it a secure process-local random key is used; restart/another instance invalidates outstanding cursors, requiring a fresh search. No secret is checked in or sent to Unity.

Redis token buckets use Redis time, per-account/action keys and expiry: name search 20/minute with burst 5, friend-code lookup and unblock 30/minute, profile/other protected reads 60/minute. Redis failure returns safe temporary unavailability for rate-limited operations. The block action deliberately bypasses this dependency and still attempts durable persistence. No new presence implementation or heartbeat was added.

## Unity

The main menu now exposes Social beside History. The screen contains own code/copy, name or code search, results, pagination, safe public profile, confirmed Block, paginated Blocked Players/Unblock, and discoverability toggle. No inactive Friends/Follow/Party/Spectate controls were introduced.

`SocialApi` reuses the existing validated endpoint policy, token provider and transport, with a separate social route allowlist. It does not broaden the match API. `SocialClient` binds to the current UID; in-flight results are discarded after account switch. The view closes/cancels and discards displayed data on identity change or logout. No persistent social cache exists.

All 24 new strings are EN/ES StringTable entries. `ImportTablesOnly` was used in the isolated validation project, then only the two tables and Shared Data were copied back. Protected Localization Settings.asset was not changed.

Portrait layouts exercised: 1080×1920, 1080×2400 and 1536×2048, in both languages. Captures were inspected and a status/first-row overlap was corrected before the final run. Native Unity accessibility nodes provide translated labels, button actions, search input and status announcements. The built-in `com.unity.modules.accessibility` module was added to manifest/lock; no third-party SDK or Android configuration was changed. TalkBack/VoiceOver physical-device validation is not claimed.

## Validation evidence

- Backend XML: `server/domino/build/test-results/test/`: 519 total, 505 passed, 14 optional skips, zero failures/errors; S1.1 contributes 21 tests.
- Emulator XML: `server/domino/build/test-results/emulatorTest/`: 17 passed; S1.1 contributes 6. Includes concurrent identity/collision reservation, anonymous linking, atomic rename/no-op, privacy and inverse-block transactions, concurrent block/unblock, and authenticated rules denial.
- Client tests: `client/Validation/RunSocialClientTests.ps1`: 31 passed, fake transport only. Tests exact routes, method/route allowlist, query validation, token refresh bound, typed failures and account-switch discard.
- Unity EditMode: `Generated/S11/editmode-result.txt`: 48 table checks passed.
- Unity Play Mode: `Generated/S11/unity-result.txt`: 174 checks passed. Production Social controllers were exercised through a test-classpath-only loopback server with in-memory repositories and a local test verifier. Search, code lookup, profiles, confirmation, block exclusion, unblock, privacy, empty/error/retry, pagination, copying, native accessibility nodes and both languages were checked.
- Screenshots: `Generated/S11/search-{en|es}-{size}.png` and `privacy-{en|es}-{size}.png`.
- Final Play Mode captured zero Error/Exception/Assert logs. Earlier isolated-project dependency/compilation failures were corrected; they are not counted as final success evidence.
- Runtime and Editor compilation passed. Existing unrelated compiler warnings remain.

Additional client regressions, all run this session: Guest Auth 43; Player Foundation/Realtime 469; Online 48; I3/M5 75; Replay 1595 checks over 62 matches/31061 events; Monetization 26; Firestore isolation 16. Logs are in `Generated/S11/*.ps1.log`.

Backend suite covers Player/Profile/Auth, Entitlements, I4, M5, matchmaking, monetization and F0/F0.1. Redis-dependent optional tests remain explicitly skipped in this run. Gameplay/turn/replay reducer source was not modified.

## Emulator operation measurements

Uncontended repository/service operations, not a billing guarantee. The recorder counts document reads including batched gets and query results; separate from the prior swarm recorder, which cannot instrument this varargs getAll path correctly.

| Operation | Reads | Writes | Query calls |
|---|---:|---:|---:|
| Identity create | 6 | 4 | 0 |
| Existing identity | 3 | 0 | 0 |
| Exact friend code | 6 | 0 | 0 |
| Name search returning 20, fixture with 21 candidates | 141 | 0 | 1 |
| Public profile | 5 | 0 | 0 |
| Block | 5 | 2 | 0 |
| Repeat block | 5 | 0 | 0 |
| Blocked-list page with one item | 1 | 0 | 1 |
| Unblock | 3 | 2 | 1 |
| Privacy change | 4 | 2 | 0 |

The authenticated HTTP controller also validates the actor with `ensure`: normally +3 reads/0 writes. Those reads are **not hidden in the operation table**. Summary/settings currently invoke identity validation at service boundaries too, so they have additional fixed reads. Name search favors fresh privacy/test/block authorization over caching; cost rises with candidate count and filtering, but work is capped. Transaction retries can add reads. There is no per-frame, periodic Firestore, per-gameplay-command or presence write path.

## Isolation and cleanup

Normal Gradle tests retain the F0 `127.0.0.1:1` emulator guard. Persistence tests explicitly require `FIRESTORE_EMULATOR_HOST=127.0.0.1:18085`, project `demo-domino-f0`, and EmulatorCredentials. Rules probes use unsigned emulator-only tokens; no Firebase users were created.

Unity ran in `client/Validation/Generated/G1Project`, not the user's open project. ValidationNetworkPolicy blocked real network entrypoints; the test adapter was restricted to exact loopback `127.0.0.1:18089`. The server requires `DOMINO_S11_LOOPBACK=true`, rejects real-Firestore opt-in and runs with `firebase.enabled=false`. Its verifier is test-classpath-only. No production auth bypass was added.

The validation server exited normally after its stop signal. Ports 18089 and 18085 were verified closed. Existing backend/Unity sessions were not stopped.

## Changed source

- New backend: `server/domino/src/main/kotlin/com/teamfho/domino/social/{SocialModel,FirestoreSocialRepository,SocialServices,SocialController}.kt`.
- Backend integration: `player/FirestorePlayerFoundationRepository.kt`, `server/domino/build.gradle.kts`, `server/domino/firestore.indexes.json`.
- New tests/tooling: `server/domino/src/test/kotlin/com/teamfho/domino/social/{SocialTests,SocialHttpTests,SocialEmulatorTests,SocialRulesEmulatorTests,SocialMeasurements,SocialValidationServer}.kt`.
- Existing alias test adjusted for the intentional identity read: `player/PlayerDisplayNameTests.kt`.
- Unity: new `Scripts/Social/{SocialClient,SocialView,SocialAccessibility}.cs`, `Scripts/Social/Editor/SocialVisualValidation.cs` and metadata.
- Unity wiring: `ApplicationServices.cs`, `DominoClientController.cs`, `StartMenuView.cs`.
- Localization: `Translations.json`, `Domino UI_en.asset`, `Domino UI_es.asset`, `Domino UI Shared Data.asset`.
- Packages: manifest/lock add only the built-in accessibility module.
- Validation: `SocialClientTests.cs`, `RunSocialClientTests.ps1`, this report.

## Preserved user work and limitations

85 captured pre-existing file hashes were rechecked with no differences, including all three protected settings assets, google-services.json, Android ProjectSettings, Gradle templates and captured resolver/generated artifacts. None were staged, reset, reformatted or overwritten. The pre-existing trailing whitespace in AdsSettings.asset was deliberately left unchanged.

No friendship/request/counter/follow/presence social UI, Party, spectator, chat, report-player, public statistics, avatar upload, account deletion, code regeneration or billing is implemented. These are later-phase scope, not enabled placeholders.

No physical Android testing, real Firebase validation, production index/rules deployment or external Play Console inspection occurred. Public identity/profile/search API behavior and durable semantics were validated locally; production index readiness and device screen-reader behavior remain deployment/device checks.

S1.1 changes are left unstaged for review alongside the preserved unrelated Android work.

## Final diff audit — continuation sections 102–106

Every changed/untracked path reported by Git is classified below. Paths are repository-relative. Pre-existing Android generated files remain user work, not S1.1 validation output. The 85 captured initial hashes remain unchanged; remaining Android paths are covered by the recorded initial directory inventory, not individual hash evidence.

| File | Classification |
|---|---|
| client/DominoGame/Assets/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0/firebase-app-unity-13.16.0.pom | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0/firebase-auth-unity-13.16.0.pom | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0/firebase-firestore-unity-13.16.0.pom | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/Plugins/Android/GoogleMobileAdsPlugin.androidlib/AndroidManifest.xml | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/StreamingAssets/google-services-desktop.json | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/StreamingAssets/google-services-desktop.json.meta | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/_Domino/Editor/Localization/Translations.json | S1_1_INTENTIONAL |
| client/DominoGame/Assets/_Domino/Localization/Localization Settings.asset | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/_Domino/Localization/Tables/Domino UI Shared Data.asset | S1_1_INTENTIONAL |
| client/DominoGame/Assets/_Domino/Localization/Tables/Domino UI_en.asset | S1_1_INTENTIONAL |
| client/DominoGame/Assets/_Domino/Localization/Tables/Domino UI_es.asset | S1_1_INTENTIONAL |
| client/DominoGame/Assets/_Domino/Resources/AdsSettings.asset | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/_Domino/Resources/ApiSettings.asset | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Assets/_Domino/Scripts/Client/DominoClientController.cs | S1_1_INTENTIONAL |
| client/DominoGame/Assets/_Domino/Scripts/Infrastructure/ApplicationServices.cs | S1_1_INTENTIONAL |
| client/DominoGame/Assets/_Domino/Scripts/UI/StartMenuView.cs | S1_1_INTENTIONAL |
| client/DominoGame/Assets/google-services.json | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/Packages/manifest.json | S1_1_INTENTIONAL |
| client/DominoGame/Packages/packages-lock.json | S1_1_INTENTIONAL |
| client/DominoGame/ProjectSettings/GvhProjectSettings.xml | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/ProjectSettings/ProjectSettings.asset | PRE_EXISTING_USER_CHANGE |
| server/domino/build.gradle.kts | S1_1_INTENTIONAL |
| server/domino/src/main/kotlin/com/teamfho/domino/player/FirestorePlayerFoundationRepository.kt | S1_1_INTENTIONAL |
| server/domino/src/test/kotlin/com/teamfho/domino/player/PlayerDisplayNameTests.kt | S1_1_INTENTIONAL |
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
| client/DominoGame/Assets/_Domino/Scripts/Social.meta | S1_1_INTENTIONAL |
| client/DominoGame/Assets/_Domino/Scripts/Social/Editor.meta | S1_1_INTENTIONAL |
| client/DominoGame/Assets/_Domino/Scripts/Social/Editor/SocialVisualValidation.cs | S1_1_INTENTIONAL |
| client/DominoGame/Assets/_Domino/Scripts/Social/Editor/SocialVisualValidation.cs.meta | S1_1_INTENTIONAL |
| client/DominoGame/Assets/_Domino/Scripts/Social/SocialAccessibility.cs | S1_1_INTENTIONAL |
| client/DominoGame/Assets/_Domino/Scripts/Social/SocialAccessibility.cs.meta | S1_1_INTENTIONAL |
| client/DominoGame/Assets/_Domino/Scripts/Social/SocialClient.cs | S1_1_INTENTIONAL |
| client/DominoGame/Assets/_Domino/Scripts/Social/SocialClient.cs.meta | S1_1_INTENTIONAL |
| client/DominoGame/Assets/_Domino/Scripts/Social/SocialView.cs | S1_1_INTENTIONAL |
| client/DominoGame/Assets/_Domino/Scripts/Social/SocialView.cs.meta | S1_1_INTENTIONAL |
| client/DominoGame/ProjectSettings/AndroidResolverDependencies.xml | PRE_EXISTING_USER_CHANGE |
| client/DominoGame/ProjectSettings/ScriptableBuildPipeline.json | PRE_EXISTING_USER_CHANGE |
| client/Validation/RunSocialClientTests.ps1 | S1_1_INTENTIONAL |
| client/Validation/S1_1_SOCIAL_FOUNDATION_REPORT.md | S1_1_INTENTIONAL |
| client/Validation/SocialClientTests.cs | S1_1_INTENTIONAL |
| server/domino/firestore.indexes.json | S1_1_INTENTIONAL |
| server/domino/src/main/kotlin/com/teamfho/domino/social/FirestoreSocialRepository.kt | S1_1_INTENTIONAL |
| server/domino/src/main/kotlin/com/teamfho/domino/social/SocialController.kt | S1_1_INTENTIONAL |
| server/domino/src/main/kotlin/com/teamfho/domino/social/SocialModel.kt | S1_1_INTENTIONAL |
| server/domino/src/main/kotlin/com/teamfho/domino/social/SocialServices.kt | S1_1_INTENTIONAL |
| server/domino/src/test/kotlin/com/teamfho/domino/social/SocialEmulatorTests.kt | S1_1_INTENTIONAL |
| server/domino/src/test/kotlin/com/teamfho/domino/social/SocialHttpTests.kt | S1_1_INTENTIONAL |
| server/domino/src/test/kotlin/com/teamfho/domino/social/SocialMeasurements.kt | S1_1_INTENTIONAL |
| server/domino/src/test/kotlin/com/teamfho/domino/social/SocialRulesEmulatorTests.kt | S1_1_INTENTIONAL |
| server/domino/src/test/kotlin/com/teamfho/domino/social/SocialTests.kt | S1_1_INTENTIONAL |
| server/domino/src/test/kotlin/com/teamfho/domino/social/SocialValidationServer.kt | S1_1_INTENTIONAL |

GENERATED_BY_VALIDATION: ignored client/Validation/Generated/S11 evidence, isolated Generated/G1Project and backend build test reports. Retained as review evidence; none appears as a pending Git source change. Validation processes and temporary stop signal were cleaned up. No pre-existing generated Android files were deleted.

UNEXPECTED=0

## Continuation checkpoint

```text
PUBLIC_IDENTITY_STORAGE=players/{uid}/publicIdentity/current
PUBLIC_PROFILE_STORAGE=publicPlayerProfiles/{publicId}
FRIEND_CODE_INDEX=publicPlayerCodes/{canonicalCode}
BLOCK_STORAGE=players/{uid}/blocks/{targetUid}; inverse players/{targetUid}/blockedBy/{uid}
PRIVACY_STORAGE=players/{uid}/socialSettings/current
FIRESTORE_INDEXES_ADDED=1_COMPOSITE_LOCAL_FILE; NOT_DEPLOYED
FIRESTORE_RULES_UPDATED=NO; EXISTING_DENY_RULES_TESTED
AUTH_REQUIRED=YES
SOURCE_UID_CLIENT_CONTROLLED=NO
ACCOUNT_SWITCH_CACHE_CLEARING=PASS
UID_EXPOSED_PUBLICLY=NO_IN_SOCIAL_RESPONSES
PRIVATE_PROFILE_FIELDS_EXPOSED=NO
BLOCK_PRIVACY_LEAK=NO
TEST_ACCOUNTS_DISCOVERABLE=NO
IDENTITY_CREATE_READS=6
IDENTITY_CREATE_WRITES=4
IDENTITY_EXISTING_READS=3
IDENTITY_EXISTING_WRITES=0
FRIEND_CODE_LOOKUP_READS=6
NAME_SEARCH_20_READS=141
PROFILE_LOOKUP_READS=5
BLOCK_WRITES=2
UNBLOCK_WRITES=2
PRIVACY_UPDATE_WRITES=2
COST_SCOPE=SERVICE_REPOSITORY; CONTROLLER_EXISTING_ACTOR_GUARD_ADDS_3_READS
REDIS_COST=ONE_SCRIPT_PER_RATE_LIMITED_REQUEST; ZERO_FOR_BLOCK; NO_NEW_HEARTBEAT
AUTH_REGRESSION=PASS
PROFILE_REGRESSION=PASS
ENTITLEMENTS_REGRESSION=PASS
I4_REGRESSION=PASS
M5_REGRESSION=PASS_EXECUTED_TESTS; OPTIONAL_REDIS_TESTS_SKIPPED
MATCHMAKING_REGRESSION=PASS_EXECUTED_TESTS; OPTIONAL_REDIS_TESTS_SKIPPED
F0_F01_REGRESSION=PASS_EXECUTED_TESTS; OPTIONAL_REDIS_TESTS_SKIPPED
ANDROID_USER_WORK_PRESERVED=YES
ADS_SETTINGS_PRESERVED=YES
API_SETTINGS_PRESERVED=YES
LOCALIZATION_SETTINGS_PRESERVED=YES
GOOGLE_SERVICES_JSON_PRESERVED=YES
ANDROID_PROJECT_SETTINGS_PRESERVED=YES
ANDROID_RESOLVER_WORK_PRESERVED=YES
FRIEND_REQUESTS_IMPLEMENTED=NO
FRIENDSHIPS_IMPLEMENTED=NO
FOLLOW_IMPLEMENTED=NO
SOCIAL_PRESENCE_IMPLEMENTED=NO
PARTY_IMPLEMENTED=NO
CHAT_IMPLEMENTED=NO
SPECTATOR_IMPLEMENTED=NO
REPORT_PLAYER_IMPLEMENTED=NO
FILES_MODIFIED=SEE_EXACT_132_PATH_DIFF_INVENTORY_INCLUDING_UNTRACKED
S1_1_FILES_MODIFIED=36_MODIFIED_OR_NEW_PATHS
PRE_EXISTING_USER_CHANGE=96_PATHS
UNEXPECTED=0
UNRELATED_USER_FILES_MODIFIED_BY_S1_1=NO
REAL_FIRESTORE_CALLS=0
FIRESTORE_HEARTBEAT_WRITES=0
SOCIAL_READS_PER_GAMEPLAY_COMMAND=0
COMMIT=NONE
PUSH=NONE
DEPLOY=NONE
NEXT=S1.2_ONLY_AFTER_EXPLICIT_APPROVAL
S1_2_STARTED=NO
```
