# AUTH-02A CONTROLLED FIREBASE TEST

Result: STOP before live operations. Current production architecture does not provide an isolated real Email registration path that preserves the default Guest session.

## Source evidence

- `FirebaseSdkClient.InitializeApp` selects `FirebaseApp.DefaultInstance`; `Auth` selects `FirebaseAuth.DefaultInstance`.
- `FirebaseSdkClient.CreateEmailAsync` rejects an existing anonymous session with `GuestUpgradeRequired`, or another session with `SessionConflict`, before account creation.
- `ApplicationServices` constructs that SDK client; `ProductionAuthHost.Bind` uses its shared AuthRouter.
- `ProductionEmailPreview.Mount` wires callbacks to counters only. This is an isolated visual fixture, not a real registration mechanism.
- `TwoUnityValidation` has a named Firebase instance for a different anonymous multiplayer validation flow. It does not implement the production Email registration/verification flow and must not be repurposed by running its matchmaking/account-creation behavior.

A separately isolated real-auth fixture or execution environment would need preparation and validation before this live test. No such fixture was implemented during this request. Do not sign out the current Guest to work around this limitation.

```text
BASE_SHA=823e5c679388ac783bbba5ecb9acacd3dc8e6146
FIREBASE_TEST_CONFIRMED=NOT_RUNTIME_VALIDATED
TEST_IDENTITY=EMAIL_TEST_A (RESERVED_ALIAS_ONLY)
EXISTING_GUEST_SESSION_TOUCHED=NO
REAL_EMAIL_ACCOUNT_CREATED=NO
EMAIL_ACCOUNTS_CREATED_DURING_TEST=0
EMAIL_VERIFIED_INITIAL=NOT_RUN
UNVERIFIED_BOOTSTRAP_BLOCKED=NOT_LIVE_VALIDATED
VERIFICATION_PENDING_VISIBLE=NOT_LIVE_VALIDATED
VERIFICATION_EMAIL_SENT=NO
VERIFICATION_EMAIL_RECEIVED=NOT_RUN
VERIFICATION_LINK_COMPLETED=NOT_RUN
EMAIL_VERIFIED_AFTER_RELOAD=NOT_RUN
FORCED_TOKEN_REFRESH=NOT_RUN
EXISTING_BACKEND_AUTH_REUSED=NOT_RUN
EXISTING_PLAYER_BOOTSTRAP_REUSED=NOT_RUN
PLAYER_BOOTSTRAP_SUCCESS=NOT_RUN
PLAYER_BOOTSTRAP_DUPLICATES=NOT_RUN
IS_ANONYMOUS=NOT_RUN
EMAIL_PROVIDER_PRESENT=NOT_RUN
FINAL_ROUTE=UNCHANGED
SERVER_ENFORCES_EMAIL_VERIFICATION=NO (PREVIOUS_AUDIT)
FIREBASE_USER_VISIBLE=NOT_RUN
BACKEND_PLAYER_AVAILABLE=NOT_RUN
SOURCE_CHANGED_DURING_LIVE_TEST=NO
RAW_UIDS_IN_REPORT=0
ID_TOKENS_IN_REPORT=0
REFRESH_TOKENS_IN_REPORT=0
PASSWORDS_IN_REPORT=0
VERIFICATION_LINKS_IN_REPORT=0
PASSWORDS_IN_LOGS=NOT_SCANNED_NO_LIVE_RUN
TOKENS_IN_LOGS=NOT_SCANNED_NO_LIVE_RUN
PREEXISTING_PROTECTED_FILES_MODIFIED=0/102
SECRET_SCAN=PASS_REPORT_ONLY
TEST_ACCOUNT_RETAINED_FOR_AUTH_02B=NOT_APPLICABLE_NO_ACCOUNT_CREATED
COMMIT=NONE
PUSH=NONE
DEPLOY=NO
AUTH_02A_LIVE_SUCCESS=NO_BLOCKED_BEFORE_MUTATION
AUTH_02B_STARTED=NO
AUTH_03_STARTED=NO
NEXT=REVIEW_ISOLATED_REAL_AUTH_TEST_MECHANISM
```

The 12,352 passing isolated checks remain prior evidence, not live Firebase evidence. No suites were rerun; no real email or password was requested or collected. No SDK, API, registration, verification, or logout action was performed. Only this report was written. This is not a checkpoint approval.
