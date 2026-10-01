# ONB-BE-06 Membership Catalog + Contracts

Base: `10a8f86bf2dd8749c74c9f5bfc0d36c4cf6fdb83`.
Scope: backend catalog and contracts only. No runtime commercial authorization, rollout, real publication, billing integration, Family domain or trial transition. No existing source file modified; seven new Kotlin files and this report.

## Catalog and presentation

Membership publication schema 1 / catalog version 1 contains five plans, eight commercial feature definitions, forty explicit plan-feature relations, complete es/en translations and sixteen inactive billing mapping slots. Display order: DIAMOND, PLATINUM, GOLD, FRIENDS_AND_FAMILY, FREE. Hierarchy is separate explicit metadata: FREE < GOLD < PLATINUM < DIAMOND. Family productKind=MULTI_PLAYER; other plans INDIVIDUAL. Stable keys do not depend on translations.

| Commercial feature | FREE | GOLD | PLATINUM | DIAMOND | FAMILY |
|---|---|---|---|---|---|
| GAME_REVIEW | No | No | Yes | Yes | Yes |
| MOVE_EXPLANATIONS | No | No | No | Yes | Yes |
| ADVANCED_STATS | No | No | No | Yes | Yes |
| PUZZLES | No | Yes | Yes | Yes | Yes |
| LESSONS | No | Yes | Yes | Yes | Yes |
| COACH_GAMES | No | Yes | Yes | Yes | Yes |
| BOTS | No | Yes | Yes | Yes | Yes |
| NO_ADS | No | Yes | Yes | Yes | Yes |

These are intended BOOLEAN_CAPABILITY definitions, not assertions that their services/enforcement exist. Contract also supports QUOTA and PRESENTATION_ONLY; contradictory quota values are rejected. V1 uses an explicit known feature inventory; an unknown future feature cannot enter through inheritance. Adding new capabilities requires an explicit contract/policy update.

## Inactive target entitlement policy

The target policy is deliberately a separate type from SubscriptionPolicy. No runtime resolver references it. Its version is independent from the catalog and trial reference versions.

| Backend key | FREE | GOLD | PLATINUM | DIAMOND | FAMILY |
|---|---|---|---|---|---|
| PUBLIC_DUEL | Yes | Yes | Yes | Yes | Yes |
| PUBLIC_PARTNERS | Yes | Yes | Yes | Yes | Yes |
| FOLLOW_PLAYER | Yes | Yes | Yes | Yes | Yes |
| FRIENDS | Yes | Yes | Yes | Yes | Yes |
| FRIEND_REQUESTS | Yes | Yes | Yes | Yes | Yes |
| PARTY_CREATE | No | Yes | Yes | Yes | Yes |
| PARTY_INVITE | No | Yes | Yes | Yes | Yes |
| PRIVATE_DUEL | No | Yes | Yes | Yes | Yes |
| PRIVATE_PARTNERS | No | Yes | Yes | Yes | Yes |
| CHOOSE_2V2_PARTNER | No | Yes | Yes | Yes | Yes |
| PARTY_MATCHMAKING | No | Yes | Yes | Yes | Yes |
| FULL_HISTORY | No | No | Yes | Yes | Yes |
| FULL_REPLAY | No | No | Yes | Yes | Yes |
| ADVANCED_STATS | No | No | No | Yes | Yes |
| PREMIUM_THEMES | No | Yes | Yes | Yes | Yes |
| FRIENDS_MAX | 5 | 100 | 100 | 100 | 100 |
| HISTORY_MAX | 10 | 10 | Unlimited | Unlimited | Unlimited |
| REPLAY_MAX | 3 | 3 | Unlimited | Unlimited | Unlimited |

REPLAY_MAX is an eligible-match window, not consumable plays. Family limits are per Player. FULL_HISTORY, FULL_REPLAY and GAME_REVIEW remain distinct. Catalog reads never grant these capabilities. Current FREE/PREMIUM resolver, historical grants, policyVersion behavior, Social and replay enforcement remain unchanged. No PREMIUM-to-commercial-tier mapping.

## Publication and persistence

Firestore adapter follows the existing catalog transaction pattern:

- `membershipCatalogs/{catalogVersion}`: immutable complete publication, including target configuration snapshot and trial reference.
- `systemConfig/membershipCatalog`: monotonic publishedVersion pointer.
- `membershipTargetPolicies/{version}`: immutable inactive commercial target policy, isolated from systemConfig/subscriptionPolicy.
- `membershipTrialReferences/{version}`: immutable reference/presentation contract, not a second runtime trial-policy loader.

All reads precede writes in publication transaction. Identical seed is a no-op. Existing content/policy conflicts are rejected. Historical identical seed does not roll back the current pointer. Translation-only publication can reuse unchanged target/trial versions; changing their content without changing the corresponding version fails. Publication size is bounded to 512 KiB. Seed has no automatic startup hook, public mutation endpoint or invocation against real Firestore.

Validation rejects incomplete/duplicate plan-feature matrix, invalid translations, unknown feature keys/kinds, active target enforcement, inconsistent History/Replay limits, invalid quotas and invalid billing mappings. There is no UNRESOLVED runtime value.

## Family, billing and trial boundaries

Family metadata: 2–5 Players, owner included, Diamond-equivalent features, per-Player limits, invitation expiry default 7 days configurable in a future immutable publication (accepted range 1–30). No invitations, capacity checks, ownership relation, membership resolver or permanent member grants implemented. Invitation configuration is not exposed as an active capability.

Sixteen mapping slots: four paid plans x MONTHLY/YEARLY x APPLE_APP_STORE/GOOGLE_PLAY. All storeProductId values are null, active=false, purchasable=false. No fake IDs, mock prices or price field. Plan feature relations do not depend on billing period. Price authority is APPLE_GOOGLE_STORE. Active mappings would require real configured identifiers; this phase configures none.

Trial presentation identifies PREMIUM_LEGACY only. No commercial-tier trial advertised; Family trial disabled. No old mock trial CTA/copy copied into production catalog. The reference version does not pin or change the current runtime policy. No trial endpoint, no bootstrap change, no reminder delivery. BE-07 owns future activation and reconciliation.

## Read API and DTOs

`GET /api/v1/membership/catalog?locale=es&version=1`.

Uses existing Firebase authentication and OnboardingCatalogAccess ACTIVE-Player read check. Locale normalization reuses OnboardingCatalogLocalization.locale (es-US -> es, en-US -> en, unsupported -> en); no third implementation. Whole-response English fallback when requested translation coverage is incomplete on read; publication requires full es/en.

Explicit DTOs: MembershipCatalogResponse, MembershipPlanResponse, MembershipFeatureResponse, BillingProductMetadataResponse; nested safe feature, trial and Family presentation DTOs. No persistence entity returned. No Player-specific data, target-policy body, admin rules, receipt, purchase secret or grant metadata.

Current read filters inactive plans; explicit historical read retains them with active=false and purchasable=false. Order deterministic. Errors follow catalog conventions: invalid version 400, missing catalog/version 404, storage/contract failure 503, safe requestId and no-store errors. Current/private cache max-age=300; historical=86400; ETag differentiates locale/version/current vs historical. Authentication/access still runs before 304.

Read has no mutation dependency: no Player bootstrap, trial grant, onboarding write, purchase or Family creation. Optional onboarding Membership semantics and pins unchanged.

## Validation evidence

- Focused initial domain tests passed.
- Expanded focused run found one defect: nonnumeric version was handled by global exception mapping as 500. Corrected only the new controller with a scoped type-mismatch handler returning 400.
- Final command: `./gradlew.bat :test --console=plain` in server/domino. BUILD SUCCESSFUL.
- XML totals: 851 discovered, **827 passed, 0 failures, 0 errors, 24 skipped**.
- New BE06: **30 passed** (24 catalog/domain, 5 HTTP, 1 Firestore SDK transaction-double test).
- Full existing suite includes BE01–05, Auth/bootstrap, Entitlements, Social/lifecycle and History/Replay. Skipped tests remain skipped, not claimed as passed.
- Tests cover exact matrices, es/en parity/fallback, ordering, historical/current reads, missing versions, inactive filtering, future-feature rejection, seed repeat/retry, immutable policy boundaries, Family metadata, sixteen inactive slots, DTO privacy and legacy resolver behavior.
- Production Firestore repository exercised with SDK doubles: transaction retry, all reads before writes, idempotent repeat, unchanged Player/entitlement/trial/onboarding documents across reads. No real Firestore or emulator execution claimed.
- Authenticated MockMvc endpoint checks use test auth and test storage. No real Firebase or Unity tests performed.
- Warnings remain: JVM class sharing, Gradle deprecations and Redis connection-closed warnings during test shutdown. These are not compiler errors; suite exit code 0.
- Protected inventory: 0/102 changed. Scoped source scan: no private keys, JWT-like values, API keys or real email addresses found. Manual review of new fixtures: synthetic identities only, no credential material.

## Changed files

New production:
1. server/domino/src/main/kotlin/com/teamfho/domino/catalog/MembershipCatalog.kt
2. server/domino/src/main/kotlin/com/teamfho/domino/catalog/MembershipCatalogSeed.kt
3. server/domino/src/main/kotlin/com/teamfho/domino/catalog/MembershipCatalogRepository.kt
4. server/domino/src/main/kotlin/com/teamfho/domino/catalog/MembershipCatalogHttp.kt

New tests:
5. server/domino/src/test/kotlin/com/teamfho/domino/catalog/MembershipCatalogTests.kt
6. server/domino/src/test/kotlin/com/teamfho/domino/catalog/MembershipCatalogHttpTests.kt
7. server/domino/src/test/kotlin/com/teamfho/domino/catalog/MembershipCatalogFirestoreTests.kt

New report:
8. client/Validation/ONB_BE_06_MEMBERSHIP_CATALOG_CONTRACT_REPORT.md

No preexisting file edited. No staging, commit, push, deploy, catalog publication or BE-07 start.

ONB_BE_06_SUCCESS=YES
NEXT=ONB-BE-06 IMPLEMENTATION REVIEW

## Authorized final checkpoint review

Review base remains 10a8f86bf2dd8749c74c9f5bfc0d36c4cf6fdb83 (BE05 checkpoint is HEAD). Product and test source unchanged since the successful suite; suite not repeated. Existing warnings documented above, no blocking test exception.

Pending inventory: 114 = 102 protected + 4 unrelated prior design reports + 8 BE06 files. Unclassified=0; BE05 pending=0. The unrelated reports are FUNCTIONAL_00_AUTH_PLAYER_BACKEND_AUDIT.md, ONB_00A_BACKEND_DOMAIN_API_DESIGN.txt, ONB_00_BACKEND_CONTRACT_FINAL_REVIEW.md and ONB_00_ONBOARDING_PLAYER_COACH_MEMBERSHIP_DESIGN.txt; all remain excluded.

Classification: MembershipCatalog.kt = domain/catalog/translations/target-policy/billing contracts; MembershipCatalogSeed.kt = canonical seed and localized matrix; MembershipCatalogRepository.kt = persistence; MembershipCatalogHttp.kt = API; three new test files = TEST; this report = VALIDATION. All 102 protected paths retain baseline SHA256.

Final source review confirms five unique plan keys, eight unique feature keys, independent hierarchy, exact approved matrices, inactive target policy, no runtime commercial wiring, no real publication, no Player-specific response fields. Historical no-commit notes above describe implementation phase; this review authorizes only the eight-file checkpoint. Commit and verified remote SHA are reported in the final response.
