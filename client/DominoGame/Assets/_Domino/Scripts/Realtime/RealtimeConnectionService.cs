using System;
using System.Threading;
using System.Threading.Tasks;
using Domino.Identity;
using Domino.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Domino.Realtime
{
    public interface IRealtimeMatchChannel
    {
        event Action<string, JObject> MatchMessage;
        Task SendMatchCommandAsync(JObject command);
    }
    public interface IRealtimeConnectionService : IDisposable
    {
        RealtimeConnectionState State { get; }
        GlobalActivitySnapshot Activity { get; }
        event Action Changed;
        void Start();
        void SetBackground(bool background);
    }
    // Owned by the application, not by menus. All continuations/events use the captured
    // Unity context. Background cancellation fully drains before another socket is created.
    public sealed class RealtimeConnectionService : IRealtimeConnectionService, IRealtimeMatchChannel
    {
        readonly RealtimeConfiguration config;
        readonly IPlayerIdentityService identity;
        readonly IAuthTokenProvider tokens;
        readonly Func<IRealtimeSocket> factory;
        readonly Func<double> jitter;
        readonly Func<TimeSpan, CancellationToken, Task> delay;
        readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        CancellationTokenSource active;
        Task runner;
        bool background, stopped, terminal;
        readonly SynchronizationContext context = SynchronizationContext.Current;
        public RealtimeConnectionState State { get; private set; }
        public GlobalActivitySnapshot Activity { get; private set; }
        public event Action Changed;
        public event Action<string, JObject> MatchMessage;
        Func<string, JObject, Task> matchSender;
        public Task SendMatchCommandAsync(JObject command)
        {
            if (State != RealtimeConnectionState.CONNECTED || matchSender == null) throw new InvalidOperationException("Realtime unavailable");
            return matchSender("MATCH_COMMAND", (JObject)command.DeepClone());
        }
        public RealtimeConnectionService(RealtimeConfiguration config, IPlayerIdentityService identity, IAuthTokenProvider tokens,
            Func<IRealtimeSocket> factory = null, Func<double> jitter = null, Func<TimeSpan, CancellationToken, Task> delay = null)
        {
            this.config = config; this.identity = identity; this.tokens = tokens;
            this.factory = factory ?? (() => new ClientRealtimeSocket());
            var random = new Random(); this.jitter = jitter ?? random.NextDouble;
            this.delay = delay ?? Task.Delay;
        }
        public void Start() => Dispatch(() => {
            if (stopped || background || terminal || runner != null || config?.Endpoint == null) return;
            // Start asynchronously so reentrant event listeners observe the reserved runner.
            runner = Run();
        });
        void Dispatch(Action action) {
            if (context != null && SynchronizationContext.Current != context) context.Post(_ => action(), null); else action();
        }
        async Task Run()
        {
            await Task.Yield();
            int attempt = 0;
            try {
                while (!stopped && !background && !terminal) {
                    bool refresh = false;
                    active = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                    try {
                        Set(attempt == 0 ? RealtimeConnectionState.CONNECTING : RealtimeConnectionState.RECONNECTING);
                        try { await CancellableTask.Wait(identity.InitializeAsync(), active.Token); }
                        catch (OperationCanceledException) { throw; }
                        catch { throw new RealtimeFailure("IDENTITY"); }
                        var uid = identity.Current?.Uid;
                        if (string.IsNullOrEmpty(uid)) throw new RealtimeFailure("IDENTITY");
                        while (true) {
                            try { await Session(uid, refresh, active.Token); break; }
                            catch (RealtimeFailure ex) when (ex.Code == "AUTH_TOKEN_EXPIRED" && !refresh) { refresh = true; }
                        }
                    } catch (OperationCanceledException) { if (!stopped && !background) attempt++; }
                    catch (RealtimeFailure ex) { terminal = !ex.Retryable; attempt++; }
                    catch (Domino.Infrastructure.Api.DominoApiException) { terminal = true; }
                    catch (Newtonsoft.Json.JsonException) { terminal = true; }
                    catch (FormatException) { terminal = true; }
                    catch (OverflowException) { terminal = true; }
                    catch { attempt++; }
                    finally { Activity = null; }
                    if (stopped || background || terminal) { active.Dispose(); active = null; break; }
                    Set(RealtimeConnectionState.RECONNECTING);
                    try { await delay(TimeSpan.FromSeconds(Backoff(attempt, jitter())), active.Token); }
                    catch (OperationCanceledException) { }
                    finally { active.Dispose(); active = null; }
                }
            } finally {
                runner = null; Set(RealtimeConnectionState.DISCONNECTED);
                if (!stopped && !background && !terminal) Start();
            }
        }
        public static double Backoff(int attempt, double random) => Math.Min(30, Math.Pow(2, Math.Min(5, Math.Max(0, attempt - 1)))) * (.8 + .2 * Math.Max(0, Math.Min(1, random)));
        async Task Session(string uid, bool refresh, CancellationToken cancellation)
        {
            using var socket = factory();
            using var session = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            long sequence = 0, incoming = 0;
            using var sendGate = new SemaphoreSlim(1, 1);
            async Task Send(string type, JObject payload) {
                await sendGate.WaitAsync(session.Token);
                try { await socket.SendAsync(RealtimeProtocol.Write(type, ++sequence, payload), session.Token); }
                finally { sendGate.Release(); }
            }
            using (var opening = CancellationTokenSource.CreateLinkedTokenSource(session.Token)) {
                opening.CancelAfter(TimeSpan.FromSeconds(15));
                // Acquire current SDK token before opening: slow SDK refresh does not consume server auth deadline.
                string token;
                try { token = await CancellableTask.Wait(tokens.GetIdTokenAsync(refresh, opening.Token), opening.Token); }
                catch (OperationCanceledException) { throw; }
                catch { throw new RealtimeFailure("IDENTITY"); }
                if (identity.Current?.Uid != uid || string.IsNullOrWhiteSpace(token)) throw new RealtimeFailure("IDENTITY");
                await socket.ConnectAsync(config.Endpoint, opening.Token);
                Set(RealtimeConnectionState.AUTHENTICATING);
                await socket.SendAsync(RealtimeProtocol.Write("AUTH", ++sequence, new JObject { ["idToken"] = token }), opening.Token);
                token = null;
            }
            int interval = 20, timeout = 45;
            using (var auth = CancellationTokenSource.CreateLinkedTokenSource(session.Token)) {
                auth.CancelAfter(TimeSpan.FromSeconds(7));
                var response = RealtimeProtocol.Read(await socket.ReceiveAsync(auth.Token), ref incoming);
                Reject(response);
                if ((string)response["type"] != "AUTHENTICATED") throw new RealtimeFailure("PROTOCOL");
                interval = (int?)response["payload"]["heartbeatIntervalSeconds"] ?? 0;
                timeout = (int?)response["payload"]["heartbeatTimeoutSeconds"] ?? 0;
                if (interval < 5 || interval > 60 || timeout <= interval || timeout > 300) throw new RealtimeFailure("PROTOCOL");
            }
            Set(RealtimeConnectionState.CONNECTED);
            matchSender = Send;
            await Send("GLOBAL_ACTIVITY_SUBSCRIBE", new JObject());
            // .NET's Unity-compatible ClientWebSocket does not expose native pong callbacks.
            // Application PING/PONG gives both peers a verifiable timeout, at 20s (not polling).
            var heartbeat = Heartbeat();
            async Task Heartbeat() {
                try { while (true) { await delay(TimeSpan.FromSeconds(interval), session.Token); await Send("PING", new JObject()); } }
                catch { session.Cancel(); }
            }
            var lastPong = System.Diagnostics.Stopwatch.StartNew();
            try {
                while (true) {
                    using var receiving = CancellationTokenSource.CreateLinkedTokenSource(session.Token);
                    var remaining = timeout - lastPong.Elapsed.TotalSeconds;
                    if (remaining <= 0) throw new RealtimeFailure("HEARTBEAT", true);
                    receiving.CancelAfter(TimeSpan.FromSeconds(remaining));
                    var response = RealtimeProtocol.Read(await socket.ReceiveAsync(receiving.Token), ref incoming);
                    if (identity.Current?.Uid != uid) throw new RealtimeFailure("IDENTITY");
                    Reject(response);
                    switch ((string)response["type"]) {
                        case "MATCH_UPDATE": case "COMMAND_ACCEPTED": case "COMMAND_REJECTED":
                            foreach(Action<string,JObject> listener in MatchMessage?.GetInvocationList() ?? Array.Empty<Delegate>())
                                try {listener((string)response["type"],(JObject)response["payload"]);} catch { }
                            break;
                        case "PONG": lastPong.Restart(); break;
                        case "PRESENCE_READY": break;
                        case "GLOBAL_ACTIVITY_SNAPSHOT": case "GLOBAL_ACTIVITY_UPDATED":
                            Activity = RealtimeProtocol.Activity((JObject)response["payload"]); Notify(); break;
                        default: throw new RealtimeFailure("PROTOCOL");
                    }
                }
            } finally { matchSender = null; session.Cancel(); await heartbeat; }
        }
        static void Reject(JObject message) {
            string type = (string)message["type"];
            if (type == "AUTH_FAILED" || type == "SYSTEM_ERROR") {
                string code = (string)message["payload"]["code"];
                bool retry = code == "UNAVAILABLE" || code == "DEPENDENCY_UNAVAILABLE" || code == "INTERNAL_ERROR";
                // Expiry may refresh exactly once in this reconnect attempt.
                throw new RealtimeFailure(code == "AUTH_TOKEN_EXPIRED" ? code : retry ? "UNAVAILABLE" : "REJECTED", retry);
            }
        }
        void Set(RealtimeConnectionState state) { State = state; Notify(); }
        void Notify() {
            if (stopped) return;
            foreach (Action callback in Changed?.GetInvocationList() ?? Array.Empty<Delegate>()) try { callback(); } catch { }
        }
        public void SetBackground(bool value) => Dispatch(() => { background = value; if (value) active?.Cancel(); else Start(); });
        public void Dispose() => Dispatch(() => { stopped = true; Changed = null; active?.Cancel(); lifetime.Cancel(); Activity = null; State = RealtimeConnectionState.DISCONNECTED; });
    }
}
