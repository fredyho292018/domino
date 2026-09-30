# UI-00 Trial Back Contract Review

CLASSIFICATION=OUTDATED_VALIDATOR
TRIAL_BACK_VISIBLE=YES_BY_SOURCE
TRIAL_BACK_CLICKABLE=YES_BY_SOURCE
TRIAL_BACK_ICON_ASSET=AppShellMockIcons/icon_arrow_left
TRIAL_BACK_ACCESSIBLE_NAME=Back (existing tooltip/name contract; screen-reader behavior not independently tested)
VALIDATOR_EXPECTATION=Previously required nonempty SubpageTitle; MembershipScreen intentionally clears it and renders PremiumTitle.
PRODUCT_UI_FILES_CHANGED=0
VALIDATOR_FILES_CHANGED=2
BACK_CONTRACT_DEFECTS_FOUND=0_BY_SOURCE_AND_STATE_TESTS
BACK_VALIDATOR_MISMATCHES_FOUND=3 (Trial, Plans, Membership title-placement assumption)
ROOT_TABS_BACK_ABSENT=PASS_BY_SOURCE
COMPILER_ERRORS=0_LOCAL_BUILD
FOCUSED_TESTS=66_BACK_ROUTE_STATE_CHECKS_PASS
OTHER_CURRENT_TESTS=162_PASS
UNITY_FIX_VALIDATION=PENDING_MANUAL_EXECUTION

The validator now verifies the named BackAction root, exact shared vector asset, Back tooltip, enabled/clickable/focusable root and expected return route. It retains icon tint/target size/no textual Back controls checks. It no longer makes title placement part of the Back icon contract. UI hierarchy, prices, features, styles and navigation code are untouched.

## All-page source audit

For each required page, the shared Rebuild header supplies the visible arrow and clickable 48px target. Stack parents mean the actual caller via Go; direct preview fallback is Welcome or Home according to onboarding completion. Local tests check all 33 required routes and preserve experience, coach, contacts and billing selection. Unity tests of rendered roots and click dispatch remain pending.

| Page | Back required | Present / arrow / clickable (source) | Expected parent |
|---|---|---|---|
| Welcome | NO | Absent by design | Root / initial onboarding |
| SignIn | YES | YES / YES / YES | Caller stack; fallback Welcome/Home |
| EmailRegister | YES | YES / YES / YES | Caller stack; fallback Welcome/Home |
| EmailSignIn | YES | YES / YES / YES | Caller stack; fallback Welcome/Home |
| ExistingEmail | YES | YES / YES / YES | Caller stack; fallback Welcome/Home |
| SendEmail | YES | YES / YES / YES | Caller stack; fallback Welcome/Home |
| CheckEmail | YES | YES / YES / YES | Caller stack; fallback Welcome/Home |
| LoadAccount | YES | YES / YES / YES | Caller stack; fallback Welcome/Home |
| Phone | YES | YES / YES / YES | Caller stack; fallback Welcome/Home |
| Otp | YES | YES / YES / YES | Caller stack; fallback Welcome/Home |
| Provider | YES | YES / YES / YES | Caller stack; fallback Welcome/Home |
| ProviderConflict | YES | YES / YES / YES | Caller stack; fallback Welcome/Home |
| OtherDevice | YES | YES / YES / YES | Caller stack; fallback Welcome/Home |
| SecureAccount | YES | YES / YES / YES | Caller stack; fallback Welcome/Home |
| Experience | NO | Absent by design | Root / initial onboarding |
| CoachSelection | YES | YES / YES / YES | Experience |
| Contacts | YES | YES / YES / YES | CoachSelection |
| ContactResults | YES | YES / YES / YES | Caller stack; fallback Welcome/Home |
| Trial | YES | YES / YES / YES | Contacts |
| Plans | YES | YES / YES / YES | Caller stack; fallback Welcome/Home |
| Home | NO | Absent by design | Root / initial onboarding |
| Puzzles | NO | Absent by design | Root / initial onboarding |
| Learn | NO | Absent by design | Root / initial onboarding |
| Watch | NO | Absent by design | Root / initial onboarding |
| Menu | NO | Absent by design | Root / initial onboarding |
| Friends | YES | YES / YES / YES | Caller stack; fallback Welcome/Home |
| Messages | YES | YES / YES / YES | Caller stack; fallback Welcome/Home |
| Conversation | YES | YES / YES / YES | Caller stack; fallback Welcome/Home |
| Stats | YES | YES / YES / YES | Caller stack; fallback Welcome/Home |
| Coach | YES | YES / YES / YES | Caller stack; fallback Welcome/Home |
| Theme | YES | YES / YES / YES | Caller stack; fallback Welcome/Home |
| Membership | YES | YES / YES / YES | Caller stack; fallback Welcome/Home |
| Settings | YES | YES / YES / YES | Caller stack; fallback Welcome/Home |
| Support | YES | YES / YES / YES | Caller stack; fallback Welcome/Home |
| Play | YES | YES / YES / YES | Caller stack; fallback Welcome/Home |
| Bots | YES | YES / YES / YES | Caller stack; fallback Welcome/Home |
| CoachGame | YES | YES / YES / YES | Caller stack; fallback Welcome/Home |
| Placeholder | YES | YES / YES / YES | Caller stack; fallback Welcome/Home |
| Profile | YES | YES / YES / YES | Caller stack; fallback Welcome/Home |
| ProfileHistory | YES | YES / YES / YES | Caller stack; fallback Welcome/Home |

BACK_SCREENS_AUDITED=40 (33 required)
PREEXISTING_102_FILES_MODIFIED=0
STAGED_FILE_COUNT=0
COMMIT=NONE
PUSH=NONE
UI_00_SUCCESS=NO
UI_01_STARTED=NO
NEXT=MANUAL UNITY VALIDATE WELCOME ROUTING
