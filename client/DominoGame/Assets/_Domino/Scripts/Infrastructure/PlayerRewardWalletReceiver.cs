using System;
using System.Threading;
using System.Threading.Tasks;
using Domino.Ads;
using Domino.Player;

namespace Domino.Infrastructure
{
    public sealed class PlayerRewardWalletReceiver : IRewardWalletReceiver
    {
        readonly PlayerService player;
        public PlayerRewardWalletReceiver(PlayerService player) { this.player = player; }
        public Task<bool> ApplyAsync(Func<CancellationToken, Task<long>> confirmedCoins, CancellationToken token) =>
            player.ApplyConfirmedRewardWalletAsync(confirmedCoins, token);
    }
}
