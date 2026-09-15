using System;
using System.Threading;
using System.Threading.Tasks;
using Domino.Identity;
using Domino.Infrastructure.Firebase;
using Domino.Infrastructure.Api;
using Domino.Player;
using Domino.Realtime;
using Domino.Ads;
using Domino.UI;
using UnityEngine.Localization.Settings;
using UnityEngine;

namespace Domino.Infrastructure
{
    // One composition root per Play/application lifetime, independent of scene reloads.
    public static class ApplicationServices
    {
        static CancellationTokenSource lifetime;
        static GameObject lifecycleObject;
        public static IPlayerIdentityService Identity { get; private set; }
        public static FirebaseBootstrap Firebase { get; private set; }
        public static PlayerService Player { get; private set; }
        public static IRealtimeConnectionService Realtime { get; private set; }
        public static Domino.Online.IOnlineMatchApi OnlineApi { get; private set; }
        public static IAdsService Ads { get; private set; }
        public static IRewardedAdsService Rewarded { get; private set; }
        public static RewardVerificationService RewardVerification { get; private set; }
        public static Domino.Rewards.RoundRewardFlow RoundRewards { get; private set; }
        public static Domino.Rewards.IMonetizationPolicyService Monetization { get; private set; }
        public static Domino.Catalog.GameCatalogService GameCatalog { get; private set; }
#if UNITY_EDITOR
        public static EditorMockAdsConsent EditorAdsConsent { get; private set; }
        // Opt-in editor validation only; never compiled into a player build.
        public static Func<IFirebaseClient> ValidationFirebaseFactory;
        public static Func<IRewardIntentApi> ValidationRewardIntentApiFactory;
        public static Func<IDominoApiClient> ValidationPlayerApiFactory;
        public static Func<RewardVerificationService, IRewardedAdsService> ValidationRewardedFactory;
        public static Func<Domino.Rewards.IMonetizationPolicyApi> ValidationMonetizationFactory;
        public static Func<IAuthTokenProvider, Domino.Catalog.GameCatalogService> ValidationGameCatalogFactory;
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            Shutdown(); Application.quitting -= Shutdown;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.playModeStateChanged -= OnEditorPlayMode;
#endif
            Identity = null; Firebase = null; Player = null; Realtime = null; Ads = null; Rewarded = null;
            RewardVerification = null;
            RoundRewards = null;
            Monetization = null;
            GameCatalog = null;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Start()
        {
            if (Identity != null) return;
            lifetime = new CancellationTokenSource();
            var adsAsset = Resources.Load<DominoAdsSettings>("AdsSettings");
            var adsConfiguration = adsAsset ? adsAsset.Configuration :
                new AdsConfiguration(false, AdsEnvironment.DEVELOPMENT, AdsPlatform.Unsupported, false);
            IAdsConsentGate adsConsent = new PendingAdsConsent();
#if UNITY_EDITOR
            EditorAdsConsent = new EditorMockAdsConsent();
            adsConsent = EditorAdsConsent;
#endif
            Ads = new GoogleMobileAdsService(adsConfiguration, adsConsent, new UnityGoogleAdsSdk(), Debug.Log);
            IFirebaseClient client = new FirebaseSdkClient(() => Identity?.Current);
#if UNITY_EDITOR
            client = ValidationFirebaseFactory?.Invoke() ?? client;
#endif
            Firebase = new FirebaseBootstrap(client, Debug.Log, lifetime.Token);
            Identity = new FirebaseAuthService(Firebase, client, Debug.Log, lifetime.Token);
            var asset = Resources.Load<DominoApiSettings>("ApiSettings");
            var settings = asset ? asset.Configuration : new DominoApiConfiguration(false, "");
            try {
                var bundled = Resources.Load<TextAsset>("GameCatalogFallback");
                GameCatalog = new Domino.Catalog.GameCatalogService(
                    new Domino.Catalog.GameCatalogApi(settings, (IAuthTokenProvider)client, new UnityApiTransport()),
                    new Domino.Catalog.FileGameCatalogCache(System.IO.Path.Combine(Application.persistentDataPath, "game-catalog-v1.json")),
                    bundled ? bundled.text : "", lifetime.Token, Debug.Log);
#if UNITY_EDITOR
                if (ValidationGameCatalogFactory != null) GameCatalog = ValidationGameCatalogFactory((IAuthTokenProvider)client);
#endif
            } catch { Debug.LogWarning("[GAME-CATALOG] initialization unavailable; no valid bundled catalog"); }
            IDominoApiClient api = new DominoApiClient(settings, (IAuthTokenProvider)client, new UnityApiTransport(), new UnityApiJsonCodec());
            var rewardHttp = new RewardIntentApiClient(settings, (IAuthTokenProvider)client, new UnityApiTransport(), new UnityRewardIntentCodec());
            IRewardIntentApi rewardApi = rewardHttp;
            Domino.Rewards.IMonetizationPolicyApi monetizationApi = new Domino.Rewards.MonetizationPolicyApi(rewardHttp);
#if UNITY_EDITOR
            rewardApi = ValidationRewardIntentApiFactory?.Invoke() ?? rewardApi;
            api = ValidationPlayerApiFactory?.Invoke() ?? api;
            monetizationApi = ValidationMonetizationFactory?.Invoke() ?? monetizationApi;
#endif
            Player = new PlayerService(Identity, api, CurrentLanguageAsync, lifetime.Token, Debug.Log);
            Monetization = new Domino.Rewards.MonetizationPolicyService(monetizationApi, lifetime.Token);
            RewardVerification = new RewardVerificationService(rewardApi, lifetime.Token, new PlayerRewardWalletReceiver(Player));
            Rewarded = new GoogleRewardedAdsService(adsConfiguration, Ads, adsConsent, new UnityRewardedAdLoader(), Debug.Log,
                intents: RewardVerification, remoteGate: () => Monetization.AllowAds);
#if UNITY_EDITOR
            if (ValidationRewardedFactory != null) { Rewarded.Dispose(); Rewarded = ValidationRewardedFactory(RewardVerification); }
#endif
            Rewarded = new Domino.Rewards.PolicyRewardedAds(Rewarded, Monetization);
            RoundRewards = new Domino.Rewards.RoundRewardFlow(Rewarded, RewardVerification,
                () => Player.HasConfirmedSnapshots && Player.State == PlayerSyncState.SYNCED, lifetime.Token,
                recoveryReady: () => Player.HasConfirmedSnapshots, policy: Monetization);
            Realtime = new RealtimeConnectionService(new RealtimeConfiguration(settings), Identity, (IAuthTokenProvider)client);
            OnlineApi = new Domino.Online.OnlineMatchApi(settings, (IAuthTokenProvider)client, new UnityApiTransport());
            var lifecycle = new GameObject("Realtime lifecycle");
            lifecycleObject = lifecycle;
            UnityEngine.Object.DontDestroyOnLoad(lifecycle);
            lifecycle.AddComponent<RealtimeLifecycle>();
            lifecycle.AddComponent<RewardConfirmationToast>().Initialize(RoundRewards);
            lifecycle.AddComponent<MonetizationLifecycle>().Initialize(Monetization, RoundRewards);
            Realtime.Start();
            Application.quitting += Shutdown;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.playModeStateChanged += OnEditorPlayMode;
#endif
            // Unity's main-thread entry point and captured UnitySynchronizationContext
            // keep SDK continuations/logging on main. Offline presentation proceeds independently.
            _ = ObserveAsync(Identity, lifetime.Token);
            _ = InitializePlayerAndRecoverAsync();
        }
        static async Task InitializePlayerAndRecoverAsync()
        {
            var player = Player; var policy = Monetization; var rewarded = Rewarded; var rounds = RoundRewards;
            var token = lifetime.Token;
            await player.InitializeAsync();
            if (!token.IsCancellationRequested && player.HasConfirmedSnapshots) {
                await policy.RefreshAsync();
                if (token.IsCancellationRequested) return;
                await rewarded.InitializeAsync();
                if (token.IsCancellationRequested) return;
                await rounds.RecoverAsync();
            }
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Explicit development tool only. Never reachable in a release player.
        public static async Task ResetDevelopmentAnonymousIdentityAsync()
        {
            if (!Domino.Development.DevelopmentAuthentication.CanReset)
                throw new InvalidOperationException("Development authentication unavailable");
            var current = await Identity.InitializeAsync();
            if (!current.IsAnonymous) throw new InvalidOperationException("Anonymous identity required");
            var auth = global::Firebase.Auth.FirebaseAuth.DefaultInstance;
            var previous = current.Uid;
            Reset();
            try
            {
                auth.SignOut();
                var result = await auth.SignInAnonymouslyAsync();
                if (result?.User == null || result.User.UserId == previous)
                    throw new InvalidOperationException("New anonymous identity unavailable");
                Debug.Log("[DEV AUTH] identity changed old=" + Domino.Development.DevelopmentAuthentication.Fingerprint(previous)
                    + " new=" + Domino.Development.DevelopmentAuthentication.Fingerprint(result.User.UserId));
            }
            finally
            {
                Start();
                await UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
            }
            await Player.InitializeAsync();
            if (!Player.HasConfirmedSnapshots) throw new InvalidOperationException("Player bootstrap unavailable");
        }
#endif
        // Menu-triggered refresh changes future resolutions only; active sessions keep their snapshot.
        public static async Task RefreshGameCatalogAsync(bool force = false)
        {
            var catalog = GameCatalog; var identity = Identity;
            if (catalog == null || identity == null) return;
            try { await identity.InitializeAsync(); await catalog.RefreshAsync(force); }
            catch { /* Offline identity cannot prevent use of the already loaded bundled/cache snapshot. */ }
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
            if (lifecycleObject) { UnityEngine.Object.Destroy(lifecycleObject); lifecycleObject = null; }
            RoundRewards?.Dispose();
            Player?.Dispose();
            Rewarded?.Dispose();
            Ads?.Dispose();
            Realtime?.Dispose();
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
