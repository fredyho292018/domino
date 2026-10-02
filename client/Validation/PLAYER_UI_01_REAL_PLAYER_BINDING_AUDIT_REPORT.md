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
