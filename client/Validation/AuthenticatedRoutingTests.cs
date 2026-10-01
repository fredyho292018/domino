using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Domino.Identity;
using Domino.Infrastructure.Api;
using Newtonsoft.Json;

public static class AuthenticatedRoutingTests
{
    static int checks;
    static void Need(bool value,string label){if(!value)throw new Exception(label);checks++;}
    public sealed class Fixture : IRoutingSessionSource, IRoutingPlayerBinding
    {
        public FirebaseAuthSessionSnapshot User=new FirebaseAuthSessionSnapshot("fixture-a",true,false,false);
        public OnboardingStateDto State=MakeState("NOT_STARTED",null);
        public int Sessions,Bootstraps,Reads,Disposals;
        public Exception SessionError,BootstrapError,ReadError;
        public TaskCompletionSource<bool> SessionHold,BootstrapHold,ReadHold;
        public string BootstrapOwner;
        public async Task<FirebaseAuthSessionSnapshot> ResolveAsync(CancellationToken t){Sessions++;var user=User;if(SessionHold!=null)await SessionHold.Task;if(SessionError!=null)throw SessionError;return user;}
        public IRoutingPlayerBinding Bind(FirebaseAuthSessionSnapshot s)=>this;
        public async Task<string> BootstrapAsync(CancellationToken t){Bootstraps++;var owner=BootstrapOwner??User.Uid;if(BootstrapHold!=null)await BootstrapHold.Task;if(BootstrapError!=null)throw BootstrapError;return owner;}
        public async Task<OnboardingStateDto> ReadOnboardingAsync(CancellationToken t){Reads++;var state=State;if(ReadHold!=null)await ReadHold.Task;if(ReadError!=null)throw ReadError;return state;}
        public void Dispose(){Disposals++;}
    }
    public static OnboardingStateDto MakeState(string status,int? version,string step=null)=>new OnboardingStateDto{
        status=status,catalogVersion=version,currentStepKey=step,revision=7,domainRevisions=new OnboardingDomainRevisionsDto(),answers=Array.Empty<OnboardingAnswerDto>(),
        updatedAt="2030-01-01T00:00:00Z",completedAt=status=="COMPLETED"?"2030-01-01T00:00:00Z":null,
        completionOrigin=status=="COMPLETED"?(version.HasValue?"FLOW":"LEGACY_EXEMPT"):null,
        completedStepKeys=Array.Empty<string>(),skippedStepKeys=Array.Empty<string>(),requiredFieldsMissing=Array.Empty<string>()};
    sealed class SwitchingSource:IRoutingSessionSource
    {
        public Fixture Current;
        public Task<FirebaseAuthSessionSnapshot> ResolveAsync(CancellationToken t)=>Current.ResolveAsync(t);
        public IRoutingPlayerBinding Bind(FirebaseAuthSessionSnapshot s)=>Current;
    }
    sealed class Wire:IApiTransport,IAuthTokenProvider,IRoutingSessionSource
    {
        public OnboardingStateDto State=MakeState("COMPLETED",null);
        public int Writes,Reads,Bootstraps;
        public Task<string> GetIdTokenAsync(bool force,CancellationToken t)=>Task.FromResult("isolated-fixture-only");
        public Task<FirebaseAuthSessionSnapshot> ResolveAsync(CancellationToken t)=>Task.FromResult(new FirebaseAuthSessionSnapshot("fixture-a",true,false,false));
        public IRoutingPlayerBinding Bind(FirebaseAuthSessionSnapshot identity){var config=new DominoApiConfiguration(true,"https://example.test");return new RoutingApiBinding(new DominoApiClient(config,this,this,new UnityApiJsonCodec()),new OnboardingApiSession(config,this,this,()=>identity.Uid,default),"en");}
        public Task<ApiHttpResponse> SendAsync(string method,Uri url,string body,string bearer,int timeout,CancellationToken t)
        {
            if(url.AbsolutePath=="/api/v1/player/bootstrap"){
                Bootstraps++;Need(method=="POST"&&TrialContractHeader.Required(method,url)&&TrialContractHeader.Name=="X-Trial-Activation-Contract"&&TrialContractHeader.Value=="1","bootstrap_header_contract");
                return Task.FromResult(new ApiHttpResponse(200,JsonConvert.SerializeObject(new PlayerBootstrapResponseDto{player=new PlayerResponseDto{uid="fixture-a",accountType="GUEST",status="ACTIVE",displayName="FixturePlayer",language="en"},wallet=new WalletResponseDto{coins=0}})));
            }
            if(method!="GET")Writes++;
            Need(method=="GET"&&url.AbsolutePath=="/api/v1/player/onboarding","existing_client_read");Reads++;
            return Task.FromResult(new ApiHttpResponse(200,JsonConvert.SerializeObject(State)));
        }
    }
    public static async Task<int> Run()
    {
        checks=0;
        var f=new Fixture{User=null};using(var r=new AuthenticatedRoutingOrchestrator(f)){await r.ResolveAsync();Need(r.Route==AuthenticatedRoute.Welcome&&f.Bootstraps==0&&f.Reads==0,"no_session");}
        f=new Fixture{User=new FirebaseAuthSessionSnapshot("fixture-a",false,false,true)};
        using(var r=new AuthenticatedRoutingOrchestrator(f)){await r.ResolveAsync();Need(r.Route==AuthenticatedRoute.VerificationPending&&f.Bootstraps==0&&f.Reads==0,"unverified");f.User=new FirebaseAuthSessionSnapshot("fixture-a",false,true,true);await r.ResolveAsync();Need(r.Route==AuthenticatedRoute.Onboarding&&f.Bootstraps==1,"verification_transition");}
        foreach(bool guest in new[]{true,false})foreach(int version in new[]{1,2})foreach(string status in new[]{"NOT_STARTED","IN_PROGRESS","COMPLETED"}){
            f=new Fixture{User=new FirebaseAuthSessionSnapshot("fixture-a",guest,!guest,!guest),State=MakeState(status,status=="NOT_STARTED"?(int?)null:version,status=="IN_PROGRESS"?"COACH_STEP":null)};
            using(var r=new AuthenticatedRoutingOrchestrator(f)){await r.ResolveAsync();Need(r.Route==(status=="COMPLETED"?AuthenticatedRoute.Home:AuthenticatedRoute.Onboarding),"matrix");Need(r.Onboarding.catalogVersion==f.State.catalogVersion&&r.Onboarding.currentStepKey==f.State.currentStepKey,"pinned_cursor");Need(f.Bootstraps==1&&f.Reads==1,"one_resolution");}
        }
        foreach(string step in new[]{"BASIC_PROFILE_STEP","EXPERIENCE_STEP","COACH_STEP","CONTACTS_STEP","MEMBERSHIP_STEP"}){f=new Fixture{State=MakeState("IN_PROGRESS",2,step)};using var r=new AuthenticatedRoutingOrchestrator(f);await r.ResolveAsync();Need(r.Onboarding.currentStepKey==step,"exact_step");}
        f=new Fixture{User=new FirebaseAuthSessionSnapshot("fixture-a",false,false,false),State=MakeState("COMPLETED",1)};using(var r=new AuthenticatedRoutingOrchestrator(f)){await r.ResolveAsync();Need(r.Route==AuthenticatedRoute.Home,"future_provider_not_password");}
        foreach(var stage in new[]{RoutingStage.Session,RoutingStage.Player,RoutingStage.Onboarding}){
            f=new Fixture();var error=new Exception("fixture failure not for presentation");if(stage==RoutingStage.Session)f.SessionError=error;else if(stage==RoutingStage.Player)f.BootstrapError=error;else f.ReadError=error;
            using var r=new AuthenticatedRoutingOrchestrator(f);await r.ResolveAsync();Need(r.Route==AuthenticatedRoute.Error&&r.CanRetry&&r.Onboarding==null&&!r.Message.Contains(error.Message),"safe_failure");Need(r.FailedStage==stage,"retry_stage");
            f.SessionError=f.BootstrapError=f.ReadError=null;await r.RetryAsync();Need(r.Route==AuthenticatedRoute.Onboarding,"retry_result");Need(f.Sessions==(stage==RoutingStage.Session?2:1)&&f.Bootstraps==(stage==RoutingStage.Player?2:1)&&f.Reads==(stage==RoutingStage.Onboarding?2:1),"retry_only_required_stage");
        }
        f=new Fixture{BootstrapError=new DominoApiException(ApiFailure.Server,409,"CLIENT_UPDATE_REQUIRED")};using(var r=new AuthenticatedRoutingOrchestrator(f)){await r.ResolveAsync();await r.RetryAsync();Need(r.Route==AuthenticatedRoute.UpdateRequired&&!r.CanRetry&&f.Bootstraps==1&&f.Reads==0,"update_required");}
        f=new Fixture{BootstrapOwner="fixture-wrong"};using(var r=new AuthenticatedRoutingOrchestrator(f)){await r.ResolveAsync();Need(r.Route==AuthenticatedRoute.Error&&f.Reads==0,"bootstrap_identity_mismatch");}
        f=new Fixture{SessionHold=new TaskCompletionSource<bool>(),State=MakeState("COMPLETED",2)};using(var r=new AuthenticatedRoutingOrchestrator(f)){var seen=new List<AuthenticatedRoute>();r.Changed+=()=>seen.Add(r.Route);var task=r.ResolveAsync();Need(r.Route==AuthenticatedRoute.ResolvingSession,"restore_wait");f.SessionHold.SetResult(true);await task;Need(r.Route==AuthenticatedRoute.Home&&!seen.Contains(AuthenticatedRoute.Welcome),"restore_no_welcome_flash");f.User=null;await r.ResolveAsync();Need(r.Route==AuthenticatedRoute.Welcome&&r.Onboarding==null,"logout_clears");}
        foreach(var stage in new[]{RoutingStage.Session,RoutingStage.Player,RoutingStage.Onboarding}){
            var a=new Fixture{State=MakeState("COMPLETED",2)};var hold=new TaskCompletionSource<bool>();if(stage==RoutingStage.Session)a.SessionHold=hold;else if(stage==RoutingStage.Player)a.BootstrapHold=hold;else a.ReadHold=hold;
            var b=new Fixture{User=new FirebaseAuthSessionSnapshot("fixture-b",true,false,false),State=MakeState("IN_PROGRESS",1,"MEMBERSHIP_STEP")};var source=new SwitchingSource{Current=a};using var r=new AuthenticatedRoutingOrchestrator(source);var task=r.ResolveAsync();Need(r.Onboarding==null,"loading_no_stale_screen");source.Current=b;await r.ResolveAsync();hold.SetResult(true);await task;Need(r.Route==AuthenticatedRoute.Onboarding&&r.Onboarding.currentStepKey=="MEMBERSHIP_STEP"&&r.Onboarding.catalogVersion==1,"late_a_ignored");Need(b.Bootstraps==1&&b.Reads==1,"b_owns");
        }
        f=new Fixture();using(var r=new AuthenticatedRoutingOrchestrator(f)){await r.ResolveAsync();r.Onboarding.status="COMPLETED";Need(r.Route==AuthenticatedRoute.Onboarding,"snapshot_not_authority");f.State=MakeState("COMPLETED",2);f.State.revision=9;await r.ReevaluateOnboardingAsync();Need(r.Route==AuthenticatedRoute.Home&&f.Bootstraps==1&&f.Reads==2,"authoritative_completion_reread");}
        f=new Fixture{State=MakeState("IN_PROGRESS",1,"COACH_STEP")};using(var r=new AuthenticatedRoutingOrchestrator(f)){await r.ResolveAsync();f.State=MakeState("COMPLETED",2);await r.ReevaluateOnboardingAsync();Need(r.Route==AuthenticatedRoute.Error,"pinned_version_no_upgrade");await r.RetryAsync();Need(r.Route==AuthenticatedRoute.Error,"retry_preserves_version_guard");}
        f=new Fixture{ReadHold=new TaskCompletionSource<bool>()};using(var r=new AuthenticatedRoutingOrchestrator(f)){var task=r.ResolveAsync();r.Dispose();f.ReadHold.SetResult(true);await task;Need(r.Onboarding==null,"disposed_late_ignored");}
        foreach(string status in new[]{"UNKNOWN","IN_PROGRESS"}){f=new Fixture{State=MakeState(status,2,"UNKNOWN")};using var r=new AuthenticatedRoutingOrchestrator(f);await r.ResolveAsync();Need(r.Route==AuthenticatedRoute.Error,"malformed_fail_closed");}
        var wire=new Wire();using(var r=new AuthenticatedRoutingOrchestrator(wire)){await r.ResolveAsync();Need(r.Route==AuthenticatedRoute.Home&&r.Onboarding.catalogVersion==null,"legacy_via_client01");Need(wire.Writes==0&&wire.Reads==1&&wire.Bootstraps==1,"side_effects_zero");}
        foreach(string origin in new[]{"FLOW","UNKNOWN",null}){wire=new Wire();wire.State.completionOrigin=origin;using var r=new AuthenticatedRoutingOrchestrator(wire);await r.ResolveAsync();Need(r.Route==AuthenticatedRoute.Error,"legacy_negative_contract");}
        for(int malformed=0;malformed<4;malformed++){wire=new Wire();if(malformed==0)wire.State.completedAt=null;if(malformed==1)wire.State.currentStepKey="COACH_STEP";if(malformed==2)wire.State.startedAt="2030-01-01T00:00:00Z";if(malformed==3)wire.State.completedStepKeys=new[]{"COACH_STEP"};using var r=new AuthenticatedRoutingOrchestrator(wire);await r.ResolveAsync();Need(r.Route==AuthenticatedRoute.Error,"legacy_malformed_contract");}
        f=new Fixture{State=MakeState("IN_PROGRESS",2,"COACH_STEP")};using(var r=new AuthenticatedRoutingOrchestrator(f)){await r.ResolveAsync();f.State=MakeState("COMPLETED",2);f.State.revision=6;await r.ReevaluateOnboardingAsync();Need(r.Route==AuthenticatedRoute.Error,"revision_rollback");await r.RetryAsync();Need(r.Route==AuthenticatedRoute.Error,"retry_revision_guard");}
        f=new Fixture{ReadError=new DominoApiException(ApiFailure.Server,409,"CLIENT_UPDATE_REQUIRED")};using(var r=new AuthenticatedRoutingOrchestrator(f)){await r.ResolveAsync();await r.RetryAsync();Need(r.Route==AuthenticatedRoute.UpdateRequired&&f.Reads==1,"onboarding_update_required");}
        return checks;
    }
    public static async Task Main(){Console.WriteLine("ROUTING_CHECKS="+await Run()+"_PASS REAL_NETWORK_CALLS=0");}
}
