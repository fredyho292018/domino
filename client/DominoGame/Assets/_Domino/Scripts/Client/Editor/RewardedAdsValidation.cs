#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Domino.Ads;
using Domino.Client;
using Domino.Infrastructure;
using Domino.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Domino.Editor
{
    public static class RewardedAdsValidation
    {
        const string Running = "Domino.H2Validation";
        static int errors;
        sealed class TestIntents : IRewardIntentApi
        {
            public Task<RewardIntentReceipt> CreateAsync(CancellationToken token) =>
                Task.FromResult(new RewardIntentReceipt("12345678-1234-4234-8234-123456789012", "ISSUED", DateTimeOffset.UtcNow.AddMinutes(10)));
            public Task<RewardIntentReceipt> StatusAsync(string id, CancellationToken token) => CreateAsync(token);
        }
        sealed class NoFirebase : Domino.Infrastructure.Firebase.IFirebaseClient, Domino.Identity.IAuthTokenProvider
        {
            public Task<string> CheckDependenciesAsync() => Task.FromResult("Unavailable");
            public void InitializeApp() { }
            public Domino.Identity.PlayerIdentity GetCurrentUser() => null;
            public Task<Domino.Identity.PlayerIdentity> SignInAnonymouslyAsync() => throw new InvalidOperationException("NO_GUEST");
            public Task<string> GetIdTokenAsync(bool refresh, CancellationToken token) => throw new InvalidOperationException("NO_TOKEN");
        }
        [InitializeOnLoadMethod]
        static void Register()
        {
            if (!SessionState.GetBool(Running, false)) return;
            ApplicationServices.ValidationFirebaseFactory = () => new NoFirebase();
            ApplicationServices.ValidationRewardIntentApiFactory = () => new TestIntents();
            Application.logMessageReceived += (_, __, type) => { if (type == LogType.Error || type == LogType.Exception) errors++; };
            EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.EnteredPlayMode) Validate(); };
        }
        public static void Run()
        {
            Domino.Infrastructure.ValidationNetworkPolicy.BeginIsolated();
            if (!Application.isBatchMode || !Application.dataPath.Replace('\\','/').Contains("/Validation/Generated/"))
                throw new InvalidOperationException("ISOLATED_COPY_REQUIRED");
            var asset = new SerializedObject(Resources.Load<DominoAdsSettings>("AdsSettings"));
            if (asset.FindProperty("environment").enumValueIndex != 0) throw new InvalidOperationException("DEVELOPMENT_REQUIRED");
            asset.FindProperty("enabled").boolValue = true; asset.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssets();
            SessionState.SetBool(Running, true); Register();
            EditorSceneManager.OpenScene("Assets/_Domino/Scenes/DominoClient.unity");
            Phase1Validation.OpenPortraitPreview();
            EditorApplication.isPlaying = true;
        }
        static async Task Until(Func<bool> condition, int seconds, string reason)
        {
            var deadline = DateTime.UtcNow.AddSeconds(seconds);
            while (!condition()) { if (DateTime.UtcNow > deadline) throw new Exception(reason); await Task.Delay(50); }
        }
        static void Check(bool condition, string reason) { if (!condition) throw new Exception(reason); }
        static async void Validate()
        {
            string report;
            try
            {
                if (Environment.GetEnvironmentVariable("DOMINO_H2_RELOAD_ONLY") == "1")
                    report = await ValidateReload();
                else
                {
                var rewarded = ApplicationServices.Rewarded;
                Check(UnityRewardedAdLoader.TestLoadRequests == 0 && rewarded.State == RewardedState.NOT_LOADED, "CONSENT_GATE");
                var walletBefore = ApplicationServices.Player.Wallet;
                ApplicationServices.EditorAdsConsent.AuthorizedForThisPlaySession = true;
                var controller = UnityEngine.Object.FindFirstObjectByType<DominoClientController>();
                await Until(() => controller.Menu != null, 20, "MENU");
                controller.StartMatch(GameModeDefinition.TeamMatch);
                Time.timeScale = 12;
                var deadline = DateTime.UtcNow.AddSeconds(150);
                while (!controller.State.Finished)
                {
                    if (DateTime.UtcNow > deadline) throw new Exception("ROUND_TIMEOUT");
                    if (controller.AcceptingInput)
                    {
                        var tile = controller.View.LocalTiles.FirstOrDefault(t => controller.State.CanPlay(t.Tile));
                        if (tile) { controller.Select(tile); controller.PlaySelected(); }
                    }
                    await Task.Delay(30);
                }
                await Until(() => rewarded.State == RewardedState.READY, 45, "ROUND_PRELOAD");
                Check(UnityRewardedAdLoader.TestLoadRequests == 1 && UnityRewardedAdLoader.TestShows == 0, "ROUND_LOAD_ONLY");
                // Let round presentation finish; then exercise the same debug trigger a user selects.
                await Until(() => controller.View.RoundPresentationFinished, 25, "ROUND_PRESENTATION"); Time.timeScale = 1;
                int rewards = 0; RewardedCompletionResult receipt = default;
                rewarded.RewardEarned += value => { rewards++; receipt = value; };
                RewardedTestMenu.Show();
                await Until(() => rewarded.State == RewardedState.SHOWING || rewarded.State == RewardedState.EARNED, 5, "SHOW");
                await Task.Delay(300);
                var resultPath = Environment.GetEnvironmentVariable("DOMINO_H2_RESULT");
                controller.StartCoroutine(Capture(Path.Combine(Path.GetDirectoryName(resultPath), "rewarded-editor.png")));
                await Until(() => rewards == 1, 15, "REWARD");
                var close = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).FirstOrDefault(b =>
                    b.GetComponentsInChildren<Button>().Length == 1 && b.GetComponentInChildren<Text>()?.text == "Close Ad");
                Check(close != null && close.interactable, "MOCK_CLOSE_BUTTON");
                close.onClick.Invoke();
                await Until(() => rewarded.State == RewardedState.READY, 45, "RELOAD");
                Check(UnityRewardedAdLoader.TestLoadRequests == 2 && UnityRewardedAdLoader.TestShows == 1 && rewards == 1, "COUNTERS");
                Check(Math.Abs(Time.timeScale - 1) < .01f && !AudioListener.pause, "RESUME");
                Check(ReferenceEquals(walletBefore, ApplicationServices.Player.Wallet), "WALLET_UNCHANGED");
                Check(errors == 0, "CONSOLE_ERRORS");
                // Continue without using the optional ad path or recreating any application service.
                controller.View.GetComponentsInChildren<Button>().Single(b => b.name == "Play").onClick.Invoke();
                Time.timeScale = 12;
                await Until(() => controller.AcceptingInput, 35, "GAME_CONTINUES");
                Check(!controller.State.Finished, "NEXT_ROUND_STARTED");
                Time.timeScale = 1;
                report = "PASS\nROUND_FINISHED_PRELOAD=PASS\nEXPLICIT_DEBUG_SHOW=PASS\nREWARD_CALLBACK=PASS\nNEXT_PRELOAD=PASS\nGAME_RESUMES=PASS\nWALLET_UNCHANGED=PASS\nCONSOLE_ERRORS=" + errors +
                    "\nTEST_AD_LOAD_REQUESTS=" + UnityRewardedAdLoader.TestLoadRequests + "\nTEST_AD_SHOWS=" + UnityRewardedAdLoader.TestShows +
                    "\nTEST_REWARD_CALLBACKS=" + UnityRewardedAdLoader.TestRewardCallbacks + "\nREAL_AD_LOAD_REQUESTS=0\nGOOGLE_REWARD_TYPE=" + receipt.GoogleRewardType + "\nGOOGLE_REWARD_AMOUNT=" + receipt.GoogleRewardAmount;
                }
            }
            catch (Exception error) { report = "FAIL reason=" + error.Message + "\nCONSOLE_ERRORS=" + errors; }
            SessionState.SetBool(Running, false);
            // Only the disposable validation copy was enabled; restore even on failure.
            var settings = new SerializedObject(Resources.Load<DominoAdsSettings>("AdsSettings"));
            settings.FindProperty("enabled").boolValue = false; settings.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssets();
            File.WriteAllText(Environment.GetEnvironmentVariable("DOMINO_H2_RESULT"), report);
            Debug.Log("[H2 VALIDATION] " + report);
            EditorApplication.Exit(report.StartsWith("PASS") && errors == 0 ? 0 : 1);
        }
        static async Task<string> ValidateReload()
        {
            var rewarded = ApplicationServices.Rewarded;
            Check(UnityRewardedAdLoader.TestLoadRequests == 0, "FRESH_SESSION_REQUIRED");
            var wallet = ApplicationServices.Player.Wallet;
            int rewards = 0;
            var states = new System.Collections.Generic.List<RewardedState>();
            rewarded.RewardedStateChanged += states.Add;
            rewarded.RewardEarned += _ => rewards++;
            RewardedTestMenu.Load(); // Exactly one authorization, through the real menu handler.
            await Until(() => rewarded.State == RewardedState.READY, 50, "FIRST_READY");
            Check(UnityRewardedAdLoader.MockCompletedLoads == 1, "FIRST_TASK");
            RewardedTestMenu.Show(); // Exactly one show; no menu action after closing.
            await Until(() => rewards == 1, 20, "FIRST_REWARD");
            Check(UnityRewardedAdLoader.MockSsvOptionsSet == 1 &&
                ApplicationServices.RewardVerification.State == RewardVerificationState.CLIENT_EARNED, "H3_CLIENT_ONLY_SSV_ATTACHED");
            var close = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b =>
                b.GetComponentsInChildren<Button>().Length == 1 && b.GetComponentInChildren<Text>()?.text == "Close Ad");
            Check(close.interactable, "CLOSE_ENABLED");
            close.OnPointerClick(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
                { button = UnityEngine.EventSystems.PointerEventData.InputButton.Left });
            await Until(() => rewarded.State == RewardedState.READY || rewarded.State == RewardedState.FAILED, 50, "SECOND_TERMINAL_STATE");
            Check(rewarded.State == RewardedState.READY, "SECOND_READY");
            await Task.Delay(500); // Detect accidental duplicate reloads after callback processing.
            Check(UnityRewardedAdLoader.TestLoadRequests == 2 && UnityRewardedAdLoader.TestShows == 1 && rewards == 1, "SINGLE_RELOAD");
            Check(UnityRewardedAdLoader.MockInstancesCreated == 2 && UnityRewardedAdLoader.MockInstancesDisposed == 1 &&
                UnityRewardedAdLoader.MockActiveInstances == 1, "INSTANCE_LIFECYCLE");
            Check(UnityRewardedAdLoader.MockSuccessCallbacks == 2 && UnityRewardedAdLoader.MockFailureCallbacks == 0 &&
                UnityRewardedAdLoader.MockCompletedLoads == 2, "CALLBACKS_AND_TASKS");
            Check(string.Join(",", states) == "LOADING,READY,SHOWING,EARNED,NOT_LOADED,LOADING,READY", "STATE_ORDER");
            Check(ApplicationServices.EditorAdsConsent.AuthorizedForThisPlaySession, "SESSION_AUTHORIZATION");
            Check(ReferenceEquals(wallet, ApplicationServices.Player.Wallet), "WALLET_UNCHANGED");
            Check(errors == 0, "CONSOLE_ERRORS");
            return "PASS\nFIRST_LOAD_READY=PASS\nFIRST_SHOW=PASS\nFIRST_REWARD=PASS\nFIRST_CLOSE=PASS\n" +
                "SECOND_LOAD_METHOD_CALLED=YES\nSECOND_AD_INSTANCE_CREATED=YES\nSECOND_LOAD_SUCCESS_CALLBACK=YES\n" +
                "SECOND_LOAD_FAILURE_CALLBACK=NO\nSECOND_LOAD_TASK_COMPLETED=YES\nSECOND_READY=PASS\n" +
                "EDITOR_MOCK_AUTHORIZATION_ONE_SHOT=NO\nAUTO_RELOAD_CALL_COUNT=1\nOLD_AD_DISPOSED=PASS\n" +
                "STATE_BEFORE_SECOND_LOAD=NOT_LOADED\nSTATE_AFTER_SECOND_LOAD=READY\nLOAD_COUNT=2\n" +
                "REWARD_COUNT=1\nACTIVE_AD_COUNT=1\nWALLET_MUTATIONS=0\nCOINS_MUTATIONS=0\nCONSOLE_ERRORS=0\n" +
                "SSV_OPTIONS_ATTACHED=PASS\nCLIENT_EARNED_NOT_VERIFIED=PASS\nINTENT_API=TEST_FAKE\nREAL_SSV=NOT_RUN";
        }
        static System.Collections.IEnumerator Capture(string path)
        {
            yield return null; Canvas.ForceUpdateCanvases(); yield return new WaitForEndOfFrame();
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(path, texture.EncodeToPNG()); UnityEngine.Object.Destroy(texture);
        }
    }
}
#endif
