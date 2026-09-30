# APP-SHELL-01 PREWORK REVIEW

Read-only review completed. No commit, push, mock implementation, build, formatter, resolver, ad request or live validation was executed. The original 175-file snapshot is retained; review reports/inventories created afterward are accounted for separately.

## Primary classification

| Category | Files |
|---|---:|
| A — AdMob / Ads | 2 |
| B — Android build / Gradle | 7 |
| C — Firebase configuration / integration | 10 |
| D — Signing / release configuration | 1 |
| E — Social reports | 7 |
| F — SERVER-7 source / tests / validation | 66 |
| G — Pre-existing client assets | 3 |
| H — Generated / temporary | 79 |
| I — Confirmed credential material | 0 |
| J — Unknown functional origin | 0 |
| Total | 175 |

The companion CSV assigns each file exactly one category, its function, checkpoint disposition, scan outcome and SHA-256. Classification by function does not assert an author or prove completion. Category D takes precedence for the mixed PlayerSettings file; its Android hunks must not be double-counted.

## AdMob

Two pending files: AdsSettings.asset activates the existing service (enabled false → true) while environment remains DEVELOPMENT; GoogleMobileAdsPlugin AndroidManifest adds the real app identifier and Unity version metadata. Production rewarded-unit configuration already existed in the asset; it is not a newly introduced secret. Ad IDs are configuration identifiers, not signing credentials.

AdsConfiguration, DominoAdsSettings and rewarded-ad service code are already present, not pending new implementation. Development uses demo ad units and rejects production inventory in development context; DEVELOPMENT in a release context is unavailable. Activation changes runtime behavior in supported development contexts and is not merely formatting. The manifest retains INTERNET and introduces no new permission in this diff.

AdsFoundationTests, RewardedAdsTests and editor validation exist. Historical H1/H2 reports describe successful foundational/rewarded checks with source ads disabled; those reports do not validate this pending enablement or current Android package/release changes. H1 documents a consent boundary and no UMP flow at that phase; do not infer release-ready consent integration from the manifest. No new tests were run during this read-only task. Completion of the pending integration is NOT ESTABLISHED; hold the activation checkpoint until its intended build context and current validation evidence are reviewed.

## Android and Firebase

Gradle templates include Ads 25.4.0, UMP 4.0.0, Firebase Unity 13.16.0 and native Auth/Firestore dependencies, AndroidX/Jetifier and ARM ABI exclusions. settingsTemplate.gradle explicitly references Assets/GeneratedLocalRepo/Firebase/m2repository. Tracked source POMs switch packaging from aar to srcaar while generated Maven artifacts contain resolved aar copies. Excluding generated artifacts without a documented resolver/regeneration step can leave an incomplete checkout; this review did not run a clean build to establish reproducibility.

GvhProjectSettings disables prompting before dependency resolution and enables Jetifier; analytics remains disabled. FirebaseApp.androidlib is generated Android resource integration, not Firebase authentication code. Its resource XML contains client configuration. No pending Firebase Auth C# or backend source change is present in this set.

Both google-services JSON files are already tracked, are not ignored, and add a second client while retaining the same project_info. They are client configuration, not service-account JSON. The accompanying desktop meta changes import metadata. Existing tracking is evidence of repository practice, not a blanket publication policy. Their client API keys/resource values were inspected without printing them; no private_key/service-account material was detected. Publication/restriction policy and clean Android integration remain review gates; do not stage them merely because they are tracked.

## Signing and release

ProjectSettings/ProjectSettings.asset mixes version 1.0.0, Android version code 3, target SDK 36, package identity change, Gradle template switches, preloaded asset/texture settings, and enabled custom signing with a machine-local keystore reference and alias. No keystore was opened. No .keystore/.jks/private-key file is in the 175-file inventory. Nevertheless this file is EXCLUDE AS-IS because private/local signing configuration is mixed with potentially safe release configuration. A future approved split/sanitization is required; none was performed. There is no pending standalone safe signing-config file.

## Social and SERVER-7

The seven Social files are historical design/validation reports, including the incomplete five-friend validation and inconclusive 503 diagnosis. They are a coherent documentation checkpoint, NOT proof of completed Social functionality. The reports include safe identifiers and local paths; publication privacy review should retain only project-appropriate evidence. No pending Social runtime source is included. Generated Social helpers/evidence are ignored and are not silently included.

The 66 SERVER-7 files contain 18 server/tool source/test/build files and 48 validation/report files. They preserve timing instrumentation, bounded metrics/correlation, load/provisioning/review tooling, lifecycle/capacity observations and regression evidence. S7-15 reports 21 unit and 8 emulator passes; S7-17 reports 37 unit and 9 emulator passes. These are historical reports, not fresh test results or a new performance certification. The provisioning tooling references external identity stores but does not contain those identity files. No tooling was executed against a server.

## Generated, temporary and UI

79 files are CMake/NDK artifacts, Python bytecode, generated Maven dependencies, Android Addressables content-state evidence, resolver state or build-pipeline state. Exclude them from blanket staging; preserve them locally. GeneratedLocalRepo's build dependency needs an explicit regeneration decision rather than deletion.

Three pre-existing UI assets: ProfileDataSourceSettings.asset and its meta contain built-in local Addressables paths without configured remote environments; Localization Settings.asset changes only managed reference IDs while retaining selector behavior. The latter is serialization churn, not a feature; leave it local. The Addressables pair can be a small separate configuration checkpoint if intended, but is unnecessary for the proposed server/report checkpoints.

## Directed secret scan

All 175 files were read without executing them. Text scanning checked private-key markers, JWT literals, literal credential assignments, Cloudflare token commands and private signing-file extensions. The only pattern hit was Server7ProvisionTests.py's explicitly synthetic fixture; it was manually classified as a false positive. No real token, refresh credential, password, service-account key or Swarm identity file was identified in commit-candidate text. Binary/generated outputs are excluded, not certified credential-free by a text scan. External stores and keystore contents were not searched. Client configuration keys are not treated as secret-free publication approval. The mixed signing file remains excluded regardless of scan outcome.

## Proposed independent checkpoints — no commits yet

1. SERVER-7 instrumentation, controlled load tooling, tests and historical validation reports: category F, 66 files. Coherent preservation candidate; use explicit paths, keep dependent timing classes/tests together, and perform final staged diff/secret review. Avoid splitting shared OnlineRepository/Transport hunks blindly across historical phases.
2. Social validation/design checkpoint: category E, seven reports, clearly preserving PARTIAL/INCONCLUSIVE outcomes. No Social/Ads/App-shell implementation mixed in.
3. Android/Firebase/AdMob integration: categories A/B/C plus only reviewed safe release hunks from D. HOLD until signing material is separated, client-config publication is settled, generated dependency regeneration is documented and appropriate clean build evidence exists. Do not include all H files to make it build.

Review inventories/reports themselves are additional documentation, not original product files or mock assets. No unknown functional-origin files remain; unresolved readiness decisions are explicitly recorded above rather than hidden as approval.

```text
TOTAL_PENDING_FILES=175_ORIGINAL_SNAPSHOT
ADMOB_FILES=2
ANDROID_FILES=7_PRIMARY_CATEGORY_B
FIREBASE_SOURCE_FILES=0_AUTH_RUNTIME_SOURCE
FIREBASE_CONFIG_FILES=10_PRIMARY_CATEGORY_C
SIGNING_SAFE_CONFIG_FILES=0_STANDALONE_READY
SIGNING_PRIVATE_FILES=1_MIXED_LOCAL_SIGNING_REFERENCE_FILE_EXCLUDED
SOCIAL_FILES=7_REPORTS
SERVER7_VALIDATION_FILES=66_INCLUDING_18_SERVER_TOOL_FILES
OTHER_VALIDATION_FILES=REVIEW_REPORTS_AND_INVENTORIES_OUTSIDE_ORIGINAL_175
PREEXISTING_UI_FILES=3
GENERATED_TEMP_FILES=79
SECRET_FILES=0_CONFIRMED_CREDENTIAL_FILES_IN_ORIGINAL_SET
UNKNOWN_FILES=0
ADMOB_IMPLEMENTATION_COMPLETE=NOT_ESTABLISHED_FOR_PENDING_ACTIVATION
ADMOB_TEST_STATUS=HISTORICAL_TESTS_EXIST_CURRENT_ACTIVATION_NOT_VALIDATED
ADMOB_SAFE_TO_CHECKPOINT=HOLD
ANDROID_CHANGE_PURPOSE=FIREBASE_ADS_DEPENDENCY_AND_ANDROID_BUILD_INTEGRATION
ANDROID_SAFE_TO_CHECKPOINT=HOLD_REPRODUCIBILITY_AND_RELEASE_SPLIT
FIREBASE_SAFE_TO_CHECKPOINT=HOLD_CONFIG_POLICY_AND_RESOLVER_COHERENCE
SIGNING_PRIVATE_FILES_COMMITTED=NO
SOCIAL_SAFE_TO_CHECKPOINT=YES_DOCUMENTATION_ONLY_SUBJECT_TO_FINAL_STAGED_REVIEW
SECRET_SCAN=PASS_SCOPED_TEXT_SCAN_WITH_EXCLUSIONS
APP_SHELL_01_NEW_FILES=0_MOCK_OR_UI_FILES
PROPOSED_CHECKPOINT_1=SERVER7_TIMING_CAPACITY_TOOLING_TESTS_REPORTS
PROPOSED_CHECKPOINT_2=SOCIAL_VALIDATION_DESIGN_REPORTS
PROPOSED_CHECKPOINT_3=ANDROID_FIREBASE_ADMOB_INTEGRATION_HOLD
FILES_MODIFIED_DURING_REVIEW=0_OF_ORIGINAL_175
REVIEW_ARTIFACTS_CREATED=2
COMMIT_CREATED=NO
PUSH=NO
APP_SHELL_01_STARTED=NO
NEXT=APP-SHELL-01 PREWORK CHECKPOINT REVIEW
```

## Exact per-category file lists

### A (2)

- `client/DominoGame/Assets/Plugins/Android/GoogleMobileAdsPlugin.androidlib/AndroidManifest.xml` — HOLD_RUNTIME_ACTIVATION_VALIDATION
- `client/DominoGame/Assets/_Domino/Resources/AdsSettings.asset` — HOLD_RUNTIME_ACTIVATION_VALIDATION

### B (7)

- `client/DominoGame/Assets/Plugins/Android/gradleTemplate.properties` — HOLD_CLEAN_BUILD_EVIDENCE
- `client/DominoGame/Assets/Plugins/Android/gradleTemplate.properties.meta` — HOLD_CLEAN_BUILD_EVIDENCE
- `client/DominoGame/Assets/Plugins/Android/mainTemplate.gradle` — HOLD_CLEAN_BUILD_EVIDENCE
- `client/DominoGame/Assets/Plugins/Android/mainTemplate.gradle.meta` — HOLD_CLEAN_BUILD_EVIDENCE
- `client/DominoGame/Assets/Plugins/Android/settingsTemplate.gradle` — HOLD_CLEAN_BUILD_EVIDENCE
- `client/DominoGame/Assets/Plugins/Android/settingsTemplate.gradle.meta` — HOLD_CLEAN_BUILD_EVIDENCE
- `client/DominoGame/ProjectSettings/GvhProjectSettings.xml` — HOLD_BUILD_COHERENCE

### C (10)

- `client/DominoGame/Assets/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0/firebase-app-unity-13.16.0.pom` — HOLD_RESOLVER_COHERENCE
- `client/DominoGame/Assets/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0/firebase-auth-unity-13.16.0.pom` — HOLD_RESOLVER_COHERENCE
- `client/DominoGame/Assets/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0/firebase-firestore-unity-13.16.0.pom` — HOLD_RESOLVER_COHERENCE
- `client/DominoGame/Assets/Plugins/Android/FirebaseApp.androidlib.meta` — HOLD_CONFIG_POLICY_AND_BUILD_COHERENCE
- `client/DominoGame/Assets/Plugins/Android/FirebaseApp.androidlib/AndroidManifest.xml` — HOLD_CONFIG_POLICY_AND_BUILD_COHERENCE
- `client/DominoGame/Assets/Plugins/Android/FirebaseApp.androidlib/project.properties` — HOLD_CONFIG_POLICY_AND_BUILD_COHERENCE
- `client/DominoGame/Assets/Plugins/Android/FirebaseApp.androidlib/res/values/google-services.xml` — HOLD_CONFIG_POLICY_AND_BUILD_COHERENCE
- `client/DominoGame/Assets/StreamingAssets/google-services-desktop.json` — HOLD_CONFIG_POLICY_AND_BUILD_COHERENCE
- `client/DominoGame/Assets/StreamingAssets/google-services-desktop.json.meta` — HOLD_CONFIG_POLICY_AND_BUILD_COHERENCE
- `client/DominoGame/Assets/google-services.json` — HOLD_CONFIG_POLICY_AND_BUILD_COHERENCE

### D (1)

- `client/DominoGame/ProjectSettings/ProjectSettings.asset` — EXCLUDE_AS_IS_SPLIT_REQUIRED

### E (7)

- `client/Validation/S1_4_SOCIAL_PRESENCE_REPORT.md` — CANDIDATE_REPORT_CHECKPOINT
- `client/Validation/SOCIAL_1A_ELIGIBLE_USERS_REPORT.md` — CANDIDATE_REPORT_CHECKPOINT
- `client/Validation/SOCIAL_1B_TEST_SESSION_RECOVERY_REPORT.md` — CANDIDATE_REPORT_CHECKPOINT
- `client/Validation/SOCIAL_1C_FIVE_FRIEND_VALIDATION_REPORT.md` — CANDIDATE_REPORT_CHECKPOINT
- `client/Validation/SOCIAL_1D_AUTH_VALIDATOR_CONTRACT_REVIEW.md` — CANDIDATE_REPORT_CHECKPOINT
- `client/Validation/SOCIAL_1_FIVE_FRIEND_VALIDATION_REPORT.md` — CANDIDATE_REPORT_CHECKPOINT
- `client/Validation/SOCIAL_HTTP_503_DIAGNOSIS.md` — CANDIDATE_REPORT_CHECKPOINT

### F (66)

- `client/Validation/InitializeServer7LoadStorage.ps1` — CANDIDATE_SERVER7_CHECKPOINT
- `client/Validation/ObserveServer7Recovery.py` — CANDIDATE_SERVER7_CHECKPOINT
- `client/Validation/RunServer7Coordinator.ps1` — CANDIDATE_SERVER7_CHECKPOINT
- `client/Validation/RunServer7Load.py` — CANDIDATE_SERVER7_CHECKPOINT
- `client/Validation/S707TimingAgent.java` — CANDIDATE_SERVER7_CHECKPOINT
- `client/Validation/S707TimingAnalysis.py` — CANDIDATE_SERVER7_CHECKPOINT
- `client/Validation/S707TimingAnalysisTests.py` — CANDIDATE_SERVER7_CHECKPOINT
- `client/Validation/S708TimingAgent.java` — CANDIDATE_SERVER7_CHECKPOINT
- `client/Validation/S708TimingAnalysis.py` — CANDIDATE_SERVER7_CHECKPOINT
- `client/Validation/S708TimingAnalysisTests.py` — CANDIDATE_SERVER7_CHECKPOINT
- `client/Validation/S721ConnectOnly.kt` — CANDIDATE_SERVER7_CHECKPOINT
- `client/Validation/S721ConnectOnlyTest.kt` — CANDIDATE_SERVER7_CHECKPOINT
- `client/Validation/S721JvmAgent.java` — CANDIDATE_SERVER7_CHECKPOINT
- `client/Validation/S7_04B_DEPLOYMENT_RECONCILIATION_REPORT.md` — CANDIDATE_SERVER7_CHECKPOINT
- `client/Validation/S7_04_CHECKPOINT_DEPLOYMENT_REVIEW.md` — CANDIDATE_SERVER7_CHECKPOINT
- `client/Validation/S7_05B_SINGLETON_RECONCILIATION_REPORT.md` — CANDIDATE_SERVER7_CHECKPOINT
- `client/Validation/S7_05_RESIDUAL_LOAD_MATCH_DIAGNOSIS.md` — CANDIDATE_SERVER7_CHECKPOINT
- `client/Validation/S7_06_ACK_LATENCY_DIAGNOSIS.md` — CANDIDATE_SERVER7_CHECKPOINT
- `client/Validation/S7_07_ACK_PHASE_TIMING_REPORT.md` — CANDIDATE_SERVER7_CHECKPOINT
- `client/Validation/S7_08_FIRESTORE_CRITICAL_PATH_REPORT.md` — CANDIDATE_SERVER7_CHECKPOINT
- `client/Validation/S7_09_FIRESTORE_TRANSACTION_READ_REVIEW.md` — CANDIDATE_SERVER7_CHECKPOINT
- `client/Validation/S7_11_FIRESTORE_READ_ROUND_REMOTE_AB_REPORT.md` — CANDIDATE_SERVER7_CHECKPOINT
- `client/Validation/S7_12_CAPACITY_R3_REPORT.md` — CANDIDATE_SERVER7_CHECKPOINT
- `client/Validation/S7_13_POST_OPTIMIZATION_LATENCY_REVIEW.md` — CANDIDATE_SERVER7_CHECKPOINT
- `client/Validation/S7_14_AUTHORITATIVE_REVISION_DESIGN_REVIEW.md` — CANDIDATE_SERVER7_CHECKPOINT
- `client/Validation/S7_15_RECEIPT_VS_GET_ALL_TIMING_REPORT.md` — CANDIDATE_SERVER7_CHECKPOINT
- `client/Validation/S7_16_MATCHMAKING_NETWORK_REVIEW.md` — CANDIDATE_SERVER7_CHECKPOINT
- `client/Validation/S7_17_MATCH_CREATION_PHASE_TIMING_REPORT.md` — CANDIDATE_SERVER7_CHECKPOINT
- `client/Validation/S7_18_LOCAL_WRITE_PREPARATION_REVIEW.md` — CANDIDATE_SERVER7_CHECKPOINT
- `client/Validation/S7_19_200_USER_EXPLORATORY_STRESS_REPORT.md` — CANDIDATE_SERVER7_CHECKPOINT
- `client/Validation/S7_20_200_USER_CONCURRENCY_GAP_REVIEW.md` — CANDIDATE_SERVER7_CHECKPOINT
- `client/Validation/S7_21_200_CONNECTED_USERS_HOLD_REPORT.md` — CANDIDATE_SERVER7_CHECKPOINT
- `client/Validation/S7_22_PROGRESSIVE_GAMEPLAY_REPORT.md` — CANDIDATE_SERVER7_CHECKPOINT
- `client/Validation/S7_23_CURRENT_RESOURCE_CAPACITY_REPORT.md` — CANDIDATE_SERVER7_CHECKPOINT
- `client/Validation/SERVER_7B_R2_100_REPORT.md` — CANDIDATE_SERVER7_CHECKPOINT
- `client/Validation/SERVER_7P_LOAD_IDENTITIES_REPORT.md` — CANDIDATE_SERVER7_CHECKPOINT
- `client/Validation/SERVER_7_CAPACITY_100_REPORT.md` — CANDIDATE_SERVER7_CHECKPOINT
- `client/Validation/Server7GroupFixture.ps1` — CANDIDATE_SERVER7_CHECKPOINT
- `client/Validation/Server7MetadataGet.java` — CANDIDATE_SERVER7_CHECKPOINT
- `client/Validation/Server7PreserveServer6.py` — CANDIDATE_SERVER7_CHECKPOINT
- `client/Validation/Server7Provision.py` — CANDIDATE_SERVER7_CHECKPOINT
- `client/Validation/Server7ProvisionTests.py` — CANDIDATE_SERVER7_CHECKPOINT
- `client/Validation/Server7ReadOnlyReview.py` — CANDIDATE_SERVER7_CHECKPOINT
- `client/Validation/Server7ReplaySample.py` — CANDIDATE_SERVER7_CHECKPOINT
- `client/Validation/Server7ResourceObserver.py` — CANDIDATE_SERVER7_CHECKPOINT
- `client/Validation/Server7Supervisor.ps1` — CANDIDATE_SERVER7_CHECKPOINT
- `client/Validation/Server7SupervisorTests.ps1` — CANDIDATE_SERVER7_CHECKPOINT
- `client/Validation/SummarizeServer7Load.py` — CANDIDATE_SERVER7_CHECKPOINT
- `server/domino/src/main/kotlin/com/teamfho/domino/matchmaking/MatchCreationTiming.kt` — CANDIDATE_SERVER7_CHECKPOINT
- `server/domino/src/main/kotlin/com/teamfho/domino/matchmaking/MatchmakingService.kt` — CANDIDATE_SERVER7_CHECKPOINT
- `server/domino/src/main/kotlin/com/teamfho/domino/online/OnlineConfiguration.kt` — CANDIDATE_SERVER7_CHECKPOINT
- `server/domino/src/main/kotlin/com/teamfho/domino/online/OnlineMatchService.kt` — CANDIDATE_SERVER7_CHECKPOINT
- `server/domino/src/main/kotlin/com/teamfho/domino/online/OnlineRepository.kt` — CANDIDATE_SERVER7_CHECKPOINT
- `server/domino/src/main/kotlin/com/teamfho/domino/realtime/ConnectionOutbound.kt` — CANDIDATE_SERVER7_CHECKPOINT
- `server/domino/src/main/kotlin/com/teamfho/domino/realtime/FirestorePhaseTiming.kt` — CANDIDATE_SERVER7_CHECKPOINT
- `server/domino/src/test/kotlin/com/teamfho/domino/matchmaking/MatchCreationTimingTest.kt` — CANDIDATE_SERVER7_CHECKPOINT
- `server/domino/src/test/kotlin/com/teamfho/domino/online/GroupedCommandReadsEmulatorTests.kt` — CANDIDATE_SERVER7_CHECKPOINT
- `server/domino/src/test/kotlin/com/teamfho/domino/realtime/ConnectionOutboundTests.kt` — CANDIDATE_SERVER7_CHECKPOINT
- `server/domino/src/test/kotlin/com/teamfho/domino/realtime/FirestoreReadRoundTimingTest.kt` — CANDIDATE_SERVER7_CHECKPOINT
- `server/domino/tools/bot-swarm/build.gradle.kts` — CANDIDATE_SERVER7_CHECKPOINT
- `server/domino/tools/bot-swarm/src/main/kotlin/com/teamfho/swarm/LoadAuthSample.kt` — CANDIDATE_SERVER7_CHECKPOINT
- `server/domino/tools/bot-swarm/src/main/kotlin/com/teamfho/swarm/Metrics.kt` — CANDIDATE_SERVER7_CHECKPOINT
- `server/domino/tools/bot-swarm/src/main/kotlin/com/teamfho/swarm/SimulatedClient.kt` — CANDIDATE_SERVER7_CHECKPOINT
- `server/domino/tools/bot-swarm/src/main/kotlin/com/teamfho/swarm/Transport.kt` — CANDIDATE_SERVER7_CHECKPOINT
- `server/domino/tools/bot-swarm/src/provision/kotlin/com/teamfho/swarm/Provision.kt` — CANDIDATE_SERVER7_CHECKPOINT
- `server/domino/tools/bot-swarm/src/provision/kotlin/com/teamfho/swarm/ReviewRun.kt` — CANDIDATE_SERVER7_CHECKPOINT

### G (3)

- `client/DominoGame/Assets/AddressableAssetsData/ProfileDataSourceSettings.asset` — CANDIDATE_UI_CONFIG
- `client/DominoGame/Assets/AddressableAssetsData/ProfileDataSourceSettings.asset.meta` — CANDIDATE_UI_CONFIG
- `client/DominoGame/Assets/_Domino/Localization/Localization Settings.asset` — EXCLUDE_SERIALIZATION_CHURN

### H (79)

- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/query/client-agp/cache-v2` — EXCLUDE_GENERATED
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/query/client-agp/cmakeFiles-v1` — EXCLUDE_GENERATED
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/query/client-agp/codemodel-v2` — EXCLUDE_GENERATED
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/cache-v2-2c0909d0b4389f2443c3.json` — EXCLUDE_GENERATED
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/cmakeFiles-v1-2afea77556dece6ed3b6.json` — EXCLUDE_GENERATED
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/codemodel-v2-56ef99f20c5d90a856eb.json` — EXCLUDE_GENERATED
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/directory-.-RelWithDebInfo-d0094a50bb2071803777.json` — EXCLUDE_GENERATED
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/directory-FramePacing-RelWithDebInfo-7f9c8865fd027a154c90.json` — EXCLUDE_GENERATED
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/index-2026-09-20T07-54-05-0123.json` — EXCLUDE_GENERATED
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/target-swappywrapper-RelWithDebInfo-de42165ac0b744ec5a6b.json` — EXCLUDE_GENERATED
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.ninja_deps` — EXCLUDE_GENERATED
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.ninja_log` — EXCLUDE_GENERATED
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeCache.txt` — EXCLUDE_GENERATED
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeCCompiler.cmake` — EXCLUDE_GENERATED
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeCXXCompiler.cmake` — EXCLUDE_GENERATED
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeDetermineCompilerABI_C.bin` — EXCLUDE_GENERATED
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeDetermineCompilerABI_CXX.bin` — EXCLUDE_GENERATED
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeSystem.cmake` — EXCLUDE_GENERATED
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CompilerIdC/CMakeCCompilerId.c` — EXCLUDE_GENERATED
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CompilerIdC/CMakeCCompilerId.o` — EXCLUDE_GENERATED
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CompilerIdCXX/CMakeCXXCompilerId.cpp` — EXCLUDE_GENERATED
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CompilerIdCXX/CMakeCXXCompilerId.o` — EXCLUDE_GENERATED
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/TargetDirectories.txt` — EXCLUDE_GENERATED
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/cmake.check_cache` — EXCLUDE_GENERATED
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/rules.ninja` — EXCLUDE_GENERATED
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/FramePacing/CMakeFiles/swappywrapper.dir/UnitySwappyWrapper.cpp.o` — EXCLUDE_GENERATED
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/FramePacing/cmake_install.cmake` — EXCLUDE_GENERATED
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/additional_project_files.txt` — EXCLUDE_GENERATED
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/android_gradle_build.json` — EXCLUDE_GENERATED
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/android_gradle_build_mini.json` — EXCLUDE_GENERATED
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/build.ninja` — EXCLUDE_GENERATED
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/build_file_index.txt` — EXCLUDE_GENERATED
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/cmake_install.cmake` — EXCLUDE_GENERATED
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/compile_commands.json` — EXCLUDE_GENERATED
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/compile_commands.json.bin` — EXCLUDE_GENERATED
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/configure_fingerprint.bin` — EXCLUDE_GENERATED
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/metadata_generation_command.txt` — EXCLUDE_GENERATED
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/prefab_config.json` — EXCLUDE_GENERATED
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/symbol_folder_index.txt` — EXCLUDE_GENERATED
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/hash_key.txt` — EXCLUDE_GENERATED
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/prefab/arm64-v8a/prefab/lib/aarch64-linux-android/cmake/games-frame-pacing/games-frame-pacingConfig.cmake` — EXCLUDE_GENERATED
- `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/prefab/arm64-v8a/prefab/lib/aarch64-linux-android/cmake/games-frame-pacing/games-frame-pacingConfigVersion.cmake` — EXCLUDE_GENERATED
- `client/DominoGame/.utmp/tools/release/arm64-v8a/compile_commands.json` — EXCLUDE_GENERATED
- `client/DominoGame/Assets/AddressableAssetsData/Android.meta` — EXCLUDE_GENERATED
- `client/DominoGame/Assets/AddressableAssetsData/Android/addressables_content_state.bin` — EXCLUDE_GENERATED
- `client/DominoGame/Assets/AddressableAssetsData/Android/addressables_content_state.bin.meta` — EXCLUDE_GENERATED
- `client/DominoGame/Assets/GeneratedLocalRepo.meta` — EXCLUDE_PENDING_REGENERATION_PLAN
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase.meta` — EXCLUDE_PENDING_REGENERATION_PLAN
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository.meta` — EXCLUDE_PENDING_REGENERATION_PLAN
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com.meta` — EXCLUDE_PENDING_REGENERATION_PLAN
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google.meta` — EXCLUDE_PENDING_REGENERATION_PLAN
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase.meta` — EXCLUDE_PENDING_REGENERATION_PLAN
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity.meta` — EXCLUDE_PENDING_REGENERATION_PLAN
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0.meta` — EXCLUDE_PENDING_REGENERATION_PLAN
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0/firebase-app-unity-13.16.0.aar` — EXCLUDE_PENDING_REGENERATION_PLAN
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0/firebase-app-unity-13.16.0.aar.meta` — EXCLUDE_PENDING_REGENERATION_PLAN
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0/firebase-app-unity-13.16.0.pom` — EXCLUDE_PENDING_REGENERATION_PLAN
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0/firebase-app-unity-13.16.0.pom.meta` — EXCLUDE_PENDING_REGENERATION_PLAN
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity.meta` — EXCLUDE_PENDING_REGENERATION_PLAN
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0.meta` — EXCLUDE_PENDING_REGENERATION_PLAN
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0/firebase-auth-unity-13.16.0.aar` — EXCLUDE_PENDING_REGENERATION_PLAN
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0/firebase-auth-unity-13.16.0.aar.meta` — EXCLUDE_PENDING_REGENERATION_PLAN
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0/firebase-auth-unity-13.16.0.pom` — EXCLUDE_PENDING_REGENERATION_PLAN
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0/firebase-auth-unity-13.16.0.pom.meta` — EXCLUDE_PENDING_REGENERATION_PLAN
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity.meta` — EXCLUDE_PENDING_REGENERATION_PLAN
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0.meta` — EXCLUDE_PENDING_REGENERATION_PLAN
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0/firebase-firestore-unity-13.16.0.aar` — EXCLUDE_PENDING_REGENERATION_PLAN
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0/firebase-firestore-unity-13.16.0.aar.meta` — EXCLUDE_PENDING_REGENERATION_PLAN
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0/firebase-firestore-unity-13.16.0.pom` — EXCLUDE_PENDING_REGENERATION_PLAN
- `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0/firebase-firestore-unity-13.16.0.pom.meta` — EXCLUDE_PENDING_REGENERATION_PLAN
- `client/DominoGame/ProjectSettings/AndroidResolverDependencies.xml` — EXCLUDE_GENERATED
- `client/DominoGame/ProjectSettings/ScriptableBuildPipeline.json` — EXCLUDE_GENERATED
- `client/Validation/__pycache__/CapacityCoordinator.cpython-312.pyc` — EXCLUDE_GENERATED
- `client/Validation/__pycache__/CapacityDiscoveryTests.cpython-312.pyc` — EXCLUDE_GENERATED
- `client/Validation/__pycache__/CapacityInstrumentationTests.cpython-312.pyc` — EXCLUDE_GENERATED
- `client/Validation/__pycache__/CapacityMetrics.cpython-312.pyc` — EXCLUDE_GENERATED
- `client/Validation/__pycache__/CapacityRegistry.cpython-312.pyc` — EXCLUDE_GENERATED
- `client/Validation/__pycache__/S707TimingAnalysis.cpython-312.pyc` — EXCLUDE_GENERATED
- `client/Validation/__pycache__/S708TimingAnalysis.cpython-312.pyc` — EXCLUDE_GENERATED

### I (0)

None.

### J (0)

None.
