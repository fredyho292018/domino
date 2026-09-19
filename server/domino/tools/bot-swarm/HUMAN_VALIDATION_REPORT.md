# I3.1 HUMAN VALIDATION REPORT

Date: 2026-09-19. Source: `9522bb921585fc3f709fded3ac4aa3cc8df86354` plus existing pending M5/I3.1 work.
Real Unity 6000.0.41f1 Editor, user-operated normal mode selector and matchmaking. No manual match ID, forced seats, engine bots, or repeated load matrix.

```text
HUMAN_VS_SWARM_DUEL=PASS
DUEL_REAL_UNITY_CLIENT=YES
DUEL_SWARM_OPPONENT=YES
DUEL_MATCHMAKING=PASS
DUEL_ROUND_COMPLETED=PASS
DUEL_PRIVATE_HAND=PASS
DUEL_OPPONENT_HAND_LEAK=NO

HUMAN_PLUS_3_SWARM=PASS
PARTNERS_REAL_UNITY_CLIENTS=1
PARTNERS_SWARM_CLIENTS=3
FOUR_DISTINCT_UIDS=PASS
REMOTE_HUMANS=4
MATCH_ENGINE_BOTS=0
TEAMS_0_2_VS_1_3=PASS
TURN_ORDER_0_3_2_1=PASS
LOCAL_VIEW_ROTATION=PASS
PARTNER_HAND_LEAK=NO
OPPONENT_HAND_LEAK=NO
PARTNERS_ROUND_COMPLETED=PASS
TEAM_SCORE=PASS

F0_IDLE_FIRESTORE=PASS (unchanged scheduling path; see measurement scope below)
F01_UNCHANGED_PARTICIPANT_WRITES=0 (prior measured baseline; unchanged guarded write path)
RESOURCE_EXHAUSTED=NO
SWARM_MONETIZATION_ACTIVITY=0
CONSOLE_ERRORS=0

COMMIT=NONE
PUSH=NONE
DEPLOY=NONE
```

## Direct human evidence

DUEL match `2afc77ac-1e54-4f67-a056-1bb86728e0c8`: one simulated client only, avoiding a second simulated-only match. Human Guest-SH3PH3VJ received authoritative seat 1, Tomas seat 0; human was displayed at bottom. Normal matchmaking, starter, tile selection/play, animations, countdown/turn changes and hidden opponent hand were observed. First round completed BLOCKED: remaining pips `[28,11]`, winner seat 1, award 28, score `[0,28]`. User explicitly confirmed seeing the round summary. Server inspection verified two distinct identities, two REMOTE_HUMAN seats, one server-side swarm marker. Swarm ended with 16 commands, one completed round, no command rejection/fatal error and zero monetization activity.

PARTNERS match `620ae336-ea1f-43ba-92e3-99bf564e19ae`: three simulated clients were observed SEARCHING with zero matches before human entry; all three then received this single match. Frozen rules are `double-nine-partners` v1, target 200, turn order `[0,3,2,1]`. Authoritative participants: seat 0 human/team 0; seat 1 Gabriel/team 1; seat 2 Laura/team 0; seat 3 Tomas/team 1. Human bottom, partner Laura top, Gabriel left, Tomas right. No seat override was used. This 2v2 run randomly assigned seat 0; it does not newly exercise every nonzero 2v2 viewpoint. The duel did exercise a nonzero local seat.

First partners round completed BLOCKED, pips `[12,30,16,12]`: opposing minima tied at 12, no winner, award 0, score `[0,0]`. Unity displayed the draw summary, matching aggregates, and next-round x2 indicator. The pass hand animation, chain growth, selection/play feedback, team roles and hidden partner/opponents were observed. User explicitly confirmed the draw and private hands. Three clients ended with 30 commands, one completed round, no command rejections/fatal errors, zero monetization activity. Inspection verified four distinct identities, four REMOTE_HUMAN seats and three swarm markers.

The visible role beneath Tomas is **Rival**, not Bot. An initial low-resolution visual misreading was corrected against subsequent observation and `PortraitBoardPresentation.cs`. Normal aliases are used; internal swarm markers/slot numbers are not shown. Avatar initials still come from the existing presentation, not necessarily the remote alias.

## Safety, scope and limitations

- Two functional human tables only. No 10/20-client rerun, no load test. Three stable Firebase test slots provisioned/reused; user identity unchanged.
- Real Firebase authentication, WebSocket and authoritative backend were used. A read-only extension to `InspectMatch.kt` reads the known first-round document and emits safe score/topology evidence, without hands, UIDs or tokens. Its first partners invocation hit a diagnostic JSON mapping error; correcting `.toList().map` completed inspection successfully. No gameplay/backend product change.
- No simulator reward endpoint is allowlisted; both runtime summaries report monetizationActivity=0. No ad was requested as part of this validation. User AdsSettings/ApiSettings files preserved byte-for-byte.
- F0 due discovery remains Redis-based; the Firestore `due()` query has no worker caller. The scheduler sources are unchanged. F0.1 still compares full participant documents inside the authoritative transaction before writing. No additional production metrics were installed: **this human run does not provide a new measured Firestore operation/write count**. The prior 88-second zero-idle-operations measurement and zero unchanged-participant-writes remain the quantitative evidence. Normal presence/abandonment recovery for unfinished matches can still perform document reads/writes; do not interpret the absence of due polling as zero total Firestore traffic.
- Both requested first rounds completed; full matches were intentionally not required and remain retained IN_PROGRESS. The duel advanced into round 2 because the seat-0 swarm normally requests NEXT_ROUND. Leaving the Unity table does not disconnect its general realtime session; Stop Play plus the normal 180-second reconnect expiry was used to free the human assignment before 2v2. No data deletion, UID substitution or forced completion.
- Unity console displayed zero errors (one warning). Backend showed repeated AUTH_TOKEN_MISSING rejections unrelated to authenticated match traffic; no authentication weakening applied. No RESOURCE_EXHAUSTED appeared.
- Existing automatic evidence (not rerun): backend 469 PASS, swarm 25 PASS and the previously documented multi-mode matrix.
- Cleanup completed: both swarms stopped via their local cancellation signals, user stopped Play Mode, owned backend PID 10648 and dedicated Redis on 16380 stopped. Normal user Redis was not stopped. No files staged.

## Local evidence

Ignored logs under `server/domino/build/`: `i31-human-backend.log`, `i31-human-provision.log`, `i31-human-duel.log`, `i31-human-duel-inspection.log`, `i31-human-partners.log`, `i31-human-partners-inspection.log`. UI evidence was inspected live in the task; user confirmations are recorded in conversation. Runtime logs/credentials are not staged.

Preserved SHA256:

- AdsSettings.asset: `C377416E334727264806761518A4B5EDF381A837A927A1C3F3D523A1ACBDB5D6`
- ApiSettings.asset: `AC6B0ED6D30240BC531B235E43E8B9B55D3B48DCBCCCF365ADEC62D3EE6BCB37`
