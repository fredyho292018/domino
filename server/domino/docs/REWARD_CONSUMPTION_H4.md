# H4 — ledger and atomic rewarded consumption

H3 was committed separately and fast-forwarded into local main before this phase:
cb486c494cdff4ea44958d59ca2f92db50417ce2.
H4 originated on codex/h4-economy-ledger and was integrated into main for its checkpoint.
The pre-existing local ApiSettings change is preserved in the named Git stash
`local: preserve API enabled setting before H4 checkpoint`, outside the H4 commit.
See client/Validation/REWARD_CONSUMPTION_H4_CHECKPOINT.txt for integration validation.
No deployment or real Firestore credit is part of this checkpoint.

## Authority and policy

Spring Boot alone determines credit. The configuration property
domino.economy.rewarded-ad.reward-coins defaults to 10 and must be positive and within
the existing Wallet.MAX_COINS limit. REWARDED_AD_STANDARD is the only supported policy.
The controller does not choose an amount. Create/status previews use the backend policy;
Google reward_amount and Unity completion never directly credit Wallet.

A verified intent survives its original issuance TTL for consumption; TTL applies to ISSUED
SSV eligibility, not an already accepted grant. ISSUED, EXPIRED and REJECTED cannot consume.

## REST

POST /api/v1/economy/ad-rewards/intents/{intentId}/consume
requires Firebase Bearer and JSON {}. Unknown fields (uid, amount, coins, ledger ID, etc.)
are rejected. Owner comes exclusively from the principal. Foreign/missing intents return 404.
Success returns reward {type, amount}, wallet {coins}, intent {intentId, status: CONSUMED}.
It exposes neither ledger paths nor Google callback data.

GET /api/v1/economy/ad-rewards/pending requires Firebase Bearer and returns
{intent: <normal intent response>} or {intent: null}.
Only the latest verified intent referenced by the existing owner pointer is returned.
To preserve it, issue rejects REWARD_PENDING (409) until that VERIFIED intent is consumed.
No Firestore index deployment or polling job is needed.

Legacy limitation: if an older H3 deployment already overwrote its pointer while leaving
older VERIFIED intents behind, pending discovers only the latest pointer. An older known ID
is still consumable via the authenticated endpoint. A future migration/list endpoint may
be needed for such pre-H4 records; no speculative migration or destructive cleanup runs here.

## Atomic transaction and ledger

For each consume, one Firestore transaction reads:
1. RewardIntent (ownership, VERIFIED/CONSUMED, source, policy, verifiedAt, Google transaction).
2. Existing valid Wallet.
3. players/{uid}/walletTransactions/reward:{intentId}.

On first consumption it:
- creates an immutable CREDIT/REWARDED_AD ledger record;
- sets coins and lifetimeCoinsEarned to checked sums;
- preserves lifetimeCoinsSpent and createdAt;
- sets Wallet.updatedAt using Firestore serverTimestamp;
- marks the intent CONSUMED with consumedAmount, ledgerTransactionId and consumedAt.

Ledger fields: transactionId, uid, type, source, amount, balanceBefore, balanceAfter,
rewardIntentId, rewardPolicyKey, version=1 and server-createdAt.
No update/delete ledger route exists. The repository uses create, never set/update, for ledger.

The amount is frozen on the consumed intent and checked against the ledger on replay.
Thus later policy changes do not rewrite prior awards. Duplicate responses contain the original
reward amount and the CURRENT wallet balance (which may include subsequent legitimate changes).
A CONSUMED intent with a missing/conflicting ledger, or a VERIFIED intent with pre-existing ledger,
returns 409 without repair or overwrite.

All writes commit together; five transaction attempts and a 30-second caller deadline.
A timeout may hide a successful commit, so clients can safely retry the deterministic ledger ID.
Repeated signed Google callbacks also acknowledge CONSUMED intents without any credit operation.

## Numeric and Wallet invariants

Math.addExact prevents signed-long overflow for coins and lifetimeCoinsEarned.
The existing safe-integer Wallet cap (9,007,199,254,740,991) is stricter than Long.MAX_VALUE
and remains enforced. No cap or existing Player/Wallet invariant was relaxed.
Missing/invalid Wallet returns WALLET_STATE_INVALID (409); consume does not create or repair it.
Existing Player Foundation bootstrap remains responsible for initialization.

## Unity

RewardVerificationService adds CONSUMING/CONSUMED and explicit ConsumeRewardAsync.
It requires a backend receipt with VERIFIED (or CONSUMED for idempotent refresh), never merely
CLIENT_EARNED. One logical consume is in flight. Create/refresh are blocked during consumption.

RewardIntentApiClient posts only {} and the opaque intent path, with the existing Firebase token
and one 401 refresh retry. Response parsing rejects wrong status/types, invalid balances and
mismatched intent IDs. Pending recovery and consume are explicit, not periodic.

PlayerRewardWalletReceiver delegates to PlayerService's shared operation gate, so bootstrap,
alias updates and consumption cannot race to overwrite snapshots locally. It captures and rechecks
identity, cancellation and response bounds. A successful backend balance REPLACES WalletSnapshot;
no code adds the reward amount locally. SnapshotChanged informs existing presentation.
Failure preserves the previous Wallet reference. The local receipt remains retryable; the server
may already have committed after a timeout, and the retry simply returns its current balance.

Editor debug commands:
- Domino > Ads > Refresh Reward Intent Status
- Domino > Ads > Recover Pending Verified Reward
- Domino > Ads > Consume Verified Reward

On restart, bootstrap reads the real Wallet; the pending endpoint allows retrying the latest
VERIFIED intent. A reward already consumed before a lost response is recovered by normal bootstrap.
No final reward UI/toast/CTA or automatic consume was introduced.

## Validation scope

Tests use the actual repository with the staged Firestore SDK transaction double, signed H3
fixtures and MockMvc authentication. The harness now supports null transaction results (empty pending)
and an injected error AFTER commit to test unknown outcomes. This is not a remote Firestore or
emulator contention test. No real user Wallet was credited by validation.

Client tests exercise the actual PlayerService receiver, absolute snapshot replacement, repeated
calls, single flight, transport/timeout failures, identity changes, pending recovery and request JSON.
Unity compilation, mock lifecycle, Player Foundation, Portrait and gameplay regressions run separately.

No Redis economy authority, WebSocket changes, production SSV bypass, AdMob production traffic,
Cloud Run deploy, wallet client arithmetic or H5/H6 UI/limits.
Real SSV and real Firestore consumption are NOT_RUN. See REWARD_CONSUMPTION_H4_REPORT.txt.
