# Match platform M4

M4 is a persistence and reconstruction foundation. The existing Unity offline engine does not call it and remains independent of network availability. No gameplay, monetization, G3 Redis or WebSocket code is changed. The two user-owned enabled AdsSettings/ApiSettings assets remain untouched.

## Authority and domain

`MatchPersistenceService.create` resolves `GameCatalogService`, selects an active mode from the published graph, validates generic participants/teams/human counts, and freezes the selected mode. There is no HTTP creation or transition endpoint, and no client-supplied rule JSON. Callers are trusted server code; future I1 must supply authenticated roster resolution and confirmed engine transitions. LOCAL is supported; ONLINE is modeled but creation rejected until an online engine exists. Remote humans are likewise modeled but not executed.

`Match`, `MatchParticipant`, `MatchRound`, `MatchResult`, and `PlayerMatchHistory` are typed persistence values. Duel has two individual owners; partners has two team owners and can include bots without fabricated Firebase UIDs. Participant names are historical snapshots. A participant with a UID gets history; bots with null UIDs do not. No token or credential field exists. Domain status WAITING_FOR_PLAYERS is deferred to a lobby/matchmaking phase.

`MatchRuleSnapshot` retains catalog/mode/topology/rule/schema versions, engine semantics `domino-m3-v1`, a canonical immutable `effectiveModeJson` string, and its SHA-256. This JSON contains the exact effective topology, complete rules, turn policy, and disconnect policy. Typed views decode detached copies. It is not re-resolved against a later catalog. Snapshot replacement is forbidden by the transaction boundary even before IN_PROGRESS, a stricter invariant than required. `Match` duplicates identifying fields for querying; the full rule value is nested under `ruleSnapshot`.

Winner/finish reason are stored under `Match.result`; score owners are typed PLAYER/TEAM with an index. Round outcomes retain NORMAL/CAPICUA/BLOCKED, starter, winner, recipient, award and remaining pips. BLOCKED is displayed as **Tranque**. M4 records confirmed scoring results; it does not become a second Domino rules engine. The M3 formulas remain opponent +10, opponent *2 +10 for Capicúa, and opponent only for Tranque. Future I1 must validate play legality, scoring, private deals and starter selection before calling persistence. M4 validates structural/lifecycle consistency, not whether a reported local game truly occurred.

## Events and atomicity

One confirmed domain event is one document. `MatchPayload` is a sealed versioned union with an explicit `kind` discriminator; envelope type is derived, not chosen independently. Current events include match/round/turn lifecycle, tile played, pass, hand dealt and private starter results. Animations, heartbeat, ping, presence and spectator joins are not permanent match events.

`MatchRepository.transact` reads the root and current round, checks expected lastSequence, and atomically writes the new root, sequence, event, round, optional history projections and command receipt. Events receive `lastSequence+1` in the transaction. A failed transition makes no writes. Concurrent different commands at the same expected sequence cannot both commit. Retrying the same command returns the originally committed Match receipt; it never appends twice. Command IDs are internal opaque identifiers, with no tokens/PII. A future command gateway should also bind a command ID to its authenticated actor/request digest; M4 exposes no network command API.

Persistence records are append-only/detached at repository boundaries; mutating a returned object cannot rewrite stored events. Do not treat a Kotlin `List` reference handed to arbitrary code as a database write capability. Firestore transaction callbacks must remain deterministic and side-effect free because Firestore may retry them. Domain time is captured by the service's server Clock before the retry loop, validated monotonic against the previous event.

Canonical paths:

| Data | Path / ID |
|---|---|
| Match | `matches/{server-generated UUID}` |
| Participant | `matches/{matchId}/players/{decimal seatIndex}` |
| Round | `matches/{matchId}/rounds/{decimal roundNumber}` |
| Event | `matches/{matchId}/events/{12-digit zero-padded sequence}` |
| Command receipt | `matches/{matchId}/commands/{commandId}` |
| History | `players/{uid}/matchHistory/{matchId}` |

Event IDs sort lexicographically in numeric sequence order. Repository pagination uses `sequence > afterSequence`, ascending, max 500. Keep pages in order; reconstruction requires a complete trusted prefix. Filtered public streams intentionally have gaps where private events exist and cannot be used to reconstruct private hands. A future reconnect consumer must use the appropriate authorized cursor/stream protocol rather than demand contiguous public-only event numbers.

History pagination uses a stable document-ID ascending cursor (Firestore built-in document order) (max 100, default 20). It is bounded and avoids offset scans; **it is not chronological sorting**. A later recent-history UI should add finishedAt + document-ID keyset pagination and the associated Firestore composite index. No full event stream, hand, token, or UID roster is returned by the history projection.

Firestore root/players creation is one batch. Each subsequent transition is one transaction. Finishing writes all human history entries in that same transaction. No writes target wallet, ledger, rewards, catalog, or global Player profile documents. History subcollections for synthetic validation identities do not create Firebase users or Player profiles.

## Authenticated API

`GET /api/v1/players/me/matches?limit=20&cursor=<matchId>` returns `HistoryPage`. UID comes only from `FirebaseIdentity` supplied by the existing authentication filter. A query parameter named uid has no authority. Invalid pagination returns a safe 400; unavailable storage returns a safe 503. No Premium gating is applied.

Match detail, events, creation, spectator and transition HTTP/WebSocket endpoints are deliberately absent. `readTrustedEvents` is an internal repository primitive retaining private payloads; never wire it directly to a public controller. The existing global exception handler may return INTERNAL_ERROR for an absent route; M4 does not alter unrelated exception routing.

No Firestore security-rules source is tracked in this repository. Nothing here grants Unity direct Match access. Before exposing client Firestore access, independently verify deployed rules deny direct Match/round/player/event/command writes and private reads. Future authority writes must use Spring Admin identity. This phase tests service/API boundaries, not deployed Firebase client security rules.

## Public, private, and delayed views

`HAND_DEALT` and private starter audit data require PLAYER_PRIVATE visibility and a valid target seat. `HandDealt.seat` must match that target. A seat can receive its initial hand only once per round, before turns start. Both current hands and reserved hidden tiles must never appear in PublicMatchSnapshot. The public snapshot contains board, counts, turn, scores and public participant metadata, with no UIDs or private setup.

`MatchReplayReducer` reconstructs an initial CREATED state from frozen metadata and ordered events; it does not seed historical views from the Match's latest score/status/current turn. Private deals are maintained separately. `MatchViewService.player` resolves a seat from an authenticated UID; callers cannot choose someone else's seat. No finished-replay permission broadening is implicit.

`MatchViewService.spectator` is a pure server-side foundation, not a delivery scheduler. It validates PUBLIC/enabled policy, selected seat and delay, then computes `cutoff = serverClock - delaySeconds`. It invokes one reducer for the public state and selected hand, returning the same sequence in both. Events after the cutoff cannot contribute to either. TABLE_ONLY returns no hand. FOLLOW_PLAYER requires a seat and SELECTED_PLAYER_DELAYED permission. The client is never sent a current private hand to hide locally for 90 seconds.

Default PUBLIC policy is enabled, SELECTED_PLAYER_DELAYED, 90 seconds. PRIVATE/FRIENDS_ONLY defaults disabled; no friends system exists. Minimum hand delay is configured server-side by `domino.match.min-hand-spectator-delay-seconds`, with `MIN_HAND_SPECTATOR_DELAY_SECONDS` as its fallback environment setting (default 60). Values below 60 cannot weaken the hard floor; an increased minimum also raises the effective default if necessary. Hand view with zero or 30 seconds is rejected. TABLE_ONLY can be designed separately with nonnegative delay; no runtime policy mutation endpoint exists.

No selected hand is ever merged from a live cache into a delayed board. Tests explicitly exercise cutoff 910: event909 included, event910 included, event911 excluded, including hand removal, scores and status. Complete trusted event streams are required; delivery batching/materialized snapshots at scale remain future work.

## Future runtime contracts

Reserved typed event payloads: TURN_TIMEOUT, AUTO_PLAYED (seat/tile/end/reason TURN_TIMEOUT or DISCONNECTED), PLAYER_DISCONNECTED, PLAYER_RECONNECTED, PLAYER_ABANDONED. M4 rejects generating them. Turn policy60s and reconnect180s are stored in frozen rules only. No timers, autoplay, takeover, abandonment consequence or online reconnect executes.

Future discovery can read Match status/mode/participants/score/startedAt/visibility/policy. Redis would own ephemeral subscriber counts, spectator connections, presence and delivery scheduling; no spectatorCount is persisted as Match authority. Future contracts are JOIN_AS_SPECTATOR, SELECT_SPECTATOR_SEAT and MATCH_SPECTATOR_SNAPSHOT, with server authorization and delayed projection before transport. They are not handlers in M4.

Replay will load the stored semantics/rules, then ordered events, then deterministic reducers. The current reducer covers board, turn, scores, counts and selected private hands; there is no visual replay. Version1 rejects unknown payload/schema/semantics instead of guessing. A future migration/reducer registry must preserve old semantics when engine versions evolve.

Retention must later be configurable separately for match events, private events, history and command receipts. No deletion jobs or assumption of permanent retention is implemented. Future FREE/PREMIUM visibility and analysis entitlements may differ; data capture currently applies to every human participant and is not paywalled.

## Validation

`MatchFoundationTests` covers both topologies, frozen rules, participants, concurrency/idempotency, rollback on failed transitions, rounds, Capicúa/Tranque audit, finish/history, privacy, replay and cutoff security. `MatchHistoryHttpTests` exercises the existing Firebase filter and authenticated history boundary.

`./gradlew validateMatchM4Firestore` is opt-in only. It uses existing ADC and the real published catalog to create one `m4-validation-<UUID>` synthetic match, two synthetic participants, rounds, events and two history projections. Every root/history is marked `validationData=true`; data is intentionally retained for inspection and must not count as a real game in future discovery/analytics. It does not create Firebase guests or credit wallets. Output gives the exact synthetic match ID. Ordinary unit tests do not contact Firestore.

No commit or push in M4 implementation. See the M4 report for actual completed checks and the real validation record.
