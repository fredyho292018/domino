using System;
using System.Threading;
using System.Threading.Tasks;
using Domino.Identity;
using Domino.Infrastructure.Api;
using Domino.Infrastructure.Firebase;
using Domino.Player;

namespace Domino.UI.AppShell
{
    // Shared by application startup and fake-dependency composition tests. No alternate route policy.
    public sealed class ProductionRoutingComposition : IAuthenticatedDestination, IRoutingSessionSource
    {
        readonly FirebaseAuthService auth;
        readonly PlayerService player;
        readonly Func<OnboardingApiSession> sessions;
        readonly Func<string> locale;
        Binding binding;
        bool disposed;
        AuthenticatedRoute published;
        public AuthenticatedRoutingOrchestrator Router { get; }
        public OnboardingShellController Onboarding => binding?.Controller;
        public event Action Changed;
        public string Message=>Router.Message;
        public ProductionAuthRoute Route=>Router.Route==AuthenticatedRoute.Welcome?ProductionAuthRoute.Welcome:
            Router.Route==AuthenticatedRoute.VerificationPending?ProductionAuthRoute.VerificationPending:
            Router.Route==AuthenticatedRoute.Home?ProductionAuthRoute.AppShell:
            Router.Route==AuthenticatedRoute.Onboarding?ProductionAuthRoute.Onboarding:
            Router.Route==AuthenticatedRoute.Error?ProductionAuthRoute.Error:
            Router.Route==AuthenticatedRoute.UpdateRequired?ProductionAuthRoute.UpdateRequired:ProductionAuthRoute.Loading;
        public ProductionRoutingComposition(FirebaseAuthService auth,PlayerService player,Func<OnboardingApiSession> sessions,Func<string> locale)
        {this.auth=auth;this.player=player;this.sessions=sessions;this.locale=locale;Router=new AuthenticatedRoutingOrchestrator(this);published=Router.Route;Router.Changed+=RouteChanged;}
        void RouteChanged(){if(disposed||published==Router.Route)return;published=Router.Route;Changed?.Invoke();}
        public Task ResolveAsync()=>Router.ResolveAsync();
        public Task RetryAsync()=>Router.RetryAsync();
        public OnboardingApiSession CreatePlayerApiSession()=>sessions();
        async Task<FirebaseAuthSessionSnapshot> IRoutingSessionSource.ResolveAsync(CancellationToken token)
        {
            var user=await auth.RestoreAsync();token.ThrowIfCancellationRequested();
            if(user==null)return null;
            var s=auth.Session??new FirebaseAuthSessionSnapshot(user.Uid,user.IsAnonymous,false,false);
            if(s.Uid!=user.Uid)throw new DominoApiException(ApiFailure.Authentication);
            if(!s.IsAnonymous&&s.IsPasswordProvider){await auth.ReloadEmailAsync();token.ThrowIfCancellationRequested();s=auth.Session;
                if(s==null||s.Uid!=user.Uid)throw new DominoApiException(ApiFailure.Authentication);
                if(s.IsEmailVerified){await auth.RefreshVerifiedTokenAsync();token.ThrowIfCancellationRequested();}}
            return s;
        }
        IRoutingPlayerBinding IRoutingSessionSource.Bind(FirebaseAuthSessionSnapshot identity)
        {binding=new Binding(this,sessions(),identity.Uid);return binding;}
        // The controller owns all start/save/complete operations. Router only reads authority.
        public Task LoadOnboardingAsync()=>Onboarding?.LoadAsync(locale())??Task.CompletedTask;
        void Completed(Binding owner)
        {
            if(disposed||binding!=owner||owner.Controller.Phase!=OnboardingShellPhase.Completed)return;
            // Completed is a diagnostic/isolated presentation. Production resolves directly to Home
            // after the server-confirmed completion, without a timed success flash or local flag.
            _=Router.ReevaluateOnboardingAsync();
        }
        sealed class Binding : IRoutingPlayerBinding
        {
            readonly ProductionRoutingComposition owner;readonly OnboardingApiSession session;readonly string uid;
            readonly OnboardingApiClient api;bool disposed;
            public readonly OnboardingShellController Controller;
            public Binding(ProductionRoutingComposition owner,OnboardingApiSession session,string uid)
            {
                this.owner=owner;this.session=session;this.uid=uid;api=new OnboardingApiClient(session);
                Controller=new OnboardingShellController(new OnboardingShellApiSource(api,new CoachCatalogApiClient(session),new MembershipCatalogApiClient(session),new TrialActivationApiClient(session,owner.player),()=>owner.player.TrialEligibility,()=>owner.player.Entitlements));
                Controller.Changed+=OnChanged;
            }
            void OnChanged()=>owner.Completed(this);
            public async Task<string> BootstrapAsync(CancellationToken token)
            {
                session.EnsureCurrent();
                await(owner.player.State==PlayerSyncState.FAILED?owner.player.RetryBootstrapFromAuthAsync(token):owner.player.InitializeAsync(token));
                token.ThrowIfCancellationRequested();session.EnsureCurrent();
                if(owner.player.State!=PlayerSyncState.SYNCED||!owner.player.HasConfirmedSnapshots)throw owner.player.Error??new DominoApiException(ApiFailure.Contract);
                if(owner.player.Player.Uid!=uid)throw new DominoApiException(ApiFailure.Contract);
                return uid;
            }
            public Task<OnboardingStateDto> ReadOnboardingAsync(CancellationToken token)=>api.LoadAsync(token);
            public void Dispose(){if(disposed)return;disposed=true;Controller.Changed-=OnChanged;Controller.Dispose();session.Dispose();if(owner.binding==this)owner.binding=null;}
        }
        public void Dispose(){if(disposed)return;disposed=true;Router.Changed-=RouteChanged;Router.Dispose();Changed=null;}
    }
}
