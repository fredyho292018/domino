# APP-SHELL-01 PREWORK FINAL PRODUCT REVIEW

Read-only review of exactly 23 pending product/config/UI files. No mock, commit, push, regeneration, build or ad request. The 79 generated/temp files were not re-reviewed; they remain excluded. All 23 product-file hashes still match the prior inventory.

## Decision

**C — INCOMPLETE_KEEP_LOCAL.** The integration has a coherent purpose but current clean-checkout Android build evidence is absent. It depends on generated dependency resolution and mixes local signing with release changes. Do not label it ready solely from old test reports. No small implementation fix is proven sufficient, so option D is not asserted.

## AdMob runtime and safety

The two changes are CONFIG_ONLY: the settings asset enables existing DEVELOPMENT ads; the plugin manifest supplies configured application metadata. Existing AdsConfiguration rejects production inventory in Editor/development and rejects DEVELOPMENT in release context. Normal mobile ApplicationServices uses PendingAdsConsent, which returns false; Editor uses explicitly authorized per-session mock consent. Therefore enabled configuration does NOT prove initialized SDK or live serving. SDK/service source and declared dependencies exist; compilation of the present Android state is NOT VERIFIED. No real ads ran. Production-capable code exists but current configuration/consent does not enable production serving. Actual mobile activation would require a reviewed consent implementation; this is not authorized here. Firebase Analytics is declared by Firebase dependency configuration, not shown to be an Ads initialization prerequisite in the application path. Native Ads/UMP and Android metadata remain build dependencies.

## Android

Six Gradle template/meta files plus GvhProjectSettings configure Ads/Firebase dependencies, AndroidX, Jetifier and resolver behavior. No new permission name is introduced by these seven files. The separate AdMob manifest retains android.permission.INTERNET with no permission delta. Transitive merged-manifest permissions were not established without a build and are not claimed audited. Existing manifest INTERNET supports network SDK requests.

The configuration is logically related, but readiness is conditional: settingsTemplate references GeneratedLocalRepo/Firebase/m2repository. Firebase source POMs use srcaar; the three source srcaar archives are tracked. EDM4U must materialize the generated Maven aar/POM outputs and Firebase Android resources for a clean checkout. Inputs exist in tracked Firebase Editor dependency XML, srcaar files, platform config and resolver/templates. Reproducible from project configuration: YES by source mechanism; deterministic successful clean regeneration/build NOT VERIFIED. Do not manually checkpoint generated aar copies or resolver caches merely to bypass that validation. No regeneration was performed.

## Firebase policy and file split

No Firebase runtime source is changed. Three Firebase SDK POMs and four FirebaseApp.androidlib integration files are resolver/platform outputs (7). Two already-tracked google-services JSON files plus desktop .meta are platform-config files (3); local-only credential configuration files: 0. JSON adds a second client with unchanged project_info. Existing history includes these configs in commit 3edb841; .gitignore does not exclude them. This establishes existing versioning practice, not a new blanket publication/security policy. Config contains client identifiers/API configuration, not a service-account key. Values are not reproduced here. Auth and Firestore use this integration; Analytics is declared in existing dependency XML. No evidence of a Crashlytics change in these ten files.

## Signing metadata

The containing ProjectSettings.asset is tracked, so SIGNING_FILE_LOCAL_ONLY=NO for the file itself. Its pending custom-signing references are local private configuration and must remain uncommitted. No keystore, alias value or password was printed/opened/copied. The mixed file must remain excluded as-is; a later authorized split can preserve safe package/version/SDK/template changes while keeping signing configuration private.

## Pre-existing UI and default theme

ProfileDataSourceSettings.asset and its .meta define built-in local Addressables paths, not visual screens. Localization Settings.asset only changes managed-reference IDs; selector semantics remain unchanged. All three are LEAVE_UNCOMMITTED for now; no dependency requires them to be checkpointed before an isolated mock. They are not Auth, toolbar or a previous shell experiment.

CURRENT_DEFAULT_THEME_SOURCE: client/DominoGame/Assets/_Domino/Scripts/UI/DominoVisualTheme.cs, with UiKit and TileStyles. Existing name ModernSocialPremium: deep navy background, green table treatment, warm gold accent, ivory text/tiles, subdued secondary text. Preserve this existing palette; do not introduce a theme system. Theme source is context inspected read-only, not one of the pending 23 files.

## Validation and security

Existing AdsFoundationTests, RewardedAdsTests, GuestAuthTests and Editor FirebaseGuestValidation/PlayerFoundationValidation/FirestoreIsolationValidation cover relevant behaviors. Historical H1/H2 report Android resolution/rewarded tests with ads disabled and older package/build settings; they are not proof of this pending change set. No normal Unity build was launched because import/resolver/build would generate or rewrite project artifacts during this read-only review.

Targeted byte/text scan found no Firebase UID, token, password or private-key material in the commit-candidate files. A broad-pattern hit in excluded PlayerSettings resolved to serialized console-platform configuration, not evidence of a Firebase identity. Local signing references are excluded regardless. Text scan is not a guarantee about arbitrary encoded data. No credentials were printed.

## Per-file assessment

| Path | Category | Purpose | Completeness | Safe to commit now | Dependency | Secret risk | Action |
|---|---|---|---|---|---|---|---|
| `client/DominoGame/Assets/AddressableAssetsData/ProfileDataSourceSettings.asset` | G | Addressables built-in local build/load profile; no remote environment configured | Present; no shell functionality | Not required for checkpoint | Addressables/localization | No credential detected | LEAVE_UNCOMMITTED |
| `client/DominoGame/Assets/AddressableAssetsData/ProfileDataSourceSettings.asset.meta` | G | Addressables built-in local build/load profile; no remote environment configured | Present; no shell functionality | Not required for checkpoint | Addressables/localization | No credential detected | LEAVE_UNCOMMITTED |
| `client/DominoGame/Assets/Firebase/m2repository/com/google/firebase/firebase-app-unity/13.16.0/firebase-app-unity-13.16.0.pom` | C | Tracked Firebase SDK POM changes aar packaging to srcaar | Configuration present; integration unverified | HOLD | Android resolver + clean build | No credential detected | Preserve; distinguish platform config from regeneratable output |
| `client/DominoGame/Assets/Firebase/m2repository/com/google/firebase/firebase-auth-unity/13.16.0/firebase-auth-unity-13.16.0.pom` | C | Tracked Firebase SDK POM changes aar packaging to srcaar | Configuration present; integration unverified | HOLD | Android resolver + clean build | No credential detected | Preserve; distinguish platform config from regeneratable output |
| `client/DominoGame/Assets/Firebase/m2repository/com/google/firebase/firebase-firestore-unity/13.16.0/firebase-firestore-unity-13.16.0.pom` | C | Tracked Firebase SDK POM changes aar packaging to srcaar | Configuration present; integration unverified | HOLD | Android resolver + clean build | No credential detected | Preserve; distinguish platform config from regeneratable output |
| `client/DominoGame/Assets/Plugins/Android/FirebaseApp.androidlib.meta` | C | Firebase client configuration/generated Android resources, not server Auth source | Configuration present; integration unverified | HOLD | Android resolver + clean build | No credential detected | Preserve; distinguish platform config from regeneratable output |
| `client/DominoGame/Assets/Plugins/Android/FirebaseApp.androidlib/AndroidManifest.xml` | C | Firebase client configuration/generated Android resources, not server Auth source | Configuration present; integration unverified | HOLD | Android resolver + clean build | No credential detected | Preserve; distinguish platform config from regeneratable output |
| `client/DominoGame/Assets/Plugins/Android/FirebaseApp.androidlib/project.properties` | C | Firebase client configuration/generated Android resources, not server Auth source | Configuration present; integration unverified | HOLD | Android resolver + clean build | No credential detected | Preserve; distinguish platform config from regeneratable output |
| `client/DominoGame/Assets/Plugins/Android/FirebaseApp.androidlib/res/values/google-services.xml` | C | Firebase client configuration/generated Android resources, not server Auth source | Configuration present; integration unverified | HOLD | Android resolver + clean build | No credential detected | Preserve; distinguish platform config from regeneratable output |
| `client/DominoGame/Assets/Plugins/Android/GoogleMobileAdsPlugin.androidlib/AndroidManifest.xml` | A | Enable existing development ads or add real AdMob app metadata | Configuration present; integration unverified | HOLD | Android resolver + clean build | No credential detected | Preserve; validate intended development activation and consent boundary |
| `client/DominoGame/Assets/Plugins/Android/gradleTemplate.properties` | B | Gradle template or paired Unity metadata; Firebase/Ads dependencies and generated Maven path | Configuration present; integration unverified | HOLD | Android resolver + clean build | No credential detected | Preserve; validate resolver + build coherence |
| `client/DominoGame/Assets/Plugins/Android/gradleTemplate.properties.meta` | B | Gradle template or paired Unity metadata; Firebase/Ads dependencies and generated Maven path | Configuration present; integration unverified | HOLD | Android resolver + clean build | No credential detected | Preserve; validate resolver + build coherence |
| `client/DominoGame/Assets/Plugins/Android/mainTemplate.gradle` | B | Gradle template or paired Unity metadata; Firebase/Ads dependencies and generated Maven path | Configuration present; integration unverified | HOLD | Android resolver + clean build | No credential detected | Preserve; validate resolver + build coherence |
| `client/DominoGame/Assets/Plugins/Android/mainTemplate.gradle.meta` | B | Gradle template or paired Unity metadata; Firebase/Ads dependencies and generated Maven path | Configuration present; integration unverified | HOLD | Android resolver + clean build | No credential detected | Preserve; validate resolver + build coherence |
| `client/DominoGame/Assets/Plugins/Android/settingsTemplate.gradle` | B | Gradle template or paired Unity metadata; Firebase/Ads dependencies and generated Maven path | Configuration present; integration unverified | HOLD | Android resolver + clean build | No credential detected | Preserve; validate resolver + build coherence |
| `client/DominoGame/Assets/Plugins/Android/settingsTemplate.gradle.meta` | B | Gradle template or paired Unity metadata; Firebase/Ads dependencies and generated Maven path | Configuration present; integration unverified | HOLD | Android resolver + clean build | No credential detected | Preserve; validate resolver + build coherence |
| `client/DominoGame/Assets/StreamingAssets/google-services-desktop.json` | C | Firebase client configuration/generated Android resources, not server Auth source; client entries=2; service-account private key=False | Configuration present; integration unverified | HOLD | Android resolver + clean build | No credential detected | Preserve; distinguish platform config from regeneratable output |
| `client/DominoGame/Assets/StreamingAssets/google-services-desktop.json.meta` | C | Firebase client configuration/generated Android resources, not server Auth source | Configuration present; integration unverified | HOLD | Android resolver + clean build | No credential detected | Preserve; distinguish platform config from regeneratable output |
| `client/DominoGame/Assets/_Domino/Localization/Localization Settings.asset` | G | Only managed-reference IDs change; locale selectors retain behavior | Present; no shell functionality | Not required for checkpoint | Addressables/localization | No credential detected | LEAVE_UNCOMMITTED |
| `client/DominoGame/Assets/_Domino/Resources/AdsSettings.asset` | A | Enable existing development ads or add real AdMob app metadata | Configuration present; integration unverified | HOLD | Android resolver + clean build | No credential detected | Preserve; validate intended development activation and consent boundary |
| `client/DominoGame/Assets/google-services.json` | C | Firebase client configuration/generated Android resources, not server Auth source; client entries=2; service-account private key=False | Configuration present; integration unverified | HOLD | Android resolver + clean build | No credential detected | Preserve; distinguish platform config from regeneratable output |
| `client/DominoGame/ProjectSettings/GvhProjectSettings.xml` | B | EDM4U disables resolution prompt, enables Jetifier; analytics remains disabled | Configuration present; integration unverified | HOLD | Android resolver + clean build | No credential detected | Preserve; validate resolver + build coherence |
| `client/DominoGame/ProjectSettings/ProjectSettings.asset` | D | Mixed Android release/package/version/SDK/template changes and local keystore reference/alias | Mixed local/release settings | NO | Authorized separation of release/signing | Private local signing references | LEAVE_UNCOMMITTED_EXCLUDE_AS_IS |

## Final output

```text
REVIEWED_PRODUCT_FILES=23
ADMOB_FILES=2
ADMOB_IMPLEMENTATION_STATE=CONFIG_ONLY
ADMOB_RUNTIME_STATE=ENABLED_DEVELOPMENT_CONFIG_MOBILE_CONSENT_CLOSED_INITIALIZATION_NOT_OBSERVED
ADMOB_CHECKPOINT_READY=NO
ANDROID_FILES=7
ANDROID_CHANGE_SET_COHERENT=YES_PURPOSE_BUILD_READINESS_UNVERIFIED
ANDROID_PERMISSION_REVIEW=NO_DIRECT_PERMISSION_DELTA_MERGED_MANIFEST_NOT_VALIDATED
FIREBASE_FILES=10
FIREBASE_SOURCE_FILES=0_RUNTIME
FIREBASE_GENERATED_FILES=7_RESOLVER_INTEGRATION
FIREBASE_PLATFORM_CONFIG_FILES=3
FIREBASE_LOCAL_CONFIG_FILES=0
FIREBASE_CONFIG_POLICY=EXISTING_TRACKED_PLATFORM_CONFIG_PRACTICE_NO_NEW_POLICY
WHAT_NEEDS_REGENERATION=EDM4U_FIREBASE_MAVEN_ARTIFACTS_AND_ANDROID_RESOURCES_ON_CLEAN_CHECKOUT
DEPENDENCY_REGENERATION_REQUIRED=YES_FOR_CLEAN_CHECKOUT_WITH_GENERATED_OUTPUT_EXCLUDED
REPRODUCIBLE_FROM_PROJECT_CONFIG=YES_SOURCE_MECHANISM_BUILD_NOT_PROVEN
SIGNING_FILES=1
SIGNING_FILE_TRACKED=YES
SIGNING_FILE_LOCAL_ONLY=NO_TRACKED_CONTAINER_WITH_LOCAL_PRIVATE_CHANGES
SIGNING_FILE_CONTAINS_PRIVATE_CONFIGURATION=YES
SIGNING_RECOMMENDED_ACTION=EXCLUDE_AS_IS_PRESERVE_LOCAL
PREEXISTING_UI_FILES=3
PREEXISTING_UI_PURPOSE=ADDRESSABLES_PROFILE_AND_LOCALIZATION_SERIALIZATION
PREEXISTING_UI_CLASSIFICATION=LEAVE_UNCOMMITTED_ALL_THREE
UI_CHECKPOINT_REQUIRED_BEFORE_APP_SHELL=NO
CURRENT_DEFAULT_THEME_SOURCE=DominoVisualTheme.cs_MODERN_SOCIAL_PREMIUM
CURRENT_BUILD_WITH_PENDING_FILES=NOT_VERIFIED
EXISTING_RELEVANT_TESTS=ADS_FOUNDATION_REWARDED_ADS_GUEST_AUTH_FIREBASE_EDITOR_VALIDATORS
ANDROID_FIREBASE_ADMOB_DECISION=C_INCOMPLETE_KEEP_LOCAL
MINIMAL_COMPLETION_REQUIRED=NOT_IMPLEMENTED_SIGNING_SPLIT_CONFIG_REVIEW_AND_CLEAN_RESOLVER_BUILD_VALIDATION_PROPOSED
RAW_FIREBASE_UIDS=0_IDENTIFIED
TOKENS=0_IDENTIFIED
PASSWORDS=0_IDENTIFIED
PRIVATE_KEYS=0_IDENTIFIED
SIGNING_SECRETS=0_IN_COMMIT_CANDIDATES
GENERATED_TEMP_FILES=79
GENERATED_TEMP_COMMITTED=NO
GENERATED_TEMP_DELETED=NO
PRODUCT_FILES_MODIFIED_DURING_REVIEW=0
COMMIT_CREATED=NO
PUSH=NO
DEPLOY=NO
APP_SHELL_01_STARTED=NO
NEXT=APP-SHELL-01 FINAL PREWORK DECISION
```
