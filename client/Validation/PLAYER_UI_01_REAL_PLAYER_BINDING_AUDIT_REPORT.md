# PLAYER-UI-01 — Real Player binding audit and contract design

Current implementation status is recorded in **PLAYER_UI_01A_IMPLEMENTATION** at the end. The audit/design sections below retain their original pre-implementation observations and evidence scope.

Base: `3fc9f0f5882f163a879cd9625ab0f1b3117388c8`, branch `main`. Environment: TEST. Source audit performed on 2026-10-02. **Audit/design only; no implementation or live request.**

The authenticated production shell constructs demo data sources instead of consuming the existing authenticated Player state. The observed `Alex · Demo player` and `FREE · Cuban Domino Club` are therefore explained by production composition, not by missing authentication or failed onboarding. There is no current shared production Player presentation binding owner.

The minimal binding can reuse existing contracts for display name, effective membership, and the completed v2 profile. Full replacement of the Profile join date requires an additive backend response field: Player `createdAt` exists in persistence but is not exposed by the audited APIs. This is a response-contract gap, not a missing database field. Generalizing profile/Coach retrieval to legacy-exempt and unpinned onboarding also needs a separate contract decision; the current completed v2 account does not need that change to expose its saved values.

## Evidence and scope

- Local HEAD matches the authorized base. The 132 preexisting pending files match the Country checkpoint inventory byte-for-byte. All 102 protected files remain unchanged.
- Current source, rather than older Auth implementation reports, establishes the contracts below. Previously approved live evidence is retained from `ONB_LIVE_01_NEW_USER_E2E_REPORT.md` and sanitized `Generated/OnbLive01/restore-e-result.json` / `restore-e-after.json`; it is not represented as a new live inspection.
- Safe identity alias throughout: `E2E_PLAYER`. No email, UID, real Player identifier, password, token, or session contents were collected into this report.
- Retained snapshot at `2026-10-02T18:22:07.728528+00:00`: Player active/registered, foundation 5/5, onboarding COMPLETED/v2/revision 7; first name, last name and display name present; country `US`; language/preferred locale `es`; experience `RULES_KNOWN`; Coach `SOFIA`, selected Coach catalog 1; trial not consumed, grants 0. These are retained data-presence observations, not proof that each value is already bound to a visible widget.
- No Play interaction, logout, account switch, bootstrap, onboarding GET, Social GET, trial activation, test execution, commit, push or deployment was performed.

## Source landmarks

Client paths in this report are relative to `client/DominoGame/Assets/_Domino/`; backend paths are relative to `server/domino/src/main/kotlin/com/teamfho/domino/`.

| Source | Responsibility |
|---|---|
| `Scripts/Infrastructure/Firebase/FirebaseSdkClient.cs:GetSession`, `FirebaseAuthService.cs:RestoreAsync` | Firebase session restoration and safe in-memory identity snapshot |
| `Scripts/Infrastructure/ApplicationServices.cs:Start`, `Shutdown`, `Reset` | Creates the session-owned Player service, routing composition and transport; tears them down |
| `Scripts/Infrastructure/Api/DominoApiClient.cs:BootstrapAsync`, `PlayerBootstrapDtos.cs`, `PlayerSnapshotMapper.cs` | Supported Player bootstrap wire contract and validation |
| `Scripts/Player/PlayerService.cs:RunAsync`, `ReceiveEntitlements`, `Dispose`; `PlayerSnapshot.cs` | Existing authenticated Player/wallet/entitlement owner |
| `Scripts/UI/AppShell/ProductionRoutingComposition.cs:Binding.BootstrapAsync`, `ReadOnboardingAsync` | Uses that same Player service and CLIENT-01 onboarding session |
| `Scripts/Auth/AuthenticatedRoutingOrchestrator.cs:RunAsync`, `Onboarding` | Validates identity, revision and catalog; owns the confirmed routing onboarding snapshot |
| `Scripts/UI/AppShell/ProductionAuthHost.cs:Render`, `Rebind` | Builds the actual production shell after Home routing |
| `Scripts/UI/AppShell/ProductionAppShell.cs:46,55` | Instantiates demo Profile/Home/Menu/Learn/Puzzles/Watch sources |
| `Scripts/UI/AppShell/HomeData.cs`, `MenuData.cs`, `ProfileData.cs`, `RootContentData.cs` | Presentation DTOs/interfaces and demo providers |
| `player/Player.kt`, `PlayerBootstrapResponse.kt`, `PlayerController.kt` | Backend model and five-field Player response |
| `player/OnboardingProgressService.kt:32-50`, `OnboardingProgressModels.kt`, `OnboardingFoundation.kt` | Read-only profile/answer projection from authoritative Player/preferences/domino documents |
| `entitlement/EntitlementModels.kt`, `EntitlementService.kt`; `catalog/MembershipCatalog.kt` | Effective access authority versus commercial catalog presentation |

Existing pending observer additions in ApplicationServices/ProductionRoutingComposition and pending sanitized diagnostics in AuthenticatedRoutingOrchestrator were identified separately. They are not prerequisites for this design and must not be swept into a future Player binding commit.

## Current data flow and ownership

```text
Firebase SDK persisted authentication
  -> FirebaseSdkClient / FirebaseAuthService
  -> ProductionAuthRouter -> ProductionRoutingComposition
  -> PlayerService.InitializeAsync
     -> DominoApiClient POST /api/v1/player/bootstrap
     -> FirebaseAuthenticationFilter / FirebaseAdminTokenVerifier
     -> PlayerController / PlayerBootstrapService / repository.ensure
     -> PlayerBootstrapResponseDto
     -> PlayerSnapshotMapper -> ApplicationServices.Player (PlayerService)
  -> existing OnboardingApiSession / OnboardingApiClient
     -> GET /api/v1/player/onboarding
     -> AuthenticatedRoutingOrchestrator.Onboarding
  -> authoritative COMPLETED -> Home/AppShell
     -> ProductionAuthHost creates ProductionAppShell without Player input
     -> demo data sources -> Home / Menu / Profile / Learn
```

`BOOTSTRAP_PLAYER_DTO=PlayerBootstrapResponseDto.player:PlayerResponseDto` mirrors backend `PlayerBootstrapResponse.player:PlayerResponse`.

`PLAYER_CLIENT_STATE_OWNER=ApplicationServices.Player:PlayerService`. It owns `Player`, `Wallet`, `Entitlements`, `TrialEligibility`, sync/error state and notifications. It validates response UID against the active Firebase identity before accepting bootstrap. `SnapshotChanged`, `SyncStateChanged` and `EntitlementsChanged` are the existing observation seams. `PlayerService.Dispose` clears snapshots and subscribers.

`APP_SHELL_PLAYER_BINDING_OWNER=NONE`. Home has `Refresh()` but its source is demo. Menu reads its source once in the constructor. Profile similarly captures `ReadProfile()` and `ReadGames()` while constructing the page. None subscribe to PlayerService. There is no production PlayerStore or PlayerSession to replace with a competing store.

Onboarding has a separate **domain** owner: CLIENT-01's `OnboardingApiClient` caches a deep-copied response with nondecreasing revisions; the routing orchestrator exposes its confirmed copy. The onboarding controller also holds editable step state, but is not a Player UI authority. In particular, restoring a completed account reads onboarding through the routing API without loading an onboarding screen: `composition.Onboarding.State` can be uninitialized even while `composition.Router.Onboarding` contains the completed response. Do not bind Home to the editable controller or its drafts.

No Player/profile disk cache was found in PlayerService, PlayerSnapshot or CLIENT-01 onboarding state. Firebase persists authentication; `domino.language` persists the application's locale preference; the game catalog cache is content. None is an authenticated Player presentation cache. Do not introduce one for this phase.

## Demo inventory and visible surfaces

The reproducible search covers authored `_Domino` C#/JSON/UXML/USS and validation C#/JSON, excluding generated output. It searches the specified identity/plan/brand literals plus demo-source references, and adds the production AppShell's other demo/fictional/Country/Coach markers. A reference count means a **distinct matching source line**, including declarations and composition references; it is not a widget count. Every matching line is enumerated in the appendix.

| Classification | Matching lines | Interpretation |
|---|---:|---|
| PRODUCTION_VISIBLE_DEMO | 26 | Sources reachable from the real production shell, including demo defaults |
| MOCK_ONLY | 18 | AppShellMock data/rendering; preserve as visual reference |
| TEST_FIXTURE | 52 | Validation/Editor fixtures/assertions; not real identity evidence |
| LOCALIZATION | 5 | Product brand/copy and translation resources, not a fake user |
| UNRELATED | 9 | Five legacy gameplay-name references and four genuine entitlement/catalog code references |
| FALLBACK | 0 | No separate neutral fallback among the literal-search hits; existing neutral-name conventions are audited below |
| UNCLASSIFIED | 0 | All search results classified |

The production `?? new DemoProfileDataSource()` default is counted as production-visible demo, not as a safe fallback. `FREE` comparisons in real entitlement/catalog code are not demo identity. `Cuban Domino Club` is product branding; its placement next to a fabricated membership does not make the brand a Player field.

| Surface | Current source / values | Classification and future boundary |
|---|---|---|
| Home greeting | DemoHomeDataSource: `Alex`; renders `Hola, Alex.` | TEMP_DEMO_DATA; bind authoritative display name |
| Home Coach | `amara`, Amara, fixed Spanish greeting, Coach portrait | TEMP_DEMO_DATA; saved Coach selection plus versioned Coach catalog |
| Home friends | Count 5, `fictional players` | TEMP_DEMO_DATA; Social-owned summary, not Player bootstrap |
| Menu identity card | `demo-alex`, `Alex · Demo player` | TEMP_DEMO_DATA; use authenticated Player display name |
| Menu membership/product line | Fixed `FREE` plus `Cuban Domino Club` | TEMP_DEMO_DATA for plan; LOCALIZATION/brand for product text |
| Menu avatar | `AppShellMockIcons/icon_menu_avatar` vector | Neutral static presentation fallback, not a personal avatar |
| Profile identity | Demo Alex or other-profile Pedro | TEMP_DEMO_DATA; current own-profile binding only |
| Profile avatar | Mateo for own profile; David for other fixture | TEMP_DEMO_DATA; Coach portrait incorrectly stands in for a Player photo |
| Profile country/flag | `CU`, Cuba, `icon_flag_cu` | TEMP_DEMO_DATA; actual saved code is independent of this fixture |
| Profile joined | `new DateTime(2022,1,21)` | TEMP_DEMO_DATA; no current client contract for real join date |
| Profile history | Eight fake games with names, scores, results and Coach portraits; first five shown in Profile | DEMO_ONLY in this view; existing History/Replay services remain owners |
| Learn / Your Coach | A second DemoHomeDataSource supplies Amara | TEMP_DEMO_DATA; must share the same selected Coach projection as Home |
| Puzzles | Intermediate / `0 / 4 demo challenges` and static categories | Demo content/progress; not the Player's collected experience or real progression |
| Watch | Three fictional matches, participant names and durations | Demo content; not current Player history, requires its own future integration |
| Menu Stats, Coach, Membership and other destinations | `Coming Soon` placeholder subpages | FUTURE; no real Player stats or selected Coach page yet |
| Header / root branding | CUBAN DOMINO CLUB, YOUR CORNER, page titles | Product copy; keep separate from identity |
| Legacy BoardView / DominoClientController | Fixed local gameplay seat names | UNRELATED to this shell; ProductionAuthEntry disables legacy controllers in production Auth |

`EXPERIENCE_CURRENT_UI_SURFACES=NONE_IN_APP_SHELL`. The onboarding Experience view is the only current selected-experience UI. Puzzles' “Intermediate” is puzzle difficulty, not Player experience. Do not add an experience line to Menu.

`COUNTRY_CURRENT_UI_SURFACES=PROFILE_ONLY_OUTSIDE_ONBOARDING`. Do not add flags or location to Home/Menu. The existing BasicProfile Country formatter uses RegionInfo EnglishName with ISO fallback; complete localized country names are not implemented. No new country-display system is silently included here.

## Supported-field matrix

“Available” below means a supported client contract can supply the field, not that it is in PlayerSnapshot or currently visible. No Firestore read is proposed for Unity.

| Field | Authoritative storage/model | Bootstrap returns | Existing supported client access / owner | Current production use / gap |
|---|---|---|---|---|
| Internal Player ID / Firebase UID | Player.uid; foundation verifies document identity | Yes, `player.uid` | PlayerSnapshot.Uid; FirebaseAuthSessionSnapshot.Uid | Correlation only; never visible and not a Social publicPlayerId |
| accountType / status | Player | Yes | PlayerSnapshot.AccountType / Status | Available; not bound to current shell identity |
| displayName | Player.displayName | Yes | PlayerSnapshot.DisplayName; v2 `Router.Onboarding.basicProfile.displayName` also projects current Player | REAL_BINDABLE_NOW; remove demo name |
| firstName / lastName | Player optional fields | No | v2 BasicProfileDto via existing GET onboarding | REAL_BINDABLE_NOW for completed v2; absent on v1/legacy-exempt response; not a reason to expose personal full name in Menu |
| country / countryCode | Player.countryCode; no separate stored country-name field | No | v2 BasicProfileDto.countryCode | Code real-bindable; name is derived presentation, not backend text |
| preferredLocale | PlayerPreferences.preferredLocale | No as that field; `player.language` exists | v2 BasicProfileDto.preferredLocale; local UI locale has separate ownership | Real-bindable v2; do not mutate locale preferences just to display profile |
| timeZone | PlayerPreferences.timeZone | No | v2 BasicProfileDto.timeZone | Optional; no current shell surface |
| experience | DominoProfile.experienceLevel | No | OnboardingStateDto.answers question `DOMINO_EXPERIENCE` / SINGLE_SELECT, catalog binding `EXPERIENCE_LEVEL`; projected from domain by GET | Present for versioned flow; no current shell surface |
| preferredCoachKey | DominoProfile.preferredCoachKey | No | OnboardingStateDto.answers `COACH_SELECTION` / COACH_SELECT | Supported for v1/v2; current Home/Learn still use Amara |
| selectedCoachCatalogVersion | DominoProfile.selectedCoachCatalogVersion | No | Direct field exists in DominoProfileDto on relevant mutation response, not GET state; GET pinned onboarding catalog supplies coachCatalogVersion | Resolve exact version through existing catalog contract for completed FLOW; never assume latest catalog |
| membership/effective access | EntitlementResolver.resolve | Yes, `entitlements` | PlayerService.Entitlements, EffectiveEntitlementsDto; GET player/entitlements refresh | Real-bindable; UNKNOWN/UNAVAILABLE must not become FREE |
| trial state/eligibility | Entitlement state/grants/policy | Yes | Entitlements.snapshot.trialActive/trialConsumed/trialEndsAt/status; PlayerService.TrialEligibility | Real-bindable; no activation from render |
| Player avatar | No Player avatar field/selection pipeline | No | Public Social DTO has optional avatarKey, but repository creates/resolves profiles without populating it | FUTURE; not an implemented avatar system |
| createdAt / Joined | Player.createdAt | No | Not in PlayerResponseDto, BasicProfileDto or Social public response | MISSING_CLIENT_CONTRACT caused by backend response gap; do not substitute Auth creation or onboarding completedAt |
| email / emailVerified | Firebase Auth / verified server identity | No Player fields | FirebaseAuthSessionSnapshot.DisplayEmail / IsEmailVerified | Auth-only; excluded from public Player UI |
| wallet coins | wallet/main | Yes | PlayerService.Wallet | Available but not shown in current shell; do not add a balance UI |
| stats | No aggregate Player-stats model/endpoint found in audited product source | No | Stats destination is placeholder; historical match results are not an all-time aggregate | FUTURE; never calculate fake stats from a capped history page |
| game history/replay | PlayerMatchHistoryRepository and match/replay authority | No | GET /api/v1/players/me/history; ReplayClient.History; JObject metadata | Existing supported service, not bound to ProductionProfilePage; preserve access limits and ownership |
| friends/count | Friendship service and Social summary | No | SocialClient.Summary / Friends | Separate Social authority, not bootstrap; integration not part of identity projection |

The completed v2 GET reconstructs answers from **current domain values**, not from an editable draft. It includes BasicProfile only if the pinned catalog has profile bindings. Catalog v1 and LEGACY_EXEMPT with null version therefore cannot be treated as if they guaranteed v2 profile fields. The returned domain revision vector must be retained.

The current completed E2E Player's supported field paths are established by code plus retained successful GET evidence; this audit did not capture a new response body or inspect the running in-memory PlayerService. The effective plan in the live current process is consequently not asserted from the hardcoded Menu label. Retained zero grants imply FREE under the audited resolver, but UI must still read its own authoritative AVAILABLE snapshot.

## Single-source presentation design

**One existing Player owner, one derived UI projection, multiple views.** Extend the existing PlayerService/PlayerSnapshot contract only as needed; do not create a competing PlayerSession, independently fetching Home/Menu/Profile stores, or a Firebase-derived UI identity.

```text
Confirmed bootstrap -> PlayerService.Player / Entitlements
Confirmed routing GET onboarding -> session-checked enrichment of same owner
Versioned Coach catalog -> shared catalog presentation lookup
                    |
           one read-only Player UI projection
                    |
             Home / Menu / Profile / Learn
```

Proposed PLAYER-UI-01A contract:

1. Preserve PlayerService as the sole owner of the authenticated Player snapshot. Add optional profile/domino presentation fields and provenance (session generation, profile/preferences/domino revisions, catalog version, loaded/unavailable state). These are confirmed server inputs, not a second editable domain model.
2. At the existing composition boundary, forward the already-read onboarding response through a session-bound acceptance method **before** publishing the ready UI projection. Do not execute an extra bootstrap, start onboarding, save an answer or force completion to acquire the data. Do not change routing decisions.
3. `PlayerService.Player.DisplayName` remains the canonical name. For the first registration flow, bootstrap occurred before Basic Profile was saved, and `InitializeAsync()` returns immediately once SYNCED. Therefore bootstrap-only binding would show the old generated alias after completion. Accept the final confirmed GET's basicProfile display name into that same snapshot; do not keep a separate Home-only name override or use controller draft.
4. Honor all three domain revisions independently. Reject late results from an older session/service instance or older accepted revision. Treat contradictory values at an equal comparable revision as a contract conflict, not as a new overwrite. Catalog version is not a timestamp.
5. The existing display-name update endpoint omits profileRevision. A future consumer must not replay an older cached onboarding response over a newer alias update; invalidate/enrich through a fresh supported read or add the revision to that future edit contract. Profile editing is not authorized here.
6. A single presentation adapter reads the owner and shared catalog result and publishes immutable view summaries/change notifications. Home/Menu/Profile data-source interfaces may be adapters to it, but cannot own separate authoritative Players or start independent API requests. Detach subscriptions when the shell/session is disposed.
7. Keep Social and History/Replay services as separate domain owners. They may supply their own read-only sections under the same session guard; do not move them into PlayerSnapshot or infer their values from sample cards.

`PLAYER_PRESENTATION_SOURCE_COUNT=1` is the proposed invariant, not a claim that such binding already exists. The current binding count is zero.

## Name, membership, trial, Coach and avatar policy

**Name / Menu.** Use the authoritative `displayName` exactly as user data; never translate it. Menu already has two text lines, not three: primary `displayName`; secondary localized effective-membership label, middle dot, unchanged product brand `Cuban Domino Club`. Remove `· Demo player`; do not replace it with firstName + lastName, email, UID or an invented experience descriptor. Personal first/last names remain available for a separately authorized own-profile details/edit screen.

The existing legacy PlayerProfileView uses `profile.title` when no Player is present and `profile.choose` (“Choose your name” / “Elige tu alias”) for a generated name. These are proven neutral conventions, not Alex fallbacks. For the ready shell, a valid backend-generated alias may be shown as the real stored alias; do not manufacture an edit CTA where editing is unavailable. An absent/invalid required displayName fails snapshot validation: routing/loading/error presentation handles that, rather than substituting an email or arbitrary name. Optional missing names/country/date are omitted or shown as unavailable with existing localized copy, never fabricated.

**Membership.** `MEMBERSHIP_DISPLAY_SOURCE=SERVER_DERIVED_ENTITLEMENT_STATE`. Require `availability=AVAILABLE` and a valid snapshot. The runtime `Plan` enum is `{FREE, PREMIUM}`. Commercial `GOLD`, `PLATINUM`, `DIAMOND`, `FRIENDS_AND_FAMILY` are catalog plans; MembershipTargetPolicy explicitly is not the runtime SubscriptionPolicy and is required inactive. A selected onboarding tab is not owned access. Family's catalog `effectivePlanKey=DIAMOND` is a policy/presentation declaration, not evidence that this user is a Family member. There is currently no authoritative owned-commercial-plan field to bind.

**Legacy Premium.** Existing localization renders runtime PREMIUM as “Premium” in both languages. Retain that label; `PREMIUM_LEGACY` identifies the catalog's trial product. Do not silently convert either to Gold, Platinum, Diamond or Family. If a future server contract supplies owned commercial plan keys, use an explicit versioned mapping then; no such backend change is implemented or inferred now.

**Trial.** Trial is a qualifier of effective Premium access, not another purchased plan. Use server `trialActive` and `trialEndsAt`, localized trial text/badge and actual status. Existing `premium.trial`, `premium.expired`, `premium.active`, `premium.free` and `premium.unavailable` provide semantic conventions. `trialConsumed` alone is not proof a trial is currently active, nor a reason to demote another active grant. Do not replay the initial grant welcome on restore merely because a snapshot exists. Unknown entitlement state shows unavailable, never FREE. At a server-provided nextTransitionAt, any future refresh belongs centrally to the entitlement owner and must reread the server; the client clock cannot grant/extend access. No timer, refresh, or activation was run in this audit.

**Coach.** Read saved COACH_SELECTION from the confirmed versioned onboarding response, fetch/reuse the matching onboarding catalog for its coachCatalogVersion, then use `CoachCatalogApiClient` for that version and locale. A completed FLOW was checked against that version by the server; the GET alone does not expose DominoProfile.selectedCoachCatalogVersion directly. Do not use `CoachSelection` draft, first selectable Coach, Amara fallback, or latest unpinned catalog as identity. Home and Learn must consume one resolved Coach summary. `CoachAvatarResources` is an asset allowlist, not a selection authority. The versioned Coach endpoint includes historical inactive entries, and `AuthoritativeCoachValidation.validateExisting` preserves them: display the saved historical Coach when its entry is available even if it is no longer selectable. For an actually missing entry/catalog, preserve the key and show unavailable/neutral content; never auto-select another Coach. LEGACY_EXEMPT without a pinned catalog remains unavailable until a supported canonical profile contract exists.

**Avatar.** `PLAYER_AVATAR_IMPLEMENTED=NO`. PublicPlayerProfile.avatarKey is a nullable placeholder, not evidence of an implemented selection/storage/renderer flow: FirestoreSocialRepository does not populate it. Current Menu uses a neutral vector avatar; Profile uses a Coach portrait as demo identity. Propose the existing neutral Menu person/avatar asset for the real Player fallback, with no claim it is a personal image. Coach portraits remain Coach portraits. No asset removal or Mock edits are required.

## Binding matrices

### Menu

| Element | Classification | Proposed binding |
|---|---|---|
| Primary name | REAL_BINDABLE_NOW | Shared projection of confirmed PlayerService.Player.DisplayName |
| `Demo player` suffix | DEMO_ONLY | Remove suffix; no replacement concept |
| Membership text | REAL_BINDABLE_NOW | Available server effective entitlement; localized FREE/Premium/trial qualifier, not catalog selection |
| Product text | LOCALIZATION | Keep brand; no profile lookup |
| Avatar | FUTURE personal avatar / existing safe fallback | Neutral `icon_menu_avatar`; never Coach portrait |
| Profile target ID | REAL_BINDABLE_NOW internally | Current own Player identity, kept opaque; do not pass Firebase UID to public Social-ID routes |
| Friends/Stats/Coach/Membership destinations | FUTURE integration | Existing routes/placeholders unchanged; no new navigation feature |

### Home and shared Learn Coach

| Element | Classification | Proposed binding |
|---|---|---|
| Greeting name | REAL_BINDABLE_NOW | Same canonical displayName as Menu; localize greeting pattern, not the name |
| Coach name/portrait/description | REAL_BINDABLE_NOW via existing versioned read contracts | Shared saved Coach + pinned Coach catalog resolution; map catalog content rather than Amara fixture |
| Home/Learn Coach consistency | UI_BINDING_GAP | Same projection and version; no independent selection/cache |
| Five friends card | DEMO_ONLY currently; existing Social API available | Defer real Social integration or show unavailable; never report 0 or 5 as a fetched count without evidence |
| Continue Learning | Existing action | Preserve route/geometry; do not infer learning progress or Coach AI |
| Membership / Player avatar / stats / own game history | No current Home surface | Do not add new cards merely because a field exists |
| Puzzles progress and Watch feed | DEMO_ONLY / FUTURE | Separate domain integration; do not relabel demo activity as the Player's progression/history |

### Profile

| Field / section | Classification | Proposed binding / boundary |
|---|---|---|
| Own Player display name | REAL_BINDABLE_NOW | Same canonical snapshot as Menu/Home |
| Own/internal ID | REAL_BINDABLE_NOW, private | Session identity only; not a visible string |
| Country code/name | REAL_BINDABLE_NOW for completed v2 | Authoritative code; derived country label with safe ISO fallback; no Cuba default |
| Country flag | DEMO_ONLY fixed Cuba asset | Never show Cuba for another country; no new flag system in this phase, use absent/neutral presentation pending scope approval |
| Joined date | MISSING_CLIENT_CONTRACT | Additive backend createdAt response plus client DTO/state mapping if preserving this value is required; otherwise omit pending authorization |
| Personal avatar | FUTURE | Existing neutral vector fallback; no fabricated image |
| First/last names / preferredLocale | REAL_BINDABLE_NOW v2, no current Profile display | Do not add fields or edit inputs in this binding phase |
| Experience / Coach / membership | No current Profile presentation surface | Keep in shared contract where applicable, do not add UI solely to expose them |
| History / recent five / View All | DEMO_ONLY current view, supported separate History API | Future adapter to existing ReplayClient, pagination, metadata and access limits; don't fabricate games, scores, result or opponent avatar |
| Game details / Share / Edit Profile | FUTURE placeholders | Preserve existing scope; no mutation implementation |
| Other-player relationship state | TEST_FIXTURE/FUTURE for this composition | Current own-profile only; real friend state is Social-owned, not a local enum toggle |

History limits are server-derived. The production `MaxVisibleGames=5` is a visual slice; it does not authorize unlimited history/replay. Existing history responses include nextCursor, historyLimited, participants' historical display-name snapshots, selfSeat, teams, replayAvailabilityReason and premiumLocked. Do not replace historical participant names with today's Player name or derive lifetime stats from that bounded page.

Social summary/profile GETs are **not guaranteed write-free**: SocialController.ready calls identity.ensure, which can create public identity/profile/code/settings. They must not be smuggled into this read-only audit or into a supposedly mutation-free core Player bind. Friends integration requires its own explicit scope and existing Social protection; no calls were made.

## Session isolation, loading and failure

- Reuse the routing orchestrator's generation/cancellation epoch and CLIENT-01's `EnsureCurrent`; bind the presentation to the captured PlayerService instance plus session generation, not just a UID string. This also rejects late responses when the same account logs in again under a new session.
- Normal logout stops/disposes routing, shuts down PlayerService/transports, signs out locally and recreates ApplicationServices; SessionReplaced causes ProductionAuthHost.Rebind. Clear/unsubscribe the projection when that lifetime ends. A late callback from A must not populate B's view even if it completes after cancellation.
- During ResolvingSession/ResolvingPlayer/ResolvingOnboarding, retain ProductionRoutingStatusView's existing loading convention; don't mount a demo shell. Ready identity is shown only when the existing routing owner allows AppShell. Optional Coach/membership sections can be unavailable without inventing a fake identity.
- Bootstrap errors remain routing-owned (`Error` or `UpdateRequired`); do not substitute a demo profile or initiate a second view-owned retry. The existing generic “Could not connect” wording is not redesigned in PLAYER-UI.
- PlayerService currently retains confirmed snapshots after a same-session refresh failure. Presentation must distinguish stale/unavailable from ready and must never expose them under a new session. Clear data on identity/lifetime replacement, not on a cancelled logout confirmation.
- Reopening Menu/Profile, tab refresh and locale changes must reuse the same owner; they cannot issue bootstrap, save onboarding, change membership selection or activate trial.

Design invariants: `PLAYER_UI_CLEARED_ON_SESSION_CHANGE=YES`, `STALE_PLAYER_PRESENTATION_APPLIED=NO`, `PLAYER_UI_REQUIRES_ONBOARDING_WRITE=NO`. These are requirements for subsequent implementation/regression, not newly executed session-switch tests.

## Contract gaps and minimal implementation phases

`BACKEND_CONTRACT_CHANGE_REQUIRED=YES` **for complete real binding of the existing Profile Joined field**: expose Player.createdAt from a supported authenticated response. No schema change is needed. Backend changes are **not required** for the initial Menu/name/effective-membership binding or for current completed-v2 country/name/Coach access. If Joined is deferred/omitted, the initial client-only slice can proceed after authorization.

For universal profile/Coach access independent of onboarding versions, explicitly decide a later additive Player response/projection for firstName, lastName, countryCode, profileRevision, preferences.preferredLocale/revision and domino.experienceLevel/preferredCoachKey/selectedCoachCatalogVersion/revision. Those fields already exist in persistence; the v2 GET is a supported immediate source, but v1/legacy/unpinned GET does not guarantee all of them. Do not invent new storage, re-open onboarding, or read Firestore directly to bridge that difference. Owned commercial membership tiers need a future entitlement product contract, not a Player UI mapping trick.

`CLIENT_CONTRACT_CHANGE_REQUIRED=YES`: presentation state currently lacks profile enrichment and revision/provenance ownership; PlayerSnapshot/PlayerService need an explicitly reviewed extension. Existing BasicProfileDto/OnboardingStateDto already model v2 fields, so do not duplicate those wire DTOs. createdAt would additionally require PlayerResponseDto/mapper/PlayerSnapshot changes only after the backend response extension is authorized. Profile's non-nullable JoinedAt presentation type must support unavailable data rather than DateTime.MinValue. Coach/history availability and section refresh/disposal need explicit presentation contracts.

`UI_BINDING_CHANGE_REQUIRED=YES`: ProductionAuthHost and ProductionAppShell composition; HomeData/MenuData/ProfileData adapters; ProductionHomePage, ProductionMenuPage, ProductionProfilePage; and ProductionLearnPage's shared Coach input. Future event-driven refresh must preserve the approved layout and navigation. Do not implement a Player copy inside each view.

| Phase | Minimal authorized-next proposal | Validation to design, not run here |
|---|---|---|
| PLAYER-UI-01A | Existing PlayerService contract enrichment, single read-only presentation adapter, same-session confirmed onboarding ingestion and versioned Coach lookup; no new routing policy | Bootstrap-before-profile-save staleness, completed restore without controller load, revision/provenance, v1/legacy missing data, unknown entitlements, no write calls |
| PLAYER-UI-01B | Inject Menu identity/membership/neutral avatar; keep two-line card and existing destinations | Exact authoritative name, no demo suffix/email/UID, FREE vs unavailable vs Premium/trial, selected catalog plan ignored |
| PLAYER-UI-01C | Home name and shared Home/Learn Coach; explicitly defer friends/progression rather than fake them | Name parity across tabs, pinned Coach version, missing Coach fallback, no independent fetches or selection |
| PLAYER-UI-01D | Own Profile identity/country/neutral avatar; separately approve createdAt contract or omission and History adapter scope | No fabricated date/flag/history, existing pagination/access ownership, neutral absent fields; no profile edit writes |
| PLAYER-UI-01E | Session-switch/loading/error and refresh regression; targeted visual review | A→logout→B, same-UID new epoch, late bootstrap/Coach/entitlements, disposal, EN/ES copy, no stale identity, geometry retained |

No protected file is required for these proposed changes: `PLAYER_UI_PROTECTED_FILES_REQUIRED=NO`. Locale strings may use the existing DominoLocalization / approved presentation mechanism; raw names must never be translated. No change to protected Localization Settings, Firebase settings, platform config or ProjectSettings is needed. The existing localization source/table paths are not among the 102 protected files; exact generated candidates must still be reviewed in an implementation checkpoint.

The pending 132 files are 102 protected historical files, 11 SMOKE02 observer files/hunks, 2 mixed Country validation helpers, 15 reports, and 2 unrelated routing-diagnostic files. No pending product Player binding implementation was found. Relevant pending runtime files (ApplicationServices, ProductionRoutingComposition, AuthenticatedRoutingOrchestrator) require future **hunk-level** review to exclude those existing diagnostics; do not stage whole files blindly. AppShellMock remains read-only and may keep its visual fixtures and shared icon/Coach assets.

## Audit outcome

```makefile
BASE_SHA=3fc9f0f5882f163a879cd9625ab0f1b3117388c8
PLAYER_UI_SURFACES=HOME,MENU,PROFILE,LEARN,PUZZLES,WATCH,HEADERS,PLACEHOLDER_SUBPAGES
PRODUCTION_VISIBLE_DEMO_REFERENCES=26_MATCHING_LINES
MOCK_ONLY_REFERENCES=18_MATCHING_LINES
TEST_FIXTURE_REFERENCES=52_MATCHING_LINES
LOCALIZATION_REFERENCES=5_MATCHING_LINES
UNRELATED_REFERENCES=9_MATCHING_LINES
UNCLASSIFIED_PLAYER_DEMO_REFERENCES=0
BOOTSTRAP_PLAYER_DTO=PlayerBootstrapResponseDto.player:PlayerResponseDto
PLAYER_CLIENT_STATE_OWNER=ApplicationServices.Player:PlayerService
APP_SHELL_PLAYER_BINDING_OWNER=NONE
PLAYER_PRESENTATION_SOURCE_COUNT=1_PROPOSED
PLAYER_UI_AVAILABLE_FIELDS=DISPLAY_NAME,ACCOUNT_TYPE,STATUS,LANGUAGE,WALLET,ENTITLEMENTS,TRIAL;V2_BASIC_PROFILE;VERSIONED_ONBOARDING_EXPERIENCE_AND_COACH
MENU_PRIMARY_NAME_SOURCE=CONFIRMED_PLAYER_DISPLAY_NAME
MENU_IDENTITY_PRESENTATION_CONTRACT=DISPLAY_NAME;EFFECTIVE_MEMBERSHIP_PLUS_PRODUCT_BRAND
MEMBERSHIP_DISPLAY_SOURCE=SERVER_DERIVED_ENTITLEMENT_STATE
LEGACY_PREMIUM_DISPLAY_POLICY=LOCALIZED_PREMIUM_NO_COMMERCIAL_TIER_REMAP
TRIAL_PLAYER_UI_POLICY=SERVER_TRIAL_QUALIFIER_AND_DATE_NO_ACTIVATION
COACH_UI_SOURCE=CONFIRMED_SAVED_KEY_PLUS_PINNED_COACH_CATALOG
EXPERIENCE_CURRENT_UI_SURFACES=NONE_IN_APP_SHELL
COUNTRY_CURRENT_UI_SURFACES=PROFILE_ONLY_OUTSIDE_ONBOARDING
PLAYER_AVATAR_IMPLEMENTED=NO
PLAYER_AVATAR_FALLBACK=EXISTING_NEUTRAL_MENU_AVATAR_PROPOSED_FOR_PROFILE
MENU_PLAYER_DATA_BINDING_MATRIX=DOCUMENTED
HOME_PLAYER_DATA_BINDING_MATRIX=DOCUMENTED
PROFILE_BINDING_MATRIX=DOCUMENTED
PLAYER_UI_CLEARED_ON_SESSION_CHANGE=YES_DESIGN_REQUIRED
STALE_PLAYER_PRESENTATION_APPLIED=NO_DESIGN_REQUIRED
PLAYER_UI_LOADING_POLICY=EXISTING_ROUTING_LOADING_NO_DEMO_IDENTITY
PLAYER_UI_FAILURE_POLICY=ROUTING_OWNS_FAILURE_NO_FAKE_PLAYER
PLAYER_UI_REQUIRES_ONBOARDING_WRITE=NO
BACKEND_CONTRACT_CHANGE_REQUIRED=YES_FOR_REAL_PROFILE_CREATED_AT
INITIAL_MENU_HOME_BINDING_REQUIRES_BACKEND_CHANGE=NO
SCHEMA_CHANGE_REQUIRED=NO_FOR_EXISTING_PLAYER_FIELDS
CLIENT_CONTRACT_CHANGE_REQUIRED=YES
UI_BINDING_CHANGE_REQUIRED=YES
PLAYER_NAME_FALLBACK_POLICY=NO_DEMO_NO_EMAIL_NO_UID;EXISTING_NEUTRAL_LOCALIZED_CONVENTION
MEMBERSHIP_FALLBACK_POLICY=UNAVAILABLE_NOT_FREE
EMAIL_USED_AS_PUBLIC_PLAYER_NAME=NO
UID_VISIBLE_IN_PLAYER_UI=NO
PLAYER_WRITES=0
ONBOARDING_WRITES=0
TRIAL_WRITES=0
LIVE_REQUESTS=0
APP_SHELL_MOCK_CHANGED=NO
APP_SHELL_MOCK_USED_AS_PLAYER_DATA_AUTHORITY=NO
PLAYER_UI_PROTECTED_FILES_REQUIRED=NO
PREEXISTING_PROTECTED_FILES_MODIFIED=0/102
PREEXISTING_PENDING_FILES_PRESERVED=132/132
IMPLEMENTATION_PHASES=01A_STATE,01B_MENU,01C_HOME_LEARN,01D_PROFILE,01E_SESSION_REGRESSION
SOURCE_CHANGED=NO
REPORT_CREATED=YES
COMMIT=NONE
PUSH=NONE
DEPLOY=NO
PLAYER_UI_01_AUDIT_SUCCESS=YES
NEXT=PLAYER-UI-01 IMPLEMENTATION AUTHORIZATION
```

No new behavioral tests were necessary or executed for this read-only report. Audit success means the sources, supported contracts, gaps and proposed ownership are documented; it does not mean real Player binding has been implemented or visually approved.

## Exact demo-reference index

The following table records every matched source line, including fixtures and non-demo hits. Multiple matching literals on one line count once. Paths are repository-relative; generated classification detail is retained locally in `client/Validation/Generated/PlayerUi01/demo-reference-classification.json`.

| Classification | Source file | Lines |
|---|---|---|
| LOCALIZATION | `client/DominoGame/Assets/_Domino/Editor/Localization/Translations.json` | 840, 845 |
| LOCALIZATION | `client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/ProductionContentRoots.cs` | 14 |
| LOCALIZATION | `client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/ProductionHomePage.cs` | 45 |
| LOCALIZATION | `client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/ProductionWelcomeView.cs` | 22 |
| MOCK_ONLY | `client/DominoGame/Assets/_Domino/AppShellMock/MockProfile.cs` | 36, 37, 53 |
| MOCK_ONLY | `client/DominoGame/Assets/_Domino/AppShellMock/MockShellState.cs` | 49, 71, 79, 137, 142 |
| MOCK_ONLY | `client/DominoGame/Assets/_Domino/AppShellMock/MockShellView.cs` | 29, 109, 131, 132, 163, 207, 325, 334, 344, 349 |
| PRODUCTION_VISIBLE_DEMO | `client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/HomeData.cs` | 33, 35, 36, 37 |
| PRODUCTION_VISIBLE_DEMO | `client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/MenuData.cs` | 18, 20, 21 |
| PRODUCTION_VISIBLE_DEMO | `client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/ProductionAppShell.cs` | 46, 55 |
| PRODUCTION_VISIBLE_DEMO | `client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/ProfileData.cs` | 44, 47, 50, 52, 53, 54, 55, 56, 57, 58, 59 |
| PRODUCTION_VISIBLE_DEMO | `client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/RootContentData.cs` | 18, 20, 24, 27, 35, 37 |
| TEST_FIXTURE | `client/DominoGame/Assets/_Domino/AppShellMock/Editor/MockProfileValidation.cs` | 20 |
| TEST_FIXTURE | `client/DominoGame/Assets/_Domino/AppShellMock/Editor/MockWelcomeValidation.cs` | 28 |
| TEST_FIXTURE | `client/DominoGame/Assets/_Domino/Scripts/Client/Editor/EntitlementVisualValidation.cs` | 66, 76, 88, 89, 90, 91, 96 |
| TEST_FIXTURE | `client/DominoGame/Assets/_Domino/Scripts/Client/Editor/S601CodecValidation.cs` | 12, 19, 21, 24, 28, 57 |
| TEST_FIXTURE | `client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/Editor/IsolatedOnboardingServer.cs` | 27 |
| TEST_FIXTURE | `client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/Editor/ProductionMembershipPreview.cs` | 18, 36, 61, 62, 63 |
| TEST_FIXTURE | `client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/Editor/ProductionMenuValidation.cs` | 32 |
| TEST_FIXTURE | `client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/Editor/ProductionProfileValidation.cs` | 19, 21, 27, 33, 104, 108, 120 |
| TEST_FIXTURE | `client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/Editor/ProductionRootContentValidation.cs` | 52 |
| TEST_FIXTURE | `client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/Editor/ProductionShellPreview.cs` | 141 |
| TEST_FIXTURE | `client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/Editor/ProfileLayoutDiagnostic.cs` | 15, 21 |
| TEST_FIXTURE | `client/Validation/AppShell01NavigationTests.cs` | 24, 71, 74 |
| TEST_FIXTURE | `client/Validation/DomainTests.cs` | 86, 158 |
| TEST_FIXTURE | `client/Validation/MembershipCatalogFixture.json` | 5, 95, 138, 140, 441, 574 |
| TEST_FIXTURE | `client/Validation/MembershipTests.cs` | 17, 35, 37, 41, 44, 45 |
| TEST_FIXTURE | `client/Validation/OnboardingCheckpointTests.cs` | 23 |
| TEST_FIXTURE | `client/Validation/OnboardingClientTests.cs` | 103 |
| UNRELATED | `client/DominoGame/Assets/_Domino/Scripts/Client/DominoClientController.cs` | 205, 230, 259 |
| UNRELATED | `client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/ProductionMembershipView.cs` | 31, 43, 74 |
| UNRELATED | `client/DominoGame/Assets/_Domino/Scripts/UI/BoardView.cs` | 130, 499 |
| UNRELATED | `client/DominoGame/Assets/_Domino/Scripts/UI/EntitlementProfilePresentation.cs` | 29 |

## PLAYER_UI_01A_IMPLEMENTATION

Authorized base: `3fc9f0f5882f163a879cd9625ab0f1b3117388c8`. Implemented and tested 2026-10-02. This phase adds the shared production presentation contract; it does not migrate any visible Home/Menu/Profile data source.

### Ownership and minimal contract

`ApplicationServices.Player:PlayerService` remains the only Player domain owner. `ProductionAuthHost.PlayerPresentation` holds one `PlayerPresentationSource` for its existing session/host lifetime. It derives immutable `PlayerPresentationState` values from that PlayerService and the existing authenticated routing state. It owns no Player DTO cache, persistence, API client, auth credentials, asynchronous fetch, or replacement session-generation mechanism.

The presentation model exposes only availability (`Empty`, `Loading`, `Ready`, `Unavailable`), confirmed display name, neutral name fallback localization key, account type, preferred locale, effective membership key, server trial state/consumed flag/end date. It exposes no UID, email, createdAt, Country, Experience, Coach, avatar or product brand.

The primary name is `PlayerService.Player.DisplayName`. Unavailable names use the existing neutral `profile.title` key (English **Player profile**, Spanish **Perfil de jugador**); no email, UID or fabricated demo name is synthesized. The model supplies a localization key, not a second translation store. Concrete label binding remains a consumer-phase responsibility.

`PlayerService` is split into partial files to keep the small added in-memory contract separate. Existing bootstrap/DTO wire contracts are unchanged. Added members are `PreferredLocale`, `IsCurrentSession`, `PresentationInvalidated`, and internal `ReceiveConfirmedProfile`. The latter consumes the existing router's validated onboarding response after its authoritative Home decision. It accepts only the current Player snapshot reference/session and valid monotonic profile/preferences revisions. Older revisions and conflicting equal revisions are rejected. The method changes only the in-memory confirmed display name/preferred locale; it does not save Player data or change Player language, wallet, account type or status.

This closes the audited name freshness gap: completing onboarding can save a name after the initial bootstrap, while `InitializeAsync` deliberately does not fetch again when already synchronized. The already available completed-onboarding GET now informs the same PlayerService before presentation is ready. Controller drafts are never read. v1/legacy responses without the optional Basic Profile projection keep the confirmed bootstrap name. No DTO fields or backend endpoints were added. An existing confirmed alias/bootstrap name update cannot be overwritten by the same previously observed profile revision.

### Membership and trial mapping

Only an `AVAILABLE` server entitlement envelope with a recognized plan/status supplies membership. Missing/unavailable/malformed or unsupported entitlement state remains `Unknown`; it never becomes Free because a DTO is absent. Membership keys are presentation labels/keys, not an access-control policy.

| Authoritative `snapshot.plan` / status | Presentation key | Consumer label intent |
| --- | --- | --- |
| `FREE` / `FREE` or `EXPIRED` | `Free` | Existing localized Free/Gratis label |
| `PREMIUM` or `PREMIUM_LEGACY` / `ACTIVE` | `PremiumLegacy` | Premium, existing `premium.active` copy; never Gold/Platinum/Diamond |
| `GOLD` / `ACTIVE` | `Gold` | Gold |
| `PLATINUM` / `ACTIVE` | `Platinum` | Platinum |
| `DIAMOND` / `ACTIVE` | `Diamond` | Diamond |
| `FAMILY` or `FRIENDS_AND_FAMILY` / `ACTIVE` | `Family` | Family product representation |
| Missing, unavailable or unrecognized plan/status | `Unknown` | Unavailable, no Free claim |

Current backend runtime emits `FREE`/`PREMIUM`; commercial cases are synthetic contract tests for representability, not evidence of deployed commercial entitlement issuance. Catalog selection, onboarding Membership selection, prices, entitlement enforcement and trial activation are not consulted or changed. **Cuban Domino Club** remains product branding outside this state.

Trial activity/consumption/end date come exclusively from the same server snapshot. Explicit server `EXPIRED` maps to expired; consumed but inactive trial with an active paid grant stays inactive, not inferred expired. A missing/invalid active-trial date remains unknown. No local clock expires or extends a trial, and no state construction grants a trial. The trial date is parsed into an immutable `DateTimeOffset`; no mutable response object escapes into presentation.

### Session and failure lifecycle

- Router resolving/onboarding states produce neutral Loading; Welcome/unverified/no session produce neutral Empty. Home requires a current synchronized Player. Routing errors/update-required and failed Player refresh produce neutral Unavailable, even when PlayerService retains a domain snapshot for retry. No route decisions or retry actions are added.
- `IsCurrentSession` checks the existing PlayerService identity/session and lifetime. The existing authenticated router's epoch continues to reject late async responses. The presentation layer issues no async requests of its own.
- Player disposal clears presentation-related authority and notifies listeners. Host rebind disposes the old projection before creating the next one; host destruction removes subscriptions. Reads after invalidation/disposal cannot return the previous name or membership. Already handed-out immutable values are historical snapshots; consumers must use the current source and its change event, rather than cache an old value as authority.
- Source creation after session restore derives from the already confirmed router/Player data without an extra bootstrap/GET. Late responses from A cannot populate B, including a new service lifetime using the same synthetic identity.
- The source observes existing entitlement changes and never creates an entitlement refresh timer or HTTP call. Entitlement freshness scheduling and actual view rendering remain later consumer work; the source cannot promise a newer server snapshot than its existing owner has received.
- Subscriber exceptions cannot interrupt routing, Player teardown, or the other presentation subscribers. No dependency on temporary SMOKE observers/helpers was added.

### Validation performed

| Suite | Current result | Scope |
| --- | --- | --- |
| `RunPlayerPresentationTests.ps1` | **90 PASS**: 50 mapping, 40 session | Confirmed name, neutral fallback, each membership key, missing data, explicit/inactive/expired trial, exact server date, immutable DTO detachment, minimal field surface, explicit no Alex/Demo player synthesis, restore, failure, invalidation, A-to-B and same-identity late responses, profile revision protection, disposal/unsubscribe, no extra reads/writes |
| `RunPlayerFoundationClientTests.ps1` | **490 PASS** | Existing Player bootstrap, identity correlation, retries, alias, wallet/connection/realtime regressions; isolated transports |
| `RunProductionRoutingCompositionTests.ps1` | **89 PASS** | Existing auth/onboarding/restore/completion/error routing composition; isolated transports |

All 669 checks passed. No live API/Firebase call, trial activation, account switch or logout was executed by validation. The initial new fixture test incorrectly changed the fake SDK session without invalidating FirebaseAuthService's intentionally cached identity; it was corrected to exercise the existing sign-out/restore lifecycle in the isolated fixture, without changing production Auth behavior.

Unity was confirmed outside Play by the user. After final Assets Refresh, the current Editor log records successful compilation/domain reload/import, and `Assembly-CSharp.dll` is newer than all changed product sources and contains the new presentation types. **Compiler errors: 0. New 01A warnings: 0. Existing distinct compiler warnings: 9** (unused test events in the preexisting Online/Client Editor validators). **Exceptions in the current import interval: 0; current blocking exceptions observed: 0.** This describes the current import, not a claim that historical Editor logs contain no exceptions. No new visual review or real-session runtime test is claimed in this state-only phase.

### Exact changed files

| File | Change |
| --- | --- |
| `client/DominoGame/Assets/_Domino/Scripts/Player/PlayerService.cs` | Partial class, preferred-locale seed, confirmed-name revision invalidation, cancellation/teardown presentation guard |
| `client/DominoGame/Assets/_Domino/Scripts/Player/PlayerPresentationAuthority.cs` | Minimal client-side confirmed profile/lifecycle extension |
| `client/DominoGame/Assets/_Domino/Scripts/Player/PlayerPresentationAuthority.cs.meta` | New Unity script metadata |
| `client/DominoGame/Assets/_Domino/Scripts/Player/PlayerPresentationState.cs` | Immutable projection and entitlement/trial mapping |
| `client/DominoGame/Assets/_Domino/Scripts/Player/PlayerPresentationState.cs.meta` | New Unity script metadata |
| `client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/PlayerPresentationSource.cs` | Shared read-only projection/subscriptions and confirmed-profile bridge |
| `client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/PlayerPresentationSource.cs.meta` | New Unity script metadata |
| `client/DominoGame/Assets/_Domino/Scripts/UI/AppShell/ProductionAuthHost.cs` | Own/dispose the single shared source; no visible view binding changes |
| `client/Validation/PlayerPresentationTests.cs` | Isolated focused tests |
| `client/Validation/RunPlayerPresentationTests.ps1` | Isolated runner using existing cached dependencies |
| `client/Validation/PLAYER_UI_01_REAL_PLAYER_BINDING_AUDIT_REPORT.md` | This implementation addendum and audit snapshot clarification |

Client contract changes are confined to the PlayerService partial extension and new immutable presentation/source contracts. Bootstrap/Onboarding DTOs, backend, configuration, App Shell Mock, Menu/Home/Profile view sources and the existing pending composition/SMOKE/routing-observability files are unchanged. All **132 preexisting pending files** remain byte-identical; all **102 protected files** remain unchanged. The audit report was the additional 133rd pending file and is explicitly updated by this authorization. No files were staged.

Scoped secret/identity scan: PASS; real emails, credential values, JWTs/tokens, raw Firebase UIDs, private keys, action URLs and mojibake markers: 0. Ignored local evidence is stored under `Validation/Generated/PlayerUi01A`; it is not a credential/session store.

### Remaining consumer work / stop boundary

01B must inject/observe this single source in Menu and resolve membership/name localization, including unavailable/loading behavior. 01C owns Home and separately audited Coach/catalog binding. 01D owns Profile, including the createdAt contract decision, Country and real History service integration. Demo providers are deliberately retained until their consumer migrations. No consumer may start another Player fetch/store or treat a cached projection as authority after invalidation.

```text
PLAYER_UI_01A_SUCCESS=YES
PLAYER_DOMAIN_STATE_OWNER=ApplicationServices.Player:PlayerService
PLAYER_DOMAIN_STATE_OWNER_COUNT=1
PLAYER_PRESENTATION_MODEL=Domino.Player.PlayerPresentationState
PLAYER_NAME_FALLBACK=NEUTRAL_NON_IDENTITY_COPY(profile.title)
PRIMARY_NAME_SOURCE=PLAYER_DISPLAY_NAME
MEMBERSHIP_SOURCE=SERVER_DERIVED_ENTITLEMENT_STATE
MISSING_ENTITLEMENT_MEANS_FREE=NO
LEGACY_PREMIUM_COMMERCIAL_REMAP=NO
COMMERCIAL_TIER_ENFORCEMENT_CHANGED=NO
TRIAL_STATE_SOURCE=SERVER
TRIAL_ACTIVATION_SIDE_EFFECTS=0
PLAYER_PRESENTATION_CLEARED_ON_SESSION_CHANGE=YES
STALE_PLAYER_A_PRESENTATION_APPLIED_TO_B=NO
STALE_PRESENTATION_RESPONSE_APPLIED=NO
PLAYER_PRESENTATION_AFTER_LOGOUT=EMPTY_OR_LOADING
SESSION_RESTORE_PRESENTATION_SUPPORTED=YES
PLAYER_PRESENTATION_TESTS=90_PASS
SESSION_PRESENTATION_TESTS=40_PASS_INCLUDED
PLAYER_FOUNDATION_REGRESSION=490_PASS
ROUTING_COMPOSITION_REGRESSION=89_PASS
DEMO_IDENTITY_GENERATED_BY_PRESENTATION_STATE=NO
MENU_VISUAL_BINDING_CHANGED=NO
HOME_VISUAL_BINDING_CHANGED=NO
PROFILE_VISUAL_BINDING_CHANGED=NO
EXPERIENCE_ADDED_TO_SHARED_PRESENTATION=NO
COUNTRY_ADDED_TO_SHARED_PRESENTATION=NO
CREATED_AT_IMPLEMENTED=NO
PLAYER_AVATAR_IMPLEMENTED=NO
APP_SHELL_MOCK_CHANGED=NO
BACKEND_SOURCE_CHANGED=NO
PLAYER_WRITES=0
ONBOARDING_WRITES=0
TRIAL_WRITES=0
PREEXISTING_PROTECTED_FILES_MODIFIED=0/102
PREEXISTING_PENDING_FILES_PRESERVED=132/132
SMOKE_OBSERVER_DEPENDENCY=NO
UNITY_COMPILER_ERRORS=0
UNITY_WARNINGS_CURRENT=9
NEW_PLAYER_UI_01A_WARNINGS=0
CURRENT_BLOCKING_EXCEPTIONS=0
SECRET_SCAN=PASS
COMMIT=NONE
PUSH=NONE
DEPLOY=NO
PLAYER_UI_01B_STARTED=NO
NEXT=PLAYER-UI-01A IMPLEMENTATION REVIEW
```

## PLAYER_UI_01B_MENU_BINDING

Base: `9d3ce0225f7d30ad67d74a030821a5bd486d1e02`. Implemented and validated on 2026-10-02. This section supersedes the earlier Menu/demo observations only; Home, Profile and the other consumers retain their prior behavior.

### Consumer ownership and binding

The production view is `ProductionMenuPage`. `ProductionAppShell` owns the retained Menu page, navigation and existing logout callback; there is no separate Menu identity controller. Before this change it constructed `DemoMenuDataSource` directly. `ProductionAuthHost` now injects `PlayerMenuDataSource` over its existing single `PlayerPresentationSource`:

`ApplicationServices.Player:PlayerService -> PlayerPresentationSource.Current:PlayerPresentationState -> PlayerMenuDataSource -> ProductionMenuPage`

The adapter is stateless and never reads PlayerService, bootstraps, opens an API client or owns another Player cache. Menu subscribes while attached and unsubscribes when detached. It updates the existing labels and neutral avatar without rebuilding rows or changing styles. Reattachment rereads the current shared source. Session invalidation/disposal removes the previous identity; late responses from the prior service lifetime cannot supply a new session's Menu.

The production shell's unconfigured/default Menu is neutral. The old demo provider remains available, and `ProductionShellPreview` explicitly injects it to preserve its existing fixture contract. App Shell Mock is unchanged.

### Identity and membership presentation

The name is the literal authoritative displayName, including legitimate server-generated aliases. It is never replaced with an email, UID or a synthetic personal name. Long valid names retain their entire value and use the existing wrap policy.

| Presentation state | Menu membership |
| --- | --- |
| Confirmed Free | FREE in English; GRATIS in Spanish via existing `premium.free` copy |
| Confirmed PremiumLegacy | PREMIUM via existing `premium.active` copy |
| Confirmed Gold / Platinum / Diamond / Family | GOLD / PLATINUM / DIAMOND / FAMILY product labels |
| Missing or unresolved entitlements | No membership claim; brand remains visible |
| Empty / Loading / Unavailable | Neutral `profile.title` copy; no demo identity or fake FREE |

`MenuPlayerText.ProductBrand` retains the existing `Cuban Domino Club` product copy outside entitlement state. The avatar remains `AppShellMockIcons/icon_menu_avatar`; no Coach portrait or Player-avatar implementation was added. Routing remains the failure authority. Locale updates use the existing localization tables when initialized, with equivalent neutral English/Spanish presentation fallbacks before initialization. Product labels without an existing localized entitlement key retain their stable commercial names.

### Validation and evidence boundaries

- `RunPlayerMenuBindingTests.ps1`: **60 passed**, no failures, isolated transport only. Covers all six memberships (including both legacy Premium wire aliases), literal names, localized neutral copy, missing/loading/failure state, logout, session replacement, restore, late prior-session response and zero additional requests from reading Menu presentation.
- `PlayerMenuBindingValidation`: **1,221 Unity checks passed** at all eight established presets in English and Spanish. Includes a valid 16-character wide name, wrap/clipping/overlap, unchanged margins and neutral avatar dimensions, existing row order/height/touch targets, all nine Menu destinations and Back, fake logout callback, reattachment, actual `ProductionAuthHost` injection, missing entitlements and no demo fallback. Result: `Library/PlayerUi01B/Unity.validation.txt`, current passing run started at `2026-10-02T20:41:21.9753993Z`.
- An earlier attempt overlapped ongoing script import and did not finish its viewport check. It is not counted as a pass. The complete run above started after import and passed every preset; no product geometry was changed to satisfy the test.
- A subsequent live-inspection helper initially lacked an Editor-only namespace import; this was corrected before live execution. Product sources did not change after the passing Menu suite.
- During final Console sampling, the Editor encountered one transient `IOException` because a request producer still held its local request file. This was a validation-tool issue, not a Menu/session error. Play was stopped without logout, request consumption was corrected to defer a locked file to the next Editor tick, and **three focused request-file checks passed**: locked request retained, released request consumed, missing request ignored. No second TEST startup was performed. The Menu suite was retained because product code was unchanged.

Latest Unity import is complete. Compiler errors: **0**. Current Console warnings: **10**, comprising the **9 existing compiler warnings** plus **1 preexisting Firebase SDK Future-release warning on teardown** (the same warning pattern also exists earlier in the Editor log). New warnings originating in 01B source: **0**. Current Console errors and blocking exceptions after the final import: **0**. The earlier validation-tool exception is explicitly recorded above, not treated as if it never occurred. Final tooling evidence: `Library/PlayerUi01B/Preflight.txt` and `Tool.validation.txt`.

### Authorized real TEST preview

The user explicitly authorized the normal startup of the existing TEST session after being informed that bootstrap can update `lastSeenAt`, overriding the original zero-Player-write condition for that normal startup only. Exactly **one** real startup/bootstrap was observed and succeeded; no retry, login, logout, onboarding action or trial activation was performed. No live credential material or Player identifier was collected into the report.

At `2026-10-02T20:47:12.4519835Z`, the existing production host rendered Menu in the real **393x852 Game View**. Its visible name matched `PlayerPresentationState.DisplayName`, its visible membership matched the server-derived membership projection, and its avatar was the neutral existing asset. The full card was also visually inspected after closing the separate isolated preview. The observer only selected the retained Menu and read its state; it did not fetch or bootstrap. The product source and observed startup log establish one successful bootstrap and no routing error. No independent backend write audit was performed, so **total real Player writes must not be reported as proven zero**: normal startup may have updated `lastSeenAt` under the existing 15-minute policy. Menu-originated Player writes are zero.

Safe evidence: `Library/PlayerUi01B/Live.validation.txt`. Real displayName is deliberately omitted. The live pass is retained. Final state is Play OFF with the clearly labeled isolated Menu at 393x852 open; the persisted real authentication session was not cleared. No additional TEST startup was used to re-open the live card after the tooling correction.

### Scope and protection

Changed product files: `MenuData.cs`, `MenuPlayerText.cs` (+ meta), `PlayerMenuDataSource.cs` (+ meta), `ProductionMenuPage.cs`, `ProductionAppShell.cs`, `ProductionAuthHost.cs`. Changed validation/preview files: `Editor/ProductionShellPreview.cs`, new `Editor/PlayerMenuBindingValidation.cs` (+ meta), `Validation/PlayerMenuBindingTests.cs`, `Validation/RunPlayerMenuBindingTests.ps1`, and this report. **14 files total.** No changes to shared Player authority/model, backend, configuration, Home/Profile/other consumer bindings, App Shell Mock, or temporary SMOKE code. The Editor inspector rejects a runtime with test/SMOKE factories installed; production binding has no SMOKE dependency.

All **132 preexisting pending files** retain their baseline contents and Git status; all **102 protected files** are unchanged. The scoped secret/identity scan finds no real emails, raw Firebase UIDs, passwords, JWTs, private keys, action URLs or mojibake. Nothing is staged; no commit, push or deploy was performed. PLAYER-UI-01C is not started.

```text
BASE_SHA=9d3ce0225f7d30ad67d74a030821a5bd486d1e02
MENU_VIEW=ProductionMenuPage
MENU_CONTROLLER=ProductionAppShell_RETAINED_PAGE
CURRENT_MENU_DATA_PROVIDER=PlayerMenuDataSource
PREVIOUS_MENU_DATA_PROVIDER=DemoMenuDataSource
MENU_PLAYER_SOURCE=PlayerPresentationState
MENU_PLAYER_SOURCE_COUNT=1
MENU_PRIMARY_NAME_SOURCE=PLAYER_DISPLAY_NAME
MENU_DEMO_NAME_VISIBLE=NO
MENU_DEMO_PLAYER_COPY_VISIBLE=NO
MENU_MEMBERSHIP_SOURCE=SERVER_DERIVED_ENTITLEMENT_STATE
MISSING_ENTITLEMENT_RENDERS_FREE=NO
PREMIUM_LEGACY_DISPLAY=PREMIUM
PRODUCT_BRAND_SOURCE=EXISTING_PRODUCT_COPY
MENU_AVATAR_SOURCE=NEUTRAL_EXISTING_ICON
MENU_LOADING_DEMO_IDENTITY=NO
MENU_LOADING_FAKE_MEMBERSHIP=NO
MENU_FAILURE_FAKE_PLAYER=NO
MENU_CLEARED_ON_SESSION_CHANGE=YES
STALE_MENU_PLAYER_A_VISIBLE_FOR_B=NO
MENU_SESSION_RESTORE_SUPPORTED=YES
MENU_OPEN_PLAYER_NETWORK_REQUESTS=0
MENU_LAYOUT_CHANGED=NO
LONG_DISPLAY_NAME_OVERFLOW=0
LONG_DISPLAY_NAME_OVERLAP=0
MENU_EN=PASS
MENU_ES=PASS
MENU_ACCESSIBILITY_REGRESSION=PASS
HOME_VISUAL_BINDING_CHANGED=NO
PROFILE_VISUAL_BINDING_CHANGED=NO
SHARED_PLAYER_BINDINGS_CHANGED=NO
APP_SHELL_MOCK_CHANGED=NO
MENU_BINDING_TESTS=60_PASS
MENU_UNITY_CHECKS=1221_PASS
VALIDATOR_REQUEST_LIFECYCLE_CHECKS=3_PASS
MENU_DEMO_REGRESSION=PASS
MENU_RESPONSIVE=8/8_PASS_EN_ES
REAL_PLAYER_MENU_PREVIEW=PASS
REAL_DISPLAY_NAME_VISIBLE=YES
REAL_MEMBERSHIP_VISIBLE=YES
UNITY_COMPILER_ERRORS=0
UNITY_WARNINGS_CURRENT=10
NEW_PLAYER_UI_01B_WARNINGS=0
CURRENT_BLOCKING_EXCEPTIONS=0
PLAYER_WRITES=NOT_ASSERTED_ZERO_AUTHORIZED_NORMAL_BOOTSTRAP
MENU_PLAYER_WRITES=0
NORMAL_BOOTSTRAP_COUNT=1
NORMAL_BOOTSTRAP_LAST_SEEN_WRITE=POSSIBLE_NOT_SEPARATELY_AUDITED
ONBOARDING_WRITES=0
TRIAL_WRITES=0
PREEXISTING_PROTECTED_FILES_MODIFIED=0/102
PREEXISTING_PENDING_FILES_PRESERVED=132/132
SMOKE_OBSERVER_DEPENDENCY=NO
BACKEND_SOURCE_CHANGED=NO
BACKEND_REDEPLOYED=NO
SECRET_SCAN=PASS
COMMIT=NONE
PUSH=NONE
DEPLOY=NO
PLAYER_UI_01B_SUCCESS=YES
PLAYER_UI_01C_STARTED=NO
NEXT=PLAYER-UI-01B VISUAL REVIEW
```

## PLAYER_UI_01C_HOME_BINDING

The contract-review subsection below is historical. Its pending decision and unimplemented status are superseded by **Accepted version resolution and completed implementation** at the end of this section. The 01B evidence above remains retained.

### Contract review before implementation — 2026-10-02

Base: `9d3ce0225f7d30ad67d74a030821a5bd486d1e02`. The 14 files recorded by the final 01B evidence matched their SHA-256 hashes before this report-only append. Menu's approved implementation and its retained 60 mapping tests, 1,221 Unity checks and real TEST validation are unchanged. No tests or real startup were initiated during this contract review.

`ProductionAppShell.cs:55` still constructs Home with `DemoHomeDataSource`; `HomeData.cs:35-36` supplies Alex, Amara and the fixed Coach greeting/portrait. Therefore `PREVIOUS_HOME_AMARA_SOURCE=DEMO_HARDCODED`. This proves presentation provenance, not the current authenticated Player's saved Coach. The prior audit's SOFIA/catalog-1 observation is historical and is not promoted to a current-session measurement.

### Selection/version contract distinction

| Existing contract | Saved Coach information available |
| --- | --- |
| `POST /api/v1/player/bootstrap` | Neither saved Coach key nor saved Coach catalog version (`PlayerBootstrapResponse.kt:13`) |
| `GET /api/v1/player/onboarding` | `COACH_SELECTION` answer comes from `DominoProfile.preferredCoachKey`; domain revision is included. `selectedCoachCatalogVersion` is not serialized (`OnboardingProgressService.kt:32-50`; `OnboardingProgressModels.kt:13-19`) |
| `GET /api/v1/onboarding/catalog?version=<onboarding version>` | Immutable onboarding publication's `coachCatalogVersion`; this is a different field from the Player's persisted selected version |
| `GET /api/v1/player/profile` | Root identity/profile fields; no DominoProfile selection/version (`EntitlementConfiguration.kt:40-48`) |
| Coach-step mutation response | `OnboardingMutationResponse.domino` can include the full saved key/version when the domain changes; it is not a read/restore mechanism (`OnboardingProgressService.kt:73`) |
| `GET /api/v1/coaches?version=<coach version>&locale=<locale>` | Exact historical Coach catalog with localized name/descriptions and avatar reference; includes inactive entries with `selectable=false` (`CoachCatalogHttp.kt:14-20`; `CoachCatalog.kt:40-49`) |

The existing client mirrors this distinction: `OnboardingStateDto` has answers but no DominoProfile, while `OnboardingMutationDto.domino` contains `DominoProfileDto.selectedCoachCatalogVersion` (`OnboardingDtos.cs:24-30`). `PlayerPresentationState` currently has no Coach selection. The existing `CoachCatalogApiClient` already supports a requested version and rejects a mismatching returned version; no Home-specific API client is needed.

The earlier audit proposed an **indirect, completed-FLOW-only** resolution: confirmed saved Coach answer + pinned onboarding publication + that publication's Coach catalog version. Backend save and complete enforce the relationship between the saved selection version and the pinned publication (`OnboardingProgressService.kt:156-160,207-208`). However, restored GET does not independently return or recheck equality of the persisted selected version; it returns the answer and onboarding version. This strategy must be described as contract-derived provenance, never as a direct read of `selectedCoachCatalogVersion`. Unpinned/legacy-exempt or otherwise unsupported cases must remain unavailable.

The current authorization specifically names the Player's saved key and selected version as authority. A clarification is pending on whether the previously audited indirect strategy is acceptable for completed FLOW, or direct serialization is required. Product implementation is paused at this decision; no backend change was made or implicitly authorized.

### Minimal direct-contract alternative — design only

If direct provenance is required, add a nullable saved Coach projection (`preferredCoachKey`, `selectedCoachCatalogVersion`, `revision`) to the existing onboarding response, taken from the **already-read** `Snapshot.domino`. Do not change storage/schema, routing policy, selections, trial state or GET side effects. Add the corresponding client DTO fields and ingest the confirmed pair into the existing Player/presentation authority with session and monotonic-revision guards. Older responses lacking the projection remain Coach-unavailable.

Resolve that exact version through the existing catalog infrastructure; match the exact key, allow historical inactive entries for presentation, and never substitute the first/current/default Coach. Resolve supported bundled avatar key/version references; unsupported asset versions receive a neutral portrait, not another Coach. All async completion must be rejected after session/owner/selection/version/locale invalidation. Neither Home nor Menu should fetch a separate Player, bootstrap again or maintain another Player store. Learn, Profile and Mock stay unchanged.

Required checks after a strategy is authorized: all ten keys; requested-version mismatch; missing/invalid key and version; inactive historical selection; unavailable catalog/avatar; EN/ES and all eight viewports; long/missing name; loading/failure; session A-to-B and late A completion; retained Menu regression; one current TEST inspection that reports only the Coach key/version and comparison booleans. No mutation request may be used to obtain the saved selection.

### Current gate

Unity was already in Play when inspected; Game View shows 393x852 and the existing demo-backed Home. No Play transition, retry, logout, account switch, import or real API operation was initiated. That screen is not evidence of real Home binding. The current screenshot's Console counters show 0 errors and 3 warnings; this is not a new compiler/test run and does not replace the previous 01B Console classification.

Fresh baseline comparison confirmed all 132 preexisting files preserve their bytes and Git status, including all 102 protected files. Nothing is staged. Only this report is being updated by 01C so far.

```text
PLAYER_UI_01C_STATUS=PENDING_COACH_VERSION_PROVENANCE_DECISION
MENU_REAL_BINDING_PRESERVED=YES
MENU_REAL_DISPLAY_NAME=PASS_RETAINED_01B
MENU_REAL_MEMBERSHIP=PASS_RETAINED_01B
HOME_CURRENT_SOURCE=DemoHomeDataSource
PREVIOUS_HOME_AMARA_SOURCE=DEMO_HARDCODED
HOME_TARGET_SOURCE=PlayerPresentationState
HOME_COACH_CATALOG_SOURCE=EXISTING_CoachCatalogApiClient_PROPOSED
HOME_SPECIFIC_COACH_API_CLIENT_CREATED=NO
SELECTED_COACH_VERSION_DIRECT_READ_CONTRACT_AVAILABLE=NO
COMPLETED_FLOW_INDIRECT_VERSION_STRATEGY=PREVIOUSLY_AUDITED_PENDING_CLARIFICATION
REAL_PLAYER_COACH_KEY=NOT_MEASURED_CURRENT_SESSION
REAL_PLAYER_COACH_RESOLUTION=NOT_RUN
REAL_HOME_COACH_MATCHES_PLAYER_SELECTION=NOT_VALIDATED
HOME_NAME_BINDING_TESTS=NOT_RUN
HOME_COACH_BINDING_TESTS=NOT_RUN
HOME_RESPONSIVE=NOT_RUN
HOME_REAL_DISPLAY_NAME_VISIBLE=NO_DEMO_REMAINS
HOME_REAL_COACH_MATCHES_SAVED_SELECTION=NOT_VALIDATED
HOME_FRIENDS_DEMO_CONTENT=DEFERRED
MENU_LAYOUT_CHANGED=NO
LEARN_PAGE_BEHAVIOR_CHANGED=NO
PROFILE_VISUAL_BINDING_CHANGED=NO
PRODUCT_SOURCE_FILES_CHANGED_THIS_REVIEW=0
BACKEND_SOURCE_CHANGED=NO
APP_SHELL_MOCK_CHANGED=NO
REAL_OPERATIONS_INITIATED_THIS_REVIEW=0
PREEXISTING_PROTECTED_FILES_MODIFIED=0/102
PREEXISTING_PENDING_FILES_PRESERVED=132/132
SECRET_SCAN=PASS_REPORT_SCOPE
STAGED_FILES=0
COMMIT=NONE
PUSH=NONE
DEPLOY=NO
PLAYER_UI_01C_SUCCESS=NO
NEXT=COACH_VERSION_PROVENANCE_DECISION
```

### Accepted version resolution and completed implementation — 2026-10-02

The owner explicitly accepted the indirect authoritative contract. Home now uses the same `PlayerPresentationState` exposed by the host's single `PlayerPresentationSource` as Menu. The source adds an immutable, session-scoped Coach presentation; Home holds no separate Player store or Coach domain authority.

The saved `COACH_SELECTION` answer in the confirmed onboarding read is the server projection of `DominoProfile.preferredCoachKey`. Resolution reads that Player's pinned onboarding publication, follows its explicit `coachCatalogVersion`, and reads that exact Coach catalog. It never equates the two version numbers, uses the current Coach pointer, reopens onboarding, or performs a selection write. This is contract-derived provenance, not a newly exposed direct read of `selectedCoachCatalogVersion`.

`PLAYER_UI_01C_DIRECT_COACH_VERSION_API=DEFERRED_NOT_REQUIRED`. A direct saved-version projection can be reconsidered if more consumers make indirect resolution unnecessarily complex. There is no backend contract, schema, source or deployment change in this task.

The existing `OnboardingApiClient` gains an explicit versioned catalog read; the existing `OnboardingShellController` exposes read-only access through its existing catalog infrastructure and reuses matching loaded catalogs. It does not modify the controller's onboarding state. Both COMPLETED and IN_PROGRESS with a saved selection and pinned context are supported by the resolver; normal production routing is unchanged.

Missing or inconsistent context, missing key, mismatching catalog versions and failed reads render neutral/unavailable Coach presentation. An inactive historical Coach remains the selected Coach for display. Missing or unsupported bundled avatar versions receive a neutral icon, never another Coach. Async results are discarded after session, owner, route, selection, version or locale invalidation. Home greeting and catalog copy disable rich-text interpretation of names. Opening Home or Menu does not bootstrap or fetch a separate Player.

The prior production Home's Alex and Amara came from `DemoHomeDataSource`. Production now receives `PlayerHomeDataSource`; the explicit development preview keeps its existing demo source, and Learn's existing demo provider is untouched. The existing Home geometry, navigation, buttons and five fictional friends remain unchanged. Menu's dedicated source, presentation, page and tests match their 01B hashes; only the shared host/shell composition is extended to inject Home from the same source.

### Current validation evidence

| Validation | Current result | Scope |
| --- | --- | --- |
| Home pure binding/contract suite | 222 PASS, 0 FAIL | Name 39; Coach 82; version resolution 73; session/I/O 28 |
| Shared Player presentation regression | 104 PASS | Retains previous 90 checks and adds 14 assertions for the authorized immutable Coach projection |
| Menu mapping regression | 60 PASS | Dedicated 01B binding implementation unchanged |
| Unity Home validation | 598 PASS, 0 FAIL | Eight logical presets in EN/ES, geometry, reachability, no clipping/overlap, attached-state refresh, same-source Menu, actual host injection and all ten correct portraits |
| Real TEST Home | PASS | Existing restored session, one normal bootstrap, actual Game View 393x852, current name/Coach/avatar equality |

The version tests deliberately use onboarding version 2 with Coach version 7, require explicit query versions, and reject mismatching returned versions. Coverage includes all ten keys, long/missing names, invalid/duplicate/missing Coach data, inactive Coach, unsupported avatar, loading/failure, IN_PROGRESS, locale changes, session A-to-B and late completion from a replaced session. No network or real writes occur in these isolated tests.

The eight Unity presets are 375x667, 393x852, 412x915, 430x932, 480x1040, 600x960, 768x1024 and 834x1194, each in EN and ES. The production Home and Menu use the same real display name in the checks. Continue Learning retains its prior route and behavior.

Initial test-only corrections were necessary: enum casing and invalid empty snapshot construction, then a fixture response missing the required `requiredCapabilities` collection. The first Unity attempt used an older imported fixture assembly and failed before completion; it is not counted as passing evidence. After explicit Refresh imported the final fixture, the complete Unity run started at `2026-10-02T22:18:41Z` and passed all 598 checks. Product source did not change after that passing run.

### Current real TEST observation

At `2026-10-02T22:25:15Z`, the read-only runtime observer compared the actual host's presentation with its confirmed onboarding state and the rendered Home. It reported:

```text
ENVIRONMENT=TEST
SCREEN=393x852
REAL_PLAYER_COACH_KEY=SOFIA
REAL_PINNED_ONBOARDING_VERSION=2
REAL_RESOLVED_COACH_CATALOG_VERSION=1
REAL_HOME_COACH_KEY=SOFIA
NAME_MATCH=YES
COACH_KEY_MATCH=YES
COACH_COPY_AVATAR_MATCH=YES
BOTH_VISIBLE=YES
MENU_PRESERVED=YES
```

The real screenshot confirms Home with the authenticated Player's greeting, localized Sofía name/description and matching portrait. Firebase UID, Player ID, email and credentials are deliberately omitted from this report. The restored locale is Spanish. This is a current runtime observation, not the earlier historical SOFIA audit or an isolated fixture. The owner subsequently approved the real Home at 393x852: visible display name `Guest-QFTC85G`, Coach `Sofía`, matching avatar visible, and no Amara displayed.

One authorized normal bootstrap started and succeeded during this run. Home catalog reads use the existing authenticated API infrastructure. No retry, logout, account switch, profile edit, onboarding submission, Coach selection or trial activation was initiated. The normal bootstrap may update `lastSeenAt`; that write was authorized and was not separately audited in Firestore. No additional Player writes were introduced by Home.

Fresh Console sampling at `2026-10-02T22:25:49Z`: Play ON, compilation/import idle, 0 errors and 2 runtime warnings. These are the existing PanelSettings message about no Theme Style Sheet and Firebase's missing Database URL configuration warning; the messages predate 01C and their originating paths were not changed here. The compilation gate before Play had 9 existing compiler warnings and 0 errors. Entering Play clears the Console according to its existing setting, so these counts describe different scopes; they do not imply that the previous warnings disappeared from the session history. No new 01C warnings or blocking exceptions were observed. No settings were changed to suppress warnings.

### Scope and protection

There are 35 pending paths owned jointly by 01B/01C: the previously recorded 14 paths plus 21 additional paths. The 132 preexisting pending files retain their bytes and Git status, including all 102 protected files. No paths are staged. Backend, configuration, App Shell Mock, Learn and Profile source remain untouched. The explicit development shell preview alone retains a demo Home adapter; production does not depend on it.

The scoped final scan covers the complete contents of all 35 owned paths, including this report, tests and validators. It checks real email addresses, raw Firebase UIDs, JWT/token or password literals, private/signing key material, credential URLs and mojibake. Matched test placeholders, if any, must be identified as fixtures without printing sensitive material. Final scan result and file hashes are retained in ignored `Validation/Generated/PlayerUi01C/implementation-result.json`.

```text
PLAYER-UI-01C HOME REAL BINDING
BASE_SHA=9d3ce0225f7d30ad67d74a030821a5bd486d1e02
MENU_REAL_BINDING_PRESERVED=YES
HOME_PLAYER_SOURCE=PlayerPresentationState
HOME_GREETING_NAME_SOURCE=PLAYER_DISPLAY_NAME
COACH_KEY_SOURCE=PLAYER_PREFERRED_COACH_KEY
COACH_VERSION_SOURCE=AUTHORITATIVE_PINNED_ONBOARDING_CONTEXT
COACH_VERSION_DERIVATION=PINNED_ONBOARDING_VERSION_TO_EXPLICIT_COACH_CATALOG_REFERENCE
COACH_VERSION_RESOLUTION=PINNED_ONBOARDING_EXPLICIT_REFERENCE
CURRENT_COACH_CATALOG_POINTER_USED_AS_VERSION_AUTHORITY=NO
HOME_COACH_SELECTION_SOURCE=PLAYER_PREFERRED_COACH_KEY
HOME_COACH_VERSION_SOURCE=PINNED_ONBOARDING_EXPLICIT_COACH_REFERENCE
HOME_COACH_NAME_SOURCE=VERSIONED_COACH_CATALOG
HOME_COACH_AVATAR_SOURCE=VERSIONED_BUNDLED_RESOURCE_REFERENCE
REAL_PLAYER_COACH_KEY=SOFIA
REAL_PINNED_ONBOARDING_VERSION=2
REAL_RESOLVED_COACH_CATALOG_VERSION=1
REAL_HOME_COACH_KEY=SOFIA
REAL_HOME_COACH_MATCHES_PLAYER_SELECTION=YES
PREVIOUS_HOME_AMARA_SOURCE=DEMO_HARDCODED
HOME_AMARA_HARDCODE_DEPENDENCY=NO
MISSING_PINNED_COACH_VERSION_FALLBACK_TO_CURRENT=NO
MISSING_PINNED_COACH_VERSION_SUBSTITUTES_AMARA=NO
COMPLETED_PLAYER_COACH_RESOLUTION=SUPPORTED
IN_PROGRESS_SAVED_COACH_RESOLUTION=SUPPORTED
BACKEND_CONTRACT_CHANGE_REQUIRED_FOR_01C=NO
PLAYER_UI_01C_DIRECT_COACH_VERSION_API=DEFERRED_NOT_REQUIRED
HOME_SPECIFIC_COACH_STATE_OWNER_CREATED=NO
HOME_SPECIFIC_COACH_API_CLIENT_CREATED=NO
HOME_NAME_BINDING_TESTS=PASS
HOME_COACH_BINDING_TESTS=PASS
COACH_VERSION_RESOLUTION_TESTS=PASS
HOME_PURE_CHECKS=222_PASS
PLAYER_PRESENTATION_REGRESSION=104_PASS
MENU_MAPPING_REGRESSION=60_PASS
HOME_UNITY_CHECKS=598_PASS
HOME_RESPONSIVE=8/8_PASS_EN_ES
COACH_AVATARS=10/10_PASS
HOME_REAL_DISPLAY_NAME_VISIBLE=YES
HOME_REAL_COACH_VISIBLE=YES
HOME_REAL_COACH_MATCHES_SAVED_SELECTION=YES
HOME_LOADING_DEMO_NAME=NO
HOME_LOADING_DEMO_COACH=NO
HOME_FAILURE_FAKE_PLAYER=NO
HOME_FAILURE_FAKE_COACH=NO
STALE_HOME_PLAYER_A_NAME_VISIBLE_FOR_B=NO
STALE_HOME_PLAYER_A_COACH_VISIBLE_FOR_B=NO
HOME_FRIENDS_DEMO_CONTENT=DEFERRED
PROFILE_VISUAL_BINDING_CHANGED=NO
LEARN_PAGE_BEHAVIOR_CHANGED=NO
MENU_LAYOUT_CHANGED=NO
APP_SHELL_MOCK_CHANGED=NO
BACKEND_SOURCE_CHANGED=NO
BACKEND_REDEPLOYED=NO
UNITY_COMPILER_ERRORS=0
UNITY_WARNINGS_CURRENT=2
UNITY_WARNINGS_BEFORE_PLAY=9_EXISTING_COMPILER_WARNINGS
NEW_PLAYER_UI_01C_WARNINGS=0
CURRENT_BLOCKING_EXCEPTIONS=0
NORMAL_BOOTSTRAP_COUNT=1
NORMAL_BOOTSTRAP_LAST_SEEN_WRITE=POSSIBLE_NOT_SEPARATELY_AUDITED
PLAYER_WRITES=0_EXCEPT_AUTHORIZED_BOOTSTRAP_LAST_SEEN
ONBOARDING_WRITES=0
TRIAL_WRITES=0
COACH_SELECTION_WRITES=0
PREEXISTING_PROTECTED_FILES_MODIFIED=0/102
PREEXISTING_PENDING_FILES_PRESERVED=132/132
SECRET_SCAN=PASS
STAGED_FILES=0
MANUAL_HOME_VISUAL_REVIEW=PASS
FINAL_VIEW=REAL_TEST_HOME_393x852
COMMIT=NONE
PUSH=NONE
DEPLOY=NO
NEXT=AWAIT_EXPLICIT_NEXT_BLOCK_SELECTION
```

### Manual approval and final checkpoint review

The owner approved the final real Home presentation and requested closure of PLAYER-UI-01C. No product, layout, localization, friends-demo, Menu, Profile, Learn, backend or API changes were made during this checkpoint review. All approved implementation hashes matched the preceding evidence before this report update. The retained 222 Home checks, 104 shared-presentation checks, 60 Menu checks and 598 Unity checks remain applicable; the large suites are not being repeated.

The exact candidate inventory is 35 paths: 25 Home/shared paths and 10 dedicated 01B Menu paths. Because 01B was not committed and Home depends on its approved presentation helpers and shared composition, the owner explicitly authorized including those 10 previously validated Menu paths as dependencies. This does not authorize further Menu changes. The 132 historical pending paths, including the 102 protected paths, remain excluded and unchanged.

The checkpoint review identified and resolved a separate tooling dependency: both `PlayerHomeBindingValidation.cs` and `PlayerMenuBindingValidation.cs` directly accessed `ApplicationServices.ValidationSmokeTransportFactory`. That field exists only in the excluded historical SMOKE observer changes, not in HEAD. The owner explicitly authorized a minimal correction in these two validators. They now inspect the optional public static field without a compile-time dependency, still reject an active hook, and retain the existing Firebase test-factory guard. No historical SMOKE file is included. The product implementation and all other validation code remain byte-for-byte unchanged from the approved evidence.

A clean candidate assembled from HEAD plus exactly the 35 approved paths, excluding the 132 historical pending files, compiled successfully: 166 runtime sources and 240 combined Editor sources, zero errors. Twelve focused checks extracted the exact guard expressions from the two candidate validators and verified absent, null and active SMOKE hooks with inactive/active Firebase test factories. All passed. The large visual and functional suites were retained, not rerun.

Unity imported the corrected validators with Play OFF. The Editor assembly was rebuilt at `2026-10-02T23:23:39Z`; the completed import snapshot at `2026-10-02T23:23:48Z` reports zero errors and ten existing warnings (the nine compiler warnings plus the Firebase disposal warning). No additional live bootstrap, logout or account change was performed for the checkpoint. The safe final Editor state is outside Play; this does not invalidate the owner's approved Home screenshot.

The checkpoint uses the message `feat(player-ui): bind home to real player and selected coach`. Its exact SHA and remote verification are reported after commit/push rather than embedded in the commit's own content. The containing Git commit is the authoritative checkpoint identifier. Final index review and secret scanning are required before publication; no deployment or subsequent block is authorized here.

```text
PLAYER_UI_01C=COMPLETE
MANUAL_HOME_REAL_PLAYER_VISUAL=PASS
MANUAL_HOME_REAL_COACH_VISUAL=PASS
MANUAL_VISUAL_REVIEW=PASS
VISIBLE_PLAYER_NAME=Guest-QFTC85G
VISIBLE_COACH_NAME=Sofía
VISIBLE_COACH_AVATAR=YES
VISIBLE_AMARA=NO
HOME_FRIENDS_DEMO_CONTENT=DEFERRED_UNCHANGED
CHECKPOINT_CANDIDATE_FILES=35
VALIDATED_01B_DEPENDENCY_FILES_AUTHORIZED=10
CHECKPOINT_GATE=PASS
PRODUCT_SOURCE_CHANGED_DURING_CHECKPOINT=NO
VALIDATOR_ONLY_FILES_CORRECTED=2
CLEAN_CANDIDATE_RUNTIME_COMPILE=PASS
CLEAN_CANDIDATE_EDITOR_COMPILE=PASS
VALIDATOR_GUARD_CHECKS=12_PASS
LARGE_SUITES_REPEATED=NO
UNITY_COMPILER_ERRORS=0
UNITY_WARNINGS_CURRENT=10_EXISTING
NEW_CHECKPOINT_WARNINGS=0
UNITY_PLAY_MODE=OFF
PROTECTED_FILES_MODIFIED=0/102
PENDING_FILES_PRESERVED=132/132
COMMIT_MESSAGE=feat(player-ui): bind home to real player and selected coach
CHECKPOINT_IDENTIFIER=CONTAINING_GIT_COMMIT
COMMIT_AND_REMOTE_SHA=REPORTED_AFTER_PUBLICATION
DEPLOY=NO
NEXT=AWAIT_EXPLICIT_NEXT_BLOCK_SELECTION
```
