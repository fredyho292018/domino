using System;
using System.Threading;
using System.Threading.Tasks;
using Domino.Identity;
using Domino.Infrastructure.Firebase;
using Domino.Infrastructure.Api;
using Domino.Player;
static class GuestAccount01BTests
{
    static int checks;static void Need(bool value,string key){checks++;if(!value)throw new Exception(key);}
    static string Secret()=>new string('x',12);
    sealed class Fake:IFirebaseClient,IFirebaseEmailSessionClient,IFirebaseCredentialLinkClient,IAuthTokenProvider,IFirebaseSessionControl
    {
        public FirebaseAuthSessionSnapshot Session=new FirebaseAuthSessionSnapshot("FIXTURE_A",true,false,false);
        public int Links,Sends,Reloads,Creates,Outs,Tokens;public bool CommitThenFail,WrongUid,Verify,SendFail,RefreshFail;
        public EmailAuthError Failure;public TaskCompletionSource<bool> Hold;
        public Task<string> CheckDependenciesAsync()=>Task.FromResult("Available");public void InitializeApp(){}
        public PlayerIdentity GetCurrentUser()=>Session==null?null:new PlayerIdentity(Session.Uid,Session.IsAnonymous);
        public Task<PlayerIdentity> SignInAnonymouslyAsync()=>throw new Exception("FORBIDDEN_CREATE");
        public FirebaseAuthSessionSnapshot GetSession()=>Session;
        public Task<FirebaseAuthSessionSnapshot> CreateEmailAsync(string e,string p){Creates++;throw new Exception("FORBIDDEN_CREATE");}
        public async Task<FirebaseAuthSessionSnapshot> LinkCurrentUserAsync(string uid,string e,string p){
            Links++;if(Hold!=null)await Hold.Task;
            if(CommitThenFail)Session=new FirebaseAuthSessionSnapshot(uid,false,false,true);
            if(Failure!=EmailAuthError.None)throw new EmailAuthException(Failure);
            if(Session.Uid!=uid)throw new EmailAuthException(EmailAuthError.SessionConflict);
            Session=new FirebaseAuthSessionSnapshot(WrongUid?"FIXTURE_B":uid,false,false,true);return Session;
        }
        public Task<FirebaseAuthSessionSnapshot> ReloadLinkSessionAsync(string uid){Reloads++;if(Session.Uid!=uid)throw new EmailAuthException(EmailAuthError.SessionConflict);return Task.FromResult(Session);}
        public Task ReloadEmailAsync(string uid){Reloads++;if(Session.Uid!=uid)throw new EmailAuthException(EmailAuthError.SessionConflict);if(Verify)Session=new FirebaseAuthSessionSnapshot(uid,false,true,true);return Task.CompletedTask;}
        public Task SendVerificationAsync(string uid){Sends++;if(SendFail)throw new EmailAuthException(EmailAuthError.NetworkError);return Task.CompletedTask;}
        public void SignOutUnverifiedEmail(string uid)=>throw new Exception("FORBIDDEN_CANCEL_LINK");
        public void SignOut(){Outs++;Session=null;}
        public Task<string> GetIdTokenAsync(bool refresh,CancellationToken token){Tokens++;if(RefreshFail)throw new EmailAuthException(EmailAuthError.NetworkError);return Task.FromResult("isolated");}
    }
    sealed class Api:IDominoApiClient
    {
        public Fake Auth;public int Bootstraps;public bool Fail;public bool IsAvailable=>true;
        public Task<PlayerBootstrapResponseDto> BootstrapAsync(string locale,CancellationToken token){Bootstraps++;if(Fail)throw new DominoApiException(ApiFailure.Transport);return Task.FromResult(new PlayerBootstrapResponseDto{
            player=new PlayerResponseDto{uid=Auth.Session.Uid,accountType=Auth.Session.IsAnonymous?"GUEST":"REGISTERED",displayName="Guest-FIXTURE",language="en",status="ACTIVE",createdAt="2026-01-01T00:00:00Z"},wallet=new WalletResponseDto{coins=7}});}
        public Task<PlayerBootstrapResponseDto> UpdateDisplayNameAsync(string alias,CancellationToken token)=>throw new Exception("FORBIDDEN_ALIAS_WRITE");
    }
    static async Task<(FirebaseAuthService auth,AccountLinkController link,PlayerService player,ProductionAuthRouter router,Api api)> Build(Fake sdk)
    {
        var auth=new FirebaseAuthService(new FirebaseBootstrap(sdk,null),sdk,null,default,true);await auth.RestoreAsync();
        var api=new Api{Auth=sdk};var player=new PlayerService(auth,api,()=>Task.FromResult("en"),default);await player.InitializeAsync();
        var router=new ProductionAuthRouter(auth,player);
        AccountLinkSnapshot Capture()=>new AccountLinkSnapshot().Add("Uid",sdk.Session?.Uid).Add("Player",player.Player.Uid).Add("Alias",player.Player.DisplayName).Add("Wallet",player.Wallet.Coins.ToString()).Add("CreatedAt",player.Player.CreatedAt.ToString());
        var link=new AccountLinkController(auth,Capture,async()=>{await router.AcceptLinkedAccountAsync();if(router.EmailError!=EmailAuthError.None)throw new EmailAuthException(router.EmailError);});
        return(auth,link,player,router,api);
    }
    static Task Submit(AccountLinkController link)=>link.SubmitAsync("fixture@example.invalid",Secret(),Secret());
    sealed class Destination:IAuthenticatedDestination
    {
        public ProductionAuthRoute Route{get;set;}
        public string Message=>"";public int Resolves;
        public event Action Changed {add{}remove{}}
        public Task ResolveAsync(){Resolves++;return Task.CompletedTask;}
        public Task RetryAsync()=>ResolveAsync();public void Dispose(){}
    }
    static async Task Main(){
        {
            var sdk=new Fake();var f=await Build(sdk);await Submit(f.link);
            Need(sdk.Links==1&&sdk.Creates==0&&sdk.Outs==0,"ONLY_LINK");
            Need(f.auth.Current.Uid=="FIXTURE_A"&&!f.auth.Current.IsAnonymous,"SAME_UID_ADOPTED");
            Need(f.link.Before.Same(f.link.After,"Player")&&f.link.Before.Same(f.link.After,"Alias"),"SNAPSHOT_PRESERVED");
            Need(f.router.Route==ProductionAuthRoute.VerificationPending&&sdk.Sends==1&&f.api.Bootstraps==1,"UNVERIFIED_NO_BOOTSTRAP");
            Need(!f.auth.CanCancelCreatedEmail&&!f.link.CanCancel,"NO_ROLLBACK");
            Need(AuthRecoverabilityClassifier.Classify(sdk.Session)==AuthRecoverability.Recoverable,"UNVERIFIED_RECOVERABLE");
            sdk.Verify=true;await f.router.CheckVerificationAsync();
            Need(f.router.Route==ProductionAuthRoute.AppShell&&f.api.Bootstraps==2&&sdk.Tokens>0,"VERIFIED_FORCED_BOOTSTRAP");
            Need(f.player.Player.Uid=="FIXTURE_A"&&f.player.Player.AccountType==PlayerAccountType.Registered&&f.player.Player.DisplayName=="Guest-FIXTURE"&&f.player.Wallet.Coins==7,"SAME_PLAYER_ALIAS_WALLET");
        }
        foreach(var error in new[]{EmailAuthError.EmailAlreadyInUse,EmailAuthError.InvalidEmail,EmailAuthError.WeakPassword,EmailAuthError.NetworkError}){
            var sdk=new Fake{Failure=error};var f=await Build(sdk);await Submit(f.link);
            Need(f.link.Error==error&&f.link.State==AccountLinkState.ErrorBeforeLink,"FIELD_ERROR_"+error);
            Need(f.auth.Current.IsAnonymous&&sdk.Outs==0&&sdk.Creates==0&&f.link.CanCancel,"PRELINK_PRESERVED");
            sdk.Failure=EmailAuthError.None;await Submit(f.link);Need(sdk.Reloads==1&&sdk.Links==2,"RECONCILE_BEFORE_RETRY");
        }
        foreach(var input in new[]{("bad",Secret(),Secret()),("fixture@example.invalid","x","x"),("fixture@example.invalid",Secret(),"different")}){
            var sdk=new Fake();var f=await Build(sdk);await f.link.SubmitAsync(input.Item1,input.Item2,input.Item3);Need(sdk.Links==0&&f.auth.Current.IsAnonymous,"LOCAL_INVALID_NO_CALL");
        }
        {var sdk=new Fake{Failure=EmailAuthError.NetworkError,CommitThenFail=true};var f=await Build(sdk);await Submit(f.link);Need(sdk.Links==1&&f.link.State==AccountLinkState.VerificationPending,"COMMITTED_ERROR_RECOVERED");}
        {var sdk=new Fake{SendFail=true};var f=await Build(sdk);await Submit(f.link);Need(f.link.State==AccountLinkState.ErrorAfterLink&&f.router.Route==ProductionAuthRoute.VerificationPending,"SEND_FAILURE_AFTER_LINK");sdk.SendFail=false;await f.router.ResendVerificationAsync();Need(sdk.Links==1&&sdk.Sends==2,"RESEND_NO_RELINK");}
        {var sdk=new Fake{WrongUid=true};var f=await Build(sdk);await Submit(f.link);Need(f.link.Error==EmailAuthError.SessionConflict&&f.auth.Current.Uid=="FIXTURE_A"&&sdk.Sends==0,"UID_MISMATCH_REJECTED");}
        {var sdk=new Fake{Hold=new TaskCompletionSource<bool>()};var f=await Build(sdk);var first=Submit(f.link);var second=Submit(f.link);Need(ReferenceEquals(first,second)&&sdk.Links==1,"SINGLE_FLIGHT");sdk.Hold.SetResult(true);await first;Need(sdk.Sends==1,"ONE_VERIFICATION");}
        {var sdk=new Fake{Hold=new TaskCompletionSource<bool>()};var f=await Build(sdk);var first=Submit(f.link);sdk.Session=new FirebaseAuthSessionSnapshot("FIXTURE_B",true,false,false);sdk.Hold.SetResult(true);await first;Need(f.link.Error==EmailAuthError.SessionConflict&&sdk.Sends==0,"STALE_RESPONSE_REJECTED");}
        {var sdk=new Fake();var f=await Build(sdk);f.link.Dispose();await Submit(f.link);Need(sdk.Links==0&&sdk.Outs==0,"CANCEL_NO_SIDE_EFFECT");}
        foreach(bool tokenFailure in new[]{false,true}){var sdk=new Fake();var f=await Build(sdk);await Submit(f.link);sdk.Verify=true;sdk.RefreshFail=tokenFailure;f.api.Fail=!tokenFailure;await f.router.CheckVerificationAsync();Need(f.router.Route==ProductionAuthRoute.VerificationPending&&sdk.Links==1,"POSTLINK_RETRY_PENDING");sdk.RefreshFail=false;f.api.Fail=false;await f.router.CheckVerificationAsync();Need(f.router.Route==ProductionAuthRoute.AppShell&&sdk.Links==1,"POSTLINK_RECOVERY_NO_RELINK");}
        {var sdk=new Fake();var f=await Build(sdk);int captures=0;var link=new AccountLinkController(f.auth,()=>new AccountLinkSnapshot().Add("Alias",(++captures).ToString()),()=>throw new Exception("MUST_NOT_VERIFY"));await Submit(link);Need(link.State==AccountLinkState.ErrorAfterLink&&!link.CanCancel&&!link.CanRetryAfterLink,"PRESERVATION_CONFLICT_BLOCKS_ROLLBACK_AND_RETRY");await Submit(link);Need(sdk.Links==1&&sdk.Sends==0,"PRESERVATION_CONFLICT_NO_RELINK");}
        {var sdk=new Fake{WrongUid=true};var f=await Build(sdk);await Submit(f.link);Need(!f.link.CanCancel,"CONFLICT_NOT_PRESENTED_AS_GUEST_CANCEL");}
        foreach(var route in new[]{ProductionAuthRoute.Onboarding,ProductionAuthRoute.AppShell}){
            var sdk=new Fake();var f=await Build(sdk);var destination=new Destination{Route=route};var router=new ProductionAuthRouter(f.auth,f.player,destination:destination);
            await f.auth.LinkGuestEmailAsync("fixture@example.invalid",Secret(),Secret());await router.AcceptLinkedAccountAsync();
            Need(destination.Resolves==0&&router.Route==ProductionAuthRoute.VerificationPending,"NO_PREMATURE_DESTINATION");
            sdk.Verify=true;await router.CheckVerificationAsync();Need(destination.Resolves==1&&router.Route==route&&sdk.Links==1,"AUTHORITATIVE_DESTINATION_REUSED_"+route);
        }
        foreach(var state in new[]{"IN_PROGRESS","COMPLETED"}){
            var sdk=new Fake();var f=await Build(sdk);using(var fixture=new Domino.Editor.RoutingCompositionFixture(2)){
                fixture.State(state,2,state=="IN_PROGRESS"?"COACH_STEP":null);
                string before=Newtonsoft.Json.JsonConvert.SerializeObject(fixture.Server.State);
                using(var composition=new Domino.UI.AppShell.ProductionRoutingComposition(f.auth,f.player,()=>new OnboardingApiSession(new DominoApiConfiguration(true,"https://example.test"),sdk,fixture.Server,()=>sdk.Session?.Uid,default),()=>"en")){
                    var router=new ProductionAuthRouter(f.auth,f.player,destination:composition);
                    await f.auth.LinkGuestEmailAsync("fixture@example.invalid",Secret(),Secret());await router.AcceptLinkedAccountAsync();sdk.Verify=true;await router.CheckVerificationAsync();
                    Need(router.Route==(state=="COMPLETED"?ProductionAuthRoute.AppShell:ProductionAuthRoute.Onboarding),"PRODUCTION_COMPOSITION_"+state);
                    Need(before==Newtonsoft.Json.JsonConvert.SerializeObject(fixture.Server.State),"AUTHORITATIVE_STATE_REVISION_STEP_PRESERVED_"+state);
                    Need(fixture.Server.Applied==0&&fixture.Server.TrialApplied==0,"NO_ONBOARDING_OR_TRIAL_WRITES_"+state);
                }
            }
        }
        Console.WriteLine("GUEST_ACCOUNT_01B_CHECKS="+checks+"_PASS\nREAL_OPERATIONS=0");
    }
}
