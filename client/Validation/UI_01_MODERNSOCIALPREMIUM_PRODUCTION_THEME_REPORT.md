# UI-01 Production theme system

## Latest authoritative result — viewport fixture correction

UI_01_TECHNICAL_VALIDATION=PASS; UI_01_SUCCESS=YES. This result supersedes the pending/failed historical attempts below. Final checkpoint is authorized; see the checkpoint section for scope and SHA resolution.

The original frame was a normal flex child of the 440×700 Editor root, with no minimum height or non-shrinking constraint. Requested height was reduced by parent layout before the visual transform; minSize(440,700) was a window minimum, not an explicit logical maxHeight=700. The fix is restricted to ThemeProbeValidation.cs: an absolutely positioned non-shrinking logical frame with width/minWidth/maxWidth and height/minHeight/maxHeight set to requested dimensions. Uniform visual scaling remains separate. RequestedViewportWidth, RequestedViewportHeight and ActualLogicalViewportSize expose the contract. No production tokens, component styles or ThemeProbeView were edited for this fix.

PHYSICAL_EDITOR_LIMIT=ACTUAL_DISPLAY_440x700_NOT_A_REQUIRED_MAXIMUM
LOGICAL_VIEWPORT_LIMIT=REQUESTED_SIZE_INDEPENDENT_OF_DISPLAY

Fixture validation ran first: 15 assertions, all five exact frame/application viewport sizes passed. Only then did the runner execute the current 195 overflow/scroll assertions. No earlier 200/196/4 results were reused. The 265 base checks are retained as authorized because runtime sources are byte-identical to the before-fix snapshot.

| Requested / actual logical | Display scale | Content width | Content height | Scroll required | Overflow / reachability |
|---|---:|---:|---:|---|---|
| 375×667 / 375×667 | 1 | 375 | 666 | No | PASS |
| 393×852 / 393×852 | 0.8215963 | 393 | 666 | No | PASS |
| 412×915 / 412×915 | 0.7650273 | 412 | 666 | No | PASS |
| 768×1024 / 768×1024 | 0.5729167 | 620 | 666 | No | PASS |
| 834×1194 / 834×1194 | 0.5275779 | 620 | 666 | No | PASS |

Both tablets are horizontally centered. Checks use logical layout, not scaled screenshot pixels. All current content fits, so these results verify first/final/control reachability without claiming exercised nonzero scrolling. There is no Editor scroll substitute or bottom overlay in this isolated probe; physical safe-area simulation is outside this fixture.

```text
FAILURE_CLASSIFICATION=PROBE_VIEWPORT_FIXTURE_CORRECTED
HEIGHT_CAP_SOURCE=FLEX_CHILD_SHRINKING_TO_EDITOR_ROOT_HEIGHT
SCALE_TO_FIT=YES_UNIFORM
VIEWPORT_375x667_ACTUAL=375x667
VIEWPORT_393x852_ACTUAL=393x852
VIEWPORT_412x915_ACTUAL=412x915
VIEWPORT_768x1024_ACTUAL=768x1024
VIEWPORT_834x1194_ACTUAL=834x1194
VIEWPORT_FIXTURE=5/5_PASS
CONTENT_MAX_WIDTH=620
375x667_OVERFLOW=PASS
375x667_SCROLL=PASS_NOT_REQUIRED
393x852_OVERFLOW=PASS
393x852_SCROLL=PASS_NOT_REQUIRED
412x915_OVERFLOW=PASS
412x915_SCROLL=PASS_NOT_REQUIRED
768x1024_OVERFLOW=PASS
768x1024_SCROLL=PASS_NOT_REQUIRED
768x1024_CENTERED=YES
834x1194_OVERFLOW=PASS
834x1194_SCROLL=PASS_NOT_REQUIRED
834x1194_CENTERED=YES
OVERFLOW_SCROLL_CHECKS_RUN=195
OVERFLOW_SCROLL_CHECKS_PASS=195
OVERFLOW_SCROLL_CHECKS_FAIL=0
BASE_PROBE_CHECKS=265_PASS_RETAINED
PRODUCTION_THEME_FILES_CHANGED_FOR_FIX=0
THEME_PROVIDER_TYPE=Domino.UI.Theming.ThemeProvider_STATIC_SINGLETON
THEME_STORAGE_TYPE=AppTheme_READONLY_CSHARP_TOKEN_GROUPS_IN_MEMORY
TOKEN_ACCESS_PATTERN=ThemeProvider.Current_OR_Resolve().Colors/Typography/Spacing/Radius/Sizing
COMPONENT_STYLE_ACCESS_PATTERN=Domino.UI.Theming.ThemeStyles_AND_ThemeButton
PAGE_SPECIFIC_THEME_COPY_COUNT=0
PRODUCTION_THEME_LITERAL_COLOR_COUNT=0_OUTSIDE_CANONICAL_TOKEN_DEFINITION
PRODUCTION_THEME_HARDCODED_APPROVED_COLORS=0_OUTSIDE_CANONICAL_TOKEN_DEFINITION
MOCK_HARDCODED_APPROVED_COLORS=10
COMPILER_ERRORS=0
CURRENT_BLOCKING_EXCEPTIONS=0
NON_BLOCKING_WARNINGS=9_DISTINCT_CS0067_EXISTING_VALIDATORS
PREEXISTING_PROTECTED_FILES_MODIFIED=0/102
SECRET_SCAN=PASS
UI_01_TECHNICAL_VALIDATION=PASS
UI_01_SUCCESS=YES
COMMIT=NONE
PUSH=NONE
DEPLOY=NO
PRODUCTION_APP_SHELL_STARTED=NO
UI_02_STARTED=NO
NEXT=UI-01_FINAL_ARCHITECTURE_AND_CHECKPOINT_REVIEW
```

Color-count scope is consumer literals outside canonical AppTheme.cs; canonical token definitions necessarily contain the approved hexadecimal values. Editor expected-value assertions are test contracts. Preserved mock literals were not refactored. Remaining architecture review points: existing font/Back resources are reused from AppShellMock resource paths; ancestor enabled-state changes require ThemeButton.RefreshState. Neither was altered in this fixture fix.

## Historical work log (superseded statuses retained for traceability)

## Completed audit before implementation

Base 87d40cb748b4a5c3e60c19d8d811708a4301aaaa verified. Legacy runtime is programmatic uGUI: UiKit constructs Text/Button/Image and uses built-in LegacyRuntime.ttf. DominoVisualTheme is a static board/presentation palette whose Name says ModernSocialPremium but whose colors predate the approved mock. StartMenuView contains literal green colors and dimensions, and connects Play/History/Social to existing events. TileStyles is the saved tile-skin selector, not application chrome. MockShellTheme and MockButtonTypography are scoped UI Toolkit styling with bundled Source Sans 3 Medium/Semibold/Bold. No application USS/UXML or theme ScriptableObject was found; existing ScriptableObjects are API/Ads settings, outside scope. Localization passes keys through DominoLocalization, independent of colors. Existing Addressables/localization pending assets need no change.

Decision: introduce an opt-in immutable application-chrome definition and authoritative ThemeProvider in Domino.UI.Theming; preserve existing legacy board/uGUI palette and helpers. Reuse UiKit.Hex and existing bundled font/arrow resources. Do not download/move assets, alter global font, attach to startup scenes or refactor the mock. New UI Toolkit components resolve the same singleton on construction; UI-02 owns mounting and any future resource relocation. CONTENT_MAX_WIDTH=620 logical units follows the approved preview maximum. No persistence or alternate themes.

This is an additive production-consumable theme layer, not a second selectable legacy skin implementation. The isolated probe proves resolution/rendering; it does not replace Home.

## Implemented architecture

Four additive C# files: AppTheme.cs (immutable token groups and singleton resolution), ThemeStyles.cs (opt-in styles and ThemeButton interaction state), ThemeProbeView.cs (isolated runtime-consumable view), Editor/ThemeProbeValidation.cs (five-size mounted validation). All original files remain byte-identical. New folder/file metadata is included. No production scene or bootstrap references were changed.

ThemeProvider.Current / Resolve returns the same immutable AppTheme. Null preference resolves MODERN_SOCIAL_PREMIUM; unsupported enum values are rejected. No persistence, preferences UI, network or service access. Future UI-02 constructs components using the provider; existing legacy initialization remains unchanged intentionally.

Approved palette: primary 71A84B, background 2A2623, surface 41403C, text primary/icon active FBFAFA, text secondary/icon inactive 969495. Semantic colors: success/selected/family use primary, disabled uses secondary; error E89898, warning/gold E5BE67, info/diamond 69B9E8, platinum C9CDD5, win B8DEA5, loss E8B0B0. Semantic values other than the exact approved seven are documented compatible choices for architecture review.

Typography sizes/weights: title 29/700, section 20/700, body 15/500, secondary 13/500, caption 11/500, primary button 15/700, secondary button 15/600, nav label 11/600. Existing bundled Source Sans faces are reused only for new opt-in components; global LegacyRuntime.ttf and UiKit.Font remain unchanged. Fonts and Back SVG are currently read from existing AppShellMock resource paths to avoid asset duplication/migration. This explicit resource dependency is a review point for UI-02, not a dependency on MockShellState/View.

Spacing: 4/8/12/20/24, semantic aliases for margins/cards/rows/sections/icon gaps. Radius: card16/button12/input10/segmented12/icon17. Logical sizing: min touch44, button48, menu56, profile50, bottom tab60, nav icon26, auth56, icon container34, max content620. No viewport-specific rules in runtime tokens.

Style contracts cover primary/secondary buttons, selected surface cards, input and placeholder/focus, icons, supplied Back root/icon, future five-tab styling, full clickable menu/auth rows and segmented container/buttons. Selection belongs to the consumer; segmented styling does not implement billing. ThemeButton provides normal/hover/pressed/selected/disabled/focused rendering. For an ancestor-enabled change, a consumer calls RefreshState; no page-level theme allocation is needed. Back styling takes an existing Button/Image and reuses the approved arrow asset; no navigation routing implementation was added.

## Current tests

LOCAL_COMPILER_ERRORS=0
GUEST_AUTH_REGRESSION=43_PASS
GAME_CATALOG_GAMEPLAY_REGRESSION=251685_PASS
HISTORY_REPLAY_REGRESSION=1595_PASS
SOCIAL_CLIENT_REGRESSION=89_PASS
REAL_FIRESTORE_CALLS=0
LEGACY_ASSET_HASH_REGRESSION=PASS_ALL_EXISTING_ASSETS_UNCHANGED
PREEXISTING_102_FILES_MODIFIED=0

These are existing logic/service-contract tests plus a byte-level preservation check of legacy startup/navigation/UI. They do not claim a live authenticated legacy server session or a new manual gameplay run. The current production start-menu Play/History/Social event wiring remains untouched. No mock visual rerun is needed because its source/assets are unchanged.

THEME_AND_COMPONENT_EDITOR_TESTS=PENDING_UNITY_IMPORT
RESPONSIVE_PROBE_TESTS=PENDING_375x667_393x852_412x915_768x1024_834x1194

## Boundaries

DEFAULT_THEME=ModernSocialPremium (new opt-in production theme provider)
LEGACY_UI_CHANGED=NO
BACKEND_SOURCE_CHANGED=NO
FIRESTORE_SCHEMA_CHANGED=NO
REDIS_CHANGED=NO
REAL_AUTH_CHANGED=NO
PRODUCTION_APP_SHELL_STARTED=NO
COMMIT=NONE
PUSH=NONE
DEPLOY=NO
UI_01_SUCCESS=NOT_YET_PROBE_PENDING
NEXT=UI-01 ARCHITECTURE REVIEW_AFTER_PROBE_VALIDATION

SECRET_SCAN=PASS
SECRETS=0
RAW_UIDS=0
PRIVATE_KEYS=0
SIGNING_MATERIAL=0

## Current Unity probe attempt — 2026-09-29 21:28 local

Unity consumed UI01Theme.request and ran the imported validator. This attempt FAILED with NullReferenceException in UnityEngine.UIElements.FocusOutEvent.PostDispatch, originating from the validator's manually pooled FocusOutEvent. This is a validator event-construction defect, not evidence of a broken theme provider. No successful five-viewport result is claimed.

Minimal correction: validator now calls VisualElement.Focus()/Blur() instead of synthesizing focus events. The isolated ThemeProbeView additionally mounts the previously missing five-tab navigation representation using ThemeStyles.BottomTab. This changes only new UI-01 probe/validation files; no architecture or legacy UI change. Added checks cover tab logical touch/icon dimensions and content centering. Local reference compilation passes; Unity reimport and a fresh run remain required.

CURRENT_PROBE_RESULT=FAIL_BEFORE_VALIDATOR_CORRECTION
CURRENT_BLOCKING_EXCEPTIONS=1_VALIDATOR_NULL_REFERENCE
CORRECTED_PROBE_UNITY_RUN=PENDING_IMPORT
PREEXISTING_102_FILES_MODIFIED=0
ALL_PREEXISTING_ASSET_HASH_CHANGES=0
APP_SHELL_MOCK_CHANGED=NO
UI_01_SUCCESS=NO_PENDING_CORRECTED_PROBE
UI_02_STARTED=NO
COMMIT=NONE
PUSH=NONE
DEPLOY=NO

### Corrected Unity execution

After manual refresh, Unity executed the corrected validator: THEME_CHECKS=265_PASS; all five required presets PASS. No exception occurred in that execution. The initial exception remains recorded above. Latest import had zero compiler errors and nine distinct CS0067 warnings (unused events in existing legacy validation fixtures), unrelated to UI-01.

Additional explicit horizontal-bound and bottom-scroll reachability assertions have now been prepared and locally compiled. They require another Unity import/run; the 265 result does not cover those newly added assertions. UI_01_SUCCESS remains PENDING_FINAL_IMPORT, not PASS.

THEME_PROVIDER_TYPE=STATIC_SINGLETON
THEME_STORAGE_TYPE=IN_MEMORY_CSHARP_READONLY_TOKEN_GROUPS
TOKEN_ACCESS_PATTERN=ThemeProvider.Current_OR_Resolve().Colors/Typography/Spacing/Radius/Sizing
COMPONENT_STYLE_ACCESS_PATTERN=ThemeStyles_AND_ThemeButton
PROBE_USES_THEME_PROVIDER=YES
ACTIVE_THEME_COUNT=1
CONTENT_MAX_WIDTH=620_LOGICAL
TABLET_CENTERING=PASS_IN_265_CHECK_RUN
SECRET_SCAN=PASS
RAW_FIREBASE_UIDS=0
TOKENS=0
PASSWORDS=0
PRIVATE_KEYS=0
SIGNING_PRIVATE_MATERIAL=0
LOAD_IDENTITIES=0
PREEXISTING_102_FILES_MODIFIED=0
UI_02_STARTED=NO

## FINAL CURRENT GATE — overflow/scroll execution (2026-09-29)

Status: STOP, technical validation NOT PASSED. Only the new overflow/scroll entry point was executed; the base interaction/token tests were not repeated. Production theme and probe component source were unchanged in this turn; only the Editor validator gained a separate request/result path and focused logical-bounds checks.

The mounted suite reported 195/195 checks, but its recorded dimensions reveal an invalid viewport fixture: requested heights 852, 915, 1024 and 1194 all resolve to 700 logical units. Independent automatic audit of the five recorded viewport dimensions passed one and failed four. Consequently the five-size acceptance gate FAILS; no complete responsive success is claimed. The frame/probe hierarchy is constrained by the Editor window height, consistent with flex shrinking. This is a validation-host sizing defect, not evidence of a production theme layout defect. No layout correction was applied after discovery, in accordance with STOP on failure.

Counts are explicit: Unity overflow/scroll assertions = 195 run / 195 pass / 0 fail; additional logical viewport integrity assertions = 5 run / 1 pass / 4 fail. Combined final gate = 200 run / 196 pass / 4 fail. The four affected results are invalid for their advertised sizes, despite internal assertions passing.

```text
BASE_PROBE_CHECKS=265_PASS_RETAINED_NOT_RERUN
COMPILER_ERRORS=0
CURRENT_BLOCKING_EXCEPTIONS=0
NON_BLOCKING_WARNINGS=9_CS0067_EXISTING_VALIDATORS
OVERFLOW_SCROLL_CHECKS_RUN=200
OVERFLOW_SCROLL_CHECKS_PASS=196
OVERFLOW_SCROLL_CHECKS_FAIL=4
375x667_OVERFLOW=PASS
375x667_SCROLL=PASS_CONTENT_FITS_NO_SCROLL_REQUIRED
393x852_OVERFLOW=NOT_VALIDATED_ACTUAL_HEIGHT_700
393x852_SCROLL=NOT_VALIDATED_ACTUAL_HEIGHT_700
412x915_OVERFLOW=NOT_VALIDATED_ACTUAL_HEIGHT_700
412x915_SCROLL=NOT_VALIDATED_ACTUAL_HEIGHT_700
768x1024_OVERFLOW=NOT_VALIDATED_ACTUAL_HEIGHT_700
768x1024_SCROLL=NOT_VALIDATED_ACTUAL_HEIGHT_700
768x1024_MAX_WIDTH=620_CONFIRMED_WIDTH_ONLY
834x1194_OVERFLOW=NOT_VALIDATED_ACTUAL_HEIGHT_700
834x1194_SCROLL=NOT_VALIDATED_ACTUAL_HEIGHT_700
834x1194_MAX_WIDTH=620_CONFIRMED_WIDTH_ONLY
CONTENT_MAX_WIDTH=620_LOGICAL
THEME_PROVIDER_TYPE=STATIC_SINGLETON
THEME_STORAGE_TYPE=IN_MEMORY_READONLY_CSHARP_TOKEN_GROUPS
TOKEN_ACCESS_PATTERN=ThemeProvider.Current_OR_Resolve
COMPONENT_STYLE_ACCESS_PATTERN=ThemeStyles_AND_ThemeButton
HARDCODED_APPROVED_THEME_COLORS_OUTSIDE_THEME_SYSTEM=10_LITERAL_OCCURRENCES_IN_2_EXISTING_MOCK_FILES
PREEXISTING_PROTECTED_FILES_MODIFIED=0_OF_102
SECRET_SCAN=PASS
UI_01_TECHNICAL_VALIDATION=FAIL_VIEWPORT_FIXTURE
UI_01_SUCCESS=NO
COMMIT=NONE
PUSH=NONE
DEPLOY=NO
UI_02_STARTED=NO
NEXT=UI-01_ARCHITECTURE_REVIEW_WITH_PROBE_VIEWPORT_DEFECT
```

Color inventory scope: exact approved hexadecimal literals in Assets .cs/.uss/.uxml/.svg files outside Scripts/UI/Theming. Five occurrences in AppShellMock/MockShellTheme.cs lines 10–14; five expected-value occurrences in AppShellMock/Editor/MockShellPreviewValidation.cs lines 70–74. Colors: 71A84B, 2A2623, 41403C, FBFAFA, 969495, once in each file. No automatic rewriting of existing mock/legacy colors. This literal scan does not claim to detect equivalent numeric RGB encodings.

Current probe has navigation inside scroll content, no overlay or physical-device safe-area simulation. Reachability checks cover direct component bounds, first/last element, viewport capacity and available scroll range. No physical screenshot pixel comparison is used. Existing base 265-check evidence remains recorded, but its responsive coverage also needs the corrected viewport fixture before five-size claims are accepted.

## Authorized final checkpoint

Scope: approved UI-01 theme source, shared styles, isolated probe/Editor validation, Unity metadata and this report/inventory only. Branch main; configured upstream origin/main. Commit message: `feat: add ModernSocialPremium production theme system`. Existing protected 102-file work remains local and excluded. No generated/cache files are candidates. Source is unchanged since the successful 5/5 fixture and 195-check execution; the 265 base checks remain retained.

The checkpoint SHA is the Git commit containing this report and inventory (resolve with `git log -1 --format=%H -- client/Validation/UI_01_CHECKPOINT_INVENTORY.csv`). The verified remote SHA and UI_02_BASE_SHA are recorded in the final delivery and local Generated/UI01/checkpoint-receipt.json after push; embedding a commit's own hash in its contents is not possible. This checkpoint does not authorize starting UI-02.
