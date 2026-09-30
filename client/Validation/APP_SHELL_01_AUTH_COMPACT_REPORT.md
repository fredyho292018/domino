# APP-SHELL-01 AUTH COMPACT ROWS

ROW_HEIGHT=56px
ROW_RADIUS=12px
ROW_VERTICAL_PADDING=6px
ROW_HORIZONTAL_PADDING=12px
ROW_GAP=8px
ICON_CONTAINER_SIZE=34x34px
ICON_CONTAINER_SHAPE=CIRCLE
GOOGLE_ICON=MULTICOLOR_G_ON_LIGHT_CIRCLE
FACEBOOK_ICON=WHITE_F_ON_BLUE_CIRCLE
EMAIL_ICON=ENVELOPE_IN_DARK_CIRCLE
PHONE_ICON=GREEN_HANDSET
GUEST_ICON=PERSON_IN_DARK_CIRCLE
PHONE_ICON_COLOR=#71A84B

All five rows retain their exact labels, secondary text and existing callbacks. Fixed columns reserve the same circle, 26px-high divider and 14px chevron space. Primary text remains 15px Bold; secondary text is 11px Medium. The Google SVG backing was removed so the circular UI surface supplies the background. Facebook uses a white f silhouette on a blue circular UI surface. Phone is an original filled rounded handset SVG with dynamic green tint.

LOCAL_COMPILE_ERRORS=0
NAVIGATION_CHECKS=85_PASS
OTHER_REGRESSIONS=17_TOOLBAR_11_CONTACT_31_MEMBERSHIP_18_PROFILE_PASS
PREEXISTING_102_FILES_MODIFIED=0
TEXT_COLUMNS_ALIGNED=PENDING_UNITY_CHECK
CHEVRONS_ALIGNED=PENDING_UNITY_CHECK
375x667=PENDING_UNITY_CHECK
393x852=PENDING_UNITY_CHECK
ALL_8_PRESETS=PENDING_UNITY_CHECK

The Unity validator checks row height, circle dimensions, icon/divider/text/chevron alignment, subtitle bounds, provider callbacks and responsive containment across all eight presets. It leaves Welcome at 375x667. Import/Editor execution and manual optical review remain pending; local compilation is not a substitute for that review.

GOOGLE_BEHAVIOR_CHANGED=NO
FACEBOOK_BEHAVIOR_CHANGED=NO
EMAIL_BEHAVIOR_CHANGED=NO
PHONE_BEHAVIOR_CHANGED=NO
GUEST_BEHAVIOR_CHANGED=NO
REAL_AUTH_CHANGED=NO
BACKEND_CHANGED=NO
COMMIT=NONE
PUSH=NONE
DEPLOY=NO
NEXT=MANUAL WELCOME/AUTH COMPACT REVIEW
