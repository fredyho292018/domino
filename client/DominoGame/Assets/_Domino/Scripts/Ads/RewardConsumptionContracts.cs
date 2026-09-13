using System;
using System.Threading;
using System.Threading.Tasks;

namespace Domino.Ads
{
    public sealed class RewardConsumeReceipt
    {
        public string IntentId { get; }
        public long Amount { get; }
        public long Coins { get; }
        public RewardConsumeReceipt(string id, long amount, long coins)
        {
            if (!Guid.TryParseExact(id, "D", out var guid) || guid == Guid.Empty ||
                amount < 1 || amount > 9007199254740991L || coins < 0 || coins > 9007199254740991L)
                throw new FormatException("CONSUME_CONTRACT");
            IntentId = id; Amount = amount; Coins = coins;
        }
    }
    public interface IRewardConsumptionApi
    {
        Task<RewardConsumeReceipt> ConsumeRewardAsync(string intentId, CancellationToken token);
        Task<RewardIntentReceipt> PendingAsync(CancellationToken token);
    }
    public interface IRewardConsumptionCodec
    {
        RewardConsumeReceipt ReadConsume(string json);
        RewardIntentReceipt ReadPending(string json);
    }
    // Serializes with Player bootstrap/alias operations, and assigns a backend snapshot.
    public interface IRewardWalletReceiver
    {
        Task<bool> ApplyAsync(Func<CancellationToken, Task<long>> confirmedCoins, CancellationToken token);
    }
}
