# SOCIAL-1 — Five-friend functional validation

Result: **PARTIAL — identity eligibility blocker**. No friendships or requests were created. No deploy, product changes, server configuration changes, commit or push.

## Phase 1: existing flow (reviewed before API probes)

All `/api/**` endpoints require a verified Firebase Bearer identity. Actor UID comes from the authenticated principal, never a caller-supplied actor parameter. Public player IDs are separate from authentication UIDs.

| Operation | Existing endpoint |
|---|---|
| Own profile/capacity | GET /api/v1/player/social-summary |
| Name lookup | GET /api/v1/players/search?mode=NAME&q=... |
| Friend-code lookup | GET /api/v1/players/search?mode=FRIEND_CODE&friendCode=... |
| Public profile/relationship | GET /api/v1/players/{publicPlayerId}/profile |
| Send request | POST /api/v1/players/{publicPlayerId}/friend-request |
| Pending requests | GET /api/v1/player/friend-requests?direction=INCOMING or OUTGOING |
| Accept/decline | POST /api/v1/friend-requests/{requestId}/accept or decline |
| Cancel own request | DELETE /api/v1/friend-requests/{requestId} |
| List friends | GET /api/v1/player/friends |
| Remove friend | DELETE /api/v1/player/friends/{publicPlayerId} |

Name search is normalized prefix search (3–16 letters/numbers/underscore/hyphen), gated by discoverableByName. Default name discoverability is false unless the player's default says otherwise. Exact friend-code lookup does not require name discoverability; both respect eligibility and blocks. Name search orders normalizedDisplayName and document ID ascending. Friends and requests order sortTime then document ID descending, using actor-bound encrypted cursors.

Firestore owns friendRequests, socialPairs, friendships, players/{uid}/friends and socialCounters. The pair key hashes a canonical ordered UID pair. Acceptance transaction creates one friendship and two per-user projections, updates counts, marks the request ACCEPTED and clears the pending pair reference. Accepted request records remain as resolved records, not pending requests. Subscription friend limits and both users' eligibility are transactionally checked.

Source behavior: a duplicate pending request returns the existing relationship; an already-friend request returns 409 ALREADY_FRIENDS; self-request returns 400 SELF_RELATION_NOT_ALLOWED. These protections have NOT been validated live in this task. Durable send limits are five per minute and twenty per day. Redis supplies resilient rate limiting and presence/invalidation transport; it is not the authoritative friendship store.

Unity SocialView uses SocialClient/SocialApi and the existing Firebase token provider. Selecting Amigos refreshes summary and friends. A refresh clears local items/cursor before loading backend items; the client discards results after identity changes. No direct Unity Firestore friendship read or locally fabricated list is used. The current UI format is a localized capacity label (observed `0 / 5 amigos`), rather than a literal `AMIGOS (0)` title.

Sources: server/domino/src/main/kotlin/com/teamfho/domino/social/{SocialController,FriendshipController,Friendships,FirestoreFriendships,FirestoreSocialRepository,SocialServices,SocialModel}.kt; security/SecurityConfiguration.kt; client/DominoGame/Assets/_Domino/Scripts/Social/{SocialClient,SocialView}.cs.

## Phase 2: baseline evidence and blocker

Six existing credential files were tested against the actual TEST API at https://domino-api-test.teamfho.com. Firebase project guard: teamfho-domino. All six refreshed authentication successfully and matched the expected identity internally. No credential or raw authentication UID is included in evidence.

- TEST/slot-01, TEST/slot-02, TEST/slot-03
- LOAD/group-01/slot-01, LOAD/group-01/slot-02, LOAD/group-01/slot-03

Every GET player/social-summary returned HTTP 403 with SOCIAL_ACTION_NOT_ALLOWED. The probe stopped querying each account after the denial. Thus their friendship/request counts are UNKNOWN, not zero. LOCAL/slot-01..03 contain the same identities as TEST/slot-01..03; they are not additional candidates. Remaining LOAD accounts were not individually probed, so this report does not claim an exhaustive live census of every identity.

The implementation explicitly requires an ACTIVE player and developmentTestAccounts.isTestAccount != true. The provisioner creates BOT_SWARM accounts with isTestAccount=true. The observed denials are consistent with this intentional eligibility restriction; an authoritative per-account marker audit was not performed, so the exact stored condition for each denial is not asserted. Changing/removing test markers would change account eligibility and was not performed as a workaround.

Existing Unity session is already in Social in Play mode. The Amigos button was clicked through the normal UI, and the backend-driven view displayed `0 / 5 amigos` and `Aún no tienes amigos`. Candidate PRIMARY_USER is that existing session (public friend code FHO-RP2JG3M0WK4A, as displayed); its authentication identity was not exported or matched to a credential file. No account switch/logout was attempted, preserving the session while suitable additional identities are unresolved.

Social GET handlers may lazily provision public identity metadata through ensure; this behavior was disclosed before the probes. No manual database writes, social-settings changes, deletions, friend requests or acceptances were executed.

## Phases 3–5

Not executable with the currently located eligible population. Five suitable additional existing TEST accounts are needed. A clarification is pending for their location/access, without passwords or tokens. No new identities created. No unrelated social data removed. No protection bypass or deployment proposed as a supposed defect fix.

## Required output

```text
SOCIAL_API_AVAILABLE=YES_AUTHENTICATED_POLICY_RESPONSE_AND_UNITY_VIEW
PRIMARY_USER=EXISTING_UNITY_SESSION_CANDIDATE_NOT_AUTH_CORRELATED
TEST_IDENTITIES_REUSED=6_AUTH_PROBED_0_ELIGIBLE_CONFIRMED
FRIEND_REQUESTS_CREATED=0
FRIEND_REQUESTS_ACCEPTED=0
PRIMARY_USER_FRIEND_COUNT=0_OBSERVED_IN_CURRENT_UNITY_SESSION
BIDIRECTIONAL_RELATIONSHIPS_VALID=NOT_RUN
DUPLICATES_FOUND=NOT_ASSESSED
PENDING_REQUESTS_REMAINING=UNKNOWN
SELF_FRIEND_PROTECTION=SOURCE_PRESENT_NOT_RUNTIME_VALIDATED
DUPLICATE_REQUEST_PROTECTION=SOURCE_PRESENT_NOT_RUNTIME_VALIDATED
UNITY_FRIEND_LIST_VALID=BASELINE_ONLY_FIVE_FRIENDS_NOT_VALIDATED
LOGOUT_LOGIN_PERSISTENCE=NOT_RUN
DEFECTS_FOUND=0_CONFIRMED_PRODUCT_DEFECTS
VALIDATION_BLOCKER=EXISTING_PROBED_IDENTITIES_REJECTED_BY_SOCIAL_ELIGIBILITY
NEW_IDENTITIES_CREATED=0
SOCIAL_DATA_DELETED=0
PRODUCT_SOURCE_CHANGED=NO
SERVER_CONFIGURATION_CHANGED=NO
DEPLOY=NONE
COMMIT=NONE
PUSH=NONE
SOCIAL_1=PARTIAL
```

Evidence: Generated/SOCIAL1/baseline.json (sanitized status/code per alias), baseline.py (bounded API probe). A PASS requires five created/accepted relationships, pairwise pending/duplicate checks, authenticated re-login persistence and the actual Unity five-friend list; none is inferred from source inspection.
