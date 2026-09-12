using System;
using System.Threading.Tasks;
using GoogleMobileAds.Api;

namespace Domino.Ads
{
    public sealed class UnityGoogleAdsSdk : IAdsSdk
    {
        // SDK initialization belongs to the application domain, not to a scene/service instance.
        static Task sharedInitialization;
        public Task InitializeAsync()
        {
            if (sharedInitialization != null) return sharedInitialization;
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            sharedInitialization = AwaitCallbackAsync(completion.Task);
            try
            {
                MobileAds.Initialize(status =>
                {
                    if (status == null) completion.TrySetException(new InvalidOperationException("SDK_INITIALIZATION"));
                    else completion.TrySetResult(true);
                });
            }
            catch (Exception) { completion.TrySetException(new InvalidOperationException("SDK_INITIALIZATION")); }
            return sharedInitialization;
        }

        static async Task AwaitCallbackAsync(Task callback)
        {
            if (await Task.WhenAny(callback, Task.Delay(TimeSpan.FromSeconds(30))) != callback)
            {
                _ = callback.ContinueWith(t => { var observed = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
                throw new TimeoutException("SDK_INITIALIZATION");
            }
            await callback;
        }
    }
}
