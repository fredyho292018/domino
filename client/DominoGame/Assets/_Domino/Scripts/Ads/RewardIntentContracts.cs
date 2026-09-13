using System;
using System.Threading;
using System.Threading.Tasks;

namespace Domino.Ads
{
    public enum RewardVerificationState { NONE, INTENT_CREATED, CLIENT_EARNED, VERIFYING, VERIFIED, EXPIRED, FAILED, CONSUMING, CONSUMED }
    public sealed class RewardIntentReceipt
    {
        public string IntentId { get; }
        public string Status { get; }
        public DateTimeOffset ExpiresAt { get; }
        public RewardIntentReceipt(string id, string status, DateTimeOffset expiresAt)
        {
            if (!Guid.TryParseExact(id, "D", out var guid) || guid == Guid.Empty ||
                (status != "ISSUED" && status != "VERIFIED" && status != "EXPIRED" && status != "REJECTED" && status != "CONSUMED"))
                throw new FormatException("INTENT_CONTRACT");
            IntentId = id; Status = status; ExpiresAt = expiresAt;
        }
    }
    public interface IRewardIntentApi
    {
        Task<RewardIntentReceipt> CreateAsync(CancellationToken token);
        Task<RewardIntentReceipt> StatusAsync(string id, CancellationToken token);
    }
    public interface IRewardIntentAuthorization
    {
        Task<RewardIntentReceipt> CreateAsync(CancellationToken token);
        void ClientEarned(string intentId);
    }
    public interface IRewardIntentCodec { RewardIntentReceipt Read(string json); }
}
