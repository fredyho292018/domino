# INFRA-1 â€” Backend infrastructure + CI validation

Current status after INFRA-1R: PASS — ready for review/checkpoint. Original INFRA-1 failure is retained below.

Branch: main. Source before: `163282b1b29fdd7fa9ff1317f699b00e9b3d4417`.
SERVER-3 runtime is untouched: no SSH, container restart, Compose change or cloud mutation in INFRA-1.

## Implementation

Sanitized single-instance TEST Compose, explicit secret paths, default Spring profile, loopback-only API,
private Redis and separate API egress. Existing SERVER-3 Dockerfile and deny-by-default Docker context preserved.
No standalone local API Compose because booting the runtime requires Firebase and activates listeners.

CI: PR/main path filters -> Java 21 / Gradle cache -> secret, shell and Compose checks -> compile ->
isolated Redis and unit tests -> existing Firestore Emulator -> bootJar -> linux/amd64 image -> non-root
and application credential checks. No registry publish, automatic CD, SSH or cloud secrets.

## Validation environment and diagnostic history

Local equivalents execute in a disposable Java 21 Linux runner with a read-only source mount and separate
Redis network namespace. No service-account/ADC secrets mounted. Docker socket is available only to this
validation runner for the existing OS-worker test and image checks, never to the application image.

Initial dependency download failed with Maven TLS handshake errors. Reused existing Gradle dependency
cache (excluding locks); offline compilation passed. CI still downloads normal public dependencies.

First test attempt identified two infrastructure configuration errors:
- Setting FIREBASE_PROJECT_ID interfered with the missing-project validation test. Removed from workflow.
- An existing OS-worker test invokes `docker exec domino-redis`. Hosted CI now creates that exact disposable
  name. For local validation only, a CLI alias routes that name to domino-infra1-redis to preserve the existing
  developer container. Initial attempt addressed the existing local Redis using its random test namespace;
  it found no matching queued test users and failed its assertion. No production Redis or server was contacted.

No test assertions or application source were modified. Corrected normal suite: 645 discovered, 644 passed, 1 skipped, 0 failures. Simulated-client unit suite: 25 passed. The earlier attempt had already executed those 25 successfully; the corrected invocation reused that unchanged result.

## Blocking clean-checkout defect

`ReplayEmulatorTests.kt:20-21` requires `build/swarm-emulator/retained-matches` and fails when absent.
The archives exist only in ignored developer output, not Git. The original wrapper therefore cannot run
this test on a fresh CI checkout. The test was retained and its failure was not suppressed.

Recommended follow-up for review: a deterministic, versioned fixture-generation strategy for replay
integration coverage, independent of a live backend or swarm. Do not silently publish old private-hand
runtime archives. No change to application/test code or data copying was made in INFRA-1.

Fail-fast respected: fresh bootJar/image build stages were not run after Emulator failed. Existing
SERVER-3 image was independently checked successfully (linux/amd64, UID 10001, exec Java, port 8080,
application artifact secret indicators absent). This does not substitute for an end-to-end green CI run.

## Test inventory / exclusions

Normal task includes unit/in-memory/mocked Firebase and all five Redis opt-ins. `:bot-swarm:test` is unit
validation only, not a swarm run. EMULATOR suite uses existing 1.22.0 JAR and known checksum, localhost:18085,
EmulatorCredentials/demo project. REAL_FIRESTORE is excluded by established task; real-cloud opt-in false.
M5 Unity-generated 100-trace differential remains explicitly skipped; no Unity or real Auth/Swarm/chaos
harness is launched. See infrastructure/README.md for the complete grouping and flags.

## Security and limitations

Secret scan covers tracked files and pending infrastructure plus packaged application resources/classes;
three synthetic detection checks and safe example check pass. It is not a comprehensive dependency or
entropy-based audit. Compose validation uses empty temporary placeholders, never host secrets.
GitHub-hosted workflow has not been pushed or triggered; local execution is the available evidence.

SPRING_BOOT_INSTANCE_COUNT=1
MULTI_INSTANCE_GAMEPLAY_SCALE_OUT=BLOCKED
RETENTION_CLEANUP_READY=NO
PUBLIC_PRODUCTION_READY=NO
IMAGE_PUBLISHED=NO
REAL_FIRESTORE_CALLS=0
REAL_FIREBASE_AUTH_CALLS=0
PRODUCTION_REDIS_CALLS=0
GCP_RESOURCES_CREATED=0
FIREBASE_RESOURCES_CREATED=0
CLOUDFLARE_RESOURCES_CREATED=0
COMMIT=NONE
PUSH=NONE
DEPLOY=NONE

## Exact changed-file classification

Dockerfile/.dockerignore are pre-existing SERVER-3 deployment files adopted into INFRA-1 scope, unchanged.
The other 97 baseline files are pre-existing user work. Local validation caches/logs are outside Git in the
throwaway runner and OS temporary directory; no generated runtime files are added to source.

| Classification | Path |
|---|---|
| INFRA_1_INTENTIONAL | `.github/workflows/backend-ci.yml` |
| INFRA_1_INTENTIONAL | `client/Validation/INFRA_1_BACKEND_INFRASTRUCTURE_CI_REPORT.md` |
| INFRA_1_INTENTIONAL | `infrastructure/.gitignore` |
| INFRA_1_INTENTIONAL | `infrastructure/README.md` |
| INFRA_1_INTENTIONAL | `infrastructure/cloudflare/README.md` |
| INFRA_1_INTENTIONAL | `infrastructure/docs/architecture.md` |
| INFRA_1_INTENTIONAL | `infrastructure/docs/deployment.md` |
| INFRA_1_INTENTIONAL | `infrastructure/docs/rollback.md` |
| INFRA_1_INTENTIONAL | `infrastructure/docs/security.md` |
| INFRA_1_INTENTIONAL | `infrastructure/scripts/health-check.sh` |
| INFRA_1_INTENTIONAL | `infrastructure/scripts/run-emulator-tests.sh` |
| INFRA_1_INTENTIONAL | `infrastructure/scripts/secret-scan.py` |
| INFRA_1_INTENTIONAL | `infrastructure/scripts/verify-compose.sh` |
| INFRA_1_INTENTIONAL | `infrastructure/scripts/verify-image.sh` |
| INFRA_1_INTENTIONAL | `infrastructure/test/README.md` |
| INFRA_1_INTENTIONAL | `infrastructure/test/compose.env.example` |
| INFRA_1_INTENTIONAL | `infrastructure/test/compose.yaml` |
| INFRA_1_INTENTIONAL | `server/domino/.dockerignore` |
| INFRA_1_INTENTIONAL | `server/domino/Dockerfile` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/query/client-agp/cache-v2` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/query/client-agp/cmakeFiles-v1` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/query/client-agp/codemodel-v2` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/cache-v2-2c0909d0b4389f2443c3.json` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/cmakeFiles-v1-2afea77556dece6ed3b6.json` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/codemodel-v2-56ef99f20c5d90a856eb.json` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/directory-.-RelWithDebInfo-d0094a50bb2071803777.json` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/directory-FramePacing-RelWithDebInfo-7f9c8865fd027a154c90.json` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/index-2026-09-20T07-54-05-0123.json` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/target-swappywrapper-RelWithDebInfo-de42165ac0b744ec5a6b.json` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.ninja_deps` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.ninja_log` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeCache.txt` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeCCompiler.cmake` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeCXXCompiler.cmake` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeDetermineCompilerABI_C.bin` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeDetermineCompilerABI_CXX.bin` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeSystem.cmake` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CompilerIdC/CMakeCCompilerId.c` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CompilerIdC/CMakeCCompilerId.o` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CompilerIdCXX/CMakeCXXCompilerId.cpp` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CompilerIdCXX/CMakeCXXCompilerId.o` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/TargetDirectories.txt` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/cmake.check_cache` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/rules.ninja` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/FramePacing/CMakeFiles/swappywrapper.dir/UnitySwappyWrapper.cpp.o` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/FramePacing/cmake_install.cmake` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/additional_project_files.txt` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/android_gradle_build.json` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/android_gradle_build_mini.json` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/build.ninja` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/build_file_index.txt` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/cmake_install.cmake` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/compile_commands.json` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/compile_commands.json.bin` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/configure_fingerprint.bin` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/metadata_generation_command.txt` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/prefab_config.json` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/symbol_folder_index.txt` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/hash_key.txt` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/prefab/arm64-v8a/prefab/lib/aarch64-linux-android/cmake/games-frame-pacing/games-frame-pacingConfig.cmake` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/prefab/arm64-v8a/prefab/lib/aarch64-linux-android/cmake/games-frame-pacing/games-frame-pacingConfigVersion.cmake` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/tools/release/arm64-v8a/compile_commands.json` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/AddressableAssetsData/Android.meta` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/AddressableAssetsData/Android/addressables_content_state.bin` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/AddressableAssetsData/Android/addressables_content_state.bin.meta` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/AddressableAssetsData/ProfileDataSourceSettings.asset` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/AddressableAssetsData/ProfileDataSourceSettings.asset.meta` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0/firebase-app-unity-13.16.0.pom` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0/firebase-auth-unity-13.16.0.pom` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0/firebase-firestore-unity-13.16.0.pom` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/GeneratedLocalRepo.meta` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase.meta` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository.meta` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com.meta` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google.meta` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase.meta` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity.meta` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0.meta` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0/firebase-app-unity-13.16.0.aar` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0/firebase-app-unity-13.16.0.aar.meta` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0/firebase-app-unity-13.16.0.pom` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0/firebase-app-unity-13.16.0.pom.meta` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity.meta` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0.meta` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0/firebase-auth-unity-13.16.0.aar` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0/firebase-auth-unity-13.16.0.aar.meta` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0/firebase-auth-unity-13.16.0.pom` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0/firebase-auth-unity-13.16.0.pom.meta` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity.meta` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0.meta` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0/firebase-firestore-unity-13.16.0.aar` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0/firebase-firestore-unity-13.16.0.aar.meta` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0/firebase-firestore-unity-13.16.0.pom` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0/firebase-firestore-unity-13.16.0.pom.meta` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/Plugins/Android/FirebaseApp.androidlib.meta` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/Plugins/Android/FirebaseApp.androidlib/AndroidManifest.xml` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/Plugins/Android/FirebaseApp.androidlib/project.properties` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/Plugins/Android/FirebaseApp.androidlib/res/values/google-services.xml` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/Plugins/Android/GoogleMobileAdsPlugin.androidlib/AndroidManifest.xml` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/Plugins/Android/gradleTemplate.properties` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/Plugins/Android/gradleTemplate.properties.meta` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/Plugins/Android/mainTemplate.gradle` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/Plugins/Android/mainTemplate.gradle.meta` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/Plugins/Android/settingsTemplate.gradle` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/Plugins/Android/settingsTemplate.gradle.meta` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/StreamingAssets/google-services-desktop.json` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/StreamingAssets/google-services-desktop.json.meta` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/_Domino/Localization/Localization Settings.asset` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/_Domino/Resources/AdsSettings.asset` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/_Domino/Resources/ApiSettings.asset` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/google-services.json` |
| PREEXISTING_USER_FILE | `client/DominoGame/ProjectSettings/AndroidResolverDependencies.xml` |
| PREEXISTING_USER_FILE | `client/DominoGame/ProjectSettings/GvhProjectSettings.xml` |
| PREEXISTING_USER_FILE | `client/DominoGame/ProjectSettings/ProjectSettings.asset` |
| PREEXISTING_USER_FILE | `client/DominoGame/ProjectSettings/ScriptableBuildPipeline.json` |
| PREEXISTING_USER_FILE | `client/Validation/S1_4_SOCIAL_PRESENCE_REPORT.md` |

## Final validation evidence

| Check | Result |
|---|---|
| Linux Java 21 compilation | PASS (offline dependency cache after network failure) |
| Backend normal + all Redis groups | 644 PASS / 1 SKIPPED / 0 FAILED |
| Simulated-client unit tests | 25 PASS / 0 FAILED; no Swarm launched |
| Firestore Emulator | 45 PASS / 1 FAILED / 0 SKIPPED |
| Emulator failed test | ReplayEmulatorTests retained archive prerequisite |
| Concurrent Follow/Block Emulator test | PASS; no assertion/timeouts changed |
| Emulator cleanup | PASS; port 18085 closed |
| Compose syntax/security | PASS with synthetic empty files |
| Shell syntax | PASS, four shell scripts |
| Workflow YAML/structure | PASS; hosted execution NOT_RUN |
| Secret scan | PASS; three negative synthetic cases + one safe case |
| Existing image security | PASS; no application startup |
| Fresh bootJar/image build | NOT_RUN_AFTER_EMULATOR_FAILURE (fail-fast) |
| Disposable containers cleanup | PASS; existing developer Redis left running |

JUnit aggregate evidence: `client/Validation/Generated/INFRA1/results.json` (ignored, GENERATED_VALIDATION).
Validation runner image and OS temporary cache files are local generated artifacts, not source changes.
All 99 baseline hashes match, including the 97 unrelated user files and two adopted Docker files.
Gradle reports existing deprecated APIs for future Gradle 10 compatibility; no source changes made.

## Historical INFRA-1 blocked candidate (superseded by INFRA-1R below)

```text
BRANCH=main
SOURCE_SHA_BEFORE=163282b1b29fdd7fa9ff1317f699b00e9b3d4417
SOURCE_SHA_AFTER=163282b1b29fdd7fa9ff1317f699b00e9b3d4417
INFRA_1_IMPLEMENTED=YES_WITH_BLOCKING_VALIDATION_DEFECT
INFRA_1_SUCCESS=NO
INFRA_1_READY_FOR_CHECKPOINT=NO
DOCKERFILE=server/domino/Dockerfile
DOCKERIGNORE=server/domino/.dockerignore
INFRASTRUCTURE_DIRECTORY=infrastructure/
CI_WORKFLOW=.github/workflows/backend-ci.yml
REPORT=client/Validation/INFRA_1_BACKEND_INFRASTRUCTURE_CI_REPORT.md
INFRA_1_FILES_MODIFIED=19 intentional files; includes 2 unchanged adopted SERVER-3 files
PREEXISTING_USER_FILES=97 unrelated; 99 total baseline including Dockerfile/.dockerignore
PREEXISTING_USER_FILES_PRESERVED=YES
GENERATED_VALIDATION=client/Validation/Generated/INFRA1/results.json; disposable environment and OS temp files
UNEXPECTED_FILES=0
CI_TRIGGER_PULL_REQUEST=YES
CI_TRIGGER_MAIN=YES
CI_PATH_FILTERING=YES
CI_JAVA_VERSION=21
CI_GRADLE_WRAPPER=server/domino/gradlew (9.7.1)
CI_UNIT_TESTS=644 PASS / 1 SKIPPED; client-unit 25 PASS
CI_REDIS=ISOLATED_PINNED_CONTAINER; ALL_EXISTING_REDIS_OPT_INS_ENABLED
CI_FIRESTORE_EMULATOR=IMPLEMENTED; 45 PASS / 1 FAIL_MISSING_IGNORED_REPLAY_ARCHIVES
CI_REAL_FIRESTORE_CALLS=0
CI_REAL_PRODUCTION_FIRESTORE_CALLS=0
CI_REAL_FIREBASE_AUTH_CALLS=0
CI_BOOTJAR=CONFIGURED; FRESH_RUN_BLOCKED_BY_EMULATOR
CI_DOCKER_BUILD=CONFIGURED; FRESH_RUN_BLOCKED_BY_EMULATOR
CI_IMAGE_PLATFORM=linux/amd64
CI_IMAGE_NON_ROOT=PASS_EXISTING_IMAGE
CI_COMPOSE_VALIDATION=PASS
CI_SHELL_VALIDATION=PASS
CI_SECRET_SCAN=PASS_LIGHTWEIGHT_SCOPE
IMAGE_TAG_CONVENTION=cuban-domino-api:<FULL_GIT_SHA>
IMAGE_PUBLISHED=NO
REAL_SERVICE_ACCOUNT_IN_REPOSITORY=NO_INDICATORS_IN_SCANNED_SCOPE
REAL_SOCIAL_CURSOR_IN_REPOSITORY=NO_INDICATORS_IN_SCANNED_SCOPE
REAL_SSH_KEY_IN_REPOSITORY=NO_INDICATORS_IN_SCANNED_SCOPE
REAL_CLOUDFLARE_TOKEN_IN_REPOSITORY=NONE_INTRODUCED
SERVER3_API_RESTARTED=NO
SERVER3_REDIS_RESTARTED=NO
SERVER3_COMPOSE_MODIFIED=NO
SERVER3_RUNTIME_PRESERVED=YES
SPRING_BOOT_INSTANCE_COUNT=1
MULTI_INSTANCE_GAMEPLAY_SCALE_OUT=BLOCKED
RETENTION_CLEANUP_READY=NO
PUBLIC_PRODUCTION_READY=NO
GCP_RESOURCES_CREATED=0
FIREBASE_RESOURCES_CREATED=0
CLOUDFLARE_RESOURCES_CREATED=0
COMMIT=NONE
PUSH=NONE
DEPLOY=NONE
NEXT=REVIEW_REPRODUCIBLE_REPLAY_FIXTURES_BEFORE_INFRA_1_CHECKPOINT
```


## INFRA-1R — original failure and reproducible fixture correction

Original failing test: `com.teamfho.domino.match.ReplayEmulatorTests.retained swarm archives read only paging parity and cost on emulator()`.
Original failure: `Retained I3.1 exports required; no remote fallback`.
Required input previously: JSON archives in `server/domino/build/swarm-emulator/retained-matches`,
with match, runtime stateJson, rounds, ordered events and history for DUEL_1V1 and PARTNERS_2V2_ONLINE.
The test selected one archive per mode with event count closest to 500.

Origin: `SwarmEmulatorInspection.main` exports finished validation matches from the local
`demo-domino-swarm` Emulator after earlier I3.1 validation. This is **class C: validation evidence
accidentally reused as a fixture** (also derived runtime output), not a canonical golden fixture.
The developer worktree retains 62 JSON files / 14,378,009 bytes; no contents were imported into CI.
`git check-ignore -v server/domino/build/swarm-emulator/retained-matches/example.json` identifies
`server/domino/.gitignore:3:build/`. Ignore rules remain unchanged.

Chosen option A: reuse the source-controlled in-memory `ReplayFixture` from `ReplayTests.kt`.
Its natural ownership is test source, not client validation evidence or a versioned generated corpus.
The generator runs OnlineEngine to match completion with canonical catalog/rules, seed 9, Instant.EPOCH,
ordered synthetic players p0..p3, deterministic command IDs and engine sequence IDs; no UUID, clock,
locale-dependent input, filesystem path, external identity or network dependency supplies fixture state.
Two distinct fixed match IDs isolate the two persisted modes. Histories now retain the engine's
original completion outputs. No production engine/reducer/sequence code is changed.

All original replay assertions remain: manifest availability, final sequence count, zero replay writes,
board parity, score parity, and 45 history rows ordered with subsecond timestamp precision over three pages.
The missing-directory prerequisite is replaced by deterministic setup. Added checks enforce >250 events
per mode and repeat-generation equality of state/events/rounds/histories, contiguous sequence, finished status
and participant history count. No ignored archive fallback exists.

Fixtures are generated in memory: **0 data files, 0 versioned data bytes**. Inputs contain synthetic identities,
no credentials, tokens, email, real player data or machine paths. Existing source helper reused; no large corpus added.
Focused Linux Emulator execution: 1 PASS / 0 skipped / 0 failed, from exported HEAD plus intentional changes,
with no build directory initially. DUEL: 354 events, 2 pages; PARTNERS: 809 events, 4 pages.
The existing manifest counters report 361 and 816 reads respectively; replay writes remain zero.
These are test-counter observations, not production billing guarantees.

Clean-source method: `git archive HEAD` plus only intentional infrastructure and two test-file overlays.
No ignored repository files copied. Gradle dependency caches were reused, which contain libraries, not fixtures.
The pinned Emulator binary was downloaded by the CI script and checksum verified.
An isolated Git index was initialized inside the disposable source copy only for the secret scan; the user's
workspace index is untouched. All tests execute against local demo Emulator / disposable Redis with no ADC.
The local Docker CLI adapter redirects the pre-existing OS-worker test's hardcoded `domino-redis` name to
`domino-infra1-redis`, preserving the user's existing Redis. Hosted CI uses its configured exact name directly.
Linux job commands and flags otherwise match the workflow; initial offline compile was followed by the actual
workflow compile/test/Emulator sequence. Hosted GitHub Actions itself was not triggered (no push).

Windows independent clean-source Replay regression also passed; exact count is recorded below.
Full CI results and image results follow when complete.

### INFRA-1R regression observations

Normal Linux CI backend: 646 discovered = 645 PASS / 1 SKIPPED / 0 FAILED.
Simulated-client unit suite: 25 PASS / 0 SKIPPED / 0 FAILED (no Swarm process).
Windows clean-source ReplayTests: 6 PASS / 0 SKIPPED / 0 FAILED, Java 21, offline dependency cache.
Only skipped backend case: `PartnersOnlineTests.existing local engine differential full matches maintain every deal move pass score tie and target()`;
its existing DOMINO_M5_PARITY opt-in requires separate Unity-exported parity traces and remains documented in INFRA-1.
No new skip/exclude was introduced; ReplayEmulatorTests retains its original method name for failure traceability.
Production source diff is empty; user workspace staged diff is empty.
A full-worktree whitespace check observes pre-existing whitespace in AdsSettings.asset; it is preserved, not normalized.
The INFRA-1R two-test-file whitespace check passes.

### INFRA-1R final validation and packaging

- Linux full CI backend: **645 PASS / 1 SKIPPED / 0 FAILED**.
- Simulated-client unit tests: **25 PASS / 0 FAILED**.
- Focused clean-source Emulator replay: **1 PASS**.
- Full Firestore Emulator suite: **46 PASS / 0 SKIPPED / 0 FAILED** (4m50s).
- Windows clean-source Replay regression: **6 PASS**.
- Compile, secret indicator scan, shell syntax, Compose validation, bootJar and fresh image validation: **PASS**.
- CI workflow unchanged; no fixture preparation step needed. No tests excluded or assertions weakened.
- No hosted Actions run: local Linux command-sequence evidence only; local Redis name adapter described above.

Fresh bootJar SHA256: `057135581a20c7aacaea57ae0a499746387cad775f612cc4bed4d03ba02063fb`.
Fresh local image: `cuban-domino-api:163282b1b29fdd7fa9ff1317f699b00e9b3d4417`.
Image ID: `sha256:8ba81db7c109ef09d29354050b3d73eb93be189b0af2008b429923f8b4b86257`.
Verified linux/amd64, UID/GID 10001:10001, port 8080, expected entrypoint and Java 21.0.12.1.
No secret indicators found in scanned application artifact; this is not an exhaustive third-party dependency audit.
Local Docker used its legacy builder (deprecation warning); build and resulting image checks passed.
No API was started, image published, or remote deployment touched. The full-SHA tag identifies the current
base plus the reviewed uncommitted infrastructure/test changes; no new commit was invented.

Both disposable containers removed. Existing developer `domino-redis` remained running on loopback.
No remote SSH/cloud calls. No Swarm, real Firestore or real Firebase Auth. No application source changes.
All 99 initial file hashes preserved. All 97 unrelated user-local files remain untouched.
Single-instance gameplay limitation and deferred retention cleanup from INFRA-1 remain unchanged.

### Final exact changed-file classification (supersedes earlier table)

INFRA-1 has 19 intentional paths including 2 unchanged adopted Docker files.
INFRA-1R touches 4 paths: 2 test files and 2 existing INFRA-1 documents.
The union is 21 intentional paths (17 INFRA_1_INTENTIONAL + 4 INFRA_1R_REPLAY_FIXTURE),
plus 97 unrelated user files. No unexpected path and no workspace staging.

| Classification | Path |
|---|---|
| INFRA_1_INTENTIONAL | `.github/workflows/backend-ci.yml` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/query/client-agp/cache-v2` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/query/client-agp/cmakeFiles-v1` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/query/client-agp/codemodel-v2` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/cache-v2-2c0909d0b4389f2443c3.json` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/cmakeFiles-v1-2afea77556dece6ed3b6.json` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/codemodel-v2-56ef99f20c5d90a856eb.json` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/directory-.-RelWithDebInfo-d0094a50bb2071803777.json` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/directory-FramePacing-RelWithDebInfo-7f9c8865fd027a154c90.json` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/index-2026-09-20T07-54-05-0123.json` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/target-swappywrapper-RelWithDebInfo-de42165ac0b744ec5a6b.json` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.ninja_deps` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.ninja_log` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeCache.txt` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeCCompiler.cmake` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeCXXCompiler.cmake` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeDetermineCompilerABI_C.bin` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeDetermineCompilerABI_CXX.bin` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeSystem.cmake` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CompilerIdC/CMakeCCompilerId.c` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CompilerIdC/CMakeCCompilerId.o` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CompilerIdCXX/CMakeCXXCompilerId.cpp` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CompilerIdCXX/CMakeCXXCompilerId.o` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/TargetDirectories.txt` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/cmake.check_cache` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/rules.ninja` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/FramePacing/CMakeFiles/swappywrapper.dir/UnitySwappyWrapper.cpp.o` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/FramePacing/cmake_install.cmake` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/additional_project_files.txt` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/android_gradle_build.json` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/android_gradle_build_mini.json` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/build.ninja` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/build_file_index.txt` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/cmake_install.cmake` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/compile_commands.json` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/compile_commands.json.bin` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/configure_fingerprint.bin` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/metadata_generation_command.txt` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/prefab_config.json` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/symbol_folder_index.txt` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/hash_key.txt` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/prefab/arm64-v8a/prefab/lib/aarch64-linux-android/cmake/games-frame-pacing/games-frame-pacingConfig.cmake` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/prefab/arm64-v8a/prefab/lib/aarch64-linux-android/cmake/games-frame-pacing/games-frame-pacingConfigVersion.cmake` |
| PREEXISTING_USER_FILE | `client/DominoGame/.utmp/tools/release/arm64-v8a/compile_commands.json` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/AddressableAssetsData/Android.meta` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/AddressableAssetsData/Android/addressables_content_state.bin` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/AddressableAssetsData/Android/addressables_content_state.bin.meta` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/AddressableAssetsData/ProfileDataSourceSettings.asset` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/AddressableAssetsData/ProfileDataSourceSettings.asset.meta` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0/firebase-app-unity-13.16.0.pom` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0/firebase-auth-unity-13.16.0.pom` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0/firebase-firestore-unity-13.16.0.pom` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/GeneratedLocalRepo.meta` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase.meta` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository.meta` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com.meta` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google.meta` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase.meta` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity.meta` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0.meta` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0/firebase-app-unity-13.16.0.aar` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0/firebase-app-unity-13.16.0.aar.meta` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0/firebase-app-unity-13.16.0.pom` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0/firebase-app-unity-13.16.0.pom.meta` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity.meta` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0.meta` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0/firebase-auth-unity-13.16.0.aar` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0/firebase-auth-unity-13.16.0.aar.meta` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0/firebase-auth-unity-13.16.0.pom` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0/firebase-auth-unity-13.16.0.pom.meta` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity.meta` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0.meta` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0/firebase-firestore-unity-13.16.0.aar` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0/firebase-firestore-unity-13.16.0.aar.meta` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0/firebase-firestore-unity-13.16.0.pom` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0/firebase-firestore-unity-13.16.0.pom.meta` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/Plugins/Android/FirebaseApp.androidlib.meta` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/Plugins/Android/FirebaseApp.androidlib/AndroidManifest.xml` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/Plugins/Android/FirebaseApp.androidlib/project.properties` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/Plugins/Android/FirebaseApp.androidlib/res/values/google-services.xml` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/Plugins/Android/GoogleMobileAdsPlugin.androidlib/AndroidManifest.xml` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/Plugins/Android/gradleTemplate.properties` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/Plugins/Android/gradleTemplate.properties.meta` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/Plugins/Android/mainTemplate.gradle` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/Plugins/Android/mainTemplate.gradle.meta` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/Plugins/Android/settingsTemplate.gradle` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/Plugins/Android/settingsTemplate.gradle.meta` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/StreamingAssets/google-services-desktop.json` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/StreamingAssets/google-services-desktop.json.meta` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/_Domino/Localization/Localization Settings.asset` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/_Domino/Resources/AdsSettings.asset` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/_Domino/Resources/ApiSettings.asset` |
| PREEXISTING_USER_FILE | `client/DominoGame/Assets/google-services.json` |
| PREEXISTING_USER_FILE | `client/DominoGame/ProjectSettings/AndroidResolverDependencies.xml` |
| PREEXISTING_USER_FILE | `client/DominoGame/ProjectSettings/GvhProjectSettings.xml` |
| PREEXISTING_USER_FILE | `client/DominoGame/ProjectSettings/ProjectSettings.asset` |
| PREEXISTING_USER_FILE | `client/DominoGame/ProjectSettings/ScriptableBuildPipeline.json` |
| INFRA_1R_REPLAY_FIXTURE | `client/Validation/INFRA_1_BACKEND_INFRASTRUCTURE_CI_REPORT.md` |
| PREEXISTING_USER_FILE | `client/Validation/S1_4_SOCIAL_PRESENCE_REPORT.md` |
| INFRA_1_INTENTIONAL | `infrastructure/.gitignore` |
| INFRA_1R_REPLAY_FIXTURE | `infrastructure/README.md` |
| INFRA_1_INTENTIONAL | `infrastructure/cloudflare/README.md` |
| INFRA_1_INTENTIONAL | `infrastructure/docs/architecture.md` |
| INFRA_1_INTENTIONAL | `infrastructure/docs/deployment.md` |
| INFRA_1_INTENTIONAL | `infrastructure/docs/rollback.md` |
| INFRA_1_INTENTIONAL | `infrastructure/docs/security.md` |
| INFRA_1_INTENTIONAL | `infrastructure/scripts/health-check.sh` |
| INFRA_1_INTENTIONAL | `infrastructure/scripts/run-emulator-tests.sh` |
| INFRA_1_INTENTIONAL | `infrastructure/scripts/secret-scan.py` |
| INFRA_1_INTENTIONAL | `infrastructure/scripts/verify-compose.sh` |
| INFRA_1_INTENTIONAL | `infrastructure/scripts/verify-image.sh` |
| INFRA_1_INTENTIONAL | `infrastructure/test/README.md` |
| INFRA_1_INTENTIONAL | `infrastructure/test/compose.env.example` |
| INFRA_1_INTENTIONAL | `infrastructure/test/compose.yaml` |
| INFRA_1_INTENTIONAL | `server/domino/.dockerignore` |
| INFRA_1_INTENTIONAL | `server/domino/Dockerfile` |
| INFRA_1R_REPLAY_FIXTURE | `server/domino/src/test/kotlin/com/teamfho/domino/match/ReplayEmulatorTests.kt` |
| INFRA_1R_REPLAY_FIXTURE | `server/domino/src/test/kotlin/com/teamfho/domino/match/ReplayTests.kt` |

Generated validation evidence (ignored, not test inputs):

- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/backend/binary/output-events.bin`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/backend/binary/results-generic.bin`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/backend/TEST-com.teamfho.domino.catalog.DuelCatalogTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/backend/TEST-com.teamfho.domino.catalog.GameCatalogApiTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/backend/TEST-com.teamfho.domino.catalog.GameCatalogTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/backend/TEST-com.teamfho.domino.catalog.GameCatalogV3Test.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/backend/TEST-com.teamfho.domino.config.FirebaseAdminConfigurationTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/backend/TEST-com.teamfho.domino.DominoApplicationTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/backend/TEST-com.teamfho.domino.economy.reward.AdMobSsvTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/backend/TEST-com.teamfho.domino.economy.reward.MonetizationPolicyTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/backend/TEST-com.teamfho.domino.economy.reward.PersistentMonetizationPolicyTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/backend/TEST-com.teamfho.domino.economy.reward.PersistentPolicyControllerTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/backend/TEST-com.teamfho.domino.economy.reward.PersistentPolicyRewardIntegrationTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/backend/TEST-com.teamfho.domino.economy.reward.RewardConsumptionTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/backend/TEST-com.teamfho.domino.economy.reward.RewardControllerTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/backend/TEST-com.teamfho.domino.economy.reward.RewardIntentTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/backend/TEST-com.teamfho.domino.entitlement.EntitlementHistoryTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/backend/TEST-com.teamfho.domino.entitlement.EntitlementHttpTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/backend/TEST-com.teamfho.domino.entitlement.EntitlementTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/backend/TEST-com.teamfho.domino.match.MatchFoundationTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/backend/TEST-com.teamfho.domino.match.MatchHistoryHttpTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/backend/TEST-com.teamfho.domino.match.ReplayHttpTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/backend/TEST-com.teamfho.domino.match.ReplayTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/backend/TEST-com.teamfho.domino.matchmaking.MatchmakingContractTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/backend/TEST-com.teamfho.domino.matchmaking.PartnersMatchmakingTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/backend/TEST-com.teamfho.domino.matchmaking.RedisMatchmakingTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/backend/TEST-com.teamfho.domino.online.OnlineHttpTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/backend/TEST-com.teamfho.domino.online.OnlineMatchTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/backend/TEST-com.teamfho.domino.online.OnlineParticipantProfileTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/backend/TEST-com.teamfho.domino.online.OnlineTransportTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/backend/TEST-com.teamfho.domino.online.OnlineTurnTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/backend/TEST-com.teamfho.domino.online.ParticipantPersistenceTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/backend/TEST-com.teamfho.domino.online.PartnersOnlineTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/backend/TEST-com.teamfho.domino.online.RedisTurnIndexTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/backend/TEST-com.teamfho.domino.online.TurnIndexTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/backend/TEST-com.teamfho.domino.player.PlayerBootstrapServiceTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/backend/TEST-com.teamfho.domino.player.PlayerControllerTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/backend/TEST-com.teamfho.domino.player.PlayerDisplayNameTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/backend/TEST-com.teamfho.domino.player.PlayerFoundationRepositoryTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/backend/TEST-com.teamfho.domino.RealFirestoreGuardTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/backend/TEST-com.teamfho.domino.realtime.ConnectionOutboundTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/backend/TEST-com.teamfho.domino.realtime.OutboundPayloadTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/backend/TEST-com.teamfho.domino.realtime.OutboundTransportTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/backend/TEST-com.teamfho.domino.realtime.RealtimeHandlerTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/backend/TEST-com.teamfho.domino.realtime.RealtimeProtocolTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/backend/TEST-com.teamfho.domino.realtime.RedisPresenceTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/backend/TEST-com.teamfho.domino.security.FirebaseAdminTokenVerifierTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/backend/TEST-com.teamfho.domino.security.FirebaseSecurityTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/backend/TEST-com.teamfho.domino.social.EphemeralBarrierTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/backend/TEST-com.teamfho.domino.social.FollowRateFailureTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/backend/TEST-com.teamfho.domino.social.FollowRedisRateTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/backend/TEST-com.teamfho.domino.social.FollowTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/backend/TEST-com.teamfho.domino.social.FriendshipTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/backend/TEST-com.teamfho.domino.social.SocialHttpTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/backend/TEST-com.teamfho.domino.social.SocialInvalidationRedisTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/backend/TEST-com.teamfho.domino.social.SocialInvalidationTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/backend/TEST-com.teamfho.domino.social.SocialOutboundAuthorizationTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/backend/TEST-com.teamfho.domino.social.SocialPresenceFoundationTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/backend/TEST-com.teamfho.domino.social.SocialPresenceRedisTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/backend/TEST-com.teamfho.domino.social.SocialRateGateRedisTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/backend/TEST-com.teamfho.domino.social.SocialRateGateTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/backend/TEST-com.teamfho.domino.social.SocialTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/backend/TEST-com.teamfho.domino.ValidationServerCleanupTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/client/binary/output-events.bin`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/client/binary/results-generic.bin`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/client/TEST-com.teamfho.swarm.SwarmTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/emulator/binary/output-events.bin`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/emulator/binary/results-generic.bin`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/emulator/TEST-com.teamfho.domino.entitlement.EntitlementEmulatorTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/emulator/TEST-com.teamfho.domino.entitlement.EntitlementRulesEmulatorTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/emulator/TEST-com.teamfho.domino.match.ReplayEmulatorTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/emulator/TEST-com.teamfho.domino.online.FirestoreEmulatorTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/emulator/TEST-com.teamfho.domino.social.FollowEmulatorTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/emulator/TEST-com.teamfho.domino.social.FriendshipEmulatorTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/emulator/TEST-com.teamfho.domino.social.SocialEmulatorTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/emulator/TEST-com.teamfho.domino.social.SocialInvalidationDistributedEmulatorTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/emulator/TEST-com.teamfho.domino.social.SocialInvalidationEmulatorTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/emulator/TEST-com.teamfho.domino.social.SocialPresenceEmulatorTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/emulator/TEST-com.teamfho.domino.social.SocialRateGateEmulatorTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/emulator/TEST-com.teamfho.domino.social.SocialRulesEmulatorTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/emulator/TEST-com.teamfho.domino.social.SocialTransactionRetryEmulatorTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/focused/TEST-com.teamfho.domino.match.ReplayEmulatorTests.xml`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/results.json`
- GENERATED_VALIDATION: `client/Validation/Generated/INFRA1R/windows/TEST-com.teamfho.domino.match.ReplayTests.xml`

OS temporary clean-source copies, dependency caches and local Docker validation images are also
GENERATED_VALIDATION; none are staged or used as replay fixture input.

## INFRA-1R REPLAY REPRODUCIBILITY

```text
SOURCE_SHA=163282b1b29fdd7fa9ff1317f699b00e9b3d4417
ORIGINAL_FAILING_TEST=com.teamfho.domino.match.ReplayEmulatorTests.retained swarm archives read only paging parity and cost on emulator()
ORIGINAL_FAILURE=Retained I3.1 exports required; no remote fallback
FIXTURE_CLASS=C_VALIDATION_EVIDENCE_ACCIDENTALLY_REUSED_AS_FIXTURE
REQUIRED_FIXTURE_FILES=NONE_NOW; previously retained JSON archives for both modes
FIXTURE_FILE_COUNT=0_DATA_FILES
FIXTURE_TOTAL_SIZE=0_VERSIONED_DATA_BYTES
FIXTURE_IGNORE_RULE=server/domino/.gitignore:3:build/
FIXTURE_CANONICAL_LOCATION=server/domino/src/test/kotlin/com/teamfho/domino/match/ReplayTests.kt
FIXTURE_SOLUTION=A_REUSE_DETERMINISTIC_ENGINE_GENERATOR
GENERATED_DURING_TEST=YES_IN_MEMORY
FIXTURE_DETERMINISTIC=YES
SENSITIVE_FIXTURE_CONTENT=NO
REPLAY_ASSERTIONS_WEAKENED=NO
REPLAY_EVENT_SEQUENCE_CHANGED=NO
MATCH_EVENT_SEQUENCE_CHANGED=NO
CLEAN_CHECKOUT_REPLAY_TEST=PASS
IGNORED_LOCAL_FILES_REQUIRED=0
FIRESTORE_EMULATOR_TESTS=46_PASS
FIRESTORE_EMULATOR_FAILS=0
REPLAY_REGRESSION=PASS
MATCH_REGRESSION=PASS
BACKEND_TESTS=645_PASS_1_SKIPPED_0_FAILED
SIMULATED_CLIENT_TESTS=25_PASS
WINDOWS_REPLAY_TESTS=6_PASS
CI_REPRODUCIBLE_FROM_CLEAN_CHECKOUT=YES_LOCAL_LINUX_COMMAND_SEQUENCE
HOSTED_GITHUB_ACTIONS_RUN=NOT_RUN_NO_PUSH
CI_DOCKER_BUILD=PASS
CI_IMAGE_VALIDATION=PASS
CI_REAL_FIRESTORE_CALLS=0
CI_REAL_FIREBASE_AUTH_CALLS=0
SERVER3_API_RESTARTED=NO
SERVER3_REDIS_RESTARTED=NO
SERVER3_COMPOSE_MODIFIED=NO
SERVER3_RUNTIME_PRESERVED=YES
INFRA_1_FILES_MODIFIED=19_INTENTIONAL_PATHS_INCLUDING_2_UNCHANGED_ADOPTED_FILES
INFRA_1R_FILES_MODIFIED=4_INCLUDING_2_SHARED_INFRA_1_DOCUMENTS
PREEXISTING_USER_FILES=99_BASELINE_97_UNRELATED_PLUS_2_DOCKER_FILES
PREEXISTING_USER_FILES_PRESERVED=YES_99_OF_99_HASHES
UNEXPECTED_FILES=0
INFRA_1_SUCCESS=YES
INFRA_1_READY_FOR_CHECKPOINT=YES
COMMIT=NONE
PUSH=NONE
DEPLOY=NONE
NEXT=INFRA-1 REVIEW/CHECKPOINT
```
