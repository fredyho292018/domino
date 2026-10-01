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
        public static ProductionLogoutService Logout { get; private set; }
        public static event Action SessionReplaced;
        public static ProductionAuthRouter AuthRouter { get; private set; }
        public static Domino.UI.AppShell.ProductionRoutingComposition Routing { get; private set; }
        public static bool UsesProductionAuth { get; private set; }
        public static IPlayerIdentityService Identity { get; private set; }
        public static FirebaseBootstrap Firebase { get; private set; }
        public static PlayerService Player { get; private set; }
        public static IRealtimeConnectionService Realtime { get; private set; }
        public static Domino.Online.IOnlineMatchApi OnlineApi { get; private set; }
        public static Domino.Online.IOnlineMatchApi SocialApi { get; private set; }
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
            AuthRouter = null;
            Routing = null;
            Identity = null; Firebase = null; Player = null; Realtime = null; Ads = null; Rewarded = null;
            RewardVerification = null;
            RoundRewards = null;
            Monetization = null;
            GameCatalog = null;
            SocialApi = null; OnlineApi = null;
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
            UsesProductionAuth = true;
#if UNITY_EDITOR
            UsesProductionAuth = ValidationFirebaseFactory == null;
#endif
            Identity = new FirebaseAuthService(Firebase, client, Debug.Log, lifetime.Token, UsesProductionAuth);
            var asset = Resources.Load<DominoApiSettings>("ApiSettings");
            var settings = asset ? asset.Configuration : new DominoApiConfiguration(false, "");
            if (ValidationNetworkPolicy.Isolated) settings = new DominoApiConfiguration(false, "");

            var transport=new SessionApiTransport(new UnityApiTransport(),lifetime.Token);
            try {
                var bundled = Resources.Load<TextAsset>("GameCatalogFallback");
                GameCatalog = new Domino.Catalog.GameCatalogService(
                    new Domino.Catalog.GameCatalogApi(settings, (IAuthTokenProvider)client, transport),
                    new Domino.Catalog.FileGameCatalogCache(System.IO.Path.Combine(Application.persistentDataPath, "game-catalog-v1.json")),
                    bundled ? bundled.text : "", lifetime.Token, Debug.Log);
#if UNITY_EDITOR
                if (ValidationGameCatalogFactory != null) GameCatalog = ValidationGameCatalogFactory((IAuthTokenProvider)client);
#endif
            } catch { Debug.LogWarning("[GAME-CATALOG] initialization unavailable; no valid bundled catalog"); }
            IDominoApiClient api = new DominoApiClient(settings, (IAuthTokenProvider)client, transport, new UnityApiJsonCodec());
            SocialApi = new Domino.Social.SocialApi(settings, (IAuthTokenProvider)client, transport);
            var rewardHttp = new RewardIntentApiClient(settings, (IAuthTokenProvider)client, transport, new UnityRewardIntentCodec());
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
            OnlineApi = new Domino.Online.OnlineMatchApi(settings, (IAuthTokenProvider)client, transport);
            var lifecycle = lifecycleObject = new GameObject("Realtime lifecycle");
            UnityEngine.Object.DontDestroyOnLoad(lifecycle);
            if(!UsesProductionAuth) StartSessionLifecycles();
            if(!UsesProductionAuth) Realtime.Start();
            Application.quitting += Shutdown;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.playModeStateChanged += OnEditorPlayMode;
#endif
            // Unity's main-thread entry point and captured UnitySynchronizationContext
            // keep SDK continuations/logging on main. Offline presentation proceeds independently.
            if(UsesProductionAuth) {
                Routing = new Domino.UI.AppShell.ProductionRoutingComposition((FirebaseAuthService)Identity, Player,
                    ()=>new OnboardingApiSession(settings,(IAuthTokenProvider)client,transport,()=>Identity?.Current?.Uid,lifetime.Token),()=>DominoLocalization.Language);
                AuthRouter = new ProductionAuthRouter((FirebaseAuthService)Identity, Player, destination:Routing);
                var logoutRouter=AuthRouter;var logoutAuth=(FirebaseAuthService)Identity;
                Logout=new ProductionLogoutService(()=>logoutAuth.Current, PrepareLogoutAsync,
                    ()=>logoutRouter.StopAsync(), Shutdown, logoutAuth.SignOut,
                    ()=>{Reset();Start();SessionReplaced?.Invoke();});
                bool sessionStarted=false;
                AuthRouter.Changed += () => {
                    if(!sessionStarted&&AuthRouter?.Route==ProductionAuthRoute.AppShell){sessionStarted=true;StartSessionLifecycles();Realtime.Start();_ = InitializePlayerAndRecoverAsync();}
                };
            } else {
                _ = ObserveAsync(Identity, lifetime.Token);
                _ = InitializePlayerAndRecoverAsync();
            }
        }
        static void StartSessionLifecycles()
        {
            if(!lifecycleObject || lifecycleObject.GetComponent<RealtimeLifecycle>())return;
            lifecycleObject.AddComponent<RealtimeLifecycle>();
            lifecycleObject.AddComponent<RewardConfirmationToast>().Initialize(RoundRewards);
            lifecycleObject.AddComponent<MonetizationLifecycle>().Initialize(Monetization, RoundRewards);
        }
        static async Task InitializePlayerAndRecoverAsync()
        {
            var expectedPlayer=Player;var expectedLifetime=lifetime;
            var policy=Monetization;var rewarded=Rewarded;var rewards=RoundRewards;
            await expectedPlayer.InitializeAsync();
            if (lifetime == expectedLifetime && lifetime != null && !lifetime.IsCancellationRequested && ReferenceEquals(Player,expectedPlayer) && Player.HasConfirmedSnapshots) {
                await policy.RefreshAsync();
                if(lifetime!=expectedLifetime || expectedLifetime.IsCancellationRequested)return;
                await rewarded.InitializeAsync();
                if(lifetime!=expectedLifetime || expectedLifetime.IsCancellationRequested)return;
                await rewards.RecoverAsync();
            }
        }
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
        // Existing compatibility entry delegates to the same confirmation transaction.
        public static Task LogoutGuestAsync(bool confirmedLossOfAccess)
        {
            if(!confirmedLossOfAccess || Identity?.Current?.IsAnonymous!=true)throw new InvalidOperationException("Guest confirmation required.");
            Logout.Request();return Logout.ConfirmAsync();
        }
        static async Task PrepareLogoutAsync()
        {
            RequireNoActiveGame();
            foreach(var view in UnityEngine.Object.FindObjectsByType<Domino.Online.MatchmakingView>(FindObjectsInactive.Include,FindObjectsSortMode.None)) {
                if(view.Client==null)throw new InvalidOperationException();
                await view.Client.CancelAsync();
                if(view.Client.MatchId!=null || view.Client.State!=Domino.Online.MatchmakingState.IDLE)throw new InvalidOperationException();
                view.Client.Dispose();view.gameObject.SetActive(false);UnityEngine.Object.Destroy(view.gameObject);
            }
            RequireNoActiveGame();
            // Recover remote queue state too; no local view does not prove absence of a queue.
            if(Player?.HasConfirmedSnapshots==true) {
                using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(20));
                var status=await OnlineApi.SendAsync("GET","matchmaking/queue",null,deadline.Token);
                var state=(string)status["state"];
                if(state=="MATCHED")throw new InvalidOperationException();
                if(state!="NOT_QUEUED") {
                    if(state!="QUEUED" && state!="RESERVED")throw new InvalidOperationException();
                    status=await OnlineApi.SendAsync("DELETE","matchmaking/queue",null,deadline.Token);
                    if((string)status["state"]!="NOT_QUEUED")throw new InvalidOperationException();
                }
            }
            RequireNoActiveGame();
        }
        static void RequireNoActiveGame()
        {
            // No invented leave/forfeit command: finish or close gameplay through its own UI first.
            if(UnityEngine.Object.FindObjectsByType<Domino.Online.OnlineMatchController>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length>0)throw new InvalidOperationException();
            foreach(var controller in UnityEngine.Object.FindObjectsByType<Domino.Client.DominoClientController>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(controller.Session!=null)throw new InvalidOperationException();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(UnityEngine.Object.FindObjectsByType<Domino.Online.OnlineEntryView>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length>0)throw new InvalidOperationException();
#endif
        }
        static void Shutdown()
        {
            AuthRouter?.Dispose();
            foreach(var view in UnityEngine.Object.FindObjectsByType<Domino.Social.SocialView>(FindObjectsInactive.Include,FindObjectsSortMode.None)) {view.gameObject.SetActive(false);UnityEngine.Object.Destroy(view.gameObject);}
            foreach(var view in UnityEngine.Object.FindObjectsByType<Domino.Replay.HistoryReplayView>(FindObjectsInactive.Include,FindObjectsSortMode.None)) {view.gameObject.SetActive(false);UnityEngine.Object.Destroy(view.gameObject);}
            if(lifecycleObject){UnityEngine.Object.Destroy(lifecycleObject);lifecycleObject=null;}
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
            if (state == UnityEditor.PlayModeStateChange.EnteredEditMode) ValidationNetworkPolicy.EndValidation();
        }
#endif
    }
}
