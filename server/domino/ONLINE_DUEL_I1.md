# Online DUEL I1

I1 adds a server-authoritative Double Nine duel alongside the unchanged local C# engine.
The base is M4 `786073aed2d1c45cab87540dbe8b7ff67b895205`. No catalog publication,
deployment, economic changes, commit or push are part of this implementation.

## Running the explicit development flow

1. Start the existing loopback Redis container and local Spring backend.
2. Open Unity and enter Play Mode using the existing LOCAL API configuration.
3. Wait for Firebase identity and G3 realtime to connect.
4. Open **Domino > Online > Open DUEL development controls**.
5. On the first authenticated client select **Create online DUEL (seat 0)**.
6. Copy its Match ID into the second independent client's window and select **Join Match ID (seat 1)**.
   The Firebase UIDs must differ. A second connection with the same UID cannot become the opponent.
7. Select the starter tiles or submit the requested even/odd guess. Play by selecting a tile and
   choosing an arrow, or dragging onto the required chain end. Pass is an explicit action.
8. After a round, either player can request the next round. The server checks the phase and
   uses the preceding winner as starter. The other simultaneous request cannot deal twice.

The Editor window is an opt-in development launcher, not matchmaking or a lobby. The runtime
controller and API client have no Editor dependency. It reuses the existing BoardView, tile pool,
portrait layout, local-seat mapping, wash/deal, safe area and TeamFHO backs. Online initialization
never constructs ClientGame. Generic server display names are currently Player 1 and Player 2.

## REST contract

All routes require the existing verified Firebase REST principal. UID, seats, winners, points,
rules and hands are never accepted as authority from a request.

| Route | Request | Response |
| --- | --- | --- |
| POST `/api/v1/matches` | `{ "modeKey": "DUEL_1V1" }` | caller snapshot, seat 0 |
| POST `/api/v1/matches/{id}/join` | `{ "commandId": "unique-id" }` | caller snapshot, seat 1 |
| GET `/api/v1/matches/{id}/snapshot` | none | public state + own private state at one sequence |
| GET `/api/v1/matches/{id}/events?afterSequence=N` | nonnegative cursor | up to 500 authorized event positions |

`throughSequence` is the page cursor. Continue from it for additional pages. A private event for
the other player becomes `{sequence, type: "SEQUENCE_ADVANCED", event: null}`. This reveals no
payload, actor, target or command ID and preserves a continuous sequence for gap detection.
Snapshot/event routes reject nonparticipants, including authenticated users in another match.
There is no spectator route. Existing M4 history remains `/api/v1/players/me/matches`.

## WebSocket contract

Use the single existing Firebase-authenticated `/ws/v1/realtime` connection. G3's outer
`type/version/sequence/timestamp/payload` envelope is unchanged. Its sequence belongs to the
connection, separately from the durable Match sequence.

Client `type=MATCH_COMMAND` contains this DTO directly in `payload`:

```json
{
  "protocolVersion": 1,
  "commandId": "a-new-uuid",
  "matchId": "server-issued-id",
  "type": "PLAY_TILE",
  "tile": { "sideA": 9, "sideB": 7 },
  "chainEnd": "RIGHT"
}
```

Other commands are `PASS`, `SELECT_STARTER_TILE` (`candidate: 0|1`),
`SUBMIT_EVEN_ODD_GUESS` (`even: true|false`), and `NEXT_ROUND`. Only fields belonging
to that command are permitted. No AUTO placement, UID or client result fields are accepted.
The strict codec rejects unknown fields, scalar coercion, float-to-int coercion and null primitives.

After durable commit, every connected device of each participant UID on the LOCAL server receives
`MATCH_UPDATE`: Match ID, firstSequence, authorized event positions, and its own snapshot at the
resulting sequence. Outsiders receive nothing. `COMMAND_ACCEPTED` returns commandId, matchId
and resultingSequence; `COMMAND_REJECTED` returns commandId and a safe typed code.
Invalid commands do not change Match sequence. Malformed match DTOs receive a structured rejection.
Outer protocol, auth, size and rate protection remain G3 responsibilities (32 KiB, configured rate).
The Unity reader permits up to 12 JSON container levels for MATCH_UPDATE (the full private deal
uses nine). Every other G3 message retains its previous eight-level bound. The generated server
wire fixture is parsed by the actual Unity protocol code in the client tests (17,136 bytes).

Unity validates each batch as contiguous. A duplicate is ignored; a gap freezes interaction and
requests a fresh snapshot. Missing acknowledgement after 20 seconds also requests a snapshot;
it does not optimistically replay the command or award anything. Requests remain single-flight.
Match-level disconnect recovery and timers are intentionally deferred; G3 transport reconnection remains intact.

## Rules and server authority

The explicit I1 creation service resolves the active DUEL from GameCatalogService and freezes the
complete DOUBLE_NINE_DUEL v1 snapshot. It does not alter immutable catalog v2's LOCAL capability
metadata: ONLINE enablement is the explicit DUEL-only I1 service, not the existing local mode selector.
Existing matches never resolve rules through a new catalog again.

SecureRandom is the production RNG behind an injectable OnlineRandom. Starter tiles are virtual,
separate from the shuffled 55-tile gameplay deck. Equal high selections repeat manually. The
even/odd tile is not exposed before the guess. Resolved selection audit is private to participants.

The server deals ten per player, retains 35, permits no draw, validates turn/ownership/end/pass,
resets passes after a play, and finishes Tranque after two legitimate passes. Scoring comes from
the frozen v1 rules: normal opponent pips +10; Capicua opponent pips ×2 +10; Tranque opponent pips
only. Lowest individual pips wins Tranque; ties go to the round starter. Match target is 150.
Round result remains visible until an explicit NEXT_ROUND command; the next deal is one transaction.

## Persistence and concurrency

Authoritative paths reuse M4:

- `matches/{id}`: M4 Match and frozen rule snapshot.
- `matches/{id}/runtime/authoritative`: trusted private state encoded as JSON (Firestore forbids
  nested arrays). Hands/reserve, phase, board and current round persist here, not solely in Redis.
- `players/{seat}`, `rounds/{number}`, `events/{12-digit-sequence}` beneath the Match.
- `commands/{commandId}`: SHA-256 fingerprint bound to verified UID and canonical typed command,
  firstSequence and resultingSequence. No token or raw credential is stored.
- `players/{uid}/matchHistory/{id}`: M4 WIN/LOSS projection on final commit.

Firestore transaction reads receipt + authoritative revision, validates the transition, then writes
root, private state, round(s), every event, receipt and final histories atomically. The server-observed
lastSequence is the CAS revision. Conflicting concurrent requests cannot commit against the same
revision, including after a Firestore retry. A reused ID with changed payload or UID is rejected.
Duplicate requests return the original receipt even after the match advances. No JVM mutex is the
production authority. The in-memory repository and fake verifier exist only in test sources.

M4's LOCAL append service now explicitly rejects ONLINE matches so it cannot change only their
public root and diverge from private state. Its replay reducer recognizes starter progress. These
are the limited M4 changes needed for consistent ownership of online state.

## Validation and boundaries

Commands: `gradlew test build`, `gradlew exportOnlineFixtures`, and the explicit
`gradlew validateOnlineI1Real`. The last creates two temporary Firebase anonymous test identities,
uses real G3 on loopback 18083 with existing Redis and Firestore, completes a match, verifies
private event delivery/history, closes its server, and deletes only identities created by that run.
Synthetic Match/history records remain flagged validationData=true. No Player bootstrap, wallet,
ledger, reward intent, ad or catalog write is made by the runner.

Unity: `client/Validation/RunOnlineMatchTests.ps1` and isolated batch
`Domino.Online.Editor.OnlinePlayModeValidation.Run` (nine Portrait sizes, both local seats).
Generated fixtures/results/images remain under ignored `client/Validation/Generated`.

I1 routes messages through one local backend instance. Atomic state is safe across instances;
cross-instance WebSocket fan-out is not yet provided and must precede multi-instance hosting.
No client-side polling, matchmaking, runtime turn deadline, autoplay, abandonment, spectator
runtime, replay UI, H7 or production deployment is introduced. Redis remains a transport/presence
dependency; REST Match persistence has no Redis dependency. A broadcast failure can leave a
client behind a committed state; snapshot resync is the recovery path, with a durable outbox deferred.
No Android physical or combined live-network Unity pair test is claimed: real networking used two
independent authenticated JVM clients; Unity presentation was validated separately using server fixtures.
