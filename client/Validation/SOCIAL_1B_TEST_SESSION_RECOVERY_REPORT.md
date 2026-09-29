# SOCIAL-1B — Recover existing TEST sessions

**PARTIAL.** FHO and one additional existing eligible user authenticated successfully against the real TEST API. Four additional reusable friend sessions remain missing. No friendship execution started.

## Scope and mechanism

Only Domino validation tooling, named app evidence, the dedicated TeamFHO/DominoSwarm TEST and LOAD stores, and the exact Firebase Auth namespace derived from Domino's configured package/project were inspected. LOCAL identity files were not loaded or used in this task. No personal-file search, browser credential store, unrelated application vault, password reset, provider link, custom-token minting or account creation occurred.

The I2.1 report describes named Firebase sessions Domino-I21-A and Domino-I21-B. The current matching Domino stores yielded only two eligible default-app sessions, not those named sessions. M5's historical harness resets anonymous identities; old result files/UIDs alone are not reusable authentication. No old executable was launched because its startup might sign in anonymously or execute gameplay/bootstrap.

The Firebase desktop SDK derives the secure-store namespace from package name + project ID, not mobilesdk_app_id. Initial empty probes using app ID were corrected before drawing conclusions. The SDK uses chunked Windows credentials and an encoded FlatBuffer. Diagnostic code read only the matching Domino namespace, reconstructed data in process memory, and emitted only UID, display name and boolean/status evidence. The cache was never modified or copied to disk. The two matches were refreshed through Firebase's normal secure-token endpoint in memory; verified account IDs and token audience/issuer matched teamfho-domino. Returned credentials were never printed, persisted, or passed on a command line. This is normal reuse of existing sessions, not new credential provisioning.

Official implementation references: [namespace derivation](https://github.com/firebase/firebase-cpp-sdk/blob/main/app/src/app_identifier.cc), [Windows persistence](https://github.com/firebase/firebase-cpp-sdk/blob/main/app/src/secure/user_secure_windows_internal.cc), [Auth persistence/schema](https://github.com/firebase/firebase-cpp-sdk/blob/main/auth/src/desktop/user_data.fbs).

## Candidates

Eligibility is from the authoritative SOCIAL-1A read: existing ACTIVE Player, enabled Firebase account, and no developmentTestAccounts.isTestAccount=true marker. The rule is at developmentTestAccounts/{uid}, not a Player.isTestAccount field. TEST project is teamfho-domino; only accounts with validated stored sessions are usable now. Records marked NO are not proven permanently unrecoverable elsewhere; no session was found in the inspected Domino infrastructure.

| UID | Display name | SOCIAL_ELIGIBLE | TEST_ENVIRONMENT | REUSABLE_SESSION_AVAILABLE |
|---|---|---|---|---|
| SOCIAL_USER_01 | FHO-A | YES | YES (project) | NO — not recovered |
| SOCIAL_USER_02 | FHO-M5-A | YES | YES (project) | NO — not recovered |
| SOCIAL_USER_03 | FHO-A | YES | YES (project) | NO — not recovered |
| SOCIAL_USER_04 | FHO-B | YES | YES (project) | NO — not recovered |
| SOCIAL_USER_05 | FHO-A | YES | YES (project) | NO — not recovered |
| SOCIAL_USER_06 | FHO-A | YES | YES (project) | NO — not recovered |
| SOCIAL_USER_07 | FHO-M5-B | YES | YES (project) | NO — not recovered |
| SOCIAL_USER_08 | FHO-M5-C | YES | YES (project) | NO — not recovered |
| SOCIAL_USER_09 | FHO-B | YES | YES (project) | NO — not recovered |
| SOCIAL_USER_10 | Guest-SH3PH3VJ | YES | YES (project) | YES |
| SOCIAL_USER_11 | FHO-a | YES | YES (project) | NO — not recovered |
| SOCIAL_USER_12 | FHO-A | YES | YES (project) | NO — not recovered |
| SOCIAL_USER_13 | FHO | YES | YES (project) | YES |
| SOCIAL_USER_14 | FHO-M5-D | YES | YES (project) | NO — not recovered |
| SOCIAL_USER_15 | FHO-A | YES | YES (project) | NO — not recovered |
| SOCIAL_USER_16 | FHO-A | YES | YES (project) | NO — not recovered |
| SOCIAL_USER_17 | FHO-B | YES | YES (project) | NO — not recovered |

```text
PRIMARY_USER=FHO
PRIMARY_USER_UID=SOCIAL_USER_13
FRIEND_01=Guest-SH3PH3VJ
FRIEND_01_UID=SOCIAL_USER_10
FRIEND_02=NOT_RECOVERED
FRIEND_03=NOT_RECOVERED
FRIEND_04=NOT_RECOVERED
FRIEND_05=NOT_RECOVERED
```

## Non-destructive authentication validation

Both recovered sessions successfully authenticated to GET /api/v1/players/me/history?limit=1 on https://domino-api-test.teamfho.com (HTTP 200). No bootstrap, matchmaking, player update or Social mutation endpoint was used.

FHO additionally returned HTTP 200 for player/social-summary and player/friends?limit=1. Its public ID matched the existing authoritative profile. No SOCIAL_ACTION_NOT_ALLOWED occurred. FHO already had a public identity, so ensure did not create one.

Guest-SH3PH3VJ has no publicIdentity/current record. All valid normal Social read endpoints call ensure and would create its public Social identity. To honor SOCIAL_DATA_MUTATED=NO, those endpoints were not invoked for this user. Its Social eligibility is confirmed by the prior authoritative metadata read, but its live Social entry remains NOT_RUN. Do not call this full five-user Social validation. Normal API authentication may update operational rate counters; no durable Social profile, friendship, request, block, follow or privacy settings were changed.

The dedicated TEST store contains 3 credentials and LOAD contains 200; none maps to any of the 17 eligible UIDs. Protected accounts were not authenticated again or reclassified. Unity remains on TEST and its current FHO session remains untouched.

## Safest remaining access path — proposal only

First prefer any surviving original Domino session on its original test device/app profile. The known desktop namespaces did not yield four more. All 17 candidates were previously verified with no linked provider; a UID alone is not a login credential and password reset is not an appropriate recovery route.

A separate explicitly authorized administrative TEST access operation could use Firebase Admin's supported custom-token mechanism for four exact existing eligible UIDs. It would verify account existence/enabled/eligibility immediately before exchange, fail closed on mismatch, add no elevated claims, use only the existing approved signing identity (if it already has capability), keep the signed token in memory and exchange it through Firebase Auth. This obtains NEW sessions for existing accounts, not recovery of an old cached session; token exchange can issue long-lived refresh credentials and affect sign-in metadata. Therefore it was NOT executed under this task's no-authentication-data-change boundary. No new IAM roles, keys, auth endpoint or deployment should be introduced merely to enable it. Reference: [Firebase custom tokens](https://firebase.google.com/docs/auth/admin/create-custom-tokens).

If that existing-account access mechanism is unavailable or not authorized, four new controlled TEST accounts would be needed, subject to separate authorization. No accounts need to be created now. Never remove protected test markers or weaken Social eligibility. Later permission is also needed to let the normal Social flow initialize missing public profiles before relationship tests.

## Final output

```text
SOCIAL_1B=PARTIAL
ENVIRONMENT=TEST
PRIMARY_USER=FHO
ELIGIBLE_USERS_AVAILABLE=17
REUSABLE_FRIEND_SESSIONS=1
MISSING_SESSIONS=4
AUTH_VALIDATED=YES_FHO_AND_FRIEND_01_TEST_HTTP_200
SOCIAL_ELIGIBILITY_VALIDATED=YES_METADATA;LIVE_SOCIAL_FHO_ONLY
CREDENTIALS_EXPOSED=NO
SOCIAL_DATA_MUTATED=NO
ACCOUNTS_CREATED=0
CODE_CHANGED=NO
DEPLOY_PERFORMED=NO
READY_FOR_SOCIAL_1=NO
SERVER_CONFIGURATION_CHANGED=NO
COMMIT=NONE
PUSH=NONE
NEXT=AUTHORIZE_SCOPED_TEST_ACCESS_FOR_FOUR_EXISTING_ELIGIBLE_ACCOUNTS
```

CODE_CHANGED=NO refers to product/application code. Only isolated diagnostic helpers and sanitized evidence/report artifacts were added. Evidence: session-discovery.json, auth-validation.json and store-coverage.json in Generated/SOCIAL1B. No credentials are included in those files.
