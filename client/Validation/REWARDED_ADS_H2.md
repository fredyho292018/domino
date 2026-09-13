# Domino Monetization H2 — TEST ONLY

Base: clean `main` at `63ceb732530bcf6d96e5c8e1a31bf2dbce0d7be7`.
No commit/push, production requests, backend or economy changes.

## SDK and safety

Uses installed Google Mobile Ads Unity **11.5.0**, without changing dependencies.
`RewardedAd.Load(unit, new AdRequest(), callback)`, `CanShowAd()`, `Show(rewardCallback)`
and `Destroy()` are compiled against the installed assemblies. Fullscreen events are
`OnAdFullScreenContentOpened`, `OnAdFullScreenContentClosed`, and
`OnAdFullScreenContentFailed`.
References: [rewarded lifecycle](https://developers.google.com/admob/unity/rewarded),
[official demo units and Editor behavior](https://developers.google.com/admob/unity/test-ads).
The demo-unit table identifies Android `/5224354917` and iOS `/1712485313` as rewarded;
the generic rewarded guide currently displays interstitial example IDs, which we do
not use. H1's explicitly agreed rewarded IDs remain unchanged.

`AdsSettings.asset` remains **Enabled=false**, DEVELOPMENT. Its production unit is
configuration only. H2's service rejects PRODUCTION entirely; the SDK boundary also
checks Editor/development context and the exact platform demo unit before requesting.
No production fallback, test-device advertising identifier, Firebase UID or SSV data.

## Lifecycle and threading

`GoogleRewardedAdsService` is separate from H1's SDK initialization service. Both are
owned by ApplicationServices and share the same consent boundary. Rewarded initialization
awaits SDK READY before loading. All SDK callbacks are posted to the captured Unity
SynchronizationContext before touching service state, subscribers, or native ad objects.

States: DISABLED, NOT_LOADED, LOADING, READY, SHOWING, EARNED, FAILED.
EARNED means Google eligibility was reported, and remains active until fullscreen closes.
It does not mean Domino coins were awarded. Reward type/amount are informational only.
Each ad publishes at most one RewardEarned event. Old instance callbacks are ignored.

One shared load task and one current ad. READY/LOADING/SHOWING/EARNED suppress redundant
round preloads. Successful close destroys the consumed instance before the next load.
Load and fullscreen failures are nonfatal, with no automatic failure loop; a later round
or explicit load can retry. Disposal ends the service lifetime and ignores/destroys late
load results. A consumed ad can be replaced; a disposed service cannot be reused.

Inventory is considered stale at 55 minutes, conservatively below Google's documented
one-hour lifetime, or whenever CanShowAd returns false. Preload/show checks discard and
reload stale inventory; show returns unavailable until READY. No polling/timer retries.
The adapter times out a missing load callback at 45 seconds and destroys late arrivals.

Events for H5: RewardedStateChanged, RewardedAvailabilityChanged, RewardEarned.
Availability is recomputed on access and lifecycle transitions; there is no background
expiration timer. Future UI must still handle an unavailable response when showing.
Subscriber exceptions cannot prevent cleanup. No game timeScale/audio changes in Domino
ads code; the official SDK owns its fullscreen behavior.

## Canonical round hook

ClientGame emits `GameEventType.ROUND_FINISHED` after applying RoundScoring/MatchState.
DominoClientController's existing Enqueue method forwards that event to
RewardedRoundPreload. The helper requests preload without awaiting it. No inference from
UI text, changed engine contract, scoring change, or delay in round continuation.
Other events including TURN_CHANGED and GAME_FINISHED do not request ads.
ShowRewardedAsync is called only by the explicit Editor test menu, never by the round hook.

## Consent and manual Editor test

Mobile keeps PendingAdsConsent closed. No UMP flow or mobile consent claim is implemented.
The Editor has a separate, explicitly authorized **local mock session** gate which is
false on every new Play session and absent from player builds. It is not a mobile consent
override. Both initialization and loads check their gate; denied/revoked consent prevents
loads and shows, including after an asynchronous load completes.

To test manually:

1. Temporarily set AdsSettings Enabled=true, keeping DEVELOPMENT, before Play.
2. Enter Play, then select **Domino → Ads → Authorize and Load Editor Mock**.
3. Once READY, select **Domino → Ads → Show Rewarded Test Ad**.
4. Wait for the mock's countdown; use its **Close Ad** button. Next inventory preloads.
5. Restore Enabled=false after the test.

The menus are Editor-only. They do not add final monetization UI. The official Editor
prefab reuses artwork reading “interstitial test ad”; the API exercised is RewardedAd
and its reward callback. Native Android behavior is not established by that artwork.

## Validation

`RunRewardedAdsTests.ps1`: 45 isolated checks covering configuration/consent denial,
load single flight, explicit/double show, duplicate reward, close without reward, failure,
stale inventory, late callbacks, disposal, repeated cycles and round event behavior.
No real SDK requests in these unit tests.

`Domino.Editor.RewardedAdsValidation.Run` only accepts an isolated Generated copy and
batch Editor. Set DOMINO_H2_RESULT to an output file. It temporarily enables DEVELOPMENT,
uses a Firebase client that cannot create a guest/token, and authorizes only the Editor
mock gate. It completes a round through the normal controller, proves no automatic show,
invokes the explicit test trigger, captures the official prefab, waits for reward, closes,
verifies reload/audio/timeScale, and continues using the existing next-round button.
The copy's settings are restored to disabled even after a diagnostic failure.

Final mock run: 2 loads, 1 show, 1 reward callback (`Reward`, amount 10), 0 real loads;
wallet snapshot unchanged, zero Console errors. An earlier harness attempt selected the
parent SDK button instead of Close Ad and timed out; the selector was corrected. Across
both Editor attempts: 3 test loads, 2 shows, 2 reward callbacks, 0 real loads. No ad was
shown without an explicit test action. The native mobile SDK/network was not exercised.

Logs/capture under ignored `client/Validation/Generated/H2/`. Generated files and APKs
are not source assets. `adb devices -l` found no connected device: physical test NOT_RUN.
See REWARDED_ADS_H2_REPORT.txt for final regression/build results.

## Reload diagnosis (2026-09-12)

The reported second-load stall was not reproduced. Do not interpret this investigation
as a functional reload fix: the existing service and SDK lifecycle are unchanged.
Editor authorization is session-wide, not one-shot. A fresh Play Mode regression now
calls the actual Load menu handler once and Show handler once, earns, clicks the SDK
Close button through OnPointerClick, and waits without any additional menu commands.
Set DOMINO_H2_RELOAD_ONLY=1 alongside DOMINO_H2_RESULT when invoking the existing
isolated-copy validator to select this scenario. This is automated Play Mode validation,
not a claim of a fresh human/manual test in the user's open Editor.

Fresh result: two loads, two successful callbacks/completed tasks, one show/reward,
one disposed old instance, one active replacement. State order:
LOADING -> READY -> SHOWING -> EARNED -> NOT_LOADED -> LOADING -> READY.
The service clears current before disposing the old adapter; the adapter detaches SDK
callbacks before Destroy. Both occur before the single automatic preload starts.

Editor-only diagnostics number load attempts and report callback success/failure,
completion, disposal and TIMEOUT_NO_CALLBACK. The existing 45-second timeout ends in
FAILED through the service's safe LOAD error log; it does not retry indefinitely.
These traces allow a future occurrence to be identified without interpreting old
Console counters. No Android/production behavior or consent policy was changed.

Evidence: Generated/H2/reload.log and reload-result.txt (ignored artifacts).
Pure service regressions: 46 checks PASS. Unity compilation and SDK Editor mock: PASS,
Console errors 0. No real ad requests; no wallet/coins/backend changes. Source AdsSettings
remains disabled/DEVELOPMENT. The previously pending ApiSettings change was not touched.

## Future scope (unchanged)

H5 may show “Continue” plus an optional “Watch ad / +10 coins” preview after a round.
H2 deliberately implements neither that final UI nor credit. Google reward amount must
never be treated as authoritative Domino coins. H3 defines authenticated claim semantics
and persistent idempotency; H4 implements the atomic wallet ledger. Neither starts here.
