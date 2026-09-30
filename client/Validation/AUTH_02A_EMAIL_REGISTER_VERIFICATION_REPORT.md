# CUBAN DOMINO CLUB — AUTH-02A EMAIL REGISTER + VERIFICATION

Status: PASS — current isolated Unity validation and behavioral regressions complete. Manual visual approval remains pending. No live Firebase operations were executed.

## Implementation

The existing Firebase adapter and ProductionAuthRouter now support Email entry, registration and verification. Provider classification uses anonymous state and password-provider metadata, not the presence of an email string. Both service and SDK adapter reject account creation when an existing anonymous user is present. An existing registered session also cannot be replaced by registration.

Registration validates a trimmed email, password length 6–4096 and confirmation. Passwords remain in memory only, are cleared from fields on submit/detach, and are sent exclusively to Firebase. The pending state never starts Player bootstrap. Verification reloads the same user, checks verification, refreshes its token, and then uses the existing Player pipeline and UID correlation. A failed verification email can be resent without recreating the account.

Use Another Email requires confirmation and ownership of the newly created, still-unverified Email session. Restored sessions and Guest sessions cannot be ended by this action. Sign In and Forgot Password remain placeholders. No linking or account merging was added.

Editor previews use presentation callbacks only; they never resolve ApplicationServices or Firebase. All 11 preview states (Email Entry plus five registration and five verification states) passed for the eight requested viewports. The final target is REGISTER_EMPTY at 393x852.

## Current isolated results

- AUTH-02A behavioral checks: 49 passed, 0 failed, 0 skipped.
- AUTH-01: 14 passed.
- Guest Auth: 43 passed.
- Player foundation: 489 passed.
- Matchmaking: 75 passed.
- History/Replay: 1595 passed.
- Social: 89 passed.
- Local compilation against installed Unity/Firebase assemblies: 0 errors.
- Protected inventory: 102 checked, 0 modified.
- Candidate credential scan: no findings; test identities and emails are synthetic aliases / reserved domains.

An initial isolated test exposed an overbroad final email-verification condition. It was restricted to non-anonymous password-provider sessions and all behavioral regressions passed afterward. No live Guest session was used by these tests.

## Gate

```text
BASE_SHA=823e5c679388ac783bbba5ecb9acacd3dc8e6146
EMAIL_PASSWORD_PROVIDER_STATUS=ENABLED_CONFIRMED_BY_USER
PASSWORD_POLICY_MODE=NOTIFY
PASSWORD_MIN_LENGTH=6
PASSWORD_MAX_LENGTH=4096
REQUIRE_UPPERCASE=NO
REQUIRE_LOWERCASE=NO
REQUIRE_NUMBER=NO
REQUIRE_SPECIAL_CHARACTER=NO
AUTH_ROUTER_STATES=NO_SESSION,RESTORED_GUEST,EMAIL_UNVERIFIED,EMAIL_VERIFIED
EMAIL_ENTRY=IMPLEMENTED
REGISTER_PAGE=IMPLEMENTED
VERIFICATION_PENDING_PAGE=IMPLEMENTED
EMAIL_NORMALIZATION=TRIM
PASSWORD_STORED_LOCALLY=NO
PASSWORD_LOGGED=NO
PASSWORD_SENT_TO_BACKEND=NO
ANONYMOUS_GUARD=PASS_ISOLATED
EMAIL_ACCOUNT_CREATED_FROM_GUEST=NO
REGISTER_DOUBLE_SUBMIT_PREVENTED=YES
BOOTSTRAP_BEFORE_VERIFICATION=NO
EXISTING_PLAYER_BOOTSTRAP_REUSED=YES
PARTIAL_REGISTER_RECOVERY=PASS_ISOLATED
ACCOUNT_RECREATED_AFTER_EMAIL_SEND_FAILURE=NO
UNVERIFIED_RESTORE=PASS_ISOLATED
VERIFIED_RESTORE=PASS_ISOLATED
EMAIL_ALREADY_IN_USE_FLOW=PASS_ISOLATED
ERROR_MODEL=TYPED_SAFE_MESSAGES
EMAIL_ENTRY_RESPONSIVE=PASS_8_OF_8
REGISTER_RESPONSIVE=PASS_8_OF_8
VERIFICATION_RESPONSIVE=PASS_8_OF_8
WELCOME_REGRESSION=PASS_8_OF_8
AUTH_01_GUEST_REGRESSION=PASS
GUEST_IDENTITY_CONTINUITY=PASS_ISOLATED
SHELL_REGRESSION=PASS_8_OF_8
AUTH_REGRESSION=PASS
PLAYER_BOOTSTRAP_REGRESSION=PASS
MATCHMAKING_REGRESSION=PASS
HISTORY_REPLAY_REGRESSION=PASS
SOCIAL_REGRESSION=PASS
AUTH_02A_CHECKS_RUN=3297
AUTH_02A_CHECKS_PASS=3297
AUTH_02A_CHECKS_FAIL=0
AUTH_02A_CHECKS_SKIPPED=0
COMPILER_ERRORS=0
CURRENT_BLOCKING_EXCEPTIONS=0
MOJIBAKE_MARKERS=0
PREEXISTING_PROTECTED_FILES_MODIFIED=0/102
SECRET_SCAN=PASS_CANDIDATES
BACKEND_CHANGED=NO
SCHEMA_CHANGED=NO
FIREBASE_CONFIG_CHANGED=NO
REAL_ACCOUNTS_CREATED=0
REAL_VERIFICATION_EMAILS_SENT=0
COMMIT=NONE
PUSH=NONE
DEPLOY=NO
AUTH_02A_SUCCESS=YES
AUTH_02B_STARTED=NO
AUTH_03_STARTED=NO
NEXT=MANUAL_EMAIL_REGISTER_VERIFICATION_REVIEW
```

The client verification gate does not add server-side email-verification enforcement. Backend behavior is unchanged. Manual visual approval and live TEST registration remain separate gates.

## Current Unity validation — 2026-09-30

The complete Email run began at 13:50:27 UTC after the updated validator assembly was imported. All 88 screen/viewport cases completed. An earlier incomplete run was excluded. No product source changed during this validation; only independent eye-toggle assertions were added to the Editor validator.

| Current suite | Passed |
|---|---:|
| Email Unity | 3248 |
| Email behavioral | 49 |
| Welcome | 680 |
| Shell resize | 41 |
| Home | 720 |
| Menu | 993 |
| Profile | 1019 |
| Puzzles/Learn/Watch | 1712 |
| Isolated functional regressions | 2305 |
| Total | 10767 |

Failures: 0. Skipped: 0. AUTH-02A-specific subtotal: 3297.

Current log interval: 0 compiler errors, 0 compiler warnings, 0 exception headers. The broader Editor session retains two Device Simulator StoreSerializedStates stack occurrences; these are separate from current product exceptions.

The Email preview completed by mounting REGISTER_EMPTY at 393x852 and remains available alongside the regression previews. VERIFICATION_PENDING is available in its selector. This is automated geometry/routing validation, not manual visual approval or a live mobile keyboard test.

```text
ASSETS_REFRESH=PASS
REGISTER_EMPTY=PASS
PASSWORD_VISIBILITY=PASS
CONFIRM_PASSWORD_VISIBILITY=PASS
LOCAL_VALIDATION=PASS_ISOLATED
EMAIL_NORMALIZATION=TRIM
ANONYMOUS_GUARD=PASS
CREATE_EMAIL_FIREBASE_CALLS_FROM_GUEST=0
REGISTER_DOUBLE_SUBMIT_PREVENTED=YES
REGISTER_SUCCESS_STATE=EMAIL_UNVERIFIED
VERIFICATION_PENDING=PASS
IVE_VERIFIED_FALSE=PASS_NO_TOKEN_REFRESH_NO_BOOTSTRAP
IVE_VERIFIED_TRUE=PASS
FORCED_TOKEN_REFRESH=YES
EXISTING_PLAYER_BOOTSTRAP_REUSED=YES
PARTIAL_REGISTER_RECOVERY=PASS
RESEND=PASS
EMAIL_ALREADY_IN_USE_FLOW=PASS
ERROR_MODEL=PASS_SAFE_TYPED_MESSAGES
AUTH_ROUTER_MATRIX=4/4_PASS
UNVERIFIED_RESTORE=VERIFICATION_PENDING
VERIFIED_RESTORE=EXISTING_BOOTSTRAP_TO_APP_SHELL
WELCOME_EMAIL_ROUTE=PASS
AUTH_01_GUEST_REGRESSION=PASS
EMAIL_ENTRY_RESPONSIVE=8/8_PASS
REGISTER_RESPONSIVE=8/8_PASS
VERIFICATION_RESPONSIVE=8/8_PASS
CURRENT_GUEST_SESSION_CHANGED=NO
REAL_FIREBASE_ACCOUNTS_CREATED=0
REAL_VERIFICATION_EMAILS_SENT=0
RAW_UIDS_IN_VALIDATION_OUTPUT=0
TOKENS_IN_VALIDATION_OUTPUT=0
AUTH_02A_SUCCESS=YES_ISOLATED_GATE
```
