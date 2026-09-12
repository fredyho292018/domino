using Domino.UI;
using Domino.Player;
using Domino.Infrastructure.Api;

static partial class PlayerFoundationClientTests
{
    static void ConnectionTests()
    {
        Check(PlayerSyncPresentation.Key(PlayerSyncState.NOT_SYNCED) == "profile.not_synced", "Not synced is not connecting");
        Check(PlayerSyncPresentation.Key(PlayerSyncState.SYNCING) == "profile.syncing", "Sync in progress");
        Check(PlayerSyncPresentation.Key(PlayerSyncState.SYNCED) == "profile.synced", "REST success never claims online presence");
        foreach (var error in new[]{ApiFailure.Transport, ApiFailure.Timeout})
            Check(PlayerSyncPresentation.Key(PlayerSyncState.FAILED, error) == "profile.offline", "Transport offline");
        Check(PlayerSyncPresentation.Key(PlayerSyncState.FAILED, ApiFailure.Configuration) == "profile.sync_unavailable", "Disabled API is not offline evidence");
        Check(PlayerSyncPresentation.Key(PlayerSyncState.FAILED, ApiFailure.Authentication,401) == "profile.auth_required", "Authentication failure safe label");
        Check(PlayerSyncPresentation.Key(PlayerSyncState.FAILED, ApiFailure.Server,403) == "profile.auth_required", "Authorization failure safe label");
        Check(PlayerSyncPresentation.Key(PlayerSyncState.FAILED, ApiFailure.Server,503) == "profile.service_unavailable", "Dependency failure safe label");
        foreach(var error in new[]{ApiFailure.Contract,ApiFailure.Cancelled})
            Check(PlayerSyncPresentation.Key(PlayerSyncState.FAILED,error) == "profile.sync_failed", "Other failures do not claim transport outage");
        System.Console.WriteLine("PLAYER_CONNECTION_SEMANTICS=PASS");
    }
}
