using System;
using System.Threading;
using System.Threading.Tasks;
using Domino.Realtime;
using Newtonsoft.Json.Linq;

namespace Domino.Online
{
    public enum MatchmakingState { IDLE, JOINING, SEARCHING, MATCH_FOUND, ENTERING_MATCH, CANCELLING, FAILED }

    // Presentation only: identity, compatibility, FIFO, seats and assignment belong to the server.
    public sealed class MatchmakingClient : IDisposable
    {
        readonly IOnlineMatchApi api;
        readonly IRealtimeMatchChannel channel;
        readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        Task join, cancel;
        bool disposed, connected;
        long operation;
        public MatchmakingState State { get; private set; }
        public string MatchId { get; private set; }
        public int Seat { get; private set; } = -1;
        public event Action Changed;
        public event Action<string> Diagnostic;
        public MatchmakingClient(IOnlineMatchApi api, IRealtimeMatchChannel channel)
        { this.api=api; this.channel=channel; channel.MatchMessage+=Receive; }

        void Set(MatchmakingState value) { if(disposed)return; State=value; Changed?.Invoke(); }
        void Apply(JObject response)
        {
            if(disposed)return;
            if((string)response["state"]=="MATCHED") {
                string id=(string)response["match"]?["matchId"];
                int? seat=(int?)response["match"]?["seat"];
                if(!Guid.TryParse(id,out _) || seat<0 || seat>1 || seat==null)throw new FormatException("Invalid assignment");
                if(MatchId!=null) {
                    if(MatchId!=id||Seat!=seat.Value)throw new FormatException("Assignment changed");
                    if(State!=MatchmakingState.FAILED)return;
                }
                MatchId=id;Seat=seat.Value;Set(MatchmakingState.MATCH_FOUND);return;
            }
            // Late join/cancel replies cannot undo an already committed assignment.
            if(MatchId!=null)return;
            switch((string)response["state"]) {
                case "NOT_QUEUED": Set(MatchmakingState.IDLE);break;
                case "QUEUED": Set(connected?MatchmakingState.SEARCHING:MatchmakingState.FAILED);break;
                case "RESERVED": Set(MatchmakingState.JOINING);break;
                case "FAILED": Set(MatchmakingState.FAILED);break;
                default: throw new FormatException("Invalid matchmaking state");
            }
        }
        void Receive(string type,JObject payload)
        {
            if(type!="MATCH_FOUND" && type!="MATCHMAKING_STATUS")return;
            try { Apply(payload); } catch { Diagnostic?.Invoke("ASSIGNMENT_INVALID");if(MatchId==null)Set(MatchmakingState.FAILED); }
        }
        public void BeginEntering(){if(State==MatchmakingState.MATCH_FOUND)Set(MatchmakingState.ENTERING_MATCH);}
        public void EntryFailed(){Set(MatchmakingState.FAILED);}
        void Reply(JObject response,long expected)
        {
            // Late HTTP recovery/join replies must not resurrect a cancelled search.
            if(expected==operation||(string)response["state"]=="MATCHED")Apply(response);
        }
        public async Task RecoverAsync()
        {
            connected=true;
            long expected=operation;
            try {
                // One recovery request on connection, never polling. Assignment survives lost WS delivery.
                Reply(await api.SendAsync("GET","matchmaking/queue",null,lifetime.Token),expected);
            } catch { if(MatchId==null&&expected==operation)Set(MatchmakingState.FAILED); }
        }
        public void ConnectionLost() { connected=false;if(MatchId==null)Set(MatchmakingState.FAILED); }
        public Task JoinAsync()
        {
            if(join!=null&&!join.IsCompleted)return join;
            if(disposed||!connected||MatchId!=null||State==MatchmakingState.SEARCHING||State==MatchmakingState.CANCELLING)return Task.CompletedTask;
            return join=Join();
        }
        async Task Join()
        {
            long expected=++operation;
            Set(MatchmakingState.JOINING);
            try { Reply(await api.SendAsync("POST","matchmaking/queue",new JObject{["modeKey"]="DUEL_1V1"},lifetime.Token),expected); }
            catch { if(MatchId==null&&expected==operation)Set(MatchmakingState.FAILED); }
        }
        public Task CancelAsync()
        {
            if(disposed||MatchId!=null)return Task.CompletedTask;
            if(cancel!=null&&!cancel.IsCompleted)return cancel;
            return cancel=Cancel();
        }
        async Task Cancel()
        {
            long expected=++operation;
            Set(MatchmakingState.CANCELLING);
            // A cancel clicked during join must reach Redis after the join request completes.
            if(join!=null)await join;
            if(disposed||MatchId!=null)return;
            Set(MatchmakingState.CANCELLING);
            try { Reply(await api.SendAsync("DELETE","matchmaking/queue",null,lifetime.Token),expected); }
            catch { if(MatchId==null&&expected==operation)Set(MatchmakingState.FAILED); }
        }
        public void Dispose() {disposed=true;channel.MatchMessage-=Receive;lifetime.Cancel();Changed=null;Diagnostic=null;}
    }
}
