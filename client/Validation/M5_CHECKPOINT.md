# M5 — PARTNERS_2V2_ONLINE checkpoint

Validated 2026-09-19 on main, based on F0.1 `9522bb921585fc3f709fded3ac4aa3cc8df86354`. F0 `8e3a174b31683b17fa0600eb2c3451a3fee16528` and F0.1 remain ancestors.

## Scope

Four remote humans, no engine bots; teams [0,2] versus [1,3], turn order [0,3,2,1], target 200. Catalog v4 adds the online mode/binding and reuses the unchanged `double-nine-partners` v1. Includes the normal scrollable EN/ES selector, four-player atomic matchmaking, private projections and existing partners presentation.

This commit excludes I3.1 implementation, its Gradle subproject, emulator tools, account markers, history test-marker propagation and swarm profile tests. The shared profile projection contains only the server-owned display name. These exclusions were made in the Git index without modifying the original working files. AdsSettings.asset, ApiSettings.asset and unrelated Localization Settings serialization-ID changes remain unstaged.

## Validation

```text
PARTNERS_2V2_ONLINE=PASS
REMOTE_HUMANS=4
MATCH_ENGINE_BOTS=0
RULESET_ID=double-nine-partners
RULESET_VERSION=1
SAME_RULESET_AS_LOCAL_PARTNERS=YES
TEAMS=[0,2] vs [1,3]
TURN_ORDER=[0,3,2,1]
TARGET_SCORE=200
HUMAN_PLUS_3_SWARM=PASS
FOUR_DISTINCT_UIDS=PASS
PARTNER_HAND_LEAK=NO
OPPONENT_HAND_LEAK=NO
LOCAL_VIEW_ROTATION=PASS
REAL_ROUND=PASS
PARTNERS_2V2_LOCAL_REGRESSION=PASS
DUEL_1V1_REGRESSION=PASS
I3_REGRESSION=PASS
F0=PASS
F0_1=PASS
CONSOLE_ERRORS=0
```

The preceding user-operated Unity validation completed round 1 of match `620ae336-ea1f-43ba-92e3-99bf564e19ae`: authoritative seats 0 human, 1 Gabriel, 2 Laura, 3 Tomas. Remaining pips [12,30,16,12], opposing minima 12–12, award 0, score 0–0, next multiplier x2. The user confirmed the visible summary and hidden partner/opponent hands. Human was displayed at bottom, partner above. The real 2v2 assignment was seat 0; prior visual tests cover all four seats, while the preceding real duel exercised local seat 1. This previously performed real test used the pending swarm as an external validation client; its implementation is not included here. No new real Firestore calls or catalog publication were performed during checkpoint closure.

An exact export of the staged source was compiled and tested independently, without the unstaged swarm:

- Backend: **466 PASS, 0 failures, 0 skipped**, Redis tests and local/server differential parity enabled; normal Gradle test pins Firestore to the unreachable emulator endpoint. Includes existing F0/F0.1, DUEL and I3 regressions. The prior combined 469 count included three excluded I3.1 profile-marker tests.
- Local/server partners differential: existing fixtures for 100 complete matches, 29,304 trace steps; no network gameplay/load test.
- Online C# client: **48 checks PASS**.
- Matchmaking C# client: **75 checks PASS**.
- Unity compilation/Play Mode/console: preceding real human session PASS, zero errors. Product Unity source is identical to that session; no further Unity run required for selective staging.
- F0 scheduling files remain unchanged; F0.1 participant writes still compare full documents inside the transaction. No new runtime cost counters are claimed.

No production deployment, backend startup, swarm startup or real Firestore validation during this closure. Original pending-file SHA256 hashes were checked for preservation.

## Files included

- `client/DominoGame/Assets/_Domino/Editor/Localization/Translations.json`
- `client/DominoGame/Assets/_Domino/Localization/Tables/Domino UI Shared Data.asset`
- `client/DominoGame/Assets/_Domino/Localization/Tables/Domino UI_en.asset`
- `client/DominoGame/Assets/_Domino/Localization/Tables/Domino UI_es.asset`
- `client/DominoGame/Assets/_Domino/Scripts/Catalog/GameCatalogCodec.cs`
- `client/DominoGame/Assets/_Domino/Scripts/Catalog/GameCatalogSnapshot.cs`
- `client/DominoGame/Assets/_Domino/Scripts/Client/DominoClientController.cs`
- `client/DominoGame/Assets/_Domino/Scripts/Client/GameModeDefinition.cs`
- `client/DominoGame/Assets/_Domino/Scripts/Online/Editor/PartnersViewValidation.cs`
- `client/DominoGame/Assets/_Domino/Scripts/Online/Editor/PartnersViewValidation.cs.meta`
- `client/DominoGame/Assets/_Domino/Scripts/Online/MatchmakingClient.cs`
- `client/DominoGame/Assets/_Domino/Scripts/Online/MatchmakingView.cs`
- `client/DominoGame/Assets/_Domino/Scripts/Online/OnlineMatchClient.cs`
- `client/DominoGame/Assets/_Domino/Scripts/Online/OnlineMatchController.cs`
- `client/DominoGame/Assets/_Domino/Scripts/UI/OnlineBoardPresentation.cs`
- `client/DominoGame/Assets/_Domino/Scripts/UI/StartMenuView.cs`
- `client/Validation/M5_BLOCKER_REPORT.md`
- `client/Validation/M5_CHECKPOINT.md`
- `client/Validation/M5NetworkValidation.cs`
- `client/Validation/M5ParityFixtures.cs`
- `client/Validation/MatchmakingClientTests.cs`
- `client/Validation/OnlineMatchClientTests.cs`
- `client/Validation/PARTNERS_ONLINE_M5_REPORT.md`
- `client/Validation/RunM5ParityFixtures.ps1`
- `server/domino/build.gradle.kts`
- `server/domino/src/main/kotlin/com/teamfho/domino/catalog/GameCatalogV4Publisher.kt`
- `server/domino/src/main/kotlin/com/teamfho/domino/catalog/GameCatalogValidator.kt`
- `server/domino/src/main/kotlin/com/teamfho/domino/matchmaking/MatchmakingModels.kt`
- `server/domino/src/main/kotlin/com/teamfho/domino/matchmaking/MatchmakingService.kt`
- `server/domino/src/main/kotlin/com/teamfho/domino/matchmaking/RedisMatchmakingStore.kt`
- `server/domino/src/main/kotlin/com/teamfho/domino/online/OnlineConfiguration.kt`
- `server/domino/src/main/kotlin/com/teamfho/domino/online/OnlineEngine.kt`
- `server/domino/src/main/kotlin/com/teamfho/domino/online/OnlineMatchService.kt`
- `server/domino/src/main/kotlin/com/teamfho/domino/online/OnlineModels.kt`
- `server/domino/src/main/kotlin/com/teamfho/domino/online/OnlineParticipantProfile.kt`
- `server/domino/src/main/kotlin/com/teamfho/domino/online/OnlineRepository.kt`
- `server/domino/src/test/kotlin/com/teamfho/domino/matchmaking/PartnersMatchmakingTests.kt`
- `server/domino/src/test/kotlin/com/teamfho/domino/online/OnlineHttpTests.kt`
- `server/domino/src/test/kotlin/com/teamfho/domino/online/PartnersFixtureExporter.kt`
- `server/domino/src/test/kotlin/com/teamfho/domino/online/PartnersOnlineTests.kt`
- `server/domino/src/test/kotlin/com/teamfho/domino/online/PartnersRealInspection.kt`
