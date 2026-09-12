# Player Foundation V1 — Unity transport and A–F checkpoint

Authenticated REST bootstrap transport and in-memory Player state, with the Phase E validation history and A–F checkpoint recorded below. The checkpoint introduces no Player UI, gameplay, backend behavior, Firebase Console or deployment changes.

## Composition and configuration

`ApplicationServices` owns the existing Firebase bootstrap/authentication and the new `PlayerService`. Player initialization runs independently of scene/menu initialization. The service awaits the existing identity task; it never signs in, signs out or creates another Guest itself.

`Assets/_Domino/Resources/ApiSettings.asset` is explicitly **disabled**, with base URL `http://127.0.0.1:8080`, environment `LOCAL`, and timeout 15 seconds. Missing/disabled/invalid configuration produces `FAILED / Configuration` with no snapshots or HTTP calls; offline play remains available. Enable this asset in the Inspector only for an approved reachable backend. Remote destinations require HTTPS; the local HTTP exception is described below. Embedded credentials, query strings and fragments are rejected. No production URL is bundled.

On success, `ApplicationServices.Player.Player` and `.Wallet` expose immutable snapshots, separate from HTTP DTOs. Coins use `long`, bounded by the backend's safe integer maximum. Failed synchronization never invents a confirmed zero balance. `State` is `NOT_SYNCED`, `SYNCING`, `SYNCED` or `FAILED`; `Error` contains a safe category, HTTP status, recognized server code and validated request ID.

Concurrent `InitializeAsync()` callers share the same task. Calling again after `FAILED` explicitly retries; calling after `SYNCED` reuses the successful task. There is no polling loop or automatic 503 retry. A lifetime cancellation or the 45-second overall initialization deadline prevents late publication. Each HTTP attempt, including token acquisition, has its own 15-second deadline. Exiting Play Mode or quitting cancels pending work.

## Authentication and transport

`FirebaseSdkClient` implements `IAuthTokenProvider` through the existing SDK's `FirebaseUser.TokenAsync(forceRefresh)`. The testable `FirebaseIdTokens` boundary checks the current user and expected identity before and after awaiting the SDK. Empty tokens are rejected. Firebase operations cannot themselves be aborted; cancellation stops waiting and observes late failures without publishing results.

`DominoApiClient` sends only `{"language":"en"}` or `{"language":"es"}` to `POST /api/v1/player/bootstrap`. Language comes from the existing initialized Unity Localization system. The response does not change the local language.

First attempt acquires a token with `forceRefresh=false`. Only a 401 causes one further attempt with `forceRefresh=true`. A second 401 stops. Each attempt obtains a token from the provider; tokens are never persisted or logged. `UnityApiTransport` uses UnityWebRequest, native certificate validation and `redirectLimit=0`; it aborts pending requests and disposes handlers on cancellation.

The server response UID and the still-current identity must both match the identity used to start synchronization. Unknown account/status enums, missing fields, invalid JSON and non-integer wallet amounts are contract failures. The API reader reuses the project's already-installed Unity Newtonsoft JSON 3.2.1 package, now declared directly. This avoids JsonUtility's observed decimal-to-long coercion. Parsing uses explicit fields, duplicate-property rejection, a depth limit and a 64 KiB input limit; no polymorphic deserialization is used.

Server error messages and raw bodies are not retained in exceptions or shown to players. Recognized codes are the Phase D authentication, validation, conflict, unavailable, contention and internal-error codes. Unknown/proxy error bodies still produce a sanitized failure with the HTTP status. The preexisting auth log now records identity readiness without printing a full UID or SDK exception.

## Validation

Run from repository root:

```powershell
& client/Validation/RunPlayerFoundationClientTests.ps1
& client/Validation/RunGuestAuthTests.ps1
& client/Validation/RunTests.ps1
& client/Validation/Compile.ps1
```

The client suite uses the **production JSON codec**, fake token/HTTP boundaries and the existing auth service with a fake SDK. It checks normal/forced tokens, missing user, cancellation, changed identity, empty token, long amounts, malformed contracts, all error categories, one 401 refresh, second-401 stop, timeout, task sharing, explicit retry, late-result suppression and no second Guest.

For the opt-in Unity validation, launch a separate batch editor with `-executeMethod Domino.Editor.PlayerFoundationValidation.Run` (without `-quit`). The runner uses an editor-only fake Firebase factory, the disabled API asset, the production codec and the existing portrait smoke suite. It exits its own editor after testing nine portrait sizes, all four seats, dealing, drag/drop and a complete round. It never invokes real Firebase or a backend. Do not launch this runner in a user's existing working editor.

Phase E results:

- Client suite: **173 checks PASS**.
- Unity production codec: **9 checks PASS**.
- Existing Guest Auth unit suite: **43 checks PASS**.
- Existing rules/geometry suite: **2,981,089 checks PASS**, 1,000 games.
- Existing configuration suite: **82 checks PASS**.
- Existing scoring suite: **32,679 checks PASS**; regression traces unchanged.
- Unity portrait suite: **139,410 checks PASS**, nine display sizes.
- Unity compilation and Play Mode: **PASS**, Console errors **0**.

Generated logs and screenshots are kept under `Validation/Generated/`, outside version control. Test quantities are assertions/checks, not independent test methods.

## Phase E historical limits

Real Firebase token acquisition, a real HTTPS bootstrap and UnityWebRequest over an actual connection were not exercised in this phase. HTTP outcomes/timeouts were simulated; native TLS, redirect and disposal configuration were reviewed and compiled. No Firestore SDK calls were added.

Phase F: run the backend with approved local ADC, establish a suitable development connection, perform the first real authenticated bootstrap and verify the Firestore documents. Do not add ads or Player UI yet.

## Local HTTP correction (Phase E/F)

The original HTTPS-only policy described above is now extended only for parsed HTTP hosts exactly `localhost` or `127.0.0.1`, environment exactly `LOCAL`, and Unity Editor or a Development Build. `DominoApiSettings` supplies the execution context through `UNITY_EDITOR` / `Debug.isDebugBuild`; it is not a serialized bypass. Release builds reject local HTTP. Remote HTTP, LAN addresses, deceptive subdomains, numeric aliases and credentials in URLs remain rejected. HTTPS and native certificate validation remain unchanged, as does redirect protection.

PlayerService now emits safe start/success/failure logs. Invalid configuration emits `[PLAYER] bootstrap unavailable reason=API_CONFIGURATION`, retains FAILED and requests neither token nor HTTP. No raw response, token or authorization header is logged. The manually configured ApiSettings asset is not altered by this correction.

Validation after the correction: **230 client checks PASS**, including deterministic development/release policy cases and local-bootstrap simulation; Guest Auth **43 checks PASS**; existing gameplay/configuration/scoring suites PASS; static compilation against Unity assemblies PASS. These tests do not call the running local backend or Firebase.

## A–F checkpoint — real integration, 2026-09-12

The following real-integration results were supplied and confirmed by the project owner for this checkpoint; the automated checkpoint validation does not repeat cloud writes.

- REAL_FIREBASE_AUTH=PASS; REAL_FIREBASE_TOKEN=PASS; REAL_BACKEND_LOCAL=PASS.
- ADC_AVAILABLE=YES through service-account impersonation (`domino-local-dev@teamfho-domino.iam.gserviceaccount.com`), without a service-account private key JSON.
- REAL_FIRESTORE_PLAYER=PASS; REAL_FIRESTORE_WALLET=PASS in `teamfho-domino`, using the existing anonymous Firebase user.
- Player persisted as GUEST / ACTIVE, language es, displayName Guest-6DH9C7CA, with identity-matching UID and timestamps present.
- Wallet persisted with coins=0, lifetimeCoinsEarned=0, lifetimeCoinsSpent=0 and timestamps present.
- REAL_SECOND_BOOTSTRAP_IDEMPOTENCY=PASS: the same Player and Wallet documents were reused, displayName and Wallet createdAt preserved, coins remained zero, no duplicates or wallet reset.
- OFFLINE_TRANSPORT_FAILURE=PASS: stopping the backend produced a controlled Transport failure, without a crash, new Guest or loss of the authentication session.
- RESTART_RECOVERY=PASS: restarting the backend and then stopping/starting Unity Play Mode restored synchronization with the same identity, Player and Wallet.

SAME_SESSION_AUTO_RETRY=NOT_IMPLEMENTED. Same-session recovery was not validated; do not confuse restart recovery with automatic retry. The existing service method can be called explicitly after failure, but this checkpoint adds no RetryAsync API or recovery UI.

Committed development configuration: Environment=LOCAL, BaseUrl=http://127.0.0.1:8080, TimeoutSeconds=15, **Enabled=false**. This is the only checkpoint configuration adjustment, preventing an automatic localhost bootstrap on opening a clone. Enabling it remains an explicit local development action.

No Player UI, ads/economy rewards or Cloud Run TEST deployment are included. Next task: Phase G1, PlayerService RetryAsync and same-session recovery plus connection/sync state. Do not deploy Cloud Run yet.

Final checkpoint automated validation: backend **125 tests PASS**, Gradle build PASS; Guest Auth **43 checks PASS**; Player Foundation **230 checks PASS**; rules/geometry **2,981,089 checks PASS**; configuration **82 checks PASS**; scoring **32,679 checks PASS**; portrait **139,394 checks PASS** across nine sizes and a complete round. Unity static compilation and isolated Editor Play Mode PASS, Console errors=0. Portrait evidence is in the ignored `Validation/Generated/checkpoint-af-unity.log` and `Validation/Generated/portrait-unity-result.txt`. No live backend or real Firebase calls were used by these checkpoint tests.
