using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Domino.Identity;
using Domino.Infrastructure;
using Domino.Infrastructure.Api;
using Domino.Realtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Domino.Online
{
    // Presentation snapshot only. There is deliberately no ClientGame instance in the online client.
    public sealed class OnlineMatchSnapshot
    {
        readonly JObject data;
        public string MatchId => (string)data["publicState"]["matchId"];
        public long Sequence => (long)data["lastSequence"];
        public int Seat => (int)data["privateState"]["seat"];
        public string Phase => (string)data["phase"];
        public JObject Public => (JObject)data["publicState"].DeepClone();
        public JArray Hand => (JArray)data["privateState"]["hand"].DeepClone();
        public JObject Starter => data["starter"] as JObject == null ? null : (JObject)data["starter"].DeepClone();
        public JObject Rules => (JObject)data["ruleSnapshot"].DeepClone();
        public JObject RoundResult => data["roundResult"] as JObject == null ? null : (JObject)data["roundResult"].DeepClone();
        public OnlineMatchSnapshot(JObject value)
        {
            data=(JObject)value.DeepClone();
            if (Sequence<0 || Seat<0 || Seat>1 || (long)data["publicState"]["lastSequence"]!=Sequence || (long)data["privateState"]["lastSequence"]!=Sequence || Hand.Count>10)
                throw new FormatException("Invalid online snapshot");
            if(data["publicState"]["hands"]!=null || data["privateState"]["hands"]!=null)throw new FormatException("Invalid private projection");
        }
    }
    public interface IOnlineMatchApi { Task<JObject> SendAsync(string method,string path,JObject body,CancellationToken cancellation); }
    public sealed class OnlineMatchApi : IOnlineMatchApi
    {
        readonly DominoApiConfiguration config; readonly IAuthTokenProvider tokens; readonly IApiTransport transport;
        public OnlineMatchApi(DominoApiConfiguration config,IAuthTokenProvider tokens,IApiTransport transport) {this.config=config;this.tokens=tokens;this.transport=transport;}
        public async Task<JObject> SendAsync(string method,string path,JObject body,CancellationToken cancellation)
        {
            if(!config.IsAvailable)throw new DominoApiException(ApiFailure.Configuration);
            if(!path.StartsWith("matches",StringComparison.Ordinal)||path.Contains("..")||path.Contains(":")||path.Contains("\\"))throw new ArgumentException("Invalid match path");
            for(int attempt=0;attempt<2;attempt++) {
                using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellation);timeout.CancelAfter(TimeSpan.FromSeconds(config.TimeoutSeconds));
                var token=await CancellableTask.Wait(tokens.GetIdTokenAsync(attempt==1,timeout.Token),timeout.Token);
                if(string.IsNullOrWhiteSpace(token)||token.IndexOfAny(new[]{'\r','\n'})>=0)throw new DominoApiException(ApiFailure.Authentication);
                var result=await CancellableTask.Wait(transport.SendAsync(method,new Uri(config.Endpoint,"../"+path),body?.ToString(Formatting.None),token,config.TimeoutSeconds,timeout.Token),timeout.Token);
                if(result.Status==401&&attempt==0)continue;
                if(result.Status!=200)throw new DominoApiException(ApiFailure.Server,result.Status);
                return JObject.Parse(result.Body);
            }
            throw new DominoApiException(ApiFailure.Authentication);
        }
    }
    public sealed class OnlineMatchClient : IDisposable
    {
        readonly IOnlineMatchApi api; readonly IRealtimeMatchChannel channel;
        readonly CancellationTokenSource lifetime=new CancellationTokenSource();
        public OnlineMatchSnapshot Snapshot {get;private set;}
        public bool NeedsResync {get;private set;}
        public bool Pending {get;private set;}
        public OnlineTurnClock TurnClock {get;}=new OnlineTurnClock();
        public event Action<string> EventApplied;
        string pendingId; Task resync;
        public event Action Changed;
        public event Action<string> Rejected;
        public OnlineMatchClient(IOnlineMatchApi api,IRealtimeMatchChannel channel) {this.api=api;this.channel=channel;channel.MatchMessage+=Receive;}
        public async Task CreateAsync() {ApplySnapshot(await api.SendAsync("POST","matches",new JObject {["modeKey"]="DUEL_1V1"},lifetime.Token));await ResyncAsync();}
        public async Task JoinAsync(string id) {Id(id);ApplySnapshot(await api.SendAsync("POST","matches/"+id+"/join",new JObject {["commandId"]=Guid.NewGuid().ToString()},lifetime.Token));}
        static void Id(string id) {if(string.IsNullOrEmpty(id)||id.Length>128||id.Any(c=>!char.IsLetterOrDigit(c)&&c!='-'&&c!='_'))throw new ArgumentException("Invalid match ID");}
        public void ApplySnapshot(JObject data) {
            var next=new OnlineMatchSnapshot(data);
            if(Snapshot!=null && (Snapshot.MatchId!=next.MatchId || Snapshot.Seat!=next.Seat))throw new FormatException("Match identity changed");
            if(Snapshot!=null && next.Sequence<Snapshot.Sequence)return;
            Snapshot=next;TurnClock.Apply(data);NeedsResync=false;Changed?.Invoke();
        }
        public bool ApplyUpdate(JObject update) {
            if(Snapshot==null||(string)update["matchId"]!=Snapshot.MatchId)return false;
            var next=new OnlineMatchSnapshot((JObject)update["snapshot"]);
            if(next.Sequence<=Snapshot.Sequence)return false;
            long expected=Snapshot.Sequence+1;
            if((long)update["firstSequence"]!=expected) {NeedsResync=true;Changed?.Invoke();return false;}
            foreach(var e in (JArray)update["events"])if((long)e["sequence"]!=expected++) {NeedsResync=true;Changed?.Invoke();return false;}
            if(expected-1!=next.Sequence) {NeedsResync=true;Changed?.Invoke();return false;}
            ApplySnapshot((JObject)update["snapshot"]);
            foreach(var e in (JArray)update["events"])EventApplied?.Invoke((string)e["type"]);
            return true;
        }
        void Receive(string type,JObject payload) {
            try {
                if(type=="MATCH_UPDATE") {ApplyUpdate(payload);if(NeedsResync) _=ResyncAsync();}
                else if((string)payload["commandId"]==pendingId) {
                    Pending=false;pendingId=null;
                    if(type=="COMMAND_REJECTED") {Rejected?.Invoke((string)payload["code"]);_=ResyncAsync();}
                    else if((long?)payload["resultingSequence"]>Snapshot?.Sequence) _=ResyncAsync();
                    Changed?.Invoke();
                }
            } catch {NeedsResync=true;Changed?.Invoke();_=ResyncAsync();}
        }
        public Task ResyncAsync() => resync??(resync=Resync());
        public void ConnectionLost() {Pending=false;pendingId=null;NeedsResync=true;Changed?.Invoke();}
        public async Task ConnectionRestoredAsync() {
            NeedsResync=true;Changed?.Invoke();await ResyncAsync();
            // A pre-disconnect in-flight REST request may have failed while the new socket authenticated.
            if(NeedsResync&&!lifetime.IsCancellationRequested)await ResyncAsync();
        }
        async Task Resync() {
            await Task.Yield();
            try {if(Snapshot!=null)ApplySnapshot(await api.SendAsync("GET","matches/"+Snapshot.MatchId+"/snapshot",null,lifetime.Token));}
            catch {NeedsResync=true;Rejected?.Invoke("RESYNC_UNAVAILABLE");}
            finally {resync=null;Changed?.Invoke();}
        }
        public async Task SendAsync(string type,JObject fields=null) {
            if(Snapshot==null||Pending||NeedsResync)throw new InvalidOperationException("Online input unavailable");
            var command=fields==null?new JObject():(JObject)fields.DeepClone();
            command["protocolVersion"]=1;command["commandId"]=Guid.NewGuid().ToString();command["matchId"]=Snapshot.MatchId;command["type"]=type;
            Pending=true;pendingId=(string)command["commandId"];Changed?.Invoke();
            _=WatchAcknowledgement(pendingId);
            try {await channel.SendMatchCommandAsync(command);}catch {Pending=false;pendingId=null;Changed?.Invoke();throw;}
        }
        async Task WatchAcknowledgement(string id) {
            try {
                await Task.Delay(TimeSpan.FromSeconds(20),lifetime.Token);
                if(pendingId!=id)return;
                Pending=false;pendingId=null;NeedsResync=true;Rejected?.Invoke("ACK_TIMEOUT");Changed?.Invoke();await ResyncAsync();
            }catch(OperationCanceledException) { }
        }
        public void Dispose() {channel.MatchMessage-=Receive;lifetime.Cancel();Changed=null;Rejected=null;EventApplied=null;}
    }
}
