# Game Mode Phase 1

Source: `5125ee3246a38932272d09facf4ff008dc04251f`.

The application now opens a main menu. PLAY opens the mode selector; its PLAY action creates an offline team session and the existing board. No game is created, dealt or simulated in the menus.

`GameModeDefinition.TeamMatch` owns the mode identity and participant metadata. `SessionSetup` binds it to the loaded configuration and checks team compatibility. The mode id is `offline-team-match`; the configuration/ruleset id remains `double-nine-partners` (ruleset version 1.0). Rules values displayed on the card come from the validated snapshot.

One existing scene, `Assets/_Domino/Scenes/DominoClient.unity`, hosts the navigation and the reusable match. `StartMenuView` presents the menu and selector; `DominoClientController` owns session lifetime. Exit Match clears the presentation queue, coroutines, board and wash audio, then returns to the selector. Restart preserves the session and starts a fresh match. The existing bot algorithm and delay are unchanged; seat ownership is read from the session.

Local human: seat 0. Bots: seats 1, 2, 3. Teams: 0+2 and 1+3. No new competitive rules, services, scenes, packages or online modes.

## Validation

- Static compilation passed (three existing serialized-field warnings).
- Configuration: 80 checks passed.
- Domain regression: 2,981,089 checks / 1,000 games passed; trace unchanged: `EFADD088F8AB7A9E11CC109A322267C5B7800F7F85BB2D64B6F8F30F95998D52`.
- `Phase1Validation.RunModes`: Play Mode passed, zero console errors. Covers initial menu/no auto-start, selector, back, mode/session participants, restart, exit during deal, audio cleanup, reentry and exit from a playable match.
- Actual Game View sizes: 1600×900, 1950×900, 2000×900, 2100×900 and 1200×900. Card safe bounds and text fit passed. Captures saved in `Validation/Generated/mode-*.png`. This is desktop rendering validation, not physical-device testing.
- Existing dealing, match and device replay runners now explicitly enter through the menu before testing the board.
- Full-match Play Mode regression passed: seven rounds, final score 221–162, target 200, zero console errors. Includes selection, drag, bots, passes, next-round continuity and restart. Log: `Validation/Generated/mode-match-verified-unity.log`.
- The first full-match run exhausted its previous 210-second test budget. The test now allows 360 seconds and logs each completed round; the editor watchdog allows 480 seconds. No gameplay timing was changed. The subsequent run above completed successfully.

No commit or push. Future modes require their own session definitions and capabilities; they are not enabled by this change.
