# Team scoring correction

Source SHA: `5125ee3246a38932272d09facf4ff008dc04251f`. Existing menu/localization changes remain uncommitted and are preserved.

## Production change

Only the two scoring source fields in `Assets/_Domino/Config/double-nine-partners-v1.json` change: `ALL_OTHER_PLAYERS` → `OPPONENTS_ONLY`. This is the existing policy equivalent to OPPONENT_TEAM_ONLY: `RoundScoring` compares `rules.Side(seat)` against the winner's side, obtained from TeamAssignments. No new enum or duplicated policy was needed.

Finish: `(opponent pips + 10) × multiplier`. Blocked: `(opponent pips + 0) × multiplier`. Determining the blocked winner remains the lowest individual hand; cross-team minimum ties keep the existing zero award and next-round multiplier policy. Teams remain [0,2] vs [1,3].

No changes to RoundScoring, MatchState, GameRules, ClientGame, bot decisions, turn order, deal, target score, ruleset identifiers, or UI rendering. The existing result view receives BasePoints/Bonus/Award from the domain. Old generic policies remain supported by the engine for compatibility, but neither shipped 2v2 scoring policy selects ALL_OTHER_PLAYERS.

## Required cases

| Case | Expected base | Bonus | Multiplier | Award |
|---|---:|---:|---:|---:|
| Fredy/seat 0 finishes; Alex 24, Maria 18, John 31 | 55 | 10 | 1 | 65 |
| Maria/seat 2 finishes; Fredy 12, Alex 14, John 25 | 39 | 10 | 1 | 49 |
| Alex/seat 1 finishes; Fredy 17, Maria 29, John 20 | 46 | 10 | 1 | 56 |
| John/seat 3 finishes; Fredy 11, Alex 15, Maria 22 | 33 | 10 | 1 | 43 |
| Finish bonus case | 55 | 10 | 1 | 65 |
| Multiplier case | 55 | 10 | 2 | 130 |
| Blocked Team A: [14,25,21,30] | 55 | 0 | 1 | 55 |
| Blocked Team B: [17,12,26,18] | 43 | 0 | 1 | 43 |

All eight cases PASS. Additional tests increase the winning partner's points without affecting the award, permute team assignments, separate finish/blocked bonuses, verify ties/multipliers and target termination.

## Regression evidence

- Configuration: 82 checks PASS.
- Dedicated scoring: 32,679 checks PASS, including 250 seeded rounds run side-by-side with the old policy. Hands, reserve, turns, valid plays, passes, blocked detection and winners match. For non-ties, the base-score difference equals exactly the winning partner's remaining points.
- Domain/geometry: 2,981,089 checks / 1,000 games PASS.
- The historical complete-match trace is retained under its historical ALL_OTHER_PLAYERS test configuration: `EFADD088F8AB7A9E11CC109A322267C5B7800F7F85BB2D64B6F8F30F95998D52`.
- The new default configuration intentionally changes payouts and match lengths. Its complete trace is `CF8D7610D54AF6C74C3A6F1A20A3FAE0881C2FAF8A46F6E78EC0273893DB26EC`; it is not presented as identical to the old scoring trace.
- Static compilation passed with the three existing serialized-field warnings.
- The Play Mode smoke now independently checks opponents-only scoring for each actual completed round and confirms the visible result contains the domain base, bonus, multiplier and award.
- Unity 6000.0.41f1 Play Mode: SUCCESS, 11 rounds, final score 173–217, target 200, console errors 0. Log: `Validation/Generated/team-scoring-unity.log`; result: `Validation/Generated/phase1-unity-result.txt`. Includes an initial tied block, subsequent payout/reset of multiplier and both winning teams across rounds.

No commit or push.
