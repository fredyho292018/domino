using Domino.Infrastructure.Api;
using Domino.Player;

namespace Domino.UI
{
    // REST outcomes describe synchronization, never a persistent connection or presence.
    public static class PlayerSyncPresentation
    {
        public static string Key(PlayerSyncState state, ApiFailure? failure = null, long httpStatus = 0)
        {
            switch (state)
            {
                case PlayerSyncState.NOT_SYNCED: return "profile.not_synced";
                case PlayerSyncState.SYNCING: return "profile.syncing";
                case PlayerSyncState.SYNCED: return "profile.synced";
                default:
                    if (failure == ApiFailure.Transport || failure == ApiFailure.Timeout) return "profile.offline";
                    if (failure == ApiFailure.Configuration) return "profile.sync_unavailable";
                    if (failure == ApiFailure.Authentication || httpStatus == 403) return "profile.auth_required";
                    if (httpStatus >= 500) return "profile.service_unavailable";
                    return "profile.sync_failed";
            }
        }
    }
}
