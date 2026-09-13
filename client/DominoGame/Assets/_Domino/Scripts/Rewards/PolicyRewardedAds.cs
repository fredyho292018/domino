using System;
using System.Threading.Tasks;
using Domino.Ads;

namespace Domino.Rewards
{
    // Coordinates remote gates without adding policy fetch/caching responsibilities to the SDK adapter.
    public sealed class PolicyRewardedAds : IRewardedAdsService
    {
        readonly IRewardedAdsService inner;
        readonly IMonetizationPolicyService policy;
        public PolicyRewardedAds(IRewardedAdsService inner,IMonetizationPolicyService policy){this.inner=inner;this.policy=policy;policy.Changed+=PolicyChanged;inner.RewardedAvailabilityChanged+=AvailabilityChanged;}
        public RewardedState State=>inner.State;
        public bool IsAvailable=>policy.AllowAds&&inner.IsAvailable;
        public event Action<RewardedState> RewardedStateChanged {add=>inner.RewardedStateChanged+=value;remove=>inner.RewardedStateChanged-=value;}
        public event Action<bool> RewardedAvailabilityChanged;
        public event Action<RewardedCompletionResult> RewardEarned {add=>inner.RewardEarned+=value;remove=>inner.RewardEarned-=value;}
        void PolicyChanged()=>RewardedAvailabilityChanged?.Invoke(IsAvailable);
        void AvailabilityChanged(bool available)=>PolicyChanged();
        public Task InitializeAsync()=>LoadRewardedAsync();
        public async Task LoadRewardedAsync(){if(inner.State==RewardedState.DISABLED)return;await policy.RefreshAsync();if(policy.AllowAds)await inner.LoadRewardedAsync();}
        public void PreloadIfNeeded(){_=LoadRewardedAsync();}
        public async Task<RewardedShowResult> ShowRewardedAsync(){if(inner.State==RewardedState.DISABLED||!await policy.BeforeShowAsync())return RewardedShowResult.Unavailable;return await inner.ShowRewardedAsync();}
        public void Dispose(){policy.Changed-=PolicyChanged;inner.RewardedAvailabilityChanged-=AvailabilityChanged;inner.Dispose();}
    }
}
