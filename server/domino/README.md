# Domino backend — Player Foundation Phases A–D

Spring Boot 4.0.8, Kotlin 2.2.21, Java 21 and Gradle wrapper 9.7.1.

## Scope

Firebase Admin 9.10.0 supplies the Auth and Firestore client classes. Spring owns one named FirebaseApp (`domino-backend`), one FirebaseAuth and one Firestore bean for the default database. Phase B adds Firebase Bearer authentication. Phase C adds Player/Wallet models and a transactional repository. Phase D exposes authenticated bootstrap. There are no startup calls to that repository.

`firebase.project-id` is validated before ADC lookup. Application configuration defaults to `teamfho-domino`; set `FIREBASE_PROJECT_ID` explicitly for another approved environment. The port uses `PORT`, default 8080. No configuration is read from Unity.

Spring calls FirebaseApp.delete() at shutdown. Firebase's app owns its SDK services, so Spring's inferred Firestore close is disabled to prevent duplicate cleanup. A duplicate live context with the same app name fails instead of reusing another context's app. Closing the context releases that registration.

## Credentials

Production integration is prepared for Application Default Credentials (ADC) supplied by a future Cloud Run service account. No cloud deployment or IAM configuration is included.

For future authorized local integration, prefer ADC impersonation of an approved development service account:

```text
gcloud auth application-default login --impersonate-service-account=<approved-development-account>
```

This is documentation only. Do not run against production to test bean configuration. Plain user ADC from gcloud has limitations with Firebase Authentication; impersonation avoids depending on that user OAuth configuration. No private credential files are needed or belong in this repository.

When Firebase is enabled, missing ADC fails startup. It does not silently switch projects or disable Firebase. `firebase.enabled=false` explicitly disables SDK bean creation, primarily for isolated tests. Project ID validation remains active.

## Tests and build

```text
./gradlew clean test
./gradlew build
```

Dependency/tool downloads require network on the first build. Once cached, test execution needs neither internet nor Google credentials.

The `test` profile disables Firebase beans and provides `domino-test` as the project ID. The application context test activates that profile. Configuration tests replace ADC with in-memory credentials and replace the Firestore factory with a mock; they never refresh a token, verify a user or read/write a document. They check binding, fail-fast validation, singleton creation and registry cleanup across sequential contexts.

No real Firebase connectivity, IAM, token verification or Firestore data access is validated by this phase. Those require later, separately authorized integration work.

## Phase B — HTTP authentication

Only `GET /actuator/health` is public, returning basic status without components or details. `/api/**` requires Firebase identity; all other routes are denied. There is no Player bootstrap endpoint. Test-only identity endpoints exist exclusively in test sources.

The Firebase filter accepts one `Authorization: Bearer <ID token>` header. Empty, malformed or duplicate headers are rejected. Query strings, cookies and alternate headers are never identity sources. Bearer scheme matching is case-insensitive; header length is bounded. The health check ignores credentials so its availability does not depend on Auth.

`FirebaseAdminTokenVerifier` calls `verifyIdToken(token, true)` on Phase A's configured Auth bean. Firebase Admin enforces the configured project, signature and expiration and checks revocation/disabled users. It then calls `getUser(verifiedUid)` to inspect current linked providers. At least one non-anonymous provider means `isAnonymous=false`; the sign-in provider claim is not treated as the current provider inventory. No status is cached. These Auth lookups add latency and depend on future approved ADC/IAM; no real lookups were performed during implementation.

The SecurityContext contains only `FirebaseIdentity(uid, isAnonymous)`, with null credentials. The API is stateless: form login, Basic, CSRF, logout and request caching are disabled; no HTTP sessions are created. Both custom filters are instantiated only in the security chain, never registered as servlet filter beans. No open CORS policy is added.

Errors are JSON with `code`, a fixed public `message`, and a server-generated UUID `requestId` (also in `X-Request-ID`). Auth failures use 401 (`AUTH_TOKEN_MISSING`, `AUTH_TOKEN_INVALID`, `AUTH_TOKEN_EXPIRED`, `AUTH_SESSION_INVALID`); authorization denial uses 403 `ACCESS_DENIED`; known transient SDK failures use 503 `DEPENDENCY_UNAVAILABLE`; unexpected/configuration failures use 500 `INTERNAL_ERROR`. SDK exception messages and causes are discarded at the adapter boundary. Logs contain only the category and generated request ID, not raw tokens, headers or claims.

The test profile still disables real Firebase. Context/security tests explicitly import `FakeAuthConfiguration` from test sources. MockMvc exercises the real security chain with a fake verifier; adapter tests mock FirebaseAuth, tokens and user records. The fake can never enter the production artifact. No additional runtime or test dependencies are required.

## Phase C — Player and Wallet persistence

`PlayerFoundationRepository.ensure(verifiedIdentity, initialLanguage, candidateDisplayName)` uses exactly `players/{uid}` and `players/{uid}/wallet/main`. The caller generates one alias with `GuestDisplayNames.generate()` before calling ensure. It validates the initial language (`en`/`es`); the repository also rejects invalid arguments defensively. No token verification or HTTP behavior occurs here.

One Firestore transaction reads both documents before validation/writes. It creates missing documents, upgrades GUEST to REGISTERED without downgrade, preserves existing names/languages/creation dates, and never writes to an existing Wallet. Missing Player with existing Wallet is repaired without changing any wallet field. Existing unknown fields are preserved by partial Player updates. Malformed stored documents fail without any scheduled writes. New wallet values are all zero; each counter uses Long, bounded to 0..9,007,199,254,740,991 with no floating-point coercion.

Timestamps are explicit server transforms. `createdAt` is set only on creation; `updatedAt` changes on account-type reconciliation; `lastSeenAt` changes after at least 15 minutes. Activity-only updates do not change `updatedAt`. An injected Clock decides the threshold, with time captured outside the retryable callback. Five transaction attempts are allowed; the caller waits up to 30 seconds. A timeout can have an unknown commit outcome; retries remain safe. Internal failures use PLAYER_STATE_CONFLICT, WALLET_STATE_INVALID, FIRESTORE_UNAVAILABLE or FIRESTORE_CONTENTION_EXHAUSTED, without HTTP mappings.

`BootstrapResult` contains domain Player/Wallet. Existing timestamps are `FoundationTimestamp.Recorded(Instant)`. Fields changed by a successful server transform return `FoundationTimestamp.ServerAssigned`: committed server-assigned value, exact timestamp not fetched. This intentionally avoids inventing dates or adding read-after-commit operations. Consumers must not treat the marker as an Instant. The next document read resolves it; Phase D's response does not include timestamps.

Normal costs per attempt: two reads; zero writes if unchanged; two creates for a new identity; one create for a missing document; one partial update for reconciliation/activity. A repair plus Player update can require two writes. The callback has no randomness, external authentication calls, logs, events or global mutation.

Tests use a staged, atomic in-memory transaction double around the SDK interface, exercising the actual repository and mapping code. They cover simultaneous initialization of absent documents and a pre-existing funded Wallet, callback retries, corrupt state, exact Long values, unknown fields and the 15-minute boundary. This is conceptual concurrency coverage, not verification of Firestore's real contention behavior.

Firestore Emulator integration is NOT_RUN: no host or installed emulator was found. No broad tooling was installed. No test falls back to a real project, and no real Firestore reads or writes were performed. Normal tests keep Firebase disabled. Any future emulator test must require an explicitly configured emulator host and isolated test project before constructing a client.

## Phase D: authenticated bootstrap

`POST /api/v1/player/bootstrap` requires `Authorization: Bearer <Firebase ID Token>`.
For a JSON body, send `Content-Type: application/json`. The body is optional:

```json
{"language":"es"}
```

Only `en` and `es` are supported, case-sensitive. Absent body, `{}` or `{"language":null}` defaults to `en` for creation only. Existing profiles keep their language and display name. Unknown fields, including `uid`, `coins`, `accountType`, `status` and `isAnonymous`, are rejected, not ignored. Identity comes exclusively from the verified security principal.

Success is always **200**, for creation, reuse, partial-state repair or account upgrade:

```json
{
  "player": {
    "uid": "firebase-uid",
    "accountType": "GUEST",
    "displayName": "Guest-A7F3C921",
    "language": "es",
    "status": "ACTIVE"
  },
  "wallet": {"coins": 0}
}
```

Explicit response DTOs omit timestamps and lifetime counters. The service generates the candidate name before calling the transactional repository. Bootstrap never grants or resets coins.

Errors share the Phase B contract: `{"code":"REQUEST_INVALID","message":"Request body is invalid.","requestId":"<server-generated UUID>"}`. The same ID is returned in `X-Request-ID`; a supplied client ID is not trusted. No internal exceptions, tokens or Authorization values are included in responses or application logs.

| HTTP | Code | Meaning |
|---|---|---|
| 400 | REQUEST_INVALID | Malformed body or unknown field |
| 400 | LANGUAGE_UNSUPPORTED | Language is not exactly en or es |
| 401 | AUTH_TOKEN_MISSING / AUTH_TOKEN_INVALID / AUTH_TOKEN_EXPIRED / AUTH_SESSION_INVALID | Existing authentication failures |
| 403 | ACCESS_DENIED | Existing authorization failure |
| 409 | PLAYER_STATE_CONFLICT | Persisted Player is inconsistent |
| 409 | WALLET_STATE_INVALID | Persisted Wallet is invalid; never reset |
| 503 | DEPENDENCY_UNAVAILABLE | Auth or Firestore is temporarily unavailable |
| 503 | FIRESTORE_CONTENTION_EXHAUSTED | Transaction retry budget exhausted |
| 500 | INTERNAL_ERROR | Unexpected failure, sanitized |

Controller tests exercise the real Jackson 3 MVC converter and security chain with a fake verifier and in-memory transactional repository. Service tests check idempotency and validation before persistence. All normal tests use the test profile without ADC or real Firestore access.

Next phase: Unity IAuthTokenProvider + DominoApiClient and authenticated bootstrap transport. Do not implement Player UI yet.

