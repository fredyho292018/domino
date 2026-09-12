# Player Foundation V1 — Phase G1

Source checkpoint: `e17b17d5a3f85d09d223c21ecfd22f820cfaa700`, branch main, initially clean. No backend, gameplay, UI, Firebase configuration, IAM or schema changes. No commit/push.

## Public service API and semantics

- `InitializeAsync(CancellationToken = default)` and `RetryAsync(CancellationToken = default)` share one active task. The operation is reserved before callbacks run, including reentrant event subscribers and concurrent callers.
- NOT_SYNCED: no attempt yet. Either method starts normal initialization.
- SYNCING: all callers join the same task; no parallel bootstrap.
- SYNCED: RetryAsync and InitializeAsync are no-ops returning the completed operation.
- FAILED: the last operation failed. RetryAsync starts one logical attempt only if `CanRetry` is true. Non-retryable failures remain unchanged without token/HTTP calls.
- `Player`, `Wallet`, `HasConfirmedSnapshots`, `IsFresh`, `Error`, `Availability` and `CanRetry` have no public setters.

FAILED does not mean logout, invalid Firebase identity or unavailable offline gameplay. Initial failure leaves both snapshots absent. Later failures preserve prior confirmed snapshots as in-memory, last-known data, with `IsFresh=false`. Successful retry atomically replaces the snapshots after UID and contract validation. An internal `RefreshConfirmedAsync` seam exercises this future-refresh behavior; it is not exposed to UI or automatically triggered.

Retries reuse `identity.Current`, never call identity initialization, SignOut or SignInAnonymously. A missing or changed identity fails safely and cannot replace confirmed data. The first initialization still uses the existing identity service. Each logical retry obtains a new SDK token with forceRefresh=false; the unchanged API client allows only one forceRefresh=true retry after 401.

## Availability and retry policy

BackendAvailability is independent of login and synchronization:

| State | Evidence |
|---|---|
| UNKNOWN | No API observation, or local configuration/identity prevents an attempt |
| AVAILABLE | Successful bootstrap or an HTTP rejection proving the server responded |
| UNAVAILABLE | Transport/timeout failure or HTTP 5xx |

Availability describes the last observation, not a live internet monitor. It remains unchanged while an attempt is in progress and on cancellation. No internetReachability check, health polling, timer retry or foreground auto-retry is added. A future foreground handler can explicitly call RetryAsync after checking CanRetry.

Retryable: Transport, Timeout, HTTP 500/502/503/504, and caller cancellation while the application lifetime is still alive. Non-retryable: Configuration, Contract/UID mismatch, persistent 401 after internal token refresh, 403, other 4xx including invalid persisted state. Existing `Server` plus `HttpStatus=403` represents authorization failure without introducing another category. Authentication failures require identity repair outside this phase.

## Cancellation, events and lifetime

The first caller's token owns a shared operation. Joining callers receive exactly the same task; their tokens do not independently cancel somebody else's operation. Application lifetime cancellation and PlayerService.Dispose always cancel the active operation. Cancellation settles in FAILED/Cancelled, never permanently SYNCING, and never publishes late results. Retry cannot start after shutdown.

Construction in ApplicationServices captures Unity's main synchronization context. Calls from other threads are dispatched to that context. `SyncStateChanged` and `BackendAvailabilityChanged` run there, only on value changes. Each subscriber is isolated: one exception cannot prevent other subscribers or corrupt synchronization. Consumers unsubscribe using normal `-=`; Dispose removes subscriptions and is called by ApplicationServices shutdown. Plain unit tests without a Unity context run on their caller context; a deterministic context test verifies worker dispatch and event delivery.

Safe logs: bootstrap/retry starting, succeeded and failed category. Configuration errors use `bootstrap unavailable reason=API_CONFIGURATION`. No token, authorization header, full response or UID is logged by PlayerService.

## Editor validation

`Domino > Player > Retry synchronization` is enabled only during Play with CanRetry=true. It invokes the same ApplicationServices.Player instance and is compiled only in the Editor. No player-visible button or production debug UI exists.

`Domino.Editor.PlayerRetryValidation.RunReal` is an opt-in isolated batch harness. It wraps the production FirebaseSdkClient with an existing-Guest-only guard that prohibits anonymous sign-in. Its file signals coordinate the test, not a production polling/retry mechanism. Normal automated validation still uses fakes. The source ApiSettings asset remains disabled; only the ignored real-test copy is temporarily enabled with LOCAL/http://127.0.0.1:8080.

Real same-session validation on 2026-09-12:

1. Port 8080 confirmed OFF; isolated Unity Play started with the existing anonymous Guest.
2. Bootstrap failed Transport, service FAILED, no fake wallet.
3. Two authorized REST reads using impersonated ADC confirmed the existing Player and Wallet in teamfho-domino before retry.
4. Started the unchanged local backend JAR with real Firebase Admin/ADC; health UP.
5. Triggered RetryAsync in the still-running Play session: retry starting → retry succeeded → SYNCED. No Stop/Play occurred.
6. Two further Firestore reads confirmed identical document paths, UID, displayName, Player createdAt, Wallet createdAt, coins and lifetime counters. Unity snapshots matched these persisted values.
7. Closed only the isolated validation editor and the backend started for this test, restoring the initial backend-OFF state. The user's open editor was not closed.

REAL_SAME_SESSION_RECOVERY=PASS; SAME_UID=PASS; SAME_PLAYER=PASS; SAME_WALLET=PASS; COINS_PRESERVED=PASS; CREATED_AT_PRESERVED=PASS; NEW_GUEST_CREATED=NO; CONSOLE_ERRORS=0. Four inspection reads; the existing bootstrap transaction additionally performs its normal reads and may throttle-update lastSeenAt. No manual Firestore writes, deletions or wallet updates were made.

Ignored evidence: `Validation/Generated/G1Real/final.txt`, `firestore-validation.txt`, `state.txt` and `Validation/Generated/g1-real-unity.log`. Temporary inspection tooling and UID-bearing comparison artifacts remain under Generated and are not versioned. No credential/token files were created.

## Automated regression

Run the existing RunGuestAuthTests.ps1, RunPlayerFoundationClientTests.ps1, RunTests.ps1 and Compile.ps1. The Player suite includes PlayerRetryTests.cs and the production API codec/client, with deterministic fake transport/identity. It checks retry/no-retry policy, concurrent and reentrant single flight, exact state sequences, subscriber exceptions, worker-to-main-context delivery, unsubscribe, cancelled queued operations, cached snapshots, UID mismatch and late response suppression.

- Guest Auth: 43 checks PASS.
- Player Foundation including G1: 341 checks PASS.
- Configuration: 82 checks PASS.
- Scoring: 32,679 checks PASS, traces unchanged.
- Rules/geometry: 2,981,089 checks PASS, 1,000 games.
- Static compilation against Unity assemblies: PASS.
- Isolated Unity portrait/Play Mode: 139,400 checks PASS across nine sizes and a complete round; Console errors=0. Production codec checks: 9 PASS. Evidence: `Validation/Generated/g1-portrait-unity.log` and `Validation/Generated/portrait-unity-result.txt`.

Portrait validation uses the existing PlayerFoundationValidation.Run against an isolated copy with disabled API/fake Firebase. No live backend is required. Backend sources are unchanged.

## Limits and next step

No automatic foreground retry, backoff, periodic checks or recovery UI. Persistent authentication/configuration/contract failures are deliberately non-retryable. Cached snapshots are not fresh server confirmation. No nonzero-balance writes were performed during real validation.

Next: Phase G2, Player UI and editable alias. Do not implement Ads or deploy Cloud Run yet.
