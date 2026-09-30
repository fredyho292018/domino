using System;
using System.Threading;
using System.Threading.Tasks;
using Domino.Identity;
using Domino.Player;
using Domino.Infrastructure.Api;
using Domino.Infrastructure.Firebase;
static class Auth02ATests {
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
 static (FirebaseAuthService auth,ProductionAuthRouter router,Http http) Build(Sdk sdk){var auth=new FirebaseAuthService(new FirebaseBootstrap(sdk,null),sdk,null,default,true);var h=new Http{Sdk=sdk};var api=new DominoApiClient(new DominoApiConfiguration(true,"https://test.invalid"),sdk,h,h);var player=new PlayerService(auth,api,()=>Task.FromResult("en"),default);return(auth,new ProductionAuthRouter(auth,player),h);}
 static async Task Main(){
  Check(EmailAuthRules.Normalize("  Demo@example.invalid  ")=="Demo@example.invalid","trim only");
  foreach(var address in new[]{"","bad","a b@example.invalid"})Check(EmailAuthRules.Validate(address,Secret(),Secret())==EmailAuthError.InvalidEmail,"email rejected");
  Check(EmailAuthRules.Validate("demo@example.invalid",Secret(5),Secret(5))==EmailAuthError.WeakPassword,"min");
  Check(EmailAuthRules.Validate("demo@example.invalid",Secret(4097),Secret(4097))==EmailAuthError.WeakPassword,"max");
  Check(EmailAuthRules.Validate("demo@example.invalid",Secret(4096),Secret(4096))==EmailAuthError.None,"max accepted");
  Check(EmailAuthRules.Validate("demo@example.invalid",Secret(),Secret(7))==EmailAuthError.PasswordMismatch,"confirm");
  Check(EmailAuthRules.Validate("demo@example.invalid",Secret(),Secret())==EmailAuthError.None,"no complexity");
  foreach(var code in new[]{11,23,8,13,19,999})Check(EmailAuthRules.Message(EmailAuthRules.FirebaseCode(code)).Length>0,"typed safe error");
  var sdk=new Sdk();var f=Build(sdk);await f.router.RestoreAsync();Check(f.router.Route==ProductionAuthRoute.Welcome&&sdk.Signs==0&&f.http.Calls==0,"no implicit guest");
  f.router.NavigateEmail(ProductionAuthRoute.EmailEntry);Check(f.router.Route==ProductionAuthRoute.EmailEntry&&sdk.Creates==0,"entry no authentication");
  f.router.NavigateEmail(ProductionAuthRoute.Register);await f.router.RegisterAsync("bad",Secret(),Secret());Check(sdk.Creates==0&&f.router.EmailError==EmailAuthError.InvalidEmail,"validation before sdk");
  sdk.Hold=new TaskCompletionSource<bool>();var a=f.router.RegisterAsync(" demo@example.invalid ",Secret(),Secret());var b=f.router.RegisterAsync("demo@example.invalid",Secret(),Secret());Check(ReferenceEquals(a,b)&&sdk.Creates==1&&f.router.Busy,"register single flight");sdk.Hold.SetResult(true);await a;
  Check(sdk.Session.DisplayEmail=="demo@example.invalid"&&sdk.Sends==1,"normalized registration send");
  Check(f.router.Route==ProductionAuthRoute.VerificationPending&&f.router.SessionKind==AuthSessionKind.EmailUnverified&&f.http.Calls==0,"unverified no bootstrap");
  await f.router.CheckVerificationAsync();Check(f.http.Calls==0&&f.router.Route==ProductionAuthRoute.VerificationPending&&sdk.Forced==0,"not yet verified");
  sdk.SendError=EmailAuthError.TooManyRequests;await f.router.ResendVerificationAsync();Check(f.router.EmailError==EmailAuthError.TooManyRequests&&sdk.Creates==1&&f.http.Calls==0,"resend rate limit");
  sdk.SendError=EmailAuthError.None;await f.router.ResendVerificationAsync();Check(sdk.Creates==1&&sdk.Sends==3,"resend same account");
  sdk.SendHold=new TaskCompletionSource<bool>();var resend=f.router.ResendVerificationAsync();var resendAgain=f.router.ResendVerificationAsync();var checkDuringSend=f.router.CheckVerificationAsync();Check(ReferenceEquals(resend,resendAgain)&&ReferenceEquals(resend,checkDuringSend)&&sdk.Sends==4,"resend single flight");sdk.SendHold.SetResult(true);await resend;
  sdk.ReloadHold=new TaskCompletionSource<bool>();var reloads=sdk.Reloads;var verify=f.router.CheckVerificationAsync();var verifyAgain=f.router.CheckVerificationAsync();Check(ReferenceEquals(verify,verifyAgain)&&sdk.Reloads==reloads+1&&f.router.Busy,"verify single flight");sdk.ReloadHold.SetResult(true);await verify;
  sdk.VerifyOnReload=true;await f.router.CheckVerificationAsync();Check(sdk.Forced==1&&f.http.Calls==1&&f.router.Route==ProductionAuthRoute.AppShell,"reload refresh bootstrap");
  await f.router.CancelUnverifiedAsync(true);Check(sdk.Out==0&&f.router.Route==ProductionAuthRoute.AppShell,"cannot cancel verified");
  var guest=new Sdk{Session=Snapshot(true)};var g=Build(guest);await g.router.RegisterAsync("demo@example.invalid",Secret(),Secret());Check(guest.Creates==0&&guest.Out==0&&g.router.EmailError==EmailAuthError.GuestUpgradeRequired,"actual anonymous guard before restore");
  await g.router.RestoreAsync();Check(g.router.Route==ProductionAuthRoute.AppShell&&guest.Signs==0&&g.http.Calls==1,"guest continuity");
  var restored=Build(new Sdk{Session=Snapshot()});await restored.router.RestoreAsync();Check(restored.router.Route==ProductionAuthRoute.VerificationPending&&restored.http.Calls==0,"unverified restore");
  await restored.router.CancelUnverifiedAsync(true);Check(restored.http.Sdk.Out==0&&restored.router.Route==ProductionAuthRoute.VerificationPending,"restored session not owned");
  var verified=Build(new Sdk{Session=Snapshot(false,true)});await verified.router.RestoreAsync();Check(verified.router.Route==ProductionAuthRoute.AppShell&&verified.http.Sdk.Reloads==1&&verified.http.Sdk.Forced==1,"verified restore");
  var other=Build(new Sdk{Session=Snapshot(false,false,false)});await other.router.RestoreAsync();Check(other.router.Route==ProductionAuthRoute.AppShell&&other.http.Sdk.Reloads==0,"provider metadata not email string");
  var partial=Build(new Sdk{SendError=EmailAuthError.NetworkError});partial.router.NavigateEmail(ProductionAuthRoute.Register);await partial.router.RegisterAsync("demo@example.invalid",Secret(),Secret());Check(partial.router.Route==ProductionAuthRoute.VerificationPending&&partial.http.Sdk.Creates==1&&partial.http.Calls==0,"created but email failed");
  partial.http.Sdk.SendError=EmailAuthError.None;await partial.router.ResendVerificationAsync();Check(partial.http.Sdk.Creates==1&&partial.router.VerificationSent,"partial recovery");
  await partial.router.CancelUnverifiedAsync(false);Check(partial.http.Sdk.Out==0,"cancel confirmation required");await partial.router.CancelUnverifiedAsync(true);Check(partial.http.Sdk.Out==1&&partial.auth.Current==null&&partial.router.Route==ProductionAuthRoute.EmailEntry,"owned unverified cancel");
  foreach(var error in new[]{EmailAuthError.EmailAlreadyInUse,EmailAuthError.InvalidEmail,EmailAuthError.WeakPassword,EmailAuthError.TooManyRequests,EmailAuthError.NetworkError,EmailAuthError.Unknown}){var failed=Build(new Sdk{CreateError=error});failed.router.NavigateEmail(ProductionAuthRoute.Register);await failed.router.RegisterAsync("demo@example.invalid",Secret(),Secret());Check(failed.router.EmailError==error&&failed.http.Calls==0&&!failed.router.Busy&&failed.router.Route==ProductionAuthRoute.Register,"safe registration failure");}
  var swap=Build(new Sdk{Session=Snapshot(),SwapOnReload=true});await swap.router.RestoreAsync();Check(swap.http.Calls==0&&swap.router.Route!=ProductionAuthRoute.AppShell,"identity swap denied");
  var empty=Build(new Sdk{Session=Snapshot(false,true),EmptyToken=true});await empty.router.RestoreAsync();Check(empty.http.Calls==0&&empty.router.Route!=ProductionAuthRoute.AppShell,"empty token denied");
  var retry=Build(new Sdk{Session=Snapshot(false,true)});retry.http.Fail=true;await retry.router.RestoreAsync();Check(retry.router.Route!=ProductionAuthRoute.AppShell,"bootstrap failure denied");retry.http.Fail=false;await retry.router.CheckVerificationAsync();Check(retry.router.Route==ProductionAuthRoute.AppShell&&retry.http.Calls==2,"bootstrap retry same identity");
  var mismatch=Build(new Sdk{Session=Snapshot(false,true)});mismatch.http.Mismatch=true;await mismatch.router.RestoreAsync();Check(mismatch.router.Route!=ProductionAuthRoute.AppShell,"backend correlation denied");
  var late=Build(new Sdk{Hold=new TaskCompletionSource<bool>()});var run=late.router.RegisterAsync("demo@example.invalid",Secret(),Secret());late.router.Dispose();late.http.Sdk.Hold.SetResult(true);await run;Check(late.http.Calls==0&&late.http.Sdk.Sends==0&&late.router.Route!=ProductionAuthRoute.AppShell,"disposed completion cannot route");
  var navigation=Build(new Sdk());await navigation.router.RestoreAsync();
  navigation.router.NavigateEmail(ProductionAuthRoute.EmailPlaceholder);Check(navigation.router.Message=="Coming Soon","placeholder feedback retained");
  navigation.router.NavigateEmail(ProductionAuthRoute.EmailEntry);navigation.router.NavigateEmail(ProductionAuthRoute.Welcome);
  Check(navigation.router.Route==ProductionAuthRoute.Welcome&&navigation.router.Message=="","return Welcome clears stale placeholder");
  navigation.router.NavigateEmail(ProductionAuthRoute.Register);navigation.router.NavigateEmail(ProductionAuthRoute.EmailEntry);
  Check(navigation.router.Route==ProductionAuthRoute.EmailEntry&&navigation.router.Message==""&&navigation.http.Calls==0&&navigation.http.Sdk.Creates==0,"register back remains navigation only");
  Console.WriteLine("AUTH02A_CHECKS_RUN="+count+" AUTH02A_CHECKS_PASS="+count+" AUTH02A_CHECKS_FAIL=0 AUTH02A_CHECKS_SKIPPED=0 REAL_NETWORK_CALLS=0");
 }
}
