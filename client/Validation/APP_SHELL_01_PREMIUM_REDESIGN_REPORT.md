# APP-SHELL-01 Premium selector redesign

Replaces the four-card comparison with four vector tabs (Diamond, Platinum, Gold, Friends & Family), one selected-plan feature panel, Yearly/Monthly segments, effective price, explicit actual charge, savings and synchronized trial summary. Shared across onboarding and Menu; Menu preserves selection and uses Back, onboarding retains Not Now. View All Plans and technical disclaimers were removed from the product-facing component. Development preview controls remain outside the phone UI. All actions remain session-local mock state.

New original SVG icons use 24x24 viewports, rounded 2-unit strokes and runtime tint; existing matching vectors are reused. No reference branding/assets/prices were copied.

LOCAL_COMPILE_ERRORS=0
STATE_MEMBERSHIP_TESTS=31_PASS
NAVIGATION_TESTS=85_PASS
TOOLBAR_TESTS=17_PASS
CONTACT_RESULTS_TESTS=11_PASS
PROTECTED_PREEXISTING_FILES=102_UNCHANGED

MOUNTED_PREMIUM_CHECKS=300_PASS. All eight combinations passed across three simulated phone sizes (375x667, 393x852, 412x915). Evidence: Library/AppShellPremium.result.txt. Menu preservation, context footer, selected indicators, exact feature vectors, prices, annual charge and summaries passed. Membership was left open with Diamond + Yearly. Manual visual review pending.

REAL_BILLING=NO
APP_STORE_PURCHASE=NO
GOOGLE_PLAY_PURCHASE=NO
ENTITLEMENT_CHANGED=NO
COMMIT=NONE
PUSH=NONE
DEPLOY=NO
NEXT=MANUAL MEMBERSHIP REDESIGN REVIEW

