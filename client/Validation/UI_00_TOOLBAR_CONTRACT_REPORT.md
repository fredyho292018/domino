# UI-00 Toolbar Contract + Final Gate

CLASSIFICATION=OUTDATED_OR_OVERSTRICT_VALIDATOR
TARGET_LOGICAL_ICON_SIZE=26x26
PREVIEW_SCALE_AFFECTS_RENDERED_DIMENSIONS=YES
HOME_LOGICAL_SIZE=26x26
PUZZLES_LOGICAL_SIZE=26x26
LEARN_LOGICAL_SIZE=26x26
WATCH_LOGICAL_SIZE=26x26
MENU_LOGICAL_SIZE=26x26
ICON_CONTAINER_SIZE_UNIFORM=YES
TAB_WIDTH_UNIFORM=YES
ICON_ALIGNMENT_UNIFORM=YES
PRODUCT_UI_FILES_CHANGED=0
VALIDATOR_FILES_CHANGED=1
TOOLBAR_UNIFORM_ICONS=PASS
RESPONSIVE_TOOLBAR_PRESETS=8_PASS
COMPILER_ERRORS=0_LOCAL_BUILD

Only MockToolbarValidation.cs changed. Configured and resolved icon sizes must be 26x26; rendered sizes must equal each other and 26 times PreviewScale within 0.05 units. Alignment checks remain separate; subjective optical balance is not encoded. Touch targets are measured in logical layout units and the bottom inset comparison now scales the inset consistently. All eight presets are exercised. No icon, layout, color, label or route changed.

## Current focused Unity measurements

Every row below applies individually to all five icons. Configured and resolved sizes are 26x26 in all 40 samples.

| Preset | Preview scale | Rendered icon box |
|---|---:|---:|
|375x667|0.928036|24.129x24.129|
|393x852|0.726526|18.890x18.890|
|412x915|0.676503|17.589x17.589|
|430x932|0.664163|17.268x17.268|
|480x1040|0.595192|15.475x15.475|
|600x960|0.644792|16.765x16.765|
|768x1024|0.604492|15.717x15.717|
|834x1194|0.518426|13.479x13.479|

TOOLBAR_FOCUSED_CHECKS=680_PASS

After focused PASS, reran local source tests: 228 PASS. Started current Menu Editor validation; at 19:50:27 it failed on Touch height. Its six preceding assertions passed. Stopped immediately as required. No historical counts are included below.

TESTS_RUN=915 (680 focused + 228 local + 7 Menu)
TESTS_PASS=914
TESTS_FAIL=1
TESTS_SKIPPED=REMAINING_SUITES_NOT_RUN_AFTER_STOP; EXACT_ASSERTION_TOTAL_NOT_FIXED
COMPLETE_SUITE=STOPPED_AT_MENU_TOUCH_HEIGHT

Menu currently compares row.worldBound.height against 56..64, which may also mix scaled and logical units. This is a diagnosis lead only; Menu source/test was not modified. Current failure is a Menu validation assertion, not the former Toolbar Uniform icons assertion.

SECRET_SCAN=NOT_RUN_AFTER_STOP
STAGED_FILE_COUNT=0
PREEXISTING_FILES_ACCIDENTALLY_STAGED=0
STAGED_SECRET_SCAN=NOT_RUN
COMMIT_SHA=NONE
PUSH=NOT_RUN
REMOTE_SHA=NOT_VERIFIED
APP_SHELL_PENDING_AFTER_COMMIT=NOT_APPLICABLE_NO_COMMIT
PREEXISTING_PENDING_FILES_REMAINING=102_CONFIRMED_PROTECTED
UI_01_BASE_SHA=NOT_CREATED
UI_00_SUCCESS=NO
UI_01_STARTED=NO
NEXT=UI-00 MENU TOUCH HEIGHT CONTRACT REVIEW

PREEXISTING_PROTECTED_FILES_MODIFIED=0
