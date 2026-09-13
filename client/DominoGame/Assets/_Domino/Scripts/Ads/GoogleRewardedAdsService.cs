using System;
using System.Threading.Tasks;
using System.Threading;

namespace Domino.Ads
{
    // Main-thread service. The SDK adapter marshals every callback before entering here.
    public sealed class GoogleRewardedAdsService : IRewardedAdsService
    {
        readonly AdsConfiguration configuration;
        readonly IAdsService initialization;
        readonly IAdsConsentGate consent;
        readonly IRewardedAdLoader loader;
        readonly Func<DateTimeOffset> now;
        readonly Action<string> log;
        readonly IRewardIntentAuthorization intents;
        readonly Func<bool> remoteGate;
        readonly CancellationTokenSource shutdown = new CancellationTokenSource();
        Task<RewardedShowResult> preparing;
        IRewardedAd current;
        Task load;
        TaskCompletionSource<RewardedShowResult> show;
        DateTimeOffset loadedAt;
        bool disposed, earned;
        public RewardedState State { get; private set; }
        // Reject expired inventory on interaction/preload, with no per-frame polling.
        public bool IsAvailable => !disposed && Allowed && State == RewardedState.READY && Fresh;
        bool Fresh => current != null && now() - loadedAt < TimeSpan.FromMinutes(55) && current.CanShow;
        bool Allowed => remoteGate() && configuration.IsAvailable && configuration.Environment == AdsEnvironment.DEVELOPMENT &&
            IsTestUnit(configuration.RewardedUnitId) && consent.CanInitializeAds;
        public static bool IsTestUnit(string id) => id == AdsConfiguration.AndroidDemoRewarded || id == AdsConfiguration.IosDemoRewarded;
        public event Action<RewardedState> RewardedStateChanged;
        public event Action<bool> RewardedAvailabilityChanged;
        public event Action<RewardedCompletionResult> RewardEarned;

        public GoogleRewardedAdsService(AdsConfiguration configuration, IAdsService initialization,
            IAdsConsentGate consent, IRewardedAdLoader loader, Action<string> log = null, Func<DateTimeOffset> now = null,
            IRewardIntentAuthorization intents = null, Func<bool> remoteGate = null)
        {
            this.configuration = configuration; this.initialization = initialization; this.consent = consent;
            this.loader = loader; this.log = log; this.now = now ?? (() => DateTimeOffset.UtcNow);
            this.intents = intents;
            this.remoteGate = remoteGate ?? (() => true);
            State = configuration.Enabled ? RewardedState.NOT_LOADED : RewardedState.DISABLED;
        }
        public Task InitializeAsync() => LoadRewardedAsync();
        public void PreloadIfNeeded() { _ = LoadRewardedAsync(); }
        public Task LoadRewardedAsync()
        {
            if (disposed || !configuration.Enabled || !Allowed) return Task.CompletedTask;
            if (load != null) return load;
            if (State == RewardedState.SHOWING || State == RewardedState.EARNED) return Task.CompletedTask;
            if (State == RewardedState.READY && Fresh) return Task.CompletedTask;
            Release();
            var completion = new TaskCompletionSource<bool>();
            load = completion.Task;
            _ = LoadCoreAsync(completion);
            return completion.Task;
        }
        async Task LoadCoreAsync(TaskCompletionSource<bool> completion)
        {
            IRewardedAd candidate = null;
            try
            {
                SetState(RewardedState.LOADING);
                await initialization.InitializeAsync();
                if (disposed) return;
                if (!Allowed) { SetState(RewardedState.NOT_LOADED); return; }
                if (initialization.State != AdsState.READY) { LoadFailed("SDK_NOT_READY"); return; }
                log?.Invoke("[ADS] rewarded load starting");
                candidate = await loader.LoadAsync(configuration.RewardedUnitId);
                if (disposed) return;
                if (!Allowed) { SetState(RewardedState.NOT_LOADED); return; }
                if (candidate == null || !candidate.CanShow) { LoadFailed("LOAD"); return; }
                current = candidate; candidate = null; loadedAt = now();
                var captured = current;
                current.Opened += () => { if (current == captured && show != null) log?.Invoke("[ADS] rewarded opened"); };
                current.Closed += () => Finish(captured, false);
                current.Failed += () => Finish(captured, true);
                SetState(RewardedState.READY);
                log?.Invoke("[ADS] rewarded ready");
            }
            catch (Exception) { if (!disposed) LoadFailed("LOAD"); }
            finally { SafeDispose(candidate); load = null; completion.TrySetResult(true); }
        }
        public Task<RewardedShowResult> ShowRewardedAsync()
        {
            if (!disposed && Allowed && State == RewardedState.READY && !Fresh) PreloadIfNeeded();
            if (preparing != null || !IsAvailable || intents == null)
                return Task.FromResult(RewardedShowResult.Unavailable);
            var completion = new TaskCompletionSource<RewardedShowResult>();
            preparing = completion.Task;
            _ = PrepareAsync(completion);
            return completion.Task;
        }
        async Task PrepareAsync(TaskCompletionSource<RewardedShowResult> completion)
        {
            var captured = current;
            try
            {
                var intent = await intents.CreateAsync(shutdown.Token);
                if (disposed || current != captured || !IsAvailable || intent == null ||
                    intent.Status != "ISSUED" || intent.ExpiresAt <= now())
                { completion.TrySetResult(RewardedShowResult.Unavailable); return; }
                captured.SetVerificationIntent(intent.IntentId);
                completion.TrySetResult(await ShowPreparedAsync(intent.IntentId));
            }
            catch
            {
                log?.Invoke("[ADS] rewarded unavailable reason=REWARD_INTENT");
                completion.TrySetResult(RewardedShowResult.Unavailable);
            }
            finally { preparing = null; }
        }
        Task<RewardedShowResult> ShowPreparedAsync(string intentId)
        {
            if (!disposed && Allowed && State == RewardedState.READY && !Fresh) PreloadIfNeeded();
            if (!IsAvailable) return Task.FromResult(RewardedShowResult.Unavailable);
            earned = false;
            var completion = new TaskCompletionSource<RewardedShowResult>();
            show = completion;
            var captured = current;
            SetState(RewardedState.SHOWING);
            try
            {
                log?.Invoke("[ADS] rewarded show starting");
                captured.Show(result =>
                {
                    if (disposed || current != captured || show == null || earned) return;
                    earned = true;
                    // EARNED lasts until the same fullscreen instance closes; no second load/show.
                    SetState(RewardedState.EARNED);
                    intents.ClientEarned(intentId);
                    log?.Invoke("[ADS] rewarded client completion received");
                    log?.Invoke("[ADS] rewarded earned");
                    Publish(RewardEarned, result);
                });
            }
            catch (Exception) { Finish(captured, true); }
            return completion.Task;
        }
        void Finish(IRewardedAd ad, bool failed)
        {
            if (disposed || current != ad || show == null) return;
            var completion = show; show = null;
            var result = failed ? RewardedShowResult.Failed : earned ? RewardedShowResult.Earned : RewardedShowResult.Closed;
            Release();
            SetState(failed ? RewardedState.FAILED : RewardedState.NOT_LOADED);
            log?.Invoke(failed ? "[ADS] rewarded show failed category=FULLSCREEN" : "[ADS] rewarded closed");
            completion.TrySetResult(result);
            // A failed open waits for a future explicit/round retry, avoiding failure loops.
            if (!failed) PreloadIfNeeded();
        }
        void LoadFailed(string category) { Release(); SetState(RewardedState.FAILED); log?.Invoke("[ADS] rewarded load failed category=" + category); }
        void Release() { var old = current; current = null; SafeDispose(old); }
        static void SafeDispose(IRewardedAd ad) { try { ad?.Dispose(); } catch (Exception) { } }
        void SetState(RewardedState value)
        {
            if (State == value) return;
            State = value; Publish(RewardedStateChanged, value); Publish(RewardedAvailabilityChanged, IsAvailable);
        }
        static void Publish<T>(Action<T> handlers, T value)
        {
            if (handlers == null) return;
            foreach (Action<T> handler in handlers.GetInvocationList())
                try { handler(value); } catch (Exception) { /* Presentation consumers cannot break ad cleanup. */ }
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; Release(); show?.TrySetResult(RewardedShowResult.Unavailable); show = null;
            shutdown.Cancel(); shutdown.Dispose();
            RewardedStateChanged = null; RewardedAvailabilityChanged = null; RewardEarned = null;
            State = RewardedState.DISABLED;
        }
    }
}
