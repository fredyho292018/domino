# UI-02A Production App Shell foundation

## Latest authoritative validation: resize fix PASS

This section supersedes pending/failed statuses in the historical log below. Only Editor/ProductionShellPreview.cs changed for this fix; runtime navigation/theme/host sources remained byte-identical to the captured pre-fix hashes.

Root cause: the PopupField RegisterValueChangedCallback invoked Mount(false), which cleared the Editor tree and instantiated ProductionAppShell; its normal constructor selects Home. Navigation state owner remains ProductionAppShell.ActiveTab. Preview now routes preset changes through instance method Resize(int), invoking Mount(false, true). Mount captures the existing root tab and HasSubpage before reconstruction, restores the tab via Select and optionally restores the minimal detail through OpenDetail. No static state, restart persistence, scroll persistence or production navigation changes were introduced. A newly opened preview still calls Mount without preservation and starts Home.

Current Unity run: 40 root-route resize cases (five roots × eight transitions including wraparound) passed with logical-size assertions. One extra detail-preservation/Back case passed: 41 focused cases total, zero failures. Only after those passed, the complete existing suite reran: 312 checks passed, eight responsive presets, zero failures. Final programmatic demonstration Learn at 393×852 -> 768×1024 and Menu -> 375×667 passed. Preview was then reset and left open Home at 393×852.

```text
CLASSIFICATION=FIXTURE_DEFECT_CORRECTED
PREVIEW_RESIZE_HANDLER=ProductionShellPreview.Resize
PREVIEW_REBUILD_METHOD=ProductionShellPreview.Mount
NAVIGATION_STATE_OWNER=ProductionAppShell.ActiveTab
PRODUCTION_NAVIGATION_SOURCE_CHANGED_FOR_FIX=NO
OPEN_NEW_PREVIEW_DEFAULT_ROUTE=HOME
RESIZE_EXISTING_PREVIEW_PRESERVES_ROUTE=YES
HOME_RESIZE=8_PASS
PUZZLES_RESIZE=8_PASS
LEARN_RESIZE=8_PASS
WATCH_RESIZE=8_PASS
MENU_RESIZE=8_PASS
SUBPAGE_RESIZE_AND_BACK=PASS
ROUTE_PRESERVATION_TESTS_RUN=41
ROUTE_PRESERVATION_TESTS_PASS=41
ROUTE_PRESERVATION_TESTS_FAIL=0
ROUTE_PRESERVATION_ON_RESIZE=PASS
UNITY_CHECKS_RUN=312
UNITY_CHECKS_PASS=312
UNITY_CHECKS_FAIL=0
RESPONSIVE_PRESETS=8_PASS
BOTTOM_NAV_COUNT=5
BOTTOM_NAV_ORDER=HOME,PUZZLES,LEARN,WATCH,MENU
LEARN_POSITION=CENTER
PAGE_HOST=ONE_RETAINED_HOST_PER_SHELL
BOTTOM_NAV_FIXED=PASS
SAFE_AREA=PASS_SIMULATED_INSETS
BACK_TEST_ROUTE=MENU_DETAIL_MENU_PASS
DEFAULT_THEME=ModernSocialPremium
THEME_PROVIDER_INSTANCE_COUNT=1
NEW_SHELL_HARDCODED_THEME_COLORS=0
LEGACY_UI_CHANGED=NO
GAMEPLAY_CHANGED=NO
AUTH_CHANGED=NO
BACKEND_CHANGED=NO
APP_SHELL_MOCK_REGRESSION=PASS_RETAINED_SOURCE_UNCHANGED
PREEXISTING_PROTECTED_FILES_MODIFIED=0/102
SECRET_SCAN=PASS
COMPILER_ERRORS=0
CURRENT_BLOCKING_EXCEPTIONS=0
NON_BLOCKING_WARNINGS=9_EXISTING_CS0067
COMMIT=NONE
PUSH=NONE
DEPLOY=NO
UI_02A_SUCCESS=YES
UI_02B_STARTED=NO
NEXT=MANUAL_PRODUCTION_APP_SHELL_REVIEW
```

Manual production-shell visual review is APPROVED by the user. No claim of physical-device certification or live authenticated legacy gameplay is made. The new tests use mounted Unity UI and logical dimensions; existing static asset reuse and boundaries remain documented below.

## Historical implementation and validation log

Base: 86c2ce0abf2763237223c39eff32c1a698996c34, verified before changes.

## Entry audit

Production scene: Assets/_Domino/Scenes/DominoClient.unity (enabled build scene). Root: DominoClient with Domino.Client.DominoClientController. Its Start coroutine initializes localization, resolves bundled game configuration, verifies tile/player prefabs, ensures EventSystem and creates the uGUI StartMenuView named Domino Start Menu. ApplicationServices initializes before scene load and owns Firebase/identity, APIs, catalog and realtime. Authentication is service-owned rather than a new shell entry gate.

Legacy Home is StartMenuView.MainMenu. Jugar selects a mode and raises StartRequested -> DominoClientController.StartFromMenu: local StartMatch or Online.MatchmakingView. HistoryRequested creates HistoryReplayView using OnlineApi, tile/player prefabs and a callback to MainMenu. SocialRequested creates SocialView with SocialApi, current identity and Realtime, returning to MainMenu. Those callbacks, services, prefabs, scene and controller remain unchanged.

## Architecture and migration boundary

ProductionAppShell is a runtime UI Toolkit VisualElement outside AppShellMock. One PageHost retains five ProductionRootPage ScrollViews; an enum ShellTab tracks the selected root. Only one root is displayed. The persistent BottomNavigation is a sibling after the flexing PageHost, never inside application scroll. ThemeStyles, ThemeButton and the singleton ThemeProvider provide styling. Five minimal concrete pages: ProductionHomePage, ProductionPuzzlesPage, ProductionLearnPage, ProductionWatchPage, ProductionMenuPage. Menu -> Shell Test Detail -> arrow Back uses a single parent-preserving subpage; no speculative router/persistence system.

ProductionShellHost mounts that same shell in a UIDocument and converts Screen.safeArea pixel insets to logical root units. The standalone Editor preview mounts the same runtime element, with uniform scale and an absolute fixed logical frame. Eight requested sizes are covered with synthetic top/bottom insets of 24 logical units; this is not a physical-device certification. Tests add temporary tall content to exercise nonzero application scroll and verify that navigation stays fixed; test content is removed before review.

Editor-only menu: Domino / Production App Shell / Open preview. Separate Editor-only Play Mode entry / PRODUCTION_SHELL requires an idle existing legacy main menu, hides it and mounts a child of DominoClient. LEGACY reverses that switch. No saved preference, startup hook, build-visible switch or automatic Play Mode transition. Default production startup remains legacy. Play Mode entry is not exercised automatically because it would start existing authenticated services; preview validation does not call them.

No dependency on mock host/view/state/routing. Approved static icon assets remain loaded from AppShellMockIcons resources; UI-01 similarly owns reuse of existing font/Back resources. This is an explicit asset-path dependency, not independence from all mock-named asset folders. No asset duplication or relocation was performed.

## Tests and current state

GUEST_AUTH=43_PASS
MATCHMAKING=75_PASS
HISTORY_REPLAY=1595_PASS
SOCIAL=89_PASS
MOCK_PREVIEW_ROUTING=47_PASS
MOCK_THEME=12_PASS
MOCK_BACK=33_PASS
MOCK_PANEL_MOUNTED=YES
PROTECTED_FILES=102_INTACT
LEGACY_SOURCE_CHANGED=NO
NEW_SHELL_HARDCODED_THEME_COLORS=0
LOCAL_REFERENCE_COMPILATION=PASS_ALL_FOUR_FILES
UNITY_SHELL_VALIDATION=PENDING_IMPORT
PRODUCTION_PREVIEW_OPENED=PENDING_IMPORT
UI_02A_SUCCESS=PENDING

Four additive source files and metadata in Scripts/UI/AppShell: ProductionAppShell.cs, ProductionShellHost.cs, Editor/ProductionShellPreview.cs, Editor/ProductionShellDevelopmentEntry.cs. No existing product file edited. Regression suites cover existing logic/contracts; a live legacy gameplay/auth navigation session is not claimed.

COMMIT=NONE
PUSH=NONE
DEPLOY=NO
UI_02B_STARTED=NO
NEXT=MANUAL_PRODUCTION_APP_SHELL_REVIEW_AFTER_UNITY_VALIDATION

SECRET_SCAN=PASS

## Current Unity validation — STOP on uncovered resize defect

A fresh UI02A.request was consumed in this continuation. Result: all eight presets PASS, CHECKS=312_PASS, FAIL=0. Unity compiled all four files, with zero observed compiler errors/current blocking exceptions and nine distinct existing CS0067 unused-event warnings. The production window is mounted, titled PRODUCTION APP SHELL, returned by the runner to Home at its prepared default 375×667.

This is NOT full UI-02A acceptance. Source review of the newly required resize-preservation contract found FIXTURE_DEFECT: ProductionShellPreview.cs line 32 calls Mount(false) when the viewport selector changes; Mount clears the root at line 30 and creates a new ProductionAppShell at line 38. Its constructor unconditionally selects Home (ProductionAppShell.cs line 76). Thus Learn/Menu state is discarded by the preview viewport change. No runtime click test of that additional transition is claimed; this is a direct source-contract failure. The prepared suite does not test resize retention. No code was modified after classification; full acceptance remains stopped. Do not count this source finding as an extra Unity test.

```text
ASSETS_REFRESH=PASS
COMPILER_ERRORS=0
CURRENT_BLOCKING_EXCEPTIONS=0
NON_BLOCKING_WARNINGS=9_CS0067
PRODUCTION_SHELL_PREVIEW_OPENED=YES
PRODUCTION_SHELL_ROOT=Domino.UI.AppShell.ProductionAppShell
PAGE_HOST_TYPE=RETAINED_VISUAL_ELEMENT
ROOT_PAGE_HOST_COUNT=1
BOTTOM_NAV_COUNT=5
BOTTOM_NAV_ORDER=HOME,PUZZLES,LEARN,WATCH,MENU
LEARN_POSITION=CENTER
HOME=PASS_PREPARED_ROUTE_CHECK
PUZZLES=PASS_PREPARED_ROUTE_CHECK
LEARN=PASS_PREPARED_ROUTE_CHECK
WATCH=PASS_PREPARED_ROUTE_CHECK
MENU=PASS_PREPARED_ROUTE_CHECK
ROOT_BACK_ICON_PRESENT=NO
BACK_TEST_ROUTE=MENU_DETAIL_MENU_PASS
BACK_TOUCH_TARGET=AT_LEAST_44_LOGICAL_PASS
BOTTOM_TAB_TOUCH_HEIGHT=AT_LEAST_60_LOGICAL_PASS
BOTTOM_NAV_FIXED=PASS
SAFE_AREA=PASS_SIMULATED_24_TOP_AND_BOTTOM
DEFAULT_THEME=ModernSocialPremium
SHELL_THEME_RESOLUTION=PASS
THEME_PROVIDER_INSTANCE_COUNT=1
NEW_SHELL_HARDCODED_THEME_COLORS=0
375x667=PASS_PREPARED_SUITE
393x852=PASS_PREPARED_SUITE
412x915=PASS_PREPARED_SUITE
430x932=PASS_PREPARED_SUITE
480x1040=PASS_PREPARED_SUITE
600x960=PASS_PREPARED_SUITE
768x1024=PASS_PREPARED_SUITE
834x1194=PASS_PREPARED_SUITE
CONTENT_MAX_WIDTH=620
TABLET_CENTERING=PASS
ROUTE_PRESERVATION_ON_RESIZE=FAIL_SOURCE_CONTRACT
FAILURE_CLASSIFICATION=FIXTURE_DEFECT
PRODUCTION_DEPENDS_ON_APP_SHELL_MOCK=NO_BEHAVIOR_DEPENDENCY_STATIC_ASSETS_REUSED
LEGACY_REGRESSION=PREVIOUS_CONTRACT_TESTS_PASS_SOURCE_UNCHANGED
GAMEPLAY_CHANGED=NO
AUTH_CHANGED=NO
BACKEND_CHANGED=NO
PREEXISTING_PROTECTED_FILES_MODIFIED=0/102
SECRET_SCAN=PASS
UNITY_CHECKS_RUN=312
UNITY_CHECKS_PASS=312
UNITY_CHECKS_FAIL=0
UNITY_CHECKS_SKIPPED=0_WITHIN_PREPARED_SUITE
COMMIT=NONE
PUSH=NONE
DEPLOY=NO
UI_02A_SUCCESS=NO_RESIZE_CONTRACT_FAILED
UI_02B_STARTED=NO
NEXT=UI-02A_PREVIEW_RESIZE_FIX_REVIEW
```

Prepared-suite scope limitations: it checks submitted tab roots rather than separately synthesized pointer hits on icon/label/empty tab area; child icon/label picking is ignored by implementation. It verifies one shared PageHost by construction and route selection but does not certify all new checklist items via independent assertions. Existing legacy startup/Jugar/History/Social wiring is untouched and previous logic tests pass; no live authenticated legacy navigation session was started. These boundaries must not be represented as extra executed Unity checks.

## Authorized final checkpoint

Manual review approved the real Production App Shell, five tabs in order, Learn centered, active state, fixed navigation and ModernSocialPremium. Minimal placeholders and Shell Test Detail remain intentionally temporary. No source changes were made for checkpoint preparation. Retain 41 route cases, 312 Unity checks and eight responsive presets. Only UI-02A source, metadata, validation report and inventory are included; protected work and generated outputs are excluded.

Commit message: `feat: add production app shell foundation`. Branch/upstream verified as main/origin/main. Resolve checkpoint SHA with `git log -1 --format=%H -- client/Validation/UI_02A_CHECKPOINT_INVENTORY.csv`. Remote verification and UI_02B_BASE_SHA are recorded in the final delivery and local Generated/UI02A/checkpoint-receipt.json after publication; the commit cannot embed its own hash. UI-02B is not started.
