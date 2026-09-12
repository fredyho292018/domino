#if UNITY_EDITOR
using System;
using System.Threading;
using System.Threading.Tasks;
using Domino.Identity;
using Domino.Infrastructure;
using Domino.Infrastructure.Api;
using Domino.Infrastructure.Firebase;
using Domino.Player;
using UnityEditor;
using UnityEngine;

namespace Domino.Editor
{
    public static class PlayerFoundationValidation
    {
        const string Running = "Domino.PlayerFoundationValidation";
        const string Success = "{\"player\":{\"uid\":\"validation-only\",\"accountType\":\"GUEST\",\"displayName\":\"Guest-ABCDEFGH\",\"language\":\"es\",\"status\":\"ACTIVE\"},\"wallet\":{\"coins\":3000000000}}";
        sealed class FakeFirebase : IFirebaseClient, IAuthTokenProvider
        {
            public Task<string> CheckDependenciesAsync() => Task.FromResult("Available");
            public void InitializeApp() { }
            public PlayerIdentity GetCurrentUser() => new PlayerIdentity("validation-only", true);
            public Task<PlayerIdentity> SignInAnonymouslyAsync() => throw new Exception("Validation must reuse fake identity");
            public Task<string> GetIdTokenAsync(bool refresh, CancellationToken token) => Task.FromResult("validation-token");
        }
        sealed class FakeTransport : IApiTransport
        {
            public string Body;
            public Task<ApiHttpResponse> PostAsync(Uri uri, string json, string token, int seconds, CancellationToken ct)
                => Task.FromResult(new ApiHttpResponse(200, Body));
        }
        [InitializeOnLoadMethod]
        static void Register()
        {
            if (!SessionState.GetBool(Running, false)) return;
            ApplicationServices.ValidationFirebaseFactory = () => new FakeFirebase();
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode) return;
                if (ApplicationServices.Identity?.State != IdentityState.Ready ||
                    ApplicationServices.Player?.State != PlayerSyncState.FAILED || ApplicationServices.Player.Wallet != null)
                    Debug.LogError("PLAYER_OFFLINE_COMPOSITION=FAIL");
                else Debug.Log("PLAYER_OFFLINE_COMPOSITION=PASS; REAL_BACKEND=NO; REAL_FIREBASE=NO");
            };
        }
        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Run validation in its own batch editor.");
            var transport = new FakeTransport { Body = Success };
            var codec = new UnityApiJsonCodec();
            var api = new DominoApiClient(new DominoApiConfiguration(true, "https://example.invalid"), new FakeFirebase(), transport, codec);
            if (codec.Serialize(new PlayerBootstrapRequestDto { language = "es" }) != "{\"language\":\"es\"}" ||
                api.BootstrapAsync("es", default).GetAwaiter().GetResult().wallet.coins != 3000000000L)
                throw new Exception("Unity JSON contract failed");
            int rejected = 0;
            foreach (var body in new[] { "broken", "{}", Success.Replace("GUEST", "UNKNOWN"), Success.Replace("ACTIVE", "UNKNOWN"),
                Success.Replace("3000000000", "-1"), Success.Replace("\"coins\":3000000000", ""), Success.Replace("3000000000", "1.5") })
            {
                transport.Body = body;
                try { api.BootstrapAsync("es", default).GetAwaiter().GetResult(); }
                catch (DominoApiException error) when (error.Category == ApiFailure.Contract) { rejected++; }
            }
            if (rejected != 7) throw new Exception("Unity JSON accepted invalid response: rejected=" + rejected);
            Debug.Log("PLAYER_UNITY_JSON_CHECKS=9 PASS");
            SessionState.SetBool(Running, true);
            Register();
            Phase1Validation.RunPortrait();
        }
    }
}
#endif
