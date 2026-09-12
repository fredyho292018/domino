using System;

namespace Domino.Player
{
    public sealed class WalletSnapshot
    {
        public long Coins { get; }
        public WalletSnapshot(long coins)
        {
            if (coins < 0 || coins > 9007199254740991L) throw new ArgumentOutOfRangeException(nameof(coins));
            Coins = coins;
        }
    }
}
