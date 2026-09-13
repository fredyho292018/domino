using System;
using System.Threading;
using System.Threading.Tasks;

namespace Domino.Ads
{
    // Independent of fullscreen lifecycle. Only authenticated backend status may set VERIFIED.
    public sealed class RewardVerificationService : IRewardIntentAuthorization
    {
        readonly IRewardIntentApi api;
        readonly CancellationToken lifetime;
        readonly IRewardWalletReceiver wallet;
        Task<bool> consuming;
        bool creating;
        bool clientEarned;
        public RewardIntentReceipt Current { get; private set; }
        public RewardVerificationState State { get; private set; }
        public long LastConfirmedAmount { get; private set; }
        public event Action<RewardVerificationState> Changed;
        public RewardVerificationService(IRewardIntentApi api, CancellationToken lifetime, IRewardWalletReceiver wallet = null)
        { this.api = api; this.lifetime = lifetime; this.wallet = wallet; }
        public async Task<RewardIntentReceipt> CreateAsync(CancellationToken token)
        {
            if (consuming != null || creating) throw new InvalidOperationException("REWARD_BUSY");
            creating = true;
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(lifetime, token);
            try
            {
                var receipt = await api.CreateAsync(linked.Token);
                linked.Token.ThrowIfCancellationRequested();
                if (receipt.Status != "ISSUED" || receipt.ExpiresAt <= DateTimeOffset.UtcNow)
                    throw new InvalidOperationException("INTENT_UNAVAILABLE");
                Current = receipt; clientEarned = false; Set(RewardVerificationState.INTENT_CREATED);
                return receipt;
            }
            catch { if (!lifetime.IsCancellationRequested) Set(RewardVerificationState.FAILED); throw; }
            finally { creating = false; }
        }
        public void ClientEarned(string intentId)
        {
            if (Current?.IntentId != intentId || State == RewardVerificationState.VERIFIED ||
                Current.Status == "EXPIRED" || Current.Status == "REJECTED" || Current.Status == "CONSUMED") return;
            clientEarned = true;
            Set(RewardVerificationState.CLIENT_EARNED);
        }
        public async Task RefreshAsync()
        {
            var captured = Current;
            if (captured == null || creating || consuming != null || State == RewardVerificationState.VERIFIED ||
                State == RewardVerificationState.CONSUMED || lifetime.IsCancellationRequested) return;
            Set(RewardVerificationState.VERIFYING);
            try
            {
                var receipt = await api.StatusAsync(captured.IntentId, lifetime);
                if (lifetime.IsCancellationRequested || Current != captured) return;
                if (receipt.IntentId != captured.IntentId) throw new FormatException("INTENT_CONTRACT");
                Current = receipt;
                Set(receipt.Status == "CONSUMED" ? RewardVerificationState.CONSUMED : receipt.Status == "VERIFIED" ? RewardVerificationState.VERIFIED :
                    receipt.Status == "EXPIRED" || receipt.ExpiresAt <= DateTimeOffset.UtcNow ? RewardVerificationState.EXPIRED :
                    receipt.Status == "REJECTED" ? RewardVerificationState.FAILED :
                    clientEarned ? RewardVerificationState.CLIENT_EARNED : RewardVerificationState.INTENT_CREATED);
            }
            catch { if (!lifetime.IsCancellationRequested && Current == captured) Set(RewardVerificationState.FAILED); }
        }
        public async Task RecoverPendingAsync()
        {
            if (creating || consuming != null || lifetime.IsCancellationRequested || !(api is IRewardConsumptionApi economy)) return;
            var captured = Current;
            try
            {
                var pending = await economy.PendingAsync(lifetime);
                if (lifetime.IsCancellationRequested || Current != captured || creating || consuming != null) return;
                Current = pending; clientEarned = false;
                Set(pending == null ? RewardVerificationState.NONE : RewardVerificationState.VERIFIED);
            }
            catch { if (!lifetime.IsCancellationRequested && Current == captured) Set(RewardVerificationState.FAILED); }
        }
        public Task<bool> ConsumeRewardAsync()
        {
            if (consuming != null) return consuming;
            if (creating || lifetime.IsCancellationRequested || wallet == null || !(api is IRewardConsumptionApi) ||
                (Current?.Status != "VERIFIED" && Current?.Status != "CONSUMED")) return Task.FromResult(false);
            var completion = new TaskCompletionSource<bool>();
            consuming = completion.Task;
            _ = ConsumeCore(completion, Current);
            return completion.Task;
        }
        async Task ConsumeCore(TaskCompletionSource<bool> completion, RewardIntentReceipt captured)
        {
            Set(RewardVerificationState.CONSUMING);
            try
            {
                long confirmedAmount = 0;
                bool applied = await wallet.ApplyAsync(async token =>
                {
                    var response = await ((IRewardConsumptionApi)api).ConsumeRewardAsync(captured.IntentId, token);
                    if (response.IntentId != captured.IntentId) throw new FormatException("CONSUME_CONTRACT");
                    confirmedAmount = response.Amount;
                    return response.Coins; // Absolute backend balance; never local addition.
                }, lifetime);
                if (lifetime.IsCancellationRequested) { completion.TrySetResult(false); return; }
                if (applied) { LastConfirmedAmount = confirmedAmount; Current = new RewardIntentReceipt(captured.IntentId, "CONSUMED", captured.ExpiresAt, confirmedAmount); }
                Set(applied ? RewardVerificationState.CONSUMED : RewardVerificationState.FAILED);
                completion.TrySetResult(applied);
            }
            catch { if (!lifetime.IsCancellationRequested) Set(RewardVerificationState.FAILED); completion.TrySetResult(false); }
            finally { consuming = null; }
        }
        void Set(RewardVerificationState state)
        {
            State = state;
            if (Changed == null) return;
            foreach (Action<RewardVerificationState> handler in Changed.GetInvocationList())
                try { handler(state); } catch { }
        }
    }
}
