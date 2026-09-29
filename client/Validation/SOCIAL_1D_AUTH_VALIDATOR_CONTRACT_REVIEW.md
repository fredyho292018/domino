# SOCIAL-1D — Auth validator contract review

PASS. The defect was confined to validation tooling. One existing TEST account completed authentication and an authenticated History read. SOCIAL-1C was not resumed.

## Contract and root cause

The successful request was POST https://identitytoolkit.googleapis.com/v1/accounts:signInWithCustomToken, with returnSecureToken=true. The old helper immediately evaluated `a['localId']==uid`. That check attempted to confirm the intended identity but assumed the wrong endpoint response schema. Its absence raised KeyError before the ID-token checks or any Social calls.

The [official custom-token response contract](https://docs.cloud.google.com/identity-platform/docs/reference/rest/v1/accounts/signInWithCustomToken) lists idToken, refreshToken, expiresIn, isNewUser, and deprecated kind. localId is not part of this response contract; it is not a renamed or deprecated custom-token field. The assumption was endpoint-specific confusion, not a Firebase or product authentication defect. The previous HTTP 200 alone did not prove a usable session; this task's controlled retest supplies that missing evidence.

## Minimal validation-only correction

The helper now requires HTTP 200, a structured response, nonempty ID and refresh tokens, valid positive expiry, and no indication of a newly created account. It checks ID-token subject, TEST audience/issuer and expiration. Decoding claims is explicitly not treated as signature verification.

It then calls Firebase accounts:lookup with that ID token and requires exactly one returned account whose localId matches the intended existing UID and is not disabled. Finally it calls the actual TEST backend History endpoint and validates the response shape. Thus neither arbitrary HTTP 200 nor unverified claims establish success.

Backend inspection confirmed FirebaseAdminTokenVerifier uses verifyIdToken(token, true), checks the account exists/enabled, and supplies its UID to ReplayHistoryController. GET /api/v1/players/me/history?limit=1 uses the authenticated UID for History reads and does not invoke Social identity initialization. No production authentication code changed.

The single-account mode restricts the administrative preflight and minting to FHO-M5-A, UID SOCIAL_USER_02. It verifies existing enabled Auth identity, ACTIVE Player and absence of the protected test marker before signing. It does not generate access for the other three accounts.

## Evidence

- One custom token generated and exchanged; HTTP 200.
- Firebase authenticated lookup matched the expected existing UID.
- TEST History returned HTTP 200 with the expected page structure.
- No Social endpoint, request, acceptance, profile initialization or Unity operation executed.
- 12 unit tests passed, covering success without localId, authentication failure, arbitrary 200, malformed response/token, missing required material, identity/environment mismatch, expiry, newly-created-user rejection, verified lookup failures and invalid History responses.
- Credentials stayed in process memory/private subprocess pipes, were not printed or persisted, and were discarded at process exit. This is disposal, not server-side revocation or guaranteed zeroization. Existing session stores were not changed. Normal authentication can update Firebase-managed sign-in metadata; no administrative identity update was issued.

Validation-only files: Generated/SOCIAL1C/auth_contract.py, validate.py, AccessPreflight.java, test_auth_contract.py and single_retest.py. Sanitized runtime evidence: Generated/SOCIAL1C/single-retest-result.json. The prior failed result remains preserved. The full friendship executor was not run in this task.

```text
AUTH_VALIDATOR_REVIEW=PASS
FAILED_EXPECTED_FIELD=localId
FIELD_REQUIRED_BY_REAL_CONTRACT=NO
ROOT_CAUSE=INCORRECT_CUSTOM_TOKEN_RESPONSE_SCHEMA_ASSUMPTION
VALIDATOR_CHANGE_REQUIRED=YES_APPLIED
PRODUCTION_AUTH_CHANGE_REQUIRED=NO
SINGLE_ACCOUNT_RETEST=PASS_FHO-M5-A
AUTH_EXCHANGE_HTTP=200
AUTH_SESSION_USABLE=YES
EXPECTED_IDENTITY_MATCH=YES
SAFE_AUTHENTICATED_ENDPOINT_VALIDATED=GET_TEST_PLAYERS_ME_HISTORY_HTTP_200
SOCIAL_DATA_MUTATED=NO
FRIEND_REQUESTS_CREATED=0
FRIEND_REQUESTS_ACCEPTED=0
TEMP_AUTH_MATERIAL_DISCARDED=YES
CREDENTIALS_EXPOSED=NO
CODE_CHANGED=YES_VALIDATION_ONLY
CHANGE_SCOPE=AUTH_CONTRACT_VALIDATOR_AND_SINGLE_ACCOUNT_TEST_HARNESS
TESTS_PASS=YES_12_UNIT_TESTS_AND_ONE_LIVE_TEST_ACCOUNT
DEPLOY_PERFORMED=NO
COMMIT_PERFORMED=NO
PUSH_PERFORMED=NO
READY_TO_RESUME_SOCIAL_1C=YES
NEXT=SOCIAL-1C RESUME FIVE-FRIEND VALIDATION
```

Ready means the authentication blocker is resolved. Four active temporary sessions are not retained; obtain them only when SOCIAL-1C is resumed. No automatic continuation occurred.
