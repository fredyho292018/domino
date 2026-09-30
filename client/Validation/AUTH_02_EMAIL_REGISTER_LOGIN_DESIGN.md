# AUTH-02 Email Register / Login — technical design audit

BASE_SHA=823e5c679388ac783bbba5ecb9acacd3dc8e6146
MODE=READ_ONLY_AUDIT_AND_DESIGN
AUTHORITATIVE_IDENTITY_KEY=FIREBASE_UID
AUTH_02_IMPLEMENTATION_STARTED=NO

## Evidence and SDK inventory

Inspected the installed managed binary using Mono.Cecil metadata without invoking Firebase or loading native authentication. Firebase.Auth.dll assembly version is 0.0.0.0 (not its release version). Release evidence is Assets/Firebase/Editor/FirebaseAuth_version-13.16.0_manifest.txt and AuthDependencies.xml: FirebaseAuth.unitypackage / firebase-auth-unity 13.16.0; Android firebase-auth 24.2.0; iOS Firebase/Auth 12.18.0. No dependency or configuration change is proposed.

| Operation | Exact installed API | Result |
|---|---|---|
| Register | FirebaseAuth.CreateUserWithEmailAndPasswordAsync(string, string) | Task<AuthResult> |
| Sign in | FirebaseAuth.SignInWithEmailAndPasswordAsync(string, string) | Task<AuthResult> |
| Send verification | FirebaseUser.SendEmailVerificationAsync() | Task |
| Reload | FirebaseUser.ReloadAsync() | Task |
| Reset email | FirebaseAuth.SendPasswordResetEmailAsync(string) | Task |
| Sign out | FirebaseAuth.SignOut() | void |
| Token | FirebaseUser.TokenAsync(bool forceRefresh) | Task<string> |
| Session listener | FirebaseAuth.StateChanged | EventHandler |
| Token listener | FirebaseAuth.IdTokenChanged | EventHandler |
| Identity/provider | FirebaseAuth.CurrentUser; FirebaseUser.IsAnonymous, IsEmailVerified, ProviderData; IUserInfo.ProviderId | Current SDK state |
| Email credential (AUTH-03 only) | EmailAuthProvider.GetCredential(string,string); FirebaseUser.LinkWithCredentialAsync(Credential) | Credential; Task<AuthResult> |

EmailAuthProvider.ProviderId, GoogleAuthProvider.ProviderId and FacebookAuthProvider.ProviderId exist in the installed metadata; their getters call native SDK constants. Do not execute them during this audit. ProviderData is a collection of linked providers, not proof of which provider was used for the latest login. No email-presence heuristic. The adapter will prioritize IsAnonymous, then password-provider membership (compare to EmailAuthProvider.ProviderId), and retain other provider IDs for future classification. Unknown metadata is not treated as verified Email. Phone provider metadata is future scope; no provider integration is added.

EMAIL_PASSWORD_PROVIDER_STATUS=UNKNOWN. Local SDK/app configuration does not prove server-side provider enablement. Before authorized live tests, inspect the correct TEST project's Authentication sign-in method settings and confirm Email/Password (password sign-in, not merely email-link). Also verify password policy, email templates/action handler, quotas and allowed return domains. No remote configuration or credential store was queried.

## Backend and Player compatibility

Inspected server/domino/src/main/kotlin/com/teamfho/domino/security/FirebaseAdminTokenVerifier.kt: verifyIdToken(token, true) checks tokens with the configured Firebase app; getUser(decoded.uid) checks disabled state and identity equality. Provider data classifies any non-anonymous linked provider as registered. The authoritative key is the decoded Firebase UID, never email or client-selected Player ID.

PlayerBootstrapService.bootstrap delegates to FirestorePlayerFoundationRepository.ensure for both account types. ensure reads players/{uid} and its wallet in a transaction, creates missing Player/wallet with REGISTERED for non-anonymous identity, and preserves existing records/balances. It may update lastSeen and already supports GUEST-to-REGISTERED promotion; this does not authorize linking in AUTH-02. A new registered Player currently receives the existing generated Guest-style display alias; that naming does not imply anonymous authentication and is not changed here.

EMAIL_USER_BACKEND_COMPATIBILITY=YES
BOOTSTRAP_ANONYMOUS_SPECIFIC=NO
EMAIL_VERIFIED_USER_CAN_REUSE_BOOTSTRAP=YES
AUTH_02_BACKEND_REQUIRED=NO
AUTH_02_SCHEMA_CHANGE_REQUIRED=NO

Important boundary: the current backend verifier does NOT require email verification. Proposed enforcement is the production client routing/bootstrap gate, not a server security guarantee against a modified client. If server-enforced rejection of unverified users becomes a requirement, it needs separate backend authorization; it is not silently included here. No client emailVerified flag is sent as authorization to the backend.

## Minimal architecture and routing

Current ProductionAuthRouter only sees PlayerIdentity.Uid and IsAnonymous. FirebaseSdkClient.Snapshot drops provider/verification information; FirebaseAuthService memoizes ready identity; the router currently bootstraps all non-null users. These are the exact gaps to extend, not reasons to replace Auth-01.

Propose a separate immutable FirebaseAuthSessionSnapshot (UID internal only, anonymous flag, provider IDs, verification status) and narrow email-session interface on the existing FirebaseSdkClient. Keep PlayerIdentity and existing backend DTOs stable. Extend FirebaseAuthService to adopt a newly authenticated SDK user and refresh this snapshot without retaining stale initialization results. All restore/register/sign-in/verification completions pass through one router classification method before any Player initialization or lifecycle startup.

| State | Required result |
|---|---|
| No current user | Welcome; zero anonymous sign-in and bootstrap calls |
| Restored anonymous | Existing bootstrap then temporary App Shell |
| Password provider, not verified | Verification Pending; zero Player/backend/realtime/reward calls |
| Password provider, verified | Existing token/bootstrap then temporary App Shell |
| Unknown/unreadable provider state | Safe unavailable/retry state; no assumed Email verification |
| Future Google/Phone/Facebook | No new sign-in implementation; classification isolated from password rules |

Add VerificationPending plus EmailEntry/Register/SignIn/Reset UI routes to the existing router/host; keep TemporaryAppShellPolicy and the future onboarding hook. No persisted onboarding flag. StateChanged and IdTokenChanged subscriptions are lifecycle-owned and removed on disposal; callbacks classify/reconcile through the same serialized operation gate, never independently start another bootstrap. Token refresh callbacks must not recursively trigger refresh. Session generation and UID checks discard stale asynchronous completions after cancel/session replacement. Firebase SDK tasks may complete after client cancellation: keep mutation operations reserved until settled and re-read CurrentUser before permitting another operation.

## Verification completion: exact sequence

On I've Verified, reserve operation and expected session generation/UID internally. Await currentUser.ReloadAsync(); reacquire FirebaseAuth.CurrentUser and require the same UID, non-anonymous password provider and current IsEmailVerified. If false, stay Verification Pending with a gentle not-yet-verified message and no bootstrap. If true, call the existing guarded IAuthTokenProvider.GetIdTokenAsync(true, cancellationToken), implemented by FirebaseIdTokens and FirebaseUser.TokenAsync(true). Recheck session/UID after await; discard token from local variables after use and never display it. Then reuse PlayerService.InitializeAsync or the existing failed-bootstrap retry path, require SYNCED/confirmed snapshots and UID agreement, and apply TemporaryAppShellPolicy.

Reload refreshes user data; token refresh refreshes the credential used by the backend. Do not assume ReloadAsync refreshes the token. Forced refresh is a deliberate design requirement to avoid cached claims after verification, although today's backend does not inspect email_verified. Existing DominoApiClient uses a cached valid token initially and forces one refresh after HTTP401; do not rely on that 401 to discover verification. Any reload/token error prevents bootstrap and exposes safe retry. On restored Email, refresh user data before classification; do not treat StateChanged alone as verification evidence. Auth-01 Guest restore remains unchanged.

## AUTH-02A — registration and verification

Welcome Continue with Email -> Email Entry with Create Account / Sign In -> dedicated Register form (Email, Password, Confirm Password). Footer Sign In is reserved for direct Email Sign In in AUTH-02B; do not introduce a provider selector. During the independent AUTH-02A checkpoint, unimplemented sign-in/reset actions must be explicitly unavailable, not falsely functional.

Registration: validate locally, reserve global auth mutation/form operation, inspect SDK CurrentUser immediately before create. Only NO_SESSION may create. Await CreateUserWithEmailAndPasswordAsync; adopt the current/result user only with matching UID and session generation. Classify first. Normal unverified result routes Verification Pending before sending verification. SendEmailVerificationAsync uses that same authenticated user. Do not bootstrap or wait for an email-send success to preserve the authenticated session.

Partial registration: account created but verification send fails -> retain EMAIL_UNVERIFIED and offer Retry verification email. Never run create again. If create completion is ambiguous after network failure, inspect CurrentUser; if no session exists, do not claim account existence or automatically retry indefinitely. Offer explicit retry/sign-in recovery; no merge.

Verification Pending: Arrow Left, Check your email, masked email displayed only in this view. Show 'We sent...' only after send success; on restored session with unknown delivery history say 'Verify your email to continue.' Actions I've Verified and Resend Email share serialization to prevent races. Resend uses SendEmailVerificationAsync on the same current user; local proposed 60-second cooldown is UX policy, not an asserted Firebase quota. Rate-limit/network failures remain Pending, retain identity, permit retry when appropriate; no automatic mail on every restart.

Back / Use another email: explicit confirmation that the email account remains but this session will end. After confirmation, verify the same unverified non-anonymous password identity, drain active operations, Firebase SignOut, clear auth snapshot and dispose/reset Player/service lifetime, then route Email Entry/Welcome. Never silently show a no-session page while the unverified session persists. Cancel confirmation leaves Pending. No account deletion. Existing LogoutGuestAsync is Guest-specific and MUST NOT be called for this path; add a guarded unverified-email cancellation entry later. This audit does not execute any sign-out.

Restart before verification -> reload restored password identity -> Verification Pending. If already verified externally, refreshed classification -> token refresh -> bootstrap. Verification send failure survives as an unverified session, not an instruction to recreate an account.

## AUTH-02B — sign-in and reset

Footer Sign In -> Email Sign In; Email Entry Sign In reaches the same form. Email/password -> SignInWithEmailAndPasswordAsync -> adopt/reload current SDK identity -> central classification: unverified Pending; verified existing bootstrap -> App Shell. Bootstrap failure offers retry with the same Firebase user; do not repeat sign-in or create another identity.

Forgot password -> Reset Password form -> SendPasswordResetEmailAsync. Normal completion and UserNotFound produce the same privacy-safe response: 'If an account exists for this email, reset instructions have been sent.' Network/rate-limit errors may show generic retry without disclosing existence. Do not use FetchProvidersForEmailAsync as an enumeration preflight. Firebase-hosted action is the proposed reset completion: emailed link -> hosted action -> changed password -> manually return to app/Sign In. Actual configured handler/return links are UNKNOWN locally and must be verified later; no deep-link promise and no custom backend reset handler.

EmailAlreadyInUse during registration -> 'Email already registered' with Sign In / Forgot Password / Use Another Email; only show this when Firebase returns that typed condition. Privacy protection may instead return a more generic error; do not infer existence. No user details, merge or automatic account switching.

## Guest guard and AUTH-03

Authoritative guard is in FirebaseAuthService/email operation entry immediately before SDK create (and sign-in), with a defensive adapter/session-generation check, not merely disabled UI. CurrentUser.IsAnonymous -> preserve current UID/Player and reject AUTH-02 account replacement with 'Guest upgrade is not available yet.' Do not silently sign out, create a second identity, or switch to an existing account. Also reject replacement of any already authenticated non-Guest session until explicit supported cancellation/logout. UI may describe AUTH-03 but does not navigate to an unimplemented upgrade.

AUTH-03 only: Guest A -> link Email credential -> UID A and Player A remain; credential owned by another identity -> no merge. No LinkWithCredentialAsync call in AUTH-02. Current Guest logout for testing is prohibited. Use existing isolated Editor Welcome preview and injected fake session adapters.

## Inputs, credentials and errors

EMAIL_NORMALIZATION=TRIM_OUTER_WHITESPACE_ONLY. No forced lowercase of the whole address, dot/plus removal, Unicode rewriting or provider-specific canonicalization. No existing Email normalization convention was found. Installed managed SDK forwards strings to native SDK; server normalization is not established by that metadata. Use basic nonempty/syntax feedback; Firebase remains authoritative.

PASSWORD_MIN_LENGTH=PROJECT_POLICY_UNKNOWN. Firebase documentation gives default minimum 6, configurable 6–30; this is not proof that this TEST project uses 6. Confirm project policy before live acceptance. Registration client validates nonempty, exact confirmation equality and verified project policy; no invented complexity. Sign-in should not reject existing valid passwords using a newly strengthened registration policy. Never trim/normalize passwords. Mask inputs, clear fields and release references after submit/cancel; managed strings cannot promise deterministic memory zeroing. No PlayerPrefs/files/logs/analytics/backend password storage; Firebase alone receives credentials. Firebase SDK owns session persistence, not an app password cache.

Map Firebase.FirebaseException.ErrorCode (unwrap task AggregateException safely) to installed AuthError enum, then stable application codes:

| SDK code | Application code |
|---|---|
| InvalidEmail (11) | INVALID_EMAIL |
| WeakPassword (23) | WEAK_PASSWORD |
| EmailAlreadyInUse (8) | EMAIL_ALREADY_IN_USE |
| InvalidCredential (4), WrongPassword (12), UserNotFound (14) on sign-in | INVALID_CREDENTIAL |
| UserDisabled (5) | USER_DISABLED |
| TooManyRequests (13), QuotaExceeded (39) | TOO_MANY_REQUESTS |
| NetworkRequestFailed (19) | NETWORK_ERROR |
| OperationNotAllowed (7) | CONFIGURATION_UNAVAILABLE (safe generic UI) |
| Other/unrecognized | UNKNOWN |

UserNotFound reset handling is privacy-success, not sign-in behavior. Local mismatch/empty input has local validation errors, not fabricated SDK errors. Cancellation is not UNKNOWN. Never show raw exception messages, request payloads or credentials.

Each form uses IDLE / SUBMITTING / SUCCESS_OR_ROUTING / ERROR; relevant CTA disabled during SUBMITTING. Router/session-wide reservation prevents concurrent create, sign-in, reload, resend, reset and cancel conflicts. Repeat taps share or reject the in-flight operation. Retry after mail failure retries only mail; retry after verified bootstrap failure retries only bootstrap.

## Proposed file impact (not implemented)

All paths below are relative to client/DominoGame/Assets/_Domino/Scripts unless stated otherwise.

NEW: Infrastructure/Firebase/IFirebaseEmailSessionClient.cs; Identity/FirebaseAuthSessionSnapshot.cs; Auth/EmailAuthState.cs (typed errors/form state); UI/AppShell/ProductionEmailAuthViews.cs (entry/register/pending in A; sign-in/reset in B); UI/AppShell/Editor/ProductionEmailAuthValidation.cs and metadata; client/Validation/Auth02RegisterVerificationTests.cs and runner in A; Auth02SignInResetTests.cs and runner in B.

MODIFY: existing FirebaseSdkClient.cs (narrow interface implementation, metadata mapping); FirebaseAuthService.cs (guarded operations/session adoption); ProductionAuthRouter.cs (classification gate, serialized operations); ProductionAuthHost.cs (render new routes, cancellation lifetime); ProductionWelcomeView.cs (Email/footer callbacks only, preserve approved visuals); ApplicationServices.cs (composition and guarded session-reset lifecycle); existing Welcome validator expectations only when placeholders become authorized routes. Keep IFirebaseClient and PlayerIdentity contracts stable using the new interface/snapshot. No backend/DTO/schema/theme/root-page/config changes expected. Final filenames can be consolidated without changing responsibilities.

## Validation and release split

AUTH-02A checkpoint: register + verification + restore gate + cancel + anonymous guard; no real sign-in/reset implementation. AUTH-02B checkpoint: sign-in + forgot password; retain A tests. AUTH-03 owns upgrade/linking, entirely separate.

A isolated tests: valid registration; invalid email; weak password; confirmation mismatch; email-in-use mapping; create double submit; send success/failure; resend success/rate limit; verified-check false/true; reload/token failures; unverified restart; verified bootstrap success/failure/same-session retry; Guest guard; preexisting registered guard; no premature Player/realtime/rewards; UID/session switch during awaits; cancellation/late completion; no automatic resend on restart; ambiguous create recovery.

B isolated tests: verified/unverified sign-in; invalid credentials (including privacy variants); disabled user; network failure; double submit; forgot password; identical reset success/UserNotFound response; rate limiting; session restore; Guest guard; no merges; no password/log leakage.

Integration: compose existing service/router/PlayerService with fake SDK and transport boundaries; verify one bootstrap and exact UID correlation. Unity integration uses isolated adapters only, validates transitions and Back behavior without current Guest logout. Validate all pages at 375x667, 393x852, 412x915, 430x932, 480x1040, 600x960, 768x1024, 834x1194: ModernSocialPremium, max-width620, Arrow Left, no bottom nav, scroll/keyboard reachability, loading, masked fields, no visual Auth-01 regressions.

Controlled TEST Firebase tests require separate explicit authorization and dedicated identities/email inboxes, plus provider/policy/template checks. This design audit created no accounts, sent no email, invoked no Firebase auth operation, changed no configuration and did not sign out the current Guest.

## Evidence references

Local: Firebase Auth manifest/Dependencies and Firebase.Auth.dll metadata; Scripts/Identity/PlayerIdentity.cs; Infrastructure/Firebase/{FirebaseSdkClient,FirebaseAuthService,FirebaseIdTokens}.cs; Infrastructure/Api/DominoApiClient.cs; Auth/ProductionAuthRouter.cs; UI/AppShell/ProductionAuthHost.cs; Infrastructure/ApplicationServices.cs; server security FirebaseAdminTokenVerifier.kt and player PlayerBootstrapService.kt / FirestorePlayerFoundationRepository.kt.

Official documentation supplements installed metadata only for service semantics/configuration:
- [Unity FirebaseUser reference](https://firebase.google.com/docs/reference/unity/class/firebase/auth/firebase-user): ReloadAsync updates profile data, IsEmailVerified exposes verification, TokenAsync(true) forces token refresh.
- [Unity password authentication](https://firebase.google.com/docs/auth/unity/password-auth): password-policy default and configurable minimum; project policy must be checked separately.
- [Firebase email action handlers](https://firebase.google.com/docs/auth/custom-email-handler): default hosted action handling; project customization is not confirmed here.

## Audit outcome

AUTH_02_BACKEND_REQUIRED=NO
AUTH_02_SCHEMA_CHANGE_REQUIRED=NO
EMAIL_PASSWORD_PROVIDER_STATUS=UNKNOWN
PASSWORD_PROJECT_POLICY=UNKNOWN
PROJECT_EMAIL_ACTION_HANDLER=UNKNOWN
PRODUCT_SOURCE_FILES_CHANGED=0
BACKEND_FILES_CHANGED=0
CONFIG_FILES_CHANGED=0
PREEXISTING_PROTECTED_FILES_MODIFIED=0/102
RAW_UIDS=0
REAL_EMAILS=0
PASSWORDS=0
TOKENS=0
SECRETS=0
COMMIT=NONE
PUSH=NONE
DEPLOY=NO
AUTH_02_IMPLEMENTATION_STARTED=NO
NEXT=AUTH-02_TECHNICAL_DESIGN_REVIEW
