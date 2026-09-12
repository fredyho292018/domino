#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Domino.Ads;
using UnityEditor;
using UnityEngine;

namespace Domino.Editor
{
    public static class AdsFoundationValidation
    {
        const string Running = "Domino.H1Validation";
        sealed class NoFirebase : Domino.Infrastructure.Firebase.IFirebaseClient, Domino.Identity.IAuthTokenProvider
        {
            public Task<string> CheckDependenciesAsync() => Task.FromResult("Unavailable");
            public void InitializeApp() { }
            public Domino.Identity.PlayerIdentity GetCurrentUser() => null;
            public Task<Domino.Identity.PlayerIdentity> SignInAnonymouslyAsync() => throw new InvalidOperationException("NO_GUEST");
            public Task<string> GetIdTokenAsync(bool refresh, System.Threading.CancellationToken ct) => throw new InvalidOperationException("NO_TOKEN");
        }
        [InitializeOnLoadMethod]
        static void Register()
        {
            if (!SessionState.GetBool(Running, false)) return;
            // This validation never accesses Firebase or creates identities.
            Domino.Infrastructure.ApplicationServices.ValidationFirebaseFactory = () => new NoFirebase();
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode) Validate();
            };
        }
        sealed class EditorOnlyGate : IAdsConsentGate { public bool CanInitializeAds => Application.isEditor; }
        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("BATCH_ONLY");
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene);
            SessionState.SetBool(Running, true);
            Register();
            EditorApplication.isPlaying = true;
        }
        static async void Validate()
        {
            var errors = 0;
            Application.LogCallback capture = (message, stack, type) => { if (type == LogType.Error || type == LogType.Exception) errors++; };
            Application.logMessageReceived += capture;
            try
            {
                var settings = Resources.Load<DominoAdsSettings>("AdsSettings");
                if (!settings || settings.Configuration.Enabled || settings.RewardCoinsPreview != 10) throw new Exception("SOURCE_SETTINGS");
                var google = AssetDatabase.LoadMainAssetAtPath("Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset");
                var serialized = new SerializedObject(google);
                if (!serialized.FindProperty("adMobAndroidAppId").stringValue.Contains("~")) throw new Exception("APP_ID");
                // Editor placeholder initialization only. No Load/Show and no mobile consent claim.
                var service = new GoogleMobileAdsService(new AdsConfiguration(true, AdsEnvironment.DEVELOPMENT, AdsPlatform.Android, true),
                    new EditorOnlyGate(), new UnityGoogleAdsSdk(), Debug.Log);
                var first = service.InitializeAsync();
                if (!ReferenceEquals(first, service.InitializeAsync())) throw new Exception("SINGLE_FLIGHT");
                await first;
                if (service.State != AdsState.READY) throw new Exception("EDITOR_INITIALIZATION");
                service.Dispose();
                var recreated = new UnityGoogleAdsSdk();
                await recreated.InitializeAsync();
                if (errors != 0) throw new Exception("CONSOLE_ERRORS");
                Write("PASS\nEDITOR_SDK_INITIALIZATION=PASS\nCONSOLE_ERRORS=0\nAD_REQUESTS=0");
                EditorApplication.Exit(0);
            }
            catch (Exception e) { Write("FAIL category=" + e.GetType().Name + " reason=" + e.Message); EditorApplication.Exit(1); }
            finally { SessionState.SetBool(Running, false); Application.logMessageReceived -= capture; }
        }
        static void Write(string result)
        {
            var path = Environment.GetEnvironmentVariable("DOMINO_H1_RESULT");
            if (!string.IsNullOrEmpty(path)) File.WriteAllText(path, result);
            Debug.Log("[H1 VALIDATION] " + result);
        }
        public static void BuildAndroid()
        {
#if UNITY_ANDROID
            try
            {
                // Official preprocessor invokes EDM4U and generates manifest metadata.
                new GoogleMobileAds.Editor.AndroidBuildPreProcessor().OnPreprocessBuild(null);
                new GoogleMobileAds.Editor.ManifestProcessor().OnPreprocessBuild(null);
                AssetDatabase.SaveAssets();
                var output = Environment.GetEnvironmentVariable("DOMINO_H1_APK");
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                    locationPathName = output, target = BuildTarget.Android,
                    options = BuildOptions.Development
                });
                var success = report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded;
                Write("ANDROID_BUILD=" + (success ? "PASS" : "FAIL") + "\nERRORS=" + report.summary.totalErrors);
                EditorApplication.Exit(success ? 0 : 1);
            }
            catch (Exception e) { Write("ANDROID_BUILD=FAIL category=" + e.GetType().Name); EditorApplication.Exit(1); }
#else
            Write("ANDROID_BUILD=FAIL category=BUILD_TARGET"); EditorApplication.Exit(1);
#endif
        }
    }
}
#endif
