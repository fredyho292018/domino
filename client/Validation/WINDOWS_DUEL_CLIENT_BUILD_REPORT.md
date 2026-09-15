# DOMINO WINDOWS DUEL CLIENT BUILD REPORT

BRANCH=main
SOURCE_SHA=0e6b3691ac2174d3ce7c00fe7dce9203cfc6d0d2
SOURCE=Current working tree, including uncommitted I2.1 and user assets
UNITY_VERSION=6000.0.41f1
BUILD_DESTINATION=D:\Fredy\development\2026\domino\builds\duel-client-b
WINDOWS_TARGET=PASS (StandaloneWindows64)
DEVELOPMENT_BUILD=YES
AUTOCONNECT_PROFILER=NO
DEEP_PROFILING=NO
SCRIPT_DEBUGGING=NO
SCENE_INCLUDED=Assets/_Domino/Scenes/DominoClient.unity

## Addressables

ADDRESSABLES_BUILD=PASS
ADDRESSABLES_BUILD_METHOD=AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult), explicitly before BuildPipeline.BuildPlayer
ADDRESSABLE_RUNTIME_DATA_PRESENT=PASS
AA_SETTINGS_PRESENT=PASS

Output: DominoGame_Data/StreamingAssets/aa/settings.json, catalog.bin, catalog.hash and Windows bundles. Includes localization-locales, localization-assets-shared, localization-string-tables-en and localization-string-tables-es.

## Localization

LOCALIZATION_INCLUDED=PASS
SPANISH_AVAILABLE=PASS
ENGLISH_AVAILABLE=PASS
SELECTED_LOCALE_NULL=NO

The normal application initializes both EN and ES tables before exposing the menu (DominoLocalization.Initialize checks both results). Actual standalone screenshots show Spanish selector and online entry. No separate English visual-switch test was run in this task.

## Player build

PLAYER_BUILD=PASS
EXE_PATH=D:\Fredy\development\2026\domino\builds\duel-client-b\DominoGame.exe
BUILD_RESULT=Succeeded

UnityPlayer.dll, MonoBleedingEdge, DominoGame_Data and runtime dependencies are included. Do not distribute the executable alone.

## Smoke test

EXE_LAUNCH=PASS
START_MENU_VISIBLE=PASS
BACKGROUND_ONLY=NO
DUEL_1V1_VISIBLE=PASS
PARTNERS_2V2_VISIBLE=PASS
ONLINE_DUEL_ENTRY=PASS
PUBLISHED_CATALOG=3 (remote catalog ready in smoke.log)

Used the existing explicit --i21-visual probe to open the actual selector and click its DUEL action. It neither replaces identity nor creates a match. Inspected builds/windows-selector.png and builds/windows-entry.png. Probe completed PASS. Then launched the executable normally, without validation flags, and left it running (PID 39900 at validation).

## Player log

ADDRESSABLE_SETTINGS_MISSING_ERROR=NO
RUNTIME_DATA_NULL_ERROR=NO
NO_LOCALES_AVAILABLE_ERROR=NO
SELECTED_LOCALE_NULL_ERROR=NO
LOCALIZATION_INIT_ERROR=NO
NEW_ERRORS=NONE observed during startup/smoke
FIREBASE_DEPENDENCIES=PASS
FIREBASE_APP_INITIALIZED=PASS
AUTH_READY=PASS (existing anonymous identity reused)
PLAYER_BOOTSTRAP=PASS

Logs: builds/duel-client-b/smoke.log and builds/duel-client-b/Player.log. Build log: client/Validation/Generated/I21/windows-client-build.log.

## Config

API_ENVIRONMENT=LOCAL
API_BASE_URL=http://127.0.0.1:8080
BACKEND_HEALTH=UP
API_SETTINGS_USER_CHANGE_PRESERVED=YES
ADS_SETTINGS_USER_CHANGE_PRESERVED=YES

Source and build-project copies have identical SHA256:

- ApiSettings: AC6B0ED6D30240BC531B235E43E8B9B55D3B48DCBCCCF365ADEC62D3EE6BCB37
- AdsSettings: C377416E334727264806761518A4B5EDF381A837A927A1C3F3D523A1ACBDB5D6

Firebase desktop config is included at DominoGame_Data/StreamingAssets/google-services-desktop.json. No credentials printed. No ads requested by this smoke flow.

## Online preparation and identity

SECOND_CLIENT_READY=YES (standalone executable and online entry)
DISTINCT_SECOND_PLAYER_IDENTITY_VALIDATED=NO
STANDALONE_FIREBASE_UID_COLLISION=NOT_VERIFIED_IN_THIS_TASK
FIREBASE_UID=Not printed; existing Firebase identity reused

A second process does not ensure a second UID. Editor and standalone use the same product/default Firebase application and may reuse persisted identity. This task did not compare their current UIDs, sign out, delete credentials or create another guest. Backend same-UID rejection remains unchanged. For an independent identity without source changes, run the standalone under a separate Windows user profile; normal Firebase startup creates its own guest when no session exists. A future explicit development sign-out operation could call FirebaseAuth.SignOut then SignInAnonymouslyAsync for that client only; no such command/UI was added here. Do not delete Firebase users or spoof a UID.

## Reproduction and source changes

Added Assets/_Domino/Editor/DominoDevelopmentBuild.cs (+ meta), menu Domino > Build > Windows Development Duel Client. It builds Addressables, checks errors, builds Windows Development and verifies required runtime output. Batch method: Domino.Editor.DominoDevelopmentBuild.BuildWindowsDuelClient. Optional -dominoBuildOutput overrides destination.

Built from an isolated project synchronized from all current Assets, Packages and ProjectSettings at client/Validation/Generated/M2Unity so the user's open Editor is not interrupted. Both local settings assets were copied byte-for-byte. No source gameplay, backend, catalog, economy or project configuration was edited. No existing build folder needed deletion. Existing pending changes remain untouched.

FILES_CREATED_THIS_TASK=DominoDevelopmentBuild.cs, its .meta, this report; ignored build artifacts
COMMIT=NONE
PUSH=NONE
DEPLOY=NONE
