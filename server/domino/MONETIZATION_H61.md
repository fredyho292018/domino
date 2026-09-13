# H6.1 — persisted monetization policy

H6.1 supersedes H6's restart-required policy source. Runtime authority is now:

`systemConfig/monetization` → `FirestoreMonetizationPolicyRepository` →
`MonetizationPolicyService` → immutable `ResolvedMonetizationPolicy` → existing
authenticated API → existing Unity service/cache → H5 UI.

## Sources and storage

1. A strictly valid document with status ACTIVE takes precedence.
2. Missing, inactive or malformed document uses ENV/application.yaml fallback.
3. On storage failure, keep the currently cached valid snapshot; if none exists,
   use fallback. This includes retaining a cached disabled policy during outage.

Source is exposed internally as FIRESTORE or FALLBACK_CONFIG, alongside loadedAt
and the immutable policy. Repository results distinguish NotFound, Invalid (safe
reason), Unavailable and Active. Controllers do not query Firestore directly.

Required document fields:

```json
{
  "version": 1,
  "adsEnabled": true,
  "rewardedEnabled": true,
  "rewardCoins": 10,
  "maxPerRound": 1,
  "cooldownSeconds": 120,
  "maxPerHour": 5,
  "maxPerDay": 20,
  "maxCoinsPerDay": 200,
  "status": "ACTIVE"
}
```

Numbers must be Firestore integers, not strings or floating-point values.
All H6 bounds still apply, including maxPerRound exactly 1 and daily coin cap at
least rewardCoins. The seed also writes server timestamps createdAt/updatedAt and
updatedBy=bootstrap. Metadata is optional for an initial manual document, validated
when present. Supported updatedBy values: bootstrap, manual-admin, migration.
No token, UID supplied by Unity, SDK ad ID or secret belongs in this document.

## Cache and changes without redeploy

`DOMINO_MONETIZATION_POLICY_CACHE_SECONDS` defaults to 60 (valid range 1–3600).
The cache is per backend instance. Reads are synchronized, coalescing concurrent
requests into one refresh. Failures are also retried only after the TTL, avoiding
a read on every request during outage. `invalidate()` is an internal method for
tests/future administration; there is no public invalidation/admin endpoint.

Update business fields and increment version atomically in Firestore through an
authorized server/admin identity. On the next read after TTL, the same running
Spring service returns the new policy; no deployment/restart is needed. Each
economic operation captures one resolved snapshot before its Firestore transaction
and uses it throughout retries. Policy propagation is bounded by cache TTL during
healthy storage; an in-flight operation may finish under its captured revision.

Unity retains its existing 600-second cache, refresh-before-show and foreground
logic. A backend refresh does not push changes into an idle Unity screen. New
opportunities/intents are gated by the backend cache even if the UI is stale.

Changing business values without a higher version, or decreasing the last accepted
Firestore version, is rejected with MONETIZATION_POLICY_INVALID and falls back.
The last accepted revision comparison is in memory per instance. After a restart
there is no persisted revision history to detect a same-version edit that happened
while the instance was down; operators must increment versions. This phase adds
no audit-history writes or admin API.

## Existing rewards and limits

Intent creation snapshots policyVersion and rewardAmountSnapshot from the resolved
policy. Existing issued intents retain their snapshots; consuming an old 10-coin
intent still credits 10 after the active policy changes to 15.

Consume revalidates current resolved cooldown, rolling-hour, UTC-day, daily coin
and opportunity limits against the ledger inside the existing H4/H6 atomic
transaction. Wallet, lifetime earnings, ledger, opportunity and CONSUMED remain
atomic; replays do not credit or consume quota again. A newly lowered quota can
temporarily block an existing VERIFIED intent. The H6 exception remains: an already
VERIFIED earned intent may settle with kill switches disabled, but other quotas
still apply. No local Unity credit or new verification bypass was added.

OFFLINE_ROUND_VERIFICATION=NOT_SERVER_AUTHORITATIVE. Offline completion still cannot
be independently proven. The backend protects single-use opportunities and UID
quotas; online Match Engine attestation is future work. H6 legacy-intent migration
restrictions remain unchanged.

## Fallback and explicit bootstrap

All existing variables remain supported as fallback/seed inputs:

DOMINO_MONETIZATION_VERSION, DOMINO_MONETIZATION_ADS_ENABLED,
DOMINO_MONETIZATION_REWARDED_ENABLED, DOMINO_MONETIZATION_REWARD_COINS,
DOMINO_MONETIZATION_MAX_PER_ROUND, DOMINO_MONETIZATION_COOLDOWN_SECONDS,
DOMINO_MONETIZATION_MAX_PER_HOUR, DOMINO_MONETIZATION_MAX_PER_DAY,
DOMINO_MONETIZATION_MAX_COINS_PER_DAY.

Defaults remain 1 / true / true / 10 / 1 / 120 / 5 / 20 / 200.
Unity technical AdsSettings remains disabled and DEVELOPMENT.

Nothing automatically seeds on normal application startup. From server/domino,
an authorized administrator can explicitly run:

```powershell
# Review FIREBASE_PROJECT_ID and fallback ENV first; uses ADC.
.\gradlew seedMonetizationPolicy
```

This standalone, non-web command loads YAML/ENV fallback and calls Firestore
`create`, whose exists=false precondition protects existing documents, including
concurrent seeds. Existing document → ALREADY_EXISTS_UNCHANGED. It never calls
set/update on an existing document. It is not part of bootRun/startup and does not
start REST, WebSocket, Redis, rewards, or wallets. No production seed was performed.

## Client security

Unity does not access systemConfig directly; the API remains Firebase-authenticated.
Only the server/admin SDK reads/writes policy in this implementation. No Firestore
security rules are tracked in this repository, so deployed client permissions were
not changed or certified here. Deployment owners must deny mobile/client writes
to systemConfig; Admin SDK authorization is controlled by server IAM/credentials.

## Validation and real configuration-only test

Unit/HTTP tests cover repository mapping, fallback, cache expiry, outage, same-version
rejection, dynamic endpoint values, kill switches, current-limit consumption, old
amount/new amount and create-only seed behavior. Existing H4/H6 transactional tests
remain in the full suite. They are doubles, not Firestore contention benchmarks.

The opt-in RealFirestorePolicyTests requires DOMINO_H61_REAL_POLICY_TEST=true and
explicit FIREBASE_PROJECT_ID. It uses a unique isolated path under systemConfig,
asserts absence, seeds 10, proves seed cannot overwrite, changes to version 2/15,
invalidates the cache, then restores version 3/10 in finally. It exercises the actual
Firestore repository/service and config-controller DTO in-process, not a deployed
HTTP server. Separate MockMvc tests exercise authenticated HTTP routing.

Validation document left at agreed defaults (version 3, 10 coins):
`systemConfig/monetization-h61-validation-7de21e66-dd3b-4080-8f8d-2b403b4ba639`.
The active `systemConfig/monetization` document was not modified. No wallet, real ad,
Firebase user, SSV, Cloud Run, Redis or WebSocket change was made by this test.

Future GameMode/RuleSet configuration can reuse the architectural pattern:
persistent config → validated server service/cache → API → Unity. No GameMode,
RuleSet, H7, admin UI or admin endpoint is implemented here.
