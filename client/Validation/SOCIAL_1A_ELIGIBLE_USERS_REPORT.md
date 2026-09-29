# SOCIAL-1A — Existing Social-eligible development accounts

**PARTIAL: 17 backend-eligible existing accounts found, but a six-account set with currently usable sessions in LOCAL is not established.** No accounts, friendships, settings or protection flags were changed. No Social endpoint was invoked because even a GET can call ensure and lazily write a public identity.

## Environment and scope

The LOCAL credential files refer to Firebase project `teamfho-domino`, the same project configured by the backend's application.yaml and current Unity Firebase settings. Their three UIDs duplicate TEST/slot-01..03; LOCAL is a client environment label, not a separate Firebase user directory. Current Unity ApiSettings selects TEST at https://domino-api-test.teamfho.com. Local Docker has only domino-redis. TCP probes found no listener on 8080, 8085, 9099, 18085, 18141 or 18142. No server was started or configuration switched.

Old S14B validation fixtures used the demo-domino-f0 Firestore emulator at 18085. That emulator is not running; synthetic emulator fixtures are not counted as enabled reusable Firebase accounts.

A bounded, read-only administrative inventory of the configured development project read 220 Player records and their test markers. It found 203 protected players and 17 unmarked candidates. Each candidate was independently checked with Firebase Auth getUser: all 17 exist and are enabled. All 17 have zero linked-provider entries; this does not prove that a reusable guest session remains available. No email, password, JWT or credential was written to the report/evidence.

## Exact eligibility rule

`FirestoreSocialRepository.active(player, marker)` requires:

- Player document exists.
- `players/{uid}.status == ACTIVE`.
- `developmentTestAccounts/{uid}.isTestAccount != true` (absent or false is allowed).

`FirestoreSocialRepository.ensure` reads the owner identity, player and marker in a transaction, and throws HTTP 403 SOCIAL_ACTION_NOT_ALLOWED when that rule fails. `PublicPlayerIdentityService.ensure` also rejects blank/malformed actor UIDs. Account type ANONYMOUS is not an exclusion; neither is the environment name LOCAL/TEST by itself. The server-owned isTestAccount flag is the relevant explicit exclusion. An inactive/missing Player is independently disallowed.

| Operation | Relevant enforcement |
|---|---|
| Name/friend-code search, summary, public profile | SocialController.ready -> identity.ensure; PlayerDiscoveryService and FirestoreSocialRepository.resolve also filter ineligible targets |
| Send friend request | FriendshipController.ready -> ensure; FriendshipService.send/safety/eligible checks both participants transactionally |
| Accept request | FriendshipController.ready -> ensure; FriendshipService.resolve -> safety checks both participants before acceptance |
| Decline/cancel/list/remove friends and requests | Actor passes FriendshipController.ready; target filtering applies to lists; removal/cancellation has its own ownership/generation checks |
| Follow/unfollow/follower lists | FollowController.ready -> ensure; FollowService.set checks both users when adding, and list filters ineligible targets |
| Block/unblock/block list | SocialController.ready -> ensure; adding resolves an eligible target; unblocking can resolve an existing blocked target that later became ineligible |
| Social settings | Read passes ready/ensure; update calls SocialPrivacyService.updateGated -> ensure and repository patchPrivacy repeats active check |
| Presence/match activity | SocialPresenceAuthorization.read requires both ACTIVE, neither marked test, no block either way, plus visibility permissions |

Other uses of the same error code must not be confused with test-account exclusion: recipient friendRequests=NO_ONE, follow=NO_ONE, or a decline cooldown can also yield 403. Target ineligibility in FriendshipService.eligible and FollowService.set uses 404 PLAYER_NOT_FOUND; actor entry denial is 403. Recipient friend capacity can use 409 SOCIAL_ACTION_NOT_ALLOWED. These protections remain unchanged.

## Existing eligible accounts (safe identifiers only)

All have no developmentTestAccounts marker. No account was reclassified.

| Internal UID | Display name | State | Test marker | Backend Social eligible | Current access evidence |
|---|---|---|---|---|---|
| SOCIAL_USER_01 | FHO-A | ACTIVE / enabled | absent | YES | Not established |
| SOCIAL_USER_02 | FHO-M5-A | ACTIVE / enabled | absent | YES | Not established |
| SOCIAL_USER_03 | FHO-A | ACTIVE / enabled | absent | YES | Not established |
| SOCIAL_USER_04 | FHO-B | ACTIVE / enabled | absent | YES | Not established |
| SOCIAL_USER_05 | FHO-A | ACTIVE / enabled | absent | YES | Not established |
| SOCIAL_USER_06 | FHO-A | ACTIVE / enabled | absent | YES | Not established |
| SOCIAL_USER_07 | FHO-M5-B | ACTIVE / enabled | absent | YES | Not established |
| SOCIAL_USER_08 | FHO-M5-C | ACTIVE / enabled | absent | YES | Not established |
| SOCIAL_USER_09 | FHO-B | ACTIVE / enabled | absent | YES | Not established |
| SOCIAL_USER_10 | Guest-SH3PH3VJ | ACTIVE / enabled | absent | YES | Not established |
| SOCIAL_USER_11 | FHO-a | ACTIVE / enabled | absent | YES | Not established |
| SOCIAL_USER_12 | FHO-A | ACTIVE / enabled | absent | YES | Not established |
| SOCIAL_USER_13 | FHO | ACTIVE / enabled | absent | YES | Current Unity session |
| SOCIAL_USER_14 | FHO-M5-D | ACTIVE / enabled | absent | YES | Not established |
| SOCIAL_USER_15 | FHO-A | ACTIVE / enabled | absent | YES | Not established |
| SOCIAL_USER_16 | FHO-A | ACTIVE / enabled | absent | YES | Not established |
| SOCIAL_USER_17 | FHO-B | ACTIVE / enabled | absent | YES | Not established |

Primary candidate: **FHO**, UID `SOCIAL_USER_13`, public ID `7hEIUm1df5Z3CZl0WBxwCA`, friend code `FHO-RP2JG3M0WK4A`. This exactly matches the Social code observed in the existing Unity session during SOCIAL-1. It need not be replaced for eligibility reasons. No sign-out or session export was performed.

The other 16 have no publicIdentity/current record yet, so no friend code is available. This is not an eligibility failure: the supported first Social call provisions it automatically. That call was intentionally not made in this read-only task.

## Read-only relationships

For all 17 eligible candidates, not just a proposed six, the following queried collections are empty: friends, blocks, following, followers. Requests filtered by senderUid and recipientUid contain no PENDING records. No friend data was inserted or deleted. No six-person group is formally selected because authentication access for five additional accounts is unverified. Empty outgoing block lists for all 17 imply no block between any pair of these candidates; external incoming blocks were not exhaustively inventoried. No mutable Social API was used to infer these results.

## Access limitation and decision

The existing reusable credential directory exposes three functional identities and the LOAD population, all intentionally excluded by the observed policy/source classification. The three LOCAL files duplicate TEST identities. Old M5 validation source explicitly reset anonymous identities; historical result files alone cannot establish that those old sessions survive. This task did not create custom tokens, impersonate accounts, refresh guest sessions, sign in, or create new accounts.

There are **17 eligible account records**, but only **one already-observed local Unity session** correlated by public friend code. Five additional reusable sessions are missing/unverified. Strictly LOCAL end-to-end usability is also unverified because the local API is stopped and Unity currently targets TEST. Do not misreport this as only one eligible account existing, or as six ready-to-use users.

Prefer recovering the existing guest sessions for five eligible accounts before creating anything. If sessions cannot be recovered, the supported future creation route is normal Firebase anonymous sign-in in isolated development client sessions, followed by authenticated Player bootstrap, then normal Social initialization. The production player flow creates an ACTIVE player without adding a BOT_SWARM test marker. The swarm provisioner intentionally creates protected accounts and is unsuitable for this Social fixture. Never clear/change an existing marker, weaken ensure, insert friendship records, or reuse a protected LOAD account. This route is a proposal only and requires the next authorized phase; preserve the current FHO session. The LOCAL endpoint must also be explicitly resolved before claiming LOCAL validation.

## Required output

```text
SOCIAL_1A=PARTIAL_ACCESS_AND_LOCAL_ENVIRONMENT_UNVERIFIED
SOCIAL_ELIGIBILITY_RULE=PLAYER_EXISTS_AND_STATUS_ACTIVE_AND_IS_TEST_ACCOUNT_NOT_TRUE
ELIGIBLE_USER_COUNT=17
SOCIAL_ELIGIBLE_USERS_FOUND=NO_SIX_USABLE_LOCAL_ACCOUNTS_CONFIRMED
CURRENT_SESSION_CANDIDATES=1
MISSING_USER_COUNT=5_USABLE_SESSIONS_NOT_FIVE_ABSENT_ACCOUNT_RECORDS
PRIMARY_USER_SELECTED=FHO_CANDIDATE_EXISTING_UNITY_SESSION
FIVE_FRIEND_USERS_SELECTED=NO
EXISTING_RELATIONSHIPS_FOUND=NO_IN_QUERIED_ELIGIBLE_ACCOUNT_LISTS
PENDING_REQUESTS_FOUND=0
CREDENTIALS_EXPOSED=NO
DATA_MUTATED=NO
CODE_CHANGED=NO
SERVER_CHANGED=NO
ACCOUNTS_CREATED=0
SOCIAL_PROTECTION_CHANGED=NO
COMMIT=NONE
PUSH=NONE
DEPLOY=NONE
NEXT=SOCIAL-1B CREATE MISSING SOCIAL-ELIGIBLE LOCAL USERS
```

NEXT is conditional: first recover existing eligible sessions if available; create missing usable development identities only if recovery is unavailable and explicitly authorized. No creation is authorized or executed by this report. CODE_CHANGED=NO means application/product code; only read-only diagnostic helpers and this report were added under validation artifacts. DATA_MUTATED=NO refers to account/application/server data, not local evidence files.

Evidence: Generated/SOCIAL1A/inventory.json. Diagnostic SocialRead.java is limited to Firestore get/query/select and Firebase Auth getUser. It never calls create/update/delete/signIn or any Social endpoint. No external service configuration or server resources were changed.
