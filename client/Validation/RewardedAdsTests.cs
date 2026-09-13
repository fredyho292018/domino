using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Domino.Ads;
using Domino.Core;

static class RewardedAdsTests
{
    static int checks;
    static void Check(bool yes, string reason) { checks++; if (!yes) throw new Exception(reason); }
    sealed class Gate : IAdsConsentGate { public bool CanInitializeAds { get; set; } = true; }
    sealed class Init : IAdsService
    {
        public AdsState State { get; set; } = AdsState.READY;
        public string UnavailableReason => null;
        public Task InitializeAsync() => Task.CompletedTask;
        public void Dispose() { }
    }
    sealed class Ad : IRewardedAd
    {
        public bool Valid = true, ThrowShow;
        public int Disposals, Shows;
        public bool CanShow => Valid && Disposals == 0;
        public Action<RewardedCompletionResult> Reward;
        public event Action Opened, Closed, Failed;
        public void Show(Action<RewardedCompletionResult> earned) { Shows++; Reward = earned; if (ThrowShow) throw new Exception("PRIVATE"); Opened?.Invoke(); }
        public void Earn() => Reward?.Invoke(new RewardedCompletionResult("test", 100000));
        public void Close() => Closed?.Invoke();
        public void Fail() => Failed?.Invoke();
        public string Intent;
        public void SetVerificationIntent(string id) { Intent = id; }
        public void Dispose() { Disposals++; }
    }
    sealed class Loader : IRewardedAdLoader
    {
        public int Calls; public string Unit;
        public TaskCompletionSource<IRewardedAd> Pending;
        public Task<IRewardedAd> LoadAsync(string id) { Calls++; Unit = id; Pending = new TaskCompletionSource<IRewardedAd>(); return Pending.Task; }
    }
    sealed class Rig
    {
        public readonly Gate Gate = new Gate(); public readonly Init Init = new Init(); public readonly Loader Loader = new Loader();
        public DateTimeOffset Clock = DateTimeOffset.UtcNow;
        public readonly GoogleRewardedAdsService Service;
        public readonly List<string> Logs = new List<string>();
        public int Rewards;
        public Rig(bool enabled = true, bool production = false)
        {
            var config = new AdsConfiguration(enabled, production ? AdsEnvironment.PRODUCTION : AdsEnvironment.DEVELOPMENT,
                AdsPlatform.Android, !production, "ca-app-pub-1111111111111111/2222222222");
            Service = new GoogleRewardedAdsService(config, Init, Gate, Loader, Logs.Add, () => Clock, new IntentAuthorization());
            Service.RewardEarned += _ => Rewards++;
        }
        public async Task<Ad> Ready()
        {
            var task = Service.LoadRewardedAsync(); var ad = new Ad(); Loader.Pending.SetResult(ad); await task; return ad;
        }
        public void Round() => RewardedRoundPreload.Handle(new GameEvent(GameEventType.ROUND_FINISHED), Service);
    }
    sealed class IntentAuthorization : IRewardIntentAuthorization
    {
        public Task<RewardIntentReceipt> CreateAsync(System.Threading.CancellationToken token) =>
            Task.FromResult(new RewardIntentReceipt("12345678-1234-4234-8234-123456789012", "ISSUED", DateTimeOffset.UtcNow.AddMinutes(10)));
        public void ClientEarned(string id) { }
    }
    static async Task Main()
    {
        var disabled = new Rig(false); disabled.Round(); await disabled.Service.InitializeAsync();
        Check(disabled.Service.State == RewardedState.DISABLED && disabled.Loader.Calls == 0, "disabled no load");
        Check(await disabled.Service.ShowRewardedAsync() == RewardedShowResult.Unavailable, "disabled no show");
        var denied = new Rig(); denied.Gate.CanInitializeAds = false; denied.Round(); await denied.Service.InitializeAsync();
        Check(denied.Loader.Calls == 0, "consent denied");
        var prod = new Rig(production:true); await prod.Service.InitializeAsync(); prod.Round(); Check(prod.Loader.Calls == 0, "H2 never production");
        var r = new Rig();
        Check(r.Service.State == RewardedState.NOT_LOADED, "initial state");
        Check(await r.Service.ShowRewardedAsync() == RewardedShowResult.Unavailable, "not ready show");
        RewardedRoundPreload.Handle(new GameEvent(GameEventType.TURN_CHANGED), r.Service); Check(r.Loader.Calls == 0, "only round event");
        r.Round(); var shared = r.Service.LoadRewardedAsync(); r.Round();
        Check(r.Loader.Calls == 1 && r.Service.State == RewardedState.LOADING, "round preload once");
        Check(ReferenceEquals(shared,r.Service.LoadRewardedAsync()), "load single flight");
        Check(r.Loader.Unit == AdsConfiguration.AndroidDemoRewarded, "exact test unit");
        var ad = new Ad(); r.Loader.Pending.SetResult(ad); await shared;
        Check(r.Service.State == RewardedState.READY && r.Service.IsAvailable, "load success");
        r.Round(); Check(r.Loader.Calls == 1 && ad.Shows == 0, "ready round no load or show");
        var shown = r.Service.ShowRewardedAsync();
        Check(r.Service.State == RewardedState.SHOWING && !r.Service.IsAvailable, "showing");
        Check(await r.Service.ShowRewardedAsync() == RewardedShowResult.Unavailable && ad.Shows == 1, "double show blocked");
        r.Round(); Check(r.Loader.Calls == 1, "showing no load");
        ad.Earn(); ad.Earn(); Check(r.Rewards == 1 && r.Service.State == RewardedState.EARNED, "one reward");
        r.Round(); Check(r.Loader.Calls == 1, "earned still fullscreen");
        ad.Close(); Check(await shown == RewardedShowResult.Earned, "earned completion");
        Check(ad.Disposals == 1 && r.Loader.Calls == 2 && r.Service.State == RewardedState.LOADING, "close dispose reload");
        ad.Earn(); ad.Close(); Check(r.Rewards == 1 && r.Loader.Calls == 2, "late callbacks ignored");
        var next = new Ad(); var nextLoad = r.Service.LoadRewardedAsync(); r.Loader.Pending.SetResult(next); await nextLoad;
        Check(r.Service.State == RewardedState.READY && r.Loader.Calls == 2 && r.Rewards == 1 &&
            ad.Disposals == 1 && next.Disposals == 0, "second ready: two loads, one reward, one active ad");
        var skipped = r.Service.ShowRewardedAsync(); next.Close();
        Check(await skipped == RewardedShowResult.Closed && r.Rewards == 1, "close without reward");
        Check(next.Disposals == 1 && r.Loader.Calls == 3, "second cycle dispose");
        r.Service.Dispose(); var late = new Ad(); r.Loader.Pending.SetResult(late); await Task.Yield();
        Check(late.Disposals == 1 && r.Service.State == RewardedState.DISABLED, "late load destroyed");
        r.Round(); Check(r.Loader.Calls == 3, "disposed no new load");
        var fail = new Rig(); var loading = fail.Service.LoadRewardedAsync(); fail.Loader.Pending.SetException(new Exception("PRIVATE")); await loading;
        Check(fail.Service.State == RewardedState.FAILED && fail.Loader.Calls == 1, "load failure no retry loop");
        Check(!string.Join("",fail.Logs).Contains("PRIVATE"), "safe failure log");
        var failAd = await fail.Ready(); var failShow = fail.Service.ShowRewardedAsync(); failAd.Fail();
        Check(await failShow == RewardedShowResult.Failed && failAd.Disposals == 1, "fullscreen failure");
        Check(fail.Loader.Calls == 2, "no show failure loop");
        var throws = await fail.Ready(); throws.ThrowShow = true;
        Check(await fail.Service.ShowRewardedAsync() == RewardedShowResult.Failed && throws.Disposals == 1, "show exception safe");
        var stale = new Rig(); var staleAd = await stale.Ready(); stale.Clock = stale.Clock.AddHours(1);
        Check(await stale.Service.ShowRewardedAsync() == RewardedShowResult.Unavailable, "expired show blocked");
        Check(staleAd.Disposals == 1 && stale.Loader.Calls == 2, "expired reload");
        var invalid = new Rig(); var invalidAd = await invalid.Ready(); invalidAd.Valid = false;
        await invalid.Service.ShowRewardedAsync(); Check(invalidAd.Disposals == 1 && invalid.Loader.Calls == 2, "CanShow invalid reload");
        var revoked = new Rig(); var revokedLoad = revoked.Service.LoadRewardedAsync(); revoked.Gate.CanInitializeAds = false;
        var rejected = new Ad(); revoked.Loader.Pending.SetResult(rejected); await revokedLoad;
        Check(rejected.Disposals == 1 && !revoked.Service.IsAvailable, "consent revoked during load");
        var initFail = new Rig(); initFail.Init.State = AdsState.FAILED; await initFail.Service.InitializeAsync();
        Check(initFail.Service.State == RewardedState.FAILED && initFail.Loader.Calls == 0, "SDK failure no load");
        for (int i=0;i<10;i++)
        {
            var cycle = new Rig(); var cycleAd = await cycle.Ready(); var operation = cycle.Service.ShowRewardedAsync();
            cycleAd.Earn(); cycleAd.Close(); await operation; cycle.Service.Dispose();
            Check(cycleAd.Disposals == 1 && cycle.Rewards == 1, "repeat lifecycle");
        }
        var observer = new Rig(); observer.Service.RewardEarned += _ => throw new Exception("UI");
        var observerAd = await observer.Ready(); var observerShow = observer.Service.ShowRewardedAsync(); observerAd.Earn(); observerAd.Close();
        Check(await observerShow == RewardedShowResult.Earned && observerAd.Disposals == 1, "consumer cannot break cleanup");
        Console.WriteLine("H2_REWARDED_TESTS=PASS CHECKS=" + checks + "; SDK_REQUESTS=0; WALLET_OPERATIONS=0");
    }
}
