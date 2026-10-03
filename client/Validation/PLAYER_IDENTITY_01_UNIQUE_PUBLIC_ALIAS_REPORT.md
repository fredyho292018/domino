# PLAYER-IDENTITY-01 — Unique public Player alias

Base: `4890be3d4f38da14efc7924f66d57fb7b6ec29c1`. Environment audited: TEST. Product implementation is validated locally/isolated. The separately authorized TEST migration subsequently completed; see the final checkpoint review below. No product deployment or live Basic Profile execution has occurred.

Related evidence: [Player UI binding audit](PLAYER_UI_01_REAL_PLAYER_BINDING_AUDIT_REPORT.md). PLAYER-UI-01E remains paused.

## Historical pre-migration findings (superseded by 01B)

A read-only, transaction-consistent Firestore query found 226 Players with 226 display names, **42 normalized collision groups affecting 192 Players**, and zero `playerAliases` documents. All 226 aliases satisfy the preexisting ASCII character/length rule. The audit returned counts only; it did not modify Players or reservations. These findings must not be interpreted as a successful migration or as globally unique current TEST data.

Strict enforcement is implemented **fail-closed**. Alias claims and creation of new Players require `systemConfig/playerAliases` with `status=READY` and `normalizationVersion=1`. The application never creates this marker. Missing, malformed or conflicting legacy ownership is not repaired during a request. Existing Player bootstrap and completed onboarding are not reopened or migrated.

**Do not deploy this implementation as though migration were complete:** absent the reviewed marker/reservations, new registration bootstrap and Basic Profile alias saves will be rejected with `DISPLAY_NAME_RESERVATIONS_NOT_READY` (409). No marker was published in TEST.

## Audited existing behavior and identity contract

- `GuestDisplayNames.generate`, in the backend Player package, uses `SecureRandom`, the alphabet `ABCDEFGHJKLMNPQRSTUVWXYZ23456789` and eight random characters after `Guest-`. Before this change there was no authoritative reservation or collision retry; uniqueness was probabilistic only. Generation is retained; bootstrap now retries a collided generated candidate at most five times.
- `Player.displayName` remains the sole public alias. Private `firstName`/`lastName` remain in the existing Player profile authority. They are never concatenated or substituted for the alias; neither email nor Firebase UID is used to derive it.
- Basic Profile already contained a `DISPLAY_NAME` binding and field. This change names it **Player alias / Alias de jugador** and retains the existing EN/ES helper. The response's current Player display name prefills the draft, including an intentionally retained Guest alias. No new domain key, public identity field, or per-view Player store was introduced.
- Existing Home/Menu/Profile continue consuming PlayerPresentationState. Public bootstrap DTOs do not acquire personal first/last names.

## Normalization and validation

Accepted presentation: outer trim, Unicode NFC, then the established `[A-Za-z0-9_-]{3,16}` constraint. Displayed capitalization is preserved. Canonical uniqueness key: outer trim + NFC + lowercase using `Locale.ROOT`. The reservation document ID is the SHA-256 of the canonical UTF-8 value, so no alias becomes a Firestore path fragment.

Minimum 3, maximum 16; internal spaces, markup, accents, emoji and other non-ASCII characters remain outside the existing contract. International-character expansion is deliberately not bundled into this change. NFC equivalence is tested at the canonical-key layer; this does not imply accented aliases are now accepted. Outer whitespace is the intentional compatibility change.

The existing reserved set is retained case-insensitively: admin, administrator, moderator, support, teamfho, system. “Cuban Domino Club” is rejected by the existing spaces/length rule. No large censorship list was added.

## Atomic reservation and failure behavior

`PlayerAliasReservations.prepare` is shared by new Player foundation creation, `PUT /api/v1/player/display-name`, and the existing onboarding Basic Profile save.

`playerAliases/{sha256(canonical)}` stores `state`, `playerId` when claimed, and `normalizationVersion`. Claims require absent/released target or the same owner. Changing an alias also verifies the previous reservation belongs to that Player, then replaces it with a RELEASED record without ownership. Both operations and the Player/profile/public projection writes commit in the same Firestore transaction. All SDK reads precede buffered writes. No process lock provides production authority.

Same-owner identical saves add no reservation mutation; existing onboarding operation receipts retain their original revision/idempotency behavior. A conflict aborts private-name, country, preferences, onboarding and receipt writes together. No partial identity save is allowed.

`DISPLAY_NAME_TAKEN` is HTTP 409. Basic Profile retains all draft values and its route, marks/focuses the alias field, and displays “That alias is already taken.” / “Ese alias ya está en uso.” It releases the unsuccessful prepared operation so a corrected choice can be submitted. Rollout-not-ready is also an explicit inline domain message, not a generic connection error. Network uncertainty preserves the existing prepared operation and form for same-operation retry.

Continue is disabled for an invalid alias and refreshes when edited. The accepted character rule excludes rich markup; helper labels explicitly disable rich text. Other surfaces keep their existing presentation.

No availability endpoint was added: availability would be advisory and would add traffic without removing the transactional check. No per-keystroke requests occur. Existing authentication/write authorization and bounded transaction retries remain in use. The audited distributed rate limiter is Social-specific, not a general Player HTTP limiter; this change does not claim a new endpoint throttle. A general Player write-rate policy remains a separate hardening decision.

## Immediate client synchronization

After a validated successful Basic Profile response, the controller emits a defensive confirmed-state event. The session-scoped PlayerPresentationSource observes it and calls the existing `PlayerService.ReceiveConfirmedProfile`, preserving its current-session, snapshot and revision guards. It unsubscribes on disposal/controller replacement. Draft edits never update Player. A presentation listener cannot convert a committed save into a network retry.

No app restart or extra bootstrap is necessary. Presentation remains neutral while onboarding is active; Home/Menu/Profile receive the accepted alias when their existing route becomes available. Existing coach, membership and createdAt authorities remain unchanged.

## Migration and compatibility plan — design only

1. Obtain explicit authorization for a migration and collision-resolution policy. Preserve current Player identities, progression, grants and completed status.
2. Freeze all alias writers and new Player bootstrap across every API instance/version. An old instance can bypass the new collection; a per-process lock is insufficient.
3. Repeat a complete consistent audit with the exact backend canonical algorithm; review every collision group. Do not select winners or rename accounts automatically. The current 42 groups prevent claiming globally unique existing aliases.
4. After an approved resolution, transactionally create each reservation only when the observed Player alias still matches and ownership is unambiguous. Checkpoint/retry migration operations without overwriting conflicting owners. Audit both directions: every Player has its matching reservation, every claimed reservation has its Player, and canonical duplicates are zero.
5. Publish the versioned READY marker only after that verification under the write freeze. Deploy only writers that use this mechanism, then release the freeze. Rerun read-only reconciliation. Rollback to a writer that bypasses reservations is unsafe without another freeze.

No migration tool or collision repair was executed. An untouched completed legacy Player can still bootstrap; a legacy Player missing a reservation cannot claim/rename opportunistically. Post-onboarding Edit Profile UI is deferred; the already-existing display-name endpoint now uses the same reservation mechanism.

## Current validation

- Backend unit/in-memory regression: 903 discovered, **879 passed, 0 failed, 24 skipped** (including explicit emulator/real-environment exclusions). New tests cover generated reservations, Guest preservation, normalized collision, same-owner retry, rollback, missing rollout/legacy reservation, private fields, and guest collision without a second foundation.
- Actual local Firestore emulator: **1 passed**, simultaneous case-insensitive claims yield exactly one owner; losing Player remains unchanged; old winning reservation is released; identical retry does not rewrite Player. Emulator process and port cleanup passed. This is not a real TEST Firestore write.
- Basic Profile client: **49 passed**; alias binding: **9 passed**; onboarding shell: **93 passed**; bootstrap: **31 passed**; complete onboarding flow: **407 passed**.
- Menu binding **60 passed**, Home/Coach/version/isolation **222 passed**, Profile/createdAt/country **80 passed**. Session isolation harness **263 passed**, without resuming real A→B validation.
- Unity final imported-source run, started 2026-10-03 04:49:14 UTC: **2704 checks passed, 0 failed**, covering eight presets and EN/ES, including generated-alias prefill and invalid-alias Continue disabling. The earlier 2656-check run was not used for the final alias gate because its assembly predated the added checks.
- EN/ES copy passed; eight-preset focus/scroll passed **16/16**. Horizontal overflow and text-wrap checks passed. Unity compilation completed with **0 errors**; the current Console contains **9 warnings**, not a warning-free Editor claim.

## Implementation-stage safety and state (historical; superseded below)

```text
PUBLIC_PLAYER_IDENTITY_FIELD=displayName
FIRST_NAME_PUBLIC_IDENTITY=NO
LAST_NAME_PUBLIC_IDENTITY=NO
EMAIL_PUBLIC_IDENTITY=NO
UID_PUBLIC_IDENTITY=NO
DISPLAY_NAME_UNIQUENESS_SERVER_AUTHORITATIVE=YES_AFTER_REVIEWED_MIGRATION
CURRENT_TEST_ALIASES_GLOBALLY_UNIQUE=NO
DISPLAY_NAME_RESERVATION_ATOMIC=YES
DISPLAY_NAME_CLAIM_TRANSACTIONAL=YES
CONCURRENT_ALIAS_CLAIM_SINGLE_WINNER=PASS_FIRESTORE_EMULATOR
SAME_OWNER_ALIAS_RETRY=PASS
GUEST_TO_CUSTOM_ALIAS=SUPPORTED
KEEP_GENERATED_GUEST_ALIAS=SUPPORTED_WITH_VALID_RESERVATION
GENERATED_GUEST_ALIAS_MUST_BE_CHANGED=NO
EXISTING_ALIAS_RESERVATION_MIGRATION_REQUIRED=YES
EXISTING_COLLISIONS_AUTOMATICALLY_REPAIRED=NO
EXISTING_COMPLETED_PLAYERS_REOPENED=NO
DISPLAY_NAME_AVAILABILITY_ENDPOINT_REQUIRED=NO
BASIC_PROFILE_IDENTITY_ATOMIC=YES
PLAYER_SERVICE_ALIAS_REFRESH=YES
PLAYER_PRESENTATION_ALIAS_REFRESH=YES
BACKEND_DEPLOY_REQUIRED_FOR_ALIAS=YES
BACKEND_DEPLOY_READY=NO_MIGRATION_POLICY_PENDING
PLAYER_UI_01E_RESUMED=NO
REAL_PLAYER_ALIAS_CHANGED=NO
APP_SHELL_MOCK_CHANGED=NO
PREEXISTING_PENDING_FILES_PRESERVED=132/132
PREEXISTING_PROTECTED_FILES_MODIFIED=0/102
SECRET_SCAN=PASS_SCOPED_SOURCE_AND_REPORT
MOJIBAKE_MARKERS=0
PLAYER_IDENTITY_01_IMPLEMENTATION=TECHNICALLY_VALIDATED
TEST_ENFORCEMENT_ENABLED=NO
COMMIT=NONE
PUSH=NONE
DEPLOY=NO
```

Next: review this implementation and decide the existing-alias collision/migration policy before any deployment. No automatic continuation of PLAYER-UI-01E.

Tooling note: a desktop window inventory returned unrelated browser-title metadata, including a verification link. That material was not used or copied to source, validation artifacts or this report; subsequent UI work remained scoped to Unity. The scoped file scan does not assert that tool-output metadata was absent.

## Final checkpoint contract review — 2026-10-03

The authorized migration supersedes the duplicate-data blocker: 150 Players renamed, 226 reservations created, 42 winners preserved, zero duplicate groups and owner mismatches. Second run: zero semantic changes. See [migration report](PLAYER_IDENTITY_01_ALIAS_MIGRATION_REPORT.md), [sanitized dry run](PLAYER_IDENTITY_01_ALIAS_MIGRATION_DRY_RUN.json), and [sanitized execution](PLAYER_IDENTITY_01_ALIAS_MIGRATION_EXECUTION.json). No raw Player identifiers or private profile data are included.

The uniqueness-enforcing product remains undeployed and READY was not published. Future deployment must revalidate consistency and explicitly handle activation. Source completion does not prove the real custom-alias UI flow.

The exact product contract above is unchanged: displayName alone is public; private personal fields never replace it. Guest aliases may be retained indefinitely. Basic Profile prefill, atomic fields/reservations, same-owner retries, 409 inline conflicts, accepted-alias synchronization and existing UI consumers were reviewed. Completed Players are not reopened. Product hashes match tested implementation. Added code uses interfaces present at BASE_SHA; excluded temporary routing diagnostics/barriers are not required dependencies.

Retained evidence: backend 879 PASS; emulator concurrency PASS; Unity 2704 PASS; eight-preset EN/ES responsive; focus/scroll 16/16; onboarding 407; Menu 60; Home/Coach 222; Profile/createdAt 80. No backend or Unity suites repeated.

### Explicit checkpoint scope

33 files. Category counts: {'PLAYER_IDENTITY_PRODUCT_CLIENT': 4, 'PLAYER_IDENTITY_TEST': 17, 'PLAYER_IDENTITY_PRODUCT_BACKEND': 8, 'PLAYER_IDENTITY_REPORT': 2, 'PLAYER_IDENTITY_SANITIZED_EVIDENCE': 2}

| Category | Reviewed path |
|---|---|
| PLAYER_IDENTITY_PRODUCT_CLIENT | `client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/BasicProfileController.cs` |
| PLAYER_IDENTITY_TEST | `client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/Editor/ProductionBasicProfilePreview.cs` |
| PLAYER_IDENTITY_PRODUCT_CLIENT | `client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/OnboardingShellController.cs` |
| PLAYER_IDENTITY_PRODUCT_CLIENT | `client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/PlayerPresentationSource.cs` |
| PLAYER_IDENTITY_PRODUCT_CLIENT | `client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/ProductionBasicProfileView.cs` |
| PLAYER_IDENTITY_TEST | `client/Validation/BasicProfileTests.cs` |
| PLAYER_IDENTITY_PRODUCT_BACKEND | `server/domino/src/main/kotlin/com/teamfho/domino/common/ApiErrorResponse.kt` |
| PLAYER_IDENTITY_PRODUCT_BACKEND | `server/domino/src/main/kotlin/com/teamfho/domino/common/ApiExceptionHandler.kt` |
| PLAYER_IDENTITY_PRODUCT_BACKEND | `server/domino/src/main/kotlin/com/teamfho/domino/player/FirestorePlayerFoundationRepository.kt` |
| PLAYER_IDENTITY_PRODUCT_BACKEND | `server/domino/src/main/kotlin/com/teamfho/domino/player/OnboardingProgressService.kt` |
| PLAYER_IDENTITY_PRODUCT_BACKEND | `server/domino/src/main/kotlin/com/teamfho/domino/player/PlayerBootstrapService.kt` |
| PLAYER_IDENTITY_PRODUCT_BACKEND | `server/domino/src/main/kotlin/com/teamfho/domino/player/PlayerDisplayNameService.kt` |
| PLAYER_IDENTITY_PRODUCT_BACKEND | `server/domino/src/main/kotlin/com/teamfho/domino/player/PlayerFoundationRepository.kt` |
| PLAYER_IDENTITY_TEST | `server/domino/src/test/kotlin/com/teamfho/domino/player/BasicProfileFirestoreTests.kt` |
| PLAYER_IDENTITY_TEST | `server/domino/src/test/kotlin/com/teamfho/domino/player/BasicProfileOnboardingTests.kt` |
| PLAYER_IDENTITY_TEST | `server/domino/src/test/kotlin/com/teamfho/domino/player/InMemoryFirestoreTransactions.kt` |
| PLAYER_IDENTITY_TEST | `server/domino/src/test/kotlin/com/teamfho/domino/player/NewPlayerFoundationTests.kt` |
| PLAYER_IDENTITY_TEST | `server/domino/src/test/kotlin/com/teamfho/domino/player/OnboardingPersistenceTests.kt` |
| PLAYER_IDENTITY_TEST | `server/domino/src/test/kotlin/com/teamfho/domino/player/OnboardingProgressTests.kt` |
| PLAYER_IDENTITY_TEST | `server/domino/src/test/kotlin/com/teamfho/domino/player/PlayerBootstrapServiceTests.kt` |
| PLAYER_IDENTITY_TEST | `server/domino/src/test/kotlin/com/teamfho/domino/player/PlayerControllerTests.kt` |
| PLAYER_IDENTITY_TEST | `server/domino/src/test/kotlin/com/teamfho/domino/player/PlayerCreatedAtTests.kt` |
| PLAYER_IDENTITY_TEST | `server/domino/src/test/kotlin/com/teamfho/domino/player/PlayerDisplayNameTests.kt` |
| PLAYER_IDENTITY_TEST | `server/domino/src/test/kotlin/com/teamfho/domino/player/PlayerFoundationRepositoryTests.kt` |
| PLAYER_IDENTITY_REPORT | `client/Validation/PLAYER_IDENTITY_01_UNIQUE_PUBLIC_ALIAS_REPORT.md` |
| PLAYER_IDENTITY_TEST | `client/Validation/PlayerAliasBindingTests.cs` |
| PLAYER_IDENTITY_TEST | `client/Validation/RunPlayerAliasBindingTests.ps1` |
| PLAYER_IDENTITY_PRODUCT_BACKEND | `server/domino/src/main/kotlin/com/teamfho/domino/player/PlayerAliasReservations.kt` |
| PLAYER_IDENTITY_TEST | `server/domino/src/test/kotlin/com/teamfho/domino/player/PlayerAliasEmulatorTests.kt` |
| PLAYER_IDENTITY_TEST | `server/domino/src/test/kotlin/com/teamfho/domino/player/PlayerAliasReservationTests.kt` |
| PLAYER_IDENTITY_REPORT | `client/Validation/PLAYER_IDENTITY_01_ALIAS_MIGRATION_REPORT.md` |
| PLAYER_IDENTITY_SANITIZED_EVIDENCE | `client/Validation/PLAYER_IDENTITY_01_ALIAS_MIGRATION_DRY_RUN.json` |
| PLAYER_IDENTITY_SANITIZED_EVIDENCE | `client/Validation/PLAYER_IDENTITY_01_ALIAS_MIGRATION_EXECUTION.json` |

132 historical entries remain byte-identical: 102 PROTECTED, 2 UNRELATED, 11 TEMPORARY_SMOKE, 2 historical temporary validation helpers and 15 HISTORICAL_PENDING reports. Three additional pre-implementation entries remain excluded as PLAYER_UI_01E_FUTURE: the pending Player UI audit report and two session-isolation test/runner files. Total excluded pending entries=135. UNCLASSIFIED_PENDING_FILES=0. The older baseline lacks hashes for quoted paths; their unchanged hashes were verified against the complete 132-file preservation inventory instead.

Generated executors, credential bootstrap code, migration tests and process checks are TEMPORARY_MIGRATION, not staged. The server-only recovery artifact is SENSITIVE_ROLLBACK, not a Git candidate. Sanitized execution evidence is copied into the Validation report convention; ignored Generated outputs are not force-added. No durable migration tool is promoted in this checkpoint.

REAL_PLAYER_ALIAS_MUTATIONS_DURING_CHECKPOINT=0. APP_SHELL_MOCK_CHANGED=NO. PLAYER_UI_01E_RESUMED=NO. DEPLOY=NO. Live completion awaits TEST deployment and real alias validation. Commit/remote verification will be recorded separately after publication.
