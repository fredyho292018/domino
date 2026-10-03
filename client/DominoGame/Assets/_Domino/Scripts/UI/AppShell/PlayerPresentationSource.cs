using System;
using System.Threading;
using System.Threading.Tasks;
using Domino.Identity;
using Domino.Infrastructure.Api;
using Domino.Player;

namespace Domino.UI.AppShell
{
    // One host-scoped projection over the existing Player owner and existing routing epoch.
    // Owns disposable catalog presentation only; Player and onboarding remain the domain authorities.
    public sealed class PlayerPresentationSource : IDisposable
    {
        readonly PlayerService player;
        readonly AuthenticatedRoutingOrchestrator routing;
        readonly Func<IPlayerPresentationCatalogs> catalogs;
        CancellationTokenSource coachLifetime;
        PlayerCoachPresentation coach;
        OnboardingShellController profileController;
        string coachRequest, locale;
        long coachGeneration;
        public Task CoachResolutionTask { get; private set; } = Task.CompletedTask;
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
                return PlayerPresentationState.FromConfirmed(player.Player, player.PreferredLocale, player.Entitlements, coach);
            }
        }

        public PlayerPresentationSource(PlayerService player, AuthenticatedRoutingOrchestrator routing, Func<IPlayerPresentationCatalogs> catalogs = null)
        {
            this.player = player ?? throw new ArgumentNullException(nameof(player));
            this.routing = routing ?? throw new ArgumentNullException(nameof(routing));
            this.catalogs = catalogs;
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
            var currentController = catalogs?.Invoke() as OnboardingShellController;
            if(currentController != profileController) {
                if(profileController!=null)profileController.ProfileConfirmed-=ProfileConfirmed;
                profileController=currentController;
                if(profileController!=null)profileController.ProfileConfirmed+=ProfileConfirmed;
            }
            if (routing.Route == AuthenticatedRoute.Home && player.IsCurrentSession)
            {
                var confirmed = routing.Onboarding;
                receivingProfile = true;
                try { invalidProfile = confirmed == null || !player.ReceiveConfirmedProfile(player.Player, confirmed.basicProfile, confirmed.domainRevisions); }
                finally { receivingProfile = false; }
            }
            Publish();
        }
        void ProfileConfirmed(OnboardingStateDto confirmed)
        {
            if(disposed || routing.Route!=AuthenticatedRoute.Onboarding || !player.IsCurrentSession)return;
            player.ReceiveConfirmedProfile(player.Player,confirmed.basicProfile,confirmed.domainRevisions);
        }
        void PlayerChanged(PlayerSnapshot ignored) => Publish();
        void SyncChanged(PlayerSyncState ignored) => Publish();
        void Publish()
        {
            if (disposed || receivingProfile) return;
            SynchronizeCoach();
            Notify();
        }
        void Notify()
        {
            if(disposed || Changed==null)return;
            foreach (Action listener in Changed.GetInvocationList())
                try { listener(); } catch { /* A presentation consumer cannot interrupt domain/routing work. */ }
        }
        public void SetLocale(string value)
        {
            value=value=="es"?"es":"en";
            if(disposed || locale==value)return;
            locale=value; Publish();
        }
        string CoachRequest(OnboardingStateDto state, string language) =>
            state == null ? null : state.status+"|"+state.catalogVersion+"|"+state.revision+"|"+
                state.domainRevisions?.domino+"|"+PlayerCoachResolution.SavedKey(state)+"|"+language;
        void ClearCoach()
        {
            coachGeneration++; coachLifetime?.Cancel(); coachLifetime?.Dispose(); coachLifetime=null;
            coach=null; coachRequest=null;
        }
        void SynchronizeCoach()
        {
            if(Current.Availability!=PlayerPresentationAvailability.Ready) { ClearCoach(); return; }
            var state=routing.Onboarding;
            var language=locale ?? (player.PreferredLocale=="es"?"es":"en");
            var request=CoachRequest(state,language);
            if(request==coachRequest)return;
            ClearCoach(); coachRequest=request;
            coach=new PlayerCoachPresentation(PlayerCoachAvailability.Loading,null,PlayerCoachResolution.SavedKey(state),state?.catalogVersion);
            coachLifetime=new CancellationTokenSource();
            CoachResolutionTask=ResolveCoach(state,language,request,coachGeneration,coachLifetime.Token);
        }
        async Task ResolveCoach(OnboardingStateDto state,string language,string request,long generation,CancellationToken token)
        {
            PlayerCoachPresentation next;
            try { next=await PlayerCoachResolution.ResolveAsync(state,language,catalogs?.Invoke(),token); }
            catch(OperationCanceledException) { return; }
            catch { next=PlayerCoachResolution.Unavailable("CATALOG_UNAVAILABLE",state); }
            if(disposed || token.IsCancellationRequested || generation!=coachGeneration ||
                Current.Availability!=PlayerPresentationAvailability.Ready ||
                request!=CoachRequest(routing.Onboarding,locale ?? (player.PreferredLocale=="es"?"es":"en")))return;
            coach=next; Notify();
        }
        public void Dispose()
        {
            if (disposed) return;
            if(profileController!=null)profileController.ProfileConfirmed-=ProfileConfirmed;
            profileController=null;
            player.SnapshotChanged -= PlayerChanged;
            player.SyncStateChanged -= SyncChanged;
            player.EntitlementsChanged -= Publish;
            player.PresentationInvalidated -= Publish;
            routing.Changed -= RouteChanged;
            disposed = true;
            ClearCoach();
            var listeners = Changed; Changed = null;
            if (listeners != null) foreach (Action listener in listeners.GetInvocationList()) try { listener(); } catch { }
        }
    }
}
