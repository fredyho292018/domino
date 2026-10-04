# GUEST-ACCOUNT-01 — Protection and email linking audit

Date: 2026-10-03. Environment: TEST. Mode: read-only source/SDK contract audit and implementation design. No live authentication operations or account inspection performed.

Repository HEAD: `aca8b2ebef57177258789ec464f7c800fbeb338c`. The deployed TEST SHA is supplied by the authorization; deployment was not probed again in this audit. PLAYER-UI-01/01E completion and PLAYER-IDENTITY-01 completion are user-supplied prerequisites, not newly executed results here. The prior pending manual B-login request is superseded by this audit; no session interaction is needed.

## Decision

Email protection can use the installed Firebase SDK's credential-link operation on the **current** anonymous user. Firebase documents identity continuity across linked providers. Backend document identity is exactly the verified Firebase UID. No new Player mapping, backend endpoint, schema, or account merge is needed for a healthy existing Player.

This is feasibility from source and SDK contract, **not live linking proof**. Implementation and disposable-account live validation remain separately authorized gates. Never use registration or email sign-in as a fallback for a failed link.

Three integration details must be handled explicitly:

1. FirebaseAuthService caches an immutable PlayerIdentity and initialization task. A successful SDK link must be adopted into that same owner; a provider change with the same UID must invalidate the relevant routing generation.
2. PlayerService.InitializeAsync short-circuits when already SYNCED. The post-verification path needs an explicit same-session refresh using the existing RefreshConfirmedAsync seam, not an assumed second InitializeAsync request.
3. The existing backend already upgrades Player.accountType from GUEST to REGISTERED, updates updatedAt and can update lastSeenAt during bootstrap. These are expected metadata changes; a claim that every Player field remains byte-identical would be false.

## Source map and current owners

Paths below are relative to repository root. These are production sources inspected, not proposed duplicate owners.

| Area | Source / owner | Finding |
|---|---|---|
| SDK boundary | client/DominoGame/Assets/_Domino/Scripts/Infrastructure/Firebase/FirebaseSdkClient.cs | CurrentUser, GetSession, CreateEmailAsync, SignInEmailAsync, ReloadEmailAsync, SendVerificationAsync, SignOut; no link adapter yet |
| Auth lifecycle | client/DominoGame/Assets/_Domino/Scripts/Infrastructure/Firebase/FirebaseAuthService.cs | Single application auth owner; RestoreAsync, ContinueAsGuestAsync, RunEmail, Adopt, RegisterEmailAsync, SignInEmailAsync, SignOut |
| Provider snapshot | client/DominoGame/Assets/_Domino/Scripts/Identity/EmailAuthState.cs | UID, IsAnonymous, IsEmailVerified, IsPasswordProvider, private DisplayEmail; no complete provider-ID list |
| Interfaces | client/DominoGame/Assets/_Domino/Scripts/Infrastructure/Firebase/IFirebaseClient.cs | Existing email creation/access interfaces do not expose linking |
| Composition | client/DominoGame/Assets/_Domino/Scripts/Infrastructure/ApplicationServices.cs | Owns one FirebaseAuthService, PlayerService, routing composition and logout service per application lifetime |
| Entry/render | client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/ProductionAuthEntry.cs and ProductionAuthHost.cs | One mounted production host; renders routes and holds one PlayerPresentationSource |
| Forms | client/DominoGame/Assets/_Domino/Scripts/Auth/ProductionAuthRouter.cs | Registration/sign-in/verification commands, busy guard; authenticated navigation to ordinary email forms is rejected |
| Authenticated destination | client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/ProductionRoutingComposition.cs and Scripts/Auth/AuthenticatedRoutingOrchestrator.cs | Restore, verification gate, Player bootstrap, authoritative onboarding read, Home/onboarding destination |
| Logout | client/DominoGame/Assets/_Domino/Scripts/Auth/ProductionLogoutService.cs | Prepare, stop, clear, signOut, welcome transaction; confirmation UID checked before and after prepare |
| Confirmation UI | client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/ProductionLogoutConfirmation.cs | Existing responsive page with Cancel and destructive Sign Out; reuse this infrastructure, not a new modal framework |
| Email/password UI | client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/ProductionEmailView.cs, ProductionPasswordField.cs, AuthStatusMessage.cs | Reusable visual fields, eye controls, feedback and busy presentation; Create Account navigation/submit/cancel semantics cannot be reused unchanged |

AUTH_STATE_OWNER=ApplicationServices.Identity:FirebaseAuthService
AUTH_STATE_OWNER_COUNT=1
REGISTER_OWNER=ProductionAuthRouter.RegisterAsync -> FirebaseAuthService.RegisterEmailAsync -> FirebaseSdkClient.CreateEmailAsync
SIGN_IN_OWNER=ProductionAuthRouter.SignInEmailAsync -> FirebaseAuthService.SignInEmailAsync -> FirebaseSdkClient.SignInEmailAsync
LOGOUT_OWNER=ApplicationServices.Logout:ProductionLogoutService
SESSION_RESTORE_OWNER=FirebaseAuthService.RestoreAsync via ProductionRoutingComposition

## Anonymous versus recoverable identity

FirebaseSdkClient.GetSession reads FirebaseAuth.CurrentUser.IsAnonymous directly; Snapshot copies this into PlayerIdentity. ProductionLogoutService.Request currently uses Current.IsAnonymous. Neither path classifies from displayName or Player.accountType.

ANONYMOUS_AUTH_SOURCE=FirebaseAuth.CurrentUser.IsAnonymous
GUEST_ALIAS_DOES_NOT_IMPLY_ANONYMOUS_AUTH=YES
ANONYMOUS_AUTH_CLASSIFICATION_SERVER_PLAYER_GUESS=NO

Target classification:

- NO_SESSION: no current Firebase user.
- ANONYMOUS_UNLINKED: SDK IsAnonymous=true and no supported recoverable provider in ProviderData.
- LINKED_UNVERIFIED: password provider linked, IsAnonymous=false, IsEmailVerified=false. Password authentication is available, but product access remains Verification Pending.
- RECOVERABLE: non-anonymous identity with an enabled/supported sign-in method that the application can actually use. For phase 01 this is password. Verification is a separate access-policy axis, not the existence of credentials.
- UNKNOWN/CONFLICT: missing, stale, contradictory or unsupported provider state. Do not silently classify this as safe-to-abandon Guest or as guaranteed recoverable. Reload/reconcile; no automatic logout.

ProviderData can identify google.com/apple.com, but current production Google/Apple sign-in/linking is not implemented. Do not promise in-app recovery merely because IsAnonymous=false. Extend the existing snapshot with sanitized provider capabilities if necessary, without persisting them to Player or adding another auth owner. Unknown/disabled accounts have no absolute recovery guarantee.

ANONYMOUS_LOGOUT_RECOVERY_GUARANTEED=NO
No project recovery credential, anonymous account-reclaim flow or user-accessible recovery mechanism was found in the inspected production auth paths. SDK persisted session restoration before logout is not recovery after SignOut. A Guest-like alias is unrelated to this limitation.

## Existing flows

Email: production Create Account -> EmailAuthRules.Validate -> RegisterEmailAsync (requires no current user) -> CreateUserWithEmailAndPasswordAsync -> adopt returned user -> Verification Pending -> SendEmailVerificationAsync. I've Verified reloads user and refreshes verified token -> authenticated destination -> bootstrap -> GET onboarding -> Home if COMPLETED, otherwise existing authoritative onboarding step. SignInEmailAsync similarly adopts the password identity then follows the verification/destination gate. No email-to-Player lookup is used.

Guest: explicit Continue as Guest -> FirebaseAuthService.ContinueAsGuestAsync -> existing user reused, or SignInAnonymouslyAsync only if absent -> authenticated destination -> PlayerService -> POST /api/v1/player/bootstrap -> UID-keyed foundation. New root creates foundation atomically; existing incomplete root is not repaired by linking.

Restore: Firebase SDK owns persistence; RestoreAsync reads CurrentUser without creating a Guest. ProductionAuthEntry mounts the host, host Bind calls router.RestoreAsync, and the destination reads current identity/provider verification before routing. Current project auth wrapper uses explicit snapshots and commands, not a StateChanged/IdTokenChanged subscription in these inspected owners. Do not rely on an SDK event to update its cached Current automatically.

Actual logout order:

1. Request captures current UID and SDK-derived anonymous flag; confirmation shown. Cancel changes only confirmation state.
2. ConfirmAsync checks UID, prepares safe logout: no active gameplay; cancel/reconcile matchmaking queue (GET and potentially DELETE queue). Normal logout therefore is not universally backend-write-free.
3. Recheck UID; stop/dispose auth router and outstanding routing work.
4. Shutdown disposes PlayerService (clears domain/presentation authority), rewards/ads/realtime and application lifetime. It intentionally does not sign Firebase out on ordinary application exit.
5. FirebaseAuthService.SignOut -> FirebaseSdkClient.SignOut -> SDK SignOut clears persisted SDK login; wrapper clears Current/init/error/createdEmailUid. There is no PlayerPrefs.DeleteAll or remote account deletion here.
6. Reset/Start application services -> SessionReplaced -> host Rebind disposes old PlayerPresentationSource -> RestoreAsync finds no user -> Welcome. Projection also becomes empty for Welcome/VerificationPending.

The existing Cancel handler rebuilds the shell and selects Menu. If protection is offered from Profile/onboarding too, returning to the originating route must be explicit; current host Cancel behavior is not a universal keep-current-route implementation.

## Installed Firebase capability and contract evidence

Firebase Unity Auth package: **13.16.0**, evidenced by Assets/Firebase/Editor/FirebaseAuth_version-13.16.0_manifest.txt and dependency package paths. Managed assembly version itself is 0.0.0.0; it is not the package version.

Read-only Mono.Cecil metadata inspection of Assets/Firebase/Plugins/Firebase.Auth.dll confirmed:

```csharp
Firebase.Auth.Credential EmailAuthProvider.GetCredential(string, string)
Task<Firebase.Auth.AuthResult> FirebaseUser.LinkWithCredentialAsync(Credential)
Task FirebaseUser.ReloadAsync()
Task<string> FirebaseUser.TokenAsync(bool)
Task FirebaseUser.SendEmailVerificationAsync()
```

No SDK method was executed. Ordinary reflection could not load this assembly into the shell process due to architecture; metadata-only inspection succeeded without initializing Firebase.

The official [Unity account-linking contract](https://firebase.google.com/docs/auth/unity/account-linking) documents a stable Firebase user ID across linked providers and linking credentials to CurrentUser. It also rejects credentials belonging to another account. This is the UID-preservation contract; add runtime equality assertions rather than a migration strategy. The guide's merge example is outside this product scope and must not be copied.

The [FirebaseUser reference](https://firebase.google.com/docs/reference/unity/class/firebase/auth/firebase-user) identifies IsAnonymous, ProviderData, IsEmailVerified and the link/reload/token operations. The [anonymous auth guide](https://firebase.google.com/docs/auth/unity/anonymous-auth) describes upgrading anonymous access through linking so existing data remains usable.

The linking guide also links a [known issue, #7675](https://github.com/firebase/firebase-js-sdk/issues/7675), currently marked closed, concerning OPERATION_NOT_ALLOWED during anonymous credential linking. It does not establish a current Unity 13.16.0 failure in this TEST project. Keep an explicit unsupported/configuration error path and disposable live gate; do not weaken project security settings or substitute account creation as a workaround.

FIREBASE_LINK_API_AVAILABLE=YES_INSTALLED_METADATA
FIREBASE_LINK_API=CurrentUser.LinkWithCredentialAsync(EmailAuthProvider.GetCredential(email,password))
FIREBASE_AUTH_SDK_VERSION=13.16.0
FIREBASE_UID_CHANGED_AFTER_LINK=NO_BY_SDK_CONTRACT; LIVE_PROOF_PENDING
PROTECT_ACCOUNT_CREATES_SECOND_FIREBASE_USER=NO_TARGET

## Backend identity, preservation and actual metadata effects

Sources: server/domino/src/main/kotlin/com/teamfho/domino/security/FirebaseAuthenticationFilter.kt, FirebaseAdminTokenVerifier.kt; player/PlayerController.kt, PlayerBootstrapService.kt, FirestorePlayerFoundationRepository.kt, FirestoreFoundationMapping.kt.

FirebaseAdminTokenVerifier verifies token/project/revocation, reads Firebase user by decoded UID, checks disabled/UID equality, and derives registered/provider/verified state from Firebase Admin user data. Controller receives this principal; repository ensure uses players/{identity.uid}. There is no new ID allocator for upgrading an existing root.

BACKEND_PLAYER_LOOKUP_AUTHORITY=VERIFIED_FIREBASE_PRINCIPAL_UID
FIREBASE_UID_TO_PLAYER_MAPPING=players/{uid}; Player.uid=uid
SAME_UID_RETURNS_SAME_PLAYER=YES_FOR_EXISTING_VALID_ROOT
BACKEND_CHANGE_REQUIRED=NO_FOR_LINKING_AND_EXISTING_MAPPING
SCHEMA_CHANGE_REQUIRED=NO
PLAYER_AUTH_PROVIDER_PERSISTENCE_REQUIRED=NO_NEW_FIELDS

Player already has accountType, a coarse projection, not the client protection authority. Do not add isAnonymous/emailLinked/provider copies to the schema. The repository's existing GUEST -> REGISTERED upgrade runs on a non-anonymous principal and changes updatedAt. lastSeenAt is throttled to at least fifteen minutes. It does not rename the alias, alter profileRevision, createdAt, existing wallet, preferences or onboarding. Existing root skips new alias reservation and onboarding creation.

Important limits:

- An existing root with a missing wallet currently creates wallet/main during ensure. Do not claim zero domain writes for a malformed fixture. Future disposable fixture must have a complete foundation before linking; reject incomplete legacy state as a separate repair concern.
- EntitlementService.bootstrap reads/resolves existing state; trial activation remains a separate explicit operation. Time-based effective entitlement expiry can legitimately change presentation without modifying grants or trial dates. Compare stored grants/dates, not an unconditional Premium boolean.
- ApplicationServices starts session/reward lifecycles upon Home. Existing reward recovery can perform its own work. A test with pending rewards is unsuitable for proving link-only zero wallet/entitlement writes; require a controlled fixture without outstanding work and compare readbacks.
- Same UID does not repair invalid/missing Player/onboarding documents. A 409 remains a domain problem; no new Player or repair fallback is allowed.

Preserve exact Player UID, alias and reservation ownership, createdAt, private first/last names, country, preferred language, Coach, experience, onboarding status/version/revision, wallet balances, stored membership/grants/trial consumed/dates, inventory, social references, stats/history/matches. Only the existing accountType/updatedAt/lastSeenAt metadata changes are expected after the post-link bootstrap. There is no reason to write profile, alias, onboarding or entitlement endpoints as part of linking.

## Reference coupling

| Subsystem | Existing authority | Consequence of unchanged UID |
|---|---|---|
| Social | social/FirestoreSocialRepository.kt: players/{uid}/publicIdentity/current, publicPlayerProfiles with internalUid/publicPlayerId, friend-code mapping | Public identifiers and lookup ownership stay unchanged |
| Friendships/requests | social/Friendships.kt: socialPairs and friendships pair IDs, senderUid/recipientUid, players/{uid}/friends/{otherUid} | Forward/inverse references retained; no merge or rewrite |
| Blocks/follows | FirestoreSocialRepository.kt, Follows.kt: UID-scoped blocks/blockedBy/following/followers | UID-coupled references remain valid |
| Match | match/MatchModels.kt, FirestoreMatchRepository.kt: participants.playerUid, seat/team references and persisted events | Same participant authorization; no event or alias-snapshot rewriting |
| History/replay | players/{uid}/matchHistory/{matchId}; ReplayService.authorized checks participant.playerUid, replay sessions bind uid | Original ownership/history retained |
| Alias | PlayerAliasReservations, PlayerDisplayNameService | No claim/update operation invoked by link |

SOCIAL_REFERENCE_AUTHORITY=INTERNAL_UID_PLUS_PUBLIC_PLAYER_ID
SOCIAL_REFERENCES_UID_COUPLED=YES
MATCH_REFERENCE_AUTHORITY=PARTICIPANT_PLAYER_UID_AND_SEAT
MATCH_REFERENCES_UID_COUPLED=YES
HISTORY_REFERENCE_AUTHORITY=PLAYER_UID
HISTORY_REFERENCES_UID_COUPLED=YES

## Reuse, validation and error contract

Reuse EmailAuthRules, ProductionPasswordField (48 field/44 touch), theme and AuthStatusMessage. Keep one password policy: normalized email is trimmed, no whitespace, MailAddress parse round-trips; password length 6..4096; exact confirmation equality. Firebase's configured server policy remains authoritative; its current console policy was not queried. Do not invent a new policy for linking.

EXISTING_EMAIL_FORM_REUSABLE=COMPONENTS_YES; REGISTER_FLOW_NO
EXISTING_EMAIL_VALIDATION_REUSABLE=YES
EXISTING_PASSWORD_VALIDATION_REUSABLE=YES

ProductionEmailView is route-dependent and has private field builders, hardcoded Register submit labels, email-in-use recovery navigation and cancellation semantics. Safely extract/reuse visual composition or add an explicit Protect mode with separate callbacks; never wire Protect to RegisterAsync or SignInEmailAsync. Registration password errors already map to password fields; email errors currently use semantic status rather than a complete reusable inline-email-error contract. Protect needs explicit inline email error mapping, with the shared semantic feedback retained.

Installed AuthError metadata and required mapping:

| Error | Existing handling | Protect-account target |
|---|---|---|
| InvalidEmail=11 | InvalidEmail | Email field; preserve session |
| WeakPassword=23 | WeakPassword | Password field; preserve session |
| EmailAlreadyInUse=8 | EmailAlreadyInUse | Email conflict; remain A; no login/merge |
| CredentialAlreadyInUse=10 | Currently Unknown | Same conflict category; no automatic account switch |
| AccountExistsWithDifferentCredentials=6 | Currently Unknown | Credential/account conflict; no automatic account switch |
| ProviderAlreadyLinked=15 | Currently Unknown | Reconcile current same-UID provider snapshot; never assume success from error code alone |
| NetworkRequestFailed=19 | NetworkError | Technical failure or unknown link outcome; reconcile before retry |
| OperationNotAllowed=7, EmailChangeNeedsVerification=78, UnverifiedEmail=74 | Currently Unknown | Explicit unsupported/verification recovery classification; no configuration bypass |
| Throttling 13/39 | TooManyRequests | Preserve session; wait/user retry |
| Cancellation, unknown error | Generic | Determine whether link committed; no false anonymous/success claim |

Do not log exception bodies, SDK messages containing input, passwords, tokens, email or UID. Never store credentials in reports, preferences or validation fixtures. Clear password widgets on submission/detachment as existing forms do. Managed strings cannot promise immediate memory zeroization; avoid additional retention.

## Proposed link and verification sequence

1. Explicit Protect entry while authenticated anonymous. Read current SDK snapshot and pin expected UID + application session generation. Require no active game/queue and no conflicting auth/domain save; do not cancel a queue or log out merely to open the form.
2. Preserve originating route; show shared email/password/confirmation UI. Cancel before submit returns to that route with no writes. Prevent duplicate submit, logout and identity replacement while linking.
3. In FirebaseAuthService's serialized operation boundary call a new typed SDK adapter method LinkEmailAsync(expectedUid,...). Recheck CurrentUser and IsAnonymous; construct EmailAuthProvider credential and call current user's LinkWithCredentialAsync. Never CreateUserWithEmailAndPasswordAsync, SignInWithEmailAndPasswordAsync, DeleteAsync or UnlinkAsync.
4. Validate result.User and CurrentUser UID against the pinned UID. Refresh/reconcile provider snapshot and adopt it in the existing auth owner (cached Current.IsAnonymous must become false). UID mismatch is a hard stop; no bootstrap, migration or merge. Discard stale completion from a superseded generation, but remember the remote link may already have committed.
5. Treat successful password linking and email verification as separate outcomes. A newly supplied password email is not proof of email ownership; expected unverified until confirmation. Read IsEmailVerified after reload and never set it locally. IsAnonymous=false + password provider + not verified => LINKED_UNVERIFIED.
6. Transition to Verification Pending before allowing further gameplay/domain activity. Suspend old session lifecycles/in-flight onboarding saves without calling logout or discarding remote progress. Existing production lifecycles start once on Home and have no explicit link suspension flow; implement that narrowly. Send verification email; failure stays LINKED_UNVERIFIED with resend, not back to anonymous registration.
7. ReloadAsync on I've Verified. If still unverified, stay pending. Force TokenAsync(true) before subsequent backend use; verify same UID and updated snapshot. Existing RefreshVerifiedTokenAsync supports the verified stage. If an immediate post-link token refresh is used, it needs a dedicated same-UID unverified-capable adapter path; do not weaken the existing verified-only method.
8. After verified state, force one same-Player bootstrap refresh through PlayerService.RefreshConfirmedAsync (or a narrowly exposed auth-owned wrapper). Existing InitializeAsync alone can return its prior SYNCED result. Publish new snapshot only after UID validation; no second state owner.
9. Reset routing epoch and read authoritative onboarding. COMPLETED -> Home; IN_PROGRESS -> same revision/current step; NOT_STARTED -> existing onboarding. Reuse the same presentation source lifecycle safely. No onboarding start/save/complete command, alias claim or trial call is part of linking.
10. On restart/re-login, reload Firebase provider state and apply the same verification policy and UID-keyed bootstrap. Do not depend on an ephemeral local link-completed flag.

POST_LINK_TOKEN_REFRESH_REQUIRED=YES_BEFORE_BACKEND_USE
POST_LINK_USER_RELOAD_REQUIRED=YES_FOR_VERIFICATION_RECONCILIATION
LINKED_EMAIL_VERIFICATION_REQUIRED=YES_PRODUCT_POLICY
LINKED_EMAIL_VERIFIED_IMMEDIATELY=NOT_ASSUMED; EXPECT_FALSE_FOR_NEW_PASSWORD_EMAIL
LINKED_UNVERIFIED_ROUTE=VERIFICATION_PENDING
LINKED_UNVERIFIED_ACCOUNT_RECOVERABILITY=PASSWORD_SIGN_IN_AVAILABLE; PRODUCT_ACCESS_BLOCKED_UNTIL_VERIFIED

Current router blocks unverified password users before bootstrap. Backend OnboardingWriteAuthorization also checks verified password identities, including a formerly anonymous token whose current Firebase record has a password provider. The backend bootstrap itself is not a universal verified-email gate; keep client verification ordering and do not mistake it for enforcement on every backend endpoint.

## Partial success and recovery

- Definite rejection before link: keep anonymous A, same Player/form, field-level error; no signout or bootstrap.
- Timeout/network ambiguity: a server-side link may already have committed. Re-read/reload current user when possible before offering another attempt. If uncertain, expose reconciliation failure and block conflicting actions; do not promise A is still anonymous.
- Verification email failure after link: linked account remains; offer resend with existing throttling semantics. No relink or rollback.
- Token refresh failure: preserve the linked identity, block backend continuation, retry refresh explicitly for the same UID.
- Bootstrap/routing failure: retain linked identity and authoritative data; retry the failed stage for that UID. Never return to Create Account, issue another link, create a Player, or reset onboarding.
- Process exit during operation: on restore infer provider/verification state from Firebase. The operation cancellation does not prove server rollback.
- "Use Another Email" cannot reuse the new-unverified-registration cancellation contract blindly. FirebaseAuthService.CanCancelCreatedEmail is scoped to a newly created email user; linked Guests must not set createdEmailUid. ProductionAuthHost currently passes cancellation allowed=true and routes confirmation through normal logout. Protect verification must deliberately label/route any logout and must not imply it changes the already-linked email. Changing email is out of scope.

POST_LINK_FAILURE_RECOVERY_POLICY=RECONCILE_SAME_UID_AND_RESUME_FAILED_STAGE; NEVER_UNLINK_OR_CREATE
AUTOMATIC_PLAYER_MERGE=NO
ACCOUNT_MERGE_IMPLEMENTED_IN_GUEST_ACCOUNT_01=NO
GOOGLE_LINKING_PHASE=DEFER
APPLE_LINKING_PHASE=DEFER

## Protection UX contract

Reuse ProductionLogoutConfirmation and ProductionRootPage with explicit protection intent, localized copy and semantic buttons. Anonymous target actions: Protect account / Log out anyway / Cancel. ES: Proteger cuenta / Cerrar sesión de todos modos / Cancelar. Cancel stays safe and non-destructive; Log out anyway keeps destructive styling. Protect does not invoke the normal logout orchestrator first.

EN warning: This guest account is not linked to a sign-in method. If you log out, you may not be able to recover this profile and its progress.

ES warning: Esta cuenta de invitado no está vinculada a un método de inicio de sesión. Si cierras sesión, es posible que no puedas recuperar este perfil ni su progreso.

The existing confirmation copy is currently English in source; localization is implementation work, not evidence that an ES dialog already exists.

EXISTING_CONFIRMATION_DIALOG_REUSABLE=YES_WITH_SCOPED_ACTION_EXTENSION
RECOVERABLE_ACCOUNT_LOGOUT=NORMAL_LOGOUT
UNLINKED_ANONYMOUS_LOGOUT=PROTECTION_GATE
LOGOUT_PROTECTION_CLASSIFICATION_SOURCE=FIREBASE_AUTH_STATE
GUEST_LOGOUT_WARNING_CLAIMS_DELETION=NO
PROTECT_ACCOUNT_SIGNS_OUT_FIRST=NO
GUEST_LOGOUT_CANCEL_SIDE_EFFECTS=0_TARGET
ANONYMOUS_FORCE_LOGOUT_ALLOWED=YES_EXPLICIT_CHOICE
FORCE_LOGOUT_DELETES_ACCOUNT=NO
FORCE_LOGOUT_DELETES_PLAYER=NO
PROACTIVE_PROTECT_ACCOUNT_ENTRY_RECOMMENDED=YES_MENU_ACCOUNT_AREA

Prefer one proactive Menu account entry for anonymous users, with the same command as logout protection. Profile can link to it later; no second form/service. Existing Guest-like displayName remains valid after protection and stays the public identity. Auth email is shown only in the private auth/verification context, never as Home/Menu/Profile identity.

## Implementation phases and authorization gates

The supplied attachment ends at section 66 after the literal letter G. The following continuation is this audit's proposal, not additional user authorization.

| Phase | Exact scope | Exit gate |
|---|---|---|
| GUEST-ACCOUNT-01A | Provider classification in existing snapshot/auth owner; protection actions and originating-route cancel; localized warning | Anonymous, password/unverified, supported-provider and unknown-state fixtures; no live logout |
| GUEST-ACCOUNT-01B | Typed SDK link adapter and serialized auth service operation; same-UID checks; precise error/unknown-outcome reconciliation | Deterministic identity preservation and partial-success tests; no live link |
| GUEST-ACCOUNT-01C | Protect UI using shared fields; linked verification; forced same-Player refresh; routing epoch and lifecycle suspension/resume | Existing auth/onboarding/player binding regressions plus eight-size EN/ES isolated fixtures |
| GUEST-ACCOUNT-01D | Separately authorized disposable TEST anonymous account, link, verify, restore, logout/email re-login | Same UID/Player/alias/progress fingerprints; expected metadata-only backend changes |
| Final review | Report, security/privacy scan, explicit checkpoint authorization | No automatic commit/deploy |

Implementation review must approve metadata effects and lifecycle cancellation semantics before code changes. No backend implementation is proposed. No valuable Guest is a valid live test fixture.

## Validation matrix to implement

Unit/contract cases:

- Guest-like alias with password provider is recoverable; custom alias with anonymous Firebase state is unlinked. Unknown/contradictory provider state fails closed. No private-name fallback.
- Protection gate, cancel exact origin, explicit force logout, no remote deletion, single owner and no pre-link signout.
- Valid credential link returns same UID/current user; different UID fails closed; double submit is single-flight; simultaneous logout/restore and stale completions cannot replace identity.
- Invalid email, weak password, mismatch, occupied email/credential, already-linked provider, disabled/configuration errors, throttling, network failure before/after possible commit.
- Verification send failure after success, reload/refresh failure, backend failure and routing failure preserve linked identity; retry only failed stage; restart reconciles remote state.
- Unverified login stays pending; verified same-UID refresh does not short-circuit; authoritative COMPLETED stays complete and IN_PROGRESS resumes same step/revision.
- Preserve alias/private names/country/Coach/createdAt/grants/wallet/history. Permit only expected accountType/updatedAt/lastSeenAt changes; no automatic trial or alias calls.
- Restore and email re-login retain same Player; session-switch privacy regression; no token/password/email/UID in diagnostic output.

Isolated Unity states: GUEST_LOGOUT_WARNING, PROTECT_ACCOUNT_FORM, LINKING, LINK_SUCCESS_VERIFICATION_PENDING, INVALID_EMAIL, WEAK_PASSWORD, EMAIL_ALREADY_IN_USE, NETWORK_FAILURE, LINK_OUTCOME_UNKNOWN, POST_LINK_SEND_FAILED, VERIFIED_ROUTING_FAILED. EN/ES, eight approved sizes, 44px action targets, shared password geometry, reachability/overflow/focus, no hidden live service dependency. These tests are planned, not run in this audit.

Future live TEST: separately authorize a disposable anonymous identity with complete foundation and no pending queue/game/rewards. Capture sanitized UID/Player fingerprints, original createdAt and domain hashes; link an unused disposable email; verify same UID before any bootstrap; send/confirm verification; compare permitted metadata changes and all protected data; exit/reenter Play; normal logout then email/password login; confirm same UID, Player, alias and progress on Home/Menu/Profile. Stop on any identity mismatch or unexpected domain write. No value-bearing existing Guest, merge, automatic repair or security-setting workaround.

UNIT_TEST_MATRIX_DEFINED=YES
UNITY_FIXTURE_MATRIX_DEFINED=YES
REAL_TEST_PLAN_DEFINED=YES
POST_LINK_SESSION_RESTORE_SAME_PLAYER=REQUIRED_FUTURE_GATE
POST_LINK_EMAIL_LOGIN_SAME_PLAYER=REQUIRED_FUTURE_GATE

## Audit boundaries and final status

SOURCE_CHANGED=NO
REPORT_CREATED=YES
PREEXISTING_PROTECTED_FILES_MODIFIED=0/102
HISTORICAL_PENDING_FILES_PRESERVED=132/132
PRE_AUDIT_PENDING_FILES_PRESERVED=140/140
PENDING_FILES_AFTER_REPORT=141
SECRET_SCAN=PASS_REPORT_SCOPE
PRIVACY_SCAN=PASS_REPORT_SCOPE
IMPLEMENTATION_EXECUTED=NO
REAL_GUEST_LINK_EXECUTED=NO
REAL_GUEST_LOGOUT_EXECUTED=NO
REAL_ACCOUNT_CREATED=NO
REAL_PLAYER_MUTATIONS=0
VALUABLE_EXISTING_GUEST_USED_FOR_TEST=NO
PASSWORD_LOGGED=NO
TOKEN_LOGGED=NO
BACKEND_CHANGED=NO
CONFIG_CHANGED=NO
COMMIT=NONE
PUSH=NONE
DEPLOY=NO
NEXT=GUEST-ACCOUNT-01 IMPLEMENTATION PLAN REVIEW AND EXPLICIT PHASE AUTHORIZATION

These no-operation statements apply to this audit, not earlier separately authorized tasks. Protection and linking targets above are not represented as implemented or live-tested functionality.

Verification: hashes of all 140 pre-audit pending files match after report creation; the sole added file is this report. The historical baseline separately confirms 132/132 including 102 protected files. Report scan found no email, JWT, private-key block or credential literal. Seven raw-UID-pattern hits were reviewed: all are known source identifiers (five distinct method/class names), not identities. No real identity/session store was opened. No tests, Play transition or backend request was run for this source-only audit.

## GUEST_ACCOUNT_01A_IMPLEMENTATION

Implemented on 2026-10-03 against HEAD `aca8b2ebef57177258789ec464f7c800fbeb338c`, without staging or committing. This section supersedes the audit-only implementation status above for **01A only**. Credential linking and 01B remain unimplemented.

### Classification and session ownership

`AuthRecoverabilityClassifier` consumes the existing Firebase session snapshot. Null means NoSession; Firebase anonymous without a contradictory password provider means AnonymousUnlinked; non-anonymous password-provider sessions mean Recoverable, independent of email verification. Missing UID, contradictory provider state and unsupported providers fail closed as Unknown. No public alias, display name, email absence or Player profile field participates in classification. The existing immutable auth identity object and UID pin the dialog to a session; a new object with the same UID still invalidates old actions.

`AccountProtectionController` intercepts anonymous logout before invoking any teardown. Recoverable email accounts call the existing logout service directly, with no warning/confirmation dialog. Unknown state presents an unavailable explanation and Cancel, never a forced logout. Player presentation resolution does not determine auth classification.

Repeated Logout and Protect actions do not duplicate surfaces. Session identity is checked on actions and before/after asynchronous preparation and stop stages. An invalidated or disposed dialog cannot clear or sign out a replacement session. Existing Firebase restore and sign-in behavior are unchanged.

### Dialog, cancellation and future entry

The implementation extends `ProductionLogoutConfirmation` and uses the existing root page, theme buttons and semantic error tokens. It does not add another UI framework. EN/ES action order is Protect account (primary), Log out anyway (destructive secondary), Cancel. The warning describes possible loss of recovery, never deletion of the account or Player.

`ProductionAccountProtectionOverlay` retains the actual underlying view instances and temporarily hides them. Cancel restores the view, draft text, nonzero scroll offset and original focus without rebuilding Menu/Profile or changing route. Tab/Shift+Tab are contained among the dialog actions through Unity 6 navigation events; Escape cancels. Protect opens a minimal localized coming-soon entry with the current session intact and no credential form, linking request or backend operation.

Log out anyway delegates to the existing `ProductionLogoutService` preparation, router stop, local shutdown/clear, Firebase sign-out and Welcome/session replacement composition. A session guard was added to that service; no second teardown implementation or deletion operation exists. The isolated harness replaces these delegates with counters, so tests never log out a real account.

### Exact scope

Paths below are relative to `client/DominoGame/Assets/_Domino/Scripts/` unless stated otherwise.

| Classification | Files |
|---|---|
| New production | `Auth/AccountProtectionController.cs`, `UI/AppShell/AccountProtectionText.cs`, `UI/AppShell/ProductionAccountProtectionOverlay.cs`, and their Unity `.meta` files |
| Modified production | `Auth/ProductionLogoutService.cs`, `UI/AppShell/ProductionAuthHost.cs`, `UI/AppShell/ProductionLogoutConfirmation.cs` |
| Isolated Editor tooling | `UI/AppShell/Editor/GuestAccountProtectionPreview.cs` and `.meta` |
| Tests | `client/Validation/GuestAccount01ATests.cs`, `client/Validation/RunGuestAccount01ATests.ps1` |
| Report | This file |

There are 14 task-scoped pending files including metadata and report. No backend, schema, API, App Shell Mock, Menu/Home/Profile data binding, alias contract, restoration, entitlement, trial or configuration source was changed by this task. All 140 pre-audit pending file hashes remain identical; the separate protected baseline confirms 102/102 unchanged. No index changes were made.

### Validation evidence

| Validation | Result |
|---|---|
| New classifier/controller checks | 29 PASS |
| Existing logout regression | 22 PASS |
| Player session isolation regression | 291 PASS |
| Alias binding regression | 10 PASS; availability/uniqueness code unchanged |
| Home binding/name/coach/version/session regression | 222 PASS |
| Menu binding regression | 60 PASS |
| Profile binding regression | 80 PASS |
| Final Unity isolated dialog/entry checks | 704 PASS, 0 FAIL |

Final Unity run: `2026-10-03T22:50:17.7280587Z`; local evidence: `client/DominoGame/Library/GuestAccount01A/result.txt`. Sixteen scenarios cover EN/ES at 375x667, 393x852, 412x915, 430x932, 480x1040, 600x960, 768x1024 and 834x1194. Checks cover warning copy, primary/destructive hierarchy, one dialog, 44px minimum actions, horizontal bounds, measured text clipping, vertical reachability, forward/reverse keyboard cycling, retained form/view/scroll/focus, Protect without logout, and forced logout through fake existing-path delegates.

Earlier isolated runs exposed a double focus move because both KeyDown and Unity navigation handled Tab. The corrected overlay handles the navigation event once. Those earlier test exceptions and a temporary validation-request file-sharing exception remain historical log evidence; they are not represented as a clean entire Editor session. The final run has no blocking exception. Final Unity Console shows **9 preexisting CS0067 warnings, 0 errors**; no new 01A warnings remain. The latest import completed with Play off. No real session was used by the harness.

Privacy/security review covers the 14 scoped files: no real emails, JWTs, private keys, API keys, password/token literals or authentication action URLs. UID-shaped lexical matches are source/API identifiers, not real identities. No credential or local auth store was read for these tests. Historical pending files are preserved, not certified or staged by this scope scan.

Final observed preview: **GUEST PROTECTION · ISOLATED / ANONYMOUS LOGOUT WARNING / ES / 393x852**, left open for manual review. Manual visual approval remains the user's next gate.

```ini
AUTH_CLASSIFICATION_SOURCE=FIREBASE_AUTH
AUTH_RECOVERABILITY_STATE_EXPLICIT=YES
GUEST_ALIAS_USED_AS_AUTH_CLASSIFIER=NO
RECOVERABLE_ACCOUNT_LOGOUT=NORMAL_LOGOUT
RECOVERABLE_ACCOUNT_LOGOUT_DIALOG=NO
UNLINKED_ANONYMOUS_LOGOUT=PROTECTION_GATE
ANONYMOUS_LOGOUT_INTERCEPTED_BEFORE_SIGNOUT=YES
EXISTING_DIALOG_REUSED=YES
GUEST_LOGOUT_WARNING_EN=PASS
GUEST_LOGOUT_WARNING_ES=PASS
WARNING_CLAIMS_ACCOUNT_DELETION=NO
WARNING_CLAIMS_PLAYER_DELETION=NO
CANCEL_FIREBASE_SIGNOUT_CALLS=0
CANCEL_PLAYER_SERVICE_CLEAR_CALLS=0
CANCEL_PLAYER_PRESENTATION_CLEAR_CALLS=0
CANCEL_ROUTE_CHANGED=NO
CANCEL_DOMAIN_WRITES=0
FORCE_LOGOUT_USES_EXISTING_LOGOUT_PATH=YES
FORCE_LOGOUT_ROUTE=WELCOME
FORCE_LOGOUT_DELETES_FIREBASE_USER=NO
FORCE_LOGOUT_DELETES_PLAYER=NO
FORCE_LOGOUT_DELETES_ALIAS=NO
PROTECT_ACCOUNT_LINK_EXECUTED=NO
PROTECT_ACCOUNT_FIREBASE_SIGNOUT_CALLS=0
PROTECT_ACCOUNT_SESSION_PRESERVED=YES
DISPLAY_NAME_MUTATIONS=0
ALIAS_RESERVATION_MUTATIONS=0
PLAYER_DOMAIN_MUTATIONS=0
TRIAL_ACTIVATION_CALLS=0
TRIAL_MUTATIONS=0
BACKEND_SOURCE_CHANGED=NO
BACKEND_ENDPOINT_ADDED=NO
SCHEMA_CHANGED=NO
SESSION_RESTORE_BEHAVIOR_CHANGED=NO
EMAIL_LOGOUT_REGRESSION=PASS_ISOLATED
GUEST_PREFIX_EMAIL_ACCOUNT_CLASSIFIED_ANONYMOUS=NO
CUSTOM_ALIAS_ANONYMOUS_ACCOUNT_CLASSIFIED_RECOVERABLE=NO
NO_SESSION_LOGOUT_PROTECTION_DIALOG=NO
DUPLICATE_LOGOUT_DIALOG_COUNT=0
DUPLICATE_PROTECT_ACCOUNT_SURFACES=0
STALE_PLAYER_STATE_CONTROLS_AUTH_CLASSIFICATION=NO
STALE_GUEST_DIALOG_ACTION_APPLIED_TO_NEW_SESSION=NO
GUEST_LOGOUT_DIALOG_EN=PASS
GUEST_LOGOUT_DIALOG_ES=PASS
GUEST_LOGOUT_DIALOG_RESPONSIVE=8/8_PASS_EN_ES
HORIZONTAL_OVERFLOW=0
TEXT_CLIPPING=0
DIALOG_FOCUS_CONTAINED=YES
DIALOG_ACTIONS_KEYBOARD_REACHABLE=YES
CANCEL_ACCESSIBLE=YES
PROTECT_ACCOUNT_PRIMARY_ACTION=YES
GUEST_ACCOUNT_01A_TESTS=29_PASS
GUEST_ACCOUNT_01A_UNITY_CHECKS=704_PASS
PLAYER_UI_01_REGRESSION=PASS
PLAYER_IDENTITY_01_REGRESSION=PASS_CLIENT_BINDING
UNITY_COMPILER_ERRORS=0
UNITY_WARNINGS_CURRENT=9
NEW_GUEST_ACCOUNT_01A_WARNINGS=0
CURRENT_BLOCKING_EXCEPTIONS=0
REAL_GUEST_LOGOUT_EXECUTED=NO
REAL_GUEST_LINK_EXECUTED=NO
REAL_ACCOUNT_CREATED=NO
REAL_NETWORK_OPERATIONS=0
PREEXISTING_PENDING_FILES_PRESERVED=140/140
PREEXISTING_PROTECTED_FILES_MODIFIED=0/102
PASSWORD_LOGGED=NO
TOKEN_LOGGED=NO
SECRET_SCAN=PASS
PRIVACY_SCAN=PASS
COMMIT=NONE
PUSH=NONE
DEPLOY=NO
GUEST_ACCOUNT_01A_SUCCESS=YES_IMPLEMENTATION_AND_ISOLATED_VALIDATION
GUEST_ACCOUNT_01B_STARTED=NO
NEXT=MANUAL GUEST LOGOUT PROTECTION REVIEW
```

## GUEST_ACCOUNT_01B_EMAIL_LINKING_IMPLEMENTATION

2026-10-03. Implementation and isolated validation only; no live link was attempted. This section supersedes the earlier 01A placeholder status. Manual visual approval and a separately authorized real Firebase test remain pending.

### Integration and authority

The production host now supplies `ProductionProtectAccountView` to the existing protection overlay. The approved anonymous logout warning and its three actions are unchanged. Cancel restores the retained authenticated surface; linking never signs out first. The optional placeholder fallback remains available only to the older isolated 01A harness, not the production host entry.

`IFirebaseCredentialLinkClient` is an optional SDK capability. `FirebaseSdkClient.LinkCurrentUserAsync` captures the current anonymous Firebase user and uses `EmailAuthProvider.GetCredential` with `FirebaseUser.LinkWithCredentialAsync`. It checks both the returned user and current SDK session against the expected identity. It does not invoke registration, another-account sign-in, account deletion, or merging. The credential is disposed after the call. The existing safe Firebase error mapper supplies typed errors, not raw exception contents.

`FirebaseAuthService.LinkGuestEmailAsync` serializes authentication work, pins the current identity object/UID and adopts the linked snapshot only after identity checks. A prior uncertain attempt is reconciled with `ReloadLinkSessionAsync` before another link operation. If the current session already reflects the successful password link, recovery adopts that session without repeating the link. Late responses for a different session are rejected.

`AccountLinkController` explicitly models Form, Linking, LinkedUnverified, VerificationSending, VerificationPending, ErrorBeforeLink and ErrorAfterLink. Duplicate submission shares the in-flight task. Technical errors after link retry verification, not registration/linking. A preservation/session conflict blocks continuation. Cancel is available only before link while the original anonymous session remains current; there is no unlink/rollback UI after success.

### Identity and Player preservation

Before link, the host captures SHA-256 fingerprints in memory for Firebase UID, Player UID, public alias, country, createdAt, the currently confirmed onboarding/profile snapshot, entitlements, trial eligibility and wallet snapshot. The same cached data is compared immediately after linking. No literal identity or credential is written to this report or a log. An unavailable snapshot is explicitly `UNAVAILABLE`, not fabricated or fetched as a new write. Private profile, coach, status and revision are covered when present in the authoritative onboarding snapshot. There is no separate wallet identity in the current client model; the existing wallet snapshot is used. Trial evidence is the current client eligibility/entitlement contract, not an invented raw grant record.

The link path issues no profile/alias/onboarding/membership/trial/wallet mutation. Email remains private authentication data; Home/Menu/Profile alias binding is unchanged. `ProductionAuthRouter.AcceptLinkedAccountAsync` reuses VerificationPending and the existing verification email operation. A linked unverified password account is recoverable, not anonymous. The secondary action is explicitly Sign Out, with the existing confirmation, rather than pretending the link can be undone by using another email.

On I've Verified, existing Firebase reload and token refresh are reused. The router forces the existing same-Player bootstrap refresh, because the already-confirmed guest Player would otherwise short-circuit initialization. UID, alias, country, createdAt and wallet coins are checked across this refresh. Account type and ordinary updatedAt/lastSeen metadata may follow the existing backend contract. The existing authenticated destination coordinator then resolves authoritative onboarding/Home. Tests exercise both IN_PROGRESS and COMPLETED with unchanged authoritative state, step and revision and zero onboarding/trial writes. No backend/schema change or repair is introduced.

These are source-contract and isolated-test guarantees. Real Firebase link, verification delivery, persistence and full live pre/post Player-domain comparison have not been performed or claimed.

### Form and error handling

EN/ES form uses the current production theme, `ProductionPasswordField` and `EmailAuthRules`, with password masking by default, 48px fields and existing 44px visibility controls. Invalid email/password/mismatch and in-flight state disable submission. EmailAlreadyInUse and InvalidEmail map inline to Email; WeakPassword maps to Password; mismatch maps locally to confirmation without Firebase calls. Email-in-use never signs into or merges the other account. Pre-link failures retain the anonymous session and form. Busy/technical/partial-success feedback uses the existing semantic status component.

Fields, error messages and actions have labels/tooltips. Overlay navigation includes fields and visibility controls. Mobile keyboard inset handling preserves scrolling; isolated reduced-viewport checks verify each focused field and primary action can be reached. This is not a physical-device keyboard or screen-reader certification.

### Validation evidence

| Suite | Current result |
|---|---:|
| Guest link/service/router/controller | 48 PASS |
| Guest protection unit regression | 29 PASS |
| Guest protection Unity regression | 704 PASS |
| Protect Account Unity isolated | 3072 PASS / 144 scenarios |
| Player session isolation | 291 PASS |
| Existing logout | 22 PASS |
| Existing email registration/verification | 53 PASS |
| Existing email sign-in/reset | 55 PASS |
| Home name/coach/version/session | 222 PASS |
| Menu binding | 60 PASS |
| Profile binding | 80 PASS |
| Alias client binding | 10 PASS |

Final Unity run began at `2026-10-03T23:26:37.2158096Z`, after final product import. Nine isolated states across eight presets and EN/ES: FORM_EMPTY, FORM_VALID, INVALID_EMAIL, WEAK_PASSWORD, PASSWORD_MISMATCH, LINKING, EMAIL_ALREADY_IN_USE, NETWORK_FAILURE, LINKED_UNVERIFIED. All passed with zero real operations. Sizes: 375x667, 393x852, 412x915, 430x932, 480x1040, 600x960, 768x1024, 834x1194. Final 01A Unity regression began at `2026-10-03T23:25:52.9772540Z`.

The current Console shows 9 existing CS0067 warnings and 0 errors; no new 01B compiler warning or blocking exception. Older Editor log entries, including warnings from earlier 01A development and an SDK handle warning when Play stopped, are historical and are not represented as current failures or silently erased.

Generated local evidence: `Validation/Generated/GuestAccount01B/` baseline, scope, security/final evidence; Unity results: `DominoGame/Library/GuestAccount01A/result.txt` and `GuestAccount01B/result.txt`. They contain synthetic fixtures only. The isolated preview uses fake interfaces, has no ApplicationServices or real Firebase/HTTP construction and ends at FORM_VALID / ES / 393x852. The visible fixture email uses the reserved `.invalid` domain; masked fixture strings are not real credentials.

### Scope and preservation

Sixteen files comprise 01B scope (including metadata, tests and this shared report). Production changes: Firebase interface/adapter/service, ProductionAuthRouter, new AccountLinkController, new ProductionProtectAccountView, ProductionAuthHost, ProductionAccountProtectionOverlay, and linked-verification presentation in ProductionEmailView. Isolated tooling: GuestAccountLinkPreview, GuestAccount01BTests, RunGuestAccount01BTests and new Unity metadata.

The baseline contains 154 preexisting pending files. Of these, 151 unrelated files remain byte-identical. Three authorized integration/report files are updated: ProductionAuthHost, ProductionAccountProtectionOverlay and this report; their prior 01A functionality/evidence is retained. No historical file is removed or staged. All 102 protected files remain byte-identical. Backend, schema, config, App Shell Mock and other pending work are unchanged by 01B. Nothing is staged or committed.

Security review covers the explicit 01B scope: no real email, raw Firebase UID, JWT, API secret/private key, password logging, token logging or auth action URL. Fixture passwords are synthesized locally and are never printed. Privacy checks include source review of logging and public alias presentation, not merely pattern matching.

```ini
PROTECT_ACCOUNT_PLACEHOLDER_REMOVED=YES_PRODUCTION_ENTRY
PROTECT_ACCOUNT_SIGNS_OUT_FIRST=NO
ANONYMOUS_SESSION_ACTIVE_WHILE_LINKING=YES_UNTIL_SUCCESSFUL_LINK
FORM_FIELDS=EMAIL,PASSWORD,CONFIRM_PASSWORD
PASSWORD_PLAINTEXT_DEFAULT=NO
EMAIL_VALIDATION_REUSED=YES
PASSWORD_VALIDATION_REUSED=YES
INVALID_FORM_SUBMIT_ENABLED=NO
FIREBASE_OPERATION=LINK_CURRENT_USER_CREDENTIAL
CREATE_SECOND_FIREBASE_USER=NO
PRE_LINK_IDENTITY_SNAPSHOT=YES_IN_MEMORY_FINGERPRINTS
FIREBASE_UID_CHANGED_AFTER_LINK=NO_DESIGN_ISOLATED_PASS
NEW_PLAYER_CREATED_AFTER_LINK=NO_DESIGN_ISOLATED_PASS
DISPLAY_NAME_CHANGED_BY_ACCOUNT_LINK=NO
ALIAS_RESERVATION_CHANGED_BY_ACCOUNT_LINK=NO
FIRST_NAME_CHANGED_BY_ACCOUNT_LINK=NO
LAST_NAME_CHANGED_BY_ACCOUNT_LINK=NO
COUNTRY_CHANGED_BY_ACCOUNT_LINK=NO
COACH_CHANGED_BY_ACCOUNT_LINK=NO
CREATED_AT_CHANGED_BY_ACCOUNT_LINK=NO
ONBOARDING_STATUS_CHANGED_BY_LINK=NO
ONBOARDING_REVISION_CHANGED_BY_LINK=NO
MEMBERSHIP_CHANGED_BY_ACCOUNT_LINK=NO
TRIAL_CHANGED_BY_ACCOUNT_LINK=NO
TRIAL_ACTIVATION_CALLS=0
WALLET_CHANGED_BY_ACCOUNT_LINK=NO
EMAIL_PUBLICLY_EXPOSED=NO
LINKED_EMAIL_VERIFICATION_REQUIRED=YES_IF_UNVERIFIED
LINKED_UNVERIFIED_ROUTE=VERIFICATION_PENDING
POST_VERIFICATION_BOOTSTRAP_SAME_PLAYER=PASS_ISOLATED
ACCOUNT_TYPE_CHANGE_ALLOWED=AUTH_METADATA_ONLY
ALLOWED_OPERATIONAL_METADATA=accountType,updatedAt,lastSeenAt
EMAIL_ALREADY_IN_USE_ROUTE=PROTECT_ACCOUNT
EMAIL_ALREADY_IN_USE_SESSION_PRESERVED=YES
AUTOMATIC_ACCOUNT_MERGE=NO
INVALID_EMAIL_GENERIC_ERROR=NO
WEAK_PASSWORD_GENERIC_ERROR=NO
PASSWORD_MISMATCH_FIREBASE_REQUESTS=0
ANONYMOUS_SESSION_PRESERVED_ON_PRELINK_FAILURE=YES
FORM_VALUES_PRESERVED_ON_FAILURE=YES_PRELINK
POST_LINK_RETRY_DOES_NOT_CREATE_SECOND_ACCOUNT=YES
LINK_STATE_EXPLICIT=YES
DUPLICATE_LINK_REQUESTS=0
STALE_LINK_RESPONSE_APPLIED_TO_NEW_SESSION=NO
CANCEL_BEFORE_LINK_SIDE_EFFECTS=0
SUCCESSFUL_LINK_ROLLBACK_UI_OFFERED=NO
LINKED_UNVERIFIED_CLASSIFIED_ANONYMOUS=NO
PROTECT_ACCOUNT_RESPONSIVE=8/8_PASS_EN_ES
HORIZONTAL_OVERFLOW=0
TEXT_CLIPPING=0
PROTECT_ACCOUNT_KEYBOARD_SCROLL=PASS_ISOLATED_REDUCED_VIEWPORT
GUEST_ACCOUNT_01B_TESTS=48_PASS
GUEST_ACCOUNT_01B_FIXTURES=144_PASS_3072_CHECKS
GUEST_ACCOUNT_01A_REGRESSION=29_UNIT_AND_704_UNITY_PASS
PLAYER_UI_01_REGRESSION=PASS
PLAYER_IDENTITY_01_REGRESSION=PASS_CLIENT_BINDING
UNITY_COMPILER_ERRORS=0
UNITY_WARNINGS_CURRENT=9
NEW_GUEST_ACCOUNT_01B_WARNINGS=0
CURRENT_BLOCKING_EXCEPTIONS=0
BACKEND_SOURCE_CHANGED=NO
BACKEND_REDEPLOY_REQUIRED=NO
SCHEMA_CHANGED=NO
REAL_GUEST_LINK_EXECUTED=NO
REAL_EMAIL_VERIFICATION_SENT=NO
REAL_ACCOUNT_CREATED=NO
REAL_PLAYER_MUTATIONS=0
PREEXISTING_PENDING_BASELINE=154
UNRELATED_PENDING_FILES_PRESERVED=151/151
AUTHORIZED_EXISTING_INTEGRATION_REPORT_FILES_UPDATED=3
PREEXISTING_PROTECTED_FILES_MODIFIED=0/102
PASSWORD_LOGGED=NO
TOKEN_LOGGED=NO
SECRET_SCAN=PASS
PRIVACY_SCAN=PASS
FINAL_PREVIEW=PROTECT_ACCOUNT_FORM_VALID_393x852_ES
COMMIT=NONE
PUSH=NONE
DEPLOY=NO
GUEST_ACCOUNT_01B_SUCCESS=YES_IMPLEMENTATION_AND_ISOLATED_VALIDATION
GUEST_ACCOUNT_01C_STARTED=NO
NEXT=MANUAL PROTECT ACCOUNT FORM REVIEW
```

## GUEST_ACCOUNT_01C_POST_LINK_VERIFICATION_RECONCILIATION

Current read-only Firebase administrative lookup confirms an enabled, non-anonymous user with the password provider linked and email verified. The persisted anonymous-token subject, authoritative Firebase user and current Player ID have identical fingerprints. No raw identity or email is recorded here. The existing cached token lookup returned TOKEN_EXPIRED; it was not refreshed by this inspection. Player is ACTIVE/REGISTERED and onboarding is COMPLETED, revision 7. Linking has already committed and must not be repeated.

The exact message belongs to EmailAuthRules.Message(SessionConflict). A concrete defect was reproduced in isolation: CheckVerificationAsync -> ReloadEmailAsync -> RefreshLinkedPlayerAsync -> successful same-identity bootstrap -> CreatedAt equality guard -> SessionConflict. New foundation bootstrap may return an unknown CreatedAt while the server timestamp is unresolved; a subsequent read supplies the recorded timestamp. Treating that null-to-recorded hydration as an identity replacement is incorrect. This proves the defect, but does not uniquely prove which guard caused the original manual execution: its logs did not identify the failed guard.

The minimal client correction allows only unknown-to-recorded CreatedAt hydration, retains the original comparison baseline across retries, and still rejects changes/removal of a known timestamp, alias, country or wallet. Same-user Firebase metadata reload preserves the current identity owner. Different-user and late-response protections remain enforced. Existing verification feedback now acknowledges that linking already succeeded when reconciliation subsequently fails; EN/ES messages use the existing semantic feedback component.

An attempted live-memory inspection triggered Unity's Debug Mode recompilation/reload. This destroyed the unavailable pre-link in-memory snapshot; invalid null-value diagnostic fingerprints were discarded. Play was subsequently stopped without Firebase logout. Therefore a complete real before/after business-state comparison and real post-fix resume remain unverified. No real linking, account creation, verification send, trial action or Player write was executed by 01C. Historical SDK handle warnings from that Editor reload are distinct from the clean final compilation boundary.

Security incident: a window inventory returned an existing browser title containing a verification action URL. Its contents are deliberately omitted from this report and were not used. The tool transcript cannot be described as free of verification material. Scoped source/report scans are reported separately.

Current isolated results: 01C 69 PASS; 01B 48 PASS; 01A 29 PASS; Player session isolation 291 PASS; Home 222 PASS; Menu 60 PASS; Profile 80 PASS; alias binding 10 PASS; Email Auth 02A 53 PASS and 02B 55 PASS. The two historical Email Auth runner scripts initially failed compilation because their dependency lists omit newer dependencies; the existing prepared projects subsequently passed against current source. Those historical runners were not modified. Unity verification presentation: 800 checks, 64 EN/ES scenarios PASS. No real operations are wired into this fixture. The old large visual suites were retained rather than repeated.

Product changes are limited to ProductionAuthRouter.cs, FirebaseAuthService.cs, ProductionAuthHost.cs and ProductionEmailView.cs. Added validation consists of GuestAccount01CTests.cs, RunGuestAccount01CTests.ps1 and the Editor-only GuestAccountVerificationPreview.cs with its metadata. The preview constructs no Firebase/session service and has empty callbacks. Backend, schema and routing contracts are unchanged.

```ini
CURRENT_FIREBASE_USER_PRESENT=YES
CURRENT_FIREBASE_IS_ANONYMOUS=NO
CURRENT_FIREBASE_EMAIL_PROVIDER_LINKED=YES
CURRENT_FIREBASE_EMAIL_VERIFIED=YES
FIREBASE_LINK_ALREADY_COMMITTED=YES
CURRENT_LINK_OPERATION_REPEATED=NO
FIREBASE_UID_CONTINUITY=PERSISTED_ANONYMOUS_SUBJECT_MATCHES_CURRENT_USER
PLAYER_ID_CONTINUITY=CURRENT_PLAYER_MATCHES_UID_PRELINK_SNAPSHOT_UNAVAILABLE
ORIGINAL_EXECUTION_EXACT_GUARD=NOT_PROVEN
CREATED_AT_HYDRATION_FALSE_CONFLICT=PROVEN_ISOLATED_AND_FIXED
CROSS_ACCOUNT_SESSION_PROTECTION_WEAKENED=NO
SAME_UID_REFRESH_ALLOWED=YES
POST_VERIFICATION_USER_RELOAD_REQUIRED=YES
POST_VERIFICATION_TOKEN_REFRESH_REQUIRED=YES
IVE_VERIFIED_CREATES_NEW_SESSION_IDENTITY=NO
POST_VERIFICATION_SIGNOUT_SIGNIN_REQUIRED=NO
POST_LINK_FAILURE_DOES_NOT_MISREPORT_LINK_FAILURE=YES
POST_LINK_APP_RESTART_RECOVERY=PASS_ISOLATED
DIFFERENT_UID_SESSION_REPLACEMENT_BLOCKED=PASS_ISOLATED
REAL_PRE_POST_BUSINESS_STATE_COMPARISON=UNVERIFIED
PLAYER_BUSINESS_MUTATIONS_BY_01C=0
NEW_REAL_PLAYER_CREATED=NO
GUEST_ACCOUNT_01C_TESTS=69_PASS
UNITY_VERIFICATION_CHECKS=800_PASS
UNITY_VERIFICATION_SCENARIOS=64_PASS
UNITY_COMPILER_ERRORS=0
UNITY_WARNINGS_CURRENT=9
NEW_GUEST_ACCOUNT_01C_WARNINGS=0
CURRENT_BLOCKING_EXCEPTIONS=0
BACKEND_SOURCE_CHANGED=NO
BACKEND_REDEPLOY_REQUIRED=NO
SCHEMA_CHANGED=NO
REAL_POST_FIX_RESUME=PENDING
FINAL_EDITOR_PLAY_MODE=OFF
FINAL_PREVIEW_REQUESTED=VERIFICATION_PENDING_ES_393x852_ISOLATED
FINAL_PREVIEW_VISIBILITY=NOT_CONFIRMED
VERIFICATION_ACTION_URL_IN_TOOL_OUTPUT=YES_WINDOW_TITLE_INCIDENT
SCOPED_SOURCE_REPORT_SECRET_PRIVACY_SCAN=PASS_9_FILES
PREEXISTING_PROTECTED_FILES_MODIFIED=0/102
UNRELATED_PENDING_FILES_PRESERVED=162/162
AUTHORIZED_PREEXISTING_FILES_UPDATED=5
COMMIT=NONE
PUSH=NONE
DEPLOY=NO
GUEST_ACCOUNT_01C_SUCCESS=PARTIAL_REAL_COMPLETION_PENDING
NEXT=REAL SAME-PLAYER LINK COMPLETION REVIEW
```

## GUEST_ACCOUNT_01C_REAL_RECOVERABILITY_VALIDATION

One authorized normal Play startup was executed on 2026-10-04 at approximately 00:18 UTC. Before startup, a fresh read-only administrative Firebase/Firestore snapshot confirmed the same pinned identity, password provider linked, email verified, disabled=false, anonymous=false; Player ACTIVE/REGISTERED, onboarding COMPLETED revision 7 and recorded createdAt present. Sanitized fingerprints cover Player identity, displayName, createdAt, preferences, domino profile, onboarding, wallet, entitlement state/grants/audit, membership and the owned alias reservation. Alias collection audit found zero duplicate groups, missing reservations or owner mismatches. No literal identities, email or credentials are included in this report.

The startup did not reach Home. Unity logged Existing user reused, Identity ready, IsAnonymous=true, then bootstrap failed category=Authentication. The sanitized routing event at 2026-10-04T00:18:06.8744446Z records stage=Player, exceptionType=DominoApiException, category=Authentication; HTTP status, backend code, request ID and inner exception are UNKNOWN. The rendered screen was Could not connect / Could not load your session. Please try again. No session-replacement message was displayed. This demonstrates a discrepancy between authoritative linked-account state and the restored client classification, not proof of the underlying cause of authentication failure. It does not establish that an HTTP request reached the backend.

Per the stop-on-defect instruction, Retry, logout and email sign-in were not executed. Play was stopped and Unity remains outside Play. No source fix was made. Historical attribution is unchanged: the original manual session-error root cause was not proven. The current read-only snapshot is a pre-test baseline, not evidence of pre-link business-data preservation. Full real recoverability and post-login comparisons remain pending.

```ini
ENVIRONMENT=TEST
FIREBASE_LINK_ALREADY_COMMITTED=YES
REAL_LINK_REPEATED=NO
LINK_WITH_CREDENTIAL_CALLS=0
CURRENT_FIREBASE_USER_PRESENT=YES_ADMIN_LOOKUP
CURRENT_FIREBASE_IS_ANONYMOUS=NO_ADMIN_LOOKUP
CURRENT_FIREBASE_EMAIL_PROVIDER_LINKED=YES_ADMIN_LOOKUP
CURRENT_FIREBASE_EMAIL_VERIFIED=YES_ADMIN_LOOKUP
RESTORED_CLIENT_IS_ANONYMOUS=YES_UNEXPECTED
PRE_LOGOUT_IDENTITY_SNAPSHOT=YES
CURRENT_PLAYER_RESOLVED=YES_READ_ONLY
PLAYER_ID_CHANGED_AFTER_LINK=UNKNOWN_HISTORICAL_PRELINK_SNAPSHOT_UNAVAILABLE
REAL_POST_FIX_RESUME=FAIL_AUTHENTICATION
SESSION_REPLACEMENT_ERROR_VISIBLE=NO
CREATED_AT_UNKNOWN_TO_KNOWN_SAME_PLAYER_ACCEPTED=PASS_ISOLATED_NOT_EXERCISED_LIVE
DIFFERENT_PLAYER_SESSION_REPLACEMENT_BLOCKED=PASS_ISOLATED_RETAINED
BOOTSTRAP_HTTP_STATUS=UNKNOWN
NORMAL_LOGOUT_EXECUTED=NO
POST_LOGOUT_ROUTE=NOT_EXERCISED
PLAYER_SERVICE_CLEARED_ON_LOGOUT=NOT_EXERCISED
PLAYER_PRESENTATION_CLEARED_ON_LOGOUT=NOT_EXERCISED
EMAIL_PASSWORD_SIGN_IN=NOT_EXERCISED
POST_RELOGIN_COMPARISONS=NOT_EXERCISED
HOME_MENU_PROFILE_REAL_VALIDATION=NOT_REACHED
NORMALIZED_ALIAS_DUPLICATE_GROUPS=0
ALIAS_RESERVATION_OWNER_MISMATCH=0
MISSING_REQUIRED_RESERVATIONS=0
TRIAL_ACTIVATION_CALLS=0
TRIAL_ACTIVATED_BY_TEST=NO
ACCOUNT_RECOVERABLE_BY_EMAIL_PASSWORD=NOT_YET_PROVEN
SOURCE_CHANGED_DURING_REAL_VALIDATION=NO
PASSWORD_LOGGED=NO
TOKEN_LOGGED=NO
VERIFICATION_CODE_LOGGED=NO_THIS_EXECUTION
REQUEST_TRACE_SANITIZED=YES
FINAL_PLAY_MODE=OFF
COMMIT=NONE
PUSH=NONE
DEPLOY=NO
GUEST_ACCOUNT_01C_REAL_SUCCESS=NO_STOPPED_ON_UNEXPECTED_AUTHENTICATION_FAILURE
NEXT=RESTORED_LINKED_SESSION_CLASSIFICATION_AND_AUTHENTICATION_READ_ONLY_DIAGNOSIS
```


## Final dependency checkpoint review — 2026-10-03 (local)

Latest owner authorization explicitly confirms the later real manual flow: Guest email linking PASS_MANUAL, email verification PASS_MANUAL, and post-verification Home PASS_MANUAL. This supersedes the pending completion outcome for the latest manual flow only; earlier failed/intermediate observations above remain historical evidence. The original “The current session cannot be replaced here.” cause remains UNPROVEN. The isolated CreatedAt UNKNOWN → KNOWN correction is a proven same-Player reconciliation fix, not a retroactive diagnosis of that historical message. Exact historical pre/post business fingerprints remain unavailable and are not claimed by manual approval.

Firebase Auth is classification authority; Guest-* display aliases do not classify anonymous authentication. Recoverable password accounts use the existing logout path. Unlinked anonymous accounts see Protect account / Log out anyway / Cancel. Cancel does not sign out or clear Player. Force logout uses existing teardown and never deletes Firebase or Player. Linking uses the current Firebase user's LinkWithCredentialAsync, never normal Create Account or automatic merge. Same UID, session ownership and different-user rejection are enforced. A committed link enters explicit linked verification states; later recovery does not relink. Email/password remain private and are never used as public Player identity. Backend/schema changes are not required. Existing bootstrap may update accountType/updatedAt/lastSeenAt; linking does not intentionally write business data, onboarding, wallet, entitlements or alias reservations.

Scope: 25 durable files only. ProductionAuthHost is the sole partially staged file: remove only the Profile editor factory argument from its candidate blob. The result's SHA-256 exactly matches the pre-Profile validated Guest baseline. All other Guest files match that baseline before this report append. Worktree content is not replaced. Profile, historical routing diagnostics, smoke hooks and all three isolated Guest preview windows remain unstaged. There is no inseparable Profile/Guest shared dependency. No source redesign or additional product behavior is introduced. Localization stays in the reviewed EN/ES presentation helpers.

Retained tests: 01A 29 PASS, 01B 48 PASS, 01C 69 PASS. Retained Unity checks: 01A 704 PASS; 01B 3072 PASS across 144 scenarios; verification 800 PASS across 64 scenarios. Current successful Unity import and targeted Profile run previously confirmed zero compiler errors; no new Guest warnings, since Guest source is unchanged. The latest Editor observation contained 10 warnings; do not infer a warning-free session. Player UI/identity and Guest regressions remain retained. CHECKPOINT_TESTS_REPEATED=NO.

No live linking, verification, logout, creation, mutation or deployment is performed in this checkpoint. The existing Backend CI workflow has server/infrastructure path filters; this client-only checkpoint is expected not to trigger it. No CI PASS is claimed in advance.

### Pending-file classification

UNCLASSIFIED_PENDING_FILES=0. ProductionAuthHost's staged candidate is Guest-only; its remaining Profile hunk stays pending. ProductionRoutingComposition contains historical smoke hooks plus a pending Profile accessor and is wholly excluded. ProfileEditPreview is temporary validation and excluded.

| Category | Count |
|---|---:|
| GUEST_ACCOUNT_LOCALIZATION | 2 |
| GUEST_ACCOUNT_PRODUCT | 16 |
| GUEST_ACCOUNT_REPORT | 1 |
| GUEST_ACCOUNT_TEST | 6 |
| HISTORICAL | 21 |
| PLAYER_PROFILE_EDIT | 14 |
| PROTECTED | 102 |
| TEMPORARY_VALIDATION | 23 |

| Path | Category |
|---|---|
| client/DominoGame/Assets/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0/firebase-app-unity-13.16.0.pom | PROTECTED |
| client/DominoGame/Assets/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0/firebase-auth-unity-13.16.0.pom | PROTECTED |
| client/DominoGame/Assets/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0/firebase-firestore-unity-13.16.0.pom | PROTECTED |
| client/DominoGame/Assets/Plugins/Android/GoogleMobileAdsPlugin.androidlib/AndroidManifest.xml | PROTECTED |
| client/DominoGame/Assets/StreamingAssets/google-services-desktop.json | PROTECTED |
| client/DominoGame/Assets/StreamingAssets/google-services-desktop.json.meta | PROTECTED |
| client/DominoGame/Assets/_Domino/Localization/Localization Settings.asset | PROTECTED |
| client/DominoGame/Assets/_Domino/Resources/AdsSettings.asset | PROTECTED |
| client/DominoGame/Assets/_Domino/Scripts/Auth/AuthenticatedRoutingOrchestrator.cs | HISTORICAL |
| client/DominoGame/Assets/_Domino/Scripts/Auth/ProductionAuthRouter.cs | GUEST_ACCOUNT_PRODUCT |
| client/DominoGame/Assets/_Domino/Scripts/Auth/ProductionLogoutService.cs | GUEST_ACCOUNT_PRODUCT |
| client/DominoGame/Assets/_Domino/Scripts/Infrastructure/Api/OnboardingApiSession.cs | PLAYER_PROFILE_EDIT |
| client/DominoGame/Assets/_Domino/Scripts/Infrastructure/ApplicationServices.cs | TEMPORARY_VALIDATION |
| client/DominoGame/Assets/_Domino/Scripts/Infrastructure/Firebase/FirebaseAuthService.cs | GUEST_ACCOUNT_PRODUCT |
| client/DominoGame/Assets/_Domino/Scripts/Infrastructure/Firebase/FirebaseSdkClient.cs | GUEST_ACCOUNT_PRODUCT |
| client/DominoGame/Assets/_Domino/Scripts/Infrastructure/Firebase/IFirebaseClient.cs | GUEST_ACCOUNT_PRODUCT |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/ProductionAppShell.cs | PLAYER_PROFILE_EDIT |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/ProductionAuthHost.cs | GUEST_ACCOUNT_PRODUCT |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/ProductionEmailView.cs | GUEST_ACCOUNT_PRODUCT |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/ProductionLogoutConfirmation.cs | GUEST_ACCOUNT_PRODUCT |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/ProductionRoutingComposition.cs | TEMPORARY_VALIDATION |
| client/DominoGame/Assets/google-services.json | PROTECTED |
| client/DominoGame/ProjectSettings/GvhProjectSettings.xml | PROTECTED |
| client/DominoGame/ProjectSettings/ProjectSettings.asset | PROTECTED |
| client/Validation/AuthenticatedRoutingTests.cs | HISTORICAL |
| client/Validation/PLAYER_UI_01_REAL_PLAYER_BINDING_AUDIT_REPORT.md | HISTORICAL |
| client/Validation/PlayerAliasBindingTests.cs | HISTORICAL |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/query/client-agp/cache-v2 | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/query/client-agp/cmakeFiles-v1 | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/query/client-agp/codemodel-v2 | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/cache-v2-2c0909d0b4389f2443c3.json | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/cmakeFiles-v1-2afea77556dece6ed3b6.json | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/codemodel-v2-56ef99f20c5d90a856eb.json | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/directory-.-RelWithDebInfo-d0094a50bb2071803777.json | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/directory-FramePacing-RelWithDebInfo-7f9c8865fd027a154c90.json | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/index-2026-09-20T07-54-05-0123.json | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.cmake/api/v1/reply/target-swappywrapper-RelWithDebInfo-de42165ac0b744ec5a6b.json | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.ninja_deps | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/.ninja_log | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeCache.txt | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeCCompiler.cmake | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeCXXCompiler.cmake | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeDetermineCompilerABI_C.bin | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeDetermineCompilerABI_CXX.bin | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CMakeSystem.cmake | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CompilerIdC/CMakeCCompilerId.c | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CompilerIdC/CMakeCCompilerId.o | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CompilerIdCXX/CMakeCXXCompilerId.cpp | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/3.22.1-g37088a8-dirty/CompilerIdCXX/CMakeCXXCompilerId.o | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/TargetDirectories.txt | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/cmake.check_cache | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/CMakeFiles/rules.ninja | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/FramePacing/CMakeFiles/swappywrapper.dir/UnitySwappyWrapper.cpp.o | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/FramePacing/cmake_install.cmake | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/additional_project_files.txt | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/android_gradle_build.json | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/android_gradle_build_mini.json | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/build.ninja | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/build_file_index.txt | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/cmake_install.cmake | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/compile_commands.json | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/compile_commands.json.bin | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/configure_fingerprint.bin | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/metadata_generation_command.txt | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/prefab_config.json | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/arm64-v8a/symbol_folder_index.txt | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/hash_key.txt | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/prefab/arm64-v8a/prefab/lib/aarch64-linux-android/cmake/games-frame-pacing/games-frame-pacingConfig.cmake | PROTECTED |
| client/DominoGame/.utmp/RelWithDebInfo/2n1d343b/prefab/arm64-v8a/prefab/lib/aarch64-linux-android/cmake/games-frame-pacing/games-frame-pacingConfigVersion.cmake | PROTECTED |
| client/DominoGame/.utmp/tools/release/arm64-v8a/compile_commands.json | PROTECTED |
| client/DominoGame/Assets/AddressableAssetsData/Android.meta | PROTECTED |
| client/DominoGame/Assets/AddressableAssetsData/Android/addressables_content_state.bin | PROTECTED |
| client/DominoGame/Assets/AddressableAssetsData/Android/addressables_content_state.bin.meta | PROTECTED |
| client/DominoGame/Assets/AddressableAssetsData/ProfileDataSourceSettings.asset | PROTECTED |
| client/DominoGame/Assets/AddressableAssetsData/ProfileDataSourceSettings.asset.meta | PROTECTED |
| client/DominoGame/Assets/GeneratedLocalRepo.meta | PROTECTED |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase.meta | PROTECTED |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository.meta | PROTECTED |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com.meta | PROTECTED |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google.meta | PROTECTED |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase.meta | PROTECTED |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity.meta | PROTECTED |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0.meta | PROTECTED |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0/firebase-app-unity-13.16.0.aar | PROTECTED |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0/firebase-app-unity-13.16.0.aar.meta | PROTECTED |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0/firebase-app-unity-13.16.0.pom | PROTECTED |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0/firebase-app-unity-13.16.0.pom.meta | PROTECTED |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity.meta | PROTECTED |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0.meta | PROTECTED |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0/firebase-auth-unity-13.16.0.aar | PROTECTED |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0/firebase-auth-unity-13.16.0.aar.meta | PROTECTED |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0/firebase-auth-unity-13.16.0.pom | PROTECTED |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0/firebase-auth-unity-13.16.0.pom.meta | PROTECTED |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity.meta | PROTECTED |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0.meta | PROTECTED |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0/firebase-firestore-unity-13.16.0.aar | PROTECTED |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0/firebase-firestore-unity-13.16.0.aar.meta | PROTECTED |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0/firebase-firestore-unity-13.16.0.pom | PROTECTED |
| client/DominoGame/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0/firebase-firestore-unity-13.16.0.pom.meta | PROTECTED |
| client/DominoGame/Assets/Plugins/Android/FirebaseApp.androidlib.meta | PROTECTED |
| client/DominoGame/Assets/Plugins/Android/FirebaseApp.androidlib/AndroidManifest.xml | PROTECTED |
| client/DominoGame/Assets/Plugins/Android/FirebaseApp.androidlib/project.properties | PROTECTED |
| client/DominoGame/Assets/Plugins/Android/FirebaseApp.androidlib/res/values/google-services.xml | PROTECTED |
| client/DominoGame/Assets/Plugins/Android/gradleTemplate.properties | PROTECTED |
| client/DominoGame/Assets/Plugins/Android/gradleTemplate.properties.meta | PROTECTED |
| client/DominoGame/Assets/Plugins/Android/mainTemplate.gradle | PROTECTED |
| client/DominoGame/Assets/Plugins/Android/mainTemplate.gradle.meta | PROTECTED |
| client/DominoGame/Assets/Plugins/Android/settingsTemplate.gradle | PROTECTED |
| client/DominoGame/Assets/Plugins/Android/settingsTemplate.gradle.meta | PROTECTED |
| client/DominoGame/Assets/_Domino/Scripts/Auth/AccountProtectionController.cs | GUEST_ACCOUNT_PRODUCT |
| client/DominoGame/Assets/_Domino/Scripts/Auth/AccountProtectionController.cs.meta | GUEST_ACCOUNT_PRODUCT |
| client/DominoGame/Assets/_Domino/Scripts/Identity/AccountLinkController.cs | GUEST_ACCOUNT_PRODUCT |
| client/DominoGame/Assets/_Domino/Scripts/Identity/AccountLinkController.cs.meta | GUEST_ACCOUNT_PRODUCT |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/AccountProtectionText.cs | GUEST_ACCOUNT_LOCALIZATION |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/AccountProtectionText.cs.meta | GUEST_ACCOUNT_LOCALIZATION |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/Editor/GuestAccountLinkPreview.cs | TEMPORARY_VALIDATION |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/Editor/GuestAccountLinkPreview.cs.meta | TEMPORARY_VALIDATION |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/Editor/GuestAccountProtectionPreview.cs | TEMPORARY_VALIDATION |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/Editor/GuestAccountProtectionPreview.cs.meta | TEMPORARY_VALIDATION |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/Editor/GuestAccountVerificationPreview.cs | TEMPORARY_VALIDATION |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/Editor/GuestAccountVerificationPreview.cs.meta | TEMPORARY_VALIDATION |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/Editor/PlayerUi01EObservation.cs | TEMPORARY_VALIDATION |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/Editor/PlayerUi01EObservation.cs.meta | TEMPORARY_VALIDATION |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/Editor/ProductionAliasAvailabilityPreview.cs | TEMPORARY_VALIDATION |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/Editor/ProductionAliasAvailabilityPreview.cs.meta | TEMPORARY_VALIDATION |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/Editor/ProductionCountryPopupValidation.cs | TEMPORARY_VALIDATION |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/Editor/ProductionCountryPopupValidation.cs.meta | TEMPORARY_VALIDATION |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/Editor/ProfileEditPreview.cs | PLAYER_PROFILE_EDIT |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/Editor/ProfileEditPreview.cs.meta | PLAYER_PROFILE_EDIT |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/Editor/Smoke02Harness.cs | TEMPORARY_VALIDATION |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/Editor/Smoke02Harness.cs.meta | TEMPORARY_VALIDATION |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/Editor/Smoke02Observer.cs | TEMPORARY_VALIDATION |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/Editor/Smoke02Observer.cs.meta | TEMPORARY_VALIDATION |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/ProductionAccountProtectionOverlay.cs | GUEST_ACCOUNT_PRODUCT |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/ProductionAccountProtectionOverlay.cs.meta | GUEST_ACCOUNT_PRODUCT |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/ProductionProfileEditView.cs | PLAYER_PROFILE_EDIT |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/ProductionProfileEditView.cs.meta | PLAYER_PROFILE_EDIT |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/ProductionProtectAccountView.cs | GUEST_ACCOUNT_PRODUCT |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/ProductionProtectAccountView.cs.meta | GUEST_ACCOUNT_PRODUCT |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/ProfileEditController.cs | PLAYER_PROFILE_EDIT |
| client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/ProfileEditController.cs.meta | PLAYER_PROFILE_EDIT |
| client/DominoGame/ProjectSettings/AndroidResolverDependencies.xml | PROTECTED |
| client/DominoGame/ProjectSettings/ScriptableBuildPipeline.json | PROTECTED |
| client/Validation/DEPLOY_PREFLIGHT_01_BE07_ROLLOUT_READINESS_REPORT.md | HISTORICAL |
| client/Validation/FUNCTIONAL_00_AUTH_PLAYER_BACKEND_AUDIT.md | HISTORICAL |
| client/Validation/GUEST_ACCOUNT_01_PROTECTION_AND_LINKING_AUDIT_REPORT.md | GUEST_ACCOUNT_REPORT |
| client/Validation/GuestAccount01ATests.cs | GUEST_ACCOUNT_TEST |
| client/Validation/GuestAccount01BTests.cs | GUEST_ACCOUNT_TEST |
| client/Validation/GuestAccount01CTests.cs | GUEST_ACCOUNT_TEST |
| client/Validation/LOCAL_SERVER_PREFLIGHT_01_BACKEND_AUDIT_REPORT.md | HISTORICAL |
| client/Validation/LOCAL_TEST_BE07_DEPLOY_01_REPORT.md | HISTORICAL |
| client/Validation/LOCAL_TEST_BE07_SMOKE_02_REPORT.md | HISTORICAL |
| client/Validation/LOCAL_TEST_FOUNDATION_DEPLOY_01_REPORT.md | HISTORICAL |
| client/Validation/NEW_PLAYER_FOUNDATION_LIVE_01_REPORT.md | HISTORICAL |
| client/Validation/ONB_00A_BACKEND_DOMAIN_API_DESIGN.txt | HISTORICAL |
| client/Validation/ONB_00_BACKEND_CONTRACT_FINAL_REVIEW.md | HISTORICAL |
| client/Validation/ONB_00_ONBOARDING_PLAYER_COACH_MEMBERSHIP_DESIGN.txt | HISTORICAL |
| client/Validation/ONB_LIVE_01A_REAL_SESSION_ROUTING_REPORT.md | HISTORICAL |
| client/Validation/ONB_LIVE_01_NEW_USER_E2E_REPORT.md | HISTORICAL |
| client/Validation/PLAYER_PROFILE_EDIT_01_REPORT.md | PLAYER_PROFILE_EDIT |
| client/Validation/PlayerSessionIsolationTests.cs | HISTORICAL |
| client/Validation/ProfileEditTests.cs | PLAYER_PROFILE_EDIT |
| client/Validation/RunGuestAccount01ATests.ps1 | GUEST_ACCOUNT_TEST |
| client/Validation/RunGuestAccount01BTests.ps1 | GUEST_ACCOUNT_TEST |
| client/Validation/RunGuestAccount01CTests.ps1 | GUEST_ACCOUNT_TEST |
| client/Validation/RunPlayerSessionIsolationTests.ps1 | HISTORICAL |
| client/Validation/RunProfileEditTests.ps1 | PLAYER_PROFILE_EDIT |
| client/Validation/RunSmoke02ObserverTests.ps1 | TEMPORARY_VALIDATION |
| client/Validation/Smoke02CompositionTests.cs | TEMPORARY_VALIDATION |
| client/Validation/Smoke02ObserverTests.cs | TEMPORARY_VALIDATION |
| client/Validation/Smoke02Snapshot.py | TEMPORARY_VALIDATION |
| client/Validation/Smoke02SnapshotTests.py | TEMPORARY_VALIDATION |
| client/Validation/TEST_BE07_01_DEPLOY_SMOKE_REPORT.md | HISTORICAL |
| client/Validation/TEST_CATALOG_01_PUBLICATION_REPORT.md | HISTORICAL |
| client/Validation/TEST_USER_DELETION_IMPACT_REVIEW.md | HISTORICAL |
| client/Validation/__pycache__/CapacityCoordinator.cpython-312.pyc | PROTECTED |
| client/Validation/__pycache__/CapacityDiscoveryTests.cpython-312.pyc | PROTECTED |
| client/Validation/__pycache__/CapacityInstrumentationTests.cpython-312.pyc | PROTECTED |
| client/Validation/__pycache__/CapacityMetrics.cpython-312.pyc | PROTECTED |
| client/Validation/__pycache__/CapacityRegistry.cpython-312.pyc | PROTECTED |
| client/Validation/__pycache__/S707TimingAnalysis.cpython-312.pyc | PROTECTED |
| client/Validation/__pycache__/S708TimingAnalysis.cpython-312.pyc | PROTECTED |
| server/domino/src/main/kotlin/com/teamfho/domino/player/PlayerProfileEditing.kt | PLAYER_PROFILE_EDIT |
| server/domino/src/test/kotlin/com/teamfho/domino/player/PlayerProfileEditHttpTests.kt | PLAYER_PROFILE_EDIT |
| server/domino/src/test/kotlin/com/teamfho/domino/player/PlayerProfileEditingTests.kt | PLAYER_PROFILE_EDIT |
