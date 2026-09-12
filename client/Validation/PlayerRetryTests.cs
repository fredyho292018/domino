using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Domino.Identity;
using Domino.Infrastructure.Api;
using Domino.Player;

static partial class PlayerFoundationClientTests
{
    static async Task RetryTests()
    {
        var identity = new Identity(); var tokens = new Tokens(); var http = new Transport { Failure = new Exception() };
        var logs = new List<string>();
        var service = new PlayerService(identity, Api(tokens, http), () => Task.FromResult("es"), default, logs.Add);
        var states = new List<PlayerSyncState>(); var availability = new List<BackendAvailability>();
        service.SyncStateChanged += s => throw new Exception("subscriber-secret");
        service.SyncStateChanged += states.Add;
        service.BackendAvailabilityChanged += availability.Add;
        await service.InitializeAsync();
        Check(service.CanRetry && service.Availability == BackendAvailability.UNAVAILABLE && !service.HasConfirmedSnapshots, "First failure offline without invented data");
        await service.RetryAsync();
        Check(service.State == PlayerSyncState.FAILED && availability.Count == 1, "Retry failure and no duplicate availability event");
        http.Failure = null; http.Pending = new TaskCompletionSource<ApiHttpResponse>();
        Task reentrant = null;
        service.SyncStateChanged += s => { if (s == PlayerSyncState.SYNCING) reentrant = service.RetryAsync(); };
        var first = service.RetryAsync(); var second = service.InitializeAsync();
        Check(ReferenceEquals(first, second) && ReferenceEquals(first, reentrant) && ReferenceEquals(first, service.RetryAsync()), "Mixed calls and reentrant events share reserved task");
        http.Pending.SetResult(new ApiHttpResponse(200, Success)); await first;
        Check(service.IsFresh && service.Availability == BackendAvailability.AVAILABLE && !service.CanRetry && identity.Calls == 1, "Recovered without reinitializing identity");
        Check(states.Count == 6 && states[3] == PlayerSyncState.FAILED && states[4] == PlayerSyncState.SYNCING && states[5] == PlayerSyncState.SYNCED, "Exact recovery event sequence");
        int count = http.Calls; await service.RetryAsync(); Check(http.Calls == count && states.Count == 6, "Synced retry no-op");
        var player = service.Player; var wallet = service.Wallet;
        http.Pending = null; http.Failure = new Exception(); await service.RefreshConfirmedAsync();
        Check(ReferenceEquals(player, service.Player) && ReferenceEquals(wallet, service.Wallet) && !service.IsFresh && service.HasConfirmedSnapshots, "Failed refresh preserves stale confirmed snapshots");
        http.Failure = null; await service.RetryAsync(); Check(service.IsFresh && service.Player.Uid == player.Uid && service.Wallet.Coins == wallet.Coins && !ReferenceEquals(player, service.Player), "Successful retry refreshes confirmed snapshots");
        player = service.Player; wallet = service.Wallet;
        http.Responses.Enqueue(new ApiHttpResponse(200, Success.Replace("u1", "wrong"))); await service.RefreshConfirmedAsync();
        Check(!service.CanRetry && service.State == PlayerSyncState.FAILED && ReferenceEquals(player, service.Player) && ReferenceEquals(wallet, service.Wallet), "UID mismatch cannot replace cached data");
        Check(logs.Contains("[PLAYER] retry starting") && logs.Contains("[PLAYER] retry succeeded") && logs.Contains("[PLAYER] retry failed category=Transport"), "Safe retry logs");

        foreach (int status in new[] { 400, 401, 403, 409, 500, 503 })
        {
            http = new Transport(); http.Responses.Enqueue(new ApiHttpResponse(status, "{}"));
            if (status == 401) http.Responses.Enqueue(new ApiHttpResponse(401, "{}"));
            service = new PlayerService(new Identity(), Api(new Tokens(), http), () => Task.FromResult("es"), default);
            await service.InitializeAsync(); count = http.Calls;
            Check(service.CanRetry == (status >= 500), "HTTP retryability policy"); await service.RetryAsync();
            Check(status >= 500 ? service.IsFresh : http.Calls == count, "Retry policy enforced");
        }
        tokens = new Tokens(); http = new Transport();
        service = new PlayerService(new Identity(), new DominoApiClient(new DominoApiConfiguration(false, ""), tokens, http, new UnityApiJsonCodec()), () => Task.FromResult("es"), default);
        await service.InitializeAsync(); await service.RetryAsync(); Check(!service.CanRetry && http.Calls == 0 && tokens.Refresh.Count == 0, "Configuration not retryable");

        http = new Transport { Pending = new TaskCompletionSource<ApiHttpResponse>() };
        service = new PlayerService(new Identity(), Api(new Tokens(), http, 1), () => Task.FromResult("es"), default);
        await service.InitializeAsync(); Check(service.Error.Category == ApiFailure.Timeout && service.CanRetry, "Timeout retryable");
        http.Pending.SetResult(new ApiHttpResponse(200, Success)); http.Pending = null;
        await service.RetryAsync(); Check(service.IsFresh, "Retry after timeout works");

        identity = new Identity(); http = new Transport { Failure = new Exception() };
        service = new PlayerService(identity, Api(new Tokens(), http), () => Task.FromResult("es"), default);
        await service.InitializeAsync(); identity.Current = null; http.Failure = null; count = http.Calls;
        await service.RetryAsync(); Check(service.Error.Category == ApiFailure.Authentication && http.Calls == count && identity.Calls == 1, "Missing retry identity never initializes new Guest");

        http = new Transport { Pending = new TaskCompletionSource<ApiHttpResponse>() };
        service = new PlayerService(new Identity(), Api(new Tokens(), http), () => Task.FromResult("es"), default);
        using var cancel = new CancellationTokenSource(); first = service.RetryAsync(cancel.Token); cancel.Cancel(); await first;
        Check(service.State == PlayerSyncState.FAILED && service.Error.Category == ApiFailure.Cancelled, "Caller cancellation leaves no SYNCING");
        http.Pending.SetResult(new ApiHttpResponse(200, Success)); await Task.Delay(20); Check(service.Wallet == null, "Late cancelled response ignored");
        http.Pending = new TaskCompletionSource<ApiHttpResponse>(); first = service.RetryAsync();
        service.Dispose(); await first; Check(service.State == PlayerSyncState.FAILED && !service.CanRetry, "Dispose cancels operation");

        // Actual simultaneous callers (no Unity context in this deterministic unit test).
        http = new Transport { Pending = new TaskCompletionSource<ApiHttpResponse>() };
        service = new PlayerService(new Identity(), Api(new Tokens(), http), () => Task.FromResult("es"), default);
        var tasks = new Task[12]; Parallel.For(0, tasks.Length, i => tasks[i] = i % 2 == 0 ? service.InitializeAsync() : service.RetryAsync());
        for (int i = 1; i < tasks.Length; i++) Check(ReferenceEquals(tasks[0], tasks[i]), "Concurrent callers share task");
        Check(http.Calls == 1, "Only one concurrent HTTP request"); http.Pending.SetResult(new ApiHttpResponse(200, Success)); await tasks[0];
        MainContextTests();
        Console.WriteLine("RETRY_CONCURRENCY_EVENTS_SNAPSHOTS=PASS");
    }
    sealed class MainContext : SynchronizationContext
    {
        readonly ConcurrentQueue<Action> pending = new ConcurrentQueue<Action>();
        public override void Post(SendOrPostCallback callback, object state) => pending.Enqueue(() => callback(state));
        public void Pump() { while (pending.TryDequeue(out var callback)) callback(); }
    }
    static void MainContextTests()
    {
        var previous = SynchronizationContext.Current;
        var main = new MainContext(); SynchronizationContext.SetSynchronizationContext(main);
        try
        {
            var http = new Transport();
            var service = new PlayerService(new Identity(), Api(new Tokens(), http), () => Task.FromResult("es"), default);
            int events = 0; bool correctContext = true;
            Action<PlayerSyncState> listener = s => { events++; correctContext &= SynchronizationContext.Current == main; };
            service.SyncStateChanged += listener;
            Task operation = null;
            var worker = new Thread(() => { SynchronizationContext.SetSynchronizationContext(null); operation = service.RetryAsync(); });
            worker.Start(); worker.Join();
            Check(http.Calls == 0 && !operation.IsCompleted, "Worker call marshalled to main context");
            main.Pump(); operation.GetAwaiter().GetResult();
            Check(events == 2 && correctContext && service.IsFresh, "State events on captured context");
            service.SyncStateChanged -= listener;
            service.RefreshConfirmedAsync().GetAwaiter().GetResult(); Check(events == 2, "Subscriber can unsubscribe");
            service = new PlayerService(new Identity(), Api(new Tokens(), new Transport()), () => Task.FromResult("es"), default);
            worker = new Thread(() => { SynchronizationContext.SetSynchronizationContext(null); operation = service.InitializeAsync(); });
            worker.Start(); worker.Join();
            service.Dispose(); main.Pump(); operation.GetAwaiter().GetResult();
            Check(service.State == PlayerSyncState.FAILED && service.Error.Category == ApiFailure.Cancelled, "Dispose before queued operation runs");
        }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
    }
}
