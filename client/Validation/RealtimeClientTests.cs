using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Domino.Realtime;
using Domino.Infrastructure.Api;
using Newtonsoft.Json.Linq;

static partial class PlayerFoundationClientTests
{
    sealed class Socket : IRealtimeSocket
    {
        readonly ConcurrentQueue<string> inbox = new ConcurrentQueue<string>();
        readonly SemaphoreSlim ready = new SemaphoreSlim(0);
        public bool Disposed, Drop;
        public string AuthError;
        public bool MissingPong;
        public int Sends;
        long sequence;
        public Task ConnectAsync(Uri endpoint, CancellationToken ct) { ct.ThrowIfCancellationRequested(); return Task.CompletedTask; }
        public void Push(string type, JObject payload) { inbox.Enqueue(RealtimeProtocol.Write(type,++sequence,payload)); ready.Release(); }
        public Task SendAsync(string message, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested(); Sends++;
            var root = JObject.Parse(message);
            switch ((string)root["type"]) {
                case "AUTH":
                    Check(root["payload"].ToString().Contains("fake-token"),"WebSocket token transit");
                    if (AuthError != null) Push("AUTH_FAILED",new JObject{["code"]=AuthError});
                    else Push("AUTHENTICATED",new JObject{["heartbeatIntervalSeconds"]=MissingPong?5:20,["heartbeatTimeoutSeconds"]=MissingPong?6:45});
                    break;
                case "GLOBAL_ACTIVITY_SUBSCRIBE": Push("GLOBAL_ACTIVITY_SNAPSHOT",new JObject{["onlinePlayers"]=1,["activeMatches"]=0,["waitingPlayers"]=0,["openRooms"]=0}); break;
                case "PING": if(!MissingPong)Push("PONG",new JObject()); break;
            }
            return Task.CompletedTask;
        }
        public async Task<string> ReceiveAsync(CancellationToken ct) {
            await ready.WaitAsync(ct); if(Drop)throw new System.Net.WebSockets.WebSocketException();
            if(inbox.TryDequeue(out var message)) return message; throw new Exception("empty");
        }
        public void Disconnect() { Drop=true; ready.Release(); }
        public void Dispose() { Disposed=true; }
    }
    static async Task Eventually(Func<bool> condition) {
        var until=DateTime.UtcNow.AddSeconds(3);
        while(!condition() && DateTime.UtcNow<until)await Task.Delay(10);
        Check(condition(),"Realtime eventual state");
    }
    static async Task RealtimeTests()
    {
        RealtimeConfiguration Config(string url, string env="LOCAL", bool development=true) => new RealtimeConfiguration(new DominoApiConfiguration(true,url,15,env,development));
        Check(Config("https://example.com").Endpoint.Scheme=="wss","Secure remote WebSocket");
        foreach(var host in new[]{"localhost","127.0.0.1"}) {
            Check(Config("http://"+host+":8080").Endpoint.AbsoluteUri=="ws://"+host+":8080/ws/v1/realtime","Local WebSocket endpoint");
            Check(Config("http://"+host,"PROD").Endpoint==null,"Local env required");
            Check(Config("http://"+host,"LOCAL",false).Endpoint==null,"Development required");
        }
        foreach(var host in new[]{"192.168.1.100","example.com","evil.localhost.example.com","127.1"})Check(Config("http://"+host).Endpoint==null,"Remote HTTP rejected");
        var token=new Tokens(); var identity=new Identity(); var sockets=new List<Socket>();
        using(var service=new RealtimeConnectionService(Config("https://example.com"),identity,token,()=>{var s=new Socket();sockets.Add(s);return s;},()=>.5,
            (duration,ct)=>Task.Delay(duration.TotalSeconds<5?TimeSpan.FromMilliseconds(20):duration,ct))) {
            service.Start();service.Start();
            await Eventually(()=>service.Activity!=null);
            Check(sockets.Count==1 && service.State==RealtimeConnectionState.CONNECTED,"One application socket");
            Check(service.Activity.OnlinePlayers==1 && service.Activity.ActiveMatches==0,"Real aggregate data");
            sockets[0].Disconnect(); await Eventually(()=>sockets.Count==2&&service.Activity!=null);
            Check(sockets[0].Disposed && identity.Current.Uid=="u1","Reconnect same identity disposes prior socket");
            Check(token.Refresh.Count==2&&!token.Refresh[0]&&!token.Refresh[1],"Reconnect gets current token");
            service.SetBackground(true); await Eventually(()=>service.State==RealtimeConnectionState.DISCONNECTED);
            Check(sockets[1].Disposed && service.Activity==null,"Background closes and clears stale stats");
            service.SetBackground(false); await Eventually(()=>sockets.Count==3&&service.Activity!=null);
            service.Dispose();Check(service.State==RealtimeConnectionState.DISCONNECTED,"Shutdown state");
        }
        token=new Tokens(); int factories=0;
        using(var expired=new RealtimeConnectionService(Config("https://example.com"),new Identity(),token,()=>new Socket{AuthError=++factories==1?"AUTH_TOKEN_EXPIRED":null})) {
            expired.Start();await Eventually(()=>expired.State==RealtimeConnectionState.CONNECTED);
            Check(factories==2&&token.Refresh.Count==2&&!token.Refresh[0]&&token.Refresh[1],"Expiry refresh exactly once");
        }
        foreach(var error in new[]{"AUTH_TOKEN_EXPIRED","AUTH_SESSION_INVALID","AUTH_TOKEN_INVALID","PROTOCOL"}) {
            token=new Tokens();factories=0;
            using var rejected=new RealtimeConnectionService(Config("https://example.com"),new Identity(),token,()=>{factories++;return new Socket{AuthError=error};});
            rejected.Start();await Eventually(()=>factories>0&&rejected.State==RealtimeConnectionState.DISCONNECTED);
            int expected=error=="AUTH_TOKEN_EXPIRED"?2:1; await Task.Delay(30);
            rejected.Start();Check(factories==expected,"Terminal auth stops retry loop");
        }
        for(int i=1;i<=20;i++)Check(RealtimeConnectionService.Backoff(i,0)>0&&RealtimeConnectionService.Backoff(i,1)<=30,"Bounded backoff");
        Check(RealtimeConnectionService.Backoff(4,0)!=RealtimeConnectionService.Backoff(4,1),"Jitter present");
        factories=0;
        using(var missing=new RealtimeConnectionService(Config("https://example.com"),new Identity(),new Tokens(),()=>{factories++;return new Socket{MissingPong=true};},()=>0)) {
            missing.Start(); await Eventually(()=>missing.State==RealtimeConnectionState.CONNECTED);
            await Task.Delay(7200);Check(factories>=2,"Missing heartbeat reconnects");
        }
        var pendingIdentity=new Identity{Pending=new TaskCompletionSource<Domino.Identity.PlayerIdentity>()};
        using(var waiting=new RealtimeConnectionService(Config("https://example.com"),pendingIdentity,new Tokens(),()=>new Socket())) {
            waiting.Start();await Eventually(()=>pendingIdentity.Calls==1);
            waiting.SetBackground(true);await Eventually(()=>waiting.State==RealtimeConnectionState.DISCONNECTED);
        }
        long previous=0;
        RealtimeProtocol.Read(RealtimeProtocol.Write("PONG",1,new JObject()),ref previous);
        try{RealtimeProtocol.Read(RealtimeProtocol.Write("PONG",1,new JObject()),ref previous);throw new Exception("Sequence accepted");}catch(RealtimeFailure){checks++;}
        Console.WriteLine("REALTIME_CLIENT_STATES_AUTH_RECONNECT_LIFECYCLE=PASS");
    }
}
