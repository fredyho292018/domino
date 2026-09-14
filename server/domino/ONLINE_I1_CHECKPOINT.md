# DOMINO ONLINE — PHASE I1 CHECKPOINT

Base: `main`, M4 `786073aed2d1c45cab87540dbe8b7ff67b895205`.
This checkpoint contains I1 and its I1.1 validation only. No I2/H7 runtime is introduced.

## Validated results

- Backend: 414 passed, 2 optional skipped, no failures.
- Initial I1: two independently authenticated JVM clients completed a full match; 320 contiguous Firestore events and two WIN/LOSS history projections verified.
- I1.1: real Unity Editor/native Firebase + independent authenticated JVM; different UIDs, real create/join, Unity starter input, own-hand privacy, actual G3 WebSocket updates and synchronized board/score.
- Unity played ten tiles and won a normal round: opponent remaining pips 7 + finish bonus 10 = 17 points, awarded by the backend. Final Unity/server/JVM sequence and persisted event count: 68.
- Out-of-turn Unity PASS rejected with no state/sequence mutation. Explicit snapshot resync passed. Gap detection, event-page projection, durable dedupe, command conflicts and concurrent play races passed in I1 tests.
- Unity compilation and Play Mode passed, 365 live I1.1 checks, zero final Console errors. Previous 265 portrait checks remain valid. Local duel, partners 2v2, M4, G3, Player Foundation and monetization regressions passed.
- No wallet/ledger writes, monetization changes, ads requests or deploy.

Server owns authenticated UID, RNG, dealing, turn/move/pass validation and scoring. Unity sends intents and displays confirmed snapshots. Firestore transactions own state, event sequence and durable command receipts; in-memory test repositories are not production authority. Public events never contain hidden opponent hands.

## Synthetic/test record registry

These IDs identify development validation data, not user gameplay:

| Match | Purpose | Persisted validationData flag | Completion |
|---|---|---|---|
| `85e8edfd-5c4d-40f4-aa91-0d988b8941af` | I1 two-JVM real integration | true | Full match, 320 events, two history entries |
| `0118f5fd-4cb3-4897-8fd2-1a7cc9c9f259` | Final I1.1 Unity + JVM validation | false: ordinary authenticated create API | Normal round, 68 events, score 17–0 |
| `697401a9-c8cd-42db-aace-aca052cbb2bc` | Intermediate I1.1 harness validation | false: ordinary authenticated create API | Blocked round, 62 events |
| `c7a12823-f1a2-4bf0-9811-6526435b49b6` | Intermediate I1.1 harness validation | false: ordinary authenticated create API | Incomplete |
| `04a1de71-d671-4005-9291-c7b8cb595e71` | Intermediate I1.1 harness validation | false: ordinary authenticated create API | Incomplete |

I1.1 records are explicitly labeled synthetic/test in this registry, not retroactively changed in Firestore. Do not claim that their stored flag is true. No Firestore mutation or cleanup was performed for the checkpoint. The temporary companion Firebase identities were removed after validation; Unity's existing identity was preserved.

Full match/history verification is **PASS for initial two-JVM I1**, but **NOT_RUN for Unity I1.1**, which ended after one round.

## Limitations preserved

`WEBSOCKET_MULTI_INSTANCE_DISTRIBUTION=NOT_VALIDATED`. Single-LOCAL-server fan-out is sufficient for this checkpoint; distributed delivery must be addressed before horizontal Cloud Run scaling. No speculative implementation was added.

`REAL_TWO_UNITY_CLIENTS=NOT_RUN`; `REAL_UNITY_PLUS_JVM=PASS`. Android physical validation was not performed. Existing round-result controls can overlap a long chain; visual polish remains documented in the I1.1 report.

Turn timers, autoplay, match reconnect/abandonment runtime, matchmaking, spectators, replay UI and H7 are not implemented. Existing G3 transport reconnection remains unchanged. Local play does not require an online backend.

## Source hygiene

Only I1/I1.1 source, tests and documentation belong in this commit. Generated validation evidence stays ignored.

User-local assets are excluded from staging and preserved byte-for-byte:

- AdsSettings.asset SHA-256: `C377416E334727264806761518A4B5EDF381A837A927A1C3F3D523A1ACBDB5D6`.
- ApiSettings.asset SHA-256: `AC6B0ED6D30240BC531B235E43E8B9B55D3B48DCBCCCF365ADEC62D3EE6BCB37`.

The working tree is expected to retain only those two user modifications after commit. The earlier I1/I1.1 reports describe their pre-commit validation state; their `COMMIT=NONE` fields are historical.

## Evidence and next step

See `ONLINE_DUEL_I1.md`, `ONLINE_DUEL_I1_REPORT.txt` and `ONLINE_UNITY_I11_REPORT.md` for implementation, test counts, exact paths and reproducible validation.

Commit subject: `feat: add authoritative online duel match engine`.
Publish to `origin/main`; no deployment.

Next, after separate authorization: I2 authoritative turn runtime, 60-second deadline, deterministic timeout action and 180-second reconnect window. I2 has not started.
