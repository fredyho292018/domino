using System;
using System.Threading.Tasks;

namespace Domino.Ads
{
    public enum RewardedState { DISABLED, NOT_LOADED, LOADING, READY, SHOWING, EARNED, FAILED }
    public enum RewardedShowResult { Unavailable, Closed, Earned, Failed }
    // Google eligibility only. This payload is never a Domino coin award.
    public readonly struct RewardedCompletionResult
    {
        public bool Completed => true;
        public string GoogleRewardType { get; }
        public double GoogleRewardAmount { get; }
        public RewardedCompletionResult(string type, double amount) { GoogleRewardType = type; GoogleRewardAmount = amount; }
    }
    public interface IRewardedAd : IDisposable
    {
        bool CanShow { get; }
        event Action Opened;
        event Action Closed;
        event Action Failed;
        void SetVerificationIntent(string intentId);
        void Show(Action<RewardedCompletionResult> earned);
    }
    public interface IRewardedAdLoader { Task<IRewardedAd> LoadAsync(string testUnitId); }
    public interface IRewardedAdsService : IDisposable
    {
        RewardedState State { get; }
        bool IsAvailable { get; }
        event Action<RewardedState> RewardedStateChanged;
        event Action<bool> RewardedAvailabilityChanged;
        event Action<RewardedCompletionResult> RewardEarned;
        Task InitializeAsync();
        Task LoadRewardedAsync();
        void PreloadIfNeeded();
        Task<RewardedShowResult> ShowRewardedAsync();
    }
}
