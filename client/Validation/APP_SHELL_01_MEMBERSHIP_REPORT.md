# APP-SHELL-01 Membership plan selector

Mock-only selector shared by onboarding Trial, View All Plans, and Membership. Defaults: DIAMOND + ANNUAL. Back preserves both selections. Cards are clickable and feature rows are vertical. Annual comparisons use decimal arithmetic; percentage rounding is away from zero (25%, 33%, 33%, 38%). Monthly mode omits annual savings. Annual mode states the actual annual charge and monthly equivalent. The trial action records only session-local MOCK_<PLAN>_TRIAL; no billing service or entitlement is called.

Validation:
- MEMBERSHIP_CHECKS=31_PASS
- NAVIGATION_CHECKS=85_PASS
- TOOLBAR_CHECKS=17_PASS
- CONTACT_RESULTS_CHECKS=11_PASS
- LOCAL_COMPILE_ERRORS=0
- PREEXISTING_102_FILES_MODIFIED=0

Unity executed OpenMembership and confirmed MEMBERSHIP_PREVIEW_OPENED=YES, PLAN=DIAMOND, PERIOD=ANNUAL in Library/AppShellMembership.result.txt. Manual visual review remains pending; no visual approval claimed.

REAL_BILLING=NO
APP_STORE_PURCHASE=NO
GOOGLE_PLAY_PURCHASE=NO
ENTITLEMENT_CHANGED=NO
COMMIT=NONE
PUSH=NONE
DEPLOY=NO
NEXT=MANUAL MEMBERSHIP UX REVIEW

