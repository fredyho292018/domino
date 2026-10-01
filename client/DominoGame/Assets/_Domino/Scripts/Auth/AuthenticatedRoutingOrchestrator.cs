using System;
using System.Threading;
using System.Threading.Tasks;
using Domino.Infrastructure.Api;
using Newtonsoft.Json;

namespace Domino.Identity
{
    public enum AuthenticatedRoute { ResolvingSession, Welcome, VerificationPending, ResolvingPlayer, ResolvingOnboarding, Onboarding, Home, Error, UpdateRequired }
    public enum RoutingStage { Session, Player, Onboarding }
    public interface IRoutingSessionSource
    {
        Task<FirebaseAuthSessionSnapshot> ResolveAsync(CancellationToken token);
        // A new binding belongs exclusively to this resolution epoch.
        IRoutingPlayerBinding Bind(FirebaseAuthSessionSnapshot identity);
    }
    public interface IRoutingPlayerBinding : IDisposable
    {
        Task<string> BootstrapAsync(CancellationToken token);
        Task<OnboardingStateDto> ReadOnboardingAsync(CancellationToken token);
    }
    // Opt-in adapter only. No startup registration, token acquisition, or duplicate networking.
    public sealed class RoutingApiBinding : IRoutingPlayerBinding
    {
        readonly IDominoApiClient bootstrap;
        readonly OnboardingApiSession session;
        readonly OnboardingApiClient onboarding;
        readonly string locale;
        public RoutingApiBinding(IDominoApiClient bootstrap, OnboardingApiSession session, string locale)
        { this.bootstrap=bootstrap; this.session=session; this.locale=OnboardingApiSession.Locale(locale); onboarding=new OnboardingApiClient(session); }
        public async Task<string> BootstrapAsync(CancellationToken token)
        {
            session.EnsureCurrent();
            var response=await bootstrap.BootstrapAsync(locale,token);
            session.EnsureCurrent();
            if(response?.player?.status!="ACTIVE")throw new DominoApiException(ApiFailure.Contract);
            return response.player.uid;
        }
        public Task<OnboardingStateDto> ReadOnboardingAsync(CancellationToken token)=>onboarding.LoadAsync(token);
        public void Dispose()=>session.Dispose();
    }
    // Future sole authenticated destination owner. Deliberately NOT wired into ApplicationServices.
    // Call ResolveAsync on every session change (including logout and verification refresh).
    public sealed class AuthenticatedRoutingOrchestrator : IDisposable
    {
        readonly IRoutingSessionSource source;
        CancellationTokenSource epoch;
        IRoutingPlayerBinding binding;
        FirebaseAuthSessionSnapshot identity;
        OnboardingStateDto onboarding, lastConfirmed;
        long generation;
        bool disposed, busy;
        public AuthenticatedRoute Route { get; private set; }=AuthenticatedRoute.ResolvingSession;
        public RoutingStage FailedStage { get; private set; }
        public bool CanRetry=>!disposed&&!busy&&Route==AuthenticatedRoute.Error;
        public OnboardingStateDto Onboarding=>onboarding==null?null:JsonConvert.DeserializeObject<OnboardingStateDto>(JsonConvert.SerializeObject(onboarding));
        public string Message=>Route==AuthenticatedRoute.Error?"Could not load your session. Please try again.":Route==AuthenticatedRoute.UpdateRequired?"Update the app to continue.":"";
        public event Action Changed;
        public AuthenticatedRoutingOrchestrator(IRoutingSessionSource source){this.source=source??throw new ArgumentNullException(nameof(source));}
        public Task ResolveAsync()
        {
            if(disposed)return Task.CompletedTask;
            generation++; epoch?.Cancel(); binding?.Dispose(); binding=null; epoch?.Dispose();
            epoch=new CancellationTokenSource(); identity=null; onboarding=null; lastConfirmed=null;
            return RunAsync(RoutingStage.Session,generation,epoch.Token);
        }
        public Task RetryAsync()=>CanRetry?RunAsync(FailedStage,generation,epoch.Token):Task.CompletedTask;
        // Completion notifications carry no local completed flag. Re-read through CLIENT-01.
        public Task ReevaluateOnboardingAsync()=>!disposed&&!busy&&Route==AuthenticatedRoute.Onboarding
            ?RunAsync(RoutingStage.Onboarding,generation,epoch.Token):Task.CompletedTask;
        bool Current(long run,CancellationToken token)=>!disposed&&run==generation&&!token.IsCancellationRequested;
        void Publish(AuthenticatedRoute route){Route=route;Changed?.Invoke();}
        async Task RunAsync(RoutingStage stage,long run,CancellationToken token)
        {
            busy=true;
            var prior=lastConfirmed; onboarding=null;
            try
            {
                if(stage==RoutingStage.Session)
                {
                    Publish(AuthenticatedRoute.ResolvingSession);
                    if(!Current(run,token))return;
                    var resolved=await source.ResolveAsync(token);
                    if(!Current(run,token))return;
                    identity=resolved;
                    if(identity==null){Publish(AuthenticatedRoute.Welcome);return;}
                    if(string.IsNullOrWhiteSpace(identity.Uid))throw new DominoApiException(ApiFailure.Contract);
                    if(!identity.IsAnonymous&&identity.IsPasswordProvider&&!identity.IsEmailVerified)
                    {Publish(AuthenticatedRoute.VerificationPending);return;}
                    binding=source.Bind(identity)??throw new DominoApiException(ApiFailure.Contract);
                    stage=RoutingStage.Player;
                }
                if(stage==RoutingStage.Player)
                {
                    Publish(AuthenticatedRoute.ResolvingPlayer);
                    if(!Current(run,token))return;
                    var owner=await binding.BootstrapAsync(token);
                    if(!Current(run,token))return;
                    if(owner!=identity.Uid)throw new DominoApiException(ApiFailure.Contract);
                    stage=RoutingStage.Onboarding;
                }
                Publish(AuthenticatedRoute.ResolvingOnboarding);
                if(!Current(run,token))return;
                var result=await binding.ReadOnboardingAsync(token);
                if(!Current(run,token))return;
                Validate(result);
                if(prior!=null&&(result.revision<prior.revision||prior.catalogVersion.HasValue&&result.catalogVersion!=prior.catalogVersion))throw new DominoApiException(ApiFailure.Contract);
                onboarding=JsonConvert.DeserializeObject<OnboardingStateDto>(JsonConvert.SerializeObject(result));
                lastConfirmed=onboarding;
                Publish(result.status=="COMPLETED"?AuthenticatedRoute.Home:AuthenticatedRoute.Onboarding);
            }
            catch(Exception error)
            {
                if(!Current(run,token))return;
                FailedStage=stage; onboarding=null;
                Publish(error is DominoApiException api&&api.ServerErrorCode=="CLIENT_UPDATE_REQUIRED"?AuthenticatedRoute.UpdateRequired:AuthenticatedRoute.Error);
            }
            finally{if(Current(run,token)){busy=false;Changed?.Invoke();}}
        }
        static void Validate(OnboardingStateDto state)
        {
            if(state==null||state.revision<0)throw new DominoApiException(ApiFailure.Contract);
            if(state.status=="NOT_STARTED")
            {if(state.currentStepKey!=null||state.catalogVersion!=null)throw new DominoApiException(ApiFailure.Contract);return;}
            if(state.status!="IN_PROGRESS"&&state.status!="COMPLETED")throw new DominoApiException(ApiFailure.Contract);
            if(state.status=="COMPLETED"&&state.completionOrigin=="LEGACY_EXEMPT"&&state.catalogVersion==null&&state.currentStepKey==null)return;
            if(state.catalogVersion!=1&&state.catalogVersion!=2)throw new DominoApiException(ApiFailure.Server,409,"CLIENT_UPDATE_REQUIRED");
            if(state.status=="COMPLETED"){if(state.currentStepKey!=null)throw new DominoApiException(ApiFailure.Contract);return;}
            var allowed=new[]{"BASIC_PROFILE_STEP","EXPERIENCE_STEP","COACH_STEP","CONTACTS_STEP","MEMBERSHIP_STEP"};
            if(Array.IndexOf(allowed,state.currentStepKey)<0||state.catalogVersion==1&&state.currentStepKey=="BASIC_PROFILE_STEP")throw new DominoApiException(ApiFailure.Contract);
        }
        public void Dispose(){if(disposed)return;disposed=true;generation++;epoch?.Cancel();binding?.Dispose();epoch?.Dispose();binding=null;identity=null;onboarding=null;lastConfirmed=null;Changed=null;}
    }
}
