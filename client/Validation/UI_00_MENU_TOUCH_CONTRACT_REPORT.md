# UI-00 Menu Touch Contract + Final Gate

CLASSIFICATION=OUTDATED_OR_OVERSTRICT_VALIDATOR
MIN_TOUCH_HEIGHT_LOGICAL=44
FRIENDS_TOUCH_HEIGHT=56
MESSAGES_TOUCH_HEIGHT=56
STATS_TOUCH_HEIGHT=56
COACH_TOUCH_HEIGHT=56
THEME_TOUCH_HEIGHT=56
MEMBERSHIP_TOUCH_HEIGHT=56
SETTINGS_TOUCH_HEIGHT=56
HELP_SUPPORT_TOUCH_HEIGHT=56
ENTIRE_MENU_ROW_CLICKABLE=YES
MENU_TOUCH_TARGETS_PASS=8/8_ACROSS_8_PRESETS
PROFILE_HEADER_TOUCH_HEIGHT=50
BOTTOM_NAV_TOUCH_TARGETS=5/5_LOGICAL_HEIGHT_60_WIDTH_AT_LEAST_44
PREVIEW_SCALE_AFFECTS_RENDERED_HEIGHT=YES
PRODUCT_UI_FILES_CHANGED=0
VALIDATOR_FILES_CHANGED=4
OTHER_PHYSICAL_PIXEL_ASSUMPTIONS_FOUND=3_ADDITIONAL
OTHER_VALIDATORS_CORRECTED=Coach_avatar_minimum;Premium_tab_minimum;Menu_text_measurement
FOCUSED_MENU_TEST=960_PASS
COMPILER_ERRORS=0_LOCAL_BUILD

The eight full-row Buttons are 56 logical units high. Their children ignore picking; the root owns the action. Measurements were recorded per row and preset, and actual submission/return routes passed. At scale 0.518426 the row renders at 29.032 high, while its logical interactive height remains 56. Profile measures 50 logical units; all five bottom tabs measure 60 high. The validators now compare touch targets with logical layout dimensions and verify rendered scaling separately, without increasing tolerances or changing functional expectations.

Coach minimum avatar width and Premium minimum tab width now use logical layout values. Menu text measurement is compared with logical label width. Relative world-space alignment checks remain intact. The fourth validator change expands the responsive smoke set to all requested pages and requires mounted, nonblank views; it changes no UI.

## Current execution results

- Menu: 960 PASS across eight presets.
- Local navigation/state: 228 PASS.
- Coach: 354 PASS.
- Premium: 636 PASS.
- Profile: 144 PASS.
- Welcome: 784 PASS.
- Toolbar: 680 PASS across eight presets.
- Original responsive set: 1040 PASS in this run (48 page/preset combinations).

TESTS_RUN=4826_COMPLETED_ASSERTIONS
TESTS_PASS=4826
TESTS_FAIL=0
TESTS_SKIPPED=FINAL_EXPANDED_SMOKE_AND_ROUTING_RERUN_PENDING_IMPORT

The expanded responsive smoke now includes Welcome, Experience, Coach selection, Contacts, Membership, Home, Puzzles, Learn, Watch, Menu, Profile plus ContactResults and Trial. Its source compiles locally, but Unity has not yet imported that last change. Its results are not claimed. User import request is pending. No previous-session test totals are included above.

VISUAL_SMOKE_TEST=PARTIAL_EXPANDED_COVERAGE_PENDING
RESPONSIVE_PRESETS=8_PASS_ORIGINAL_SET_EXPANDED_SET_PENDING
SECRET_SCAN=PRELIMINARY_CANDIDATE_REVIEW_CLEAR_FINAL_GATE_PENDING
STAGED_FILE_COUNT=0
PREEXISTING_FILES_ACCIDENTALLY_STAGED=0
STAGED_SECRET_SCAN=NOT_RUN
COMMIT_SHA=NONE
PUSH=NOT_RUN
REMOTE_SHA=NOT_VERIFIED
APP_SHELL_PENDING_AFTER_COMMIT=NOT_APPLICABLE
PREEXISTING_PENDING_FILES_REMAINING=102_CONFIRMED_PROTECTED
UI_01_BASE_SHA=NOT_CREATED
UI_00_SUCCESS=NOT_YET
UI_01_STARTED=NO
NEXT=UI-00 FINAL_SMOKE_IMPORT_AND_EXECUTION

PREEXISTING_PROTECTED_FILES_MODIFIED=0
