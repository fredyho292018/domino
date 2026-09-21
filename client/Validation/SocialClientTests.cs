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
        await client.AddFriend(id,token);Check(transport.Method=="POST"&&transport.Path.EndsWith("/friend-request")&&transport.Json==null,"Send uses public ID only");
        string request="12345678-1234-1234-1234-123456789abc";
        foreach(string action in new[]{"accept","decline","cancel"}) {await client.ResolveRequest(request,action,token);Check(transport.Method==(action=="cancel"?"DELETE":"POST"),"Request method "+action);}
        await client.RemoveFriend(id,token);Check(transport.Method=="DELETE"&&transport.Path.Contains("/player/friends/"),"Remove friend");
        await client.Friends(null,token);Check(transport.Path.EndsWith("/player/friends"),"Friends page");
        await client.Requests(true,null,token);Check(transport.Path.EndsWith("/player/friend-requests"),"Request page");
        await Reject(()=>client.ResolveRequest("../invalid","accept",token),typeof(SocialException));
        await Reject(()=>client.ResolveRequest(request,"forged",token),typeof(SocialException));
        await client.Follow(id,true,token);Check(transport.Method=="POST"&&transport.Json==null&&transport.Path.EndsWith("/follow"),"Follow authenticated route");
        await client.Follow(id,false,token);Check(transport.Method=="DELETE","Unfollow route");
        foreach(bool following in new[]{true,false}) {await client.Follows(following,"opaque-cursor",token);Check(transport.Path=="/api/v1/player/"+(following?"following":"followers"),"Owner graph");}
        foreach(string field in new[]{"friendRequests","follow","presenceVisibility","matchActivityVisibility"}) {await client.Privacy(field,"NO_ONE",4,token);Check(transport.Json.Contains(field)&&transport.Json.Contains("revision")&&!transport.Json.Contains("uid"),"Typed privacy "+field);}
        await Reject(()=>client.Privacy("sourceUid","victim",4,token),typeof(SocialException));
        await Reject(()=>client.Privacy("follow","FRIENDS",4,token),typeof(SocialException));
        await Reject(()=>api.SendAsync("GET","players/"+id+"/followers",null,token),typeof(ArgumentException));
        transport.Status=503;transport.Body="{\"code\":\"SOCIAL_SERVICE_UNAVAILABLE\"}";
        foreach(Func<Task> operation in new Func<Task>[]{()=>client.Search("Alice",null,token),()=>client.Follow(id,true,token),()=>client.Privacy("follow","EVERYONE",4,token)}) {
            int requests=transport.Calls,auth=tokens.Calls;
            try {await operation();throw new Exception("503 must not report success");}
            catch(SocialException e){Check(e.Code=="SOCIAL_SERVICE_UNAVAILABLE","Typed unavailable");}
            Check(transport.Calls==requests+1&&tokens.Calls==auth+1,"503 has no retry or auth refresh");
            Check(client.SessionValid&&uid=="a","503 preserves identity/session");
        }
        transport.Status=404;transport.Body="{\"code\":\"PLAYER_NOT_FOUND\"}";await Reject(()=>client.Profile(id,token),typeof(SocialException));
        transport.Status=401;transport.Body="{}";int before=tokens.Calls;await Reject(()=>client.Summary(token),typeof(SocialException));Check(tokens.Calls-before==2,"One auth refresh");
        var d=new Deferred();var scoped=new SocialClient(d,()=>uid);var pending=scoped.Summary(token);uid="b";d.Result.SetResult(new JObject());await Reject(()=>pending,typeof(OperationCanceledException));Check(!scoped.SessionValid,"Session invalidated");
        var presence=new SocialPresenceStore();
        var requestPresence=presence.Replace(new[]{id,id});long generation=presence.Generation;
        Check(((JArray)requestPresence["publicPlayerIds"]).Count==1,"Presence deduplicates desired targets");
        Check(presence.Get(id)==SocialPresenceState.UNKNOWN,"Presence begins unknown");
        presence.Apply("SOCIAL_PRESENCE_INVALIDATED",new JObject{["generation"]=generation,["revision"]=1});
        foreach(var state in new[]{"ONLINE","OFFLINE","IN_MATCH","UNKNOWN"}) {
            presence.Apply("SOCIAL_PRESENCE_SNAPSHOT",new JObject{["generation"]=generation,["revision"]=1,["publicPlayerId"]=id,["state"]=state});
            Check(presence.Get(id).ToString()==state,"Typed presence "+state);
        }
        presence.Apply("SOCIAL_PRESENCE_UPDATED",new JObject{["generation"]=generation,["revision"]=1,["publicPlayerId"]=id,["state"]="IN_MATCH"});
        presence.Apply("SOCIAL_PRESENCE_INVALIDATED",new JObject{["generation"]=generation,["revision"]=2});
        Check(presence.Get(id)==SocialPresenceState.UNKNOWN,"Revocation clears IN_MATCH");
        presence.Apply("SOCIAL_PRESENCE_UPDATED",new JObject{["generation"]=generation,["revision"]=1,["publicPlayerId"]=id,["state"]="ONLINE"});
        Check(presence.Get(id)==SocialPresenceState.UNKNOWN,"Prior revision cannot restore state");
        presence.Replace(Array.Empty<string>());
        presence.Apply("SOCIAL_PRESENCE_SNAPSHOT",new JObject{["generation"]=generation,["revision"]=2,["publicPlayerId"]=id,["state"]="ONLINE"});
        Check(presence.Get(id)==SocialPresenceState.UNKNOWN,"Old subscription cannot restore state");
        Check(presence.Reconnect().Value<long>("generation")>generation,"Reconnect acquires new generation");
        Check(SocialPresenceStore.LocalizationKey(SocialPresenceState.UNKNOWN)!=SocialPresenceStore.LocalizationKey(SocialPresenceState.OFFLINE),"Unknown presentation differs from offline");
        Console.WriteLine("SOCIAL_CLIENT_TESTS="+checks+" PASS");
    }
}
