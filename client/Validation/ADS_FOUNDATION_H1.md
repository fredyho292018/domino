# Domino Monetization H1

## Scope and source

Started from clean `main`, `6907aef147d478d8a7fb2b7b69174728e0210ed1`.
No commit, push, deployment, backend, economy, identity, presence or gameplay changes.

Official Google Mobile Ads Unity **11.5.0** installed from
[Google's release](https://github.com/googleads/googleads-mobile-unity/releases/tag/v11.5.0)
using its `.unitypackage` assets and original metadata. Package SHA-256:
`770C34BB7485E8DB20A888CC008A14FDC94FFE8D34B4D2EE24434A7BE91E08CE`.
71 official assets imported; 14 bundled EDM4U files deliberately skipped because
Firebase already supplies the exact same **1.2.188**. No second resolver, downgrade,
OpenUPM migration or modification of Firebase dependencies.
All 71 official asset payloads are unchanged; Unity/EDM reserialized metadata on two
bundled UMP resources during import.

[Official setup requirements](https://developers.google.com/admob/unity/quick-start):
Android minimum 23, target 35 or later. This project retains minimum **26**, automatic
target (installed highest **35**) and `com.teamfho.domino`. Added Unity's built-in
`com.unity.modules.androidjni:1.0.0`, required for Android native interop. The first
Android compilation exposed its absence in Localization; no Unity install changed.

Standard Google Android SDK **25.4.0** explicitly selected, not Next-Gen.
Official package dependencies also include UMP Android **4.0.0**, GMA iOS **13.9**,
UMP iOS **3.1.0**, AndroidX constraintlayout **2.1.4**, fragment **1.7.1** and
lifecycle-process **2.6.2**. Bundled UMP is not an implemented consent flow.

## Configuration and consent

`Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset` is the only owned
App-ID configuration: real Android App ID supplied by the user; iOS blank.
The official manifest preprocessor writes Android APPLICATION_ID metadata at build
time. Do not add another App-ID field to Domino settings or hand-edit a second manifest.

`Assets/_Domino/Resources/AdsSettings.asset` defaults to **Enabled=false**, DEVELOPMENT,
preview reward **10**. The confirmed Android rewarded unit is stored only in the
production field; iOS production remains blank. No production requests are permitted
in H1/H2. An App ID is not an ad unit ID. Preview reward is not connected to Wallet
or any economy API.

Development always selects Google's immutable rewarded demo unit for the platform:

- Android: `ca-app-pub-3940256099942544/5224354917`
- iOS: `ca-app-pub-3940256099942544/1712485313`

The Inspector's development fields are normalized on validation; runtime selection
uses constants even if serialized fields are tampered with. PRODUCTION requires a
well-formed non-demo unit ID, with no fallback. Editor/development contexts reject
PRODUCTION; release rejects DEVELOPMENT. Unsupported platforms are unavailable.

`PendingAdsConsent.CanInitializeAds` is **false**. Enabling the asset alone therefore
does not initialize the SDK. A future UMP adapter must explicitly authorize the gate
before retrying initialization. Consent pending keeps NOT_INITIALIZED with reason
CONSENT_PENDING; it is not a fatal error. No COPPA/underage assumptions or consent UI.
Google warns initialization may preload ads: the boundary is before Initialize,
not merely before Load. This is preparation, not a claim of regulatory compliance.

## Lifecycle

ApplicationServices creates IAdsService independently of Firebase, REST and WebSocket.
It owns disposal and exposes DISABLED / NOT_INITIALIZED / INITIALIZING / READY / FAILED.
Calls originate on Unity's main thread. Concurrent logical requests share one task;
READY and FAILED do not retry SDK initialization. Configuration and SDK errors remain
nonfatal, with fixed safe categories. No raw SDK diagnostics are logged by Domino.

The native adapter reserves a static initialization task before invoking Google's SDK,
so recreating the composition root or changing scenes cannot initialize twice within
the application domain. Callback completion is bounded to 30 seconds; continuations
return through the captured Unity context. Disposed services ignore late callbacks.
No SDK shutdown API is needed because H1 creates no ad objects. A new Editor domain
is a new SDK lifetime, matching Unity domain reload semantics.

No RewardedAd.Load, Show, reward callback or ad request exists in Domino's H1 code.
The SDK naturally contains its own APIs/example placeholder resources; these are not
called by the game. No real or test ad requests were issued during validation.

## Reproducing validation

- `client/Validation/RunAdsFoundationTests.ps1`: configuration, environment guards,
  consent, single flight, success, safe failure, disabled, disposal and late callback.
- Existing GuestAuth, PlayerFoundationClient, RunTests and Compile scripts unchanged
  except Compile's references to the new official GMA assemblies.
- `Domino.Editor.AdsFoundationValidation.Run` in an isolated batch Editor tests the
  official Editor placeholder SDK in Play Mode, with Firebase blocked and no Load/Show.
  Set DOMINO_H1_RESULT to an output text file. Edit Mode alone does not run the SDK's
  callback loop; the initial diagnostic timed out safely and was replaced by Play Mode.
- `Domino.Editor.AdsFoundationValidation.BuildAndroid` with `-buildTarget Android`
  invokes the official resolver/preprocessors and builds a Development APK. Set
  DOMINO_H1_APK and DOMINO_H1_RESULT. Run only in a disposable validation copy.
- Existing PlayerProfileValidation.RunFake and PlayerFoundationValidation.Run cover
  profile, alias, localization, Portrait and gameplay without real identity creation.

Generated logs/builds are under ignored `client/Validation/Generated/H1/`.
No Firestore writes or real wallet operations are part of these validators.

Final results: 31 Ads checks, 43 GuestAuth, 469 Player/Realtime client checks,
82 configuration, 32679 scoring and 2981089 domain/geometry checks passed.
Profile/alias/realtime UI passed EN/ES at nine sizes. Complete-round Play Mode passed
139406 checks across nine Portrait sizes and four local seats, with zero Console errors.
The ARM64 IL2CPP Development APK built successfully (39871122 bytes). Its merged
manifest has one matching App ID and confirms minimum 26 / target 35. It was not
installed or launched on a device. Generated Gradle templates/manifests stay in the
validation copy; Google's build preprocessors reproduce them in a normal source build.
See `ADS_FOUNDATION_H1_REPORT.txt` for the full report and validation limitations.

## Limits and next phase

Physical mobile initialization, iOS native build/App ID and rewarded unit ID,
UMP consent flow and AdMob review remain pending. AdMob status “Debe revisarse” is
external; no console/account settings were changed. SDK integration does not mean
production monetization is enabled.

H2: TEST rewarded load/show lifecycle and callback only; no wallet mutation.
