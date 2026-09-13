using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Domino.Ads;

namespace Domino.Rewards
{
    public enum RoundRewardState { HIDDEN, PREPARING, AVAILABLE, STARTING, WATCHING, VERIFYING, CREDITING, REWARDED, UNAVAILABLE, FAILED }

    // Application lifetime, not panel lifetime. Round references are UI eligibility only.
    // All verification and money authority remains in the existing H3/H4 services.
    public sealed class RoundRewardFlow : IDisposable
    {
        readonly IRewardedAdsService ads;
        readonly RewardVerificationService verification;
        readonly Func<bool> ready;
        readonly Func<bool> recoveryReady;
        readonly IMonetizationPolicyService policy;
        readonly CancellationToken lifetime;
        readonly Func<int, CancellationToken, Task> delay;
        readonly HashSet<object> rewardedRounds = new HashSet<object>();
        readonly HashSet<string> announced = new HashSet<string>();
        object round, pendingRound;
        bool busy, disposed, pending;
        RoundRewardState phase;
        string message = "reward.unavailable";
        public event Action Changed;
        public event Action<long> Confirmed;
        public long PreviewCoins => (busy || pending) && verification.Current?.PreviewAmount > 0 ? verification.Current.PreviewAmount : policy?.RewardCoins ?? 0;
        public long ConfirmedCoins { get; private set; }
        public RoundRewardState State => ads.State == RewardedState.DISABLED || round == null || (!busy && !pending && policy?.AllowAds != true) ? RoundRewardState.HIDDEN :
            rewardedRounds.Contains(round) ? RoundRewardState.REWARDED : busy || pending || phase == RoundRewardState.FAILED || phase == RoundRewardState.UNAVAILABLE ? phase :
            !ready() || policy?.Eligible != true ? RoundRewardState.UNAVAILABLE : ads.IsAvailable ? RoundRewardState.AVAILABLE :
            ads.State == RewardedState.LOADING ? RoundRewardState.PREPARING : RoundRewardState.UNAVAILABLE;
        public bool CanWatch => !busy && !pending && round != null && !rewardedRounds.Contains(round) && ready() && policy?.Eligible == true && ads.IsAvailable;
        public bool CanRetry => !busy && pending && recoveryReady() && ads.State != RewardedState.DISABLED;
        public int CooldownSeconds => policy?.RemainingSeconds ?? 0;
        public string MessageKey => !busy && !pending && State == RoundRewardState.UNAVAILABLE && policy?.Eligibility?.Reason != null ? "reward.reason."+policy.Eligibility.Reason.ToLowerInvariant() : State switch {
            RoundRewardState.PREPARING => "reward.preparing", RoundRewardState.AVAILABLE => "reward.optional",
            RoundRewardState.STARTING => "reward.preparing", RoundRewardState.WATCHING => "reward.watching",
            RoundRewardState.VERIFYING => "reward.verifying", RoundRewardState.CREDITING => "reward.crediting",
            RoundRewardState.REWARDED => "reward.received", _ => message };
        public RoundRewardFlow(IRewardedAdsService ads, RewardVerificationService verification, Func<bool> ready,
            CancellationToken lifetime, Func<int, CancellationToken, Task> delay = null, Func<bool> recoveryReady = null, IMonetizationPolicyService policy = null)
        {
            this.ads = ads; this.verification = verification; this.ready = ready; this.lifetime = lifetime;
            this.delay = delay ?? ((ms, token) => Task.Delay(ms, token));
            this.recoveryReady = recoveryReady ?? ready;
            this.policy = policy;
            if(policy!=null)policy.Changed+=Notify;
            ads.RewardedStateChanged += AdState;
        }
        public void PresentRound(object identity) { round = identity; if (!busy && !pending) phase = RoundRewardState.AVAILABLE; if(policy!=null)_=policy.RefreshEligibilityAsync(true); Notify(); }
        public void LeaveRound() { round = null; Notify(); } // Never cancels an earned reward.
        public void Refresh() => Notify();
        void AdState(RewardedState state) { if (busy && (state == RewardedState.SHOWING || state == RewardedState.EARNED)) phase = RoundRewardState.WATCHING; Notify(); }
        public async Task WatchAsync()
        {
            if (!CanWatch || disposed) return;
            busy = true; pendingRound = round; Set(RoundRewardState.STARTING, "reward.preparing");
            try
            {
                var result = await ads.ShowRewardedAsync(); // H3 creates intent and attaches SSV before show.
                if (disposed || lifetime.IsCancellationRequested) return;
                if (result != RewardedShowResult.Earned)
                { Set(RoundRewardState.UNAVAILABLE, result == RewardedShowResult.Closed ? "reward.incomplete" : "reward.prepare_failed"); return; }
                pending = true;
                await ResolveAsync();
            }
            catch (Exception) { Set(RoundRewardState.FAILED, "reward.pending"); }
            finally { busy = false; Notify(); }
        }
        public async Task RetryAsync()
        {
            if (!CanRetry || disposed) return;
            busy = true;
            try { await ResolveAsync(); }
            catch (Exception) { Set(RoundRewardState.FAILED, "reward.pending"); }
            finally { busy = false; Notify(); }
        }
        public async Task RecoverAsync()
        {
            if (busy || disposed || !recoveryReady() || ads.State == RewardedState.DISABLED) return;
            if (pending) { await RetryAsync(); return; }
            busy = true;
            try
            {
                await verification.RecoverPendingAsync();
                if (verification.Current?.Status == "VERIFIED") { pendingRound = null; pending = true; await ResolveAsync(); }
            }
            catch (Exception) { /* Offline gameplay remains independent. Next explicit action may recover. */ }
            finally { busy = false; Notify(); }
        }
        async Task ResolveAsync()
        {
            // Bounded SSV observation, no permanent polling. A retry uses the SAME intent.
            int[] pauses = { 0, 2000, 3000, 5000, 8000, 12000 };
            for (int i = 0; i < pauses.Length; i++)
            {
                if (pauses[i] != 0) await delay(pauses[i], lifetime);
                lifetime.ThrowIfCancellationRequested();
                Set(RoundRewardState.VERIFYING, "reward.verifying");
                var status = verification.Current?.Status;
                if (status != "VERIFIED" && status != "CONSUMED") await verification.RefreshAsync();
                status = verification.Current?.Status;
                if (status == "VERIFIED" || status == "CONSUMED")
                {
                    Set(RoundRewardState.CREDITING, "reward.crediting");
                    if (await verification.ConsumeRewardAsync())
                    {
                        pending = false;
                        if (pendingRound != null) rewardedRounds.Add(pendingRound);
                        ConfirmedCoins = verification.LastConfirmedAmount;
                        Set(RoundRewardState.REWARDED, "reward.received");
                        if (announced.Add(verification.Current.IntentId)) Confirmed?.Invoke(ConfirmedCoins);
                        return;
                    }
                }
                else if (status == "REJECTED" || status == "EXPIRED" || verification.State == RewardVerificationState.EXPIRED)
                { pending = false; Set(RoundRewardState.FAILED, "reward.unavailable"); return; }
            }
            Set(RoundRewardState.FAILED, "reward.pending");
        }
        void Set(RoundRewardState state, string key) { phase = state; message = key; Notify(); }
        void Notify() { if (!disposed && !lifetime.IsCancellationRequested) Changed?.Invoke(); }
        public void Dispose() { disposed = true; ads.RewardedStateChanged -= AdState; if(policy!=null)policy.Changed-=Notify; Changed = null; Confirmed = null; }
    }
}
