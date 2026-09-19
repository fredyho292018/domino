#if UNITY_EDITOR
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Domino.Identity;
using Domino.Infrastructure;
using Domino.Infrastructure.Firebase;
using Domino.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Domino.Editor
{
    // Explicit editor actions only; no debug trigger exists in player builds.
    public static class PlayerRetryValidation
    {
        const string Running = "Domino.G1.RealRunning";
        static double deadline;
        static int phase;
        static string uid;
        static string Output => Path.GetFullPath(Path.Combine(Application.dataPath, "../../G1Real"));
        [MenuItem("Domino/Player/Retry synchronization")]
        static async void Retry()
        {
            if (ApplicationServices.Player != null) await ApplicationServices.Player.RetryAsync();
        }
        [MenuItem("Domino/Player/Retry synchronization", true)]
        static bool CanRetry() => EditorApplication.isPlaying && ApplicationServices.Player != null && ApplicationServices.Player.CanRetry;

        sealed class ExistingGuestOnly : IFirebaseClient, IAuthTokenProvider
        {
            readonly IFirebaseClient client;
            public ExistingGuestOnly()
            {
                var type = typeof(ApplicationServices).Assembly.GetType("Domino.Infrastructure.Firebase.FirebaseSdkClient", true);
                client = (IFirebaseClient)Activator.CreateInstance(type, new object[] { new Func<PlayerIdentity>(() => ApplicationServices.Identity?.Current) });
            }
            public Task<string> CheckDependenciesAsync() => client.CheckDependenciesAsync();
            public void InitializeApp() => client.InitializeApp();
            public PlayerIdentity GetCurrentUser()
            {
                var user = client.GetCurrentUser();
                if (user == null || !user.IsAnonymous) throw new InvalidOperationException("Existing anonymous user required for validation.");
                return user;
            }
            public Task<PlayerIdentity> SignInAnonymouslyAsync() => throw new InvalidOperationException("Creating a Guest is forbidden in this validation.");
            public Task<string> GetIdTokenAsync(bool refresh, CancellationToken token) => ((IAuthTokenProvider)client).GetIdTokenAsync(refresh, token);
        }
        [InitializeOnLoadMethod]
        static void Register()
        {
            if (!SessionState.GetBool(Running, false)) return;
            ApplicationServices.ValidationFirebaseFactory = () => new ExistingGuestOnly();
            deadline = EditorApplication.timeSinceStartup + 300;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            Application.logMessageReceived -= OnLog; Application.logMessageReceived += OnLog;
        }
        public static void RunReal()
        {
            Domino.Infrastructure.ValidationNetworkPolicy.AuthorizeReal();
            if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch editor for real validation.");
            Directory.CreateDirectory(Output);
            if (File.Exists(Path.Combine(Output, "retry.signal")) || File.Exists(Path.Combine(Output, "finish.signal")))
                throw new InvalidOperationException("Use a fresh validation signal directory.");
            SessionState.SetBool(Running, true); Register();
            EditorSceneManager.OpenScene(ClientEditorTools.ScenePath);
            EditorApplication.isPlaying = true;
        }
        static void OnLog(string message, string stack, LogType type)
        {
            if (SessionState.GetBool(Running, false) && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
                Finish(false, "CONSOLE_ERROR");
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Running, false)) return;
            if (EditorApplication.timeSinceStartup > deadline) { Finish(false, "TIMEOUT"); return; }
            if (!EditorApplication.isPlaying) return;
            var identity = ApplicationServices.Identity;
            var service = ApplicationServices.Player;
            if (identity?.State == IdentityState.Failed) { Finish(false, "EXISTING_IDENTITY_UNAVAILABLE"); return; }
            if (identity?.Current == null || service == null) return;
            if (phase == 0 && service.State == PlayerSyncState.FAILED)
            {
                if (service.Error?.Category != Infrastructure.Api.ApiFailure.Transport) { Finish(false, "EXPECTED_TRANSPORT_FAILURE"); return; }
                uid = identity.Current.Uid;
                File.WriteAllText(Path.Combine(Output, "uid.txt"), uid); // UID is not a credential; never write the token.
                File.WriteAllText(Path.Combine(Output, "state.txt"), "WAITING_BACKEND\nINITIAL=FAILED_TRANSPORT\n");
                phase = 1;
            }
            // File signals coordinate this opt-in test only; production PlayerService never polls.
            if (phase == 1 && File.Exists(Path.Combine(Output, "retry.signal"))) { phase = 2; _ = RetryRealAsync(service); }
            if (phase == 3 && File.Exists(Path.Combine(Output, "finish.signal"))) Finish(true, "SAME_SESSION_RETRY=PASS\nCONSOLE_ERRORS=0");
        }
        static async Task RetryRealAsync(PlayerService service)
        {
            await service.RetryAsync();
            if (!SessionState.GetBool(Running, false)) return;
            if (!service.IsFresh || service.Player.Uid != uid || ApplicationServices.Identity.Current?.Uid != uid)
            { Finish(false, "RETRY_FAILED_" + service.Error?.Category); return; }
            var result = new Result { uid = service.Player.Uid, displayName = service.Player.DisplayName, coins = service.Wallet.Coins };
            File.WriteAllText(Path.Combine(Output, "result.json"), JsonUtility.ToJson(result));
            File.WriteAllText(Path.Combine(Output, "state.txt"), "SYNCED\nSAME_UID=PASS\nSTOP_PLAY_REQUIRED=NO\n");
            phase = 3;
        }
        [Serializable] sealed class Result { public string uid; public string displayName; public long coins; }
        static void Finish(bool success, string detail)
        {
            SessionState.SetBool(Running, false);
            File.WriteAllText(Path.Combine(Output, "final.txt"), (success ? "PASS\n" : "FAIL\n") + detail);
            EditorApplication.Exit(success ? 0 : 1);
        }
    }
}
#endif
