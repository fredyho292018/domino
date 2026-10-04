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


## PLAYER_PROFILE_EDIT_01_TEST_LIVE (2026-10-04)

Result: PARTIAL. Deployment passed; the single controlled authenticated GET returned 403 and the live test stopped at the required gate. No PUT, Retry, profile change, alias change, or session-restore test was performed. The cause of the new 403 is UNPROVEN. The historical pre-deploy 403 remains UNPROVEN and is not rewritten.

Exact committed source d02519ff68dfcb67c739d08bddda1c8dea4834ad was exported with git archive, compiled successfully, built as a linux/amd64 image, transferred with matching archive hash, and deployed only to local dominoserver TEST. Backend CI #19 PASS retained. No dirty source was used. Only API image/revision changed; normalized Compose, environment, mounts and resource/security configuration matched. Redis ID and start time remained unchanged. Rollback image and restricted configuration backups remain available.

OLD_API_CONTAINER_ID=ce28ad229ed4a1accca595a3eae7e930f18d7390227804c531a11ee543571de8

OLD_API_IMAGE=cuban-domino-api:aca8b2ebef57177258789ec464f7c800fbeb338c

OLD_API_IMAGE_ID=sha256:951bfef610fa97b48a29c397f4059ff2fb39d53a8efea83b141208d904c639f1

NEW_API_IMAGE=cuban-domino-api:d02519ff68dfcb67c739d08bddda1c8dea4834ad

NEW_API_CONTAINER_ID=d85dd43021d407a09ee6d094c4e5ce3192466b81210e9e3fadbc29454223243a

Local/public health passed, startup marker was present with 0 blocking startup errors. Deployed JAR SHA-256 matched the built artifact. That exact JAR contains PlayerProfileEditingController with the GET editable and PUT profile route annotations. This proves endpoint implementation presence in the deployed artifact, not successful authenticated reachability. Seven catalog/pointer document hashes and update times remained unchanged.

GET /api/v1/player/profile/editable was executed exactly once against the public TEST URL, with the existing Unity session token kept in memory. Local checks confirmed TEST audience, matching subject and unexpired token; server authentication acceptance is NOT proven by the 403. Response: HTTP 403, API code absent, NON_JSON_ERROR. No raw response, identity or credential was persisted. No retries. No instrumented source changes.

REAL_EDIT_PROFILE_LOAD=NOT_RETESTED_AFTER_GET_GATE_FAILURE
REAL_EDIT_PROFILE_PREFILL_VALIDATED=NO
PROFILE_UPDATE_REQUEST_COUNT=0
REAL_PLAYER_MUTATIONS=0_BY_THIS_TASK
PRIVATE_NAME_EDITED=NO
COUNTRY_EDITED=NO
DISPLAY_NAME_EDITED=NO
LANGUAGE_EDITED=NO
ATOMIC_PERSISTENCE_AND_UI_PROPAGATION=NOT_EXECUTED
PROFILE_EDIT_SESSION_RESTORE=NOT_EXECUTED
API_HEALTH_AFTER=UP
REDIS_HEALTH_AFTER=UP
REDIS_RESTARTED=NO
UNITY_FINAL_PLAY_MODE=OFF
SOURCE_CHANGED_DURING_REAL_VALIDATION=NO
COMMIT=NONE
PUSH=NONE
PROD_DEPLOYMENT=NO
GCP_DEPLOYMENT=NO
PLAYER_PROFILE_EDIT_01_TEST_LIVE_SUCCESS=NO
NEXT=POST_DEPLOY_403_READ_ONLY_ORIGIN_AND_AUTHORIZATION_REVIEW

Unity was stopped normally without logout after the gate failure. Existing LOAD_ERROR was not retried. No private prefill values were inspected or reported. Report and ignored validation/deployment artifacts only.

## PREFERRED_LANGUAGE_RUNTIME_APPLICATION (2026-10-04)

Evidence qualification added by subsequent host review: the results in this section are ISOLATED only. User subsequently reported REAL_LANGUAGE_APPLICATION=FAIL_BEFORE_FIX. The host integration review below supersedes any interpretation of these checks as real-runtime PASS; historical results are retained.

User subsequently confirmed real prefill/save PASS manually. This is separate evidence from the controlled HTTP 403 above, which remains recorded unchanged. A read-only administrative lookup matched the current local session to its Player and found preferences/current.preferredLocale=en and Player.language=en. No identity, email, token or profile payload is reported.

Root cause: PERSISTED_BUT_RUNTIME_LOCALE_NOT_UPDATED. PUT already supports preferredLanguage and atomically persists it; editable GET returns it. The confirmed client Player projection also retained it. There was no connection from that projection to the existing Unity localization owner. No backend correction is required.

Owner remains LocalizationSettings.SelectedLocale; DominoLocalization.Select is the existing validated selection API and retains its local domino.language preference. Startup fallback remains PlayerPrefLocaleSelector, SystemLocaleSelector, then configured English. The new host-scoped PlayerLocaleBinding applies only fresh, current-session, confirmed en/es preferences after localization is ready; missing/unsupported preference makes no selection. Restored/bootstrap Player language and authoritative onboarding/profile preferences use the same binding. Onboarding drafts do not change locale; confirmed onboarding preference does. Stale/disposed sessions cannot apply late responses. Device selection does not overwrite an explicit Player preference.

Successful profile save validates the authoritative response, updates PlayerService, then SnapshotChanged applies locale before Saved navigation. Failed/no-op saves do not change locale. Existing SelectedLocaleChanged notifications refresh Edit Profile labels without rebuilding its fields; Home, Menu, Profile and bottom navigation subscribe to the same owner. No independent per-view locale store, network request or Player write was added. Static demo friends content was not changed.

Focused client tests: 42 PASS. Covers restore/login, readiness, no-op, missing/unsupported fallback, failed save, retained selection, delayed success, both language directions, disposed/stale sessions, language-only dirty state. Unity isolated validation: 46 PASS, actual controller + API source + in-memory transport + confirmed Player binding, ES to EN to ES to EN, attached Edit Profile/Home/Menu/Profile/navigation, field preservation, 393x852 overflow and clipping. Temporary Editor harness is validation-only and performs no real requests or preferences persistence. Earlier harness preparation failures (OWNER/null locale) were corrected; final run passed. They are not hidden as historical exceptions.

Regressions: Profile edit 42 PASS; Home 222 PASS; Menu 60 PASS; Profile 80 PASS; Guest post-link 69 PASS; onboarding full flow 407 PASS (bootstrap 31 and shell 93 also passed). Current imported source compiles with zero compiler errors and nine existing warnings. No large unrelated suites repeated.

```ini
REAL_PREFERRED_LANGUAGE_PERSISTED=YES_READ_ONLY_VERIFIED
REAL_PREFERRED_LANGUAGE_VALUE=en
PROFILE_UPDATE_PREFERRED_LANGUAGE_SUPPORTED=YES
PROFILE_EDITABLE_RETURNS_PREFERRED_LANGUAGE=YES
LOCALIZATION_STATE_OWNER=LocalizationSettings.SelectedLocale
CURRENT_LOCALE_SOURCE=CONFIRMED_PLAYER_PREFERENCE_THEN_EXISTING_FALLBACK
CURRENT_LOCALE_CHANGE_API=DominoLocalization.Select
PLAYER_PREFERRED_LANGUAGE_APPLIED_ON_BOOTSTRAP=YES
PLAYER_PREFERRED_LANGUAGE_APPLIED_ON_SESSION_RESTORE=YES_ISOLATED
ONBOARDING_LANGUAGE_RUNTIME_POLICY=CONFIRMED_PREFERENCE_ONLY
EDIT_PROFILE_SUCCESS_NOTIFIES_LOCALIZATION=YES
PREFERRED_LANGUAGE_ROOT_CAUSE=PERSISTED_BUT_RUNTIME_LOCALE_NOT_UPDATED
PREFERRED_LANGUAGE_APPLICATION_POLICY=IMMEDIATE_AFTER_SUCCESSFUL_SAVE
LOCALE_CHANGED_BEFORE_SUCCESSFUL_SAVE=NO
SAVE_FAILURE_CHANGES_RUNTIME_LOCALE=NO
SAVE_FAILURE_PRESERVES_LANGUAGE_SELECTION=YES
ES_TO_EN_IMMEDIATE_UI_REFRESH=PASS_ISOLATED
EN_TO_ES_IMMEDIATE_UI_REFRESH=PASS_ISOLATED
EDIT_PROFILE_RERENDERS_ON_LOCALE_CHANGE=YES_LABELS_ONLY_FORM_PRESERVED
HOME_LOCALE_REFRESH=PASS
MENU_LOCALE_REFRESH=PASS
PROFILE_LOCALE_REFRESH=PASS
BOTTOM_NAV_LOCALE_REFRESH=PASS
LANGUAGE_CHANGE_UNRELATED_PLAYER_MUTATIONS=0
LANGUAGE_CHANGE_ALIAS_RESERVATION_MUTATIONS=0
PREFERRED_LANGUAGE_SESSION_RESTORE=PASS_ISOLATED
PREFERRED_LANGUAGE_LOGIN_RESTORE=PASS_ISOLATED
EXPLICIT_PLAYER_LANGUAGE_OVERRIDDEN_BY_DEVICE=NO
MISSING_PREFERRED_LANGUAGE_FALLBACK=EXISTING_LOCAL_DEVICE_DEFAULT_POLICY
LANGUAGE_ONLY_CHANGE_DIRTY=YES
LANGUAGE_ONLY_SAVE_ENABLED=YES
SAME_LANGUAGE_PROFILE_WRITE=0
PREFERRED_LANGUAGE_TESTS=42_CLIENT_PASS_46_UNITY_PASS
SECRET_SCAN=PASS_12_SCOPED_FILES_ZERO_CREDENTIAL_PATTERN_MATCHES
PLAYER_PROFILE_EDIT_REGRESSION=PASS
PLAYER_UI_01_REGRESSION=PASS
ONBOARDING_REGRESSION=PASS
GUEST_ACCOUNT_01_REGRESSION=PASS
ADDITIONAL_REAL_PROFILE_WRITES=0
BACKEND_CHANGE_REQUIRED=NO
SOURCE_CHANGED=YES_CLIENT_AND_VALIDATION_ONLY
PREEXISTING_PROTECTED_FILES_MODIFIED=0/102
FINAL_PREVIEW=ISOLATED_EDIT_PROFILE_EN_393x852_AFTER_ES_TO_EN_SUCCESS
MANUAL_VISUAL_REVIEW=PENDING
COMMIT=NONE
PUSH=NONE
DEPLOY=NO
NEXT=MANUAL_PREFERRED_LANGUAGE_RUNTIME_REVIEW
```

## REAL_PREFERRED_LANGUAGE_HOST_INTEGRATION (2026-10-04)

Manual evidence: persistence PASS (en); top-right selector changes runtime language PASS; Edit Profile real application FAIL before this fix. No new real profile save or backend read was required. The previous read-only persistence evidence remains valid; no claim is made that the new client fix has been manually retested against TEST.

The top-right Game View control is Unity's `UnityEditor.Localization.UI.GameViewLanguageMenu` (package `com.unity.localization`, Editor/UI/GameViewLanguageMenu.cs). Its AddToolbarsToGameViews registers the PopupField callback that directly assigns `LocalizationSettings.SelectedLocale = evt.newValue`. Classification: EDITOR_VALIDATION_CONTROL. It changes presentation, not Player preference or the editable draft. Consequently top selector=en with field=Español can legitimately represent an English presentation of a Spanish saved preference (or unsaved draft); translating a field caption must not rewrite its domain value. The real field remains populated from editable GET, and after save from the authoritative response.

First divergence proven in source and reproduced by host regression: `ProductionAuthEntry.Mount` disables legacy `DominoClientController`. Its Start is the only production call to `DominoLocalization.Initialize`, which populates the adapter's english/spanish StringTable caches. The previous host gated PlayerLocaleBinding on `DominoLocalization.Ready` (both caches loaded). In the production shell that gate could remain false, so the save event never reached Select. The working Editor selector has no such legacy table-cache prerequisite. Both paths use the SAME static LocalizationSettings context; there is no second runtime locale instance.

Previous isolated test bypass: it supplied `ready=()=>true` and an AssetDatabase-backed direct SelectedLocale assignment. It proved event-driven UI response but did not exercise host readiness or the actual DominoLocalization.Select path. Its PASS was insufficient to prove integration.

Minimal client fix: DominoLocalization.LocaleSelectionReady waits for available-locale preload and supported en/es assets, independently of the legacy StringTable cache. ProductionAuthHost.BindPlayer is shared by normal startup and the Editor host entry; it wires the same PlayerLocaleBinding readiness and DominoLocalization.Select and constructs the real ProfileEditController/ProfileEditApiSource. The Editor entry substitutes only Player/auth/API dependencies. No injected locale-application callback, manual top-right selection, second persistent locale owner, production navigation change, or new backend operation is used.

Exact order: language draft -> ProfileEditController.Save -> ProfileEditApiSource PUT -> successful response contract check -> PlayerService.ReceiveConfirmedProfile -> SnapshotChanged -> PlayerLocaleBinding.Refresh -> DominoLocalization.Select -> LocalizationSettings.SelectedLocale -> attached view callbacks -> Saved's existing Profile navigation. The test observes the still-attached Edit Profile title becoming English/Spanish BEFORE that preexisting navigation; navigation is not needed to trigger translation. It reopens Edit Profile through the same shell only to leave a reviewable post-save form.

Production-host regression: **58 PASS**, using the real ProductionAuthHost, ProductionAppShell, factory, controller, API source and Select. Asserted `DominoLocalization.Ready=false` throughout setup with available locales ready, reproducing the exact old failure. Restore applied authoritative es; three fake successful saves en/es/en changed the same locale owner and visible Edit Profile; one fake failed save preserved UI locale and draft; no optimistic application while fake PUT pending; Profile/navigation translated; reopened field matched confirmed preference; no-op/rerender added zero writes. Exactly four in-memory PUT attempts (three success, one failure), zero real requests, auth writes, onboarding writes or trial writes. Final 393x852 geometry and clipping checks passed. Local domino.language preference was restored after the test; temporary locale provider is restored when the preview closes.

Relevant rerun regressions: Profile edit 42 PASS; Home 222 PASS; Menu 60 PASS; Profile 80 PASS; Guest 69 PASS; full onboarding 407 PASS plus bootstrap 31 and shell 93 PASS. Earlier isolated 46 PASS retained, not promoted to live evidence. Unity current compiler errors 0; Console has 10 warnings including a Firebase Future disposal warning from the Editor session, not a failing language test. No current blocking exception observed. Protected files unchanged 0/102; existing pending source preserved except the two authorized integration files and this report. New Editor validation harness is TEMPORARY_VALIDATION_PREVIEW, not a production dependency.

```ini
REAL_PREFERRED_LANGUAGE_PERSISTED=YES
REAL_PREFERRED_LANGUAGE_VALUE=en
TOP_RIGHT_LOCALE_SELECTOR_RUNTIME_CHANGE=PASS_MANUAL
EDIT_PROFILE_REAL_RUNTIME_LOCALE_CHANGE=FAIL_BEFORE_FIX_PENDING_RETEST
EDIT_PROFILE_ISOLATED_RUNTIME_LOCALE_CHANGE=PASS_RETAINED
TOP_RIGHT_SELECTOR_OWNER=UNITY_LOCALIZATION_EDITOR
TOP_RIGHT_SELECTOR_CLASS=UnityEditor.Localization.UI.GameViewLanguageMenu
TOP_SELECTOR_CALL_PATH=PopupField_CHANGE_TO_LocalizationSettings.SelectedLocale
TOP_SELECTOR_FINAL_LOCALE_OWNER=LocalizationSettings.SelectedLocale
LANGUAGE_PATH_FIRST_DIVERGENCE=HOST_GATED_ON_DISABLED_LEGACY_TABLE_INITIALIZATION
REAL_EDIT_PROFILE_CALLS_DOMINO_LOCALIZATION_SELECT=NO_WITH_OLD_BLOCKED_GATE_YES_AFTER_FIX_IN_HOST_TEST
EDIT_PROFILE_AND_APP_SHELL_LOCALIZATION_CONTEXT_SAME=YES
ISOLATED_LOCALIZATION_DEPENDENCY=PREVIOUS_ALWAYS_READY_DIRECT_ASSIGNMENT
PRODUCTION_LOCALIZATION_DEPENDENCY=AVAILABLE_LOCALE_READINESS_AND_DominoLocalization.Select
ISOLATED_TEST_FALSE_POSITIVE_CAUSE=PRODUCTION_READINESS_GATE_NOT_EXERCISED
LOCALIZATION_STATE_OWNER=LocalizationSettings.SelectedLocale
SECOND_LOCALIZATION_STATE_OWNER_CREATED=NO
EDIT_PROFILE_USES_EXISTING_LOCALIZATION_OWNER=YES
LOCALE_APPLIED_BEFORE_SAVE_SUCCESS=NO
REAL_EDIT_PROFILE_SCREEN_RERENDERS=PASS_PRODUCTION_COMPOSITION_PENDING_REAL_RETEST
SINGLE_RUNTIME_LOCALE_FOR_APP_SHELL=YES
PRODUCTION_LANGUAGE_CHANGE_DEPENDS_ON_TOP_SELECTOR=NO
PREFERRED_LANGUAGE_FIELD_MATCHES_PLAYER_PREFERENCE=YES_AFTER_CONFIRMED_SAVE
OBSERVED_LOCALE_FIELD_MISMATCH_ROOT_CAUSE=EDITOR_DISPLAY_LOCALE_IS_NOT_SAVED_PLAYER_PREFERENCE_OR_DRAFT
LOCALE_RERENDER_PROFILE_WRITES=0
LOCALIZATION_SAVE_LOOP=NO
REAL_HOST_LANGUAGE_REGRESSION_TEST=58_PASS
SESSION_RESTORE_PRODUCTION_COMPOSITION_TEST=PASS
ISOLATED_TESTS_RETAINED=YES
BACKEND_CHANGE_REQUIRED=NO
BACKEND_SOURCE_CHANGED=NO
BACKEND_REDEPLOY_REQUIRED=NO
ADDITIONAL_REAL_PROFILE_WRITES=0
PLAYER_PROFILE_EDIT_REGRESSION=PASS
PLAYER_UI_01_REGRESSION=PASS
ONBOARDING_REGRESSION=PASS
GUEST_ACCOUNT_01_REGRESSION=PASS
REAL_LANGUAGE_APPLICATION=PENDING_RETEST
COMMIT=NONE
PUSH=NONE
DEPLOY=NO
NEXT=MANUAL_REAL_PREFERRED_LANGUAGE_REVIEW
```

## PREFERRED_LANGUAGE_REAL_VALIDATION_CLOSURE (2026-10-04)

User-authorized manual real evidence: Profile and Edit Profile started in English; selecting Español did not translate the UI before Save. One successful language save returned to Profile and immediately translated Profile and bottom navigation to Spanish, without logout/restart or use of Unity's top-right locale selector. This closes real EN-to-ES application as PASS; no new real ES-to-EN claim is made. Previous failures and isolated results above remain historical evidence.

Subsequent read-only verification used the existing TEST session identity internally and read Firestore Player root and preferences/current through the established administrative reader. Sanitized result: identityMatches=true, preferredLanguage=es, rootLanguage=es, persisted=true. No new PUT, bootstrap, token refresh or logout was performed. The initial restricted SSH attempt failed before the read; the authorized external read then succeeded.

Unrelated-data verification boundary: reviewed deployed-source PlayerProfileEditingService and PlayerAliasReservations. The update merges firstName/lastName/displayName/countryCode/language plus profileRevision/updatedAt into Player, and changes preference locale/revision/updatedAt. Other root values, including createdAt, are retained. Coach/dominoProfile, onboarding, membership/grants/trial/entitlements and wallet are outside its write set. An unchanged owned alias generates no reservation write; public profile writes occur only for an alias change. The user described only a language change and retained regressions passed. However, no real pre-save snapshot of all requested values exists in the language evidence; previous baselines are source-file inventories, not Player-data snapshots. Therefore zero unrelated mutations is supported by contract/manual scope, but is NOT independently proven as a before/after live-data comparison. In particular same-document names/country cannot be compared retroactively from the locale-only read. No repair or repeat save was performed to manufacture evidence.

Durable localization changes remain uncommitted and are prepared as an explicit hash manifest under ignored validation artifacts, for the next checkpoint review. Includes only PlayerLocaleBinding and meta, DominoLocalization, ProductionAuthHost, ProductionAppShell, ProductionHomePage, ProductionMenuPage, ProductionProfilePage, ProductionProfileEditView, ProfileEditTests, its runner, and this report. All temporary Editor previews/observers and historical ProductionRoutingComposition changes are excluded. No source changes or repeated suites in this closure; no staging, commit, push or deploy.

```ini
REAL_PREFERRED_LANGUAGE_PERSISTED=YES
FINAL_PERSISTED_PREFERRED_LANGUAGE=es
EN_TO_ES_IMMEDIATE_UI_REFRESH=PASS_REAL_USER_CONFIRMED
LOCALE_CHANGED_BEFORE_SUCCESSFUL_SAVE=NO
PREFERRED_LANGUAGE_APPLICATION_POLICY=IMMEDIATE_AFTER_SUCCESSFUL_SAVE
PROFILE_LOCALE_REFRESH=PASS_REAL
BOTTOM_NAV_LOCALE_REFRESH=PASS_REAL
EDIT_PROFILE_SUCCESS_ROUTE=PROFILE
TOP_RIGHT_SELECTOR_REQUIRED_FOR_PRODUCTION=NO
LANGUAGE_CHANGE_UNRELATED_PLAYER_MUTATIONS=NOT_INDEPENDENTLY_PROVEN_LIVE
LANGUAGE_ONLY_WRITE_CONTRACT=NO_UNRELATED_DOMAIN_MUTATIONS
PRE_SAVE_REAL_DATA_SNAPSHOT_AVAILABLE=NO
PREFERRED_LANGUAGE_SESSION_RESTORE=PASS_RETAINED_PRODUCTION_COMPOSITION
REAL_HOST_LANGUAGE_REGRESSION_TEST=58_PASS_RETAINED
SESSION_RESTORE_PRODUCTION_COMPOSITION_TEST=PASS_RETAINED
RELEVANT_REGRESSIONS=PASS_RETAINED
REAL_LANGUAGE_APPLICATION=PASS
ADDITIONAL_REAL_PROFILE_WRITES=1_AUTHORIZED_LANGUAGE_CHANGE_BY_USER
ASSISTANT_ADDITIONAL_REAL_PROFILE_WRITES=0
PRODUCT_SOURCE_CHANGED_THIS_CLOSURE=NO
TEMPORARY_VALIDATION_CONTROLS_INCLUDED_IN_CHECKPOINT_SCOPE=NO
COMMIT=NONE
PUSH=NONE
DEPLOY=NO
NEXT=PLAYER_PROFILE_EDIT_01_FINAL_CLOSURE_AND_LOCALIZATION_FIX_CHECKPOINT
```

## LOCALIZATION_FIX_FINAL_CHECKPOINT_REVIEW (2026-10-04)

Approved checkpoint scope: 12 durable files (7 existing production localization/UI files, PlayerLocaleBinding plus its meta, 2 focused test/runner files, and this report). Product/test hashes match the reviewed manifest and retained test state; CHECKPOINT_TESTS_REPEATED=NO. Inventory before staging: 164 pending files, comprising 12 durable localization candidates, 102 protected, 28 historical pending, and 22 temporary Editor validation files. UNCLASSIFIED_PENDING_FILES=0. All 102 protected file hashes match. Historical/temporary files are excluded, including ProductionRoutingComposition's existing diagnostic changes. The main Profile and Guest checkpoints are not restaged.

Reviewed contract: one LocalizationSettings.SelectedLocale owner; confirmed current-session Player preference applies through existing Select after successful persistence; failure/draft/no-op do not apply prematurely; session restore uses the same host binding; UI locale refresh performs no API writes and does not create a save loop. Editor-only BindIsolated accepts fake services for the shared composition seam; no temporary preview/control implementation is included. Backend code/configuration and deployment are excluded.

Final real evidence retained exactly: persisted preferred language es, real EN-to-ES immediate Profile/navigation refresh PASS, successful save route Profile, no top-right Editor selector required. LANGUAGE_CHANGE_UNRELATED_PLAYER_MUTATIONS=NOT_INDEPENDENTLY_PROVEN: contract/regression coverage exists but complete real pre/post comparison is unavailable. No retrospective evidence is fabricated. Home/Menu/Edit Profile event propagation is tested composition evidence, not a claim of new real screenshots.

Retained: REAL_HOST_LANGUAGE_REGRESSION_TEST=58_PASS; SESSION_RESTORE_PRODUCTION_COMPOSITION_TEST=PASS; RELEVANT_REGRESSIONS=PASS. Additional assistant real profile writes=0. Main/deployed TEST backend remains d02519ff68dfcb67c739d08bddda1c8dea4834ad. Client-only checkpoint needs no backend redeploy. The sole repository workflow is Backend CI with server/infrastructure/workflow path filters; these candidates do not match. Actual push/remote/CI outcome is recorded in the checkpoint delivery, not assumed here.

Commit message authorized: `fix: apply preferred language after profile save`. No new block or TRIAL-LIVE-01 is started.
