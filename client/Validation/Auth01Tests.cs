using System;
using System.Threading;
using System.Threading.Tasks;
using Domino.Identity;
using Domino.Player;
using Domino.Infrastructure.Api;
using Domino.Infrastructure.Firebase;

static class Auth01Tests
{
    static int checks;
    static void Check(bool b,string name){checks++;if(!b)throw new Exception(name);}
    sealed class Sdk : IFirebaseClient, IFirebaseSessionControl, IAuthTokenProvider {
        public PlayerIdentity User;public int Signs,Tokens,Out;public bool Fail;public TaskCompletionSource<bool> Hold;
        public Task<string> CheckDependenciesAsync()=>Task.FromResult("Available");public void InitializeApp(){}
        public PlayerIdentity GetCurrentUser()=>User;
        public async Task<PlayerIdentity> SignInAnonymouslyAsync(){Signs++;if(Hold!=null)await Hold.Task;if(Fail)throw new Exception("private failure");return User=new PlayerIdentity("GUEST_A",true);}
        public Task<string> GetIdTokenAsync(bool refresh,CancellationToken token){Tokens++;return Task.FromResult("synthetic");}
        public void SignOut(){Out++;User=null;}
    }
    sealed class Http : IApiTransport, IApiJsonCodec {
        public Sdk Sdk;public int Calls,Active,Peak;public bool Fail,Mismatch;public TaskCompletionSource<bool> Hold;
        public async Task<ApiHttpResponse> SendAsync(string method,Uri url,string json,string token,int timeout,CancellationToken ct){Calls++;Active++;Peak=Math.Max(Peak,Active);try{if(Hold!=null)await Hold.Task;ct.ThrowIfCancellationRequested();return new ApiHttpResponse(Fail?503:200,"safe");}finally{Active--;}}
        public string Serialize(PlayerBootstrapRequestDto r)=>"{}";public string SerializeDisplayName(string s)=>"{}";
        public ApiErrorDto ReadError(string s)=>new ApiErrorDto{code="DEPENDENCY_UNAVAILABLE"};
        public PlayerBootstrapResponseDto ReadSuccess(string s)=>new PlayerBootstrapResponseDto {player=new PlayerResponseDto{uid=Mismatch?"OTHER_ALIAS":Sdk.User.Uid,accountType=Sdk.User.IsAnonymous?"GUEST":"REGISTERED",displayName="Demo",language="en",status="ACTIVE"},wallet=new WalletResponseDto{coins=0}};
    }
    static (FirebaseAuthService auth,PlayerService player,ProductionAuthRouter router,Http http) Build(Sdk sdk){
        var auth=new FirebaseAuthService(new FirebaseBootstrap(sdk,null),sdk,null,default,true);
        var http=new Http{Sdk=sdk};var api=new DominoApiClient(new DominoApiConfiguration(true,"https://test.invalid"),sdk,http,http);
        var player=new PlayerService(auth,api,()=>Task.FromResult("en"),default);return (auth,player,new ProductionAuthRouter(auth,player),http);
    }
    static async Task Main(){
        var sdk=new Sdk();var f=Build(sdk);await f.router.RestoreAsync();Check(f.router.Route==ProductionAuthRoute.Welcome&&sdk.Signs==0&&f.http.Calls==0,"No implicit Guest");
        sdk.Hold=new TaskCompletionSource<bool>();var first=f.router.ContinueAsGuestAsync();var second=f.router.ContinueAsGuestAsync();Check(ReferenceEquals(first,second)&&sdk.Signs==1&&f.router.Busy,"double submit");sdk.Hold.SetResult(true);await first;
        Check(f.router.Route==ProductionAuthRoute.AppShell&&sdk.Tokens==1&&f.http.Calls==1&&f.http.Peak==1,"real client pipeline");Check(f.player.Player.Uid==sdk.User.Uid,"correlation");
        var before=sdk.User.Uid;f.router.Dispose();f.player.Dispose();var restart=Build(sdk);await restart.router.RestoreAsync();Check(sdk.Signs==1&&sdk.User.Uid==before&&restart.player.Player.Uid==before&&restart.router.Route==ProductionAuthRoute.AppShell,"restart continuity");
        var broken=Build(new Sdk());broken.http.Fail=true;await broken.router.ContinueAsGuestAsync();Check(broken.router.Route==ProductionAuthRoute.Error&&!broken.router.Busy&&broken.http.Sdk.Signs==1,"partial failure");broken.http.Fail=false;await broken.router.ContinueAsGuestAsync();Check(broken.router.Route==ProductionAuthRoute.AppShell&&broken.http.Sdk.Signs==1&&broken.http.Calls==2,"retry same Guest");
        var failed=Build(new Sdk{Fail=true});await failed.router.ContinueAsGuestAsync();Check(failed.router.Route==ProductionAuthRoute.Error&&!failed.router.Busy&&!failed.router.Message.Contains("private"),"sanitized auth failure");failed.http.Sdk.Fail=false;await failed.router.ContinueAsGuestAsync();Check(failed.router.Route==ProductionAuthRoute.AppShell,"auth retry");
        var mismatch=Build(new Sdk());mismatch.http.Mismatch=true;await mismatch.router.ContinueAsGuestAsync();Check(mismatch.router.Route==ProductionAuthRoute.Error,"UID mismatch denied");
        mismatch.http.Mismatch=false;await mismatch.router.ContinueAsGuestAsync();Check(mismatch.router.Route==ProductionAuthRoute.AppShell&&mismatch.http.Sdk.Signs==1,"explicit retry revalidates contract");
        var registered=Build(new Sdk{User=new PlayerIdentity("REGISTERED_ALIAS",false)});await registered.router.RestoreAsync();Check(registered.http.Sdk.Signs==0&&registered.router.Route==ProductionAuthRoute.AppShell,"registered restore preserved");
        await restart.router.StopAsync();restart.player.Dispose();restart.auth.SignOut();var loggedOut=Build(sdk);await loggedOut.router.RestoreAsync();Check(sdk.Out==1&&sdk.User==null&&loggedOut.player.Player==null&&loggedOut.router.Route==ProductionAuthRoute.Welcome,"logout no stale Player");
        var late=Build(new Sdk());late.http.Hold=new TaskCompletionSource<bool>();var run=late.router.ContinueAsGuestAsync();late.router.Dispose();late.http.Hold.SetResult(true);await run;Check(late.router.Route!=ProductionAuthRoute.AppShell,"late response cannot mount");
        Console.WriteLine("AUTH01_CHECKS="+checks+"_PASS REAL_NETWORK_CALLS=0");
    }
}
