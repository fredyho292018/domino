using Domino.Core;

namespace Domino.Ads
{
    public static class RewardedRoundPreload
    {
        public static void Handle(GameEvent gameEvent, IRewardedAdsService rewarded)
        {
            if (gameEvent.Type == GameEventType.ROUND_FINISHED) rewarded?.PreloadIfNeeded();
        }
    }
}
