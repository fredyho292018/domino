# SOCIAL-1A HTTP 503 diagnosis

Read-only diagnosis. **INCONCLUSIVE; resume is not allowed.** No failed request was repeated, no credentials refreshed or issued, and no Social operation or Unity validation was executed.

## Evidence and limits

The prior sanitized result records six authenticated accounts, phase BASELINE, API_STATUS_503 and zero requests created/accepted. Its file modification time is 2026-09-28 00:50:04 UTC, a completion-time proxy, not an exact HTTP timestamp. The client discarded the response headers, error body and request path. No correlation ID was preserved.

Execution order establishes a successful primary-account social summary before failure. The remaining possible calls are primary-account friends, incoming requests, outgoing requests or blocks; or the first friend's History/social-summary calls. All are GET. The first friend's profile was not stored, so later accounts were not reached. It would be incorrect to identify one exact endpoint from this result alone or to assert that the failure was a database query.

Bounded SSH inspection read API container logs for 00:49:00–00:51:00 UTC (maximum 1,000 lines) and cloudflared journal for the same interval (maximum 200 lines). API output contained zero lines; the tunnel query contained no matching incident evidence. Only sanitized categories were returned. No raw payload, Firebase UID or credential is included here.

Current passive Docker state: running, healthy, OOMKilled=false, RestartCount=0, StartedAt=2026-09-27T21:46:12.210688119Z. This argues against a restart of the current API container during the incident. It does not prove dependency health or rule out a transient network/health failure at the original timestamp. Historical health transitions and host-wide OOM evidence were not established.

## Source-path review (candidate paths, not runtime proof)

- Friends/requests: FriendshipController.ready → SocialRateGate.check → identity.ensure → FriendshipService.friends/requests → FirestoreFriendships.page → Firestore query, followed by profile materialization when rows exist.
- Blocks/summary: SocialController.ready → rate gate → identity.ensure → Social services/FirestoreSocialRepository. Summary also reads friendship capacity and follow counts.
- History: ReplayHistoryController → EntitlementHistory/History repositories; no Social identity initialization.

The Social paths involve Redis rate limiting before Firestore. BoundedSocialRedis has a 200 ms caller budget; the resilient gate may use local fallback. Firestore repository reads have bounded waits, and identity initialization runs through a Firestore transaction. Missing indexes, timeout, SDK failures and malformed data are possible failure classes, but none is demonstrated for this incident. Existing prior eligibility/state evidence does not prove current data integrity; no speculative query was replayed under another identity or via an administrative bypass.

SocialErrors preserves recognized SocialFailure statuses and selected invalid-input errors, but maps other exceptions to 503 SOCIAL_SERVICE_UNAVAILABLE without logging the original exception. Therefore 503 can represent either genuine dependency unavailability or an unrelated internal exception. The source establishes an observability/classification limitation, not which branch handled this response. The discarded response body prevents even confirming that this mapper produced the original 503. Cloudflare origin/forwarding remains unknown.

Authentication had passed for all six before the baseline. That does not prove the failed request passed its own security filter. No request-specific authorization-stage evidence exists.

## Correction assessment

No root-cause correction is justified yet. A separately reviewed diagnostic plan should preserve sanitized request method/path, UTC start/end, HTTP status, allowlisted error category and correlation identifiers, and ensure server exception categories are safely correlated. This is a proposal only: no logging/source/configuration changes or reproduction were performed. Do not deploy indexes, change rules, edit data or restart services based on the current evidence.

```text
FAILED_HTTP_METHOD=GET
FAILED_ENDPOINT=UNKNOWN_WITHIN_BASELINE_CANDIDATES
FAILED_REQUEST_PURPOSE=PRE_FRIENDSHIP_BASELINE
FAILED_REQUEST_TIMESTAMP=UNKNOWN_COMPLETION_PROXY_2026-09-28T00:50:04Z
CORRELATION_ID=NOT_CAPTURED
HTTP_STATUS=503
HTTP_503_ORIGIN=UNKNOWN
REQUEST_REACHED_API=UNKNOWN_FOR_FAILED_REQUEST
CONTROLLER_REACHED=UNKNOWN
SERVICE_REACHED=UNKNOWN
REPOSITORY_REACHED=UNKNOWN
SOCIAL_QUERY_CALL_PATH=CANDIDATE_PATHS_REVIEWED_NOT_RUNTIME_CONFIRMED
EXCEPTION_TYPE=UNKNOWN
SAFE_ERROR_CATEGORY=HTTP_503_ONLY
FAILURE_COMPONENT=UNKNOWN
FIRESTORE_INVOLVED=YES_IN_CANDIDATE_SOURCE_PATHS_RUNTIME_STAGE_UNKNOWN
FIRESTORE_ERROR_CATEGORY=UNKNOWN
REDIS_INVOLVED=YES_IN_SOCIAL_RATE_GATE_RUNTIME_STAGE_UNKNOWN
AUTHORIZATION_STAGE_RESULT=SIX_IDENTITIES_PREVALIDATED_FAILED_REQUEST_STAGE_UNKNOWN
CLOUDFLARE_503_EVIDENCE=NO_CORRELATED_EVIDENCE
API_RESTARTED=NO_EVIDENCE_CURRENT_CONTAINER_RESTART_COUNT_0
OOM_EVENT=CURRENT_CONTAINER_FALSE_HISTORICAL_HOST_UNKNOWN
GENERAL_API_FAILURE_EVIDENCE=NOT_ESTABLISHED
FAILURE_SCOPE=UNKNOWN
HTTP_503_MAPPING_CORRECT=UNDETERMINED_GENERIC_MAPPING_IS_OVERBROAD
ROOT_CAUSE=INCONCLUSIVE
CONFIDENCE=INSUFFICIENT_FOR_CAUSAL_ATTRIBUTION
SOURCE_CHANGE_REQUIRED=UNDETERMINED
CONFIG_CHANGE_REQUIRED=UNDETERMINED
DATA_CHANGE_REQUIRED=UNDETERMINED
INFRA_CHANGE_REQUIRED=UNDETERMINED
FAILED_REQUEST_RETRIED=NO
FRIEND_REQUESTS_CREATED=0
FRIEND_REQUESTS_ACCEPTED=0
FRIENDSHIPS_CREATED=0
UNITY_VALIDATION=NOT_RUN
SERVER_CONFIGURATION_CHANGED=NO
SOURCE_CHANGED=NO
CREDENTIALS_EXPOSED=NO
COMMIT=NONE
PUSH=NONE
DEPLOY=NONE
SOCIAL_1A_SUCCESS=NO_EXACT_CAUSE_NOT_ESTABLISHED
SOCIAL_1_RESUME_ALLOWED=NO
NEXT=SOCIAL-1A REVIEW
```
