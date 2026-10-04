# PLAYER-PROFILE-EDIT-01

Environment: TEST. Implementation and isolated validation only. No real profile update, Firebase operation, deployment, commit or push.

## API audit and contract

The existing `PlayerController` supports PUT `/api/v1/player/display-name` (alias only). Onboarding step save is not an appropriate profile editor: it has onboarding cursor/revision semantics and rejects completed flows. `EntitlementController` already owns GET `/api/v1/player/profile`, returning a limited identity summary. There was no atomic update operation for the five requested fields. The initial new GET collided with that existing route in HTTP tests; the collision was corrected and the existing summary endpoint remains unchanged.

New authenticated GET `/api/v1/player/profile/editable` returns current private first/last names, public displayName, country, preferredLanguage and profile/preferences revision tokens. New PUT `/api/v1/player/profile` atomically replaces these five explicitly editable values. The response has the same editable shape. JSON fields are strictly typed; unknown identity fields are rejected. Responses are no-store. Identity comes exclusively from the authenticated principal. Guest and registered accounts follow identical profile rules; this operation never creates Player or authentication identities.

`PlayerProfileEditingService` reuses `PlayerProfileRules`, `DisplayNameRules`, `PlayerAliasReservations` and the existing buffered Firestore transaction repository. Reusing the repository does not call onboarding services or read/write onboarding documents. Absent Player/preferences are errors, not repaired by editing. Revisions reject stale concurrent changes. Repeating an already-applied identical request is a read-only success. Private names never become the alias.

## Atomicity and preservation

The transaction reads the Player, preferences, alias rollout/reservations, and any existing public social profile before applying buffered writes. A conflict cannot partially update names, country, preferences or public alias. Only editable Player fields, profileRevision and updatedAt change; preference locale/revision/updatedAt change only when needed. Existing public profile displayName and normalizedDisplayName track a renamed alias without receiving private names.

The existing alias normalizer is trim + NFC + lowercase ROOT. The existing availability endpoint remains advisory. Save rechecks ownership transactionally. New alias becomes CLAIMED by this Player; old reservation becomes RELEASED without playerId in the same transaction. An unchanged canonical alias causes zero reservation writes. No different uniqueness algorithm or catalog was added.

createdAt, UID, accountType, status, wallet, Coach, experience, onboarding, membership, grants, trial, stats and history are not updated by this service. No trial activation path is invoked. Preservation and conflicts were tested against isolated repository snapshots; this is not a claim of a real Firestore deployment or live edit.

## Client state and UX

Production Profile navigation injects an editor backed by a session-scoped API client. The form loads from the new authoritative GET; it does not prefill demo values or reopen onboarding. The isolated Editor fixture is a separate no-network implementation.

The controller reuses existing BasicProfile field rules/country metadata and the established alias availability API. Changed aliases debounce for 450 ms. Stale availability results are discarded; TAKEN disables Save and remains an inline error; CHECK_FAILED is a technical warning and final Save remains authoritative. Save is disabled for clean/invalid/checking/taken/busy states. A 409 preserves the draft. Field-specific validation codes map to field feedback; unknown failures have a recoverable form message. Cancel/Back without Save discards the draft and performs no writes. There was no approved unsaved-change confirmation convention in the audited shell, so no new confirmation convention was silently imposed.

Successful responses refresh the existing PlayerService through its confirmed-profile boundary, notifying the shared PlayerPresentationSource consumed by Home/Menu/Profile. No separate per-view Player cache is introduced. Private names remain only in the editor draft/response. On success the editor returns to Profile. Same-session and cancellation guards prevent a late response from updating a replacement account. createdAt/wallet/entitlements are not replaced during this refresh. The form is scoped to the active session and disposed when detached.

Preferred language is persisted and reflected in PlayerService.PreferredLocale. This operation does not change the currently selected global app locale: existing global localization owns that selection independently. No claim is made that restarting alone switches the global locale; that policy was not established by the existing code. The next profile read returns the saved preference.

## Validation evidence

- Backend targeted suite: 52 tests, 0 failures, 0 skipped, including 8 new domain tests and 3 new authenticated HTTP tests plus existing alias/basic-profile regressions.
- Client edit suite: 20 checks PASS, including real API-source-to-PlayerService/presentation propagation with a fake transport, 409 mapping, race, duplicate submission, cancellation and session replacement.
- Guest 01A: 29 PASS; 01B: 48 PASS; 01C: 69 PASS.
- Player session isolation: 291 PASS; Home: 222 PASS; Menu: 60 PASS; Profile: 80 PASS; alias binding: 10 PASS.
- Onboarding full flow: 407 PASS; shell: 93 PASS; bootstrap contract: 31 PASS.
- Unity isolated visual validator: 304 checks, 16 scenarios PASS (8 established sizes × EN/ES). Text clipping and horizontal overflow checks pass. Last selector and Save reachability pass with the logical viewport reduced to 300 px to simulate keyboard occlusion. This is isolated reduced-viewport evidence, not a physical mobile keyboard test.
- The first keyboard fixture incorrectly mixed a scaled Editor preview and logical scroll geometry, leaving roughly two pixels outside its measurement bound. Validation now uses unscaled logical coordinates; the final manual preview may scale for Editor fit. Product geometry was not altered to satisfy this fixture correction.
- Current Unity compilation: 0 compiler errors. Nine existing compiler warnings remain; no new profile-edit compiler warning is claimed.

Final preview observed: EDIT PROFILE · ISOLATED, FORM_PREFILLED, 393×852, ES. Synthetic values only. Save disabled because the form is unchanged. Manual visual approval remains with the owner.

## Scope and safety

Initial pending-file baseline: 171 files. Two previously pending integration files were intentionally extended (ProductionAuthHost and ProductionRoutingComposition); the other 169 remain byte-identical. All 102 protected files remain byte-identical. Existing App Shell Mock, real sessions, backend data and server configuration were not modified. New backend code requires a separately authorized TEST deployment before real editing can be used.

```ini
EXISTING_PROFILE_UPDATE_ENDPOINT=NO_FIVE_FIELD_UPDATE
EXISTING_PROFILE_UPDATE_ENDPOINT_PATH=EXISTING_ALIAS_ONLY_PUT_/api/v1/player/display-name
NEW_PROFILE_UPDATE_ENDPOINT=PUT_/api/v1/player/profile
NEW_PROFILE_PREFILL_ENDPOINT=GET_/api/v1/player/profile/editable
EDIT_PROFILE_REOPENS_ONBOARDING=NO
EDIT_PROFILE_FIELDS=FIRST_NAME,LAST_NAME,DISPLAY_NAME,COUNTRY,PREFERRED_LANGUAGE
EDIT_PROFILE_FIRST_NAME_PREFILL=REAL_PLAYER_CONTRACT
EDIT_PROFILE_LAST_NAME_PREFILL=REAL_PLAYER_CONTRACT
EDIT_PROFILE_ALIAS_PREFILL=PLAYER_DISPLAY_NAME
EDIT_PROFILE_COUNTRY_PREFILL=REAL_PLAYER_CONTRACT
EDIT_PROFILE_LANGUAGE_PREFILL=REAL_PLAYER_CONTRACT
FIRST_NAME_PUBLIC_IDENTITY=NO
LAST_NAME_PUBLIC_IDENTITY=NO
DISPLAY_NAME_PUBLIC_IDENTITY=YES
EDIT_PROFILE_ALIAS_NORMALIZER=SAME_AS_PLAYER_IDENTITY
EDIT_PROFILE_ALIAS_AVAILABILITY_ENDPOINT=SAME_EXISTING_ENDPOINT
CURRENT_ALIAS_ACCEPTED_FOR_OWNER=YES
EDIT_PROFILE_ALIAS_AVAILABILITY_ADVISORY=YES
EDIT_PROFILE_FINAL_ALIAS_RECHECK=YES
EDIT_PROFILE_TAKEN_ALIAS_INLINE=YES
EDIT_PROFILE_ALIAS_RACE_SAFE=YES_TRANSACTIONAL
EDIT_PROFILE_ATOMIC=YES
EDIT_PROFILE_CONFLICT_PARTIAL_WRITES=0
NEW_ALIAS_RESERVED_TO_PLAYER=YES
OLD_ALIAS_RELEASE_POLICY=RELEASED_WITHOUT_OWNER_IN_SAME_TRANSACTION
UNCHANGED_ALIAS_RESERVATION_CHURN=0
EDIT_PROFILE_DIRTY_CHECK=YES
UNCHANGED_PROFILE_WRITES=0
PLAYER_SERVICE_PROFILE_REFRESH=YES
PLAYER_PRESENTATION_PROFILE_REFRESH=YES
EDIT_PROFILE_SUCCESS_ROUTE=PROFILE
PROFILE_UPDATED_WITHOUT_RESTART=YES_SHARED_SOURCE
MENU_ALIAS_UPDATED_WITHOUT_RESTART=YES_SHARED_SOURCE
HOME_ALIAS_UPDATED_WITHOUT_RESTART=YES_SHARED_SOURCE
PRIVATE_NAME_REPLACES_ALIAS=NO
PROFILE_CREATED_AT_CHANGED=NO
HOME_COACH_CHANGED_BY_PROFILE_EDIT=NO
EDIT_PROFILE_COUNTRY_SOURCE=EXISTING_COUNTRY_CATALOG
PREFERRED_LANGUAGE_UI_APPLICATION_POLICY=PERSIST_PREFERENCE_KEEP_CURRENT_GLOBAL_LOCALE
CREATED_AT_EDITABLE=NO
EDIT_PROFILE_AUTH_PROVIDER_INDEPENDENT=YES
EDIT_PROFILE_CREATES_NEW_PLAYER=NO
EDIT_PROFILE_EMAIL_FIELD=NO
EDIT_PROFILE_PASSWORD_FIELD=NO
EDIT_PROFILE_CANCEL_WRITES=0_WITHOUT_SAVE
EDIT_PROFILE_DIRTY_EXIT_POLICY=DISCARD_DRAFT_WITHOUT_WRITES
EDIT_PROFILE_FAILURE_PRESERVES_FORM=YES
STALE_PROFILE_UPDATE_APPLIED_TO_NEW_SESSION=NO
DUPLICATE_PROFILE_SAVE_REQUESTS=0
EDIT_PROFILE_RESPONSIVE=8/8_PASS_EN_ES
EDIT_PROFILE_KEYBOARD_SCROLL=PASS_ISOLATED_REDUCED_VIEWPORT
HORIZONTAL_OVERFLOW=0
TEXT_CLIPPING=0
EDIT_PROFILE_BACKEND_TESTS=52_PASS_INCLUDING_REGRESSIONS
EDIT_PROFILE_CLIENT_TESTS=20_PASS
PLAYER_UI_01_REGRESSION=PASS
PLAYER_IDENTITY_01_REGRESSION=PASS
GUEST_ACCOUNT_01_REGRESSION=PASS
ONBOARDING_REGRESSION=PASS
REAL_PROFILE_EDIT_EXECUTED=NO
BACKEND_DEPLOY_REQUIRED=YES
PREEXISTING_PROTECTED_FILES_MODIFIED=0/102
UNRELATED_PENDING_FILES_PRESERVED=169/169
APP_SHELL_MOCK_CHANGED=NO
PASSWORD_LOGGED=NO
TOKEN_LOGGED=NO
SECRET_SCAN=PASS_16_SCOPED_FILES
PRIVACY_SCAN=PASS
UNITY_COMPILER_ERRORS=0
UNITY_WARNINGS_CURRENT=9_PREEXISTING
NEW_PROFILE_EDIT_WARNINGS=0
COMMIT=NONE
PUSH=NONE
DEPLOY=NO
PLAYER_PROFILE_EDIT_01_SUCCESS=YES_IMPLEMENTATION_AND_ISOLATED_VALIDATION
NEXT=MANUAL EDIT PROFILE REVIEW
```

## Manual preview correction and load/save review — 2026-10-03

The observed error-only screen was the real Game View in Play, not FORM_PREFILLED. Its absent Draft and the controller/view mapping identify LOAD_ERROR: Load sets LOAD_FAILED, while the previous view mapped that code to save-failure copy. The upstream HTTP reason was not established in this review. No runtime Retry was pressed. Play was stopped without logout.

The separate isolated Editor window was blank after domain reload. Its UI is now rebuilt by CreateGUI, with serialized preview selection. Validation-only selectors expose FORM_PREFILLED, LOAD_ERROR and SAVE_ERROR using an in-memory fixture, without API/session dependencies.

Production change is limited to ProductionProfileEditView: localized load-failure feedback and explicit load Retry, distinct from existing save-error feedback. Controller, transport, backend and contracts are unchanged by this follow-up. Failed saves keep the mounted form and its five values. Synthetic error scenarios use the real view/controller with an isolated source.

Current validation: client 26 PASS; Unity targeted load/save EN/ES 60 PASS (4 scenarios), including copy, Retry visibility, retained form values, overflow and clipping. Previous broad suites are retained, not rerun. Unity Console shows 0 errors and 10 warnings; this review does not claim a warning-free Editor. The compiler warning lines inspected refer to other existing validation classes.

Final screenshot confirms the isolated FORM_PREFILLED / 393x852 / ES preview with Ana, Rivera, FixturePlayer, Cuba (CU), Español; alias helper visible; Guardar cambios visible and disabled under the unchanged dirty-state contract. No Email or Password controls. Manual approval remains pending.

```ini
CURRENT_EDIT_PROFILE_PREVIEW_STATE=LOAD_ERROR_IN_RUNTIME_AT_DISCOVERY
FINAL_EDIT_PROFILE_PREVIEW_STATE=FORM_PREFILLED_ISOLATED_393x852_ES
EDIT_PROFILE_LOAD_ERROR_DISTINCT_FROM_SAVE_ERROR=YES
LOAD_ERROR_COPY_CORRECT=YES
LOAD_ERROR_DOES_NOT_CLAIM_FORM_VALUES_PRESERVED=YES
SAVE_ERROR_FORM_VISIBLE=YES
SAVE_ERROR_FORM_VALUES_PRESERVED=YES
PRODUCT_SOURCE_CHANGE_REQUIRED=YES_VIEW_ERROR_MAPPING_ONLY
FORM_PREFILLED_OPEN=YES
PREFILLED_FIRST_NAME_VISIBLE=YES
PREFILLED_LAST_NAME_VISIBLE=YES
PREFILLED_ALIAS_VISIBLE=YES
PREFILLED_COUNTRY_VISIBLE=YES
PREFILLED_LANGUAGE_VISIBLE=YES
PUBLIC_ALIAS_HELPER_VISIBLE=YES
SAVE_CHANGES_ACTION_VISIBLE=YES
EMAIL_FIELD_VISIBLE=NO
PASSWORD_FIELD_VISIBLE=NO
UNITY_COMPILER_ERRORS=0
CLIENT_CHECKS=26_PASS
TARGETED_UNITY_CHECKS=60_PASS
PREEXISTING_PROTECTED_FILES_MODIFIED=0/102
UNRELATED_PENDING_FILES_PRESERVED=169/169
MANUAL_EDIT_PROFILE_REVIEW=PENDING
REAL_PROFILE_EDIT_EXECUTED=NO
REAL_PLAYER_MUTATIONS=0
COMMIT=NONE
PUSH=NONE
DEPLOY=NO
NEXT=MANUAL EDIT PROFILE FORM REVIEW
```


## Final checkpoint review — stopped before staging (2026-10-03)

Controlled pre-deploy GET returned HTTP 403, JSON with no explicit API code. HTTP_403_ROOT_CAUSE=UNPROVEN. This is not reclassified as a security, authentication, endpoint or client defect. TEST remains at aca8b2ebef57177258789ec464f7c800fbeb338c without the new endpoints. Security review found both endpoints covered by the existing authenticated /api/** rule; no security workaround is required or applied.

Dependency gate: the currently validated ProductionAuthHost combines the Profile editor factory with uncommitted Guest Account integration. It references AccountProtectionController, AccountLinkController, ProductionAccountProtectionOverlay and ProductionProtectAccountView (absent from HEAD), plus pending ProductionAuthRouter.IsLinkedVerification/AcceptLinkedAccountAsync. Staging this complete host without Guest files would not form a complete build; including them would violate this checkpoint scope. The Profile core itself has no direct Guest dependency. A deliberately separated Profile-only integration is technically possible, but is not the current validated full host. Per the explicit stop gate, no staging, split, product edits, tests, commit or push were performed. ProductionRoutingComposition also mixes the Profile accessor with historical temporary smoke barriers, which must not enter a durable checkpoint.

Retained evidence: backend 52 PASS; client 26 PASS; targeted Unity load/save EN/ES 60 PASS; earlier responsive 304 checks / 16 scenarios retained. No suites repeated. Real prefill and save remain unvalidated, pending TEST deployment.

PLAYER_PROFILE_DEPENDS_ON_UNCOMMITTED_GUEST_ACCOUNT=YES_CURRENT_INTEGRATED_HOST
PROFILE_CORE_DIRECT_GUEST_DEPENDENCY=NO
HTTP_403_HISTORICAL_EVIDENCE_PRESERVED=YES
FILES_STAGED=0
CHECKPOINT_TESTS_REPEATED=NO
PROTECTED_FILES_MODIFIED=0/102
COMMIT=NONE
PUSH=NONE
DEPLOY=NO
NEXT=GUEST_ACCOUNT_DEPENDENCY_CHECKPOINT_ORDER_REVIEW

### Complete pending-file inventory at this gate

Mixed-file annotations describe ownership at hunk level; categories do not authorize staging whole mixed files.

| Category | Count |
|---|---:|
| GUEST_ACCOUNT_01_PENDING | 32 |
| HISTORICAL_PENDING | 20 |
| PLAYER_PROFILE_EDIT_BACKEND_PRODUCT | 1 |
| PLAYER_PROFILE_EDIT_CLIENT_PRODUCT | 7 |
| PLAYER_PROFILE_EDIT_REPORT | 1 |
| PLAYER_PROFILE_EDIT_TEST | 4 |
| PROTECTED | 102 |
| TEMPORARY_VALIDATION | 18 |

UNCLASSIFIED_PENDING_FILES=0

| File | Category | Notes |
|---|---|---|
| client/DominoGame/Assets/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0/firebase-app-unity-13.16.0.pom | PROTECTED |  |
| client/DominoGame/Assets/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0/firebase-auth-unity-13.16.0.pom | PROTECTED |  |
| client/DominoGame/Assets/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0/firebase-firestore-unity-13.16.0.pom | PROTECTED |  |
| client/DominoGame/Assets/Plugins/Android/GoogleMobileAdsPlugin.androidlib/AndroidManifest.xml | PROTECTED |  |
| client/DominoGame/Assets/StreamingAssets/google-services-desktop.json | PROTECTED |  |
| client/DominoGame/Assets/StreamingAssets/google-services-desktop.json.meta | PROTECTED |  |
| client/DominoGame/Assets/_Domino/Localization/Localization Settings.asset | PROTECTED |  |
| client/DominoGame/Assets/_Domino/Resources/AdsSettings.asset | PROTECTED |  |
| client/DominoGame/Assets/_Domino/Scripts/Auth/AuthenticatedRoutingOrchestrator.cs | GUEST_ACCOUNT_01_PENDING |  |
| client/DominoGame/Assets/_Domino/Scripts/Auth/ProductionAuthRouter.cs | GUEST_ACCOUNT_01_PENDING |  |
| client/DominoGame/Assets/_Domino/Scripts/Auth/ProductionLogoutService.cs | GUEST_ACCOUNT_01_PENDING |  |
| client/DominoGame/Assets/_Domino/Scripts/Infrastructure/Api/OnboardingApiSession.cs | PLAYER_PROFILE_EDIT_CLIENT_PRODUCT |  |
| client/DominoGame/Assets/_Domino/Scripts/Infrastructure/ApplicationServices.cs | TEMPORARY_VALIDATION |  |
| client/DominoGame/Assets/_Domino/Scripts/Infrastructure/Firebase/FirebaseAuthService.cs | GUEST_ACCOUNT_01_PENDING |  |
| client/DominoGame/Assets/_Domino/Scripts/Infrastructure/Firebase/FirebaseSdkClient.cs | GUEST_ACCOUNT_01_PENDING |  |
| client/DominoGame/Assets/_Domino/Scripts/Infrastructure/Firebase/IFirebaseClient.cs | GUEST_ACCOUNT_01_PENDING |  |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/ProductionAppShell.cs | PLAYER_PROFILE_EDIT_CLIENT_PRODUCT |  |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/ProductionAuthHost.cs | GUEST_ACCOUNT_01_PENDING | Mixed: Guest Account integration plus Profile editor factory; do not stage whole file. |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/ProductionEmailView.cs | GUEST_ACCOUNT_01_PENDING |  |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/ProductionLogoutConfirmation.cs | GUEST_ACCOUNT_01_PENDING |  |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/ProductionRoutingComposition.cs | PLAYER_PROFILE_EDIT_CLIENT_PRODUCT | Mixed: Profile session accessor plus temporary smoke barriers. |
| client/DominoGame/Assets/google-services.json | PROTECTED |  |
| client/DominoGame/ProjectSettings/GvhProjectSettings.xml | PROTECTED |  |
| client/DominoGame/ProjectSettings/ProjectSettings.asset | PROTECTED |  |
| client/Validation/AuthenticatedRoutingTests.cs | HISTORICAL_PENDING |  |
| client/Validation/PLAYER_UI_01_REAL_PLAYER_BINDING_AUDIT_REPORT.md | HISTORICAL_PENDING |  |
| client/Validation/PlayerAliasBindingTests.cs | HISTORICAL_PENDING |  |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/query/client-agp/cache-v2 | PROTECTED |  |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/query/client-agp/cmakeFiles-v1 | PROTECTED |  |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/query/client-agp/codemodel-v2 | PROTECTED |  |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/cache-v2-2c0909d0b4389f2443c3.json | PROTECTED |  |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/cmakeFiles-v1-2afea77556dece6ed3b6.json | PROTECTED |  |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/codemodel-v2-56ef99f20c5d90a856eb.json | PROTECTED |  |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/directory-.-RelWithDebInfo-d0094a50bb2071803777.json | PROTECTED |  |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/directory-FramePacing-RelWithDebInfo-7f9c8865fd027a154c90.json | PROTECTED |  |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/index-2026-09-20T07-54-05-0123.json | PROTECTED |  |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/target-swappywrapper-RelWithDebInfo-de42165ac0b744ec5a6b.json | PROTECTED |  |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.ninja_deps | PROTECTED |  |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.ninja_log | PROTECTED |  |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeCache.txt | PROTECTED |  |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeCCompiler.cmake | PROTECTED |  |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeCXXCompiler.cmake | PROTECTED |  |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeDetermineCompilerABI_C.bin | PROTECTED |  |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeDetermineCompilerABI_CXX.bin | PROTECTED |  |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeSystem.cmake | PROTECTED |  |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CompilerIdC/CMakeCCompilerId.c | PROTECTED |  |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CompilerIdC/CMakeCCompilerId.o | PROTECTED |  |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CompilerIdCXX/CMakeCXXCompilerId.cpp | PROTECTED |  |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CompilerIdCXX/CMakeCXXCompilerId.o | PROTECTED |  |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/TargetDirectories.txt | PROTECTED |  |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/cmake.check_cache | PROTECTED |  |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/rules.ninja | PROTECTED |  |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/FramePacing/CMakeFiles/swappywrapper.dir/UnitySwappyWrapper.cpp.o | PROTECTED |  |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/FramePacing/cmake_install.cmake | PROTECTED |  |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/additional_project_files.txt | PROTECTED |  |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/android_gradle_build.json | PROTECTED |  |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/android_gradle_build_mini.json | PROTECTED |  |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/build.ninja | PROTECTED |  |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/build_file_index.txt | PROTECTED |  |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/cmake_install.cmake | PROTECTED |  |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/compile_commands.json | PROTECTED |  |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/compile_commands.json.bin | PROTECTED |  |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/configure_fingerprint.bin | PROTECTED |  |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/metadata_generation_command.txt | PROTECTED |  |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/prefab_config.json | PROTECTED |  |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/symbol_folder_index.txt | PROTECTED |  |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/hash_key.txt | PROTECTED |  |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/prefab/arm64-v8a/prefab/lib/aarch64-linux-android/cmake/games-frame-pacing/games-frame-pacingConfig.cmake | PROTECTED |  |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/prefab/arm64-v8a/prefab/lib/aarch64-linux-android/cmake/games-frame-pacing/games-frame-pacingConfigVersion.cmake | PROTECTED |  |
| client/DominoGame/.utmp/tools/release/arm64-v8a/compile_commands.json | PROTECTED |  |
| client/DominoGame/Assets/AddressableAssetsData/Android.meta | PROTECTED |  |
| client/DominoGame/Assets/AddressableAssetsData/Android/addressables_content_state.bin | PROTECTED |  |
| client/DominoGame/Assets/AddressableAssetsData/Android/addressables_content_state.bin.meta | PROTECTED |  |
| client/DominoGame/Assets/AddressableAssetsData/ProfileDataSourceSettings.asset | PROTECTED |  |
| client/DominoGame/Assets/AddressableAssetsData/ProfileDataSourceSettings.asset.meta | PROTECTED |  |
| client/DominoGame/Assets/GeneratedLocalRepo.meta | PROTECTED |  |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase.meta | PROTECTED |  |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository.meta | PROTECTED |  |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com.meta | PROTECTED |  |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google.meta | PROTECTED |  |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase.meta | PROTECTED |  |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity.meta | PROTECTED |  |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0.meta | PROTECTED |  |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0/firebase-app-unity-13.16.0.aar | PROTECTED |  |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0/firebase-app-unity-13.16.0.aar.meta | PROTECTED |  |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0/firebase-app-unity-13.16.0.pom | PROTECTED |  |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0/firebase-app-unity-13.16.0.pom.meta | PROTECTED |  |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity.meta | PROTECTED |  |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0.meta | PROTECTED |  |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0/firebase-auth-unity-13.16.0.aar | PROTECTED |  |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0/firebase-auth-unity-13.16.0.aar.meta | PROTECTED |  |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0/firebase-auth-unity-13.16.0.pom | PROTECTED |  |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0/firebase-auth-unity-13.16.0.pom.meta | PROTECTED |  |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity.meta | PROTECTED |  |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0.meta | PROTECTED |  |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0/firebase-firestore-unity-13.16.0.aar | PROTECTED |  |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0/firebase-firestore-unity-13.16.0.aar.meta | PROTECTED |  |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0/firebase-firestore-unity-13.16.0.pom | PROTECTED |  |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0/firebase-firestore-unity-13.16.0.pom.meta | PROTECTED |  |
| client/DominoGame/Assets/Plugins/Android/FirebaseApp.androidlib.meta | PROTECTED |  |
| client/DominoGame/Assets/Plugins/Android/FirebaseApp.androidlib/AndroidManifest.xml | PROTECTED |  |
| client/DominoGame/Assets/Plugins/Android/FirebaseApp.androidlib/project.properties | PROTECTED |  |
| client/DominoGame/Assets/Plugins/Android/FirebaseApp.androidlib/res/values/google-services.xml | PROTECTED |  |
| client/DominoGame/Assets/Plugins/Android/gradleTemplate.properties | PROTECTED |  |
| client/DominoGame/Assets/Plugins/Android/gradleTemplate.properties.meta | PROTECTED |  |
| client/DominoGame/Assets/Plugins/Android/mainTemplate.gradle | PROTECTED |  |
| client/DominoGame/Assets/Plugins/Android/mainTemplate.gradle.meta | PROTECTED |  |
| client/DominoGame/Assets/Plugins/Android/settingsTemplate.gradle | PROTECTED |  |
| client/DominoGame/Assets/Plugins/Android/settingsTemplate.gradle.meta | PROTECTED |  |
| client/DominoGame/Assets/_Domino/Scripts/Auth/AccountProtectionController.cs | GUEST_ACCOUNT_01_PENDING |  |
| client/DominoGame/Assets/_Domino/Scripts/Auth/AccountProtectionController.cs.meta | GUEST_ACCOUNT_01_PENDING |  |
| client/DominoGame/Assets/_Domino/Scripts/Identity/AccountLinkController.cs | GUEST_ACCOUNT_01_PENDING |  |
| client/DominoGame/Assets/_Domino/Scripts/Identity/AccountLinkController.cs.meta | GUEST_ACCOUNT_01_PENDING |  |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/AccountProtectionText.cs | GUEST_ACCOUNT_01_PENDING |  |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/AccountProtectionText.cs.meta | GUEST_ACCOUNT_01_PENDING |  |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/Editor/GuestAccountLinkPreview.cs | GUEST_ACCOUNT_01_PENDING |  |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/Editor/GuestAccountLinkPreview.cs.meta | GUEST_ACCOUNT_01_PENDING |  |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/Editor/GuestAccountProtectionPreview.cs | GUEST_ACCOUNT_01_PENDING |  |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/Editor/GuestAccountProtectionPreview.cs.meta | GUEST_ACCOUNT_01_PENDING |  |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/Editor/GuestAccountVerificationPreview.cs | GUEST_ACCOUNT_01_PENDING |  |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/Editor/GuestAccountVerificationPreview.cs.meta | GUEST_ACCOUNT_01_PENDING |  |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/Editor/PlayerUi01EObservation.cs | TEMPORARY_VALIDATION |  |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/Editor/PlayerUi01EObservation.cs.meta | TEMPORARY_VALIDATION |  |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/Editor/ProductionAliasAvailabilityPreview.cs | TEMPORARY_VALIDATION |  |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/Editor/ProductionAliasAvailabilityPreview.cs.meta | TEMPORARY_VALIDATION |  |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/Editor/ProductionCountryPopupValidation.cs | TEMPORARY_VALIDATION |  |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/Editor/ProductionCountryPopupValidation.cs.meta | TEMPORARY_VALIDATION |  |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/Editor/ProfileEditPreview.cs | TEMPORARY_VALIDATION |  |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/Editor/ProfileEditPreview.cs.meta | TEMPORARY_VALIDATION |  |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/Editor/Smoke02Harness.cs | TEMPORARY_VALIDATION |  |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/Editor/Smoke02Harness.cs.meta | TEMPORARY_VALIDATION |  |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/Editor/Smoke02Observer.cs | TEMPORARY_VALIDATION |  |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/Editor/Smoke02Observer.cs.meta | TEMPORARY_VALIDATION |  |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/ProductionAccountProtectionOverlay.cs | GUEST_ACCOUNT_01_PENDING |  |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/ProductionAccountProtectionOverlay.cs.meta | GUEST_ACCOUNT_01_PENDING |  |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/ProductionProfileEditView.cs | PLAYER_PROFILE_EDIT_CLIENT_PRODUCT |  |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/ProductionProfileEditView.cs.meta | PLAYER_PROFILE_EDIT_CLIENT_PRODUCT |  |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/ProductionProtectAccountView.cs | GUEST_ACCOUNT_01_PENDING |  |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/ProductionProtectAccountView.cs.meta | GUEST_ACCOUNT_01_PENDING |  |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/ProfileEditController.cs | PLAYER_PROFILE_EDIT_CLIENT_PRODUCT |  |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/ProfileEditController.cs.meta | PLAYER_PROFILE_EDIT_CLIENT_PRODUCT |  |
| client/DominoGame/ProjectSettings/AndroidResolverDependencies.xml | PROTECTED |  |
| client/DominoGame/ProjectSettings/ScriptableBuildPipeline.json | PROTECTED |  |
| client/Validation/DEPLOY_PREFLIGHT_01_BE07_ROLLOUT_READINESS_REPORT.md | HISTORICAL_PENDING |  |
| client/Validation/FUNCTIONAL_00_AUTH_PLAYER_BACKEND_AUDIT.md | HISTORICAL_PENDING |  |
| client/Validation/GUEST_ACCOUNT_01_PROTECTION_AND_LINKING_AUDIT_REPORT.md | GUEST_ACCOUNT_01_PENDING |  |
| client/Validation/GuestAccount01ATests.cs | GUEST_ACCOUNT_01_PENDING |  |
| client/Validation/GuestAccount01BTests.cs | GUEST_ACCOUNT_01_PENDING |  |
| client/Validation/GuestAccount01CTests.cs | GUEST_ACCOUNT_01_PENDING |  |
| client/Validation/LOCAL_SERVER_PREFLIGHT_01_BACKEND_AUDIT_REPORT.md | HISTORICAL_PENDING |  |
| client/Validation/LOCAL_TEST_BE07_DEPLOY_01_REPORT.md | HISTORICAL_PENDING |  |
| client/Validation/LOCAL_TEST_BE07_SMOKE_02_REPORT.md | HISTORICAL_PENDING |  |
| client/Validation/LOCAL_TEST_FOUNDATION_DEPLOY_01_REPORT.md | HISTORICAL_PENDING |  |
| client/Validation/NEW_PLAYER_FOUNDATION_LIVE_01_REPORT.md | HISTORICAL_PENDING |  |
| client/Validation/ONB_00A_BACKEND_DOMAIN_API_DESIGN.txt | HISTORICAL_PENDING |  |
| client/Validation/ONB_00_BACKEND_CONTRACT_FINAL_REVIEW.md | HISTORICAL_PENDING |  |
| client/Validation/ONB_00_ONBOARDING_PLAYER_COACH_MEMBERSHIP_DESIGN.txt | HISTORICAL_PENDING |  |
| client/Validation/ONB_LIVE_01A_REAL_SESSION_ROUTING_REPORT.md | HISTORICAL_PENDING |  |
| client/Validation/ONB_LIVE_01_NEW_USER_E2E_REPORT.md | HISTORICAL_PENDING |  |
| client/Validation/PLAYER_PROFILE_EDIT_01_REPORT.md | PLAYER_PROFILE_EDIT_REPORT |  |
| client/Validation/PlayerSessionIsolationTests.cs | HISTORICAL_PENDING |  |
| client/Validation/ProfileEditTests.cs | PLAYER_PROFILE_EDIT_TEST |  |
| client/Validation/RunGuestAccount01ATests.ps1 | GUEST_ACCOUNT_01_PENDING |  |
| client/Validation/RunGuestAccount01BTests.ps1 | GUEST_ACCOUNT_01_PENDING |  |
| client/Validation/RunGuestAccount01CTests.ps1 | GUEST_ACCOUNT_01_PENDING |  |
| client/Validation/RunPlayerSessionIsolationTests.ps1 | HISTORICAL_PENDING |  |
| client/Validation/RunProfileEditTests.ps1 | PLAYER_PROFILE_EDIT_TEST |  |
| client/Validation/RunSmoke02ObserverTests.ps1 | TEMPORARY_VALIDATION |  |
| client/Validation/Smoke02CompositionTests.cs | TEMPORARY_VALIDATION |  |
| client/Validation/Smoke02ObserverTests.cs | TEMPORARY_VALIDATION |  |
| client/Validation/Smoke02Snapshot.py | TEMPORARY_VALIDATION |  |
| client/Validation/Smoke02SnapshotTests.py | TEMPORARY_VALIDATION |  |
| client/Validation/TEST_BE07_01_DEPLOY_SMOKE_REPORT.md | HISTORICAL_PENDING |  |
| client/Validation/TEST_CATALOG_01_PUBLICATION_REPORT.md | HISTORICAL_PENDING |  |
| client/Validation/TEST_USER_DELETION_IMPACT_REVIEW.md | HISTORICAL_PENDING |  |
| client/Validation/__pycache__/CapacityCoordinator.cpython-312.pyc | PROTECTED |  |
| client/Validation/__pycache__/CapacityDiscoveryTests.cpython-312.pyc | PROTECTED |  |
| client/Validation/__pycache__/CapacityInstrumentationTests.cpython-312.pyc | PROTECTED |  |
| client/Validation/__pycache__/CapacityMetrics.cpython-312.pyc | PROTECTED |  |
| client/Validation/__pycache__/CapacityRegistry.cpython-312.pyc | PROTECTED |  |
| client/Validation/__pycache__/S707TimingAnalysis.cpython-312.pyc | PROTECTED |  |
| client/Validation/__pycache__/S708TimingAnalysis.cpython-312.pyc | PROTECTED |  |
| server/domino/src/main/kotlin/com/teamfho/domino/player/PlayerProfileEditing.kt | PLAYER_PROFILE_EDIT_BACKEND_PRODUCT |  |
| server/domino/src/test/kotlin/com/teamfho/domino/player/PlayerProfileEditHttpTests.kt | PLAYER_PROFILE_EDIT_TEST |  |
| server/domino/src/test/kotlin/com/teamfho/domino/player/PlayerProfileEditingTests.kt | PLAYER_PROFILE_EDIT_TEST |  |


## Final checkpoint review after committed Guest baseline (2026-10-03)

This review supersedes the earlier dependency stop; historical diagnostics remain unchanged. Base main: `165a777a6717b5d764e8ea8f718d91c490d130bc`. Guest dependency is committed. All 161 pending file contents matched the prior reviewed snapshot before this report update. No durable product/test source changed; retained backend 52 PASS, client 26 PASS, targeted Unity 60 PASS, earlier responsive 304 checks / 16 scenarios across 8 presets EN/ES. No suite repeated. Retained Player UI, identity, Guest and onboarding regressions remain PASS.

The checkpoint contains 14 durable files: backend product 1, client product 8 (including metadata), tests 4, report 1. EN/ES presentation copy is inside the client view; there is no separate localization file. The routing composition is staged only for CreatePlayerApiSession; historical smoke hooks are excluded. ProfileEditPreview and all other preview-only tooling remain temporary and excluded. No committed Guest files are restaged.

Authenticated GET /api/v1/player/profile/editable reads the existing Player and preferences without creation. Authenticated PUT /api/v1/player/profile updates only firstName, lastName, displayName, country and preferredLanguage, with revision checks. Existing identity, createdAt, coach, experience, onboarding, membership, trial, entitlements, wallet, stats and history remain unchanged. First/last names are private; only displayName is public identity.

Alias normalization and advisory availability reuse PLAYER-IDENTITY-01. The final claim is transactional. An unchanged alias produces no reservation churn. For a changed normalized alias, the old reservation becomes RELEASED with playerId removed, and the new reservation becomes CLAIMED by the same Player within the same transaction. Conflicts cause no partial writes. No-op profile submissions are read-only. Final DISPLAY_NAME_TAKEN maps inline to the alias field.

Confirmed saves refresh PlayerService and PlayerPresentation and return to Profile; Home/Menu receive the public alias without replacing it with private names. Session checks reject stale responses and duplicate submission is blocked. Load and save errors remain distinct, and save errors preserve all form entries. No production authentication/security changes.

Historical controlled predeploy GET remains HTTP 403, root cause UNPROVEN. TEST baseline remains aca8b2ebef57177258789ec464f7c800fbeb338c; editable/update endpoints are not deployed. Real prefill/save remains pending TEST deployment. This checkpoint performs no live GET, PUT, Retry, profile/onboarding/alias/trial writes or deployment.

Protected files preserved: 102/102. Pending inventory classification follows; ProductionRoutingComposition also retains excluded TEMPORARY_VALIDATION smoke hunks. Unclassified: 0. Commit/push and CI outcome are reported separately after execution.

| Path | Classification |
|---|---|
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/query/client-agp/cache-v2 | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/query/client-agp/cmakeFiles-v1 | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/query/client-agp/codemodel-v2 | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/cache-v2-2c0909d0b4389f2443c3.json | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/cmakeFiles-v1-2afea77556dece6ed3b6.json | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/codemodel-v2-56ef99f20c5d90a856eb.json | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/directory-.-RelWithDebInfo-d0094a50bb2071803777.json | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/directory-FramePacing-RelWithDebInfo-7f9c8865fd027a154c90.json | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/index-2026-09-20T07-54-05-0123.json | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/target-swappywrapper-RelWithDebInfo-de42165ac0b744ec5a6b.json | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.ninja_deps | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.ninja_log | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeCache.txt | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeCCompiler.cmake | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeCXXCompiler.cmake | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeDetermineCompilerABI_C.bin | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeDetermineCompilerABI_CXX.bin | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeSystem.cmake | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CompilerIdC/CMakeCCompilerId.c | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CompilerIdC/CMakeCCompilerId.o | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CompilerIdCXX/CMakeCXXCompilerId.cpp | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CompilerIdCXX/CMakeCXXCompilerId.o | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/TargetDirectories.txt | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/cmake.check_cache | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/rules.ninja | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/FramePacing/CMakeFiles/swappywrapper.dir/UnitySwappyWrapper.cpp.o | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/FramePacing/cmake_install.cmake | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/additional_project_files.txt | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/android_gradle_build.json | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/android_gradle_build_mini.json | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/build.ninja | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/build_file_index.txt | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/cmake_install.cmake | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/compile_commands.json | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/compile_commands.json.bin | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/configure_fingerprint.bin | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/metadata_generation_command.txt | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/prefab_config.json | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/symbol_folder_index.txt | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/hash_key.txt | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/prefab/arm64-v8a/prefab/lib/aarch64-linux-android/cmake/games-frame-pacing/games-frame-pacingConfig.cmake | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/prefab/arm64-v8a/prefab/lib/aarch64-linux-android/cmake/games-frame-pacing/games-frame-pacingConfigVersion.cmake | PROTECTED |
| client/DominoGame/.utmp/tools/release/arm64-v8a/compile_commands.json | PROTECTED |
| client/DominoGame/Assets/AddressableAssetsData/Android.meta | PROTECTED |
| client/DominoGame/Assets/AddressableAssetsData/Android/addressables_content_state.bin | PROTECTED |
| client/DominoGame/Assets/AddressableAssetsData/Android/addressables_content_state.bin.meta | PROTECTED |
| client/DominoGame/Assets/AddressableAssetsData/ProfileDataSourceSettings.asset | PROTECTED |
| client/DominoGame/Assets/AddressableAssetsData/ProfileDataSourceSettings.asset.meta | PROTECTED |
| client/DominoGame/Assets/GeneratedLocalRepo.meta | PROTECTED |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase.meta | PROTECTED |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository.meta | PROTECTED |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com.meta | PROTECTED |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google.meta | PROTECTED |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase.meta | PROTECTED |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity.meta | PROTECTED |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0.meta | PROTECTED |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0/firebase-app-unity-13.16.0.aar | PROTECTED |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0/firebase-app-unity-13.16.0.aar.meta | PROTECTED |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0/firebase-app-unity-13.16.0.pom | PROTECTED |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0/firebase-app-unity-13.16.0.pom.meta | PROTECTED |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity.meta | PROTECTED |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0.meta | PROTECTED |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0/firebase-auth-unity-13.16.0.aar | PROTECTED |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0/firebase-auth-unity-13.16.0.aar.meta | PROTECTED |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0/firebase-auth-unity-13.16.0.pom | PROTECTED |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0/firebase-auth-unity-13.16.0.pom.meta | PROTECTED |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity.meta | PROTECTED |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0.meta | PROTECTED |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0/firebase-firestore-unity-13.16.0.aar | PROTECTED |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0/firebase-firestore-unity-13.16.0.aar.meta | PROTECTED |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0/firebase-firestore-unity-13.16.0.pom | PROTECTED |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0/firebase-firestore-unity-13.16.0.pom.meta | PROTECTED |
| client/DominoGame/Assets/Plugins/Android/FirebaseApp.androidlib.meta | PROTECTED |
| client/DominoGame/Assets/Plugins/Android/FirebaseApp.androidlib/AndroidManifest.xml | PROTECTED |
| client/DominoGame/Assets/Plugins/Android/FirebaseApp.androidlib/project.properties | PROTECTED |
| client/DominoGame/Assets/Plugins/Android/FirebaseApp.androidlib/res/values/google-services.xml | PROTECTED |
| client/DominoGame/Assets/Plugins/Android/gradleTemplate.properties | PROTECTED |
| client/DominoGame/Assets/Plugins/Android/gradleTemplate.properties.meta | PROTECTED |
| client/DominoGame/Assets/Plugins/Android/mainTemplate.gradle | PROTECTED |
| client/DominoGame/Assets/Plugins/Android/mainTemplate.gradle.meta | PROTECTED |
| client/DominoGame/Assets/Plugins/Android/settingsTemplate.gradle | PROTECTED |
| client/DominoGame/Assets/Plugins/Android/settingsTemplate.gradle.meta | PROTECTED |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/Editor/GuestAccountLinkPreview.cs | TEMPORARY_VALIDATION |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/Editor/GuestAccountLinkPreview.cs.meta | TEMPORARY_VALIDATION |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/Editor/GuestAccountProtectionPreview.cs | TEMPORARY_VALIDATION |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/Editor/GuestAccountProtectionPreview.cs.meta | TEMPORARY_VALIDATION |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/Editor/GuestAccountVerificationPreview.cs | TEMPORARY_VALIDATION |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/Editor/GuestAccountVerificationPreview.cs.meta | TEMPORARY_VALIDATION |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/Editor/PlayerUi01EObservation.cs | TEMPORARY_VALIDATION |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/Editor/PlayerUi01EObservation.cs.meta | TEMPORARY_VALIDATION |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/Editor/ProductionAliasAvailabilityPreview.cs | TEMPORARY_VALIDATION |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/Editor/ProductionAliasAvailabilityPreview.cs.meta | TEMPORARY_VALIDATION |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/Editor/ProductionCountryPopupValidation.cs | TEMPORARY_VALIDATION |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/Editor/ProductionCountryPopupValidation.cs.meta | TEMPORARY_VALIDATION |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/Editor/ProfileEditPreview.cs | TEMPORARY_VALIDATION |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/Editor/ProfileEditPreview.cs.meta | TEMPORARY_VALIDATION |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/Editor/Smoke02Harness.cs | TEMPORARY_VALIDATION |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/Editor/Smoke02Harness.cs.meta | TEMPORARY_VALIDATION |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/Editor/Smoke02Observer.cs | TEMPORARY_VALIDATION |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/Editor/Smoke02Observer.cs.meta | TEMPORARY_VALIDATION |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/ProductionProfileEditView.cs | PLAYER_PROFILE_EDIT_CLIENT_PRODUCT |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/ProductionProfileEditView.cs.meta | PLAYER_PROFILE_EDIT_CLIENT_PRODUCT |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/ProfileEditController.cs | PLAYER_PROFILE_EDIT_CLIENT_PRODUCT |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/ProfileEditController.cs.meta | PLAYER_PROFILE_EDIT_CLIENT_PRODUCT |
| client/DominoGame/ProjectSettings/AndroidResolverDependencies.xml | PROTECTED |
| client/DominoGame/ProjectSettings/ScriptableBuildPipeline.json | PROTECTED |
| client/Validation/DEPLOY_PREFLIGHT_01_BE07_ROLLOUT_READINESS_REPORT.md | HISTORICAL |
| client/Validation/FUNCTIONAL_00_AUTH_PLAYER_BACKEND_AUDIT.md | HISTORICAL |
| client/Validation/LOCAL_SERVER_PREFLIGHT_01_BACKEND_AUDIT_REPORT.md | HISTORICAL |
| client/Validation/LOCAL_TEST_BE07_DEPLOY_01_REPORT.md | HISTORICAL |
| client/Validation/LOCAL_TEST_BE07_SMOKE_02_REPORT.md | HISTORICAL |
| client/Validation/LOCAL_TEST_FOUNDATION_DEPLOY_01_REPORT.md | HISTORICAL |
| client/Validation/NEW_PLAYER_FOUNDATION_LIVE_01_REPORT.md | HISTORICAL |
| client/Validation/ONB_00A_BACKEND_DOMAIN_API_DESIGN.txt | HISTORICAL |
| client/Validation/ONB_00_BACKEND_CONTRACT_FINAL_REVIEW.md | HISTORICAL |
| client/Validation/ONB_00_ONBOARDING_PLAYER_COACH_MEMBERSHIP_DESIGN.txt | HISTORICAL |
| client/Validation/ONB_LIVE_01A_REAL_SESSION_ROUTING_REPORT.md | HISTORICAL |
| client/Validation/ONB_LIVE_01_NEW_USER_E2E_REPORT.md | HISTORICAL |
| client/Validation/PLAYER_PROFILE_EDIT_01_REPORT.md | PLAYER_PROFILE_EDIT_REPORT |
| client/Validation/PlayerSessionIsolationTests.cs | HISTORICAL |
| client/Validation/ProfileEditTests.cs | PLAYER_PROFILE_EDIT_TEST |
| client/Validation/RunPlayerSessionIsolationTests.ps1 | HISTORICAL |
| client/Validation/RunProfileEditTests.ps1 | PLAYER_PROFILE_EDIT_TEST |
| client/Validation/RunSmoke02ObserverTests.ps1 | TEMPORARY_VALIDATION |
| client/Validation/Smoke02CompositionTests.cs | TEMPORARY_VALIDATION |
| client/Validation/Smoke02ObserverTests.cs | TEMPORARY_VALIDATION |
| client/Validation/Smoke02Snapshot.py | TEMPORARY_VALIDATION |
| client/Validation/Smoke02SnapshotTests.py | TEMPORARY_VALIDATION |
| client/Validation/TEST_BE07_01_DEPLOY_SMOKE_REPORT.md | HISTORICAL |
| client/Validation/TEST_CATALOG_01_PUBLICATION_REPORT.md | HISTORICAL |
| client/Validation/TEST_USER_DELETION_IMPACT_REVIEW.md | HISTORICAL |
| client/Validation/__pycache__/CapacityCoordinator.cpython-312.pyc | PROTECTED |
| client/Validation/__pycache__/CapacityDiscoveryTests.cpython-312.pyc | PROTECTED |
| client/Validation/__pycache__/CapacityInstrumentationTests.cpython-312.pyc | PROTECTED |
| client/Validation/__pycache__/CapacityMetrics.cpython-312.pyc | PROTECTED |
| client/Validation/__pycache__/CapacityRegistry.cpython-312.pyc | PROTECTED |
| client/Validation/__pycache__/S707TimingAnalysis.cpython-312.pyc | PROTECTED |
| client/Validation/__pycache__/S708TimingAnalysis.cpython-312.pyc | PROTECTED |
| server/domino/src/main/kotlin/com/teamfho/domino/player/PlayerProfileEditing.kt | PLAYER_PROFILE_EDIT_BACKEND_PRODUCT |
| server/domino/src/test/kotlin/com/teamfho/domino/player/PlayerProfileEditHttpTests.kt | PLAYER_PROFILE_EDIT_TEST |
| server/domino/src/test/kotlin/com/teamfho/domino/player/PlayerProfileEditingTests.kt | PLAYER_PROFILE_EDIT_TEST |
| client/DominoGame/Assets/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0/firebase-app-unity-13.16.0.pom | PROTECTED |
| client/DominoGame/Assets/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0/firebase-auth-unity-13.16.0.pom | PROTECTED |
| client/DominoGame/Assets/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0/firebase-firestore-unity-13.16.0.pom | PROTECTED |
| client/DominoGame/Assets/Plugins/Android/GoogleMobileAdsPlugin.androidlib/AndroidManifest.xml | PROTECTED |
| client/DominoGame/Assets/StreamingAssets/google-services-desktop.json | PROTECTED |
| client/DominoGame/Assets/StreamingAssets/google-services-desktop.json.meta | PROTECTED |
| client/DominoGame/Assets/_Domino/Localization/Localization Settings.asset | PROTECTED |
| client/DominoGame/Assets/_Domino/Resources/AdsSettings.asset | PROTECTED |
| client/DominoGame/Assets/_Domino/Scripts/Auth/AuthenticatedRoutingOrchestrator.cs | HISTORICAL |
| client/DominoGame/Assets/_Domino/Scripts/Infrastructure/Api/OnboardingApiSession.cs | PLAYER_PROFILE_EDIT_CLIENT_PRODUCT |
| client/DominoGame/Assets/_Domino/Scripts/Infrastructure/ApplicationServices.cs | TEMPORARY_VALIDATION |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/ProductionAppShell.cs | PLAYER_PROFILE_EDIT_CLIENT_PRODUCT |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/ProductionAuthHost.cs | PLAYER_PROFILE_EDIT_CLIENT_PRODUCT |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/ProductionRoutingComposition.cs | PLAYER_PROFILE_EDIT_CLIENT_PRODUCT |
| client/DominoGame/Assets/google-services.json | PROTECTED |
| client/DominoGame/ProjectSettings/GvhProjectSettings.xml | PROTECTED |
| client/DominoGame/ProjectSettings/ProjectSettings.asset | PROTECTED |
| client/Validation/AuthenticatedRoutingTests.cs | HISTORICAL |
| client/Validation/PLAYER_UI_01_REAL_PLAYER_BINDING_AUDIT_REPORT.md | HISTORICAL |
| client/Validation/PlayerAliasBindingTests.cs | HISTORICAL |
