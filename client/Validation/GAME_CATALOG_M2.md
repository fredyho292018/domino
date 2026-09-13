# M2: gameplay catalog migration

The local 2v2 game now resolves its configuration through `ApplicationServices.GameCatalog.ResolveMatch()` at `DominoClientController.StartMatch`. Resolution is synchronous: remote accepted catalog, valid persistent cache, or bundled catalog. Starting a match never waits for a request.

`GameCatalogConfigurationAdapter` validates the supported local mode and creates an immutable `MatchRuleSnapshot`. `SessionSetup` retains this snapshot and passes its configuration to the existing game engine. The catalog codec already combines GameMode topology and RuleSet rules into a deeply immutable configuration; no legacy JSON fields fill gaps.

The active match, subsequent rounds and restart retain their snapshot. Exiting and starting another match resolves the latest accepted catalog. A refresh cannot mutate an existing engine. Safe diagnostics identify source, catalog version, mode, topology version, ruleset version and content hash.

The supported session is PARTNERS_2V2: four seats, two teams of two, one local human and three explicit BOT controls. Team assignments come from GameMode. Bots must be permitted and human-count constraints must admit one human. Unsupported catalogs are rejected before replacing the last valid configuration or cache.

## Compatibility

The hidden serialized `configurationJson` reference remains for existing scene/editor validation compatibility; gameplay never reads it. `LocalGameConfiguration` remains a regression reference. `GameModeDefinition.TeamMatch` is only a compatibility selection token for existing validators, never a session configuration authority. Runtime menu/session definitions use catalog metadata. Menu preview metadata is captured when the menu is initialized; match startup always resolves the current catalog.

No engine rules, random generation, scoring, turn progression, monetization or backend sources changed. The current mode remains Double-9, ten tiles each, fifteen reserved, no draw, target 200, deal order 0/1/2/3 and turn order 0/3/2/1.

## Validation

- `RunGameCatalogGameplayTests.ps1`: 251,685 checks, 250 differential rounds and 100 seeded golden matches, plus catalog refresh/fallback/session controls and explicit scoring fixtures.
- `RunGameCatalogTests.ps1`: M1 regression, 42 checks.
- Existing domain, configuration, scoring, Guest Auth, Player Foundation/realtime and H1-H6 suites rerun.
- Backend `test build --rerun-tasks`: 348 tests, 346 passed, two optional integrations skipped.

Editor validation entry points in `GameCatalogGameplayValidation` run only in an isolated `Validation/Generated` project: `RunRemote`, `RunCache`, `RunBundled`. They reuse an existing Firebase identity and do not create a guest. Remote validation reads the real local catalog API; a test-only transport fixture checks v1/v2 freezing and then restores the real v1 cache. No catalog documents are written. Cache validation starts a new Unity process with backend stopped; bundled validation starts another process after removing only the harness cache. Both start and deal a playable match with the legacy serialized reference cleared in memory.

Evidence is under ignored `client/Validation/Generated/M2Evidence`. The ordinary user Editor is not restarted or modified. Physical device validation remains NOT_RUN.

No 1v1, online match authority, history, replay, M3 or H7 implementation. No commit or push in M2 implementation.

## Additional validation finding

Portrait passed 139,402 checks, including EN/ES and all four local seats. The supplementary older Landscape `RunModes` validator failed `Card inside safe area`. The same failure reproduced after restoring the HEAD controller, mode definition and smoke test in the isolated project; the menu source itself is unchanged. This separate baseline issue remains documented and is not counted as a passing test. No UI behavior was changed to suppress it.
