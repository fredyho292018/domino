# H3: authenticated reward intents and AdMob SSV

H3 ends at VERIFIED. No credit, ledger, Wallet update or Firestore client write is implemented.
The SDK's client reward amount is never used as a Domino coin amount. The fixed backend
REWARDED_AD_STANDARD policy exposes a preview of 10 coins for future H4 only.

## HTTP contract

- POST /api/v1/economy/ad-rewards/intents: Firebase Bearer required, JSON object {} only.
  Extra fields (including uid, coins and transactionId) are rejected with 400.
- GET /api/v1/economy/ad-rewards/intents/{intentId}: Firebase Bearer required; foreign
  and nonexistent IDs both return 404.
- GET /api/v1/admob/rewarded/ssv: only this exact GET route is public. It requires a
  valid AdMob signature; there is no runtime debug verification endpoint.

Intent responses contain intentId, status, expiresAt, and reward {type: COINS, previewAmount: 10}.
They do not expose ownership, transaction IDs or signatures.

## Persistence and races

rewardIntents/{opaqueUuid} stores uid internally, source, status, policy, environment,
createdAt, expiresAt, updatedAt and (after verification) verifiedAt/adMobTransactionId.
Global opaque IDs avoid exposing Firebase UID as Google user_id or needing a collection-group lookup.
rewardIntentOwners/{sha256Uid} is a per-player active-intent pointer.
adMobRewardTransactions/{sha256TransactionId} is the persistent idempotency index.

Issue is a Firestore transaction: read pointer + previous intent; reuse unexpired ISSUED
intent or expire/reject the previous one and create a UUIDv4 intent + replace the pointer.
At most one valid ISSUED intent per UID. Firestore retries reuse the same candidate UUID.

Verify is a Firestore transaction: read transaction index + intent; validate; atomically
create the transaction record and mark the intent VERIFIED. A matching duplicate returns 200
even after the original time window. Cross-intent transaction reuse, a second transaction
for an already VERIFIED intent, or inconsistent stored tuple are rejected with 409.
No Wallet documents are read or written.

Five Firestore retry attempts, 30-second caller deadline. Unknown commit outcomes can be
retried: the same transaction/index/pointer prevents repeated processing.
createdAt/expiresAt use the backend clock; verifiedAt/updatedAt use Firestore serverTimestamp.
EXPIRED may be returned lazily from status without a write. Replacing an expired ISSUED intent
persists EXPIRED. REJECTED is used when replacing an active intent after an environment change.
Future cleanup can remove old expired documents; verified intents and transaction idempotency
records must be retained or archived consistently with H4, not blindly deleted by TTL.

## Signature and key policy

Production verifier uses Java 21 JCA/SunEC SHA256withECDSA, P-256 keys and DER signatures.
It verifies the original raw query bytes preceding &signature=; signature and key_id must
be the last two parameters. It rejects duplicate/unknown names, malformed framing and oversized
queries (8 KiB), and only decodes business values AFTER verifying. custom_data is percent-decoded
once and must be an opaque UUIDv4. transaction_id must be hex (up to 128 chars).
Optional user_id is ignored and is never configured by Unity.

Key fetch URL is fixed to https://www.gstatic.com/admob/reward/verifier-keys.json with default
TLS verification, no redirects, connect/request deadlines and a total 8-second deadline.
Keys are cached for 12 hours (configuration restricted to <=24h).
Unknown keys trigger refresh subject to a shared 60-second cooldown. Concurrent fetches coalesce;
expired keys cannot verify during an outage. Missing key = 400; dependency failure = 503.
No network fetch occurs at application startup or in deterministic unit tests.

Reference: https://developers.google.com/admob/android/ssv
AdMob SSV ad_unit is the numeric unit component; this differs from the full SDK ad unit ID.

## Configuration

Spring external configuration prefix: domino.economy.rewarded-ad

Defaults:
- intent-ttl: 10m
- environment: DEVELOPMENT
- development-ad-unit: 5224354917
- production-ad-unit: 8685423732
- clock-skew: 2m
- max-event-age: 10m
- key-cache-ttl: 12h

The backend alone selects the environment and policy. New verification must match the
intent environment and the environment's exact unit. PRODUCTION requires explicit configuration;
H3 Unity continues to reject production ad requests.
An event must not be over 2 minutes in the future, over 10 minutes old, before intent creation
minus skew, or after intent expiry. Receipt must arrive before expiry (strict H3 policy).
A delayed, already-committed identical callback remains idempotently acknowledged.

## Responses and logging

Valid/identical duplicate: 200. Invalid signature/metadata/timing/unit: 400.
Missing/foreign intent: 404. Used intent/conflicting transaction: 409.
Temporary keys/Firestore unavailable: 503 (fail closed, retryable).
Safe category-only logs; no raw query, token, signature, full UID or intent ID logging.
No generic /api/** security weakening. Existing REST/realtime remain independent.

## Unity

Existing H2 preload remains independent of backend reachability.
Show requires one authenticated create-intent request (one forced token refresh on 401).
Concurrent Show while waiting is blocked. A failed intent request never opens the ad.
The adapter calls RewardedAd.SetServerSideVerificationOptions with CustomData=intentId before
Show; UserId is not set. Local completion sets CLIENT_EARNED, never VERIFIED.
VERIFIED can only come from the authenticated status API; it is independent of fullscreen close
and cannot be downgraded by a late client completion.

Editor commands:
1. Enable existing AdsSettings only in DEVELOPMENT before Play (source remains disabled).
2. Domino > Ads > Authorize and Load Editor Mock.
3. Domino > Ads > Show Rewarded Test Ad.
   Normal Editor requires the local authenticated backend for intent creation.
4. Domino > Ads > Refresh Reward Intent Status (manual, no polling).

The isolated batch validator injects a test-only intent API and fake Firebase client; it tests
actual SDK SSV attachment + CLIENT_EARNED + H2 reload. It does not simulate Google verification
or write real Firestore documents. This is distinct from the normal Editor flow.

## Validation and limitations

Backend tests use locally generated ECDSA signed fixtures, fake keys/verifier at explicit test
boundaries, MockMvc with Firebase test principals, and mocked Firestore SDK transactions.
The Firestore harness stages all writes and serializes races; it verifies adapter transactions
and retry behavior but is NOT a Firestore emulator or remote contention test.

Google cannot reach localhost/127.0.0.1. The Editor mock generates no real SSV.
No production ad requests, public tunnels, router changes, AdMob console change, or deploy.
Real Android SSV and real Google callback delivery are NOT_RUN.

Future TEST setup needs a publicly reachable HTTPS backend and its exact
/api/v1/admob/rewarded/ssv URL configured in the appropriate AdMob rewarded unit.
Use the official SSV tester there in a later approved phase; never configure a localhost URL.
Production unit configuration and live traffic remain out of scope.

Run server ./gradlew test build, client Validation/RunRewardIntentClientTests.ps1,
RunRewardedAdsTests.ps1 and the existing regression scripts.
See client/Validation/REWARD_INTENTS_H3_REPORT.txt for evidence and limitations.
H4 may consume VERIFIED intents and atomically credit Wallet + ledger. H3 does neither.
