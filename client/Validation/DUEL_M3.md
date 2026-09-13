# M3 — local two-human duel

Base: `627fd7235108dc949554b8d73d0018a851f94afe`, main. No commit or push.

## Configuration and authority

`GameCatalogConfigurationAdapter` resolves `DUEL_1V1` from the same immutable catalog/snapshot pipeline as PARTNERS_2V2. The bundled v2 graph adds a separate Double Nine Duel v1. The server publication resource is `server/domino/src/main/resources/game-catalog-v2.json`; the v1 publication and partners version are unchanged. M1/M2 regression suites now use a frozen `GameCatalogV1Fixture.json`, preserving their original inputs and golden trace.

Duel has two human controls, no teams (`seatTeams=[]`, `teamSize=null`), no bots, twenty dealt tiles and thirty-five reserved. Scores belong to PLAYER owners; no synthetic teams are created. PARTNERS_2V2 continues to use TEAM owners. No draw, target 150, turn/deal order 0/1.

`StarterSelection` chooses one configured method using a separate random generator. Its candidate tiles never touch the actual deck or shuffle. High Tile compares sums only and repeats on equality. Even/Odd chooses roles, conceals the selector's tile until the guess, and resolves by sum parity. Invalid seats and repeated choices are rejected. The controller waits for completion before calling the engine's resolved-starter overload. Subsequent rounds read the previous winner; no second mini-game.

Capicua checks LAST tile and both legal endpoints before board mutation. A chain must already exist: an empty board has no two distinct endpoints. Equal head values qualify, including a matching double. Award is `opponent pips * 2 + finish bonus`; the bonus is not doubled. The typed result distinguishes NORMAL, CAPICUA and BLOCKED. A blocked tie awards the round to its stored starter.

## Shared device and UI

The catalog-backed selector shows both localized modes and refreshes when the accepted catalog changes. Rollback removes new duel selection; existing sessions retain their frozen configuration.

The duel keeps logical seat 0 below and seat 1 above, with no side seats or partner labels. An opaque handoff screen blocks interaction and all hands are concealed. Continuing reveals only the active seat's hand; logical ownership is still checked by the engine. The active top hand expands for interaction without swapping logical positions. Starter choices and handoff are localized EN/ES. The duel reward CTA is disabled and no duel round is submitted to the reward flow.

## Future online policies: data only

RuleSet owns turn timing: 60 seconds, autoplay enabled, FIRST_VALID_MOVE. GameMode `onlinePolicy` owns disconnect semantics: 180-second reconnection window; turn clock continues; autoplay while disconnected. These fields are immutable snapshots, not executable timers. Execution support remains LOCAL only.

Future FIRST_VALID_MOVE ordering is current stable hand order, first legally playable tile, RIGHT end preferred when both are legal, otherwise LEFT. No legal tile means PASS. A future server must compute the deadline once at turn start; disconnect at t=20 does not change a t=60 deadline. Unity does not choose authoritative online actions or clocks. Connection states and future event names are modeled but never emitted/executed by M3. No abandonment outcome, coin penalty, history, replay or socket gameplay.

## Turn/disconnect addendum

Exact serialized locations: `ruleSets/double-nine-duel/versions/1.turnPolicy` and `gameModes/duel-1v1.onlinePolicy`. The resolved catalog includes them under `modes[].ruleSet.turnPolicy` and `modes[].onlinePolicy`. The latter is the chosen name for the GameMode disconnect/execution policy; it is not part of scoring. Future online support is still disabled (`executionModesSupported=[LOCAL]`).

The two clocks are independent. The future authoritative server creates the turn deadline at turn start. Disconnect at t=20 leaves the t=60 deadline intact; reconnect at t=45 restores current control with approximately 15 seconds remaining. The reconnect window starts at disconnect, so abandonment becomes eligible at t=200 in this example, not at t=180 after turn start. At a disconnected participant's later turn, the ordinary 60-second deadline applies; the match never waits for the entire reconnect window before progressing. Autoplay happens at that turn's deadline, not immediately on disconnect.

Autoplay is one action and never transfers ownership to a permanent bot. Reconnect synchronizes current authoritative state and never rewinds an autoplay or creates a fresh turn/deadline. Duration >=180 seconds since disconnect permits the future participant state ABANDONED; no automatic loss, rival points, wallet/ledger mutation, ranking penalty, suspension or permanent replacement is decided here.

`ParticipantConnectionState` denotes a participant inside a match (CONNECTED, DISCONNECTED, RECONNECTED, ABANDONED), independently of G3's application WebSocket state. Future events are reserved enum names only: TURN_STARTED, TURN_TIMEOUT, AUTO_PLAYED, PLAYER_DISCONNECTED, PLAYER_RECONNECTED, PLAYER_ABANDONED. A later AUTO_PLAYED payload should identify seat, turn, selected tile, board side and reason TURN_TIMEOUT or PLAYER_DISCONNECTED. A later sync contract should provide MatchSnapshot plus events after lastSequence. Neither a persistent event schema nor reconnect transport is implemented here.

Conceptual future telemetry: timeoutCount, autoPlayCount, disconnectCount, reconnectCount, abandonCount. No counters are collected/persisted by M3. These semantics are design requirements for the future Match Engine, not runtime behavior claimed by these tests.

## Publication procedure

`publishGameCatalogV2` is an explicitly invoked administrative Gradle task using existing ADC. It validates and compares all immutable documents before any write, creates only missing documents, and changes the pointer from known v1/v2 to v2 in one transaction. A historical conflict aborts the transaction. Its rollback verification validates publication 1, switches 2→1, checks the repository, and restores 2 in a finally block with a compare-and-set guard. It never mutates catalog v1 or monetization documents and never runs at application startup.

The final approved blocked-round formula is **opponent remaining pips**, with zero bonus and no multiplier. The wire value is `blockedScoring.source=OPPONENTS_ONLY`, `bonus=0`; with two individual score owners this means exactly the other player's hand. The four mandatory examples (18/27, 27/18, and 24/24 with either starter) are tested both in scoring and through two legitimate engine passes. Normal finish remains opponent pips +10; Capicua remains opponent pips *2 +10. `DOUBLE_NINE_DUEL` v1 contains these final rules.

Real publication completed on 2026-09-13 with existing ADC: v2 publication and repeat publication passed, historical catalog v1 remained unchanged, and the real pointer was switched 2→1→2 and restored to 2. Only catalog documents/pointer were written; no wallet, monetization, match or identity writes were performed by the publisher.

## Validation

Run `RunDuelGameplayTests.ps1`, `RunGameCatalogGameplayTests.ps1`, M1 regression and the existing client suites. Backend `test build` includes `DuelCatalogTests`. `DuelPlayModeValidation.Run` is an opt-in isolated Editor test with no catalog network calls. It exercises both human controls, all nine Portrait resolutions, privacy, twenty dealt/thirty-five reserved, a complete round and reward CTA suppression.

`RunRemote`, `RunCache` and `RunBundled` additionally exercise separate isolated Editor processes against the authenticated local backend, a persisted test cache with that backend stopped, and the bundled fallback. They reuse an existing Firebase guest and forbid guest creation. The known Landscape menu failure from M1 remains outside this task. See `DUEL_M3_REPORT.txt` for completed validation results and limitations.
