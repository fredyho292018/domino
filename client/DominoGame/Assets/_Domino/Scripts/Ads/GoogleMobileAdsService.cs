using System;
using System.Threading.Tasks;

namespace Domino.Ads
{
    public enum AdsState { DISABLED, NOT_INITIALIZED, INITIALIZING, READY, FAILED }
    public interface IAdsConsentGate { bool CanInitializeAds { get; } }
    // H1 deliberately has no consent flow. A future UMP adapter must replace this gate.
    public sealed class PendingAdsConsent : IAdsConsentGate { public bool CanInitializeAds => false; }
    public interface IAdsSdk { Task InitializeAsync(); }
    public interface IAdsService : IDisposable
    {
        AdsState State { get; }
        string UnavailableReason { get; }
        Task InitializeAsync();
    }

    // Composition and callers use Unity's main thread. No dependency on identity or economy.
    public sealed class GoogleMobileAdsService : IAdsService
    {
        readonly AdsConfiguration configuration;
        readonly IAdsConsentGate consent;
        readonly IAdsSdk sdk;
        readonly Action<string> log;
        Task initialization;
        bool disposed;
        public AdsState State { get; private set; }
        public string UnavailableReason { get; private set; }

        public GoogleMobileAdsService(AdsConfiguration configuration, IAdsConsentGate consent,
            IAdsSdk sdk, Action<string> log = null)
        {
            this.configuration = configuration;
            this.consent = consent;
            this.sdk = sdk;
            this.log = log;
            State = configuration.Enabled ? AdsState.NOT_INITIALIZED : AdsState.DISABLED;
        }

        public Task InitializeAsync()
        {
            if (disposed || State == AdsState.DISABLED) return Task.CompletedTask;
            if (initialization != null) return initialization;
            if (!configuration.IsAvailable)
            {
                State = AdsState.FAILED;
                UnavailableReason = "CONFIGURATION";
                log?.Invoke("[ADS] initialization failed category=CONFIGURATION");
                return initialization = Task.CompletedTask;
            }
            if (!consent.CanInitializeAds)
            {
                UnavailableReason = "CONSENT_PENDING";
                return Task.CompletedTask;
            }
            // Reserve the task before entering SDK callbacks (including synchronous callbacks).
            var completion = new TaskCompletionSource<bool>();
            initialization = completion.Task;
            _ = InitializeCoreAsync(completion);
            return initialization;
        }

        async Task InitializeCoreAsync(TaskCompletionSource<bool> completion)
        {
            try
            {
                State = AdsState.INITIALIZING;
                UnavailableReason = null;
                log?.Invoke("[ADS] initialization starting");
                await sdk.InitializeAsync();
                if (!disposed) { State = AdsState.READY; log?.Invoke("[ADS] initialization ready"); }
            }
            catch (Exception)
            {
                if (!disposed)
                {
                    State = AdsState.FAILED;
                    UnavailableReason = "SDK_INITIALIZATION";
                    log?.Invoke("[ADS] initialization failed category=SDK_INITIALIZATION");
                }
            }
            finally { completion.TrySetResult(true); }
        }

        public void Dispose() { disposed = true; }
    }
}
