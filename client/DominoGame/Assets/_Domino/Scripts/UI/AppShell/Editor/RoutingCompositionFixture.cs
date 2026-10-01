using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Domino.Identity;
using Domino.Infrastructure.Api;
using Domino.Infrastructure.Firebase;
using Domino.Player;
using Domino.UI.AppShell;
using Newtonsoft.Json;

namespace Domino.Editor
{
    // No SDK or HTTP construction. Exercises the same composition constructor used at startup.
    public sealed class RoutingCompositionFixture : IFirebaseClient, IFirebaseEmailSessionClient, IAuthTokenProvider, IFirebaseSessionControl, IApiTransport, IDisposable
    {
        public FirebaseAuthSessionSnapshot Session=new FirebaseAuthSessionSnapshot("routing-fixture",true,false,false);
        public readonly IsolatedOnboardingServer Server;
        public readonly FirebaseAuthService Auth;
        public readonly PlayerService Player;
        public readonly ProductionRoutingComposition Composition;
        public readonly ProductionAuthRouter Forms;
        public int BootstrapCalls,StateCalls,AuthWrites;
        public string ErrorAt;
        public TaskCompletionSource<bool> Hold;
        public RoutingCompositionFixture(int version=2)
        {
            Server=new IsolatedOnboardingServer(version,Catalog,l=>new CoachCatalogDto{catalogVersion=1,resolvedLocale=l,items=new[]{new CoachDto{key="MATEO",name="Mateo",selectable=true}}},l=>new MembershipCatalogDto{schemaVersion=1,catalogVersion=1,resolvedLocale=l,plans=Array.Empty<MembershipPlanDto>(),features=Array.Empty<MembershipFeatureDto>(),trialPresentation=new MembershipTrialDto()});
            Auth=new FirebaseAuthService(new FirebaseBootstrap(this,null),this,null,default,true);
            var config=new DominoApiConfiguration(true,"https://example.test");
            Player=new PlayerService(Auth,new DominoApiClient(config,this,this,new UnityApiJsonCodec()),()=>Task.FromResult("en"),default);
            Composition=new ProductionRoutingComposition(Auth,Player,()=>new OnboardingApiSession(config,this,this,()=>Session?.Uid,default),()=>"en");
            Forms=new ProductionAuthRouter(Auth,Player,destination:Composition);
        }
        static OnboardingCatalogDto Catalog(int v,string locale)=>new OnboardingCatalogDto{
            catalogVersion=v,locale=locale,requiredCapabilities=Array.Empty<string>(),coachCatalogVersion=1,membershipCatalogVersion=1,
            steps=(v==2?new[]{"BASIC_PROFILE_STEP","EXPERIENCE_STEP","COACH_STEP","CONTACTS_STEP","MEMBERSHIP_STEP"}:new[]{"EXPERIENCE_STEP","COACH_STEP","CONTACTS_STEP","MEMBERSHIP_STEP"}).Select(k=>new OnboardingStepDto{key=k,title=k,required=!k.Contains("CONTACTS")&&!k.Contains("MEMBERSHIP"),skippable=k.Contains("CONTACTS")||k.Contains("MEMBERSHIP"),questions=k=="EXPERIENCE_STEP"?new[]{new OnboardingQuestionDto{key="DOMINO_EXPERIENCE",type="SINGLE_SELECT",options=new[]{"BEGINNER","RULES_KNOWN","STRATEGY","COMPETITIVE"}.Select(x=>new OnboardingOptionDto{key=x,title=x}).ToArray()}}:k=="COACH_STEP"?new[]{new OnboardingQuestionDto{key="COACH_SELECTION",type="COACH_SELECT"}}:Array.Empty<OnboardingQuestionDto>()}).ToArray()};
        public void State(string status,int? version=null,string step=null){Server.State.status=status;Server.State.catalogVersion=version;Server.State.currentStepKey=step;if(status=="COMPLETED"){Server.State.completedAt="2030-01-01T00:00:00Z";Server.State.completionOrigin=version.HasValue?"FLOW":"LEGACY_EXEMPT";}}
        public Task<string> CheckDependenciesAsync()=>Task.FromResult("Available");
        public void InitializeApp(){}
        public PlayerIdentity GetCurrentUser()=>Session==null?null:new PlayerIdentity(Session.Uid,Session.IsAnonymous);
        public FirebaseAuthSessionSnapshot GetSession()=>Session;
        public Task<PlayerIdentity> SignInAnonymouslyAsync(){AuthWrites++;throw new InvalidOperationException("Not used by routing fixture");}
        public Task<FirebaseAuthSessionSnapshot> CreateEmailAsync(string email,string password){AuthWrites++;throw new InvalidOperationException();}
        public Task ReloadEmailAsync(string uid)=>Task.CompletedTask;
        public Task SendVerificationAsync(string uid){AuthWrites++;throw new InvalidOperationException();}
        public void SignOutUnverifiedEmail(string uid){AuthWrites++;Session=null;}
        public void SignOut(){Session=null;}
        public Task<string> GetIdTokenAsync(bool force,CancellationToken token)=>Task.FromResult("isolated-fixture-only");
        public async Task<ApiHttpResponse> SendAsync(string method,Uri url,string body,string bearer,int timeout,CancellationToken token)
        {
            if(Hold!=null)await Hold.Task;
            var path=url.AbsolutePath;
            if(path.EndsWith("/bootstrap")){
                BootstrapCalls++;if(ErrorAt=="BOOTSTRAP"||ErrorAt=="UPDATE")return new ApiHttpResponse(ErrorAt=="UPDATE"?409:503,JsonConvert.SerializeObject(new{requestId="00000000-0000-0000-0000-000000000001",code=ErrorAt=="UPDATE"?"CLIENT_UPDATE_REQUIRED":"DEPENDENCY_UNAVAILABLE"}));
                return new ApiHttpResponse(200,JsonConvert.SerializeObject(new PlayerBootstrapResponseDto{player=new PlayerResponseDto{uid=Session.Uid,status="ACTIVE",accountType=Session.IsAnonymous?"GUEST":"REGISTERED",language="en",displayName="Fixture"},wallet=new WalletResponseDto{coins=0},trialEligibility=Server.Eligibility,entitlements=Server.Access}));
            }
            if(path.EndsWith("/onboarding")){StateCalls++;if(ErrorAt=="ONBOARDING")return new ApiHttpResponse(503,"{\"code\":\"DEPENDENCY_UNAVAILABLE\"}");}
            return await Server.SendAsync(method,url,body,bearer,timeout,token);
        }
        public void Dispose(){Forms.Dispose();Player.Dispose();}
    }
}
