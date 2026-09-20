using System;
using System.Threading;
using System.Threading.Tasks;
using Domino.Identity;
using Domino.Infrastructure;
using Domino.Infrastructure.Api;
using Domino.Online;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Domino.Social
{
    public sealed class SocialException : Exception { public string Code { get; } public SocialException(string code):base(code){Code=code;} }
    // Separate resource boundary: social endpoints cannot send match or economy commands.
    public sealed class SocialApi : IOnlineMatchApi
    {
        readonly DominoApiConfiguration config; readonly IAuthTokenProvider tokens; readonly IApiTransport transport;
        public SocialApi(DominoApiConfiguration config,IAuthTokenProvider tokens,IApiTransport transport){this.config=config;this.tokens=tokens;this.transport=transport;}
        public async Task<JObject> SendAsync(string method,string path,JObject body,CancellationToken cancellation)
        {
            if(!config.IsAvailable)throw new SocialException("SOCIAL_SERVICE_UNAVAILABLE");
            string resource=path.Split('?')[0];
            bool own=resource=="player/social-summary"||resource=="player/social-settings"||resource=="player/blocks";
            bool profile=System.Text.RegularExpressions.Regex.IsMatch(resource,@"^players/[A-Za-z0-9_-]{22}/profile$");
            bool block=System.Text.RegularExpressions.Regex.IsMatch(resource,@"^players/[A-Za-z0-9_-]{22}/block$");
            if(!(method=="GET"&&(own||profile||resource=="players/search")||method=="PATCH"&&resource=="player/social-settings"||(method=="POST"||method=="DELETE")&&block)||path.Contains("..")||path.Contains(":")||path.Contains("\\"))throw new ArgumentException("Invalid social path");
            for(int attempt=0;attempt<2;attempt++) {
                using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellation);timeout.CancelAfter(TimeSpan.FromSeconds(config.TimeoutSeconds));
                var bearer=await CancellableTask.Wait(tokens.GetIdTokenAsync(attempt==1,timeout.Token),timeout.Token);
                if(string.IsNullOrWhiteSpace(bearer)||bearer.IndexOfAny(new[]{'\r','\n'})>=0)throw new SocialException("SOCIAL_SERVICE_UNAVAILABLE");
                var response=await CancellableTask.Wait(transport.SendAsync(method,new Uri(config.Endpoint,"../"+path),body?.ToString(Formatting.None),bearer,config.TimeoutSeconds,timeout.Token),timeout.Token);
                if(response.Status==401&&attempt==0)continue;
                if(response.Status!=200) {
                    string code=null;try{code=(string)JObject.Parse(response.Body)["code"];}catch(JsonException){}
                    switch(code){case "PLAYER_NOT_FOUND":case "INVALID_FRIEND_CODE":case "INVALID_SEARCH_QUERY":case "SOCIAL_ACTION_RATE_LIMITED":case "REVISION_MISMATCH":throw new SocialException(code);}
                    throw new SocialException("SOCIAL_SERVICE_UNAVAILABLE");
                }
                return JObject.Parse(response.Body);
            }
            throw new SocialException("SOCIAL_SERVICE_UNAVAILABLE");
        }
    }
    // No persistent or cross-account cache. In-flight results are discarded after identity changes.
    public sealed class SocialClient
    {
        readonly IOnlineMatchApi api; readonly Func<string> currentUid; readonly string owner;
        public SocialClient(IOnlineMatchApi api,Func<string> currentUid){this.api=api;this.currentUid=currentUid;owner=currentUid();}
        public bool SessionValid=>!string.IsNullOrEmpty(owner)&&owner==currentUid();
        async Task<JObject> Send(string method,string path,JObject body,CancellationToken token) {
            if(!SessionValid||api==null)throw new OperationCanceledException();
            var result=await api.SendAsync(method,path,body,token);token.ThrowIfCancellationRequested();
            if(!SessionValid)throw new OperationCanceledException();return result;
        }
        public Task<JObject> Summary(CancellationToken t)=>Send("GET","player/social-summary",null,t);
        public Task<JObject> Search(string input,string cursor,CancellationToken t) {
            string value=(input??"").Trim();bool code=value.StartsWith("FHO-",StringComparison.OrdinalIgnoreCase);
            if(!code&&!System.Text.RegularExpressions.Regex.IsMatch(value,@"^[A-Za-z0-9_-]{3,16}$"))throw new SocialException("INVALID_SEARCH_QUERY");
            string query=code?"mode=FRIEND_CODE&friendCode="+Uri.EscapeDataString(value):"mode=NAME&q="+Uri.EscapeDataString(value);
            return Send("GET","players/search?"+query+"&limit=20"+(cursor==null?"":"&cursor="+Uri.EscapeDataString(cursor)),null,t);
        }
        public Task<JObject> Profile(string id,CancellationToken t)=>Send("GET","players/"+Id(id)+"/profile",null,t);
        public Task<JObject> Blocks(string cursor,CancellationToken t)=>Send("GET","player/blocks?limit=20"+(cursor==null?"":"&cursor="+Uri.EscapeDataString(cursor)),null,t);
        public Task<JObject> Block(string id,bool enabled,CancellationToken t)=>Send(enabled?"POST":"DELETE","players/"+Id(id)+"/block",null,t);
        public Task<JObject> Privacy(bool enabled,long revision,CancellationToken t)=>Send("PATCH","player/social-settings",new JObject{["discoverableByName"]=enabled,["revision"]=revision},t);
        static string Id(string id){if(id==null||!System.Text.RegularExpressions.Regex.IsMatch(id,@"^[A-Za-z0-9_-]{22}$"))throw new SocialException("PLAYER_NOT_FOUND");return id;}
    }
}
