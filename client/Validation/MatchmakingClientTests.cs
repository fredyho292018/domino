using System;
using System.Threading;
using System.Threading.Tasks;
using Domino.Online;
using Domino.Realtime;
using Newtonsoft.Json.Linq;

class MatchmakingClientTests
{
    static int checks;
    static void Check(bool v,string label){checks++;if(!v)throw new Exception(label);}
    sealed class Channel:IRealtimeMatchChannel {
        public event Action<string,JObject> MatchMessage;
        public Task SendMatchCommandAsync(JObject command)=>Task.CompletedTask;
        public void Emit(string t,JObject p)=>MatchMessage?.Invoke(t,p);
    }
    sealed class Api:IOnlineMatchApi {
        public int Joins,Leaves,Reads;public string Mode="DUEL_1V1";public Func<string,Task<JObject>> Reply;
        public Task<JObject> SendAsync(string method,string path,JObject body,CancellationToken token){
            Check(path=="matchmaking/queue","authenticated queue endpoint only");
            if(method=="POST"){Joins++;Check(body.Count==1&&(string)body["modeKey"]==Mode,"no UID rules seat or opponent authority");}
            else if(method=="DELETE")Leaves++;else Reads++;
            return Reply(method);
        }
    }
    static JObject Status(string s)=>new JObject{["state"]=s};
    static JObject Found(string id,int seat)=>new JObject{["state"]="MATCHED",["match"]=new JObject{["matchId"]=id,["seat"]=seat}};
    static async Task Main()
    {
        var channel=new Channel();var api=new Api{Reply=m=>Task.FromResult(Status(m=="POST"?"QUEUED":"NOT_QUEUED"))};
        using(var client=new MatchmakingClient(api,channel)) {
            await client.RecoverAsync();Check(client.State==MatchmakingState.IDLE,"initial recovery");
            await client.JoinAsync();Check(client.State==MatchmakingState.SEARCHING,"search starts");
            await client.JoinAsync();Check(api.Joins==1,"duplicate search single flight");
            await client.CancelAsync();Check(client.State==MatchmakingState.IDLE&&api.Leaves==1,"cancel confirmed");
            await client.JoinAsync();client.ConnectionLost();Check(client.State==MatchmakingState.FAILED,"disconnect not searching");
            int joins=api.Joins;await client.RecoverAsync();Check(client.State==MatchmakingState.IDLE&&api.Joins==joins,"no automatic queue rejoin after Redis loss");
            var assigned=Found(Guid.NewGuid().ToString(),1);api.Reply=m=>Task.FromResult(assigned);
            await client.RecoverAsync();Check(client.MatchId==(string)assigned["match"]["matchId"]&&client.Seat==1,"missed MATCH_FOUND recovery");
            channel.Emit("MATCHMAKING_STATUS",Status("NOT_QUEUED"));Check(client.State==MatchmakingState.MATCH_FOUND,"late cancel cannot undo assignment");
            await client.JoinAsync();Check(api.Joins==joins,"assigned user cannot requeue locally");
            await client.CancelAsync();Check(client.State==MatchmakingState.MATCH_FOUND,"cancel after assignment preserves found state");
            client.BeginEntering();int changes=0,diagnostics=0;client.Changed+=()=>changes++;client.Diagnostic+=_=>diagnostics++;
            channel.Emit("MATCH_FOUND",assigned);
            Check(client.State==MatchmakingState.ENTERING_MATCH&&changes==0,"duplicate assignment does not restart entry");
            channel.Emit("MATCH_FOUND",Found(Guid.NewGuid().ToString(),0));
            Check(client.MatchId==(string)assigned["match"]["matchId"]&&diagnostics==1,"conflicting assignment retained safely with diagnostic");
            client.EntryFailed();await client.RecoverAsync();Check(client.State==MatchmakingState.MATCH_FOUND,"failed entry recovers same assignment");
        }
        // Join still in flight when the user cancels. DELETE must be ordered after it.
        var pending=new TaskCompletionSource<JObject>();api=new Api{Reply=m=>m=="POST"?pending.Task:Task.FromResult(Status("NOT_QUEUED"))};
        using(var client=new MatchmakingClient(api,new Channel())) {
            await client.RecoverAsync();var join=client.JoinAsync();var cancel=client.CancelAsync();
            Check(api.Leaves==0,"cancel waits for pending join");
            pending.SetResult(Status("QUEUED"));await Task.WhenAll(join,cancel);
            Check(api.Joins==1&&api.Leaves==1&&client.State==MatchmakingState.IDLE,"join cancel deterministic");
        }
        channel=new Channel();api=new Api{Reply=m=>Task.FromResult(Status(m=="POST"?"QUEUED":m=="DELETE"?"RESERVED":"NOT_QUEUED"))};
        using(var client=new MatchmakingClient(api,channel)) {
            await client.RecoverAsync();await client.JoinAsync();await client.CancelAsync();
            Check(client.State==MatchmakingState.JOINING,"reservation won not false cancellation");
            channel.Emit("MATCH_FOUND",Found(Guid.NewGuid().ToString(),0));Check(client.State==MatchmakingState.MATCH_FOUND,"reserved then assigned");
        }
        pending=new TaskCompletionSource<JObject>();channel=new Channel();api=new Api{Reply=m=>m=="DELETE"?pending.Task:Task.FromResult(Status(m=="POST"?"QUEUED":"NOT_QUEUED"))};
        using(var client=new MatchmakingClient(api,channel)) {
            await client.RecoverAsync();await client.JoinAsync();var cancel=client.CancelAsync();
            var duplicateCancel=client.CancelAsync();Check(api.Leaves==1,"double tap cancel single flight");
            channel.Emit("MATCH_FOUND",Found(Guid.NewGuid().ToString(),0));pending.SetResult(Status("RESERVED"));await cancel;
            await duplicateCancel;
            Check(client.State==MatchmakingState.MATCH_FOUND,"late HTTP response after WS assignment");
        }
        channel=new Channel();api=new Api{Reply=m=>Task.FromException<JObject>(new Exception("transport"))};
        using(var client=new MatchmakingClient(api,channel)) {
            await client.RecoverAsync();Check(client.State==MatchmakingState.FAILED,"safe transport failure");
            channel.Emit("MATCH_FOUND",Found("invalid",3));Check(client.MatchId==null,"malformed assignment rejected");
            client.Dispose();channel.Emit("MATCH_FOUND",Found(Guid.NewGuid().ToString(),0));Check(client.MatchId==null,"disposed subscriptions removed");
        }
        pending=new TaskCompletionSource<JObject>();api=new Api{Reply=m=>m=="GET"?pending.Task:Task.FromResult(Status("NOT_QUEUED"))};
        using(var client=new MatchmakingClient(api,new Channel())) {
            var recovery=client.RecoverAsync();await client.CancelAsync();pending.SetResult(Status("QUEUED"));await recovery;
            Check(client.State==MatchmakingState.IDLE,"late recovery cannot resurrect cancelled search");
        }
        for(int seat=0;seat<4;seat++) {
            channel=new Channel();api=new Api{Mode="PARTNERS_2V2_ONLINE",Reply=m=>Task.FromResult(Status(m=="POST"?"QUEUED":"NOT_QUEUED"))};
            using var partners=new MatchmakingClient(api,channel,api.Mode);
            await partners.RecoverAsync();await partners.JoinAsync();Check(api.Joins==1&&partners.State==MatchmakingState.SEARCHING,"four player search");
            var bad=Found(Guid.NewGuid().ToString(),4);bad["match"]["modeKey"]=api.Mode;channel.Emit("MATCH_FOUND",bad);Check(partners.MatchId==null,"fifth seat rejected");
            var assignment=Found(Guid.NewGuid().ToString(),seat);assignment["match"]["modeKey"]=api.Mode;channel.Emit("MATCH_FOUND",assignment);
            Check(partners.Seat==seat&&partners.MatchId==(string)assignment["match"]["matchId"],"all four server seats accepted");
            int changes=0;partners.Changed+=()=>changes++;channel.Emit("MATCH_FOUND",assignment);Check(changes==0,"four player duplicate assignment ignored");
        }
        Console.WriteLine("I3_M5_CLIENT_TESTS="+checks+" PASS");
    }
}
