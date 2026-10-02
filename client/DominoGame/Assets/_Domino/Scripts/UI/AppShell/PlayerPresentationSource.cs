using System;
using Domino.Identity;
using Domino.Player;

namespace Domino.UI.AppShell
{
    // One host-scoped projection over the existing Player owner and existing routing epoch.
    // No task, API, auth credential, independent domain cache, or demo provider lives here.
    public sealed class PlayerPresentationSource : IDisposable
    {
        readonly PlayerService player;
        readonly AuthenticatedRoutingOrchestrator routing;
        bool disposed, receivingProfile, invalidProfile;
        public event Action Changed;
        public PlayerPresentationState Current
        {
            get
            {
                if (disposed || routing.Onboarding == null && routing.Route == AuthenticatedRoute.Home)
                    return PlayerPresentationState.Empty(PlayerPresentationAvailability.Empty);
                if (routing.Route == AuthenticatedRoute.Error || routing.Route == AuthenticatedRoute.UpdateRequired || invalidProfile)
                    return PlayerPresentationState.Empty(PlayerPresentationAvailability.Unavailable);
                if (routing.Route == AuthenticatedRoute.Welcome || routing.Route == AuthenticatedRoute.VerificationPending)
                    return PlayerPresentationState.Empty(PlayerPresentationAvailability.Empty);
                if (routing.Route != AuthenticatedRoute.Home)
                    return PlayerPresentationState.Empty(PlayerPresentationAvailability.Loading);
                if (!player.IsCurrentSession) return PlayerPresentationState.Empty(PlayerPresentationAvailability.Empty);
                if (player.State == PlayerSyncState.FAILED) return PlayerPresentationState.Empty(PlayerPresentationAvailability.Unavailable);
                if (player.State != PlayerSyncState.SYNCED) return PlayerPresentationState.Empty(PlayerPresentationAvailability.Loading);
                return PlayerPresentationState.FromConfirmed(player.Player, player.PreferredLocale, player.Entitlements);
            }
        }

        public PlayerPresentationSource(PlayerService player, AuthenticatedRoutingOrchestrator routing)
        {
            this.player = player ?? throw new ArgumentNullException(nameof(player));
            this.routing = routing ?? throw new ArgumentNullException(nameof(routing));
            player.SnapshotChanged += PlayerChanged;
            player.SyncStateChanged += SyncChanged;
            player.EntitlementsChanged += Publish;
            player.PresentationInvalidated += Publish;
            routing.Changed += RouteChanged;
            RouteChanged();
        }

        void RouteChanged()
        {
            if (disposed) return;
            invalidProfile = false;
            if (routing.Route == AuthenticatedRoute.Home && player.IsCurrentSession)
            {
                var confirmed = routing.Onboarding;
                receivingProfile = true;
                try { invalidProfile = confirmed == null || !player.ReceiveConfirmedProfile(player.Player, confirmed.basicProfile, confirmed.domainRevisions); }
                finally { receivingProfile = false; }
            }
            Publish();
        }
        void PlayerChanged(PlayerSnapshot ignored) => Publish();
        void SyncChanged(PlayerSyncState ignored) => Publish();
        void Publish()
        {
            if (disposed || receivingProfile || Changed == null) return;
            foreach (Action listener in Changed.GetInvocationList())
                try { listener(); } catch { /* A presentation consumer cannot interrupt domain/routing work. */ }
        }
        public void Dispose()
        {
            if (disposed) return;
            player.SnapshotChanged -= PlayerChanged;
            player.SyncStateChanged -= SyncChanged;
            player.EntitlementsChanged -= Publish;
            player.PresentationInvalidated -= Publish;
            routing.Changed -= RouteChanged;
            disposed = true;
            var listeners = Changed; Changed = null;
            if (listeners != null) foreach (Action listener in listeners.GetInvocationList()) try { listener(); } catch { }
        }
    }
}
