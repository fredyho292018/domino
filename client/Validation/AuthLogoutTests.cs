using System;
using System.Threading;
using System.Threading.Tasks;
using Domino.Identity;
using Domino.Player;
using Domino.Infrastructure.Api;
using Domino.Infrastructure.Firebase;
static class AuthLogoutTests {
 static int count;static void Check(bool pass,string key){count++;if(!pass)throw new Exception(key);}
 static string Secret(int length=6)=>new string('x',length);
 sealed class Sdk:IFirebaseClient,IFirebaseEmailSessionClient,IAuthTokenProvider,IFirebaseSessionControl {
  public FirebaseAuthSessionSnapshot Session;public int Creates,Sends,Reloads,Forced,Signs,Out;public bool VerifyOnReload,SwapOnReload,EmptyToken;public EmailAuthError CreateError,SendError;public TaskCompletionSource<bool> Hold,SendHold,ReloadHold;
  public Task<string> CheckDependenciesAsync()=>Task.FromResult("Available");public void InitializeApp(){}
  public FirebaseAuthSessionSnapshot GetSession()=>Session;
  public PlayerIdentity GetCurrentUser()=>Session==null?null:new PlayerIdentity(Session.Uid,Session.IsAnonymous);
  public Task<PlayerIdentity> SignInAnonymouslyAsync(){Signs++;Session=Snapshot(true);return Task.FromResult(GetCurrentUser());}
  public async Task<FirebaseAuthSessionSnapshot> CreateEmailAsync(string email,string password){Creates++;if(Hold!=null)await Hold.Task;if(CreateError!=EmailAuthError.None)throw new EmailAuthException(CreateError);return Session=new FirebaseAuthSessionSnapshot("EMAIL_ALIAS",false,false,true,email);}
  public async Task ReloadEmailAsync(string uid){Reloads++;if(ReloadHold!=null)await ReloadHold.Task;if(SwapOnReload)Session=new FirebaseAuthSessionSnapshot("OTHER_ALIAS",false,true,true,"");else if(VerifyOnReload)Session=Snapshot(false,true);}
  public async Task SendVerificationAsync(string uid){Sends++;if(SendHold!=null)await SendHold.Task;if(SendError!=EmailAuthError.None)throw new EmailAuthException(SendError);}
  public Task<string> GetIdTokenAsync(bool force,CancellationToken ct){if(force)Forced++;return Task.FromResult(EmptyToken?"":"synthetic");}
  public void SignOutUnverifiedEmail(string uid){if(Session.IsAnonymous||Session.IsEmailVerified||Session.Uid!=uid)throw new Exception("guard");Out++;Session=null;}
  public void SignOut(){Out++;Session=null;}
 }
 static FirebaseAuthSessionSnapshot Snapshot(bool guest=false,bool verified=false,bool password=true)=>new FirebaseAuthSessionSnapshot(guest?"GUEST_ALIAS":"EMAIL_ALIAS",guest,verified,password,"demo@example.invalid");
 sealed class Http:IApiTransport,IApiJsonCodec {
  public Sdk Sdk;public int Calls;public bool Fail,Mismatch;
  public Task<ApiHttpResponse> SendAsync(string method,Uri uri,string json,string token,int timeout,CancellationToken ct){Calls++;return Task.FromResult(new ApiHttpResponse(Fail?503:200,"safe"));}
  public string Serialize(PlayerBootstrapRequestDto r)=>"{}";public string SerializeDisplayName(string s)=>"{}";
  public ApiErrorDto ReadError(string s)=>new ApiErrorDto{code="DEPENDENCY_UNAVAILABLE"};
  public PlayerBootstrapResponseDto ReadSuccess(string s)=>new PlayerBootstrapResponseDto{player=new PlayerResponseDto{uid=Mismatch?"OTHER_ALIAS":Sdk.Session.Uid,accountType=Sdk.Session.IsAnonymous?"GUEST":"REGISTERED",displayName="Demo",language="en",status="ACTIVE"},wallet=new WalletResponseDto{coins=0}};
 }

 static async Task Main(){
  foreach(var guest in new[]{true,false}) {
   var sdk=new Sdk{Session=Snapshot(guest,true)};var auth=new FirebaseAuthService(new FirebaseBootstrap(sdk,null),sdk,null,default,true);await auth.RestoreAsync();
   int prepare=0,clear=0,welcome=0;var wait=new TaskCompletionSource<bool>();
   var service=new ProductionLogoutService(()=>auth.Current,async()=>{prepare++;await wait.Task;},()=>Task.CompletedTask,()=>clear++,auth.SignOut,()=>welcome++);
   service.Request();Check(service.GuestWarning==guest,"PROVIDER_CONFIRMATION");service.Cancel();Check(auth.Current!=null&&sdk.Out==0,"CANCEL_PRESERVES");
   service.Request();var first=service.ConfirmAsync();var second=service.ConfirmAsync();Check(ReferenceEquals(first,second)&&prepare==1,"SINGLE_FLIGHT");
   wait.SetResult(true);await first;Check(service.State==LogoutState.Success&&auth.Current==null&&sdk.Session==null&&clear==1&&welcome==1&&sdk.Out==1,"ORDERED_LOGOUT");
   service.Request();await service.ConfirmAsync();Check(sdk.Out==1,"NO_SESSION_NO_OP");
   var nextAuth=new FirebaseAuthService(new FirebaseBootstrap(sdk,null),sdk,null,default,true);var http=new Http{Sdk=sdk};var nextPlayer=new PlayerService(nextAuth,new DominoApiClient(new DominoApiConfiguration(true,"https://test.invalid"),sdk,http,http),()=>Task.FromResult("en"),default);var nextRouter=new ProductionAuthRouter(nextAuth,nextPlayer);await nextRouter.RestoreAsync();Check(nextRouter.Route==ProductionAuthRoute.Welcome&&nextRouter.SessionKind==AuthSessionKind.NoSession&&http.Calls==0,"EXISTING_ROUTER_WELCOME_NO_AUTO_GUEST");
  }
  {PlayerIdentity user=new PlayerIdentity("A_ALIAS",true);int writes=0;
   var service=new ProductionLogoutService(()=>user,()=>Task.FromException(new Exception("PRIVATE_ERROR")),()=>Task.CompletedTask,()=>writes++,()=>writes++,()=>writes++);
   service.Request();await service.ConfirmAsync();Check(writes==0&&user!=null&&service.State==LogoutState.Error,"PREPARATION_FAIL_CLOSED");Check(!service.Message.Contains("PRIVATE_ERROR"),"SAFE_ERROR");
   service.Request();user=new PlayerIdentity("B_ALIAS",false);await service.ConfirmAsync();Check(writes==0,"IDENTITY_CHANGE_REQUIRES_RECONFIRMATION");}
  {var sdk=new Sdk{Session=Snapshot(false,false)};var auth=new FirebaseAuthService(new FirebaseBootstrap(sdk,null),sdk,null,default,true);await auth.RestoreAsync();
   var http=new Http{Sdk=sdk};var player=new PlayerService(auth,new DominoApiClient(new DominoApiConfiguration(true,"https://test.invalid"),sdk,http,http),()=>Task.FromResult("en"),default);
   var router=new ProductionAuthRouter(auth,player);await router.RestoreAsync();Check(router.Route==ProductionAuthRoute.VerificationPending&&http.Calls==0,"UNVERIFIED_NO_BOOTSTRAP");
   var service=new ProductionLogoutService(()=>auth.Current,()=>Task.CompletedTask,router.StopAsync,player.Dispose,auth.SignOut,()=>{});service.Request();await service.ConfirmAsync();Check(player.Player==null&&auth.Current==null&&sdk.Out==1,"UNVERIFIED_LOGOUT");}
  {var sdk=new Sdk{Session=Snapshot(true)};var auth=new FirebaseAuthService(new FirebaseBootstrap(sdk,null),sdk,null,default,true);await auth.RestoreAsync();
   var http=new Http{Sdk=sdk};var language=new TaskCompletionSource<string>();var player=new PlayerService(auth,new DominoApiClient(new DominoApiConfiguration(true,"https://test.invalid"),sdk,http,http),()=>language.Task,default);
   var router=new ProductionAuthRouter(auth,player);var bootstrap=router.RestoreAsync();
   var service=new ProductionLogoutService(()=>auth.Current,()=>Task.CompletedTask,router.StopAsync,player.Dispose,auth.SignOut,()=>{});service.Request();await service.ConfirmAsync();language.SetResult("en");await bootstrap;
   Check(player.Player==null&&!player.HasConfirmedSnapshots&&player.Entitlements==null,"LATE_BOOTSTRAP_CANNOT_RESTORE");Check(auth.Current==null,"IDENTITY_CLEARED");}
  {var sdk=new Sdk{Session=Snapshot(true)};var http=new Http{Sdk=sdk};using var session=new CancellationTokenSource();
   var old=new SessionApiTransport(http,session.Token);await old.SendAsync("GET",new Uri("https://test.invalid"),null,"synthetic",1,default);session.Cancel();
   bool blocked=false;try{await old.SendAsync("GET",new Uri("https://test.invalid"),null,"synthetic",1,default);}catch(OperationCanceledException){blocked=true;}
   Check(blocked&&http.Calls==1,"OLD_API_CANNOT_SERVE_NEXT_USER");}
  {var sdk=new Sdk{Session=Snapshot(true)};var auth=new FirebaseAuthService(new FirebaseBootstrap(sdk,null),sdk,null,default,true);var http=new Http{Sdk=sdk};
   var player=new PlayerService(auth,new DominoApiClient(new DominoApiConfiguration(true,"https://test.invalid"),sdk,http,http),()=>Task.FromResult("en"),default);
   var router=new ProductionAuthRouter(auth,player);await router.RestoreAsync();Check(player.HasConfirmedSnapshots,"ACCOUNT_A_READY");
   var service=new ProductionLogoutService(()=>auth.Current,()=>Task.CompletedTask,router.StopAsync,player.Dispose,auth.SignOut,()=>{});service.Request();await service.ConfirmAsync();
   sdk.Session=new FirebaseAuthSessionSnapshot("B_ALIAS",false,true,true,"demo@example.invalid");var nextAuth=new FirebaseAuthService(new FirebaseBootstrap(sdk,null),sdk,null,default,true);
   var nextPlayer=new PlayerService(nextAuth,new DominoApiClient(new DominoApiConfiguration(true,"https://test.invalid"),sdk,http,http),()=>Task.FromResult("en"),default);var nextRouter=new ProductionAuthRouter(nextAuth,nextPlayer);await nextRouter.RestoreAsync();
   Check(nextRouter.Route==ProductionAuthRoute.AppShell&&nextPlayer.Player.Uid=="B_ALIAS"&&player.Player==null&&player.Wallet==null&&player.Entitlements==null,"ACCOUNT_SWITCH_PRIVATE_STATE_CLEARED");}
  Console.WriteLine("AUTH_LOGOUT_CHECKS="+count+"_PASS");
 }
}
