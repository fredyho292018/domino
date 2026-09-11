# Domino localization

## System and scope

Audit: there was no localization package, custom translation system or localized prefab text. Visible strings were in the presentation scripts. Installed **Unity Localization 1.5.13**, with official String Tables, EN/ES locales and Smart Strings. Localization brings Addressables and its local asset-loading dependencies. Test Framework 1.4.5 supplies NUnit required by the resolved Addressables editor assembly. No remote catalog, backend, authentication or multiplayer was added.

81 shared keys, each translated into English and Spanish. Player names and TeamFHO branding remain unchanged. Unused system/result keys requested for future UI are present without adding those features.

## Language selection

The official startup selector order is PlayerPrefLocaleSelector (`domino.language`), SystemLocaleSelector, then SpecificLocaleSelector (`en`). English is also the project/fallback locale; Spanish has English fallback metadata.

Settings / Ajustes is available from the start screen and the match menu. Language selection saves PlayerPrefs. Both tables are loaded before the menu is created, so switching does not need a network request. `LocalizedUiText` listens to the official selected-locale event and refreshes on enable; inactive views update when opened. `DominoLocalization` is a small presentation adapter over the official tables and Smart String formatter, not a second translation database. It retains parameter bindings for prompts, counts, results, scores and buttons.

Changing locale does not create a new engine or session, modify the configuration, restart a hand, deselect tiles, or change scores or turn ownership. Internal mode/ruleset identifiers remain stable. GameModeDefinition stores display keys rather than translated names.

## Translation workflow

- Authoring source: `DominoGame/Assets/_Domino/Editor/Localization/Translations.json` (excluded from player builds by the Editor folder).
- Import with **Domino → Localization → Import translations**. The idempotent editor command updates official tables in `Assets/_Domino/Localization/Tables`; it does not run on ordinary startup.
- Runtime uses the generated official tables. Commit their `.asset` and `.meta` files, the locale/settings assets, AddressableAssetsData configuration and EditorBuildSettings registration together.
- Use `UiKit.LLabel`, `UiKit.LButton` and `DominoLocalization.Set(text, key, arguments)` for new presentation strings. Use `Bind` when a result combines several localized phrases. Do not compare translated text to decide gameplay.
- Smart Strings handle `Target {0}` / `Meta {0}` and `{0:plural:tile|tiles}` / `{0:plural:ficha|fichas}`.
- A missing Spanish entry falls back to its English table entry. A key absent from both tables produces a development warning and the localized Unavailable message, never the raw key in UI.

## Validation

- Unity compilation: PASS; three pre-existing serialized-field warnings in the standalone compiler.
- `Phase1Validation.RunLocalization`: **988 checks PASS**, final `localization-final.log`, zero console errors.
- EN/ES catalogs complete: 81/81 each, zero missing translations. Every entry formatted with Smart Strings.
- EN → ES → EN in a live hand: engine, session, configuration, turn, chain, hand count and selected tile preserved.
- Menu, selector, HUD, round-result overlay and next-round action refreshed. Result-overlay testing uses presentation fixtures without mutating domain state.
- Singular/plural, target score, fallback to English, saved-preference selector, and all requested Spanish glyphs passed. Font remains Unity's dynamic LegacyRuntime.ttf, with no extra font download.
- Actual Game View resolutions: 1600×900, 1950×900, 2000×900, 2100×900, 1200×900. Spanish menu, selector, settings, HUD and result controls checked for clipping/safe bounds. Captures: `Validation/Generated/localization-*.png`. Physical device builds have not been tested.
- Localized button labels use a single-line-height area and bounded font sizing so “Siguiente ronda →” fits.
- Domain tests: 80 configuration checks plus 2,981,089 checks across 1,000 games. Regression trace unchanged: `EFADD088F8AB7A9E11CC109A322267C5B7800F7F85BB2D64B6F8F30F95998D52`.
- Full-match regression with localization enabled: PASS, three rounds, score 0–233, target 200, zero console errors (`Validation/Generated/localization-match.log`). This exercises real round summaries, passes, score updates and next-round navigation in addition to the focused presentation tests.

During development the isolated tests detected and helped fix an eager evaluation of individual-score indices in team mode. Tests also now wait for official asynchronous locale callbacks and use the normal celebration speed when switching result text. Final localization tests have no console errors.

No commit or push. Earlier game-mode work remains in the working tree.
