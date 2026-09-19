# DOMINO ONLINE — PHASE I4 HISTORY + REPLAY REPORT

Implementation and local validation, 2026-09-19. Awaiting review; no checkpoint created.

The real Unity validation used Unity 6000.0.41f1 Play Mode in an isolated copy of the project, the normal History menu entry, and a loopback Spring Boot server serving **unchanged completed I3.1 archives**. It did not use a production backend, Firebase authentication, or real Firestore. The test verifier exists only on the backend test classpath. This is actual Unity rendering and HTTP validation, not a synthetic reducer-only claim.

```text
BRANCH=main
BASE_COMMIT=5c5804868b00a9ae0cfd46fd6f654a8a7d5dc060
SOURCE_SHA_BEFORE=5c5804868b00a9ae0cfd46fd6f654a8a7d5dc060
SOURCE_SHA_AFTER=5c5804868b00a9ae0cfd46fd6f654a8a7d5dc060

HISTORY_IMPLEMENTED=YES
HISTORY_PAGE_SIZE=20
HISTORY_PAGINATION=PASS
HISTORY_ORDER=finishedAt DESC; matchId DESC tie-break
DUEL_HISTORY=PASS
PARTNERS_HISTORY=PASS
HISTORY_AUTHORIZATION=PASS
HISTORY_EMPTY_STATE=PASS
HISTORY_ERROR_STATE=PASS

REPLAY_MANIFEST=PASS
EVENT_PAGING=PASS
STRICT_SEQUENCE=PASS
EVENT_SCHEMA_VERSION=1
REPLAY_SCHEMA_VERSION=1
MATCH_RULE_SNAPSHOT_USED=YES
CURRENT_CATALOG_AFFECTS_REPLAY=NO
PRIVATE_HISTORICAL_HANDS_AVAILABLE=YES, for complete supported archives
CAN_RECONSTRUCT_INITIAL_HANDS=YES
CAN_RECONSTRUCT_HAND_AFTER_EACH_PLAY=YES
LEGACY_REPLAY_POLICY=Fail closed; see audit below

REPLAY_REDUCER=PASS
DETERMINISTIC=PASS
DUEL_RECONSTRUCTION=PASS
PARTNERS_RECONSTRUCTION=PASS
FINAL_STATE_PARITY=PASS
MID_STATE_PARITY=PASS
SEEK_PARITY=PASS
BACKWARD_NAVIGATION=PASS
TILE_CONSERVATION=PASS
BOARD_PARITY=PASS

DUEL_TRANQUE_REPLAY=PASS
PARTNERS_TRANQUE_REPLAY=PASS
CAPICUA_REPLAY=PASS
TIMEOUT_REPLAY=PASS
AUTOPLAY_REPLAY=PASS
ROUND_TRANSITION_REPLAY=PASS

TABLE_PERSPECTIVE=PASS
PLAYER_PERSPECTIVE=PASS
UNAUTHORIZED_REPLAY=403 for authenticated nonparticipant
UNAUTHENTICATED_REQUEST=401, existing Firebase security preserved
CROSS_MATCH_PRIVATE_ACCESS=DENIED
ACTIVE_MATCH_FULL_REPLAY=DENIED (409 ACTIVE_MATCH)

HISTORY_UI=PASS
REPLAY_UI=PASS
GAMEPLAY_TABLE_REUSED=YES
PLAY_PAUSE=PASS
PREVIOUS_NEXT=PASS
START_END=PASS
SEEK=PASS
SPEED_0_5X=PASS
SPEED_1X=PASS
SPEED_2X=PASS
PERSPECTIVE_SWITCH=PASS
GAMEPLAY_INPUT_ENABLED=NO
PORTRAIT=PASS
SPANISH=PASS
ENGLISH=PASS
UNITY_COMPILATION=PASS
UNITY_PLAY_MODE=PASS
CONSOLE_ERRORS=0
UNITY_ACCEPTANCE_CHECKS=2490

REAL_UNITY_DUEL_REPLAY=PASS (retained completed archive, loopback test server)
REAL_DUEL_FINAL_SCORE_PARITY=PASS, 159-62
REAL_DUEL_FINAL_SEQUENCE_PARITY=PASS, 291
REAL_UNITY_PARTNERS_REPLAY=PASS (retained completed archive, loopback test server)
REAL_PARTNERS_TEAM_SCORE_PARITY=PASS, 210-40
REAL_PARTNERS_FINAL_SEQUENCE_PARITY=PASS, 475

REPLAY_FIRESTORE_POLLING=0
PARTICIPANT_WRITES_FROM_REPLAY=0
MATCH_WRITES_FROM_REPLAY=0
EVENT_WRITES_FROM_REPLAY=0
HISTORY_WRITES_FROM_REPLAY=0
WALLET_WRITES_FROM_REPLAY=0
HISTORY_PAGE_READS=24 repository document reads in first-page fixture; see cost notes
REPLAY_MANIFEST_READS=520 DUEL / 528 PARTNERS
REPLAY_EVENT_PAGE_READS=1 authorization root read per cached page
REAL_FIRESTORE_CALLS=0

REPLAY_EVENT_COUNT=510 DUEL / 523 PARTNERS, cost fixtures
EVENT_PAGES_FETCHED=3 per cost fixture
NETWORK_REQUESTS_DURING_NORMAL_PLAYBACK=0
UNITY_LOOPBACK_HTTP_REQUESTS=7
UNITY_SYNTHETIC_EMPTY_ERROR_REQUESTS=4
RETAINED_MATCHES_RECONSTRUCTED=62
RETAINED_EVENTS_RECONSTRUCTED=31061
REDUCER_CHECKS=1595 PASS

REPLAY_ENGINE_DEPENDS_ON_HISTORY_UI=NO
REPLAY_ENGINE_DEPENDS_ON_FIRESTORE=NO
REPLAY_ENGINE_REQUIRES_FINISHED_MATCH=NO
I5_STARTED=NO

M4=PASS
I1=PASS
I2=PASS
I3=PASS
M5=PASS
I3_1=PASS, 25 unit tests; load matrix not repeated
F0=PASS
F0_1=PASS
DUEL_GAMEPLAY=PASS
PARTNERS_GAMEPLAY=PASS
MATCHMAKING=PASS
MONETIZATION=PASS

ADS_SETTINGS_PRESERVED=YES
API_SETTINGS_PRESERVED=YES
LOCALIZATION_SETTINGS_PRESERVED=YES
COMMIT=NONE
PUSH=NONE
DEPLOY=NONE
```

## Implementation and access contract

- `server/domino/src/main/kotlin/com/teamfho/domino/match/ReplayHistoryController.kt`: authenticated `GET /api/v1/players/me/history?limit=20&cursor=...`. Identity always comes from the verified principal. Historical names, team composition, outcome and server finish time come from persisted data.
- `ReplayService.kt`: `GET /api/v1/matches/{id}/replay` and `/replay/events?after=...&limit=250`. Each request checks membership and FINISHED status, including cache hits. Manifest validates the complete timeline, private deals, round boundaries and final result. Failure yields a typed availability reason or a safe HTTP error. Event responses strip command correlation IDs and do not expose participant UIDs.
- `ReplaySource` exposes reads only; no transaction, wallet, matchmaking or command service is injected. Eight immutable completed archives are cached, each capped at 100,000 events. Eviction may cause another full read on the next request; normal local playback requires no requests.
- `client/DominoGame/Assets/_Domino/Scripts/Replay/ReplayReducer.cs`: pure value reconstruction, strict schema/sequence/match validation, tile ownership/orientation/conservation, immutable checkpoints every 50 events and round boundaries. It accepts ordered authorized events independently of history, completion status and data provider. `ReplayTimeline` is the finished-archive loader, not the reducer itself.
- `ReplayClient.cs`: authenticated existing API transport, GET only, pages preloaded before playback; checkpoint construction runs off the Unity rendering thread.
- `HistoryReplayView.cs`, `UI/ReplayBoardPresentation.cs`: normal menu entry, details, existing BoardView/prefabs/theme/chain/player renderers, read-only historical perspective, controls and localized event descriptions. Seek clears pending animation/effects and restores the target state. Playback speed scales both event spacing and tile/pass presentation without changing global time or gameplay defaults.
- Fifty EN/ES keys added using the existing localization system. Only StringTables/shared data were imported; the user's Localization Settings asset was not rewritten.

## Historical data audit / limitations

| Data | Policy |
|---|---|
| Completed online DUEL/PARTNERS schema-1 archive with all events and HAND_DEALT per seat/round | FULL_REPLAY |
| Active match | DENIED, including participant requests |
| Missing private snapshot, missing sequence/round, unsupported schema, unsupported/local legacy mode | NOT_REPLAYABLE; no invented hands |
| Older M4 histories without sufficient event/deal data | NOT_REPLAYABLE; history remains visible |
| Public-only incomplete archive | No public-only approximation in I4 |

Initial hands are persisted as private `HAND_DEALT` payloads. Subsequent hands are derived by removing each canonical played tile. Exact physical deal order is not stored, so replay renders the recorded hand snapshots directly. Reserve values are not fabricated; `unseenCount` accounts for undealt/unknown tiles. Starter selection uses its recorded result.

History-list availability is a cheap **metadata eligibility indication**; full integrity is checked when opening the manifest. A previously eligible archive with missing data will show the unavailable/incomplete state rather than reconstructing misleading gameplay. This avoids reading every event for every history card. No historical data migration was performed.

History timestamps were already stored as variable-precision ISO strings. The repository now completes boundary-second buckets and sorts those by Instant, correcting lexical fractional-second ordering without rewriting old data. Reads are bounded: page limit plus boundary buckets, each capped at 1,000 records. An extraordinary bucket over that cap fails safely rather than downloading unlimited history.

Actual physical-device testing and production-Firebase replay requests were not performed. Existing landscape behavior was not redesigned. Completed I3.1 exports, rather than the still-active human validation matches, were used.

## Event support matrix

| Event | Reducer/presentation handling |
|---|---|
| MATCH_STARTED | Initial persisted scores |
| STARTER_PROGRESS | Recorded selection progress |
| STARTER_SELECTION_PRIVATE | Authorized recorded starter result/chosen tiles |
| ROUND_STARTED | Clear prior board/hands, restore starter/counts/multiplier |
| HAND_DEALT | Exact private hand snapshot; no fabricated animation order |
| TURN_STARTED | Current seat/phase; no live countdown |
| TURN_CHANGED | Current seat/phase |
| TILE_PLAYED | Remove owned tile, preserve oriented left/right chain; existing animation |
| PLAYER_PASSED | Audit and existing two-knock visual feedback |
| ROUND_FINISHED | Authoritative result, scores, pips and next multiplier |
| MATCH_FINISHED | Authoritative final result; remain on replay screen |
| TURN_TIMEOUT | Audit/label; no real timer wait |
| AUTO_PLAYED | Audit only; subsequent canonical move applies once |
| PLAYER_DISCONNECTED | Historical connection state |
| PLAYER_RECONNECTED | Historical connection state |
| PLAYER_ABANDONED | Historical abandoned state |

Unknown events or unsupported schema fail closed. All 16 current enum cases are supported. Retained fixtures exercise 15 cases; PLAYER_ABANDONED is implemented but was not present in those completed fixtures.

## Validation evidence

- Backend normal suite: 463 PASS / 14 optional SKIPPED (477 total); separate Bot Swarm unit module: 25 PASS. No swarm launched.
- Backend with existing loopback Redis: 476 PASS / 1 optional SKIPPED, explicitly rerun (not an up-to-date result). F0 restart/recovery/deduplication coverage passed. The temporary replay HTTP server was stopped and port 18087 released.
- Firestore Emulator: 7 PASS, including both retained archives and 45 history records over three pages with fractional-second ordering. Owned emulator process and port cleaned up.
- C# reducer: 1,595 checks across 62 completed archives / 31,061 events; final board, hands, score, winner, round and sequence compared to persisted authoritative runtime; clean reconstruction vs checkpoint seeking at early/middle/round/late points.
- Client regressions: base domain 2,981,089 checks; catalog gameplay 251,685; Duel 14,723; catalog 42; online 48; matchmaking/M5 75; H5 27; H6 26; F0 isolation 16; Player Foundation 469; Guest Auth 43. All PASS.
- Unity 6000.0.41f1: actual Play Mode; 2,490 checks, two modes, EN/ES, sizes 1080×1920, 1170×2532, 1179×2556, 1290×2796, 1206×2622, 1320×2868, 1080×2400, 1440×3120 and 1536×2048. Normal History entry, empty/error, table visibility, labels, selected-seat rotation, hand privacy, no input, safe area, controls, seeking, playback speeds and final result. Screenshots inspected.

Unity fixtures:

- DUEL `09a4bb7e-4bfe-46d4-a939-579de752c5f0`: 291 events, score 159–62.
- PARTNERS `0d5c2301-2374-4348-aa42-65007acf53f7`: 475 events, score 210–40.

Local evidence (ignored generated outputs, not source assets):

- `client/Validation/Generated/I4Tests/unity-result.txt`
- `client/Validation/Generated/I4Tests/unity-acceptance.log`
- `client/Validation/Generated/I4Tests/history.png`
- `client/Validation/Generated/I4Tests/replay-DUEL_1V1-en.png`
- `client/Validation/Generated/I4Tests/replay-PARTNERS_2V2_ONLINE-es.png`
- `client/Validation/Generated/I4Tests/result-PARTNERS_2V2_ONLINE.png`
- `client/Validation/Generated/I4Tests/reducer.log`
- `server/domino/build/i4-final-tests.log`
- `server/domino/build/i4-emulator-final.log`

Cost numbers count returned documents, not Google billing estimates. Cold manifests read match + all event documents + round documents to validate before disclosure. Cached event pages read one root document to reauthorize. The history fixture first repository page reads 21 candidates + 3 boundary documents; the HTTP endpoint adds up to 20 match-root enrichment reads (up to 44 in this case). Later pages additionally read the cursor and its boundary bucket. Setup writes to the emulator are excluded from replay measurement. All replay operations measured zero writes.

## Reproduction and safety

Run local `RunReplayTests.ps1` with the retained I3.1 exports available. It refuses missing archives and has no remote fallback. `RunFirestoreEmulatorTests.ps1` uses the existing loopback demo-project emulator and cleans up its process. Backend `replayValidationServer` is a test-classpath-only task requiring `DOMINO_I4_LOOPBACK=true` and real-Firestore opt-in disabled; it binds only 127.0.0.1:18087. Stop it through `build/i4-loopback.stop` (remove a previous stop file before a new intentional run). The Unity harness refuses the original project and requires a project under `Validation/Generated/`.

Protected SHA-256 values, unchanged before/after:

```text
AdsSettings.asset=C377416E334727264806761518A4B5EDF381A837A927A1C3F3D523A1ACBDB5D6
ApiSettings.asset=AC6B0ED6D30240BC531B235E43E8B9B55D3B48DCBCCCF365ADEC62D3EE6BCB37
Localization Settings.asset=7F07A7C99327EC2A82A924E651E5A626995848F3014EA2AF562AEAA11B4B65AE
```

Next only after review/checkpoint: S1 Social Foundation, then I5 live spectator using the pure reducer. Neither started here.
