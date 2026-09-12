#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Domino.Identity;
using Domino.Infrastructure;
using Domino.Infrastructure.Api;
using Domino.Infrastructure.Firebase;
using Domino.Player;
using Domino.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Domino.Editor
{
    // Explicit batch validation; never runs on a normal editor launch or in a player build.
    public static class PlayerProfileValidation
    {
        const string ModeKey = "Domino.G2.Mode";
        static int phase;
        static double deadline;
        static bool busy;
        static string Output => Path.GetFullPath(Path.Combine(Application.dataPath, "../../G2Real"));
        sealed class Auth : IFirebaseClient, IAuthTokenProvider
        {
            readonly IFirebaseClient sdk;
            public Auth(bool real)
            {
                if (real) sdk = (IFirebaseClient)Activator.CreateInstance(typeof(ApplicationServices).Assembly.GetType("Domino.Infrastructure.Firebase.FirebaseSdkClient", true),
                    new object[] { new Func<PlayerIdentity>(() => ApplicationServices.Identity?.Current) });
            }
            public Task<string> CheckDependenciesAsync() => sdk?.CheckDependenciesAsync() ?? Task.FromResult("Available");
            public void InitializeApp() => sdk?.InitializeApp();
            public PlayerIdentity GetCurrentUser() => sdk == null ? new PlayerIdentity("validation-only", true) :
                sdk.GetCurrentUser() ?? throw new Exception("Existing Guest required");
            public Task<PlayerIdentity> SignInAnonymouslyAsync() => throw new Exception("New Guest forbidden");
            public Task<string> GetIdTokenAsync(bool refresh, CancellationToken ct) => sdk == null ? Task.FromResult("validation-token") : ((IAuthTokenProvider)sdk).GetIdTokenAsync(refresh, ct);
        }
        sealed class Transport : IApiTransport
        {
            public bool Offline, Reserved;
            public string Alias = "Guest-ABCDEFGH";
            public int Writes;
            public async Task<ApiHttpResponse> SendAsync(string method, Uri url, string json, string token, int seconds, CancellationToken ct)
            {
                await Task.Delay(100, ct);
                if (Offline) throw new DominoApiException(ApiFailure.Transport);
                if (method == "PUT") {
                    Writes++;
                    if (Reserved) return new ApiHttpResponse(400, "{\"code\":\"DISPLAY_NAME_RESERVED\",\"requestId\":\"00000000-0000-0000-0000-000000000001\"}");
                    Alias = "Fredy92";
                }
                return new ApiHttpResponse(200, "{\"player\":{\"uid\":\"validation-only\",\"accountType\":\"GUEST\",\"displayName\":\"" + Alias + "\",\"language\":\"es\",\"status\":\"ACTIVE\"},\"wallet\":{\"coins\":1234}}");
            }
        }
        [InitializeOnLoadMethod] static void Register()
        {
            int mode = SessionState.GetInt(ModeKey, 0); if (mode == 0) return;
            ApplicationServices.ValidationFirebaseFactory = () => new Auth(mode != 1);
            deadline = EditorApplication.timeSinceStartup + 600;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            Application.logMessageReceived += (message, stack, type) => {
                if (SessionState.GetInt(ModeKey, 0) != 0 && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)) Finish(false, "CONSOLE_ERROR");
            };
        }
        public static void RunFake() => Run(1);
        public static void RunReal() => Run(2);
        public static void RunRestart() => Run(3);
        static void Run(int mode)
        {
            if (!Application.isBatchMode) throw new Exception("Isolated batch editor required");
            Directory.CreateDirectory(Output);
            LocalizationAssets.Import();
            SessionState.SetInt(ModeKey, mode); Register();
            EditorSceneManager.OpenScene(ClientEditorTools.ScenePath);
            EditorApplication.isPlaying = true;
        }
        static void Check(bool ok, string reason) { if (!ok) throw new Exception(reason); }
        static async void Tick()
        {
            int mode = SessionState.GetInt(ModeKey, 0); if (mode == 0 || busy) return;
            if (EditorApplication.timeSinceStartup > deadline) { Finish(false, "TIMEOUT"); return; }
            if (!EditorApplication.isPlaying || !DominoLocalization.Ready) return;
            var menu = UnityEngine.Object.FindFirstObjectByType<StartMenuView>(); if (!menu || !menu.Profile) return;
            var profile = menu.Profile; var service = ApplicationServices.Player;
            busy = true;
            try {
                if (mode == 1) { await Fake(profile); Finish(true, "UI_ALIAS_EN_ES=PASS\nPORTRAIT_NINE_SIZES=PASS\nCONSOLE_ERRORS=0"); return; }
                if (phase == 0 && service.IsFresh) {
                    if (mode == 3) { Check(service.Player.DisplayName == "Fredy92" && profile.DisplayedName == "Fredy92", "Restart alias mismatch"); Finish(true, "RESTART_ALIAS_PERSISTED=PASS\nCONSOLE_ERRORS=0"); return; }
                    File.WriteAllText(Path.Combine(Output,"uid.txt"), service.Player.Uid);
                    File.WriteAllText(Path.Combine(Output,"old-name.txt"), service.Player.DisplayName);
                    profile.Open(); State("WAIT_RENAME"); phase = 1;
                }
                if (phase == 1 && Signal("rename")) {
                    profile.AliasInput.text = "Fredy92"; Check(profile.SaveButton.interactable, "Save unavailable");
                    profile.SaveButton.onClick.Invoke(); await Settled(service);
                    Check(!profile.IsOpen && profile.DisplayedName == "Fredy92", "Rename UI failed");
                    Capture("real-renamed"); State("RENAMED"); phase = 2;
                }
                if (phase == 2 && Signal("offline")) {
                    await (Task)typeof(PlayerService).GetMethod("RefreshConfirmedAsync", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(service, new object[]{CancellationToken.None});
                    Check(service.State == PlayerSyncState.FAILED && service.HasConfirmedSnapshots && profile.DisplayedName == "Fredy92", "Cached state lost");
                    profile.Open(); Check(profile.RetryButton.gameObject.activeSelf && !profile.SaveButton.interactable, "Offline controls");
                    Capture("real-offline"); State("OFFLINE"); phase = 3;
                }
                if (phase == 3 && Signal("retry")) {
                    profile.RetryButton.onClick.Invoke(); await Settled(service);
                    Check(service.IsFresh && profile.DisplayedName == "Fredy92", "Retry UI failed");
                    Finish(true,"ALIAS_UI_SAVE=PASS\nOFFLINE_CACHED_UI=PASS\nRETRY_UI=PASS\nCONSOLE_ERRORS=0");
                }
            } catch (Exception error) { Finish(false, error.Message); }
            finally { busy = false; }
        }
        static async Task Settled(PlayerService service)
        {
            double until = EditorApplication.timeSinceStartup + 50;
            while (service.State == PlayerSyncState.SYNCING && EditorApplication.timeSinceStartup < until) await Task.Yield();
            await Task.Delay(150);
        }
        static async Task Fake(PlayerProfileView profile)
        {
            var http = new Transport();
            using var service = new PlayerService(ApplicationServices.Identity, new DominoApiClient(new DominoApiConfiguration(true,"https://example.invalid"), new Auth(false), http,new UnityApiJsonCodec()), () => Task.FromResult("es"), default);
            profile.Bind(service); profile.Open();
            Check(profile.DisplayedCoins == "--" && !profile.SaveButton.interactable, "No fabricated wallet");
            await service.InitializeAsync();
            Check(profile.DisplayedName != service.Player.DisplayName && profile.DisplayedCoins.Contains("1234"), "Generated name prompt or coins");
            int[,] sizes = { {1080,1920},{1080,2160},{1080,2340},{1080,2400},{1080,2520},{1170,2532},{1284,2778},{1600,2560},{1536,2048} };
            var resize = typeof(Phase1Validation).GetMethod("ResizeGameView",BindingFlags.Static | BindingFlags.NonPublic);
            for(int i=0;i<sizes.GetLength(0);i++) {
                resize.Invoke(null,new object[]{sizes[i,0],sizes[i,1]});
                DominoLocalization.Select(i%2==0 ? "es" : "en"); await Task.Delay(350);
                Check(Screen.width == sizes[i,0] && Screen.height == sizes[i,1],"Resolution");
                foreach(var element in new[]{profile.SaveButton.transform, profile.RetryButton.transform,profile.AliasInput.transform}) {
                    var corners = new Vector3[4]; ((RectTransform)element).GetWorldCorners(corners);
                    foreach(var c in corners) Check(Screen.safeArea.Contains(new Vector2(c.x,c.y)), "Profile outside safe area");
                }
                foreach(var label in profile.AliasInput.transform.parent.GetComponentsInChildren<Text>())
                    Check(label.preferredHeight <= label.rectTransform.rect.height + 2,"Text overflow: " + label.name);
                if(i==0) { Capture("profile-es"); await Task.Delay(400); }
            }
            profile.AliasInput.text = "bad value"; Check(!profile.SaveButton.interactable,"Invalid UI input");
            http.Reserved=true; profile.AliasInput.text="Fredy92"; profile.SaveButton.onClick.Invoke(); await Settled(service);
            Check(profile.IsOpen && service.Error.ServerErrorCode=="DISPLAY_NAME_RESERVED", "Reserved keeps panel");
            http.Reserved=false; profile.SaveButton.onClick.Invoke(); profile.SaveButton.onClick.Invoke(); await Settled(service);
            Check(!profile.IsOpen && profile.DisplayedName=="Fredy92" && http.Writes==2, "Save and double tap");
            http.Offline=true;
            await (Task)typeof(PlayerService).GetMethod("RefreshConfirmedAsync",BindingFlags.Instance | BindingFlags.NonPublic).Invoke(service,new object[]{CancellationToken.None});
            profile.Open(); Check(profile.DisplayedName=="Fredy92" && profile.DisplayedCoins.Contains("1234") && profile.RetryButton.gameObject.activeSelf,"Offline cached UI");
            http.Offline=false; profile.RetryButton.onClick.Invoke(); await Settled(service);
            Check(service.IsFresh && profile.DisplayedName=="Fredy92","Retry UI");
        }
        static bool Signal(string name) => File.Exists(Path.Combine(Output,name+".signal"));
        static void State(string value) => File.WriteAllText(Path.Combine(Output,"state.txt"),value);
        static void Capture(string name) => UnityEngine.Object.FindFirstObjectByType<StartMenuView>().StartCoroutine(CaptureFrame(name));
        static System.Collections.IEnumerator CaptureFrame(string name)
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return new WaitForEndOfFrame();
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(Output,name+".png"), texture.EncodeToPNG());
            UnityEngine.Object.Destroy(texture);
        }
        static void Finish(bool success,string detail)
        {
            int mode = SessionState.GetInt(ModeKey,0); SessionState.SetInt(ModeKey,0);
            File.WriteAllText(Path.Combine(Output,"result-"+mode+".txt"),(success?"PASS\n":"FAIL\n")+detail);
            EditorApplication.Exit(success?0:1);
        }
    }
}
#endif
