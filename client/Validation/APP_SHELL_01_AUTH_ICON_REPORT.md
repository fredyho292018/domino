# APP-SHELL-01 Auth provider icon update

Google and Facebook now use recognizable provider graphics sourced from Firebase UI. Google retains its multicolor paths with a white backing; Facebook retains the provider silhouette with a blue backing and white f. Phone is an original rounded smartphone SVG with dynamic #71A84B tint.

Sources:
- https://www.gstatic.com/firebasejs/ui/2.0.0/images/auth/google.svg
- https://www.gstatic.com/firebasejs/ui/2.0.0/images/auth/facebook.svg

Only provider asset selection/tint and the three graphics changed in the mock UI. The existing fixed 48px column, 24px image area, row dimensions, text, callbacks and alignment are preserved. Email and Guest assets are unchanged. No dependencies added.

LOCAL_COMPILE=PASS
NAVIGATION_CHECKS=85_PASS
OTHER_REGRESSIONS=17_TOOLBAR_11_CONTACT_31_MEMBERSHIP_18_PROFILE_PASS
PROTECTED_102_FILES_MODIFIED=0
CURRENT_UNITY_RESPONSIVE_RETEST=PENDING_REFRESH_OUTSIDE_PLAY
PREVIOUS_WELCOME_CHECKS=503_PASS_NOT_CURRENT_ICON_VALIDATION
MANUAL_VISUAL_REVIEW=PENDING

The queued Unity validator covers all eight presets, including 375x667, 393x852 and 412x915, then leaves Welcome at 393x852. Its previous result predates the latest compiled runtime and is not claimed as validation of the new icons.

GOOGLE_MOCK_BEHAVIOR_CHANGED=NO
FACEBOOK_MOCK_BEHAVIOR_CHANGED=NO
PHONE_MOCK_BEHAVIOR_CHANGED=NO
REAL_AUTH_CHANGED=NO
BACKEND_CHANGED=NO
COMMIT=NONE
PUSH=NONE
DEPLOY=NO
NEXT=MANUAL WELCOME/AUTH ICON REVIEW
