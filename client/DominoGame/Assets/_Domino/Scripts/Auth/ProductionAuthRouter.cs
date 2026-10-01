using System;
using System.Threading;
using System.Threading.Tasks;
using Domino.Infrastructure.Firebase;
using Domino.Player;

namespace Domino.Identity
{
    public enum ProductionAuthRoute { Loading, Welcome, AppShell, Error, EmailEntry, Register, VerificationPending, EmailPlaceholder, EmailSignIn, ForgotPassword, Onboarding, UpdateRequired }
    // Auth forms retain their navigation; authenticated destination decisions belong to one injected owner.
    public interface IAuthenticatedDestination : IDisposable
    {
        ProductionAuthRoute Route { get; }
        string Message { get; }
        event Action Changed;
        Task ResolveAsync();
        Task RetryAsync();
    }
    public interface IPostAuthenticationPolicy { ProductionAuthRoute Destination(PlayerSnapshot player); }
    // Temporary migration policy. No onboarding status is fabricated or stored.
    public sealed class TemporaryAppShellPolicy : IPostAuthenticationPolicy
    { public ProductionAuthRoute Destination(PlayerSnapshot player) => ProductionAuthRoute.AppShell; }

    public sealed class ProductionAuthRouter : IDisposable
    {
        readonly FirebaseAuthService identity;
        readonly PlayerService player;
        readonly IPostAuthenticationPolicy policy;
        readonly IAuthenticatedDestination destination;
        bool destinationActive;
        readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        Task operation;bool disposed;
        ProductionAuthRoute formRoute = ProductionAuthRoute.Loading;
        public ProductionAuthRoute Route { get => destinationActive ? destination.Route : formRoute; private set => formRoute=value; }
        public AuthSessionKind SessionKind { get; private set; }=AuthSessionKind.NoSession;
        public EmailOperationState EmailState { get; private set; }
        public EmailAuthError EmailError { get; private set; }
        public bool Busy { get; private set; }
        public bool VerificationSent { get; private set; }
        public string DisplayEmail => identity.Session?.DisplayEmail??"";
        public bool CanCancelCreatedEmail => identity.CanCancelCreatedEmail;
        string formMessage="";
        public string Message { get => destinationActive && !(Route==ProductionAuthRoute.VerificationPending && !string.IsNullOrEmpty(formMessage)) ? destination.Message : formMessage; private set => formMessage=value; }
        public event Action Changed;
        public ProductionAuthRouter(FirebaseAuthService identity, PlayerService player, IPostAuthenticationPolicy policy = null, IAuthenticatedDestination destination = null)
        { this.identity=identity; this.player=player; this.policy=policy??new TemporaryAppShellPolicy(); this.destination=destination;if(destination!=null)destination.Changed+=Notify; }
        public Task RetryRoutingAsync()=>destinationActive?destination.RetryAsync():RestoreAsync();
        public Task RestoreAsync()=>BeginSession(false);
        public Task ContinueAsGuestAsync()=>BeginSession(true);
        Task BeginSession(bool guest)
        {
            if(disposed||Route==ProductionAuthRoute.AppShell)return Task.CompletedTask;
            if(destination!=null)return Execute(EmailOperationState.Checking,async()=>{
                destinationActive=false;Route=ProductionAuthRoute.Loading;Notify();
                if(guest)await identity.ContinueAsGuestAsync();
                if(disposed)return;destinationActive=true;await destination.ResolveAsync();UpdateSessionKind();
            });
            return Execute(EmailOperationState.Checking,async()=>{
                var user=await(guest?identity.ContinueAsGuestAsync():identity.RestoreAsync());if(disposed)return;
                await ClassifyAndBootstrap(user,true);
            });
        }
        async Task ClassifyAndBootstrap(PlayerIdentity user,bool reload)
        {
            if(destination!=null){destinationActive=true;await destination.ResolveAsync();UpdateSessionKind();return;}
            if(user==null){SessionKind=AuthSessionKind.NoSession;Route=ProductionAuthRoute.Welcome;Message="";return;}
            SessionKind=user.IsAnonymous?AuthSessionKind.RestoredGuest:AuthSessionKind.OtherRegistered;
            if(!user.IsAnonymous&&identity.SupportsEmail){
                var session=identity.Session;
                if(session==null||session.Uid!=user.Uid)throw new EmailAuthException(EmailAuthError.SessionConflict);
                if(session.IsPasswordProvider){
                    Route=ProductionAuthRoute.VerificationPending;
                    if(reload)await identity.ReloadEmailAsync();if(disposed)return;
                    session=identity.Session;
                    if(session==null||session.Uid!=user.Uid||session.IsAnonymous||!session.IsPasswordProvider)throw new EmailAuthException(EmailAuthError.SessionConflict);
                    SessionKind=session.IsEmailVerified?AuthSessionKind.EmailVerified:AuthSessionKind.EmailUnverified;
                    if(!session.IsEmailVerified){Message="Verify your email to continue.";return;}
                    await identity.RefreshVerifiedTokenAsync();if(disposed)return;
                }
            }
            await(player.State==PlayerSyncState.FAILED?player.RetryBootstrapFromAuthAsync(lifetime.Token):player.InitializeAsync(lifetime.Token));
            if(disposed)return;
            if(player.State!=PlayerSyncState.SYNCED||!player.HasConfirmedSnapshots||player.Player.Uid!=user.Uid||identity.Current?.Uid!=user.Uid)
                throw new InvalidOperationException("Player unavailable.");
            if(identity.SupportsEmail){var current=identity.Session;if(current==null||current.Uid!=user.Uid||(!current.IsAnonymous&&current.IsPasswordProvider&&!current.IsEmailVerified))throw new EmailAuthException(EmailAuthError.SessionConflict);}
            Route=policy.Destination(player.Player);Message="";
        }
        public void NavigateEmail(ProductionAuthRoute route)
        {
            if(disposed||Busy)return;
            if(identity.Current!=null){Message=EmailAuthRules.Message(identity.Current.IsAnonymous?EmailAuthError.GuestUpgradeRequired:EmailAuthError.SessionConflict);Notify();return;}
            if(route!=ProductionAuthRoute.EmailEntry&&route!=ProductionAuthRoute.Register&&route!=ProductionAuthRoute.EmailPlaceholder&&route!=ProductionAuthRoute.Welcome&&route!=ProductionAuthRoute.EmailSignIn&&route!=ProductionAuthRoute.ForgotPassword)return;
            destinationActive=false;Route=route;Message=route==ProductionAuthRoute.EmailPlaceholder?"Coming Soon":"";EmailError=EmailAuthError.None;EmailState=EmailOperationState.Idle;Notify();
        }
        public Task RegisterAsync(string email,string password,string confirmation)=>Execute(EmailOperationState.Submitting,async()=>{
            await identity.RegisterEmailAsync(email,password,confirmation);if(disposed)return;
            if(destination!=null){destinationActive=true;await destination.ResolveAsync();}
            SessionKind=AuthSessionKind.EmailUnverified;EmailState=EmailOperationState.Routing;Route=ProductionAuthRoute.VerificationPending;VerificationSent=false;Notify();
            await identity.SendVerificationAsync();if(disposed)return;VerificationSent=true;Message="Verification email sent.";
        });
        public Task SignInEmailAsync(string email,string password)=>Execute(EmailOperationState.SigningIn,async()=>{
            await identity.SignInEmailAsync(email,password);if(disposed)return;
            VerificationSent=false;await ClassifyAndBootstrap(identity.Current,false);
        });
        public Task ResetPasswordAsync(string email)=>Execute(EmailOperationState.Resetting,async()=>{
            await identity.SendPasswordResetAsync(email);if(disposed)return;
            Message=EmailAuthRules.ResetSuccess;
        });
        public Task CheckVerificationAsync()=>Execute(EmailOperationState.Checking,async()=>{
            await identity.ReloadEmailAsync();if(disposed)return;
            await ClassifyAndBootstrap(identity.Current,false);
            if(!disposed&&SessionKind==AuthSessionKind.EmailUnverified)Message="Your email is not verified yet. Check your inbox and try again.";
        });
        public Task ResendVerificationAsync()=>Execute(EmailOperationState.Resending,async()=>{
            await identity.SendVerificationAsync();if(disposed)return;VerificationSent=true;Message="Verification email sent.";
        });
        public Task CancelUnverifiedAsync(bool confirmed)=>Execute(EmailOperationState.Submitting,async()=>{
            // Only a newly created, still-unverified Email session owned by this service can be ended.
            if(player.HasConfirmedSnapshots)throw new EmailAuthException(EmailAuthError.SessionConflict);
            await identity.CancelCreatedEmailAsync(confirmed);if(disposed)return;
            if(destination!=null){destinationActive=true;await destination.ResolveAsync();destinationActive=false;}
            SessionKind=AuthSessionKind.NoSession;VerificationSent=false;Route=ProductionAuthRoute.EmailEntry;Message="";
        });
        Task Execute(EmailOperationState state,Func<Task> work)
        {
            if(disposed)return Task.CompletedTask;if(Busy)return operation??Task.CompletedTask;
            Busy=true;EmailState=state;EmailError=EmailAuthError.None;Message="";
            var completion=new TaskCompletionSource<bool>();operation=completion.Task;Notify();_=Run(work,completion);return operation;
        }
        async Task Run(Func<Task> work,TaskCompletionSource<bool> completion)
        {
            try{await work();if(!disposed)EmailState=EmailOperationState.Idle;}
            catch(Exception error){if(!disposed){
                EmailError=error is EmailAuthException email?email.Code:EmailAuthError.Unknown;EmailState=EmailOperationState.Error;
                Message=error is EmailAuthException?EmailAuthRules.Message(EmailError):"We could not connect. Check your connection and try again.";
                if(Route==ProductionAuthRoute.Loading||Route==ProductionAuthRoute.Welcome)Route=ProductionAuthRoute.Error;
            }}
            finally{if(!disposed){Busy=false;Notify();}completion.TrySetResult(true);}
        }
        void UpdateSessionKind(){var s=identity.Session;SessionKind=s==null?AuthSessionKind.NoSession:s.IsAnonymous?AuthSessionKind.RestoredGuest:s.IsPasswordProvider?s.IsEmailVerified?AuthSessionKind.EmailVerified:AuthSessionKind.EmailUnverified:AuthSessionKind.OtherRegistered;}
        void Notify(){Changed?.Invoke();}
        public async Task StopAsync(){Dispose();if(operation!=null)await operation;}
        public void Dispose(){if(disposed)return;disposed=true;lifetime.Cancel();if(destination!=null){destination.Changed-=Notify;destination.Dispose();}Changed=null;}
    }
}
