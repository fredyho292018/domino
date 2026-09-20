using System;
using System.Threading;
using System.Threading.Tasks;
using Domino.Identity;
using Domino.Infrastructure.Api;
using Domino.Social;
using Newtonsoft.Json.Linq;
namespace Domino.Online { public interface IOnlineMatchApi { Task<JObject> SendAsync(string m,string p,JObject b,CancellationToken t); } }
static class SocialClientTests
{
    static int checks;
    static void Check(bool value,string name){checks++;if(!value)throw new Exception(name);}
    sealed class Tokens:IAuthTokenProvider {public int Calls;public Task<string> GetIdTokenAsync(bool refresh,CancellationToken t){Calls++;return Task.FromResult("local-test-token");}}
    sealed class Transport:IApiTransport {
        public int Calls;public long Status=200;public string Body="{}";public string Path,Method,Json;
        public Task<ApiHttpResponse> SendAsync(string method,Uri url,string json,string token,int timeout,CancellationToken t){Calls++;Path=url.AbsolutePath;Method=method;Json=json;Check(token=="local-test-token","Token transport");return Task.FromResult(new ApiHttpResponse(Status,Body));}
    }
    sealed class Deferred:Domino.Online.IOnlineMatchApi {public TaskCompletionSource<JObject> Result=new TaskCompletionSource<JObject>();public Task<JObject> SendAsync(string m,string p,JObject b,CancellationToken t)=>Result.Task;}
    static async Task Reject(Func<Task> call,Type expected){try{await call();throw new Exception("Expected failure");}catch(Exception e){Check(expected.IsAssignableFrom(e.GetType()),e.GetType().Name);}}
    static async Task Main() {
        var tokens=new Tokens();var transport=new Transport();var api=new SocialApi(new DominoApiConfiguration(true,"https://example.invalid"),tokens,transport);
        string uid="a";var client=new SocialClient(api,()=>uid);var token=CancellationToken.None;
        await client.Summary(token);Check(transport.Path=="/api/v1/player/social-summary","Summary route");
        await client.Search("Alice",null,token);Check(transport.Path=="/api/v1/players/search","Name route");
        await client.Search(" fHo-000000000001 ",null,token);Check(transport.Calls==3,"Exact code request");
        foreach(var q in new[]{"a","ab","abc@","a/b","abcdefghijklmnopq"})await Reject(()=>client.Search(q,null,token),typeof(SocialException));
        foreach(var p in new[]{"matches","economy/ad-rewards","../player/profile","players/../../x","https://other"})await Reject(()=>api.SendAsync("POST",p,null,token),typeof(ArgumentException));
        await client.Privacy(false,3,token);Check(transport.Method=="PATCH"&&transport.Json.Contains("revision")&&!transport.Json.Contains("uid"),"Privacy fields");
        string id=new string('a',22);await client.Block(id,true,token);Check(transport.Method=="POST","Block method");await client.Block(id,false,token);Check(transport.Method=="DELETE","Unblock method");
        await Reject(()=>client.Profile("bad",token),typeof(SocialException));
        transport.Status=404;transport.Body="{\"code\":\"PLAYER_NOT_FOUND\"}";await Reject(()=>client.Profile(id,token),typeof(SocialException));
        transport.Status=401;transport.Body="{}";int before=tokens.Calls;await Reject(()=>client.Summary(token),typeof(SocialException));Check(tokens.Calls-before==2,"One auth refresh");
        var d=new Deferred();var scoped=new SocialClient(d,()=>uid);var pending=scoped.Summary(token);uid="b";d.Result.SetResult(new JObject());await Reject(()=>pending,typeof(OperationCanceledException));Check(!scoped.SessionValid,"Session invalidated");
        Console.WriteLine("S1_1_CLIENT_TESTS="+checks+" PASS");
    }
}
