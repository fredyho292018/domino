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
            bool own=resource=="player/social-summary"||resource=="player/social-settings"||resource=="player/blocks"||resource=="player/followers"||resource=="player/following"||resource=="player/friends"||resource=="player/friend-requests";
            bool profile=System.Text.RegularExpressions.Regex.IsMatch(resource,@"^players/[A-Za-z0-9_-]{22}/profile$");
            bool block=System.Text.RegularExpressions.Regex.IsMatch(resource,@"^players/[A-Za-z0-9_-]{22}/(block|follow)$");
            bool friendship=method=="POST"&&System.Text.RegularExpressions.Regex.IsMatch(resource,@"^players/[A-Za-z0-9_-]{22}/friend-request$") ||
                method=="POST"&&System.Text.RegularExpressions.Regex.IsMatch(resource,@"^friend-requests/[a-f0-9-]{36}/(accept|decline)$") ||
                method=="DELETE"&&System.Text.RegularExpressions.Regex.IsMatch(resource,@"^friend-requests/[a-f0-9-]{36}$") ||
                method=="DELETE"&&System.Text.RegularExpressions.Regex.IsMatch(resource,@"^player/friends/[A-Za-z0-9_-]{22}$");
            if(!(friendship||method=="GET"&&(own||profile||resource=="players/search")||method=="PATCH"&&resource=="player/social-settings"||(method=="POST"||method=="DELETE")&&block)||path.Contains("..")||path.Contains(":")||path.Contains("\\"))throw new ArgumentException("Invalid social path");
            for(int attempt=0;attempt<2;attempt++) {
                using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellation);timeout.CancelAfter(TimeSpan.FromSeconds(config.TimeoutSeconds));
                var bearer=await CancellableTask.Wait(tokens.GetIdTokenAsync(attempt==1,timeout.Token),timeout.Token);
                if(string.IsNullOrWhiteSpace(bearer)||bearer.IndexOfAny(new[]{'\r','\n'})>=0)throw new SocialException("SOCIAL_SERVICE_UNAVAILABLE");
                var response=await CancellableTask.Wait(transport.SendAsync(method,new Uri(config.Endpoint,"../"+path),body?.ToString(Formatting.None),bearer,config.TimeoutSeconds,timeout.Token),timeout.Token);
                if(response.Status==401&&attempt==0)continue;
                if(response.Status!=200) {
                    string code=null;try{code=(string)JObject.Parse(response.Body)["code"];}catch(JsonException){}
                    switch(code){case "SELF_RELATION_NOT_ALLOWED":case "FRIEND_LIMIT_REACHED":case "SOCIAL_ACTION_NOT_ALLOWED":case "ALREADY_FRIENDS":case "FRIEND_REQUEST_NOT_FOUND":case "FRIEND_REQUEST_NOT_PENDING":case "FRIEND_REQUEST_NOT_RECIPIENT":case "FRIEND_REQUEST_NOT_SENDER":case "PLAYER_NOT_FOUND":case "INVALID_FRIEND_CODE":case "INVALID_SEARCH_QUERY":case "SOCIAL_ACTION_RATE_LIMITED":case "REVISION_MISMATCH":throw new SocialException(code);}
                    throw new SocialException("SOCIAL_SERVICE_UNAVAILABLE");
                }
                return JObject.Parse(response.Body);
            }
            throw new SocialException("SOCIAL_SERVICE_UNAVAILABLE");
        }
    }
    // No persistent or cross-account cache. In-flight results are discarded after identity changes.
    public sealed class SocialClient : IDisposable
    {
        readonly IOnlineMatchApi api; readonly Func<string> currentUid; readonly string owner;
        readonly Domino.Realtime.IRealtimeConnectionService realtime;
        readonly Domino.Realtime.IRealtimePresenceChannel presenceChannel;
        Domino.Realtime.RealtimeConnectionState lastRealtime;
        bool disposed;
        CancellationTokenSource presencePending;
        public SocialPresenceStore Presence { get; } = new SocialPresenceStore();
        public SocialClient(IOnlineMatchApi api,Func<string> currentUid,Domino.Realtime.IRealtimeConnectionService realtime=null){
            this.api=api;this.currentUid=currentUid;owner=currentUid();this.realtime=realtime;
            presenceChannel=realtime as Domino.Realtime.IRealtimePresenceChannel;
            if(presenceChannel!=null){presenceChannel.PresenceMessage+=PresenceMessage;realtime.Changed+=RealtimeChanged;lastRealtime=realtime.State;}
        }
        void PresenceMessage(string type,JObject payload) {
            if(!SessionValid||disposed){Presence.Clear();return;}
            if(type=="SOCIAL_PRESENCE_ERROR") {if((long?)payload?["generation"]==Presence.Generation)Presence.Clear();}else Presence.Apply(type,payload);
        }
        void RealtimeChanged() {
            var state=realtime.State;
            if(state!=lastRealtime){lastRealtime=state;if(state==Domino.Realtime.RealtimeConnectionState.CONNECTED)SendPresence(Presence.Reconnect());else Presence.Clear();}
        }
        public void ObservePresence(System.Collections.Generic.IEnumerable<string> ids)=>SendPresence(Presence.Replace(ids));
        async void SendPresence(JObject request) {
            presencePending?.Cancel();presencePending?.Dispose();presencePending=new CancellationTokenSource();
            var pending=presencePending;
            if(presenceChannel==null||realtime.State!=Domino.Realtime.RealtimeConnectionState.CONNECTED||!SessionValid)return;
            try{if(((JArray)request["publicPlayerIds"]).Count>0)await Task.Delay(300,pending.Token);
                if(!pending.IsCancellationRequested)await presenceChannel.SubscribePresenceAsync(request);
            }catch(OperationCanceledException){}catch{Presence.Clear();}
        }
        public void Dispose(){if(disposed)return;ObservePresence(Array.Empty<string>());disposed=true;
            if(presenceChannel!=null){presenceChannel.PresenceMessage-=PresenceMessage;realtime.Changed-=RealtimeChanged;}Presence.Clear();}
        public bool SessionValid=>!string.IsNullOrEmpty(owner)&&owner==currentUid();
        async Task<JObject> Send(string method,string path,JObject body,CancellationToken token) {
            if(!SessionValid||api==null)throw new OperationCanceledException();
            var result=await api.SendAsync(method,path,body,token);token.ThrowIfCancellationRequested();
            if(!SessionValid)throw new OperationCanceledException();return result;
        }
        public Task<JObject> Summary(CancellationToken t)=>Send("GET","player/social-summary",null,t);
        public Task<JObject> Follows(bool following,string cursor,CancellationToken t)=>Send("GET","player/"+(following?"following":"followers")+"?limit=20"+Page(cursor),null,t);
        public Task<JObject> Follow(string id,bool enabled,CancellationToken t)=>Send(enabled?"POST":"DELETE","players/"+Id(id)+"/follow",null,t);
        public Task<JObject> Privacy(string field,string value,long revision,CancellationToken t) {
            bool contact=field=="friendRequests"||field=="follow",visibility=field=="presenceVisibility"||field=="matchActivityVisibility";
            if(!(contact||visibility)||!(value=="EVERYONE"||value=="NO_ONE"||visibility&&value=="FRIENDS"))throw new SocialException("INVALID_SEARCH_QUERY");
            return Send("PATCH","player/social-settings",new JObject{[field]=value,["revision"]=revision},t);
        }
        public Task<JObject> Friends(string cursor,CancellationToken t)=>Send("GET","player/friends?limit=20"+Page(cursor),null,t);
        public Task<JObject> Requests(bool incoming,string cursor,CancellationToken t)=>Send("GET","player/friend-requests?direction="+(incoming?"INCOMING":"OUTGOING")+"&limit=20"+Page(cursor),null,t);
        public Task<JObject> AddFriend(string id,CancellationToken t)=>Send("POST","players/"+Id(id)+"/friend-request",null,t);
        public Task<JObject> RemoveFriend(string id,CancellationToken t)=>Send("DELETE","player/friends/"+Id(id),null,t);
        public Task<JObject> ResolveRequest(string id,string action,CancellationToken t) {
            if(id==null||!System.Text.RegularExpressions.Regex.IsMatch(id,@"^[a-f0-9-]{36}$")||!(action=="accept"||action=="decline"||action=="cancel"))throw new SocialException("FRIEND_REQUEST_NOT_FOUND");
            return Send(action=="cancel"?"DELETE":"POST","friend-requests/"+id+(action=="cancel"?"":"/"+action),null,t);
        }
        static string Page(string cursor)=>cursor==null?"":"&cursor="+Uri.EscapeDataString(cursor);
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
