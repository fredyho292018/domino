using System;
using System.Threading;
using System.Threading.Tasks;
using GoogleMobileAds.Api;
using UnityEngine;

namespace Domino.Ads
{
    public sealed class UnityRewardedAdLoader : IRewardedAdLoader
    {
#if UNITY_EDITOR
        public static int MockSuccessCallbacks { get; private set; }
        public static int MockFailureCallbacks { get; private set; }
        public static int MockCompletedLoads { get; private set; }
        public static int MockInstancesCreated { get; private set; }
        public static int MockInstancesDisposed { get; private set; }
        public static int MockActiveInstances => MockInstancesCreated - MockInstancesDisposed;
#endif
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static int TestLoadRequests { get; private set; }
        public static int TestShows { get; private set; }
        public static int TestRewardCallbacks { get; private set; }
        public static int RealLoadRequests => 0; // Hard allowlist below precedes the only SDK call.
#endif
        public async Task<IRewardedAd> LoadAsync(string testUnitId)
        {
            var expectedUnit = Application.platform == RuntimePlatform.IPhonePlayer
                ? AdsConfiguration.IosDemoRewarded : AdsConfiguration.AndroidDemoRewarded;
            if (!(Application.isEditor || Debug.isDebugBuild) || testUnitId != expectedUnit)
                throw new InvalidOperationException("TEST_ONLY");
            var main = SynchronizationContext.Current ?? throw new InvalidOperationException("MAIN_THREAD_REQUIRED");
            var completion = new TaskCompletionSource<IRewardedAd>();
            bool abandoned = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            TestLoadRequests++;
#endif
#if UNITY_EDITOR
            int attempt = TestLoadRequests;
            Debug.Log("[ADS] Editor mock load=" + attempt + " method called");
#endif
            RewardedAd.Load(testUnitId, new AdRequest(), (ad, error) => main.Post(_ =>
            {
#if UNITY_EDITOR
                if (error == null && ad != null) MockSuccessCallbacks++; else MockFailureCallbacks++;
                Debug.Log("[ADS] Editor mock load=" + attempt + " callback=" +
                    (error == null && ad != null ? "SUCCESS" : "FAILURE"));
#endif
                if (abandoned || completion.Task.IsCompleted) { ad?.Destroy(); return; }
                if (error != null || ad == null)
                {
                    ad?.Destroy(); completion.TrySetException(new InvalidOperationException("LOAD"));
                }
                else completion.TrySetResult(new Instance(ad, main));
            }, null));
            if (await Task.WhenAny(completion.Task, Task.Delay(TimeSpan.FromSeconds(45))) != completion.Task)
            {
                abandoned = true;
#if UNITY_EDITOR
                MockCompletedLoads++;
                Debug.Log("[ADS] Editor mock load=" + attempt + " completed=TIMEOUT_NO_CALLBACK");
#endif
                throw new TimeoutException("LOAD");
            }
            try { return await completion.Task; }
            finally
            {
#if UNITY_EDITOR
                MockCompletedLoads++;
                Debug.Log("[ADS] Editor mock load=" + attempt + " task completed");
#endif
            }
        }
        sealed class Instance : IRewardedAd
        {
            RewardedAd ad;
            readonly SynchronizationContext main;
            bool rewarded;
            Action<RewardedCompletionResult> reward;
            public bool CanShow => ad != null && ad.CanShowAd();
            public event Action Opened;
            public event Action Closed;
            public event Action Failed;
            public Instance(RewardedAd ad, SynchronizationContext main)
            {
                this.ad = ad; this.main = main;
#if UNITY_EDITOR
                MockInstancesCreated++;
#endif
                ad.OnAdFullScreenContentOpened += OnOpened;
                ad.OnAdFullScreenContentClosed += OnClosed;
                ad.OnAdFullScreenContentFailed += OnFailed;
            }
            void Post(Action action) => main.Post(_ => { if (ad != null) action(); }, null);
            void OnOpened() => Post(() => Opened?.Invoke());
            void OnClosed() => Post(() => Closed?.Invoke());
            void OnFailed(AdError ignored) => Post(() => Failed?.Invoke());
            public void Show(Action<RewardedCompletionResult> earned)
            {
                if (!CanShow) throw new InvalidOperationException("NOT_READY");
                reward = earned;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                TestShows++;
#endif
                ad.Show(value => Post(() =>
                {
                    if (rewarded) return;
                    rewarded = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    TestRewardCallbacks++;
#endif
                    reward?.Invoke(new RewardedCompletionResult(value.Type, value.Amount));
                }));
            }
            public void Dispose()
            {
                if (ad == null) return;
                var old = ad; ad = null;
                old.OnAdFullScreenContentOpened -= OnOpened;
                old.OnAdFullScreenContentClosed -= OnClosed;
                old.OnAdFullScreenContentFailed -= OnFailed;
                reward = null; Opened = null; Closed = null; Failed = null; old.Destroy();
#if UNITY_EDITOR
                MockInstancesDisposed++;
                Debug.Log("[ADS] Editor mock old ad disposed; active=" + MockActiveInstances);
#endif
            }
        }
    }
}
