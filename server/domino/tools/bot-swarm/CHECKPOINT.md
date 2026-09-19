# I3.1 Bot Swarm checkpoint

Base M5: `998d237f781b2aa668a64fed207c7e045a97465b`, branch `main`.
Commit subject: `feat: add multi-mode online bot swarm`.

## Verification scope

The closure Gradle invocation succeeded: backend XML results contain 469 tests, zero failures/errors/skips; swarm tests contain 25 tests, zero failures/errors/skips. Backend results were up-to-date, while swarm tests executed again. Real Firestore opt-in was explicitly false. No swarm, real backend validation, cost measurement or deployment was started during closure.

Prior acceptance evidence is preserved in [F01_RESUMED_REPORT.md](F01_RESUMED_REPORT.md) and [HUMAN_VALIDATION_REPORT.md](HUMAN_VALIDATION_REPORT.md). The load matrix and real Unity sessions were not repeated:

| Mode / clients | Completed matches | Maximum simultaneous |
|---|---:|---:|
| DUEL / 10 | 14 | 5 |
| DUEL / 20 | 28 | 10 |
| PARTNERS / 4 | 1 | 1 |
| PARTNERS / 10 | 5 | 2 |
| PARTNERS / 20 | 12 | 5 |

Defaults: 10 clients, maximum 20. Supported modes: DUEL_1V1 and PARTNERS_2V2_ONLINE.

Human versus swarm duel passed (blocked round, 28 points). Human plus three swarm clients passed with four distinct identities and zero MatchEngine bots (12–12 tie, score 0–0, next multiplier x2). Partner/opponent hands remained private. Unity validation console errors: zero.

Short reconnect passed. A long interruption can exhaust the configured eight-failure ceiling and stop a client; restarting with the same retained identities recovers the match. This limitation remains, without a reconnect redesign.

The prior bounded real Firestore validation completed one match with two clients and zero quota errors. No real 10/20-client matrix was run. Swarm reward intents, reward coins and economic activity were zero.

F0 and F0.1 remain ancestors. Their idle scheduling and dirty-participant write paths are unchanged; prior measured unchanged participant writes remain zero. No new measurement is claimed.

Server-owned developmentTestAccounts markers propagate validationData into swarm-generated matches/history. Completed validation matches and identity records are retained for future testing; runtime exports and credentials are excluded from Git.

## Commit scope and preserved local work

Only swarm implementation, configuration, tests, documentation and backend validation marker integration belong to this checkpoint. M5 gameplay/topology/rules are unchanged.

AdsSettings.asset and ApiSettings.asset are excluded and preserved. The pre-existing Localization Settings.asset serialization change is also excluded and preserved. These three Unity assets intentionally leave the worktree dirty.

No I4 implementation or deployment is included.
