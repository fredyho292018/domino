# Game Modes & RuleSets — M1

M1 is passive in Unity. `DominoClientController` still loads its serialized
`double-nine-partners-v1.json` through `LocalGameConfiguration`; `SessionSetup`
and the game engine are unchanged. No match, replay, 1v1, online engine, premium,
or monetization features are introduced.

## Server

`catalog/GameCatalogModels.kt` defines typed identities, rule versions, bindings,
publications and resolved responses. `GameCatalogValidator` validates the whole
graph, schema/capabilities, permutations, topology, capacity, starters, scoring,
default bindings and hashes. M1 supports only the existing four-seat topology
and implemented policies. New enum values do not silently acquire semantics.

`GET /api/v1/game-modes` uses the existing Firebase `/api/**` security chain.
It embeds rules in each resolved mode. No credentials or player information are
in the response. The optional standalone rules endpoint is deferred.

`GameCatalogService` has a synchronized 300-second cache. Override with
`DOMINO_GAME_CATALOG_CACHE_SECONDS` or `domino.game-catalog.cache-seconds` (1..3600).
An unavailable or invalid publication retains a previously valid snapshot.
Without one the endpoint returns 503 `GAME_CATALOG_UNAVAILABLE`; there is no
automatic server fallback or startup seed. Same-version content changes are
rejected while cached. Publication changes affect subsequent catalog reads only.

## Firestore / seed

Run explicitly from `server/domino`:

```powershell
./gradlew seedGameCatalog
```

Uses the existing Firebase project property (`FIREBASE_PROJECT_ID`, development
default `teamfho-domino`) and ADC. Review the target environment before invoking
administrative tooling. It never creates credentials or changes security rules.

The canonical input is `src/main/resources/game-catalog-v1.json`. Seed creates:

```text
gameModes/partners-2v2
ruleSets/double-nine-partners
ruleSets/double-nine-partners/versions/1
gameModeRuleBindings/partners-2v2__double-nine-partners__v1
gameCatalogs/1
systemConfig/gameCatalog                 publishedVersion=1
```

The publication embeds the complete versioned graph, including identities and
binding references. Runtime reads the pointer then the publication and validates
all embedded references. Editing a live identity document cannot silently change
an already published catalog. Timestamps are ISO-8601 strings in this contract.

Firestore represents `seatTeams` as `[{members:[0,2]},{members:[1,3]}]` because
directly nested arrays are unsupported. The codec maps back to API arrays.

All seed reads, conflict checks, create-only writes, and pointer creation occur
in one transaction. Existing semantically different documents abort the seed;
they are never overwritten. A pointer to a different publication also aborts;
seed v1 cannot roll a newer catalog back. Repeating the seed changes nothing.
The CLI repeats the operation and reads the published graph to verify this.

Future publication tooling must validate/create a new immutable publication
before atomically switching the pointer. There is no admin CRUD in M1.

Hash: SHA-256 over compact UTF-8 JSON with recursively ordinal-sorted object
keys, unchanged array order, and root `createdAt`, `updatedAt`, `contentHash`
excluded. Current semantic fields are integer/boolean/ASCII-string data, avoiding
cross-runtime floating-point normalization. This is the M1 canonical format,
not a claim to implement arbitrary RFC 8785 JSON canonicalization.

Unity never accesses Firestore. No Firestore security-rules source is maintained
in this repository; deployed client-write permissions were not changed or
claimed audited. Catalog writes remain server/admin operations in this code.

## Unity

`Scripts/Catalog` contains immutable snapshots, strict JSON/hash validation,
`GameCatalogApi`, `GameCatalogService`, and `FileGameCatalogCache`.
Transport and Firebase token provider are reused; a 401 permits one token refresh.
The existing LOCAL/loopback/development endpoint policy remains in force.

`ApplicationServices.GameCatalog` loads the bundled resource/cache immediately.
Menu activation requests a refresh after existing identity initialization. The
service enforces a 300-second TTL and failure backoff, with no polling. Editor
commands `Domino > Game Catalog > Show Status` and `Refresh` are development only.

Source `ApiSettings` remains disabled. To validate remote operation configure
the existing API settings locally; do not change gameplay configuration. M1's
batch validation uses a separate API configuration and an existing Guest only.

Cache file: `Application.persistentDataPath/game-catalog-v1.json`. It contains
validated catalog JSON, cache schema and download time, never tokens. Writes
flush a same-directory temporary file and atomically replace the old file.
Failure to persist preserves the previous file and permits in-memory remote use.
Corrupt/incompatible cache is ignored. Invalid remote responses never overwrite
good cache; same-version changed responses are rejected. Complete incompatible
catalogs fall back as a unit rather than merging fields.

Bundled resource: `Resources/GameCatalogFallback.json`. The existing gameplay
JSON is deliberately retained separately; parity tests compare every normalized
configuration field and full current-scoring seeded gameplay traces.

Localization remains Unity StringTables (`mode.team_match.title/subtitle`,
`rules.double_nine`, `rules.no_draw`). M1 does not change player-facing menus or
legacy wording; the previously identified “Tranca empatada” translation remains
documented for later correction.

## Validation

```powershell
./gradlew test build
./client/Validation/RunGameCatalogTests.ps1
./client/Validation/RunTests.ps1
```

The last two commands run from repository root. `GameCatalogValidation.Run` is
an opt-in isolated Unity batch runner. It requires an existing Firebase identity,
source API disabled, a test backend at loopback port 18081, and no ads enabled.
It tests real authenticated read and no-auth rejection, then writes
`Generated/M1Evidence/state.txt=REMOTE_PASS_STOP_BACKEND`. Stop only that test
backend and create `Generated/M1Evidence/backend-stopped`; it verifies disk-cache
rehydration, backend-off refresh, corrupt-cache fallback and three Play Mode
deals. It does not send Player/Wallet requests or create a Guest. Before rerunning,
remove only the old validation marker/cache or use a fresh isolated output folder.

The offline tests reconstruct services from disk in the same isolated Play Mode
session; they do not claim a physical-device test or an OS/application restart.
Actual Portrait regression runs separately via `Phase1Validation.RunPortrait`.

Current scoring, unlike the legacy `ALL_OTHER_PLAYERS` hash, is covered by full
catalog equivalence, explicit team-scoring tests, and current trace comparison.
Offline round completion remains NOT_SERVER_AUTHORITATIVE; M1 does not change
the H6/H6.1 economic verification limitation.
