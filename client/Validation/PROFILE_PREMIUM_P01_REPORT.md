# DOMINO PROFILE & PREMIUM — P0.1 REPORT

Validated 2026-09-19. Anonymous accounts are eligible, once per Firebase UID, as explicitly approved. This is not a once-per-person guarantee: a different anonymous UID is a different account.

```text
BRANCH=main
BASE_COMMIT=dff9fac6580ad716e402c246be7f94c53bf487fe
SOURCE_SHA_BEFORE=dff9fac6580ad716e402c246be7f94c53bf487fe
SOURCE_SHA_AFTER=dff9fac6580ad716e402c246be7f94c53bf487fe

PROFILE_ENTITLEMENTS_SEPARATED=YES
PLANS=FREE, PREMIUM
ENTITLEMENT_SERVICE=IMPLEMENTED
FEATURE_MODEL=IMPLEMENTED
LIMIT_MODEL=IMPLEMENTED
PROMOTIONAL_TRIAL_IMPLEMENTED=YES
TRIAL_DAYS=7
TRIAL_PAYMENT_REQUIRED=NO
TRIAL_AUTO_RENEW=NO
ANONYMOUS_ACCOUNTS_ELIGIBLE=YES
TRIAL_UNIQUENESS=PER_UID
NEW_USER_TRIAL=PASS
EXISTING_USER_LAZY_TRIAL=PASS
TRIAL_ONCE=PASS
CONCURRENT_TRIAL_IDEMPOTENCY=PASS
TEST_ACCOUNT_AUTO_TRIAL=NO
SERVER_TIME_AUTHORITY=YES
TRIAL_EXPIRATION=PASS

SUBSCRIPTION_POLICY_PATH=systemConfig/subscriptionPolicy
POLICY_CACHE_TTL_SECONDS=60
POLICY_DYNAMIC_REFRESH=PASS
POLICY_FALLBACK=PASS
PROMOTIONAL_TRIAL_KILL_SWITCH=PASS
POLICY_VERSION=1 (default)
FREE_PUBLIC_DUEL=PASS
FREE_PUBLIC_PARTNERS=PASS
FREE_FOLLOW=PASS (capability only)
FREE_FRIENDS_MAX=5
PREMIUM_FRIENDS_MAX=100
FREE_HISTORY_MAX=10
FREE_REPLAY_MAX=3
PREMIUM_FULL_HISTORY=PASS
PREMIUM_FULL_REPLAY=PASS
PARTY_CREATE_PREMIUM=PASS
PARTY_INVITE_PREMIUM=PASS
PRIVATE_DUEL_PREMIUM=PASS
PRIVATE_PARTNERS_PREMIUM=PASS
CHOOSE_2V2_PARTNER_PREMIUM=PASS
PARTY_MATCHMAKING_PREMIUM=PASS
ADVANCED_STATS_PREMIUM=PASS
PREMIUM_THEMES=PASS (capability only)
AD_FREE=NOT_DECIDED

FREE_HISTORY_LIMIT_ENFORCED=PASS
FREE_REPLAY_LIMIT_ENFORCED=PASS
PREMIUM_HISTORY=PASS
PREMIUM_REPLAY=PASS
I4_SECURITY_PRESERVED=PASS
REPLAY_ENGINE_CHANGED=NO
DOWNGRADE_DELETES_DATA=NO
ACTIVE_MATCH_INTERRUPTED=NO
ACTIVE_REPLAY_INTERRUPTED=NO
EXPIRATION_POLLING=0

CLIENT_CAN_GRANT_PREMIUM=NO
CLIENT_CAN_CHANGE_VALID_UNTIL=NO
DIRECT_CLIENT_ENTITLEMENT_WRITE=DENIED_IN_EMULATOR
PRODUCTION_FIRESTORE_RULES=NOT_INSPECTED_OR_DEPLOYED
SERVER_SIDE_FEATURE_AUTHORIZATION=YES

BOOTSTRAP_READS_BEFORE=2
BOOTSTRAP_READS_AFTER=9 first grant; 2 warm repeat
ENTITLEMENT_READS_PER_BOOTSTRAP=7 first grant; 0 warm repeat
ENTITLEMENT_READS_PER_PLAY_TILE=0
POLICY_CACHE_TTL=60 seconds
REAL_FIRESTORE_CALLS=0

UNITY_FREE_STATE=PASS
UNITY_TRIAL_STATE=PASS
UNITY_PREMIUM_STATE=PASS
UNITY_EXPIRED_STATE=PASS
UNITY_HISTORY_LIMIT=PASS
UNITY_REPLAY_LIMIT=PASS
UNITY_PREMIUM_LOCK=PASS
UNITY_FREE_PUBLIC_GAMEPLAY_VISIBLE=PASS
PORTRAIT=PASS (1080x1920 and 1536x2048)
SPANISH=PASS
ENGLISH=PASS
UNITY_COMPILATION=PASS
UNITY_PLAY_MODE=PASS
CONSOLE_ERRORS=0
UNITY_VISUAL_CHECKS=67 PASS

GOOGLE_PLAY_BILLING_IMPLEMENTED=NO
APPLE_STOREKIT_IMPLEMENTED=NO
PAYMENT_GATEWAY_IMPLEMENTED=NO
REAL_MONEY=NO
SOCIAL_IMPLEMENTED=NO
BACKEND_TESTS=497 PASS / 1 SKIPPED (498 total; local Redis enabled)
BOT_SWARM_UNIT_TESTS=25 PASS
BOT_SWARM_LOAD_STARTED=NO
FIRESTORE_EMULATOR_TESTS=11 PASS
PLAYER_FOUNDATION=PASS
MONETIZATION=PASS
WALLET=PASS
M4=PASS
I1=PASS
I2=PASS
I3=PASS
M5=PASS
I3_1=PASS (unit/integration only)
I4=PASS
F0=PASS
F0_1=PASS

ADS_SETTINGS_PRESERVED=YES
API_SETTINGS_PRESERVED=YES
LOCALIZATION_SETTINGS_PRESERVED=YES
COMMIT=NONE
PUSH=NONE
DEPLOY=NONE
```

## Implementation and authority

Backend sources live in `server/domino/src/main/kotlin/com/teamfho/domino/entitlement/`:

- `EntitlementModels.kt`: typed plans, sources, grants, features, explicit limited/unlimited values, policy defaults and pure resolver. Effective coverage merges contiguous grants, not gaps. Expired/revoked grants cannot authorize.
- `EntitlementService.kt`: policy cache (60 seconds), bounded UID projection cache (30 seconds, 2048 accounts), bootstrap, feature/capacity authorization and internal admin grant. Every resolution evaluates server time again. No periodic expiry worker.
- `FirestoreEntitlements.kt`: transaction-backed once-per-UID grant with immutable evidence and recovery. Player bootstrap remains separate and idempotent: a failed grant can recover on the next bootstrap without losing eligibility.
- `EntitlementConfiguration.kt`: dependency wiring, profile and entitlement routes, typed errors. No public admin-grant endpoint or client grant-writing route.
- `EntitlementHistory.kt`: server-side history window and replay eligibility enforcement before archive delivery.

Storage:

```text
players/{uid}                              existing profile; no billing fields
players/{uid}/entitlementState/current     bounded effective-resolution projection
players/{uid}/entitlementGrants/{grantId}   durable grants, retained after expiry
players/{uid}/promotions/initial-premium-trial  immutable once-only evidence
players/{uid}/entitlementAudit/TRIAL_GRANTED
players/{uid}/entitlementAudit/admin-{grantId}
developmentTestAccounts/{uid}              existing isTestAccount exclusion
systemConfig/subscriptionPolicy            optional backend-owned remote policy
```

UID-scoped document reads need no new composite index or all-user scan. Admin projection pruning preserves immutable expired grant documents. The projection permits at most 64 retained grants; exceeding it rejects the new grant instead of silently truncating active rights.

The policy document is optional; this phase does **not** write a production seed. Missing document uses validated `domino.subscription.*` defaults. Defaults: enabled=true, promotionalTrialEnabled=true, promotionalTrialDays=7, trialRequiresLinkedAccount=false, freeFriendsMax=5, premiumFriendsMax=100, freeHistoryMax=10, freeReplayMax=3. Public modes and Follow are free; the Premium capabilities listed above are modeled, without implementing their future features. Policy edits require an increased policyVersion; rollback or changed content under the same version fails closed after cache refresh.

Policy storage failure produces typed unavailability for entitlement-dependent actions and disables new grants, rather than granting unrestricted access or declaring the user permanently FREE. Public gameplay remains independent. Repository failure likewise yields ENTITLEMENTS_UNAVAILABLE. Existing cached snapshots have bounded freshness; expiration itself is checked against server time on every resolution.

Routes retain existing Player Foundation conventions:

```text
POST /api/v1/player/bootstrap      existing response + entitlements summary
GET  /api/v1/player/profile        own existing public-profile fields only
GET  /api/v1/player/entitlements   own effective rights, server time and validUntil
PUT  /api/v1/player/display-name   existing unchanged alias flow
```

Identity always comes from the verified principal. Profile response excludes grant source, grant IDs and billing internals. Entitlements expose only the summary needed by the client. No endpoint accepts a client plan, validity or feature grant. Unsupported write methods return 405. Lifecycle logs contain event categories/policy version, never credentials or tokens. TRIAL_GRANTED and ADMIN_GRANTED have durable records; TRIAL_EXPIRED is observed/logged on effective resolution, with bounded in-process deduplication rather than a polling job.

## History, replay and expiration

Both history routes use the same entitlement gate. FREE fetches only its allowed window and applies pagination within that window; an out-of-window cursor cannot bypass it. Premium retains existing I4 pagination.

FREE replay ranking reads history and match **metadata**, never event archives to determine rank. It stops after the allowed eligible matches, with a safety cap of 200 examined history rows. Hitting that cap before establishing the window returns typed unavailability, not unrestricted access. Metadata eligibility uses finished online supported-mode matches with events and membership. Legacy/corrupt event archives are still rejected by I4; such a metadata-eligible archive can occupy a free slot. No legacy archive migration was attempted.

Manifest and every event page retain participant/completed-match authorization. A random, UID-and-match-bound, 30-minute access session permits a download already authorized before expiration to finish; it is bounded to 2048 in-memory tickets. New manifest entry revalidates current rights. Loaded playback is fully local and is never interrupted by expiration. Tickets are process-local; a different backend instance/restart revalidates entitlement rather than trusting an unknown ticket. Cross-instance continuation would need a future shared ticket design if required. No ReplayReducer semantics changed.

## Unity presentation

`PlayerService` carries the backend summary independently of wallet. `EntitlementProfilePresentation` displays FREE/TRIAL/PREMIUM/expired/unavailable, using server trialEndsAt for the displayed date. First-grant welcome explicitly says no payment and no automatic renewal. It refreshes on opening/foreground and once at the server-provided next transition; no interval polling, no locally calculated authorization. `HistoryReplayView` explains locked items and limited history. Shared EN/ES tables contain nine new keys. No Party/Friends screens or store prices were added.

Play Mode ran in the isolated existing Unity project under `client/Validation/Generated/I4Unity`, using Unity 6000.0.41f1 and a loopback-only Spring test backend on port 18088. Production HTTP controllers were used with in-memory test repositories and a test-classpath-only identity verifier. No real Firebase/Firestore services were used. The original user's Unity session was left intact. Screenshots and result are in `client/Validation/Generated/P01/`.

The new loopback validation server is test-classpath-only, requires DOMINO_P01_LOOPBACK=true and refuses real-Firestore opt-in. The Unity validation entry refuses a non-generated project. These are not production authentication paths.

## Evidence

- Backend XML: `server/domino/build/test-results/test/` — 498 total, 497 passed, one skipped. Local Redis enabled. Covers Player, wallet/monetization, match/runtime/matchmaking, history/replay and F0/F0.1.
- Swarm unit XML: `server/domino/tools/bot-swarm/build/test-results/test/` — 25 passed; no swarm run.
- Emulator XML: `server/domino/build/test-results/emulatorTest/` — 11 passed, including concurrent grant/repair, policy/cost and authenticated direct-write rejection.
- Unity Play Mode: `client/Validation/Generated/P01/unity-result.txt` — 67 checks passed, four states, EN/ES, portrait/tablet, no console errors.
- Static runtime and Editor compilation passed. Existing compiler warnings remain; no compile errors.
- Client regressions: Player Foundation 469; Guest Auth 43; H6 26; H5 27; H4 35; H2 46; H3 30; online 48; I3/M5 75; F0 isolation 16; catalog 42; catalog gameplay 251685; duel 14723; replay 1595 (62 matches / 31061 events); core gameplay 2981089 checks / 1000 games. All passed; logs in `client/Validation/Generated/P01/*.ps1.log`.

Measured emulator document reads: existing bootstrap=2; initial trial adds policy1 + projection1 + transaction5 =7, total9. Warm repeats add0, total2. Cold existing account adds projection1 and policy1 only if that shared cache is cold. Transaction retries can increase read cost under contention; reported numbers are the measured uncontended path, not a billing ceiling. Grant creates projection, immutable grant, promotion marker and audit record once. Expiration writes=0; entitlement reads per gameplay command=0.

`server/domino/firestore.rules` denies direct client reads/writes; the authenticated emulator test checks grants, projection, promotion, policy and development markers. **These rules were not deployed and current production rules were not inspected. Production direct-write protection is therefore not claimed as remotely verified.** Admin SDK uses server IAM. No real Firestore access occurred.

Protected assets retain initial SHA256:

```text
AdsSettings.asset=C377416E334727264806761518A4B5EDF381A837A927A1C3F3D523A1ACBDB5D6
ApiSettings.asset=AC6B0ED6D30240BC531B235E43E8B9B55D3B48DCBCCCF365ADEC62D3EE6BCB37
Localization Settings.asset=7F07A7C99327EC2A82A924E651E5A626995848F3014EA2AF562AEAA11B4B65AE
```

P1/billing remains unstarted. Review and checkpoint P0.1 separately before any next phase.

## Checkpoint review

Account linking that preserves the Firebase UID preserves the same profile, grant projection and trial-consumption marker. Entitlement bootstrap checks trialConsumed by UID before granting; changing anonymous/linked identity status cannot issue a second trial. LINKING_GRANTS_SECOND_TRIAL=NO. This confirms server semantics; a real Firebase account-link operation was not performed during this local checkpoint.

Reviewing and deploying the intended Firestore Rules, then validating direct-client rejection in the target environment, remains a P1/release prerequisite. FIRESTORE_RULES_DEPLOYED=NO and FIRESTORE_RULES_PRODUCTION_VALIDATED=NO for this checkpoint. No rules deployment or billing configuration is authorized by this closure.
