# PLAYER-IDENTITY-01A TEST alias migration audit

## Scope and evidence

Read-only TEST audit and migration design. No migration implementation or execution is authorized by this report. Two consistent read-only snapshots confirmed the expected 42 collision groups. The final snapshot timestamp is **2026-10-03T05:02:09.754914+00:00** (explicit UTC offset); dataset digest: `78d88c1c433fab3a4d1cbea07a59c1a85791a7058304bdf519efbb658b3978c8`.

Read-only Firestore transaction: projected `players`, `developmentTestAccounts`, `players/{Player}/onboarding/current` and `playerAliases`; transaction ended with rollback. Credential contents, raw identifiers, original aliases, emails and private names are excluded from this evidence. Synthetic PLAYER labels are snapshot-local references, not public identity or executable database keys.

Normalization source: `server/domino/src/main/kotlin/com/teamfho/domino/player/PlayerAliasReservations.kt`: trim, Unicode NFC, lowercase Locale.ROOT; reservation key is SHA-256 of canonical UTF-8 alias. All observed aliases were ASCII, so the audit's equivalent normalization has no cross-runtime Unicode case-mapping ambiguity. Non-ASCII input causes this audit to stop rather than assume equivalence.

Replacement candidates were generated locally by the actual compiled `GuestDisplayNames.generate()` implementation: SecureRandom, eight characters from its official alphabet, prefixed `Guest-`. No private data or numbered suffixes were used. Candidates were compared against every existing canonical alias, hence also against all planned retained aliases.

## Collision inventory

| Metric | Result |
|---|---:|
| Players scanned / with display name | 226 / 226 |
| Without display name | 0 |
| Distinct normalized groups, including collisions | 76 |
| Singleton normalized groups | 34 |
| Duplicate groups | 42 |
| Players in duplicate groups | 192 |
| Excess owners / proposed renames | 150 |
| Groups of size 2 | 0 |
| Groups of size 3 | 12 |
| Groups of size 4+ | 30 |
| Largest group | 8 |

Full distribution: size 3 = 12 groups; 4 = 10; 5 = 11; 6 = 5; 7 = 1; 8 = 3. Sum(group size - 1) = 150.

Collision causes: EXACT_SAME_ALIAS=41; CASE_ONLY_COLLISION=1; WHITESPACE_NORMALIZATION_COLLISION=0; UNICODE_NORMALIZATION_COLLISION=0; GENERATED_GUEST_COLLISION=0; OTHER=0. No duplicate group even has a Guest-prefixed alias. Classification is complete for this snapshot, not a claim that the random generator can never collide.

Account classification among the 192: HISTORICAL_LOAD_TEST=178; OTHER_TEST_RECORD=14. Classification uses existing test/load markers, not presumed real-person identity. The latter 14 are not proven disposable and are not automatically classified as legitimate real users. Bot/swarm markers overlap the historical load category and are not double-counted. No colliding Player has authoritative onboarding COMPLETED; all have usable createdAt.

CURRENT_E2E_PLAYER_ALIAS_COLLISION=NO. OLD_409_PLAYER_ALIAS_COLLISION=NO. Internally pinned identities each matched exactly one Player; no identifiers are disclosed. Neither needs a rename or onboarding repair.

## Deterministic ownership and sanitized dry run

Priority: protected validated E2E; then ACTIVE, COMPLETED Player without confirmed disposable-test classification; then earliest createdAt; then stable Player ID as final tie-breaker. Missing createdAt sorts after known timestamps. Ambiguous metadata that could affect a future winner must stop execution for review. lastSeenAt is neither queried for ownership nor used as authority.

In this snapshot all 42 winners are decided by earliest createdAt, so no subjective legitimacy choice or final identity tie-break is required. Winners: 37 historical load, 5 other TEST records. Proposed renamed Players: 141 historical load, 9 other TEST records.

The companion `PLAYER_IDENTITY_01_ALIAS_MIGRATION_DRY_RUN.json` contains all 42 groups, winner reasons, 150 synthetic loser identifiers and 150 frozen official replacement aliases. Manifest digest: `120b7b5959a49b08a2c4498036f029a55274016b0b7379798cd0a3138a5091e2` (SHA-256 of canonical JSON before adding the digest field). It contains no raw Player mapping. Reconstruct that mapping internally from the verified source snapshot for a future authorized execution; never guess identities from labels. Any dataset drift requires review/replanning. This document is not an executable migration.

| Group | Size | Retained owner (sanitized) | Renames |
|---|---:|---|---:|
| GROUP_001 | 3 | PLAYER_0123 | 2 |
| GROUP_002 | 3 | PLAYER_0110 | 2 |
| GROUP_003 | 4 | PLAYER_0171 | 3 |
| GROUP_004 | 6 | PLAYER_0142 | 5 |
| GROUP_005 | 6 | PLAYER_0191 | 5 |
| GROUP_006 | 3 | PLAYER_0016 | 2 |
| GROUP_007 | 4 | PLAYER_0189 | 3 |
| GROUP_008 | 4 | PLAYER_0103 | 3 |
| GROUP_009 | 5 | PLAYER_0100 | 4 |
| GROUP_010 | 5 | PLAYER_0107 | 4 |
| GROUP_011 | 5 | PLAYER_0225 | 4 |
| GROUP_012 | 5 | PLAYER_0038 | 4 |
| GROUP_013 | 3 | PLAYER_0201 | 2 |
| GROUP_014 | 5 | PLAYER_0204 | 4 |
| GROUP_015 | 4 | PLAYER_0114 | 3 |
| GROUP_016 | 3 | PLAYER_0152 | 2 |
| GROUP_017 | 7 | PLAYER_0126 | 6 |
| GROUP_018 | 3 | PLAYER_0079 | 2 |
| GROUP_019 | 3 | PLAYER_0155 | 2 |
| GROUP_020 | 4 | PLAYER_0086 | 3 |
| GROUP_021 | 5 | PLAYER_0154 | 4 |
| GROUP_022 | 6 | PLAYER_0128 | 5 |
| GROUP_023 | 3 | PLAYER_0019 | 2 |
| GROUP_024 | 5 | PLAYER_0139 | 4 |
| GROUP_025 | 4 | PLAYER_0066 | 3 |
| GROUP_026 | 3 | PLAYER_0125 | 2 |
| GROUP_027 | 5 | PLAYER_0049 | 4 |
| GROUP_028 | 8 | PLAYER_0073 | 7 |
| GROUP_029 | 4 | PLAYER_0069 | 3 |
| GROUP_030 | 3 | PLAYER_0017 | 2 |
| GROUP_031 | 4 | PLAYER_0072 | 3 |
| GROUP_032 | 8 | PLAYER_0094 | 7 |
| GROUP_033 | 5 | PLAYER_0135 | 4 |
| GROUP_034 | 5 | PLAYER_0132 | 4 |
| GROUP_035 | 8 | PLAYER_0192 | 7 |
| GROUP_036 | 4 | PLAYER_0020 | 3 |
| GROUP_037 | 3 | PLAYER_0046 | 2 |
| GROUP_038 | 3 | PLAYER_0005 | 2 |
| GROUP_039 | 4 | PLAYER_0067 | 3 |
| GROUP_040 | 5 | PLAYER_0149 | 4 |
| GROUP_041 | 6 | PLAYER_0061 | 5 |
| GROUP_042 | 6 | PLAYER_0153 | 5 |

## Reservation bootstrap and atomic transition design

Current reservations=0. Reserve 76 retained aliases (42 winners plus 34 singletons), then 150 replacement aliases: RESERVATIONS_TO_CREATE=226; PLAYERS_TO_RENAME=150. Singleton and winner Player documents need no alias update.

The normal rename helper is not a legacy migration entry point: it requires the rollout READY marker and an existing previous reservation owned by the renaming Player. A separately reviewed administrative migration adapter is required; it must share the production canonicalizer, generator and reservation schema. Do not open READY early or weaken the public helper to bypass those checks.

1. Before execution, freeze every alias writer and new-Player bootstrap across all instances, including old code that bypasses reservations. Keep uniqueness rollout non-READY. Establish a secure preimage/recovery manifest and verify snapshot preconditions. These actions need separate authorization; none happened here.
2. Transactionally claim each retained key only if absent or already owned by its expected winner/singleton. Unexpected ownership or schema is a hard stop.
3. For each loser, read all preconditions before writes: frozen old alias, Player revision, retained winner ownership, candidate availability and migration receipt. Atomically claim its new reservation, update displayName/profileRevision/updatedAt and the existing public profile projection according to the production rename contract, and record a migration receipt. Preserve all other Player fields. A loser must never release or overwrite the old duplicate key: it belongs to the winner.
4. Use a frozen candidate outside transaction retry callbacks. A collision at execution time stops for replan; do not silently substitute an unreviewed alias. Firestore retries reread preconditions and atomically commit reservation, Player transition and receipt.
5. Receipt keys bind manifest and Player; an already-completed transition with matching owner/alias returns without writes. A second complete execution must make zero alias changes and zero new reservations. A partial mismatch stops, never overwrites.

Atomicity is per Player transition, not one global 226-Player transaction. The write freeze and non-READY gate protect the incomplete global phase. Transactional and idempotent properties here are design requirements, not claims of a migration tested against Firestore.

## Post-migration validation plan

Before releasing the freeze or setting normalization-version-1 READY, read the entire affected population and reservations again. Require 226 Players with required valid aliases, 226 matching claimed reservations, duplicate normalized groups=0, owner mismatches=0, duplicate reservation keys=0, missing/orphan reservations=0. Verify all 150 planned renames and all 76 retained owners, receipt completion and public-profile consistency. Recheck no late writers appeared.

Compare protected preimages for UID/Player identity, private fields, onboarding, lifecycle, stats, history, social relationships, inventory, grants, trial state and entitlements. No trial grant/revocation, onboarding repair, account deletion or historical match/event rewrite is part of this migration. Preserve historical alias snapshots unless a separate approved contract explicitly requires otherwise. Check E2E and historical 409 Player unchanged. Finally validate a second dry run produces zero proposed writes. Deployment/enforcement activation remains separately authorized.

## Recovery / rollback

Stop immediately on unexpected ownership, concurrent writes, unknown metadata or partial receipt inconsistency. Keep the write freeze and non-READY state. Prefer completing a validated partial plan. A rollback needs secure preimages and a separate authorization: restoring old aliases reintroduces duplicates, so enforcement cannot remain READY. Restore only manifest-owned Player changes and remove only proven migration-owned reservations after checking references/ownership. Never release another Player's retained key or cascade-delete shared data. Do not reactivate old writers while uniqueness enforcement is active. No export, backup modification or rollback was executed; recovery storage and access must be agreed before execution.

## Final gates

```text
ENVIRONMENT=TEST
TOTAL_PLAYERS_SCANNED=226
PLAYERS_WITH_DISPLAY_NAME=226
PLAYERS_WITHOUT_DISPLAY_NAME=0
NORMALIZED_ALIAS_UNIQUE_GROUPS=76_DISTINCT_KEYS_34_SINGLETONS
DUPLICATE_ALIAS_GROUPS=42
TOTAL_PLAYERS_IN_DUPLICATE_GROUPS=192
TOTAL_EXCESS_ALIAS_OWNERS=150
DUPLICATE_CAUSE_CLASSIFICATION_COMPLETE=YES
CURRENT_E2E_PLAYER_ALIAS_COLLISION=NO
OLD_409_PLAYER_ALIAS_COLLISION=NO
DETERMINISTIC_WINNER_POLICY_FEASIBLE=YES
LAST_SEEN_USED_AS_ALIAS_OWNER_AUTHORITY=NO
LOSER_ALIAS_POLICY=NEW_AUTHORITATIVELY_UNIQUE_GUEST_ALIAS
PERSONAL_DATA_USED_FOR_REPLACEMENT_ALIAS=NO
RESERVATIONS_TO_CREATE=226
PLAYERS_TO_RENAME=150
MIGRATION_PLAYER_ALIAS_CHANGE_TRANSACTIONAL=YES_DESIGN_ONLY
MIGRATION_IDEMPOTENT=YES_DESIGN_ONLY
DRY_RUN_GENERATED=YES
PLANNED_REPLACEMENT_COLLISIONS=0
POST_MIGRATION_VALIDATION_PLAN=PASS
BASIC_PROFILE_ALIAS_UI_CHANGED=NO
PRODUCT_SOURCE_CHANGED=NO
REAL_PLAYER_ALIAS_MUTATIONS=0
ALIAS_RESERVATIONS_CREATED=0
PLAYERS_DELETED=0
ACCOUNTS_DELETED=0
PREEXISTING_PENDING_FILES_PRESERVED=132/132
PROTECTED_FILES_MODIFIED=0/102
SECRET_SCAN=PASS
COMMIT=NONE
PUSH=NONE
DEPLOY=NO
PLAYER_UI_01E_RESUMED=NO
NEXT=TEST ALIAS MIGRATION EXECUTION REVIEW
```

## PLAYER-IDENTITY-01B execution preflight — 2026-10-03

The authorized execution has not started database writes. Fresh read-only evidence confirms TEST project `teamfho-domino`, database `(default)`, the same snapshot digest, all 42 winner/loser groups and every frozen replacement alias. Counts remain 42 collision groups, 150 proposed renames and 226 required reservations. Both protected Players remain outside the groups. API and Redis are running and healthy; neither was restarted.

Execution is pending an explicit maintenance-window boundary and confirmation that external administrative/load writers are absent. The reviewed plan requires a write freeze; per-Player optimistic concurrency alone does not prevent an old writer from creating another alias owner elsewhere. No deployed alias-specific freeze mechanism was established. Do not equate an unchanged read-only snapshot with a proven write freeze.

No reversible migration snapshot has been created yet. No migration, rollback or second-run execution occurred. Repeat the fresh precondition check after the writer freeze is established and immediately before creating the rollback snapshot and first write. Product source remains unchanged; all 132 historical pending files and 102 protected files match their recorded hashes. Sanitized preflight evidence: `Generated/PlayerIdentity01/alias-migration-execution.json`.

## PLAYER-IDENTITY-01B executed — 2026-10-03 (America/Chicago)

**Migration complete. This section supersedes the pending execution status above.** The owner explicitly authorized operational exclusivity instead of new freeze infrastructure. Unity was visually verified outside Play; local process inspection and remote process checks found no bot/load/creator/migration writers. A server-local exclusive lock prevented a second instance of this tool. API and Redis remained running.

Fresh preconditions exactly matched the reviewed snapshot digest, all 42 group plans and the frozen replacements. A reversible snapshot for 226 Players was written outside the repository in a private server directory, with directory mode 0700 and file mode 0600. It includes internal identifiers, old/new aliases, normalized keys, roles, prior reservation absence and hashes of preserved data; no raw private profile fields or authentication material. It was neither committed nor pushed.

### Executed scope and atomicity

42 duplicate-group winners and 34 singleton owners retained their visible aliases. Their 76 reservations were created first. Each of the 150 loser transactions read the Player, replacement reservation and retained winner reservation, checked the full original Player field hash, claimed the replacement and updated only `displayName` using an explicit field mask and updateTime precondition. Old duplicate reservations were never released: they belong to the retained winners.

The latest authorization superseded the earlier broader design: **no profileRevision, updatedAt, public-profile projection, migration receipt or rollout marker was written to Firestore**. The preflight confirmed none of the 150 losers had a publicIdentity/current requiring a public-profile projection update. Local snapshot/state evidence supplies recovery and idempotency information; there are no extra database bookkeeping writes.

Migration: 150 renames, 226 reservations, 42 winners preserved, 0 conflicts, 0 failures. Nine local tooling tests covered atomic success, stale state, reservation ownership conflicts, abort handling, winner preservation, old-key preservation and no-write repeat execution. These are migration-tool tests; product suites were not repeated.

### Post-validation and second execution

Both post-migration and final audits confirmed: 226 valid canonical owners, 226 matching CLAIMED reservations, 0 duplicate groups, 0 missing aliases, 0 missing reservations, 0 owner/key mismatches and 0 extra reservations. The normalization algorithm matches the implemented trim/NFC/Locale.ROOT contract for the asserted ASCII population; no claim of a separate JVM execution over live data is made.

Every Player's complete field hash matched its expected final state: original fields with only planned displayName substitutions. All 1,808 monitored nested document positions (including absent documents) retained their hashes/existence: preferences, dominoProfile, onboarding, wallet, entitlementState, initial trial grant/audit and publicIdentity. Other grants and historical data were outside the write allowlist and not rewritten. Both protected Players' complete root field hashes and monitored nested state were unchanged.

The same transactional transition logic ran a second time for every Player. Matching final fields plus expected reservation ownership caused read-only rollback/return, with **0 renames, 0 reservations and 0 semantic changes**. No database receipt or timestamp was touched. A subsequent full audit passed again. API and Redis were healthy afterward and their container start times exactly matched the pre-migration values: no restart or deployment. Rollback was not required or executed.

The uniqueness-enforcing product remains undeployed and the rollout marker was not opened. This is a verified TEST migration snapshot, not a guarantee against future duplicates from the old backend. Keep the next step at the explicit checkpoint/deploy review and revalidate before activating enforcement. PLAYER-UI-01E remains paused.

Sanitized execution evidence: `Generated/PlayerIdentity01/alias-migration-execution.json`. Detailed subprocess output and tooling remain in ignored Generated paths. No commit, push, deployment or product-source change was performed.

Final protection verification: product-scope files unchanged=30/30; historical pending files preserved=132/132; protected files unchanged=102/102. SECRET_SCAN=PASS; PRIVACY_SCAN=PASS. Snapshot field/privacy and mode checks passed without displaying its contents.

PLAYER_IDENTITY_01B_MIGRATION_SUCCESS=YES

NEXT=PLAYER-IDENTITY-01 FINAL REVIEW + CHECKPOINT
