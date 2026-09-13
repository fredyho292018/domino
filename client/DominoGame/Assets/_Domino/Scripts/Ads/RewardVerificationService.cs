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
        bool clientEarned;
        public RewardIntentReceipt Current { get; private set; }
        public RewardVerificationState State { get; private set; }
        public event Action<RewardVerificationState> Changed;
        public RewardVerificationService(IRewardIntentApi api, CancellationToken lifetime)
        { this.api = api; this.lifetime = lifetime; }
        public async Task<RewardIntentReceipt> CreateAsync(CancellationToken token)
        {
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
        }
        public void ClientEarned(string intentId)
        {
            if (Current?.IntentId != intentId || State == RewardVerificationState.VERIFIED ||
                Current.Status == "EXPIRED" || Current.Status == "REJECTED") return;
            clientEarned = true;
            Set(RewardVerificationState.CLIENT_EARNED);
        }
        public async Task RefreshAsync()
        {
            var captured = Current;
            if (captured == null || State == RewardVerificationState.VERIFIED || lifetime.IsCancellationRequested) return;
            Set(RewardVerificationState.VERIFYING);
            try
            {
                var receipt = await api.StatusAsync(captured.IntentId, lifetime);
                if (lifetime.IsCancellationRequested || Current != captured) return;
                if (receipt.IntentId != captured.IntentId) throw new FormatException("INTENT_CONTRACT");
                Current = receipt;
                Set(receipt.Status == "VERIFIED" ? RewardVerificationState.VERIFIED :
                    receipt.Status == "EXPIRED" || receipt.ExpiresAt <= DateTimeOffset.UtcNow ? RewardVerificationState.EXPIRED :
                    receipt.Status == "REJECTED" ? RewardVerificationState.FAILED :
                    clientEarned ? RewardVerificationState.CLIENT_EARNED : RewardVerificationState.INTENT_CREATED);
            }
            catch { if (!lifetime.IsCancellationRequested && Current == captured) Set(RewardVerificationState.FAILED); }
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
