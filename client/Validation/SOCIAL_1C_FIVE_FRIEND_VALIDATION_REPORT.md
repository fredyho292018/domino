# SOCIAL-1C — Scoped TEST access and five-friend validation

Result: **PARTIAL — stopped during authentication response processing, before Social operations.**

## Verified scope and candidates

The read-only administrative preflight verified that the four selected existing Firebase users are enabled, their Player status is ACTIVE, and their developmentTestAccounts marker does not set isTestAccount=true. The existing administrative credentials support signing. No account creation, role change, password reset, email change, custom claims or eligibility change was performed.

| Role | Display name | Safe existing UID |
|---|---|---|
| PRIMARY_USER | FHO | SOCIAL_USER_13 |
| FRIEND_01 | Guest-SH3PH3VJ | SOCIAL_USER_10 |
| FRIEND_02 | FHO-M5-A | SOCIAL_USER_02 |
| FRIEND_03 | FHO-M5-B | SOCIAL_USER_07 |
| FRIEND_04 | FHO-M5-C | SOCIAL_USER_08 |
| FRIEND_05 | FHO-M5-D | SOCIAL_USER_14 |

## Execution boundary

The existing FHO and Guest sessions refreshed successfully and their returned identity and project claims matched the intended TEST users. Four custom tokens were generated for exactly the four selected UIDs through the Firebase Admin SDK, with no additional claims. They were transferred only through a captured private subprocess pipe and held in process memory, never written to files or terminal output.

The first custom-token exchange returned HTTP 200. The validation helper then accessed the expected `localId` response field and raised KeyError before completing identity verification. This is a response-schema assumption in the isolated validation helper, not evidence of an API authorization rejection or a Social defect. The helper should validate the returned ID-token subject and verified backend identity instead of assuming this response field exists. That correction and another authentication attempt were not executed after the stop.

The remaining three custom tokens were not exchanged. No Social summary, search, request, acceptance or friendship endpoint was reached. The authenticated-user counter in result.json remains zero because it is assigned only after all six accounts finish authentication; it must not be interpreted as six rejected logins. Two existing sessions passed identity checks, one new exchange returned HTTP 200 without completed identity validation, and three new exchanges were not attempted. Backend authentication for all six was not established in this run.

All process-held authentication material was discarded when the helper and subprocess exited. This means local disposal, not server-side revocation or guaranteed memory zeroization. Existing Windows Firebase caches were unchanged. Normal Firebase sign-in may update provider-managed sign-in metadata; no administrative account-update operation was issued.

Unity was not operated. The previously observed empty friendship state was not remeasured, so no current friend count or UI success is asserted. No relationships were added or removed by this run.

## Final output

```text
SOCIAL_1=PARTIAL
ENVIRONMENT=TEST
PRIMARY_USER=FHO
FRIEND_01=Guest-SH3PH3VJ
FRIEND_02=FHO-M5-A
FRIEND_03=FHO-M5-B
FRIEND_04=FHO-M5-C
FRIEND_05=FHO-M5-D
AUTHENTICATED_USERS=NOT_FULLY_VALIDATED
EXISTING_SESSION_IDENTITY_CHECKS_PASSED=2
CUSTOM_TOKENS_GENERATED=4
CUSTOM_TOKEN_EXCHANGES_HTTP_200=1
FRIEND_REQUESTS_CREATED=0
FRIEND_REQUESTS_ACCEPTED=0
FHO_FRIEND_COUNT=NOT_REVALIDATED
BIDIRECTIONAL_RELATIONSHIPS_VALID=NOT_RUN
PENDING_REQUESTS_REMAINING=NOT_REVALIDATED
DUPLICATES_FOUND=NOT_EVALUATED
SELF_FRIEND_PROTECTION=NOT_RUN
DUPLICATE_REQUEST_PROTECTION=NOT_RUN
REFRESH_DUPLICATION_PROTECTION=NOT_RUN
UNITY_FRIEND_LIST_COUNT=NOT_REVALIDATED
UNITY_FRIEND_LIST_VALID=NOT_RUN
TEMPORARY_AUTH_MATERIAL_DISCARDED=YES_PROCESS_EXIT
CREDENTIALS_EXPOSED=NO
ACCOUNTS_CREATED=0
SOCIAL_DATA_MUTATED=NO
ELIGIBILITY_RULE_CHANGED=NO
CODE_CHANGED=NO_PRODUCT_CODE
DEPLOY_PERFORMED=NO
SERVER_CONFIGURATION_CHANGED=NO
COMMIT=NONE
PUSH=NONE
NEXT=SOCIAL-1C AUTH_RESPONSE_VALIDATOR_REVIEW
```

Only isolated validation helpers and this sanitized report were added. No production or client source changed. SOCIAL-2 is not ready; the five-friend validation remains incomplete.
