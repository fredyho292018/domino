# UI-02B.1 Production Home migration

## Final checkpoint authorization and approved baseline

MANUAL_VISUAL_REVIEW=APPROVED by the user. This section supersedes pending approval/status statements in the historical record below. Base: `363bf219f14e4ecb8de01457836dc48cbbb49dd9`. Dedicated checkpoint message: `feat: migrate approved production home view`; authorized destination: `main -> origin/main`.

No production or validator source changed during checkpoint preparation. Preserve the approved Unity evidence: 680 Home/shell checks PASS, 41 route checks PASS, centering and horizontal bounds 8/8 PASS, UTF-8 PASS; compiler errors0, current blocking exceptions0, nine preexisting CS0067 warnings. Source hashes are captured in the local checkpoint evidence. Manual approval covers the corrected UTF-8 view.

### Explicit inventory

| File (AppShell-relative unless stated) | Classification |
|---|---|
| ProductionHomePage.cs | PRODUCTION_HOME_SOURCE |
| HomeData.cs | TEMPORARY_HOME_DATA_SOURCE / HOME_MODELS |
| ProductionAppShell.cs | OTHER_REQUIRED_UI02B1: Home composition only |
| Editor/ProductionShellPreview.cs | TESTS / VALIDATION |
| ProductionHomePage.cs.meta | OTHER_REQUIRED_UI02B1: Unity metadata |
| HomeData.cs.meta | OTHER_REQUIRED_UI02B1: Unity metadata |
| client/Validation/UI_02B1_PRODUCTION_HOME_MIGRATION_REPORT.md | REPORT |

Home uses production theme tokens, with zero hardcoded theme colors, and receives isolated fictional data through IHomeDataSource. No dependency on mock classes, host, routing or state exists. The existing Amara image resource retains its historical AppShellMockCoaches name; this is graphic asset reuse, not a mock implementation dependency. The production shell owns the fixed navigation. Continue Learning selects Production Learn; Play only shows a local notice. Gameplay, matchmaking, WebSocket game commands and Social calls from Home remain zero. No backend, Auth or new Menu/Profile/Friends implementation is included.

The seven-file scope excludes all 102 protected files, Generated/temp artifacts and unrelated pending work. Current candidate and staged scans must report zero credential/identity indicators and zero mojibake markers; required staged strings are exact. No source edits or test reruns are necessary for this documentation-only checkpoint preparation.

### Commit / push identity and next baseline

The UI-02B.1 checkpoint is the introducing commit of this report, resolvable with `git log --diff-filter=A --format=%H -- client/Validation/UI_02B1_PRODUCTION_HOME_MIGRATION_REPORT.md`. UI_02B2_BASE_SHA is exactly that commit. A commit cannot embed its own resulting hash; the full commit SHA, observed remote SHA and push outcome are recorded after publication in the final task response and local `Validation/Generated/UI02B1/checkpoint-receipt.json`. Publication is confirmed only after remote SHA equality, not inferred from this pre-commit report.

UI_02B2_STARTED=NO. DEPLOY=NO. Stop after checkpoint publication and verification; no Menu migration is authorized in this task.

## Latest result: UTF-8 source repair and current Unity regression PASS

2026-09-29 current run (22:52 local): corrected three source literals in ProductionHomePage.cs. The damaged sequences exactly reproduce decoding intended UTF-8 bytes as Windows-1252 and saving again as UTF-8. The prior editing path used an implicit Windows encoding on read. UI Toolkit displayed those already-corrupt strings; HomeData and Amara text were intact. Source IO now explicitly uses UTF-8. No runtime transcoding was introduced.

Exact product diff for this task: Loading mojibake -> `Loading…`; play-card mojibake -> `¿Jugamos?`; malformed CTA (including doubled space) -> exact `PLAY →`. No style or geometry statements changed. Editor/ProductionShellPreview.cs adds 15 assertions per viewport: five mounted-text checks and ten font glyph checks. Thus the current Home/shell total rises from 560 to 680. Unicode escapes in expected values independently validate the source strings. The existing real theme font contains all ten requested glyphs. These automated checks establish text and glyph availability; final human visual confirmation remains pending.

Audited all ten production C# files in UI/Theming and UI/AppShell, including Home, Puzzles, Learn, Watch, Menu, Shell Test Detail, theme probe and editor tooling: three corrupt literals before repair, zero suspicious markers afterward. No historical mock edits. Current Unity compilation has zero errors and nine unique CS0067 unused-event warnings in existing validation tooling; no exceptions since the latest compilation. Historical results below are retained as history, not used to claim this run passed.

Current evidence retained under ignored Validation/Generated/UI02B1: utf8-HomeGeometry.result.txt, utf8-UI02AResize.result.txt, utf8-UI02A.result.txt and original escaped utf8-audit.json.

```text
MOJIBAKE_ROOT_CAUSE=SOURCE_UTF8_DECODED_AS_WINDOWS_1252_DURING_PRIOR_EDIT
MOJIBAKE_OCCURRENCES_FOUND=3_BEFORE_0_AFTER
PLAY_TITLE_RENDERED=¿Jugamos?
PLAY_CTA_RENDERED=PLAY →
COACH_GREETING_RENDERED=Hola, soy Amara. / Te enseñaré a jugar dominó.
HOME_UTF8_VALIDATION=PASS
MOJIBAKE_MARKERS_IN_PRODUCTION_HOME=0
REPRESENTATIVE_GLYPHS=10/10_PASS_EACH_OF_8_PRESETS
LAYOUT_CHANGED=NO
LAYOUT_FILES_CHANGED_FOR_FIX=0_GEOMETRY_EDITS_SAME_HOME_SOURCE_FILE
HOME_CENTERING_8_PRESETS=8/8_PASS
HORIZONTAL_OVERFLOW_8_PRESETS=8/8_PASS
ROUTE_PRESERVATION=41_PASS
HOME_AND_SHELL_CHECKS=680_PASS
FAILURES=0
COMPILER_ERRORS=0
WARNINGS=9_UNIQUE_CS0067_EXISTING_TEST_TOOLING
CURRENT_BLOCKING_EXCEPTIONS=0
PREEXISTING_PROTECTED_FILES_MODIFIED=0/102
APP_SHELL_MOCK_CHANGED=NO
SECRET_SCAN=PASS_SCOPED_INDICATOR_SCAN
PREVIEW=HOME_393x852_OPEN
MANUAL_VISUAL_APPROVAL=PENDING
COMMIT=NONE
PUSH=NONE
DEPLOY=NO
UI_02B1_SUCCESS=TECHNICAL_PASS_MANUAL_REVIEW_PENDING
UI_02B2_STARTED=NO
NEXT=FINAL MANUAL PRODUCTION HOME REVIEW
```

## Latest authoritative result: centering corrected and Home validation PASS

PRODUCT_LAYOUT_DEFECT confirmed by diagnostic-only captures before correction. The native ScrollView vertical scrollbar reserved13 units only when content exceeded the small-phone viewport. Minimum fix applied exclusively to ProductionHomePage: hide the visual vertical scroller while retaining scrolling. No content, typography, cards, toolbar, tokens or mock redesign. Editor validator now compares accumulated logical body x plus half width to safe-area center, with symmetric-margin assertions; no screenshot coordinates or relaxed tolerance.

| Logical viewport | Before outer width / center delta | After outer width / left / center delta |
|---|---|---|
| 375×667 | 362 / -6.5 | 375 / 0 / 0 |
| 393×852 | 393 / 0 | 393 / 0 / 0 |
| 768×1024 | 620 / 0 | 620 / 74 / 0 |

375 safe width375, safe left/right0; expected/actual center187.5. Outer left/right margins now0/0; inner visual/card margins24/24 (wrapper includes theme padding). At393 center196.5; tablet768 center384 and outer margins74/74, inner98/98. CONTENT_MAX_WIDTH=620 is the outer themed wrapper contract.

First focused gate: HOME_CENTERING=8/8_PASS, all bounds within safe area and <=620. Then route matrix41 PASS and expanded Home/shell suite560 PASS with zero failures. All eight viewports passed. Small phone natural content remains taller than its559-unit application viewport; scroll reachability and fixed navigation passed. States Content/Loading/Empty/Error, fictional data, Amara texture, no duplicate toolbar, Play notice and Continue Learning->Learn->Home passed. Preview left Home393×852. Manual visual parity approval remains pending.

```text
CLASSIFICATION=PRODUCT_LAYOUT_DEFECT_CORRECTED
FIRST_MISALIGNED_ELEMENT=ScrollView.unity-content-viewport
SCROLLVIEW_AFFECTS_CENTERING=YES_NATIVE_SCROLLBAR_GUTTER
CENTERING_REFERENCE=SAFE_CONTENT_AREA
PRODUCT_FILES_CHANGED_FOR_CENTERING_FIX=ProductionHomePage.cs
VALIDATOR_FILES_CHANGED=Editor/ProductionShellPreview.cs
HOME_CENTERING_8_PRESETS=8/8_PASS
HORIZONTAL_OVERFLOW_8_PRESETS=8/8_PASS
ROUTE_PRESERVATION=41_PASS
HOME_AND_SHELL_CHECKS=560_PASS
BOTTOM_NAV_FIXED=PASS
SAFE_AREA=PASS_SIMULATED_INSETS
HOME_TAB_ACTIVE=PASS
PLAY_REAL_GAME_CALLS=0
MATCHMAKING_CALLS_FROM_HOME=0
SOCIAL_CALLS_FROM_HOME=0
BACKEND_CHANGED=NO
APP_SHELL_MOCK_CHANGED=NO
COMPILER_ERRORS=0
CURRENT_BLOCKING_EXCEPTIONS=0
PREEXISTING_PROTECTED_FILES_MODIFIED=0/102
SECRET_SCAN=PASS
COMMIT=NONE
PUSH=NONE
DEPLOY=NO
UI_02B1_SUCCESS=YES_TECHNICAL_VALIDATION
UI_02B2_STARTED=NO
NEXT=MANUAL_PRODUCTION_HOME_REVIEW
```

Prior Centered assertion exceptions are retained in the historical Unity log and diagnosis below; none occurred during the successful corrected run. Existing Auth43, matchmaking75, History/Replay1595 and Social89 regression results remain applicable because those sources were not modified.

## Historical implementation / diagnostic log

Base SHA: 363bf219f14e4ecb8de01457836dc48cbbb49dd9 verified before edits.

## Audit and mapping

Approved MockShellView Home renders identity, greeting, subtitle, ¿Jugamos? card with PLAY, selected coach card with circular portrait, Continue Learning, and Your table of friends. Mock supporting text: Strategy, connection and a little Cuban spirit. The approved coach contract includes display name and greeting; Amara resource exists as AppShellMockCoaches/coach_amara. Production previously contained only club title and Home. Migration reuses ThemeProvider/ThemeStyles/ThemeButton and the existing portrait asset without referencing mock host/view/state/routing. Explore Friends and Secure account are intentionally excluded: no Friends/Auth migration is authorized.

## Data boundary

ProductionHomePage consumes IHomeDataSource.Read() -> HomeSummary, HomeCoachSummary and HomeFriendsSummary. DemoHomeDataSource is selected by shell composition and supplies fictional Alex, Amara and five fictional players. The view only receives models, not service-specific state. Content/Loading/Empty/Error are supported; demo supplies Content. No asynchronous/network behavior, real identity or persistence is introduced. Avatar resource loading resides in the temporary source; view receives Texture2D.

## View and routes

Home uses existing ProductionRootPage scroll/620-wide centered body. Typography, palette, cards and CTA consume UI-01 tokens. Portrait remains 120 logical units; no device-specific layout. The shell still owns the one fixed bottom toolbar. Continue Learning invokes the supplied callback selecting the existing Learn root. Play only reveals Coming Soon. Coach and friends are informational. No matchmaking, WebSocket or legacy Jugar calls exist in Home. Other four placeholders are unchanged.

## Validation progress

Local compilation PASS. Existing regressions run now: Auth43, matchmaking75, History/Replay1595, Social89 PASS; Firestore calls0. Protected102 hashes intact and approved mock unchanged. First current Unity run reached Home375×667, content731 with viewport559, then failed Centered. Validator performed layout reads immediately after toggling notice and Learn/Home routes. It was corrected to schedule geometry after a layout delay; no view change was made for that attempt. Retest pending Unity import; do not declare visual parity or success from local compilation.

FILES_CHANGED: ProductionAppShell.cs (replace placeholder/composition only); new HomeData.cs and ProductionHomePage.cs plus metadata; Editor/ProductionShellPreview.cs (Home tests and delayed geometry); this report.

COMMIT=NONE
PUSH=NONE
DEPLOY=NO
UI_02B1_SUCCESS=PENDING_UNITY_RETEST
UI_02B2_STARTED=NO
NEXT=MANUAL_PRODUCTION_HOME_REVIEW_AFTER_VALIDATION

## Retest finding

The delayed geometry retest still failed Centered on 375×667. Therefore the earlier timing hypothesis is not confirmed and is not the root-cause conclusion. Forty-one resize cases passed in this run, but the expanded Home suite stopped at the first preset. Additional failure diagnostics now include body, parent and viewport logical bounds and await Unity import. No change to ThemeStyles/tokens and no relaxed assertion. UI_02B1_SUCCESS remains NO_PENDING_CENTERING_DIAGNOSIS; responsive/visual parity is not approved.

## Measured centering diagnosis (diagnostic-only first)

Classification: PRODUCT_LAYOUT_DEFECT. Before the fix, 375×667 safe width375, PageHost width375 at x0, Home ScrollView width375 at x0; its internal unity-content-viewport and content-container narrowed to362 at x0. Body width362, x0, maxWidth620, symmetric internal padding24. Actual center181 vs expected187.5, delta -6.5. Outer left/right margins0/13; inner content/card margins24/37. First asymmetric element is unity-content-viewport when the automatic vertical scrollbar reserves13 logical units. At393×852 there is no natural vertical overflow: body393, center196.5, delta0. At768×1024 body620, left74, center384, delta0. No fixed620 phone width, double padding or asymmetric explicit margins was present.

Coordinates are logical accumulated layout.x offsets up to the shell; transform scale is not included. Centering reference is SAFE_CONTENT_AREA (safeLeft/right0 in these captures). Body is an outer wrapper including24+24 padding; therefore expected outer width is min(safeWidth,620). Inner available width subtracts48; the equivalent inner margins are documented separately, not double-counted as external margins.

Authorized minimal correction: ProductionHomePage sets verticalScrollerVisibility=Hidden, preserving the application ScrollView while removing the scrollbar gutter from the Home layout. No cards, text, tokens, shared theme, toolbar or mock changes. Validator measures body left/width against logical safe-content center and symmetric margins. A focused eight-size geometry pass precedes shell/Home regression tests. Current fix validation pending import.
