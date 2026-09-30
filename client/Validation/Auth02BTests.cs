using System;
using System.Threading;
using System.Threading.Tasks;
using Domino.Identity;
using Domino.Player;
using Domino.Infrastructure.Api;
using Domino.Infrastructure.Firebase;
static class Auth02BTests {
 static int count;static void Check(bool pass,string key){count++;if(!pass)throw new Exception(key);}
 static string Secret(int length=6)=>new string('x',length);
 sealed class Sdk:IFirebaseClient,IFirebaseEmailSessionClient,IAuthTokenProvider,IFirebaseSessionControl,IFirebaseEmailAccessClient {
  public int Logins,Resets;public string Address;public bool Verified,WrongIdentity;public EmailAuthError LoginError,ResetError;public TaskCompletionSource<bool> AccessHold;
  public async Task<FirebaseAuthSessionSnapshot> SignInEmailAsync(string email,string password){Logins++;Address=email;if(AccessHold!=null)await AccessHold.Task;if(LoginError!=EmailAuthError.None)throw new EmailAuthException(LoginError);Session=new FirebaseAuthSessionSnapshot("EMAIL_ALIAS",false,Verified,true,email);return WrongIdentity?new FirebaseAuthSessionSnapshot("WRONG_ALIAS",false,Verified,true,email):Session;}
  public async Task SendPasswordResetAsync(string email){Resets++;Address=email;if(AccessHold!=null)await AccessHold.Task;if(ResetError!=EmailAuthError.None)throw new EmailAuthException(ResetError);}
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
  Check(EmailAuthRules.Normalize("  Mixed@Example.invalid ")=="Mixed@Example.invalid","trim only");
  foreach(var address in new[]{"","bad","a b@example.invalid"}){
   var f=Build(new Sdk());f.router.NavigateEmail(ProductionAuthRoute.EmailSignIn);await f.router.SignInEmailAsync(address,Secret());Check(f.http.Sdk.Logins==0&&f.router.EmailError==EmailAuthError.InvalidEmail,"invalid login email");
   f.router.NavigateEmail(ProductionAuthRoute.ForgotPassword);await f.router.ResetPasswordAsync(address);Check(f.http.Sdk.Resets==0&&f.router.EmailError==EmailAuthError.InvalidEmail,"invalid reset email");
  }
  foreach(var secret in new[]{"x"," ",new string('x',5000)})Check(EmailAuthRules.ValidateSignIn("demo@example.invalid",secret)==EmailAuthError.None,"no registration complexity on login");
  Check(EmailAuthRules.ValidateSignIn("demo@example.invalid","")==EmailAuthError.PasswordRequired,"required password");
  var sdk=new Sdk{Verified=true,AccessHold=new TaskCompletionSource<bool>()};var f1=Build(sdk);await f1.router.RestoreAsync();f1.router.NavigateEmail(ProductionAuthRoute.EmailSignIn);
  var first=f1.router.SignInEmailAsync(" Mixed@Example.invalid ","x");var second=f1.router.SignInEmailAsync("other@example.invalid","x");
  Check(ReferenceEquals(first,second)&&sdk.Logins==1&&f1.router.Busy&&f1.router.EmailState==EmailOperationState.SigningIn,"single flight login");
  f1.router.NavigateEmail(ProductionAuthRoute.Register);Check(f1.router.Route==ProductionAuthRoute.EmailSignIn,"navigation blocked busy");
  sdk.AccessHold.SetResult(true);await first;
  Check(sdk.Address=="Mixed@Example.invalid","normalized adapter input");
  Check(f1.router.Route==ProductionAuthRoute.AppShell&&sdk.Forced==1&&f1.http.Calls==1&&f1.auth.Current.Uid=="EMAIL_ALIAS","verified refresh existing bootstrap");
  Check(sdk.Creates==0&&sdk.Signs==0&&sdk.Out==0,"no create link logout");
  var u=Build(new Sdk());u.router.NavigateEmail(ProductionAuthRoute.EmailSignIn);await u.router.SignInEmailAsync("demo@example.invalid","x");
  Check(u.router.Route==ProductionAuthRoute.VerificationPending&&u.http.Calls==0&&u.http.Sdk.Forced==0,"unverified bootstrap zero");
  await u.router.ResendVerificationAsync();Check(u.http.Sdk.Sends==1&&u.router.VerificationSent,"login verification resend reused");
  await u.router.CheckVerificationAsync();Check(u.router.Route==ProductionAuthRoute.VerificationPending&&u.http.Calls==0,"still unverified");
  u.http.Sdk.VerifyOnReload=true;await u.router.CheckVerificationAsync();Check(u.router.Route==ProductionAuthRoute.AppShell&&u.http.Sdk.Forced==1&&u.http.Calls==1,"login verification completes");
  foreach(var error in new[]{EmailAuthError.InvalidEmail,EmailAuthError.InvalidCredential,EmailAuthError.UserDisabled,EmailAuthError.TooManyRequests,EmailAuthError.NetworkError,EmailAuthError.Unknown}){
   var failed=Build(new Sdk{LoginError=error});failed.router.NavigateEmail(ProductionAuthRoute.EmailSignIn);await failed.router.SignInEmailAsync("demo@example.invalid","x");
   Check(failed.router.EmailError==error&&failed.router.Route==ProductionAuthRoute.EmailSignIn&&!failed.router.Busy&&failed.http.Calls==0,"safe typed login failure");
   Check(!failed.router.Message.Contains("demo@example.invalid")&&!failed.router.Message.Contains("EMAIL_ALIAS"),"safe UI message");
  }
  foreach(var guest in new[]{true,false}){
   var guard=Build(new Sdk{Session=Snapshot(guest)});await guard.router.SignInEmailAsync("demo@example.invalid","x");Check(guard.http.Sdk.Logins==0&&guard.http.Sdk.Out==0,"existing session never replaced");
   await guard.router.ResetPasswordAsync("demo@example.invalid");Check(guard.http.Sdk.Resets==0,"reset session guard");
  }
  var rsdk=new Sdk{AccessHold=new TaskCompletionSource<bool>()};var r=Build(rsdk);r.router.NavigateEmail(ProductionAuthRoute.ForgotPassword);
  var reset=r.router.ResetPasswordAsync(" Demo@example.invalid ");var again=r.router.ResetPasswordAsync("Demo@example.invalid");Check(ReferenceEquals(reset,again)&&rsdk.Resets==1&&r.router.EmailState==EmailOperationState.Resetting,"single flight reset");rsdk.AccessHold.SetResult(true);await reset;
  Check(r.router.Message==EmailAuthRules.ResetSuccess&&r.auth.Current==null&&r.http.Calls==0&&rsdk.Logins==0,"reset privacy no authentication");Check(rsdk.Address=="Demo@example.invalid","reset trim");
  r.router.NavigateEmail(ProductionAuthRoute.EmailSignIn);Check(r.router.Message==""&&r.router.Route==ProductionAuthRoute.EmailSignIn,"reset return clears status");
  foreach(var error in new[]{EmailAuthError.InvalidEmail,EmailAuthError.NetworkError,EmailAuthError.TooManyRequests,EmailAuthError.Unknown}){
   var e=Build(new Sdk{ResetError=error});e.router.NavigateEmail(ProductionAuthRoute.ForgotPassword);await e.router.ResetPasswordAsync("demo@example.invalid");Check(e.router.EmailError==error&&e.auth.Current==null&&e.http.Calls==0,"typed reset error");
  }
  var mismatch=Build(new Sdk{Verified=true,WrongIdentity=true});await mismatch.router.SignInEmailAsync("demo@example.invalid","x");Check(mismatch.http.Calls==0&&mismatch.router.EmailError==EmailAuthError.SessionConflict,"exchange identity mismatch rejected");
  var empty=Build(new Sdk{Verified=true,EmptyToken=true});await empty.router.SignInEmailAsync("demo@example.invalid","x");Check(empty.http.Calls==0&&empty.router.Route!=ProductionAuthRoute.AppShell,"missing token fails closed");
  var wrong=Build(new Sdk{Verified=true});wrong.http.Mismatch=true;await wrong.router.SignInEmailAsync("demo@example.invalid","x");Check(wrong.router.Route!=ProductionAuthRoute.AppShell,"backend identity mismatch fails closed");
  var late=Build(new Sdk{Verified=true,AccessHold=new TaskCompletionSource<bool>()});var pending=late.router.SignInEmailAsync("demo@example.invalid","x");late.router.Dispose();late.http.Sdk.AccessHold.SetResult(true);await pending;Check(late.http.Calls==0&&late.router.Route!=ProductionAuthRoute.AppShell,"disposed login completion cannot bootstrap");
  foreach(var verified in new[]{true,false}){var restore=Build(new Sdk{Session=Snapshot(false,verified)});await restore.router.RestoreAsync();Check(restore.router.Route==(verified?ProductionAuthRoute.AppShell:ProductionAuthRoute.VerificationPending),"email restore regression");}
  var flow=Build(new Sdk());foreach(var route in new[]{ProductionAuthRoute.EmailEntry,ProductionAuthRoute.Register,ProductionAuthRoute.EmailSignIn,ProductionAuthRoute.ForgotPassword,ProductionAuthRoute.Welcome}){flow.router.NavigateEmail(route);Check(flow.router.Route==route&&flow.http.Calls==0&&flow.http.Sdk.Logins==0&&flow.http.Sdk.Resets==0,"navigation only");}
  Console.WriteLine("AUTH02B_CHECKS_RUN="+count+" AUTH02B_CHECKS_PASS="+count+" AUTH02B_CHECKS_FAIL=0 AUTH02B_CHECKS_SKIPPED=0 REAL_NETWORK_CALLS=0");
 }
}
