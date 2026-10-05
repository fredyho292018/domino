# MEMBERSHIP-01A — Shared Membership experience

Base: `ce5e88ad81e8bd5460ddbbab641f3adfc536b550`. Implementation and isolated validation only. No commit, push, deployment, real account operation or trial activation.

## Audit and authority

The previous production onboarding view was `ProductionMembershipView`, rendered inside `ProductionOnboardingRoot`. Its presenter is the Membership partial of `OnboardingShellController` (`MembershipController.cs`). `OnboardingShellApiSource` obtains `MembershipCatalogDto` through `MembershipCatalogApiClient`, using the onboarding catalog's pinned membership version. Eligibility and entitlements come from the shared `PlayerService` projections. The existing trial API client retains the frozen operation and refreshes current entitlements rather than treating a replayed activation receipt as current access.

Menu previously routed `MenuDestination.Membership` through `ProductionAppShell.OpenSubpage` to `ShellTestDetail/SubpageMessage` (“Coming Soon”), with no Membership controller. It now creates `AppMembershipPage` and `AppMembershipController` through the production auth host's session-bound factory. Back restores Menu; switching tabs disposes the page/source. It never reads or advances onboarding state.

Domain authorities remain backend `EntitlementService`, `EntitlementResolver` and `TrialActivationService`; the versioned `MembershipCatalogService` is commercial presentation authority. `PlayerService.Entitlements` and `.TrialEligibility` remain the client session authority. `IMembershipPresentation` is a UI contract, not another durable commercial store. `SharedMembershipExperience` is the single shared renderer. App controller retains only catalog/local selection/loading/explicit-operation state, not a duplicate entitlement snapshot.

Existing read contracts are sufficient: `GET /api/v1/membership/catalog?locale=...` selects the current publication for the app; onboarding still selects its pinned version. `GET /api/v1/player/entitlements` refreshes the existing Player projection on app-page load. This does not bootstrap, write Player or obtain a separate identity. Eligibility remains the bootstrap `TrialEligibilityDto`; confirmed active/consumed/current-access state prevents a stale positive eligibility flag from offering another trial. The activation endpoint rechecks eligibility authoritatively. There is no new eligibility endpoint or backend change.

## Shared / host-specific boundary

| Concern | Authority / owner |
| --- | --- |
| Plan names, order, inclusion, feature descriptions, Family metadata | Backend catalog; shared renderer |
| Four commercial tabs, selected underline, icons, compact feature rows | SharedMembershipExperience, extracted from the validated production view |
| Monthly/Yearly selection and unavailable store copy | Shared renderer + host presentation selection; no purchase |
| Trial copy, dates and CTA rendering | Shared renderer, explicit semantic activation callback |
| Trial eligibility/effective access | Backend → existing PlayerService; no client grants |
| Current effective membership summary | MembershipStatePresentation, projected from server DTO; app header |
| Loading/error/retry and runtime locale changes | AppMembershipPage/AppMembershipController; read-only reload |
| Onboarding optional text, Not now, skip, revision, completion | ProductionMembershipView wrapper + unchanged OnboardingShellController |
| Onboarding Back | Existing ProductionOnboardingRoot/controller |
| App Back / normal navigation | AppMembershipPage → ProductionAppShell.Back → Menu |
| Future purchases/manage subscription | Not implemented; no invented action |
| State selector and comparison host selector | Editor/SharedMembershipPreview only |

The shared renderer has no onboarding step, revision, skip, completion or navigation call. Onboarding's wrapper supplies the optional explanation and existing footer. Existing `ProductionMembershipView` type/name and inherited feature-color API are retained for its validation consumers. Menu's server-derived membership summary source is unchanged.

## Contracts and plan classification

`EffectiveEntitlementsDto`: plan, status, sources, validUntil, trialActive, trialEndsAt, trialConsumed, features, limits, policyVersion, revision, serverTime, nextTransitionAt. Server effective `Plan` currently supports **FREE and PREMIUM only**. A promotional trial is a source of effective PREMIUM, not DIAMOND. No `trialStartsAt` is exposed here; no start date is fabricated. Expiration is formatted only when the actual `trialEndsAt` is parseable.

`TrialEligibilityDto`: state, eligible, activationMode, nullable policyVersion and periodDays. Current server states include NOT_STARTED, ACTIVE, EXPIRED, CONVERTED, INELIGIBLE and UNKNOWN. Only eligible explicit positive policy/duration, compatible legacy-trial catalog and current free/unconsumed access can enable a new app activation. Family cannot enable it. Unknown/unavailable access never defaults to FREE.

| Content | Classification | Treatment |
| --- | --- | --- |
| FREE, DIAMOND, PLATINUM, GOLD, FRIENDS_AND_FAMILY catalog records | SERVER_AUTHORITATIVE catalog | Not evidence of a current paid entitlement |
| Feature inclusion, order and localized descriptions | SERVER_AUTHORITATIVE | Included rows only, existing approved contract; no hardcoded feature entitlement matrix |
| Billing products/periods | SERVER_AUTHORITATIVE catalog | Selector only; no store call |
| Price authority | APPLE_GOOGLE_STORE | No price, saving amount or purchasable offer invented |
| “Coming soon” for unavailable purchases | LOCALIZED_COPY / honest future availability | Retained inside real Membership content; full-page Menu placeholder removed |
| Theme geometry, accents and icon-key mapping | CLIENT_PRESENTATION | Existing ThemeStyles/ThemeProvider, approved production geometry |
| Icons under Resources/AppShellMockIcons | Existing shared graphic assets | Reused without editing App Shell Mock or copying demo prices |
| Trial product | SERVER_AUTHORITATIVE PREMIUM_LEGACY | Separated from selected commercial plan |
| Friends & Family | SERVER_AUTHORITATIVE future multi-player product | Separate product, no Family runtime, invitation or trial |
| Harness data | ISOLATED_FIXTURE | Existing MembershipCatalogFixture.json; never product fallback |

FREE remains in the catalog/model. The four-tab commercial pattern is preserved; the app shows the real current FREE state in the header rather than introducing a fifth tab. Legacy Premium is never remapped to Gold/Platinum/Diamond. Feature comparisons do not claim currently granted benefits.

## Supported presentation matrix

| State | Exact evidence | Trial CTA |
| --- | --- | --- |
| FREE_NOT_ELIGIBLE | available snapshot, plan FREE, no eligible unconsumed offer | Disabled |
| FREE_TRIAL_AVAILABLE | FREE, unconsumed, eligible; activation additionally checks explicit policy/duration/catalog/current session | Enabled only for explicit action |
| TRIAL_ACTIVE | trialActive=true | Disabled, active copy; actual end date if available |
| TRIAL_EXPIRED | FREE, trialConsumed=true, status EXPIRED | Disabled; no second trial |
| PREMIUM_LEGACY | PREMIUM without active promotional trial | Disabled; Premium summary, no commercial remap |
| COMMERCIAL_TIER_IF_SUPPORTED | Not supported by current effective entitlement Plan contract | Unknown future plan → UNSUPPORTED; no invented tier |
| UNAVAILABLE | missing/current-session invalid/unavailable snapshot | No demo/free fallback or activation |

The existing resolver uses EXPIRED for consumed free state; the UI does not independently infer grant revocation details or create dates. Current effective state comes from the latest read, not the commercial tab selection.

## Actions, localization and isolation

SelectPlan/SelectBillingPeriod are presentation actions. App opening, retrying a read, selecting a tab and locale rerender perform zero domain writes. The explicit StartTrial boundary uses the existing TrialActivationApiClient only after a user action; frozen operation identity is retained on uncertain responses, terminal errors block further activation, receipt replay is not treated as current entitlements. This boundary was exercised only with fake sources. No real trial was started. Real trial verification remains a separately authorized TRIAL-LIVE-01 activity.

App page listens to the existing `LocalizationSettings.SelectedLocaleChanged`, reloads localized catalog through its session source and preserves selected plan/period. Concurrent locale reads use a generation guard. Session-invalid responses cannot populate the page; detach disposes subscriptions and API session. Player identity is never rendered or logged by the new code.

The Editor-only harness offers all six supported display states, EN/ES, eight existing presets, and an equivalent onboarding comparison. It constructs no real API source; real backend/trial/entitlement/Player/wallet/onboarding writes are zero. Preview locale selection changes presentation only and restores the previous Editor locale on close; no preference is persisted. No production dependency on the selector exists.

## Validation

- Shared Membership focused tests: **39 PASS**. State matrix; consumed precedence; unknown state; locale selection and latest response; invalidated session; read failure/explicit retry; no writes on open; frozen explicit activation retry with fake source; actual AppMembershipApiSource GET paths against fake transport; single Player authority; no bootstrap.
- Unity shared visual/host validation: **12,243 PASS, 112 scenarios, 0 failures**. Six app states × eight sizes × EN/ES, plus onboarding comparison at each size/locale. Checks include shared renderer, identical localized feature rows, four tabs, Family and other plan selection, billing-only changes, no app skip, onboarding Not now, Back to Menu, actual locale event, session invalidation, horizontal bounds, text measurement, minimum 44px controls, keyboard focus and scroll reachability. This is mechanical validation, not manual visual approval or a claim of native screen-reader certification.
- Presets: 375×667, 393×852, 412×915, 430×932, 480×1040, 600×960, 768×1024, 834×1194; EN and ES.
- Onboarding Membership flow: **76 PASS**. Full flow **407 PASS**, bootstrap **31 PASS**, shell **93 PASS**.
- Player UI: Home **222 PASS**, Menu **60 PASS**, Profile **80 PASS**.
- Profile edit **42 PASS**, Alias availability **20 PASS**, Guest post-link **69 PASS**.
- Static runtime + Editor compilation PASS. Imported Unity validation Console: **0 errors, 10 warnings, 0 current blocking exceptions**. Existing warnings include the Firebase Future disposal warning; they were not cleared or concealed. No warning originates from the new Membership files.

After the full suite, only the validation harness window size/locale selection was refined to leave a larger ES review surface with matching bottom navigation. Product source was unchanged; full suites were not repeated solely for this preview adjustment. Manual approval remains pending.

Final import and window inspection: preview open in App Membership / FREE_TRIAL_AVAILABLE / 393x852 / es, with Spanish bottom navigation. Unity remained outside Play. Current Console shows 0 errors and 9 warnings after reimport; the earlier 10-warning observation (including the Firebase Future disposal warning) is retained above. No Console clear was performed. Final scoped diff check, secret scan and privacy scan PASS; the unrelated preexisting AdsSettings whitespace finding in the whole-worktree diff check was not edited.

## Scope and safety

Baseline pending files: **152**. All retained byte-for-byte. Protected files: **0/102 modified**. Backend, configuration and App Shell Mock unchanged. No source-controlled screenshots, temporary credentials, real email, UID, token or verification link is needed for this work.

Durable changes: shared Membership presentation contract, extracted shared renderer, onboarding view wrapper, app controller/source/page, App Shell Membership dispatch and auth-host factory, focused test and runner, this report. Editor-only preview/harness and metadata are validation scope, not production state owners.

Final requested preview: **App Membership · FREE_TRIAL_AVAILABLE · 393×852 · ES**, with onboarding comparison available in the host selector. All trial activation in tests is in memory. No real purchases, Firestore access, logout, account change or onboarding writes were executed.

```ini
SHARED_MEMBERSHIP_BOUNDARY_DOCUMENTED=YES
MEMBERSHIP_PLAN_SOURCE_CLASSIFIED=YES
ONBOARDING_AND_APP_MEMBERSHIP_SHARE_CORE_UI=YES
APP_MEMBERSHIP_VISUAL_PARITY_WITH_ONBOARDING=YES_SHARED_CORE
ONBOARDING_MEMBERSHIP_VISUAL_REGRESSION=PASS
ONBOARDING_MEMBERSHIP_FLOW_REGRESSION=PASS
ONBOARDING_NOT_NOW_PRESERVED=YES
APP_MEMBERSHIP_COMING_SOON_REMOVED=YES_FULL_PAGE_PLACEHOLDER
APP_MEMBERSHIP_BACK_ROUTE=MENU
APP_MEMBERSHIP_ONBOARDING_SKIP_VISIBLE=NO
APP_MEMBERSHIP_USES_REAL_PLAYER_STATE=YES_PRODUCTION_BINDING
APP_MEMBERSHIP_DEMO_STATE_VISIBLE=NO_PRODUCTION
MENU_MEMBERSHIP_SOURCE_UNCHANGED=SERVER_DERIVED_ENTITLEMENT_STATE
MOCK_OR_FUTURE_PLAN_PRESENTED_AS_PURCHASABLE=NO
MEMBERSHIP_RESPONDS_TO_RUNTIME_LOCALE_CHANGE=YES
MEMBERSHIP_RESPONSIVE=8/8_PASS_EN_ES
HORIZONTAL_OVERFLOW=0
TEXT_CLIPPING=0
BACKEND_CHANGE_REQUIRED=NO
REAL_TRIAL_ACTIVATION_EXECUTED=NO
PLAYER_WRITES=0
TRIAL_WRITES=0
ENTITLEMENT_WRITES=0
WALLET_WRITES=0
ONBOARDING_REAL_WRITES=0
COMMIT=NONE
PUSH=NONE
DEPLOY=NO
MEMBERSHIP_01A_SUCCESS=YES_IMPLEMENTATION_AND_ISOLATED_VALIDATION
NEXT=MANUAL_SHARED_MEMBERSHIP_EXPERIENCE_REVIEW
```

## MEMBERSHIP_01B_AUTHORITATIVE_STATE_PRESENTATION

MEMBERSHIP-01A manual visual review was approved by the user: all four plans, selection, features, billing period, Free header, purchases-only Coming Soon copy and trial section PASS. This section supersedes 01A's app trial-action wiring and app state fallback/CTA presentation; the approved catalog geometry and onboarding behavior are preserved.

### Authority and corrections

Effective access remains `PlayerService` server-derived entitlements. A selected commercial tab and billing period are local comparison selections only; neither changes the current membership header or effective benefits. No Diamond/Platinum/Gold ownership state is introduced. Current server `Plan` remains FREE/PREMIUM.

Read-only source review of `TrialActivationService.eligibility` confirms that consumed trial state, its marker or grant returns EXPIRED and eligible=false; only NOT_STARTED is eligible. `EntitlementResolver` independently reports consumed Free state as EXPIRED. The UI never offers a second trial from an expired/consumed snapshot, even if an old eligibility projection still says eligible.

The 01A app renderer had retained onboarding's disabled CTA for noneligible states. 01B now omits that control for active/expired/Premium/unavailable/ineligible app states. Onboarding retains its existing presentation/action logic unchanged. Missing/null/UNKNOWN eligibility cannot classify a fresh FREE snapshot as a resolved Free state. Loading, failed loads, absent snapshot and unsupported effective plans display neutral unavailable semantics, never a fabricated Free label.

### State matrix

| State | Current label EN / ES | Trial CTA | Trial copy | Feature authority | Purchase state | Failure/fallback |
| --- | --- | --- | --- | --- | --- | --- |
| FREE_NOT_ELIGIBLE | Your membership: Free / Tu membresía: Gratis | Absent | No eligible offer claimed | Server effective membership; browsed features remain catalog comparison | Unavailable, no purchase CTA | Requires explicit resolved FREE and INELIGIBLE |
| FREE_TRIAL_AVAILABLE | Your membership: Free / Tu membresía: Gratis | Visible for valid explicit eligible offer; suppressed for Family | Premium promotional trial; duration from periodDays, never a hardcoded 7 | Effective Free independent of selected plan | Unavailable | Missing policy/duration cannot enable action |
| TRIAL_ACTIVE | Your membership: Premium trial / Tu membresía: Prueba Premium | Absent | Your promotional Premium benefits are active / Tus beneficios promocionales Premium están activos; localized actual end date if present | snapshot.features, separately titled Your active benefits / Tus beneficios activos | Future catalog browsable | No calculated remaining days or fabricated date |
| TRIAL_EXPIRED | Your membership: Free · Trial ended / Tu membresía: Gratis · Prueba finalizada | Absent under current consumed-trial contract | Your promotional trial has ended / Tu prueba promocional ha finalizado; actual end date if present | Effective state, not selected tab | Future catalog browsable | No restart, renewal or former paid subscription claim |
| PREMIUM_LEGACY | Your membership: Premium / Tu membresía: Premium | Absent | No trial offer | snapshot.features, never selected catalog tab | Future catalog browsable | No commercial tier remapping |
| UNAVAILABLE | Your membership could not be confirmed / No se pudo confirmar tu membresía | Absent | Neutral status | No assumed access or benefit list | Catalog may still be inspected if its read succeeded | No Free fallback |

For active/Premium states, effective feature labels localize backend entitlement keys. Unknown keys receive a neutral additional-benefit label rather than an invented capability. A missing list says it could not be confirmed; an empty list is shown as empty. No catalog features are promoted into granted benefits. The separately labeled Explore plans / Explorar planes comparison preserves the validated tabs, rows, pricing unavailability and billing geometry. Active trial status/date and granted benefits remain unchanged even when Friends & Family is the viewed tab.

### Non-mutating trial action boundary

The app controller now emits `StartTrialRequested` (`START_TRIAL_REQUESTED`) only. There is **no app activation transport connected in 01B**. The app read-source interface exposes catalog/entitlement reads and has no trial write method. The existing trial API helper is used only for its GET entitlement refresh. Selecting the CTA can show localized “Trial activation is not available here yet”; it cannot change access. The isolated harness intercepts the intention without a network request or even a simulated grant. Wiring/executing a real app activation remains separately authorized TRIAL-LIVE-01 work. Existing onboarding activation/state-machine code was not changed.

The harness can transition the fixture from ineligible Free → eligible Free → active trial → expired trial → legacy Premium → unavailable, without persistence or domain API calls. Replaced sessions hide prior commercial content; pending requests cannot publish a response for another session. Locale changes continue through the production SelectedLocaleChanged listener and localized catalog reads, with newest-request generation winning.

### Validation and safety

- Deterministic Membership tests: **91 PASS**. All six states, effective-plan rejection, missing/unknown eligibility, absent entitlements, loading/failure, four-tab/billing independence for every state, intent interception without grant, exact read-only API paths, current Player ownership, session invalidation and locale race handling.
- Onboarding regression rerun: Membership **76 PASS**, full flow **407 PASS**, bootstrap **31 PASS**, shell **93 PASS**. `MembershipController.cs` and onboarding state-machine logic unchanged.
- Runtime + Editor static compilation PASS. Unity state matrix validation: PENDING_IMPORT.
- Final intended preview: App Membership / TRIAL_ACTIVE / 393x852 / ES. Manual review pending; subsequent expired/Premium/unavailable reviews are not claimed.
- Unity was observed already in real Play during this task. No runtime action was clicked. The user was asked to stop Play and refresh before isolated checks; no claim is made that independently user-started bootstrap had zero metadata effects. Assistant real Player/trial/entitlement writes remain zero.

No backend/configuration changes, purchase implementation, entitlement writes, account changes, logout, commit, push or deployment. Baseline at start of 01B: 168 pending files (the 152 historical files plus 16 from 01A). Only authorized Membership presentation/harness/tests/report files are edited; unrelated files and the 102 protected files must remain byte-for-byte intact.

### MEMBERSHIP_01B_EDITOR_POST_REFRESH_RECOVERY

Recovery review (2026-10-04): Play OFF, no real app flow started. The complete existing stack begins at `UnityEditor.HostView.RegisterSelectedPane`, continues through HostView/DockArea/AddTab/EditorWindow.Show/GetWindow, and first enters project code at `Domino.Editor.SharedMembershipPreview.Open` (line 41 before recovery). The request entry also reproduced the same failure at `Requests` line 44. Project frame present: YES; production runtime frame: NO.

Classification: PROJECT_EDITOR_TOOL lifecycle interaction with Unity HostView, not a demonstrated Membership product failure. Reopening from the normal menu failed; the authorized Editor restart alone also failed. The preview mounted and changed its window position synchronously during CreateGUI, while GetWindow was still registering its pane. Deferring and coalescing Mount through EditorApplication.delayCall resolved opening. Only SharedMembershipPreview.cs lifecycle tooling changed; no production source changed for this recovery. A stale/destroyed window object was not independently proven (STALE_EDITOR_WINDOW_DETECTED=NOT_PROVEN).

Current Unity import completed, Play OFF, compiler errors 0; current Console 9 warnings and 0 errors after import. The earlier HostView exceptions remain documented in Editor logs and are not reclassified as product failures. No full Membership matrix or other states were executed in this recovery.

Final preview visually observed: MEMBERSHIP ISOLATED / App Membership / TRIAL_ACTIVE / es / 393x852. Current label reads Prueba Premium; active promotional-benefits feedback and fixture end date are visible; granted benefits and commercial plan tabs are separate. The source renders no Start trial CTA for this state and no onboarding Not now in the app host. Fixture source has no network transport or domain writes. Manual visual approval remains pending.

UNITY_EDITOR_RESTARTED=YES
MEMBERSHIP_01B_HARNESS_AVAILABLE=YES
MEMBERSHIP_01B_TRIAL_ACTIVE_STATE_AVAILABLE=YES
MEMBERSHIP_VALIDATION_WINDOW_OPEN=YES
TRIAL_ACTIVE_PREVIEW_OPEN=YES
PRODUCTION_SOURCE_CHANGED_FOR_EDITOR_RECOVERY=NO
REAL_TRIAL_ACTIVATION_EXECUTED=NO
PLAYER_WRITES=0
TRIAL_WRITES=0
ENTITLEMENT_WRITES=0
WALLET_WRITES=0
ONBOARDING_REAL_WRITES=0
COMMIT=NONE
PUSH=NONE
DEPLOY=NO
NEXT=MANUAL_TRIAL_ACTIVE_MEMBERSHIP_REVIEW
## MEMBERSHIP_01B_TRIAL_PLAN_TIMELINE_CONTRACT_REVIEW

Owner product update supersedes the earlier 01B acceptance of generic Premium trial presentation. This section distinguishes EXISTING IMPLEMENTATION from TARGET CONTRACT. The preceding legacy state matrix is retained as historical evidence, not approval of the newly requested commercial trial. No further manual Membership state reviews were run.

### Configuration authority audit (source inspection, no live database read)

| Concern | Existing authority | Finding |
|---|---|---|
| Duration | `systemConfig/subscriptionPolicy.promotionalTrialDays`, loaded transactionally by TrialActivationService.inputs; fallback SubscriptionPolicy bound to `domino.subscription` | Source default 7; validated 1..30. Eligibility exposes `periodDays`. Current deployed override NOT READ / UNKNOWN. UI must use policy response, not literal 7. |
| Reminder | No field/service in production subscription policy, activation, entitlement or catalog model | MISSING. No configured lead time or scheduling/delivery contract found. Two days is owner target/fixture only. |
| Eligibility | TrialActivationService.eligibility/activate | Active Player, account/test/link policy, no paid active grant, consumed marker/grant checks, enabled promotion, expected policy version; authoritative transaction recheck. |
| Commercial plan | `systemConfig/membershipCatalog.publishedVersion` -> `membershipCatalogs/{version}` | FREE plus GOLD/PLATINUM/DIAMOND/FRIENDS_AND_FAMILY catalog. Target policies inactive. Runtime Plan enum only FREE/PREMIUM. |
| Billing period | MembershipBillingProduct / MembershipBillingPeriod | MONTHLY and YEARLY. Owner concept ANNUAL maps explicitly to YEARLY; do not introduce a competing wire key. |
| Product / price | Catalog billingProducts: platform, planKey, billingPeriod, storeProductId, active/purchasable | priceAuthority=APPLE_GOOGLE_STORE. No store price, currency, trial offer, renewal or first-charge schedule in response. No automatic conversion implementation. |
| Trial product | MembershipTrialReference / MembershipCatalogValidation | Forces PREMIUM_LEGACY, commercialTrialEnabled=false and familyTrialEnabled=false. Publication/localization also forces these flags. Not compatible with the requested plan-bound trial. |

TRIAL_DURATION_CONFIG_SOURCE=systemConfig/subscriptionPolicy.promotionalTrialDays (SubscriptionPolicy fallback)
TRIAL_DURATION_DAYS=7_SOURCE_DEFAULT; LIVE_OVERRIDE_UNKNOWN
TRIAL_REMINDER_CONFIG_SOURCE=MISSING
TRIAL_REMINDER_BEFORE_END_DAYS=UNKNOWN_CURRENT; 2_OWNER_TARGET_EXAMPLE
TRIAL_REMINDER_CONFIGURATION_MISSING=YES

### Backend persisted/response gap matrix

| Requested value | Persisted today | Public contract today | Required change |
|---|---|---|---|
| trialPlan | NO commercial plan; EntitlementGrant.plan constrained PREMIUM | NO | Immutable commercial plan key + catalog/benefit policy version on accepted trial offer/grant. |
| trialBillingPeriod | NO | NO | Persist selected valid MONTHLY/YEARLY and associated store product/offer. |
| trialStartedAt | Equivalent YES: grant.validFrom and promotions/initial-premium-trial.trialGrantedAt | NOT in EffectiveEntitlements | Expose explicit authoritative start timestamp on trial status. |
| trialEndsAt | YES: grant.validUntil, promotion.trialEndsAt | YES: snapshot.trialEndsAt | Retain server timestamp, align with verified store offer when billing-backed. |
| reminder policy | NO | NO | Versioned lead time, reminderAt, channel/consent and delivery state; deduplicated durable scheduling. |
| post-trial subscription intent | NO | NO | Bound product/platform/billing offer, quoted price/currency, renewal terms, first charge authority and validated store result. |
| paid membership separate from trial access | Not separately exposed; effective PREMIUM merges any active grant | NO independent membership identity | Separate current subscription/membership from trial overlay and effective entitlements. Do not turn effective PREMIUM into FREE by inference. |

Evidence files: `server/domino/src/main/kotlin/com/teamfho/domino/entitlement/{TrialActivation.kt,TrialActivationHttp.kt,EntitlementModels.kt,EntitlementConfiguration.kt}` and `catalog/{MembershipCatalog.kt,MembershipCatalogRepository.kt}`.

Current POST `/api/v1/player/trial/activate` requires exactly TWO fields: operationId and expectedPolicyVersion. Sending plan/billing to this endpoint would be rejected. It grants initial-premium-trial from Clock.systemUTC, for policyDays * 86400 seconds, and marks trial consumed. It does not create a payment/subscription. Existing consumed grants must not be remapped to Diamond or granted again.

### Proposed boundary for review, NOT implemented backend

Membership -> select commercial plan -> select billing period -> request trial offer -> TRIAL CONFIRMATION -> explicit consent -> future authorized activation/store system.

Proposed TrialIntent: selectedPlanKey, billingPeriod (YEARLY/MONTHLY), platform, expectedCatalogVersion, expectedTrialPolicyVersion. Server must validate eligible product/period, including a separate Family eligibility decision; commercial inclusion does not authorize Family runtime or a Family trial.

Proposed authoritative offer: offerId/version, expiry/serverTime, planKey, billingPeriod, trialDurationDays, startsAt semantics, trialEndsAt, reminderBeforeEndDays/reminderAt, today amount/currency, storeProductId/offerId, localized post-trial price, firstChargeAt, renewal/cancellation disclosures. If timing starts only at acceptance, pre-confirmation dates must be explicitly provisional and refreshed as necessary. UI never calculates billing authority from device time. Missing price/product/terms prevents live confirmation; HTTP success alone is not proof of a purchased subscription.

Activation/replay must bind idempotency to the whole accepted offer and preserve the one-trial eligibility group, including legacy consumed history. Store-verified status governs billing; backend projects it to the client. Do not claim that trialEndsAt automatically means a charge under today's BE-07 grant. UTC instants and display timezone must be explicit. Reminder is relative to the authoritative end; changes/cancellation must reconcile scheduled notifications idempotently. Notification delivery/consent policy remains a required design decision.

Active target presentation: current membership (e.g. FREE ONLY when independently authoritative), separate free-trial-active status, immutable trialPlan/billing, authoritative end. Effective trial benefits must be resolved for the bound plan/policy by the backend; changing the viewed commercial tab must not change granted benefits. Catalog is a comparison/fixture source, not a client entitlement grant.

Cancellation disclosure target: the final store contract must explain renewal, price and the effective cancellation cutoff needed to avoid conversion charges. No cancellation behavior or unconditional cancellation promise is implemented here.

### Isolated target-contract harness

Added `Editor/MembershipTrialContractPreview.cs` with menu `Domino > Membership > Trial plan contract (isolated design)`. Explicitly labeled TARGET CONTRACT / FIXTURE ONLY. Separate from the existing production-backed legacy harness; no production dependency on this proposed DTO or view. States AVAILABLE and ACTIVE; locale EN/ES; plan selection and MONTHLY/YEARLY. ACTIVE trial plan and billing remain pinned to fixture fields while comparison selections vary. No automatic opening or further state manual review was performed.

Fixture: `Validation/MembershipTrialTargetFixture.json`. FREE + DIAMOND + YEARLY + AVAILABLE/ACTIVE. The sample instants use 12:00 UTC solely to make timezone explicit: starts 2026-10-04, ends 2026-10-11, reminder 2026-10-09. Current-day zero uses fixture currency USD; no post-trial price is fabricated. First charge date is marked example, billingAuthorityConfirmed=false, reminderDeliveryImplemented=false.

Confirmation prototype captures selected plan/period, duration, zero today, example end/charge/reminder and unavailable pricing/disclosures. Final confirmation disabled; no activation handler/transport. This is a contract review aid, not the final approved visual composition and not a promise of a live offer.

`CheckMembershipTrialTarget.ps1`: 35 PASS (fixture dates/policy, separate FREE membership, plan/billing binding, EN/ES catalog features, unavailable billing authority, no transport, no client-clock authority, disabled confirmation). Runtime + Editor static compilation PASS after new harness. Unity import/manual visual validation of this new prototype NOT CLAIMED. Earlier production suites retained, not rerun: production source unchanged.

TRIAL_IS_MEMBERSHIP_PLAN=NO_TARGET
TRIAL_REQUIRES_SELECTED_PLAN=YES_TARGET
TRIAL_REQUIRES_BILLING_PERIOD=YES_TARGET
TRIAL_BENEFIT_SOURCE=SELECTED_PLAN_SERVER_RESOLVED_TARGET
TRIAL_STATUS_PRESENTED_SEPARATELY_FROM_MEMBERSHIP=YES_TARGET_PROTOTYPE
TRIAL_TIME_AUTHORITY=SERVER_TARGET
TRIAL_REMINDER_RELATIVE_TO_END=YES_TARGET
TRIAL_CHARGE_TODAY_ZERO=YES_FOR_ELIGIBLE_TRIAL_TARGET
POST_TRIAL_PRICE_VISIBLE=YES_TARGET; CURRENT_AUTHORITY_MISSING
FIRST_CHARGE_DATE_VISIBLE=YES_TARGET; CURRENT_AUTHORITY_MISSING
TRIAL_CONFIRMATION_REQUIRED=YES
HARNESS_TRIAL_PLAN_BINDING_SUPPORTED=YES_ISOLATED_TARGET_ONLY
BACKEND_CHANGE_REQUIRED=YES
PRODUCTION_TRIAL_PRESENTATION_REWORK=PENDING_NEW_AUTHORITATIVE_CONTRACT
REAL_PURCHASE_FLOW_IMPLEMENTED=NO
REAL_CANCELLATION_FLOW_IMPLEMENTED=NO
REAL_TRIAL_ACTIVATION_EXECUTED=NO
BACKEND_SOURCE_CHANGED=NO
PRODUCTION_SOURCE_CHANGED_THIS_REVIEW=NO
COMMIT=NONE
PUSH=NONE
DEPLOY=NO
NEXT=MEMBERSHIP-01B TRIAL PLAN + TIMELINE CONTRACT REVIEW
## MEMBERSHIP_01C_BACKEND_CONTRACT_REVIEW_GATE

Base verified: ce5e88ad81e8bd5460ddbbab641f3adfc536b550. Read-only backend audit; implementation paused at the owner's explicit section 37 stop condition (trial benefit semantics). No backend/client product code changed in this 01C review. Earlier 01B changes remain pending and are not 01C implementation evidence.

### Exact existing domain

- Storage under players/{alias}: entitlementState/current (revision, consumed flag, grant projection); entitlementGrants/initial-premium-trial; promotions/initial-premium-trial (trialConsumed, grantId, trialGrantedAt, trialEndsAt); entitlementAudit/TRIAL_GRANTED; trialActivationReceipts/{operationId}. No real identity is recorded here.
- Service: TrialActivationService.inputs/eligibility/activate, EntitlementService.activateTrial/summary/bootstrap, EntitlementResolver.resolve.
- Endpoint: POST /api/v1/player/trial/activate. Strict JSON request currently accepts exactly operationId and expectedPolicyVersion; TrialActivationRequest/TrialActivationResponse, TrialEligibilityResponse, EntitlementSummary/EffectiveEntitlements.
- Configuration: systemConfig/subscriptionPolicy; SubscriptionPolicy bound to domino.subscription provides fallback. promotionalTrialDays default 7, range 1..30. Actual TEST override not read in this gated review; UNKNOWN, not assumed equal to the source default.
- Bootstrap reads summary and does not activate. Explicit activation uses server Clock, the existing buffered repository transaction, account-level consumed markers and request receipts. Historical grants default to Plan.PREMIUM; resolver currently uses SubscriptionPolicy.premiumFeatures for any active grant.

### Existing authority and concrete unresolved decision

MembershipCatalogSeed and MembershipCatalogValidation define authoritative stable commercial keys. Reuse these keys and versioned catalog policy, not a second independently maintained plan enum. Existing billing key YEARLY represents ANNUAL; an explicit normalized boundary alias can support ANNUAL while retaining YEARLY compatibility.

The approved ONB_BE_06_MEMBERSHIP_CATALOG_CONTRACT_REPORT.md distinguishes commercial capabilities from backend target capabilities and explicitly says commercial service/enforcement existence is not asserted. MembershipCatalogTests verifies both matrices independently. MembershipTargetPolicy is inactive and publication rejects active=true.

Commercial Gold: PUZZLES, LESSONS, COACH_GAMES, BOTS, NO_ADS. Runtime target Gold instead adds PARTY_CREATE, PARTY_INVITE, PRIVATE_DUEL, PRIVATE_PARTNERS, CHOOSE_2V2_PARTNER, PARTY_MATCHMAKING, PREMIUM_THEMES to Free. Platinum adds FULL_HISTORY and FULL_REPLAY; Diamond adds ADVANCED_STATS. Except ADVANCED_STATS, commercial feature keys are absent from EntitlementFeature. GAME_REVIEW is explicitly distinct from FULL_REPLAY/FULL_HISTORY. Calling the existing runtime subset the complete commercial benefits would be inaccurate.

Decision needed before grant/resolver changes:

A. Minimum backend scope: bind new trials to the selected plan's existing versioned backend capabilities and limits, retaining the commercial feature matrix separately as catalog metadata. Explicitly identify commercial capabilities whose services/enforcement are not implemented; do not claim complete commercial access. This is the smallest implementation, if that is the intended trial-benefit semantics.

B. Full commercial entitlement representation: introduce explicit authoritative commercial benefit grants/projection in addition to existing runtime permissions, with a separate scoped decision for each not-yet-implemented service/enforcement. This requires a larger contract scope and must not silently treat those services as available.

No choice was made automatically. Merely activating targetPolicy.active would not bridge these two namespaces or implement missing services.

### Decisions that do not require new product policy

FRIENDS_FAMILY_TRIAL_SUPPORTED=NO_CURRENT_POLICY (familyTrialEnabled=false; no Family runtime). Preserve rejection; listing Family as a commercial plan is not permission to grant a trial.
EXISTING_PREMIUM_LEGACY_TRIALS_MIGRATED_TO_DIAMOND=NO.
STORE_OFFER_REFERENCE_REQUIRED_NOW=NO for a non-billing promotional grant; defer provider-specific IDs and actual charge authority to STORE-SUBSCRIPTION-01.

### Concrete proposed minimum contract (not implemented)

New request: existing operationId/expectedPolicyVersion plus required plan and billingPeriod, normalized to catalog stable keys and MONTHLY/YEARLY. Reject duration and all client timestamps, including unknown fields. Binding must enter the idempotency hash and immutable grant snapshot. Different plan/period on an active trial must conflict, never switch/regrant. Legacy receipts/grants remain readable and do not become plan-bound by inference.

New grant fields: nullable trialPlan, trialBillingPeriod, reminderAt, plus pinned catalog/mapping version and authoritative benefit snapshot according to the pending decision. Existing validFrom/validUntil remain canonical Instants and project as trialStartedAt/trialEndsAt. Null plan/billing identifies legacy, not Diamond. Account-level initial trial consumption remains shared across all plans and periods.

Configuration proposal: SubscriptionPolicy.trialReminderBeforeEndDays default 2; use policy values, not literals in the service/UI. Validate new activation policy 0 <= lead < duration; reject invalid combinations rather than silently adjusting them. Keep existing short-duration legacy policies readable; a new activation can fail closed for invalid new reminder configuration. Server UTC Instant + durationDays*86400 gives end; end - leadDays*86400 gives reminder. This specifies elapsed UTC days, not localized calendar arithmetic or client clock authority.

New response projection: trialStatus, eligible (eligibility response), nullable trialPlan/trialBillingPeriod/trialStartedAt/trialEndsAt/reminderAt, explicit legacy distinction, and membership identity separate from effective trial overlay. No fabricated paid commercial membership from legacy PREMIUM. Expected trial end is not an actual first charge date; no price/charge/subscription or notification delivery is implemented.

Future isolated tests after decision: four requested individual plan/period combinations, Family rejection, custom duration/lead, exact fixed-clock example, malformed/client-authority requests, null legacy fields, consumed cross-plan attempts, same-request retry and conflicting retries, transaction rollback/concurrency, client legacy/new DTO mapping and localized timeline. These are planned, not PASS results.

### Current gate result

MEMBERSHIP_01C_STATUS=BLOCKED_OWNER_BENEFIT_SEMANTICS_DECISION
TRIAL_DURATION_DEFAULT_DAYS=7
TEST_TRIAL_DURATION_DAYS=UNKNOWN_NOT_READ
TRIAL_REMINDER_DEFAULT_BEFORE_END_DAYS=2_PROPOSED_NOT_IMPLEMENTED
TRIAL_PLAN_CONTRACT_BACKEND_TESTS=NOT_RUN_IMPLEMENTATION_NOT_STARTED
TRIAL_PLAN_CONTRACT_CLIENT_TESTS=NOT_RUN_IMPLEMENTATION_NOT_STARTED
HARNESS_USES_PLAN_BOUND_TRIAL_MODEL=NO_NEW_SERVER_CONTRACT_YET
DATA_MIGRATION_REQUIRED=NO_PROPOSED_ADDITIVE_LEGACY_SAFE_MODEL
REAL_TRIAL_ACTIVATION_EXECUTED=NO
REAL_TRIAL_DOCUMENT_MUTATIONS=0
REAL_ENTITLEMENT_MUTATIONS=0
REAL_PLAYER_MEMBERSHIP_MUTATIONS=0
BACKEND_SOURCE_CHANGED=NO
CLIENT_PRODUCT_SOURCE_CHANGED=NO
BACKEND_REDEPLOYED=NO
COMMIT=NONE
PUSH=NONE
DEPLOY=NO
OWNER_DECISIONS_REQUIRED=EXISTING_BACKEND_CAPABILITY_SUBSET_VS_FULL_COMMERCIAL_BENEFIT_CONTRACT
NEXT=MEMBERSHIP-01C CONTRACT REVIEW — TRIAL BENEFIT SEMANTICS
## MEMBERSHIP_01C_IMPLEMENTATION — OWNER B_COMPLETE

The owner resolved the preceding gate with B_COMPLETE. The previous BLOCKED section is historical; implementation below supersedes it. Base remains ce5e88ad81e8bd5460ddbbab641f3adfc536b550. Nothing committed, published or deployed.

### Three distinct authorities

1. COMMERCIAL_PLAN_BENEFITS: unchanged MembershipCatalogSeed/immutable membershipCatalogs planFeatures. Gold: PUZZLES, LESSONS, COACH_GAMES, BOTS, NO_ADS. Platinum adds GAME_REVIEW. Diamond adds MOVE_EXPLANATIONS and ADVANCED_STATS. Family retains Diamond-equivalent commercial inclusion. None of these relations were changed to match old technical permissions.
2. EFFECTIVE_ENTITLEMENTS: the selected plan's versioned MembershipTargetPolicy supplies existing backend capability/limit mappings, pinned into the new grant. New trials do not read SubscriptionPolicy.premiumFeatures. Free base capabilities still apply. ADVANCED_STATS is excluded from NEW trial permissions because its complete commercial implementation is not available. Legacy Premium/admin grants retain existing semantics.
3. CAPABILITY_IMPLEMENTATION_STATUS: explicit CommercialCapabilityImplementation registry; response on catalog features and trial commercial benefits. currentlyUsable=false for incomplete capabilities. Unknown future keys fail closed to FUTURE, unusable.

| Commercial benefit | Status in this architecture | Meaning |
|---|---|---|
| GAME_REVIEW | NOT_IMPLEMENTED | Match history/replay exist but are explicitly not commercial coaching/review. |
| MOVE_EXPLANATIONS | NOT_IMPLEMENTED | No production move explanation service/gate found. |
| ADVANCED_STATS | PARTIALLY_IMPLEMENTED | Technical permission and presentation foundations exist; complete commercial statistics service/access not established. |
| PUZZLES | NOT_IMPLEMENTED | Root/presentation scaffolding does not establish commercial gameplay service. |
| LESSONS | NOT_IMPLEMENTED | Learning presentation is not an implemented entitlement-controlled course service. |
| COACH_GAMES | NOT_IMPLEMENTED | Coach selection/catalog is not Coach AI/practice runtime. |
| BOTS | PARTIALLY_IMPLEMENTED | Game catalog bot capability/configuration and testing foundations exist; complete commercial user flow not established. |
| NO_ADS | PARTIALLY_IMPLEMENTED | Ads SDK/reward infrastructure exists; commercial NO_ADS enforcement not connected. |

PARTIALLY_IMPLEMENTED describes supporting architecture only; it does NOT authorize current use. All eight currentlyUsable values are false. Membership catalog and bound-trial benefit rows mark unavailable commercial capabilities Coming soon / Próximamente. This registry is code-owned, separate from commercial promise and effective permission matrix. Future implementation changes need explicit review; trial commercial snapshots are immutable activation-time evidence.

### Storage and schema evolution

Existing paths and single account-level grant ID remain unchanged. New `EntitlementGrant.planBoundTrial` is optional for reading legacy data and required for creation of a new plan-bound grant. Stored both in `entitlementGrants/initial-premium-trial` and the existing `entitlementState/current.grants` projection within the same transaction.

Fields: trialPlan, trialBillingPeriod, catalogVersion, entitlementPolicyVersion, trialStartedAt, trialEndsAt, reminderAt, commercialPlanBenefits[{key,implementationStatus,currentlyUsable}], effectiveFeatures, effectiveLimits. Kotlin Instant fields serialize as canonical ISO-8601 UTC strings using the existing MatchCodec; they are not localized calendar strings. Existing validFrom/validUntil must equal nested timestamps. Existing promotion markers continue to preserve consumed/start/end evidence. No additional trial consumption namespace was introduced.

Existing grants with no planBoundTrial are read as legacy=true, null plan/billing/reminder, with actual validFrom/validUntil exposed. No Diamond inference, no legacy migration, no backfill. Existing receipts retain their original two-field canonical hash and remain readable. New canonical hashes include plan and period; ANNUAL normalizes to existing YEARLY. Replays return the stored response; response set ordering was made deterministic so first and replay serialization match.

### Activation/configuration/timeline

Endpoint remains POST /api/v1/player/trial/activate. For NEW grants: operationId, expectedPolicyVersion, plan, billingPeriod. Unknown JSON fields and client duration/time fields are rejected. Two-field requests remain parseable only for legacy receipt/active-grant compatibility; they cannot create a new grant. Null/unsupported bindings cannot silently become Diamond. Family trial is explicitly rejected with TRIAL_PLAN_NOT_SUPPORTED. Different plan/period for an active grant returns TRIAL_BINDING_CONFLICT. Expired/consumed grants still return TRIAL_ALREADY_CONSUMED regardless of selected plan. No active switching.

Commercial identifiers are resolved against the existing published catalog, not a duplicate catalog. MONTHLY and YEARLY reuse MembershipBillingPeriod; ANNUAL is an accepted input alias of YEARLY. No store purchase identity or price is required for this non-billing domain contract. Catalog legacy trialPresentation is still a presentation/reference document, not a runtime policy loader; old immutable publications are not rewritten or activated globally.

Runtime policy: existing systemConfig/subscriptionPolicy with domino.subscription fallback. Duration property promotionalTrialDays (default 7); added trialReminderBeforeEndDays (default 2). Valid new activation requires 0 <= reminder lead < duration. Existing short-duration policy data remains readable; invalid new reminder combinations fail activation as TRIAL_POLICY_INVALID rather than silently shifting dates. TEST policy override was NOT read; TEST_TRIAL_DURATION_DAYS=UNKNOWN. No configuration changed.

Server Clock supplies activation Instant. End=start+durationDays*86400; reminder=end-leadDays*86400. No client clock or calendar authority. Fixed-clock example: Oct 4 12:00Z -> Oct 11 12:00Z, reminder Oct 9 12:00Z. Those constants appear only in fixtures/tests. Reminder is scheduling data only, not delivered notification. Trial end is NOT an actual store first-charge date. No price, charge, auto-renewal, subscription or cancellation behavior introduced.

### Response and client model

Added TrialStateResponse (trialStatus, nullable trialPlan/trialBillingPeriod/trialStartedAt/trialEndsAt/reminderAt, legacy, commercialPlanBenefits) to activation response, eligibility response and effective entitlement snapshot. Eligibility remains authoritative at the account level and exposes policy duration/reminder lead. Snapshot adds membershipPlan independently of the trial overlay. Technical compatibility snapshot.plan remains the existing FREE/PREMIUM enum; it is NOT a new commercial identity. A trial-only account's separate membershipPlan is FREE; active non-trial legacy coverage remains PREMIUM, without commercial remap.

Client DTOs mirror nullable legacy/new shape. Membership uses authoritative membershipPlan for identity and a separate active/expired bound-trial presentation, bound plan/billing, end date in explicit UTC and plan commercial benefits with availability labels. Catalog selection cannot change immutable trial binding. Existing selected catalog plan/billing and snapshot.features remain separate properties. The app StartTrialRequested boundary is still intent-only, with no real activation transport connected. Existing old onboarding activation integration is not upgraded to the new required plan-confirmation flow in this phase; new grants without binding will be rejected and must not be live-tested until the later integration is authorized.

Both isolated previews now deserialize TrialStateDto. SharedMembershipPreview's active/expired fixtures use the new server shape; legacy Premium fixtures remain separate. MembershipTrialContractPreview keeps confirmation disabled and network-free. No real activation or purchases. Unity visual validation/import is not claimed for these changes; static compilation succeeded.

### Tests and limits of evidence

- Full root backend :test: 928 total, 904 passed, 24 skipped, 0 failures (before two final compatibility-only tests were added). Runtime source unchanged afterward.
- Final focused PlanBoundTrialTests: 9 passed, including original legacy receipt fingerprint and ANNUAL/YEARLY idempotent replay. Combined distinct passed cases: 906; not described as a second full-suite run.
- Existing TrialActivation tests cover concurrency, one-time consumed state, policy changes, orphan evidence, auth and bootstrap no-grant. Updated fixtures now explicitly select plan/billing.
- SDK transaction test passes: retried transactions buffer one atomic set of five writes, all reads before writes, no writes on replay. This is mocked SDK/in-memory concurrency evidence, NOT real Firestore validation.
- New tests cover requested Diamond monthly/annual, Platinum monthly, Gold annual, Family rejection, exact commercial inclusion, incomplete-capability exclusion, custom duration/reminder, UTC timestamps, expired state, no switching, legacy fields, account-level reuse and receipt compatibility. HTTP tests reject client timestamps, duration, unknown fields and malformed input.
- Client shared Membership checks: 107 PASS; target fixture checks: 35 PASS; Runtime + Editor static compilation PASS. New/legacy DTOs, separate membership identity, active/expired binding, localized UTC timeline and unavailable commercial functions covered.
- Initial test command selected subproject bot-swarm too and failed because no matching test names existed there; rerunning the correctly scoped root :test succeeded. An HTTP exact replay test found nondeterministic set serialization; deterministic ordering fixed it. No failed test remains hidden.
- Protected historical inventory: 102 checked, 0 differences. No real API/session operation, Play action, database change or deployment executed in this phase.

COMMERCIAL_BENEFIT_CONTRACT_COMPLETE=YES
LEGACY_ENTITLEMENTS_DEFINE_COMMERCIAL_PLAN=NO
CAPABILITY_IMPLEMENTATION_STATUS_EXPLICIT=YES
NEW_TRIAL_BENEFIT_SOURCE=SELECTED_PLAN
TRIAL_GRANTS_UNIMPLEMENTED_FUNCTIONALITY=NO
FRIENDS_FAMILY_TRIAL_SUPPORTED=NO_CURRENT_POLICY
LEGACY_TRIALS_MIGRATED=NO
LEGACY_PREMIUM_COMMERCIAL_REMAP=NO
STORE_OFFER_REFERENCE_REQUIRED_NOW=NO
TRIAL_PLAN_REQUIRED_FOR_NEW_MODEL=YES
TRIAL_BILLING_PERIOD_REQUIRED_FOR_NEW_MODEL=YES
TRIAL_TIME_AUTHORITY=SERVER
TRIAL_TIMELINE_SERVER_COMPUTED=YES
TRIAL_RESPONSE_SUPPORTS_TIMELINE=YES
SECOND_TRIAL_PREVENTION_PRESERVED=YES
TRIAL_ELIGIBILITY_ACCOUNT_LEVEL=YES
TRIAL_ACTIVATION_ATOMIC=YES_EXISTING_TRANSACTION_ADAPTER
DUPLICATE_ACTIVATION_GRANTS=0_IN_ISOLATED_TESTS
MEMBERSHIP_AND_TRIAL_PRESENTATION_SEPARATED=YES
HARNESS_USES_PLAN_BOUND_TRIAL_MODEL=YES
HARNESS_DOMAIN_WRITES=0
TRIAL_PLAN_CONTRACT_BACKEND_TESTS=PASS
TRIAL_PLAN_CONTRACT_CLIENT_TESTS=PASS
DATA_MIGRATION_REQUIRED=NO
REAL_TRIAL_CTA_CONNECTED=NO_NEW_CONNECTION
ACTUAL_FIRST_CHARGE_AUTHORITY_IMPLEMENTED=NO
TRIAL_REMINDER_DELIVERY_IMPLEMENTED=NO
REAL_DATA_MUTATIONS=0
REAL_TRIAL_ACTIVATION_EXECUTED=NO
REAL_PURCHASE=NO
REAL_SUBSCRIPTION=NO
REAL_TEST_DATA_MIGRATION=NO
BACKEND_REDEPLOYED=NO
COMMIT=NONE
PUSH=NONE
DEPLOY=NO
OWNER_DECISIONS_REQUIRED=NONE_FOR_THIS_SCOPE
NEXT=MEMBERSHIP-01C CONTRACT REVIEW
## MEMBERSHIP_01D_PLAN_BOUND_TRIAL_UI — 2026-10-04

### Implementation and authority

The shared production Membership view now separates membership identity from its promotional overlay. A FREE identity with an ACTIVE DIAMOND trial displays Free/Gratis above a distinct trial card. The card uses the backend trial binding (localized catalog plan name, canonical YEARLY rendered Annual/Anual), authoritative end and optional reminder instants formatted in explicit UTC. Reminder delivery is explicitly unavailable; a date is not a delivery guarantee. No client clock or date arithmetic supplies the timeline.

Commercial benefit rows come from the trial response's commercialPlanBenefits, independently of the currently viewed catalog tab. A benefit is presented as usable only when both currentlyUsable and IMPLEMENTED agree; other and unknown statuses retain Coming soon. Browsing Gold during a Diamond trial leaves membership, binding, benefits and timeline unchanged. Expired trial and legacy Premium remain distinct states; neither creates a new commercial plan tab or a repeat-trial action. Unavailable state renders neither Free nor a stale trial card.

Available-trial intent uses configured eligibility duration and individual commercial plan selection. Friends & Family remains excluded. The app controller emits a presentation intent only; no activation transport is connected. The isolated confirmation retains selected plan/period, configured days, zero today, expected fixture end and reminder policy; its final confirmation is disabled. Removed the first-charge example entirely from both its view and fixture. Store price remains unavailable without a store offer. Existing onboarding action/controller behavior was not expanded; its Not now action remains intact. No live onboarding activation was exercised.

### Isolated harness and current validation

SharedMembershipPreview offers independent membership, trial status, trial plan, trial period, end, reminder and viewed-plan controls in an Editor-only foldout. Its source constructs fixture DTOs only, never the production API source. Presets initialize coherent combinations; independent controls can then vary them without persisting anything. Trial plan changes filter fixture commercial benefits; viewed-plan changes do not.

Current Unity run began 2026-10-04T17:09:00.6319985Z. Result: 15,812 checks PASS, 144 scenarios PASS, 0 failures. This covers EN and ES at 375x667, 393x852, 412x915, 430x932, 480x1040, 600x960, 768x1024 and 834x1194. Each locale/size covers Free ineligible; Free available Diamond monthly and yearly; Free active Diamond yearly; Free expired Diamond yearly; Premium legacy; unavailable; Family selection without trial; plus shared onboarding parity. Checks include horizontal bounds, label measurement/clipping, scroll/focus reachability, four tabs, yearly copy, server timeline, authority preservation, onboarding Not now, session invalidation and return to Menu. Additional independent-dimension checks use a Gold monthly trial with a separately viewed Diamond tab and absent reminder.

- Shared Membership client/controller checks: 142 PASS (current).
- Isolated confirmation/fixture safety checks: 38 PASS (current).
- Menu binding regression: 60 PASS (current).
- Onboarding Membership: 76 PASS; onboarding shell: 93 PASS; bootstrap client contract: 31 PASS (current).
- Runtime + Editor static compilation: PASS; Unity import completed, Console errors 0, warnings 9. No new blocking exception observed.
- Backend 01C tests retained; no backend suite rerun or source change in 01D.
- Final preview inspected: App Membership, 393x852, ES, FREE / ACTIVE / DIAMOND / YEARLY; end 11/10/2026 UTC, reminder 09/10/2026 UTC. Manual visual approval remains pending.

### Boundaries and result

Changes limited to six Membership presentation/controller/editor files, three fixture/test files and this report. Baseline held 182 pending files; all 172 outside the ten-file 01D scope retain their hashes. Protected inventory: 102/102 unchanged. Backend, App Shell Mock and production routing unchanged by 01D. Scope scan contains no credential/private-key/token material or personal identities. The global diff check also reports pre-existing trailing whitespace in AdsSettings.asset; that protected/unrelated file was not changed or repaired by this task.

MEMBERSHIP_AND_TRIAL_PRESENTATION_SEPARATED=YES
TRIAL_ACTIVE_MEMBERSHIP_LABEL=FREE
ACTIVE_TRIAL_PLAN_VISIBLE=YES
ACTIVE_TRIAL_BILLING_PERIOD_VISIBLE=YES
ACTIVE_TRIAL_END_VISIBLE=YES
ACTIVE_TRIAL_REMINDER_VISIBLE=YES_IF_PRESENT
ACTUAL_FIRST_CHARGE_AUTHORITY_IMPLEMENTED=NO
FALSE_FIRST_CHARGE_DATE_VISIBLE=NO
ACTIVE_TRIAL_BENEFIT_SOURCE=SELECTED_PLAN_AT_ACTIVATION_TRIAL_PLAN
UNIMPLEMENTED_CAPABILITY_PRESENTED_AS_ACTIVE=NO
PREMIUM_VISIBLE_AS_PLAN_TAB=NO
VIEWED_PLAN_MUTATES_TRIAL_PLAN=NO
TRIAL_AVAILABLE_DURATION_SOURCE=AUTHORITATIVE_ELIGIBILITY_PERIOD_DAYS
TRIAL_DURATION_HARDCODED_IN_UI=NO
CONFIRMATION_POST_TRIAL_PRICE_WITHOUT_STORE_OFFER=NO
REAL_TRIAL_CONFIRMATION_CAN_ACTIVATE=NO
TRIAL_EXPIRED_IS_MEMBERSHIP_PLAN=NO
TRIAL_EXPIRED_FALSE_REACTIVATION_CTA=NO
LEGACY_PREMIUM_COMMERCIAL_REMAP=NO
UNAVAILABLE_RENDERS_FREE=NO
UNAVAILABLE_FAKE_TRIAL_STATE=NO
FRIENDS_FAMILY_TRIAL_SUPPORTED=NO_CURRENT_POLICY
FRIENDS_FAMILY_TRIAL_CTA_VISIBLE=NO
ANNUAL_UI_MAPS_TO_YEARLY=YES
CLIENT_RECOMPUTES_TRIAL_END=NO
CLIENT_RECOMPUTES_REMINDER=NO
HARNESS_DIMENSIONS_INDEPENDENT=YES
HARNESS_PLAYER_WRITES=0
HARNESS_TRIAL_WRITES=0
HARNESS_ENTITLEMENT_WRITES=0
MEMBERSHIP_01D_STATE_MATRIX=PASS
MEMBERSHIP_01D_TESTS=PASS
ONBOARDING_MEMBERSHIP_REGRESSION=PASS
ONBOARDING_NOT_NOW_PRESERVED=YES
MENU_MEMBERSHIP_REGRESSION=PASS
MEMBERSHIP_01D_EN=PASS
MEMBERSHIP_01D_ES=PASS
MEMBERSHIP_01D_RESPONSIVE=8/8_PASS_EN_ES
HORIZONTAL_OVERFLOW=0
TEXT_CLIPPING=0
REAL_TRIAL_ACTIVATION_EXECUTED=NO
REAL_TRIAL_DOCUMENT_MUTATIONS=0
REAL_ENTITLEMENT_MUTATIONS=0
REAL_PLAYER_MEMBERSHIP_MUTATIONS=0
REAL_PURCHASES=0
REAL_NETWORK_REQUESTS=0
PREEXISTING_PROTECTED_FILES_MODIFIED=0/102
SECRET_SCAN=PASS
BACKEND_REDEPLOYED=NO
COMMIT=NONE
PUSH=NONE
DEPLOY=NO
MANUAL_VISUAL_REVIEW=PENDING
NEXT=MANUAL DIAMOND YEARLY ACTIVE TRIAL REVIEW

### MEMBERSHIP_01D — manual-review date/reminder correction

Owner approved the conceptual model and requested only two presentation changes. TimelineDate now follows the existing Profile date-only localization convention: the server instant remains authoritative, displayed as `11 de octubre de 2026` / `October 11, 2026`, without a raw UTC suffix. No timestamp, duration or end date is recomputed. Production Membership no longer renders either the reminder date or the reminder-delivery implementation note. reminderAt remains unchanged in the domain/API DTO and fixtures. This supersedes the earlier visible-reminder requirement; the isolated design-only confirmation is not production UI.

Current focused client checks: 142 PASS. Runtime/Editor static compilation PASS. The complete 15,812-check matrix is retained, not repeated. Added an isolated polish-only Unity request covering active/expired EN/ES at 393x852, localized text, absence of reminder promises, unchanged domain data, geometry and reachability. Execution is pending Play Mode exit: when observed, Unity was running a real scene and automatically importing scripts; no real UI action, logout, activation or purchase was invoked by this follow-up.

TRIAL_DATE_SERVER_AUTHORITY=UTC_INSTANT
TRIAL_DATE_PRESENTATION=CLIENT_LOCALIZED
RAW_UTC_LABEL_VISIBLE=NO_BY_SOURCE
CLIENT_RECOMPUTES_TRIAL_END=NO
REMINDER_DELIVERY_NOT_IMPLEMENTED_COPY_VISIBLE=NO_BY_SOURCE
TRIAL_REMINDER_DATE_VISIBLE=NO_UNTIL_DELIVERY_IMPLEMENTED
REMINDER_AT_DOMAIN_CONTRACT_PRESERVED=YES
FOCUSED_UNITY_VALIDATION=PENDING_PLAY_MODE_EXIT
PREEXISTING_PROTECTED_FILES_MODIFIED=0/102
BACKEND_SOURCE_CHANGED=NO
COMMIT=NONE
PUSH=NONE
DEPLOY=NO

### MEMBERSHIP_01D — AVAILABLE semantic correction (2026-10-04)

The owner approved the conceptual model and requested selected-plan AVAILABLE copy and a clear distinction between commercial inclusion and implemented access. The app offer now always uses the selected eligible individual plan's localized catalog name, including when an older eligibility payload has no nested trial object. Diamond ES reads `Prueba gratuita de Diamond` and `<configured days> días de acceso a los beneficios disponibles de Diamond.` No generic Premium fallback remains in the app AVAILABLE offer. The controller is still intent-only; this change connects no trial activation or store transport. Family still has no trial action.

For commercial feature rows that are not both IMPLEMENTED and currentlyUsable, the green check is replaced with neutral `Incluido` / `Included` text. The adjacent feature name retains `Próximamente` / `Coming soon`. Commercial inclusion is therefore explicit without presenting pending functionality as active. Compact list structure and catalog data remain unchanged.

Unity was observed outside Play before editing/importing this follow-up. Current focused AVAILABLE validation: 4,256 checks PASS across 48 scenarios (Diamond/Platinum/Gold, EN/ES, eight sizes), 0 failures. Tests deliberately use a 9-day configuration and omit nested eligibility.trial to catch both duration hardcoding and legacy fallback. They verify no generic Premium copy, neutral inclusion markers, configured CTA duration, Family exclusion, text geometry and reachability. Shared client/controller checks: 142 PASS; Runtime/Editor static compilation PASS. The full state matrix was not repeated. No ACTIVE preview was opened in this follow-up.

Final preview was visually inspected and left open on App Membership / FREE_TRIAL_AVAILABLE / Diamond / YEARLY / 393x852 / ES, scrolled to the configured 7-day offer and its CTA. Final manual approval remains pending. Console has 0 errors and 10 warnings, including an existing Firebase native Future warning observed before this isolated run; this is not reported as zero warnings.

The preceding localized-date/no-reminder source corrections remain intact, and localized dates pass the focused client tests. The earlier pending ACTIVE-only visual run was not executed, honoring the instruction to stop on AVAILABLE. Domain reminderAt and all backend files are unchanged.

TRIAL_AVAILABLE_GENERIC_PREMIUM_COPY_VISIBLE=NO
TRIAL_AVAILABLE_PLAN_SOURCE=SELECTED_ELIGIBLE_PLAN
TRIAL_AVAILABLE_PLAN_NAME_VISIBLE=YES
TRIAL_AVAILABLE_DURATION_SOURCE=CONFIGURATION
TRIAL_AVAILABLE_DURATION_HARDCODED=NO
PLAN_INCLUDED_AND_RUNTIME_AVAILABLE_VISUALLY_DISTINCT=YES
COMING_SOON_CAPABILITY_PRESENTED_AS_ACTIVE=NO
RAW_UTC_LABEL_VISIBLE=NO_BY_RETAINED_SOURCE_AND_FOCUSED_DATE_TESTS
TRIAL_DATE_PRESENTATION=CLIENT_LOCALIZED
REMINDER_DELIVERY_NOT_IMPLEMENTED_COPY_VISIBLE=NO
TRIAL_REMINDER_DATE_VISIBLE=NO_UNTIL_DELIVERY_IMPLEMENTED
REMINDER_AT_DOMAIN_CONTRACT_PRESERVED=YES
REAL_TRIAL_CTA_CONNECTED=NO
REAL_TRIAL_ACTIVATION_EXECUTED=NO
REAL_DATA_MUTATIONS=0
PREEXISTING_PROTECTED_FILES_MODIFIED=0/102
COMMIT=NONE
PUSH=NONE
DEPLOY=NO
NEXT=MANUAL DIAMOND YEARLY AVAILABLE TRIAL REVIEW

### MEMBERSHIP_01D — visible pricing authority audit (2026-10-04)

Owner requires informational plan pricing even while purchase execution remains unavailable. Audit found no monetary amount, currency, localized price or offer in MembershipCatalogPublication, BillingProductMetadataResponse or client BillingProductDto. They contain platform/planKey/billingPeriod/storeProductId/active/purchasable metadata only. MembershipCatalogResponse declares APPLE_GOOGLE_STORE price authority. The shared onboarding and app use the same catalog DTO, and the isolated catalog fixture has unconfigured non-purchasable mappings. No store offer integration supplies a price. Thus source is MISSING for all eight production plan/period combinations; an actual TEST price was not queried or invented.

| Plan | Monthly production source | Yearly production source | Mock-only monthly | Mock-only yearly |
|---|---|---|---|---|
| DIAMOND | MISSING | MISSING | $10 | $80 |
| PLATINUM | MISSING | MISSING | $6 | $48 |
| GOLD | MISSING | MISSING | $3.99 | $36 |
| FRIENDS_AND_FAMILY | MISSING | MISSING | $16 | $120 |

MockShellState.MockMembershipPricing contains these demo numbers and a dollar symbol, but no ISO currency/market/store offer authority. They are not promoted to production prices. Price/savings/after-trial amount presentation and its required eight-case tests remain blocked pending an approved configuration/catalog/store source, currency and market. The missing price is separate from purchase availability. No product pricing implementation or backend schema change was made.

Visible mismatch investigation: the current Unity screenshot was the real Game View in Play, with Platinum tab underlined, Monthly selected and the Platinum trial title. The prior validation evidence was a separate MEMBERSHIP · ISOLATED Editor window with Diamond/Annual. The current real view is internally consistent; it is not evidence for the isolated Diamond fixture. No stale-response or cross-plan product defect is proven. Play was stopped without logout. A validation-only `offer` request now explicitly opens the isolated Diamond/Yearly AVAILABLE combination and records actual controller state plus displayed title, instead of merely declaring expected values. Current result: DECLARED_VISIBLE_MATCH=True, DIAMOND, YEARLY, FREE_TRIAL_AVAILABLE, 393x852, ES, isolated host. No real API request was made. This is a state-match verification, not manual visual approval.

PLAN_PRICE_SOURCE=MISSING_PRODUCTION; MOCK_ONLY_NUMBERS_EXIST
PLAN_PRICE_AUTHORITY=APPLE_GOOGLE_STORE_DECLARED; OFFER_NOT_CONFIGURED
PLAN_PRICE_VISIBLE=NO_BLOCKED_MISSING_APPROVED_PRICE
PRICE_DISPLAY_ENABLES_PURCHASE=NO
PREVIEW_DECLARED_STATE_MATCHES_VISIBLE_STATE=YES_ISOLATED
TRIAL_OFFER_PLAN_MATCHES_VISIBLE_PLAN=YES
TRIAL_OFFER_BILLING_MATCHES_SELECTED_PERIOD=YES
TRIAL_OFFER_PRICE_MATCHES_PLAN_PERIOD=UNVERIFIED_NO_PRICE
MEMBERSHIP_PRICING_PRESENTATION_TESTS=NOT_RUN_MISSING_PRICE_CONTRACT
AUTHORITATIVE_FIRST_CHARGE_DATE_VISIBLE=NO
UNAUTHORIZED_PRICE_PRESENTED_AS_GUARANTEED_CHARGE=NO
REAL_PURCHASE_FLOW_IMPLEMENTED=NO
REAL_TRIAL_ACTIVATION_EXECUTED=NO
REAL_DATA_MUTATIONS=0
COMMIT=NONE
PUSH=NONE
DEPLOY=NO
NEXT=APPROVED_PLAN_PRICE_SOURCE_CURRENCY_AND_MARKET_DEFINITION

## MEMBERSHIP_CATALOG_PRICING_AUDIT — 2026-10-04

### Evidence and read-only boundary

Current sanitized Firestore TEST inspection succeeded through the existing dominoserver administrative credential, retained exclusively in server process memory. Project teamfho-domino, database (default), healthy deployed image cuban-domino-api:d02519ff68dfcb67c739d08bddda1c8dea4834ad. Only root collection-name listing, bounded collection document GETs and exact systemConfig GETs were performed. No Player/session/authentication endpoint was invoked. No publication, transaction write, repair, price configuration, trial, entitlement mutation, server upload, restart or deployment occurred. Temporary diagnostic source/output are under Validation/Generated/Membership01D; no credentials are in the output. Initial reads stopped at the absent subscriptionPolicy document (404); the final read correctly records absence. That is not evidence of an unavailable catalog.

### Persisted structure and relationships

| Path | Current TEST contents / authority | References and lifecycle |
|---|---|---|
| membershipCatalogs/1 | Complete immutable publication; schemaVersion=1, catalogVersion=1; plans, features, planFeatures, planTranslations, featureTranslations, billingProducts, hierarchy, familyPolicy, targetPolicy, trialPolicy, supportedLocales, defaultLocale, publishedAt | Canonical commercial definitions; plans are embedded records keyed by stable key, not individual top-level plan documents. Embedded policy snapshots are published with separate versioned policy records. |
| systemConfig/membershipCatalog | publishedVersion=1 | Current-catalog pointer. Publisher only advances monotonically; identical historical publication is idempotent and differing content is rejected. |
| membershipTargetPolicies/1 | version=1, active=false; FREE/GOLD/PLATINUM/DIAMOND/FRIENDS_AND_FAMILY maps of technical capabilities and limits | Immutable commercial target matrix, also embedded in catalog. Not the runtime subscriptionPolicy and not the definition of all commercial benefits. Publishing it does not grant a Player access. |
| membershipTrialReferences/1 | version=1, product=PREMIUM_LEGACY, commercialTrialEnabled=false, familyTrialEnabled=false | Catalog-wide immutable legacy presentation/reference snapshot. No per-plan offer link, market, money or duration. Not another runtime trial-policy loader. |
| systemConfig/onboardingCatalog | publishedVersion=2 | Onboarding catalog publications can pin membershipCatalogVersion; repository checks referenced version exists. |
| systemConfig/subscriptionPolicy | ABSENT in current TEST | Runtime loader and activation service support a Spring-bound SubscriptionPolicy fallback. Code defaults are not claimed as measured live effective values. No repair needed or performed for this audit. |
| players/{P}/entitlementState/current, entitlementGrants/{grant}, entitlementAudit/{event}; promotions and trialActivationReceipts | Player-owned effective access, grant/consumption evidence and idempotency/audit history | Identified from repositories only; no Player documents were read. Not price-catalog storage. |

Root-name inventory found exactly these Membership-related collections: membershipCatalogs, membershipTargetPolicies, membershipTrialReferences. Each contained one version document, 1. No related top-level offers/pricing/subscription collection was found; this bounded statement does not assert absence of every arbitrarily named document in the entire database.

Locale support is en/es, default en. Both plan translations cover five keys and feature translations cover eight keys. Server projection selects complete requested translations or falls back to en. Current catalog has five active plans including FREE, eight commercial features, forty plan-feature relations and sixteen platform billing mappings. No price, currency or market fields occur in the audited structures. Capability implementation status is added by the pending 01C server projection/registry, not persisted as a second feature definition. It distinguishes IMPLEMENTED / PARTIALLY_IMPLEMENTED / NOT_IMPLEMENTED / FUTURE and currentlyUsable. This audit does not claim 01C has been deployed.

### Plan / period matrix

Here 'mapping only' means two existing platform metadata rows (Apple and Google), NOT a complete commercial monetary offer.

| PLAN | CATALOG ENTRY | MONTHLY OFFER | YEARLY OFFER | PRICE | CURRENCY | MARKET | TRIAL POLICY | STORE BINDING |
|---|---|---|---|---|---|---|---|---|
| DIAMOND | PRESENT, active, INDIVIDUAL | Mapping only | Mapping only | MISSING | MISSING | MISSING | Catalog-global legacy ref v1; local 01C supports explicit Diamond binding | Both platforms, both periods; IDs null, active=false |
| PLATINUM | PRESENT, active, INDIVIDUAL | Mapping only | Mapping only | MISSING | MISSING | MISSING | Same reference; local 01C supports explicit Platinum binding | Same unconfigured structure |
| GOLD | PRESENT, active, INDIVIDUAL | Mapping only | Mapping only | MISSING | MISSING | MISSING | Same reference; local 01C supports explicit Gold binding | Same unconfigured structure |
| FRIENDS_AND_FAMILY | PRESENT, active, MULTI_PLAYER | Mapping only | Mapping only | MISSING | MISSING | MISSING | Family trial disabled; 01C explicitly rejects it | Same unconfigured structure |

All eight plan/period combinations are represented as metadata, with 16 rows after platform expansion. BillingProductMetadataResponse and BillingProductDto expose platform, planKey, billingPeriod, storeProductId, active and purchasable only. No amount/currency/market exists. Validation presently limits billingProducts to 16 and makes (platform, planKey, billingPeriod) unique. Adding one row per market to that existing list without changing its contract would break these invariants.

### Money, market and currency

No reusable fiat Money type, amountMinorUnits/currencyCode value object or storefront/market price resolver was found in current backend/client product source. Wallet.coins: Long is virtual economy balance, not fiat currency and must not be reused as a price.

Recommended minimal exact value object, subject to review: nonnegative checked integer minor-unit amount (Kotlin Long / client long) paired with an explicit validated currency code. Follow the currency's minor-unit exponent; never assume two decimal places. No Float/Double monetary authority. Localized formatting is presentation only, not locale-based currency selection.

Existing geographic data is Player.countryCode, country selection validation and presentation metadata, plus preferredLocale/timeZone. These describe the Player or formatting, not a pricing market or Store storefront. Proposed minimum market key: explicitly configured ISO country/region alpha-2 commerce key with future platform storefront mapping kept at the store boundary. The owner must choose initial markets and currencies; do not infer them from the current TEST Player, language, machine location or developer location. No global/default USD price is proposed. Unsupported or missing market yields no offer rather than silently substituting a price.

### Plan / commercial offer / trial separation — proposed only

Reuse existing stable plan keys, translations, features, inclusion relations and capability registry. Add a separate versioned commercial-offer structure linked to the immutable catalog version, rather than duplicating the entire plan in each offer. A minimal compatible option is an optional commercialOffers section in a new publication version, with immutable offer identity/revision, planKey, billingPeriod, marketKey, exact money, informational availability/lifecycle state and an optional versioned trial-policy reference. Existing catalog v1 must remain readable and must not be overwritten.

Within an active publication, (planKey, billingPeriod, marketKey) resolves at most one active configured offer. Require unique resolution: missing or duplicate matches produce unavailable, never first-row selection. Include catalog/offer version in downstream quote references. Keep configured offer availability separate from purchase enablement. Platform-specific resolution is a later binding layer; if several platform offers differ, resolve the current platform/storefront rather than presenting an ambiguous merged quote.

Existing storeProductId/platform metadata is a partial future-binding foundation. Retain it and allow optional binding extensions keyed by offer/version + platform (future Apple product or Google product/base plan/offer). Do not populate provider IDs now. Model the extension point now in design; implementation is not authorized. This avoids redesigning plan benefits when store integration arrives.

Trial configuration is currently global: subscription policy contains eligibility, duration and reminder lead; the persisted catalog trial reference is legacy/presentation-only. Pending local 01C accepts individual plan/billing selection, pins catalog + target policy version, snapshots commercial benefits and effective technical access, and retains account-level one-trial enforcement. It has no selected commercial offer/market/price link and no per-plan duration configuration. A future offer may reference a separate, versioned applicable trial policy; do not treat membershipTrialReferences/1.commercialTrialEnabled=false as proof of the local 01C runtime policy or silently rewrite this legacy record. Family remains NO_CURRENT_POLICY, and existing Premium grants are neither migrated nor mapped to a new commercial tier.

Future confirmation consumes the resolved commercial/store offer directly, never calculates its own price. Precedence: valid current Store offer for the exact product/period/storefront/currency is authoritative for actual purchase presentation; approved catalog-configured price may be informational before Store integration and must be labeled as such; Mock/placeholder is never production fallback. A missing/stale Store quote must block purchase confirmation rather than guarantee the configured price. No charge date or automatic renewal is inferred from trialEndsAt.

Yearly savings may be derived as monthly x 12 minus yearly using checked exact arithmetic only when both authoritative offers share market, currency, unit exponent, authority/context and comparable terms. Hide savings for missing/incompatible offers; no hardcoded savings. Market, taxes/inclusion, supported currencies and commercial amounts remain owner decisions.

### Mock price inventory

- Assets/_Domino/AppShellMock/MockShellState.cs: MockMembershipPricing.Monthly/Annual define eight plan-period demo amounts; MockCatalog.Monthly/Annual duplicate those eight as formatted strings (16 literal plan-price entries, 8 unique plan-period values).
- Assets/_Domino/AppShellMock/MockShellView.cs: derives monthly equivalent, annual total, savings and 'Then' copy from that Mock helper; one additional hardcoded zero-trial CTA, 'Try 7 Days for $0'.
- Assets/_Domino/AppShellMock/Editor/MockPremiumValidation.cs: assertion references the zero CTA and derived prices; test evidence, not another authority.
- Validation/MembershipTrialTargetFixture.json: one separate zero-today amount with fixture USD; postTrialPrice is null. Editor/MembershipTrialContractPreview consumes this fixture. No production currency authority follows from it.

MOCK_PRICE_COUNT is therefore 8 unique nonzero plan/period quotes, represented by 16 source literals, plus 2 distinct zero-today/zero-trial fixture declarations (with a duplicate UI-test expectation). Derived equivalents/savings are not counted as new configured quotes. No values were migrated or approved by this audit.

### Exact gaps and decision

Commercial plan configuration is PARTIAL: identity, display metadata, commercial inclusion, versions and billing-period/platform metadata exist; pending code has explicit capability status and plan-bound trials. Missing pieces are versioned monetary offers, exact money/currency/market authority, unique market lookup, quote availability/precedence, optional offer-to-trial linkage and extensible store-binding resolution. No need to duplicate plan identity/translations/features, replace the technical entitlement store or create another Player membership store.

CATALOG_SCHEMA_CHANGE_REQUIRED=YES_FOR_VERSIONED_COMMERCIAL_OFFERS
BACKEND_API_CHANGE_REQUIRED=YES_FOR_OFFER_PROJECTION_AND_MARKET_RESOLUTION
FIRESTORE_CONFIGURATION_CHANGE_REQUIRED=YES_FUTURE_APPROVED_PUBLICATION_ONLY

These are proposed future changes, not changes performed. Whether offers are embedded in a new immutable publication or referenced as a separately versioned collection is a design approval choice; the current normalized structure should be retained either way. No migration of v1 prices is possible because none exist. Pricing values, initial market, currency and rollout remain unapproved.

MEMBERSHIP_CATALOG_COLLECTION=membershipCatalogs
MEMBERSHIP_CATALOG_DOCUMENT_STRUCTURE=IMMUTABLE_PUBLICATION_WITH_EMBEDDED_NORMALIZED_ARRAYS_AND_TRANSLATION_MAPS
MEMBERSHIP_CATALOG_VERSIONING=SCHEMA_1_CATALOG_1_POINTER_PUBLISHED_VERSION_1_IN_TEST
MEMBERSHIP_CATALOG_LOCALE_SUPPORT=EN_ES_DEFAULT_EN
MEMBERSHIP_CATALOG_PLAN_STORAGE=EMBEDDED_PLANS_KEYED_BY_STABLE_KEY
MEMBERSHIP_COLLECTION_RELATIONSHIPS_DOCUMENTED=YES
DIAMOND_CATALOG_ENTRY=PRESENT_TEST
PLATINUM_CATALOG_ENTRY=PRESENT_TEST
GOLD_CATALOG_ENTRY=PRESENT_TEST
FRIENDS_FAMILY_CATALOG_ENTRY=PRESENT_TEST
PLAN_COMMERCIAL_CONFIGURATION_SUPPORTED=PARTIAL
BILLING_OFFER_STRUCTURE_EXISTS=PARTIAL_METADATA_ONLY_NO_MONETARY_OFFER
BILLING_PERIODS_SUPPORTED=MONTHLY_YEARLY
EXISTING_MONEY_TYPE=NONE_FOUND_FIAT
RECOMMENDED_PRICE_REPRESENTATION=EXACT_INTEGER_MINOR_UNITS_PLUS_EXPLICIT_CURRENCY_CODE
EXISTING_MARKET_MODEL=PLAYER_COUNTRY_ONLY_NO_COMMERCIAL_MARKET
INITIAL_PRICING_MARKET=OWNER_DECISION_REQUIRED
CURRENCY_SOURCE=COMMERCIAL_OFFER_OR_STORE
PLAN_BILLING_MATRIX_AUDITED=YES_8_COMBINATIONS_16_PLATFORM_MAPPINGS
PLAN_TRIAL_POLICY_STRUCTURE=GLOBAL_LEGACY_REFERENCE_PLUS_RUNTIME_POLICY_LOCAL_01C_PLAN_BINDING_NO_OFFER_LINK
PLAN_OFFER_TRIAL_SEPARATION=YES_PROPOSED_EXISTING_PLAN_AND_POLICY_REUSED
FUTURE_STORE_BINDING_SUPPORTED_BY_STRUCTURE=PARTIAL_CURRENT_YES_PROPOSED_EXTENSION
PRICE_AUTHORITY_PRECEDENCE=VALID_STORE_OFFER_FOR_PURCHASE_THEN_APPROVED_CATALOG_INFORMATIONAL_NEVER_MOCK
MOCK_PRICES_PROMOTED_TO_PRODUCTION=NO
UNAPPROVED_PRICE_VISIBLE_IN_PRODUCTION=NO
MEMBERSHIP_OFFER_LOOKUP_KEY_DOCUMENTED=YES_PLAN_PERIOD_MARKET_WITH_VERSION_AND_STORE_CONTEXT
TRIAL_CONFIRMATION_PRICE_SOURCE=SELECTED_COMMERCIAL_OFFER_OR_STORE
YEARLY_SAVINGS_REQUIRES_SAME_MARKET_CURRENCY=YES
FIRESTORE_WRITES=0
REAL_MEMBERSHIP_MUTATIONS=0
REAL_TRIAL_MUTATIONS=0
REAL_ENTITLEMENT_MUTATIONS=0
TEST_PRICE_CONFIGURATION_CHANGED=NO
PROD_PRICE_CONFIGURATION_CHANGED=NO
PRODUCT_SOURCE_CHANGED=NO
COMMIT=NONE
PUSH=NONE
DEPLOY=NO
NEXT=MEMBERSHIP PRICING MODEL REVIEW

## MEMBERSHIP-01E — US/USD pricing implementation (2026-10-04)

Recovered the interrupted implementation and its persisted validation evidence. The following supersedes the 01D pricing proposal's pending owner decisions for initial US/USD prices; deployment and real configuration publication remain unapproved.

| Plan | Monthly USD | Yearly USD | Monthly / yearly minor units |
|---|---:|---:|---|
| Gold | 6.99 | 49.99 | 699 / 4999 |
| Platinum | 10.99 | 79.99 | 1099 / 7999 |
| Diamond | 16.99 | 119.99 | 1699 / 11999 |
| Friends & Family | 27.99 | 199.00 | 2799 / 19900 |

Storage contract: immutable `membershipCommercialOffers/{offerVersion}`, with `systemConfig/membershipCommercialOffers.publishedVersion` selecting the publication. Schema 1 contains `schemaVersion`, `offerVersion`, `catalogVersion`, `marketPolicy` (`defaultMarket`, `supportedMarkets`) and `offers`. Each offer contains `planKey`, `billingPeriod`, `market`, `amountMinorUnits` (Long), `currencyCode`, and `active`. Duplicate plan/period/market keys are rejected. The initial configuration seed has eight offers and references catalog version 1; it is neither automatically published nor used as a missing-storage fallback.

Existing plan definitions in `membershipCatalogs/1` and the 16 platform mappings remain the shared identity and future Store binding layer. Prices are not duplicated into platform mappings. Publication validates catalog references and immutable content and supports idempotent retries. Its Firestore test uses simulated storage.

The existing `/api/v1/membership/catalog` endpoint now projects `pricing`, including version, market, status, authority, offers, purchases availability and future display precedence. An explicit market query or the publication's configured default chooses the commercial market. UI locale formats the price and never chooses currency. Future storefront resolution belongs at the Store/commercial market boundary. Precedence is `STORE_PRICE_IF_AUTHORITATIVE`, then `CONFIGURED_COMMERCIAL_OFFER`; actual purchase billing authority is not implemented.

Unity obtains amounts from the backend DTO. Plan and period changes resolve the selected offer; yearly equivalents and savings use decimal display arithmetic and compatible market/currency offers. Prices remain visible while purchases are unavailable. Missing, inactive, ambiguous, invalid or mismatched offers display neutral unavailable pricing, never zero or Mock amounts. Trial presentation uses the selected offer for the post-trial price, without a first-charge date. Friends & Family has no trial CTA. Approved price literals appear only in backend configuration and isolated test/preview fixtures, not Unity product constants.

Validation evidence: `Generated/Membership01E/backend-results.json` records 48 backend tests with zero failures/errors/skips. `Generated/Membership01E/compile.log` records successful static client compilation. The recovered Unity result in `client/DominoGame/Library/Membership01E.pricing.txt` records 128 scenarios and 12,192 checks, zero failures, real requests and real writes. On recovery, the client runner was repeated successfully: 129 pricing checks and 142 shared Membership checks. The Unity result records the final preview as APP MEMBERSHIP, Diamond YEARLY, trial AVAILABLE, 393x852, ES. That window has not been visually reinspected during recovery; native app control is unavailable in this session. Manual visual review remains pending.

Future configuration rollout requires separate authorization to publish the approved seed and pointer in TEST, validate live reads, and deploy. No such operation was performed in this task. No real trial or purchase was activated.

```ini
INITIAL_PRICING_MARKET=US
INITIAL_PRICING_CURRENCY=USD
INITIAL_US_OFFER_COUNT=8
COMMERCIAL_OFFER_STORAGE=membershipCommercialOffers/{offerVersion};systemConfig/membershipCommercialOffers.publishedVersion
COMMERCIAL_OFFER_VERSIONED=YES
COMMERCIAL_OFFER_LOOKUP_KEY=PLAN+BILLING_PERIOD+MARKET
AUTHORITATIVE_PRICE_USES_FLOAT=NO
PLAN_CATALOG_DUPLICATED_FOR_PRICING=NO
PLATFORM_MAPPING_PRICE_DUPLICATION=NO
CLIENT_PRICE_SOURCE=BACKEND_COMMERCIAL_OFFER
CLIENT_FORMATS_PRICE=YES
CLIENT_OWNS_PRICE_AMOUNT=NO
LOCALE_DETERMINES_CURRENCY=NO
STORE_PRICE_CAN_OVERRIDE_DISPLAY_PRICE=YES_FUTURE
PLAN_SWITCH_UPDATES_PRICE=YES
BILLING_SWITCH_UPDATES_PRICE=YES
PLAN_PRICE_VISIBLE_WHILE_PURCHASE_UNAVAILABLE=YES
YEARLY_MONTHLY_EQUIVALENT_DERIVED=YES
YEARLY_SAVINGS_DERIVED=YES
YEARLY_SAVINGS_STORED=NO
TRIAL_AVAILABLE_POST_TRIAL_PRICE_SOURCE=SELECTED_COMMERCIAL_OFFER
ACTUAL_FIRST_CHARGE_AUTHORITY_IMPLEMENTED=NO
FRIENDS_FAMILY_TRIAL_SUPPORTED=NO_CURRENT_POLICY
FRIENDS_FAMILY_TRIAL_CTA_VISIBLE=NO
MISSING_OFFER_USES_MOCK=NO
MISSING_PRICE_MEANS_FREE=NO
MOCK_PRICES_PROMOTED_TO_PRODUCTION=NO
MEMBERSHIP_PRICING_BACKEND_TESTS=PASS_RECORDED_48
MEMBERSHIP_PRICING_CLIENT_TESTS=PASS_129
HARNESS_PRICE_MODEL_MATCHES_PRODUCTION_CONTRACT=YES
HARNESS_WRITES=0
TEST_COMMERCIAL_OFFER_WRITES=0
PROD_COMMERCIAL_OFFER_WRITES=0
BACKEND_REDEPLOYED=NO
COMMIT=NONE
PUSH=NONE
DEPLOY=NO
NEXT=MANUAL DIAMOND YEARLY PRICE + TRIAL OFFER REVIEW
```

## MEMBERSHIP-01E — Manual pricing review closure + TEST publication preflight

Owner authorization received 2026-10-04 confirms manual visual review of all four plans and both periods in **MEMBERSHIP · ISOLATED**, 393x852, ES. This is owner-supplied visual evidence, not a fresh automated screenshot. The previous PRICE_NOT_AVAILABLE diagnostic request is discarded: it was not sent/executed. Manual review is now closed as PASS, superseding the pending review above.

| Plan | Monthly USD | Yearly USD | Yearly monthly equivalent | Yearly savings | Manual result |
|---|---:|---:|---:|---|---|
| Diamond | 16.99 | 119.99 | 10.00 | 83.89 / 41% | Both periods PASS |
| Platinum | 10.99 | 79.99 | 6.67 | 51.89 / 39% | Both periods PASS |
| Gold | 6.99 | 49.99 | 4.17 | 33.89 / 40% | Both periods PASS |
| Friends & Family | 27.99 | 199.00 | 16.58 | 136.88 / 41% | Both periods PASS |

The owner confirms that plan and period switches update the displayed offer, and commercial inclusion is visually distinct from runtime availability. Included denotes commercial plan inclusion; Coming Soon on an unimplemented capability denotes its runtime status, while the purchase notice denotes unavailable purchase execution. No unimplemented capability is represented as active. Prices remain visible despite unavailable purchases. Friends & Family retains no current trial policy and no trial CTA. Equivalents and savings remain derived, not persisted.

### Exact read-only TEST preflight

Fresh administrative reads on the existing dominoserver TEST backend checked only `membershipCatalogs/1`, `systemConfig/membershipCatalog`, `membershipCommercialOffers/1`, and `systemConfig/membershipCommercialOffers` in project `teamfho-domino`, database `(default)`. The wrapper required the known deployed image `cuban-domino-api:d02519ff68dfcb67c739d08bddda1c8dea4834ad`, healthy container, matching Firebase/Google project identifiers, and a read-only credential mount. Credentials remained in remote process memory and were neither printed nor saved. No Player, trial, entitlement or wallet documents were read. Firestore calls were document GETs only.

Sanitized evidence: `Generated/Membership01EPreflight/dry-run.json`; verification and secret/privacy scan: `Generated/Membership01EPreflight/validation.json`. These paths are already Git-ignored by the repository's Generated convention. The proposed eight amounts were checked against the existing backend seed; plan references, canonical periods and both platform mapping references passed.

Catalog version 1 and its pointer exist. The commercial publication version 1 and its pointer are both absent. The current catalog contains all four referenced plans and 16 inactive platform mappings, with no Store product IDs. Future publication targets **two documents**, not eight individual offer documents:

| Target | Current state | Planned future action |
|---|---|---|
| `membershipCommercialOffers/1` | MISSING | CREATE complete schema-1 publication, offerVersion=1, catalogVersion=1 |
| `systemConfig/membershipCommercialOffers` | MISSING | SET `{publishedVersion: 1}` in the same transaction |
| `membershipCatalogs/1` | PRESENT | No mutation |
| `systemConfig/membershipCatalog` | publishedVersion=1 | No mutation |

Each deterministic key identifies one embedded element in the shared `offers` array. Its actual document ID is `1` for every row; the key is a logical lookup key, not a new persisted ID field or subcollection.

| Logical offer key | Actual storage document | Minor units | Currency | Active | Existing |
|---|---|---:|---|---|---|
| DIAMOND\|MONTHLY\|US | membershipCommercialOffers/1 | 1699 | USD | true | MISSING |
| DIAMOND\|YEARLY\|US | membershipCommercialOffers/1 | 11999 | USD | true | MISSING |
| PLATINUM\|MONTHLY\|US | membershipCommercialOffers/1 | 1099 | USD | true | MISSING |
| PLATINUM\|YEARLY\|US | membershipCommercialOffers/1 | 7999 | USD | true | MISSING |
| GOLD\|MONTHLY\|US | membershipCommercialOffers/1 | 699 | USD | true | MISSING |
| GOLD\|YEARLY\|US | membershipCommercialOffers/1 | 4999 | USD | true | MISSING |
| FRIENDS_AND_FAMILY\|MONTHLY\|US | membershipCommercialOffers/1 | 2799 | USD | true | MISSING |
| FRIENDS_AND_FAMILY\|YEARLY\|US | membershipCommercialOffers/1 | 19900 | USD | true | MISSING |

Schema remains the already approved `MembershipCommercialOfferPublication`: `schemaVersion=1`, `offerVersion=1`, `catalogVersion=1`, `marketPolicy={defaultMarket:US,supportedMarkets:[US]}`, and eight offers with `planKey`, `billingPeriod`, `market`, `amountMinorUnits`, `currencyCode`, `active`. Persisted array order must match `MembershipCommercialOfferSeed.approvedUs()` exactly: GOLD monthly/yearly, PLATINUM monthly/yearly, DIAMOND monthly/yearly, FRIENDS_AND_FAMILY monthly/yearly. The dry-run payload preserves that order. No amount uses float/double and locale never selects currency.

### Future execution mechanism and rollback

After explicit publication authorization, use the existing `FirestoreMembershipCommercialOfferRepository.publish(MembershipCommercialOfferSeed.approvedUs())` through a narrowly scoped administrative wrapper, with no Spring startup and no application deployment. Recheck TEST identity/health and the exact documents immediately before execution. Compare the dry-run payload to the compiled seed, validate catalog references, and abort if the immutable publication differs or the pointer is invalid. If a newer publication has appeared, stop for a new preflight rather than changing the approved activation target.

The existing publisher reads the referenced catalog, target publication and pointer before buffering writes. Its Firestore transaction atomically creates the missing immutable publication and advances the pointer. An identical second execution performs zero semantic changes; differing existing immutable content aborts. This behavior already has the recorded simulated Firestore idempotency test. There are no price fields added to platform mappings and no Store, trial, Player, entitlement or wallet writes.

Immediately before any future write, capture raw contents, existence and update times of both target documents to an access-restricted temporary directory **outside the repository**, without credential/token material, and record the current catalog hash. The current sanitized preflight records absence of both targets but does not replace that execution-time backup. Keep the execution's resulting versions/update times for rollback preconditions.

Rollback requires explicit owner authorization. In one transaction, first verify that both targets still match the executed publication and pointer (including captured post-write update times); abort if another writer has changed either document. Restore the prior pointer or remove it if it was absent. Remove the newly created version document only if the same checks prove it is still this execution's unchanged document and the restored pointer cannot reference it. If the version existed identically before execution, leave it intact. Never overwrite an immutable version or touch the catalog/platform mappings. Verify the restored logical state and remove private backup material according to the owner's retention decision. No rollback backup containing raw remote data was created in Git, and no rollback was executed.

Future runtime expectation: the Membership read API resolves offers from TEST Firestore and Unity renders those amounts without Mock fallback. **Publication alone does not deploy the local 01E API implementation.** The observed deployed image predates this local implementation; live API exposure must be verified against an explicitly approved compatible backend rollout. Backend deployment remains outside this preflight's authorization. There is no claim that real Game View prices already use TEST offers.

```ini
MEMBERSHIP_01E_MANUAL_PRICING_REVIEW=PASS
DIAMOND_MONTHLY_VISUAL=PASS
DIAMOND_YEARLY_VISUAL=PASS
PLATINUM_MONTHLY_VISUAL=PASS
PLATINUM_YEARLY_VISUAL=PASS
GOLD_MONTHLY_VISUAL=PASS
GOLD_YEARLY_VISUAL=PASS
FRIENDS_FAMILY_MONTHLY_VISUAL=PASS
FRIENDS_FAMILY_YEARLY_VISUAL=PASS
PLAN_SWITCH_UPDATES_PRICE=PASS_MANUAL
BILLING_SWITCH_UPDATES_PRICE=PASS_MANUAL
PLAN_INCLUDED_AND_RUNTIME_AVAILABLE_VISUALLY_DISTINCT=PASS_MANUAL
UNIMPLEMENTED_CAPABILITY_PRESENTED_AS_ACTIVE=NO
PLAN_PRICE_VISIBLE_WHILE_PURCHASE_UNAVAILABLE=YES
REAL_PURCHASE_FLOW_IMPLEMENTED=NO
FRIENDS_FAMILY_TRIAL_SUPPORTED=NO_CURRENT_POLICY
FRIENDS_FAMILY_TRIAL_CTA_VISIBLE=NO
YEARLY_MONTHLY_EQUIVALENT_DERIVED=YES
YEARLY_SAVINGS_DERIVED=YES
YEARLY_SAVINGS_STORED=NO
INITIAL_PRICING_MARKET=US
INITIAL_PRICING_CURRENCY=USD
INITIAL_US_OFFER_COUNT=8
AUTHORITATIVE_PRICE_USES_FLOAT=NO
TEST_OFFER_PUBLICATION_DRY_RUN=YES
TEST_COMMERCIAL_OFFER_TARGET_PATH=membershipCommercialOffers/1
TEST_COMMERCIAL_OFFER_VERSION=1
TEST_COMMERCIAL_OFFER_POINTER=systemConfig/membershipCommercialOffers.publishedVersion
TEST_EXISTING_OFFER_CONFLICTS=0
TEST_EXISTING_OFFER_CLASSIFICATION=8_MISSING_0_IDENTICAL_0_DIFFERENT
TEST_IMMUTABLE_DOCUMENT_CONFLICTS=0
TEST_OFFER_PLAN_REFERENCES_VALID=YES
TEST_OFFER_BILLING_PERIODS_VALID=YES
TEST_OFFER_MARKET=US
TEST_OFFER_CURRENCY=USD
PLATFORM_MAPPING_MUTATIONS=0
STORE_PRODUCT_MUTATIONS=0
REAL_TRIAL_ACTIVATION_EXECUTED=NO
TRIAL_DOCUMENT_MUTATIONS=0
PLAYER_MUTATIONS=0
ENTITLEMENT_MUTATIONS=0
WALLET_MUTATIONS=0
TEST_OFFER_PUBLICATION_IDEMPOTENT_DESIGN=YES
TEST_OFFER_ROLLBACK_PLAN_READY=YES
POST_PUBLICATION_EXPECTED_PRICE_SOURCE=TEST_FIRESTORE_COMMERCIAL_OFFER
FIRESTORE_WRITES=0
TEST_COMMERCIAL_OFFER_WRITES=0
PROD_COMMERCIAL_OFFER_WRITES=0
PRODUCT_SOURCE_CHANGED=NO
DRY_RUN_SECRET_SCAN=PASS
SECRET_SCAN=PASS
PRIVACY_SCAN=PASS
COMMIT=NONE
PUSH=NONE
DEPLOY=NO
NEXT=TEST COMMERCIAL OFFER PUBLICATION AUTHORIZATION
```

Preflight complete. STOP: the eight offers have not been published. Await explicit owner publication authorization.

## MEMBERSHIP_01E_TEST_COMMERCIAL_OFFER_PUBLICATION

Executed 2026-10-04 under explicit owner authorization for TEST publication only. This section supersedes the preceding preflight's unpublished state. Project `teamfho-domino`, database `(default)`, publication `membershipCommercialOffers/1`, pointer `systemConfig/membershipCommercialOffers`, catalog version 1.

The exact pre-write read-only preflight was repeated in the execution wrapper and compared to every substantive field of the approved dry-run (excluding its observation time). Catalog content hashes/update times, catalog pointer, target absence, eight missing offers, zero conflicts, valid plans/periods and the 16 inactive platform mappings all matched. A separate compiled Java plan also validated the seed against the approved payload and real catalog immediately before the write.

Used the already implemented `FirestoreMembershipCommercialOfferRepository.publish(MembershipCommercialOfferSeed.approvedUs())`, not a separate database writer. The local build was packaged solely for a disposable administrative invocation; no backend source change, image build, deployment, application startup, API container replacement or restart occurred. Local runtime JAR SHA256: `a4107cdf09f615fc10232071b6f3e518c83b21a1d4e73de33f602643a34971cd`. The wrapper ran with the existing known TEST image, read-only runtime and credential mounts, constrained CPU/memory and capabilities, and no Spring startup.

Immediately before mutation, raw snapshots of the exact target/protected configuration documents, including absence and update times, were saved on dominoserver in restricted `/tmp/membership01e-rollback-at87_e9h/before.json` (directory 0700, file 0600), outside Git. Post-write target update times are retained in the same restricted directory. No credentials, tokens or Player documents are present in that backup. Rollback was not required or executed; the previously documented conditional strategy remains available.

First execution atomically created the complete publication and its pointer: **2 documents created, 0 updated, 0 deleted**. Eight offers are embedded in one immutable publication, all active, all US/USD, with exact integer minor-unit values and offer/catalog version 1. Immediate SDK readback compared complete publication content to the approved seed and verified the pointer. Typed repository readback also equaled the seed. Catalog, catalog pointer, target policy and trial reference content/update times were unchanged.

Only after successful first readback, the existing publisher ran a second time. Complete content and update times of both target documents remained unchanged: **0 created, 0 updated, 0 deleted, 0 semantic changes**. Protected configuration documents were checked again and remained unchanged. A subsequent independent Firestore REST GET readback classified all eight offers and the whole publication as IDENTICAL, pointer version 1; no missing, unexpected or duplicate offer keys occurred.

| Offer | Published minor units | Currency / market | Active | Readback |
|---|---:|---|---|---|
| Diamond MONTHLY | 1699 | USD / US | true | PASS |
| Diamond YEARLY | 11999 | USD / US | true | PASS |
| Platinum MONTHLY | 1099 | USD / US | true | PASS |
| Platinum YEARLY | 7999 | USD / US | true | PASS |
| Gold MONTHLY | 699 | USD / US | true | PASS |
| Gold YEARLY | 4999 | USD / US | true | PASS |
| Friends & Family MONTHLY | 2799 | USD / US | true | PASS |
| Friends & Family YEARLY | 19900 | USD / US | true | PASS |

Sanitized evidence is in `Generated/Membership01EPublication/result.json`, `readback.json`, `verification.json`, `plan-output.txt`, and `publish-output.txt`, under the repository's existing Git-ignored Generated convention. Scans of the retained evidence passed for credentials/tokens/private Player data. Product-source hashes captured before execution were unchanged afterward. No domain endpoints were called; writes were confined to the two approved commercial configuration documents. No Player, membership state, trial, entitlement, wallet, platform mapping or Store product changes occurred. PROD was not accessed. Real Game View pricing was not tested; compatible backend checkpoint/deployment is a separate future task.

```ini
ENVIRONMENT=TEST
TARGET=membershipCommercialOffers/1
COMMERCIAL_OFFER_VERSION=1
MEMBERSHIP_CATALOG_VERSION=1
PRE_WRITE_OFFER_COUNT_EXISTING=0
PRE_WRITE_CONFLICTS=0
PRE_WRITE_PLAN_REFERENCES_VALID=YES
PRE_WRITE_BILLING_PERIODS_VALID=YES
APPROVED_OFFER_COUNT=8
DIAMOND_MONTHLY=1699_USD_MINOR_UNITS
DIAMOND_YEARLY=11999_USD_MINOR_UNITS
PLATINUM_MONTHLY=1099_USD_MINOR_UNITS
PLATINUM_YEARLY=7999_USD_MINOR_UNITS
GOLD_MONTHLY=699_USD_MINOR_UNITS
GOLD_YEARLY=4999_USD_MINOR_UNITS
FRIENDS_FAMILY_MONTHLY=2799_USD_MINOR_UNITS
FRIENDS_FAMILY_YEARLY=19900_USD_MINOR_UNITS
PRICE_STORAGE_MINOR_UNITS=YES
PRICE_STORAGE_FLOAT=NO
MARKET=US
CURRENCY=USD
PUBLICATION_ATOMIC=YES
FIRESTORE_DOCUMENTS_CREATED=2
FIRESTORE_DOCUMENTS_UPDATED=0
FIRESTORE_DOCUMENTS_DELETED=0
POST_WRITE_OFFER_COUNT=8
POST_WRITE_OFFER_MISMATCHES=0
DIAMOND_MONTHLY_PRESENT=YES
DIAMOND_YEARLY_PRESENT=YES
PLATINUM_MONTHLY_PRESENT=YES
PLATINUM_YEARLY_PRESENT=YES
GOLD_MONTHLY_PRESENT=YES
GOLD_YEARLY_PRESENT=YES
FRIENDS_FAMILY_MONTHLY_PRESENT=YES
FRIENDS_FAMILY_YEARLY_PRESENT=YES
MISSING_APPROVED_OFFERS=0
UNEXPECTED_OFFERS=0
DUPLICATE_OFFER_KEYS=0
SECOND_RUN=PASS
SECOND_RUN_DOCUMENTS_CREATED=0
SECOND_RUN_DOCUMENTS_UPDATED=0
SECOND_RUN_DOCUMENTS_DELETED=0
SECOND_RUN_SEMANTIC_CHANGES=0
MEMBERSHIP_CATALOG_MUTATIONS=0
MEMBERSHIP_TARGET_POLICY_MUTATIONS=0
MEMBERSHIP_TRIAL_REFERENCE_MUTATIONS=0
PLATFORM_MAPPING_MUTATIONS=0
APPLE_STORE_PRODUCT_MUTATIONS=0
GOOGLE_PLAY_PRODUCT_MUTATIONS=0
STORE_OFFER_MUTATIONS=0
PLAYER_MUTATIONS=0
PLAYER_MEMBERSHIP_MUTATIONS=0
TRIAL_ACTIVATION_CALLS=0
TRIAL_DOCUMENT_MUTATIONS=0
ENTITLEMENT_MUTATIONS=0
WALLET_MUTATIONS=0
ROLLBACK_REQUIRED=NO
ROLLBACK_EXECUTED=NO
BACKEND_SOURCE_CHANGED=NO
BACKEND_REDEPLOYED=NO
REAL_GAME_VIEW_PRICING_VALIDATION=NOT_EXECUTED
PRODUCT_SOURCE_CHANGED=NO
SECRET_SCAN=PASS
PRIVACY_SCAN=PASS
COMMIT=NONE
PUSH=NONE
DEPLOY=NO
PROD_DEPLOYMENT=NO
MEMBERSHIP_01E_TEST_OFFER_PUBLICATION_SUCCESS=YES
NEXT=MEMBERSHIP-01E FINAL REVIEW + CHECKPOINT
```

STOP: publication, readback and second-run idempotency verification are complete. No checkpoint or backend deployment was performed.

## MEMBERSHIP-01 — Final review and checkpoint gates (2026-10-04)

Authorized base: `main` at `ce5e88ad81e8bd5460ddbbab641f3adfc536b550`. MEMBERSHIP-01A through 01E are reviewed together as one checkpoint. This section supersedes earlier pending checkpoint instructions; prior validation/publication observations remain historical evidence. Commit message: `feat: add shared membership and commercial offers`.

Final reviewed behavior: onboarding and app share `SharedMembershipExperience`; app Membership opens from Menu and returns to Menu, without onboarding skip controls. Onboarding Not now remains available. Backend owns membership, trial and entitlements; PlayerService remains the client's single commercial-state owner. Gold, Platinum, Diamond and Friends & Family are commercial plans; legacy Premium is not a new plan tab and is not remapped. Commercial inclusion and implementation availability remain distinct. New trials require selected individual plan and canonical period; ANNUAL input normalizes to YEARLY. Trial timelines remain server computed (default duration 7 days, reminder lead 2 days); UI localizes server dates without calculating new timestamps or promising reminder delivery/Store charge dates. Trial consumption remains account-wide. App trial action remains intent-only; opening, login, bootstrap and onboarding do not automatically grant trials.

Pricing remains the reviewed eight US/USD minor-unit offers keyed by plan/period/market, with unavailable pricing rather than Mock/zero fallback. Selected-plan and period switches change displayed amounts; informational equivalents and savings are derived. All eight manual pricing reviews are owner-confirmed PASS. Retained TEST publication evidence has eight exact offers, zero mismatches, successful no-change second run and no rollback required. **This checkpoint performs zero Firestore writes** and no live trial/purchase/UI pricing validation.

### Retained validation, without overlapping totals

| Evidence | Retained result |
|---|---|
| 01C full root backend test run | 904 passed, 24 skipped, 0 failures (928 total, before two final compatibility tests) |
| 01C final focused PlanBoundTrialTests | 9 passed, including the two later compatibility cases; not another full-suite count |
| 01E pricing/catalog backend | 48 passed, 0 failures/errors/skips; overlaps prior catalog tests and is not added to the full-run count |
| Shared Membership client | 142 checks PASS |
| 01E pricing client | 129 checks PASS |
| Isolated trial confirmation fixture | 38 checks PASS |
| 01D Unity state matrix | 15,812 checks / 144 scenarios PASS |
| 01D AVAILABLE follow-up | 4,256 checks / 48 scenarios PASS |
| 01E Unity pricing | 12,192 checks / 128 scenarios PASS |
| Onboarding Membership / full flow | 76 / 407 PASS; bootstrap 31 and shell 93 PASS retained |
| Player UI Home / Menu / Profile | 222 / 60 / 80 PASS retained |
| Profile edit / Alias availability / Guest post-link | 42 / 20 / 69 PASS retained |
| Membership localization/responsiveness | EN/ES PASS, eight sizes per locale, horizontal overflow 0, text clipping 0 |

Durable source has not been edited during checkpoint review, so relevant tests were not repeated. To verify that the commit does not depend on excluded historical files, the **actual Git index** was materialized as a source-only snapshot and compiled against the existing Unity dependency assemblies. Both runtime and Editor compilation passed. This is a staged-source compilation gate, not a repeated test run or real Unity Game View claim. Backend CI is required after push and supplies the committed-tree backend validation.

### Scope classification and staged review

All 191 pending files were classified individually in `Generated/Membership01Checkpoint/classification.json`: 102 PROTECTED, 39 HISTORICAL, 11 TEMPORARY_VALIDATION, 7 MEMBERSHIP_PRODUCT_BACKEND, 15 MEMBERSHIP_PRODUCT_CLIENT, 11 MEMBERSHIP_TEST, 1 MEMBERSHIP_LOCALIZATION, 4 MEMBERSHIP_VALIDATION_DURABLE and 1 MEMBERSHIP_REPORT. UNRELATED has 0 and unclassified has 0. The 39 staged files are exclusively Membership scope; the remaining 152 pending files stay untouched. Protected baseline hashes were rechecked: 0/102 modified.

The two Editor preview classes plus their metadata are retained as durable, reusable isolated Membership validation infrastructure: state/plan/period/locale/size selectors, contract fixtures, geometry checks and menu entry points; no production selector dependency or real activation transport is added. Library request/result files, screenshots, generated output, recovery files, temporary observers, private rollback snapshots and credential/configuration files are excluded.

Exact staged Membership manifest:

- Client DTOs: `Infrastructure/Api/OnboardingDtos.cs`, `Infrastructure/Api/PlayerBootstrapDtos.cs`.
- Client app hosts: `UI/AppShell/ProductionAppShell.cs`, `ProductionAuthHost.cs`, `ProductionMembershipView.cs`.
- Shared/app components and matching `.meta`: `UI/AppShell/AppMembershipController.cs`, `AppMembershipPage.cs`, `MembershipPresentation.cs`, `MembershipPricePresentation.cs`, `SharedMembershipExperience.cs`.
- Durable Editor validation and matching `.meta`: `UI/AppShell/Editor/SharedMembershipPreview.cs`, `MembershipTrialContractPreview.cs`.
- Client validation: `client/Validation/RunSharedMembershipTests.ps1`, `SharedMembershipTests.cs`, `MembershipPricingTests.cs`, `CheckMembershipTrialTarget.ps1`, `MembershipTrialTargetFixture.json`, `MembershipCatalogFixture.json`, and this report.
- Backend catalog: `catalog/MembershipCatalog.kt`, `MembershipCatalogHttp.kt`, `MembershipCommercialOffers.kt`.
- Backend trial/entitlements: `entitlement/EntitlementModels.kt`, `PlanBoundTrial.kt`, `TrialActivation.kt`, `TrialActivationHttp.kt`.
- Backend tests: `catalog/MembershipCommercialOfferTests.kt`, `MembershipCommercialOfferFirestoreTests.kt`; `entitlement/TrialActivationTests.kt`, `TrialActivationHttpTests.kt`, `TrialActivationFirestoreTests.kt`, `PlanBoundTrialTests.kt`.

Client code paths above are rooted at `client/DominoGame/Assets/_Domino/Scripts/`; backend source/test paths use the existing `server/domino/src/main|test/kotlin/com/teamfho/domino/` roots. The exact 39 full paths are retained in `Generated/Membership01Checkpoint/scope.json`.

Staged source/diffs were reviewed for shared rendering, commercial benefits, bound-trial/timeline contracts, versioned offer resolution, exact pricing, legacy receipt compatibility and no automatic trial grants. Staged scope matches the reviewed allowlist exactly. Whitespace check, secret indicator scan, privacy indicator scan and UTF-8/mojibake checks all pass; no unrelated/protected/temporary file is staged. The scope and protected-file inventories remain available as local validation evidence rather than being committed.

```ini
MEMBERSHIP_01A=PASS
MEMBERSHIP_01B=PASS
MEMBERSHIP_01C=PASS
MEMBERSHIP_01D=PASS
MEMBERSHIP_01E=PASS
BOOTSTRAP_AUTO_TRIAL_GRANT=NO
LOGIN_AUTO_TRIAL_GRANT=NO
ONBOARDING_AUTO_TRIAL_GRANT=NO
MEMBERSHIP_OPEN_AUTO_TRIAL_GRANT=NO
SECOND_TRIAL_PREVENTION_PRESERVED=YES
ONBOARDING_MEMBERSHIP_REGRESSION=PASS_RETAINED
MENU_MEMBERSHIP_REGRESSION=PASS_RETAINED
PLAYER_UI_01_REGRESSION=PASS_RETAINED
PLAYER_PROFILE_EDIT_01_REGRESSION=PASS_RETAINED
PLAYER_IDENTITY_01_REGRESSION=PASS_RETAINED
GUEST_ACCOUNT_01_REGRESSION=PASS_RETAINED
CHECKPOINT_TESTS_REPEATED=NO
CHECKPOINT_INDEX_RUNTIME_COMPILATION=PASS
CHECKPOINT_INDEX_EDITOR_COMPILATION=PASS
CHECKPOINT_FIRESTORE_WRITES=0
CHECKPOINT_MEMBERSHIP_CATALOG_MUTATIONS=0
CHECKPOINT_TARGET_POLICY_MUTATIONS=0
CHECKPOINT_TRIAL_REFERENCE_MUTATIONS=0
PLATFORM_MAPPING_MUTATIONS=0
HARNESS_PLAYER_WRITES=0
HARNESS_TRIAL_WRITES=0
HARNESS_ENTITLEMENT_WRITES=0
MEMBERSHIP_STATE_SELECTOR_PRODUCTION_VISIBLE=NO
PRODUCTION_DEPENDS_ON_MEMBERSHIP_STATE_SELECTOR=NO
FILES_STAGED=39
UNCLASSIFIED_PENDING_FILES=0
PREEXISTING_PROTECTED_FILES_MODIFIED=0/102
UNRELATED_FILES_STAGED=0
TEMPORARY_FILES_STAGED=0
PROTECTED_FILES_STAGED=0
UNCLASSIFIED_STAGED_FILES=0
SHARED_MEMBERSHIP_STAGED_CONTRACT=PASS
COMMERCIAL_PLAN_STAGED_CONTRACT=PASS
TRIAL_PLAN_STAGED_CONTRACT=PASS
TRIAL_TIMELINE_STAGED_CONTRACT=PASS
COMMERCIAL_OFFER_STAGED_CONTRACT=PASS
PRICING_STAGED_CONTRACT=PASS
LEGACY_COMPATIBILITY_STAGED_CONTRACT=PASS
NO_AUTO_TRIAL_STAGED_CONTRACT=PASS
SECRET_SCAN=PASS
STAGED_SECRET_SCAN=PASS
PRIVACY_SCAN=PASS
STAGED_PRIVACY_SCAN=PASS
MOJIBAKE_MARKERS_STAGED=0
BACKEND_REDEPLOYED=NO
DEPLOY=NO
PROD_DEPLOYMENT=NO
REAL_TRIAL_ACTIVATION_EXECUTED=NO
REAL_PURCHASE_EXECUTED=NO
```

The commit SHA cannot be embedded in its own content. Post-commit push, remote SHA and terminal CI results are captured in the local `Generated/Membership01Checkpoint` evidence and the final task response. The deploy candidate is the resulting Membership checkpoint commit SHA, not an instruction to deploy. Stop after push/terminal CI; TEST deployment and real pricing validation require a later explicit task.

## MEMBERSHIP_01_CI_FAILURE_REVIEW

The original Backend CI run [37246116647](https://github.com/fredyho292018/domino/actions/runs/37246116647) for checkpoint `488b890090186e92b808faff337ef7321265a2d0` remains **FAILED**: `:emulatorTest`, 60 tests, 1 failure in `EntitlementEmulatorTests.concurrentActivation`. No remote CI rerun, commit, push or deployment was performed during this review. The checkpoint was neither amended nor rewritten.

### Proven causal chain and fixture audit

The exact test was run first, unchanged, against a fresh local Firestore emulator and reproduced the failure: `ExecutionException` wraps `OnboardingFailure: REQUEST_INVALID`. Original CI logs expose the wrapper and OnboardingFailure frames; the local JUnit XML supplies the sanitized code and deeper causal chain. Evidence is retained under `Generated/Membership01CIFailure/`: `ci-original-result.json`, `ci-failure-sanitized.txt`, `before/test-results/`, and `local-causal-chain-sanitized.txt`.

The throw site is `onboardingCheck` in `OnboardingProgressModels.kt:8`, called by the request precondition in `PlanBoundTrial.kt:38`, via `TrialActivation.kt:101` and the transaction callback in `OnboardingProgressRepository.kt:19`. `PlanBoundTrials.create` requires a nonblank plan and canonical billing period. The old test supplied only request ID and policy version; both new fields were absent. This fails before catalog lookup. The exception name comes from a shared validator and transaction path; incomplete onboarding is not the cause, and the repository preserves the original OnboardingFailure code.

| Fixture requirement | Before | After |
| --- | --- | --- |
| Player / foundation | ACTIVE GUEST; foundation created | Preserved |
| Preferences / domino profile | Created by foundation | Preserved |
| Onboarding | NOT_STARTED, revision 0, catalogVersion null; completion is not required for explicit activation | Preserved |
| Alias global config | READY, normalizationVersion 1 | Preserved |
| Existing trial / grants | Absent; eligible through default subscription policy version 1 | Preserved before activation |
| Subscription policy | Seven-day trial, two-day reminder, linked identity not required | Preserved |
| Plan / billing period | Missing | DIAMOND / YEARLY, canonical enum |
| Membership catalog / pointer | Missing in isolated fixture | Canonical catalog and pointer published to emulator |
| Target policy / trial reference | Missing with catalog | Created by canonical emulator seed |
| Commercial offers / pricing | Absent | Absent; not required by this activation path |

After the request validation, the production path requires the Membership catalog and pointer plus its target-policy data. Without the seed, the test would encounter a dependency failure next. `FirestoreMembershipCatalogRepository.publish(MembershipCatalogSeed.canonical())` supplies those emulator-only documents. This does not publish commercial offers or touch real TEST data. The activation path does not read store pricing or commercial-offer documents. The failure is classified **STALE_TEST_FIXTURE**, not a concurrency regression or a production validator defect.

### Minimal correction and validation

Only `server/domino/src/test/kotlin/com/teamfho/domino/entitlement/EntitlementEmulatorTests.kt` was changed in source. It seeds the canonical Membership catalog and sends DIAMOND/YEARLY. The six workers and twelve activation attempts remain. Assertions require exactly one ACTIVATED and eleven ALREADY_ACTIVE results; one persisted grant, one audit record, the consumed promotion marker, and matching bound plan/end time across all results. A request for GOLD/MONTHLY after expiry is rejected with TRIAL_ALREADY_CONSUMED and leaves entitlement state unchanged. The existing `bootstrapNoTrialWrites` test remains unchanged and passed, preserving the bootstrap no-auto-grant check. Production validation and eligibility rules were not weakened.

| Local run | Total | Passed | Failed | Errors | Skipped |
| --- | ---: | ---: | ---: | ---: | ---: |
| Exact causal test, before correction | 1 | 0 | 1 | 0 | 0 |
| Exact causal test, after correction | 1 | 1 | 0 | 0 | 0 |
| First full suite, without CI Redis flags | 60 | 59 | 0 | 0 | 1 |
| Final full suite, with CI Redis flags and disposable Redis | 60 | 60 | 0 | 0 | 0 |

The first full run omitted the distributed Social emulator test because its Redis environment flag was absent. That result is retained accurately. The final full run enabled the workflow's Redis flags and used an isolated Redis container with an ownership label and no persistent volume. Both its owned Redis container and owned Firestore emulator were removed/stopped successfully. JUnit results for all four runs and `results.json` remain in the generated evidence folder. These are local results; they do not convert the original remote CI failure to a pass.

Scope checks: production source files changed 0; test files changed 1; durable report files changed 1. Protected baseline hashes remain unchanged, 0/102 modified. Existing historical pending work was preserved. Scoped whitespace checks and sanitized evidence review passed; unrelated JVM/deprecation warnings were not repaired. No files are staged. No real Firestore, Player, trial, entitlement, pricing or catalog mutations occurred.

```ini
CHECKPOINT_SHA=488b890090186e92b808faff337ef7321265a2d0
CI_RUN=37246116647
CI_TASK=:emulatorTest
CI_TESTS_TOTAL=60
CI_TESTS_FAILED=1
FAILING_TEST=EntitlementEmulatorTests.concurrentActivation
ONBOARDING_FAILURE_CODE=REQUEST_INVALID
ONBOARDING_FAILURE_MESSAGE_SANITIZED=OnboardingFailure: REQUEST_INVALID
ONBOARDING_FAILURE_THROW_SITE=OnboardingProgressModels.kt:8 via PlanBoundTrial.kt:38
CI_FAILURE_REPRODUCED_LOCALLY=YES
CONCURRENT_ACTIVATION_FIXTURE_STATE=STALE_REQUEST_AND_MISSING_MEMBERSHIP_CATALOG
FIXTURE_MATCHES_NEW_TRIAL_CONTRACT=NO_BEFORE;YES_AFTER
MISSING_FIXTURE_REQUIREMENTS=plan;billingPeriod;catalog;catalogPointer;targetPolicy;trialReference
ONBOARDING_FAILURE_CLASSIFICATION=STALE_TEST_FIXTURE
EMULATOR_GLOBAL_CONFIG_COMPLETE=YES_AFTER_FIX
EMULATOR_COMMERCIAL_OFFER_REQUIRED=NO
EMULATOR_COMMERCIAL_OFFER_PRESENT=NO
FIXTURE_BILLING_PERIOD_CANONICAL=YES_YEARLY
CONCURRENT_ACTIVATION_SINGLE_WINNER_CONTRACT_PRESERVED=YES
TEST_FIXTURE_FIX_REQUIRED=YES_APPLIED
PRODUCTION_SOURCE_FIX_REQUIRED=NO
EXACT_CAUSAL_TEST=PASS_1_OF_1
EMULATOR_TEST_AFTER_FIX=PASS_60_OF_60;FAILED_0;ERRORS_0;SKIPPED_0
MEMBERSHIP_EMULATOR_CONTRACT=PASS
PRODUCTION_SOURCE_FILES_CHANGED=0
TEST_FILES_CHANGED=1
UNRELATED_SOURCE_CHANGED=NO
PREEXISTING_PROTECTED_FILES_MODIFIED=0/102
REAL_TEST_FIRESTORE_MUTATIONS=0
REAL_PLAYER_MUTATIONS=0
REAL_TRIAL_ACTIVATIONS=0
REAL_ENTITLEMENT_MUTATIONS=0
COMMIT=NONE
PUSH=NONE
DEPLOY=NO
NEXT=MEMBERSHIP-01 CI FIX REVIEW
```

## MEMBERSHIP_01_CI_FIX_CHECKPOINT

The CI-fix checkpoint is based on `488b890090186e92b808faff337ef7321265a2d0` on main and contains only the causal emulator test correction and this durable report. The historical CI run `37246116647` remains FAILED and is not rerun. The root cause remains STALE_TEST_FIXTURE / REQUEST_INVALID; production source and validation are unchanged.

The reviewed fixture supplies DIAMOND, canonical YEARLY, catalog, catalog pointer, target policy and trial reference through the canonical emulator publication. No commercial-offer fixture was added. The original six workers / twelve requests and single-winner assertion remain, with assertions for eleven already-active responses, one grant, one audit record, consumed-trial state, consistent bound-trial results and rejection of a second trial after expiry. Duplicate activation grants: 0 in the successful local test.

The source correction has not changed since the retained exact-test PASS 1/1 and complete emulator-suite PASS 60/60 (0 failures, errors or skipped tests). These tests are not repeated for checkpoint preparation. The new remote Backend CI will validate the committed tree.

All 154 pending files were classified: 1 causal test, 1 Membership report, 102 protected, 39 historical and 11 temporary validation files. The 152 preexisting excluded files match their recorded hashes, including 0/102 protected modifications. Only the causal test and report are allowed into the index; product implementation, unrelated historical files and generated evidence are excluded. Local classification and staged-review evidence are saved under `Generated/Membership01CIFixCheckpoint/`.

```ini
HISTORICAL_FAILED_CI_PRESERVED=YES
HISTORICAL_CI_RERUN=NO
TEST_FIX_SCOPE_ONLY=YES
FIXTURE_PLAN_PRESENT=YES
FIXTURE_BILLING_PERIOD_PRESENT=YES
FIXTURE_CATALOG_PRESENT=YES
FIXTURE_CATALOG_POINTER_PRESENT=YES
FIXTURE_TARGET_POLICY_PRESENT=YES
FIXTURE_TRIAL_REFERENCE_PRESENT=YES
FIXTURE_BILLING_PERIOD=YEARLY
FIXTURE_BILLING_PERIOD_CANONICAL=YES
EMULATOR_COMMERCIAL_OFFER_REQUIRED=NO
UNNECESSARY_COMMERCIAL_OFFER_FIXTURE_ADDED=NO
CONCURRENT_ACTIVATION_SINGLE_WINNER_CONTRACT_PRESERVED=YES
DUPLICATE_ACTIVATION_GRANTS=0
PRODUCTION_VALIDATION_WEAKENED=NO
PRODUCTION_SOURCE_FIX_REQUIRED=NO
EXACT_CAUSAL_TEST=PASS_1_OF_1
EMULATOR_TEST_AFTER_FIX=PASS_60_OF_60
MEMBERSHIP_EMULATOR_CONTRACT=PASS
CHECKPOINT_EMULATOR_TEST_REPEATED=NO
UNCLASSIFIED_PENDING_FILES=0
HISTORICAL_PENDING_FILES_PRESERVED=152/152
PREEXISTING_PROTECTED_FILES_MODIFIED=0/102
CHECKPOINT_FIRESTORE_WRITES=0
TEST_COMMERCIAL_OFFER_WRITES=0
REAL_TRIAL_ACTIVATION_EXECUTED=NO
REAL_PURCHASE_EXECUTED=NO
REAL_PLAYER_MUTATIONS=0
REAL_ENTITLEMENT_MUTATIONS=0
BACKEND_REDEPLOYED=NO
DEPLOY=NO
PROD_DEPLOYMENT=NO
```

Commit/push and the new CI terminal outcome are recorded after this checkpoint is created. This commit cannot contain its own SHA or the future CI result; the post-checkpoint report update and generated evidence will retain those results without amending the checkpoint. TEST deployment requires the next explicitly authorized task.
