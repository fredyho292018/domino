using System;
using System.Threading;
using System.Threading.Tasks;
using Domino.Identity;
using Domino.Infrastructure.Firebase;
using Domino.Infrastructure.Api;
using Domino.Player;
using Domino.UI;
using UnityEngine.Localization.Settings;
using UnityEngine;

namespace Domino.Infrastructure
{
    // One composition root per Play/application lifetime, independent of scene reloads.
    public static class ApplicationServices
    {
        static CancellationTokenSource lifetime;
        public static IPlayerIdentityService Identity { get; private set; }
        public static FirebaseBootstrap Firebase { get; private set; }
        public static PlayerService Player { get; private set; }
#if UNITY_EDITOR
        // Opt-in editor validation only; never compiled into a player build.
        public static Func<IFirebaseClient> ValidationFirebaseFactory;
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            Shutdown(); Application.quitting -= Shutdown;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.playModeStateChanged -= OnEditorPlayMode;
#endif
            Identity = null; Firebase = null; Player = null;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Start()
        {
            if (Identity != null) return;
            lifetime = new CancellationTokenSource();
            IFirebaseClient client = new FirebaseSdkClient(() => Identity?.Current);
#if UNITY_EDITOR
            client = ValidationFirebaseFactory?.Invoke() ?? client;
#endif
            Firebase = new FirebaseBootstrap(client, Debug.Log, lifetime.Token);
            Identity = new FirebaseAuthService(Firebase, client, Debug.Log, lifetime.Token);
            var asset = Resources.Load<DominoApiSettings>("ApiSettings");
            var settings = asset ? asset.Configuration : new DominoApiConfiguration(false, "");
            var api = new DominoApiClient(settings, (IAuthTokenProvider)client, new UnityApiTransport(), new UnityApiJsonCodec());
            Player = new PlayerService(Identity, api, CurrentLanguageAsync, lifetime.Token, Debug.Log);
            Application.quitting += Shutdown;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.playModeStateChanged += OnEditorPlayMode;
#endif
            // Unity's main-thread entry point and captured UnitySynchronizationContext
            // keep SDK continuations/logging on main. Offline presentation proceeds independently.
            _ = ObserveAsync(Identity, lifetime.Token);
            _ = Player.InitializeAsync();
        }
        static async Task<string> CurrentLanguageAsync()
        {
            await LocalizationSettings.InitializationOperation.Task;
            return DominoLocalization.Language;
        }
        static async Task ObserveAsync(IPlayerIdentityService service, CancellationToken token)
        {
            try { await service.InitializeAsync(); }
            catch (Exception)
            {
                if (!token.IsCancellationRequested) Debug.LogWarning("[AUTH] Initialization unavailable; offline play remains available.");
            }
        }
        static void Shutdown()
        {
            lifetime?.Cancel(); lifetime?.Dispose(); lifetime = null;
            // Firebase owns persistence. Never SignOut or delete its cache here.
        }
#if UNITY_EDITOR
        static void OnEditorPlayMode(UnityEditor.PlayModeStateChange state)
        {
            if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode) Shutdown();
        }
#endif
    }
}
