using System;
using System.Threading;
using System.Threading.Tasks;
using Domino.Online;
using Domino.Realtime;
using Newtonsoft.Json.Linq;

class OnlineMatchClientTests
{
    static int checks;
    static void Check(bool value,string label) {checks++;if(!value)throw new Exception(label);}
    sealed class Channel:IRealtimeMatchChannel {
        public event Action<string,JObject> MatchMessage;public JObject Last;
        public Task SendMatchCommandAsync(JObject command) {Last=command;return Task.CompletedTask;}
        public void Emit(string type,JObject payload)=>MatchMessage?.Invoke(type,payload);
    }
    sealed class Api:IOnlineMatchApi {
        public JObject Value;public int Calls;
        public Task<JObject> SendAsync(string method,string path,JObject body,CancellationToken token) {Calls++;return Task.FromResult(Value);}
    }
    static JObject Snapshot(long seq,int seat=0,string phase="PLAYING")=>new JObject {
        ["lastSequence"]=seq,["phase"]=phase,["starter"]=null,["ruleSnapshot"]=new JObject(),
        ["publicState"]=new JObject {["matchId"]="fixture",["lastSequence"]=seq,["board"]=new JArray(),["currentSeat"]=0,["scores"]=new JArray(0,0)},
        ["privateState"]=new JObject {["seat"]=seat,["lastSequence"]=seq,["hand"]=new JArray(new JObject {["sideA"]=seat,["sideB"]=9})}
    };
    static JObject Update(long first,long last,int seat=0) {
        var events=new JArray();for(long i=first;i<=last;i++)events.Add(new JObject {["sequence"]=i,["type"]="SEQUENCE_ADVANCED",["event"]=null});
        return new JObject {["matchId"]="fixture",["firstSequence"]=first,["events"]=events,["snapshot"]=Snapshot(last,seat)};
    }
    static async Task Main() {
        var wirePath=System.Environment.GetEnvironmentVariable("DOMINO_I1_FIXTURES");
        if(!string.IsNullOrEmpty(wirePath)) {
            string wire=System.IO.File.ReadAllText(System.IO.Path.Combine(wirePath,"i1-wire-message.json"));long previous=0;
            var message=RealtimeProtocol.Read(wire,ref previous);
            Check((string)message["type"]=="MATCH_UPDATE","actual server wire contract");
            Check(System.Text.Encoding.UTF8.GetByteCount(wire)<=RealtimeProtocol.MaxBytes,"wire size budget");
            Check(new OnlineMatchSnapshot((JObject)message["payload"]["snapshot"]).Hand.Count<=10,"actual private DTO");
        }
        var api=new Api{Value=Snapshot(5)};var channel=new Channel();using var client=new OnlineMatchClient(api,channel);
        await client.CreateAsync();Check(client.Snapshot.Sequence==5,"initial snapshot");
        Check(client.ApplyUpdate(Update(6,8)),"ordered batch");Check(client.Snapshot.Sequence==8,"sequence");
        Check(!client.ApplyUpdate(Update(6,8)),"duplicate ignored");
        Check(!client.ApplyUpdate(Update(10,11)),"gap rejected");Check(client.NeedsResync&&client.Snapshot.Sequence==8,"gap freezes view");
        api.Value=Snapshot(11);await client.ResyncAsync();Check(!client.NeedsResync&&client.Snapshot.Sequence==11,"resync authoritative");
        var copy=client.Snapshot.Hand;copy.Clear();Check(client.Snapshot.Hand.Count==1,"immutable private snapshot");
        await client.SendAsync("PLAY_TILE",new JObject {["tile"]=new JObject {["sideA"]=0,["sideB"]=9},["chainEnd"]="RIGHT"});
        Check(client.Pending,"single flight reserved");Check((string)channel.Last["type"]=="PLAY_TILE","intent only");
        Check(channel.Last["uid"]==null&&channel.Last["score"]==null&&channel.Last["seat"]==null,"no client authority");
        bool blocked=false;try {await client.SendAsync("PASS");}catch(InvalidOperationException){blocked=true;}Check(blocked,"single flight");
        channel.Emit("COMMAND_ACCEPTED",new JObject {["commandId"]=channel.Last["commandId"].DeepClone(),["resultingSequence"]=11});Check(!client.Pending,"ack unlock");
        client.ApplySnapshot(Snapshot(3));Check(client.Snapshot.Sequence==11,"stale snapshot ignored");
        bool mismatch=false;try {client.ApplySnapshot(Snapshot(12,1));}catch(FormatException){mismatch=true;}Check(mismatch,"seat cannot change");
        var api2=new Api{Value=Snapshot(11,1)};using var player2=new OnlineMatchClient(api2,new Channel());await player2.JoinAsync("fixture");
        Check(player2.Snapshot.Seat==1&&client.Snapshot.Seat==0,"independent clients");
        Check((int)player2.Snapshot.Hand[0]["sideA"]==1&&(int)client.Snapshot.Hand[0]["sideA"]==0,"private hands isolated");
        foreach(var phase in new[]{"STARTER_SELECTION","PLAYING","ROUND_FINISHED","MATCH_FINISHED"}) {client.ApplySnapshot(Snapshot(12,0,phase));Check(client.Snapshot.Phase==phase,"phase applied");}
        Console.WriteLine("ONLINE_CLIENT_TESTS=PASS CHECKS="+checks);
    }
}
