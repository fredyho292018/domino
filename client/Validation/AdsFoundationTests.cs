using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Domino.Ads;

static class AdsFoundationTests
{
    static int checks;
    static void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; }
    sealed class Gate : IAdsConsentGate { public bool CanInitializeAds { get; set; } }
    sealed class Sdk : IAdsSdk
    {
        public int Calls;
        public TaskCompletionSource<bool> Completion = new TaskCompletionSource<bool>();
        public Task InitializeAsync() { Calls++; return Completion.Task; }
    }
    static async Task Main()
    {
        const string production = "ca-app-pub-1111111111111111/2222222222"; // Synthetic configuration only; never sent.
        foreach (var platform in new[] { AdsPlatform.Android, AdsPlatform.Ios })
        {
            var dev = new AdsConfiguration(true, AdsEnvironment.DEVELOPMENT, platform, true, production, production);
            Check(dev.IsAvailable, "development available");
            Check(dev.RewardedUnitId == (platform == AdsPlatform.Android ? AdsConfiguration.AndroidDemoRewarded : AdsConfiguration.IosDemoRewarded), "demo selected");
            Check(!new AdsConfiguration(true, AdsEnvironment.DEVELOPMENT, platform, false).IsAvailable, "development release guard");
            Check(!new AdsConfiguration(true, AdsEnvironment.PRODUCTION, platform, true, production, production).IsAvailable, "production editor guard");
            Check(new AdsConfiguration(true, AdsEnvironment.PRODUCTION, platform, false, production, production).RewardedUnitId == production, "production selection");
            foreach (var bad in new[] { "", "invalid", "ca-app-pub-1111111111111111~2222222222", AdsConfiguration.AndroidDemoRewarded, AdsConfiguration.IosDemoRewarded })
                Check(!new AdsConfiguration(true, AdsEnvironment.PRODUCTION, platform, false, bad, bad).IsAvailable, "invalid production rejected");
        }
        var config = new AdsConfiguration(true, AdsEnvironment.DEVELOPMENT, AdsPlatform.Android, true);
        var sdk = new Sdk(); var gate = new Gate(); var logs = new List<string>();
        var service = new GoogleMobileAdsService(config, gate, sdk, logs.Add);
        await service.InitializeAsync();
        Check(sdk.Calls == 0 && service.State == AdsState.NOT_INITIALIZED && service.UnavailableReason == "CONSENT_PENDING", "consent before SDK");
        gate.CanInitializeAds = true;
        var first = service.InitializeAsync(); var second = service.InitializeAsync();
        Check(ReferenceEquals(first, second) && sdk.Calls == 1 && service.State == AdsState.INITIALIZING, "single flight");
        sdk.Completion.SetResult(true); await first;
        Check(service.State == AdsState.READY, "ready");
        await service.InitializeAsync(); Check(sdk.Calls == 1, "once");
        Check(logs.Contains("[ADS] initialization starting") && logs.Contains("[ADS] initialization ready"), "safe success logs");
        var failed = new Sdk(); var failure = new GoogleMobileAdsService(config, gate, failed, logs.Add);
        var pending = failure.InitializeAsync(); failed.Completion.SetException(new Exception("PRIVATE_DIAGNOSTIC")); await pending;
        Check(failure.State == AdsState.FAILED && !string.Join("", logs).Contains("PRIVATE_DIAGNOSTIC"), "nonfatal sanitized failure");
        await failure.InitializeAsync(); Check(failed.Calls == 1, "no failure loop");
        var disabledSdk = new Sdk();
        var disabled = new GoogleMobileAdsService(new AdsConfiguration(false, AdsEnvironment.DEVELOPMENT, AdsPlatform.Android, true), gate, disabledSdk);
        await disabled.InitializeAsync(); Check(disabled.State == AdsState.DISABLED && disabledSdk.Calls == 0, "disabled");
        var invalid = new GoogleMobileAdsService(new AdsConfiguration(true, AdsEnvironment.PRODUCTION, AdsPlatform.Android, false), gate, disabledSdk);
        await invalid.InitializeAsync(); Check(invalid.State == AdsState.FAILED && disabledSdk.Calls == 0, "invalid no sdk");
        var disposed = new GoogleMobileAdsService(config, gate, disabledSdk); disposed.Dispose();
        await disposed.InitializeAsync(); Check(disabledSdk.Calls == 0, "disposed no sdk");
        var lateSdk = new Sdk(); var late = new GoogleMobileAdsService(config, gate, lateSdk);
        var lateTask = late.InitializeAsync(); late.Dispose(); lateSdk.Completion.SetResult(true); await lateTask;
        Check(late.State != AdsState.READY, "late callback ignored");
        Console.WriteLine("ADS_FOUNDATION_TESTS=PASS checks=" + checks);
    }
}
