# AUTH-LOGOUT-01 — Confirmation visual polish

Menu is unchanged. Only confirmation presentation and its isolated UI validator changed.

Cancel now appears first, using the production primary-safe style. Sign Out appears second on Surface, with Error-token text and border. Local state callbacks preserve destructive coloring after pointer/focus state updates; no shared styles or hardcoded red were added. Guest and registered copy and action callbacks remain unchanged.

```text
MENU_SIGN_OUT_LOCATION=APP_LAST
MENU_SIGN_OUT_LOCATION_REVIEW=APPROVED
CANCEL_POSITION=FIRST
CANCEL_STYLE=PRODUCTION_PRIMARY_SAFE
SIGN_OUT_POSITION=SECOND
SIGN_OUT_STYLE=DESTRUCTIVE_ERROR_TOKEN_TEXT_AND_BORDER
CANCEL_AND_SIGN_OUT_VISUALLY_DISTINCT=YES_IMPLEMENTED
GUEST_WARNING_COPY=PRESERVED
REGISTERED_CONFIRMATION=CANCEL_FIRST_SIGN_OUT_SECOND_NO_GUEST_WARNING
CANCEL_TOUCH_TARGET=48_MIN
SIGN_OUT_TOUCH_TARGET=48_MIN
LOGOUT_CONFIRMATION_RESPONSIVE=PENDING_FRESH_UNITY_VALIDATION
LOGOUT_CONFIRMATION_OVERFLOW=PENDING_FRESH_UNITY_VALIDATION
LOGOUT_CONFIRMATION_REACHABILITY=PENDING_FRESH_UNITY_VALIDATION
LOCAL_COMPILER_ERRORS=0
LOGOUT_FUNCTIONAL_BEHAVIOR_CHANGED=NO
REAL_GUEST_LOGOUT_EXECUTED=NO
AUTH_LOGOUT_01_CHECKPOINT_BLOCKED=YES
COMMIT=NONE
PUSH=NONE
DEPLOY=NO
NEXT=PROFILE_REGRESSION_COMPLETION
```

The current isolated validation request checks both confirmations at eight presets for ordering, semantic styles, touch targets, horizontal bounds and scroll reachability. Earlier Logout results are not evidence for this polish. Manual Unity import is pending; no real authentication or logout operation was invoked. Profile repair was not modified in this task.
