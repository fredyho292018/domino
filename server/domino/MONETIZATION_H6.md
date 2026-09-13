# H6 monetization policy

Historical H6 baseline: [H6.1](MONETIZATION_H61.md) now adds Firestore-backed policy
refresh without restart. The limits and H4/H6 economic invariants below remain.

The backend owns reward amounts and limits. `MonetizationPolicy` binds
`domino.monetization` from application.yaml/environment. Change the properties and
restart the backend; Unity does not need rebuilding. Increment the policy version
when changing economics. Defaults: 10 coins, 1 opportunity per round, 120 seconds,
5 credits per rolling hour, 20 per UTC calendar day, 200 rewarded coins per day.
The current offline implementation requires maxPerRound=1 (other values fail
configuration validation); it does not implement multiple grants per opportunity.

Environment variables:

- DOMINO_MONETIZATION_VERSION
- DOMINO_MONETIZATION_ADS_ENABLED
- DOMINO_MONETIZATION_REWARDED_ENABLED
- DOMINO_MONETIZATION_REWARD_COINS
- DOMINO_MONETIZATION_MAX_PER_ROUND
- DOMINO_MONETIZATION_COOLDOWN_SECONDS
- DOMINO_MONETIZATION_MAX_PER_HOUR
- DOMINO_MONETIZATION_MAX_PER_DAY
- DOMINO_MONETIZATION_MAX_COINS_PER_DAY

Check application.yaml for the exact variable names; the YAML is authoritative.

## Authenticated contract

- GET /api/v1/monetization/config: version, adsEnabled, rewarded.enabled,
  rewarded.rewardCoins, rewarded.limits.
- GET /api/v1/economy/ad-rewards/eligibility?opportunityId=…: eligible,
  rewardCoins, reason, nextEligibleAt, remaining (hour/day/coinsToday), serverTime.
- POST /api/v1/economy/ad-rewards/opportunities with `{}`: opaque server UUID
  and expiresAt. Unknown body fields are rejected. No client UID or amount.
- Existing intents and consume routes remain authenticated with Firebase.

Eligibility is advisory. Creation and consumption recheck policy inside database
transactions. The UID always comes from the security principal. Controllers add
bounded per-instance rate protection: 60 requests per UID per minute, 10,000 UID
windows maximum, expired windows evicted. This is supplemental transport protection;
it does not replace persistent limits and is not distributed anti-DDoS protection.

## Atomic authority and indexes

The immutable REWARDED_AD CREDIT ledger is the economic source of truth.
Each transition reads the relevant ledger window and `players/{uid}/rewardUsage/current`.
Each successful consume writes that shared usage document in the SAME Firestore
transaction as Wallet, lifetimeCoinsEarned, one deterministic ledger entry,
the opportunity OPEN→USED, and intent VERIFIED→CONSUMED. Concurrent devices
therefore conflict/retry instead of both consuming the last remaining quota slot.
Replaying CONSUMED returns the persisted receipt without spending quota again.

Ledger query: `players/{uid}/walletTransactions`, `createdAt >= cutoff`.
Firestore's automatic ascending single-field createdAt index must remain enabled.
No composite index is required. Cutoff covers both UTC day start and the larger
of rolling hour/cooldown. Legacy H4 ledger entries are included immediately.
Read budget: 4097 rows; more than 4096 fails closed. This intentionally bounded
query is performed on reward actions, not a periodic client poll. A future
high-volume economy should introduce a reconciled aggregate and migration.

The injected Clock supplies server time. The rolling hour excludes exactly
now−3600s; daily counting includes UTC midnight. No partial reward is credited.
Future ledger timestamps are counted conservatively. Redis is not used.

## Opportunity and migration limits

There is one active server-issued, expiring opportunity per UID; retries reuse it.
The client cannot invent a round ID to bypass usage of the SAME opportunity.
However an offline round cannot be independently proven by this backend: an
authenticated caller can request a new opportunity after cooldown without actually
playing a round. Hourly/daily/coin limits still apply. This is not server-authoritative
round attestation; online round identity belongs to a future gameplay phase.

Previously CONSUMED H4 intents remain replayable and their ledger credits count.
Unconsumed pre-H6 intents without version/amount/opportunity snapshots fail closed;
they require an explicit audited migration before settlement. No fabricated VERIFIED
records or production bypass were added.

New H6 intents freeze policyVersion and rewardAmountSnapshot. A 10-coin intent
remains 10 after a policy change to 15. Current quotas still apply at consume.
As requested by the final kill-switch exception, both switches block new intents
and shows, but an already VERIFIED pending reward may settle after disable.
No new ad is required for this recovery.

## Unity

MonetizationPolicyService uses authenticated transport and a 600-second in-memory
cache. Missing, invalid or expired configuration fails closed for ads. It refreshes
before show, on expired foreground resume, and eligibility at round presentation.
Countdown uses serverTime plus monotonic elapsed time; local UI ticks do not send
HTTP. A deadline is refreshed once, never a permanent per-second polling loop.
Policy cannot supply SDK IDs or choose the local build environment.

The old serialized reward preview has been removed. Active intent previews use
their frozen amount; new offers display the backend policy/eligibility amount.
Confirmed balances still come exclusively from H4 consume responses. EN/ES reasons
are in the existing Unity Localization tables. Continue remains available when
ads are unavailable. Technical AdsSettings.Enabled remains false, DEVELOPMENT,
with Google's test ad ID; no ad request was made for this validation.

## Validation scope

Tests use transactional Firestore doubles (including concurrent consumes and restart
over shared stored documents), not real Firestore or an emulator. No real SSV,
production inventory, cloud deployment or live wallet credit was performed.
Anonymous-account rotation remains a known limitation: quotas are per authenticated
UID. App Check/Play Integrity and stronger account controls are future work.

Preloading while cooldown is active is allowed when both backend switches and
the local technical switch permit it; showing still requires fresh eligibility.

OFFLINE_ROUND_VERIFICATION=NOT_SERVER_AUTHORITATIVE
For offline matches, the client requests a server-issued opportunity after reporting
round completion; the backend cannot independently prove that the round occurred.
Future online matches will use the server-authoritative Match Engine. For offline
play, a signed/local match-event strategy may be designed later if needed. Neither
strategy is implemented by this H6 checkpoint.
