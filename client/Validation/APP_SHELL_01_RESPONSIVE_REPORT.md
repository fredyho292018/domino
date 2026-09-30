# APP-SHELL-01 RESPONSIVE PREVIEW PRESETS

PREVIEW_PRESET_COUNT=8
SMALL_PHONE=375x667
TALL_PHONE=393x852
ANDROID=412x915
LARGE_PHONE=430x932
ANDROID_LARGE=480x1040
SMALL_TABLET=600x960
TABLET=768x1024
LARGE_TABLET=834x1194
PORTRAIT_ONLY=YES

Editor-only preview change. SetPreviewDevice updates logical frame dimensions without rebuilding the view or session. Scaling uses available host width/height and a visual transform around the top-left origin; its centered slot reserves the scaled size. Preset dimensions and scale remain visible. A generic 620px maximum centers the mock shell within the preview only; no production or runtime screen layout was changed. Safe insets are simulated preset values, not physical-device claims.

ROUTE_PRESERVED_ON_RESIZE=PASS_MOUNTED
MOCK_STATE_PRESERVED_ON_RESIZE=PASS_MOUNTED
SCALE_TO_FIT=PASS_MOUNTED
TABLET_CONTENT_MAX_WIDTH=620_PREVIEW_ONLY
LOCAL_COMPILER_ERRORS=0
NAVIGATION_TESTS=85_PASS
TOOLBAR_STATE_TESTS=17_PASS
CONTACT_RESULTS_TESTS=11_PASS
MEMBERSHIP_STATE_TESTS=31_PASS
PROFILE_STATE_TESTS=18_PASS
PREEXISTING_PROTECTED_FILES_MODIFIED=0

MOUNTED_RESPONSIVE_CHECKS=1040_PASS across 48 screen/preset combinations. Profile, Membership, Experience, Coach selection, Contacts results and onboarding Trial passed structural checks at all eight sizes. Evidence: Library/AppShellResponsive.result.txt. Logical dimensions, route and state retention, scale, maximum width, header/nav bounds, simulated safe bottom, top-level horizontal overflow and score separation passed. CTA remains in scroll content; optical review, gesture reachability and physical safe areas remain manual. Preview left on Profile at Tablet 768x1024. No screen was altered to force a passing result.

COMMIT=NONE
PUSH=NONE
DEPLOY=NO
NEXT=MANUAL RESPONSIVE REVIEW

