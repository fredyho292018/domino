# H5 — optional round reward and confirmed balance

The canonical `BoardView.Finish` now includes the result, match score, confirmed Wallet,
an optional ad action and a separate Continue action. The existing final-round path is
also eligible. Gameplay, scoring, server code, SSV and the ledger were not changed.

`RoundRewardFlow` belongs to ApplicationServices, not a scene/panel. It holds a single
active operation and the existing round-result object as presentation eligibility.
This identity never goes to the backend. Successful rounds cannot show a second ad
in the same application session. This is a UX limit, not H6 anti-abuse enforcement.

Watch delegates to the existing H3 Show preflight. Client-earned is not verification.
After the ad closes, the flow observes backend status at bounded intervals (initial,
2, 3, 5, 8 and 12 seconds between attempts). VERIFIED/CONSUMED is handed to H4 consume.
Transport failures retain the intent for the same bounded sequence or explicit retry.
There is no permanent polling. Continue/scene destruction never cancels this operation.

PlayerService applies the absolute backend balance through its existing operation gate.
The panel and profile both observe that WalletSnapshot; neither adds coins locally.
The consume response's reward amount drives the success label/toast. The initial
10-coin display is an informational preview of the current backend policy; the intent
preview is used once received. No client amount is sent to the backend.

The toast lasts 2.4 seconds, accepts no input, and also works after Continue. Startup
recovery after Player initialization uses H4's latest VERIFIED pending intent. If the
initial backend sync is unavailable, profile sync must first establish a snapshot.
Pending consume retry remains available with an existing confirmed snapshot even after
a transport failure. A server-consumed lost response is recovered by normal bootstrap.

Ads disabled hides the CTA and prevents H5 calls. H2 remains responsible for preload
and disposing/reloading inventory. No automatic ad show or production inventory added.
17 new keys use the existing Unity Localization StringTables and Smart Strings in EN/ES.

## Validation fixture

`Domino.Editor.RoundRewardValidation.Run` is UNITY_EDITOR-only and requires a batch
editor project under `/Validation/Generated/`. It injects in-memory identity, player API,
reward status/consume and ad doubles into the existing composition root. These factories
do not exist in player builds. It plays an actual round through DominoClientController,
uses the canonical result panel, checks both locales and all 9 portrait targets, and
checks double tap, 0 -> 10 confirmed snapshots and same-session profile refresh.

This fixture provides no production endpoint or signature bypass, and does not access
Firebase, Firestore or AdMob. Simulated verification is reported as TEST_DOUBLE.
Backend atomicity remains covered by the unchanged H4 transaction tests.

Run client flow cases with `client/Validation/RunRoundRewardTests.ps1`.
Run the Editor fixture against an isolated copy with Unity's `-batchmode -projectPath`
and `-executeMethod Domino.Editor.RoundRewardValidation.Run` arguments.
Its screenshots/result are under that copy's `Validation/Generated/H5/` directory.

The source AdsSettings remains disabled in DEVELOPMENT. This task performs no commit,
push, deploy, real ad request or real wallet credit. H6 limits and H7 physical device /
public SSV validation remain separate work.
