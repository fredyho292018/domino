using System;
using System.Threading;
using System.Threading.Tasks;
using Domino.Identity;
using Domino.Infrastructure;
using Domino.Infrastructure.Api;

namespace Domino.Player
{
    public sealed class PlayerService : IDisposable
    {
        readonly IPlayerIdentityService identity;
        readonly IDominoApiClient api;
        readonly Func<Task<string>> language;
        readonly CancellationToken lifetime;
        readonly CancellationTokenSource disposal = new CancellationTokenSource();
        readonly CancellationToken disposalToken;
        readonly Action<string> log;
        readonly SynchronizationContext context;
        readonly object gate = new object();
        Task operation;
        string sessionUid;
        bool disposed;
        public PlayerSyncState State { get; private set; }
        public BackendAvailability Availability { get; private set; }
        public PlayerSnapshot Player { get; private set; }
        public WalletSnapshot Wallet { get; private set; }
        public DominoApiException Error { get; private set; }
        public bool HasConfirmedSnapshots => Player != null && Wallet != null;
        public bool IsFresh => HasConfirmedSnapshots && State == PlayerSyncState.SYNCED;
        public bool CanRetry => !disposed && !lifetime.IsCancellationRequested && State == PlayerSyncState.FAILED && IsRetryable(Error);
        public event Action<PlayerSyncState> SyncStateChanged;
        public event Action<BackendAvailability> BackendAvailabilityChanged;
        public PlayerService(IPlayerIdentityService identity, IDominoApiClient api, Func<Task<string>> language, CancellationToken lifetime,
            Action<string> log = null)
        {
            this.identity = identity; this.api = api; this.language = language; this.lifetime = lifetime; this.log = log;
            context = SynchronizationContext.Current;
            disposalToken = disposal.Token;
        }
        public Task InitializeAsync(CancellationToken cancellationToken = default) => Start(false, cancellationToken);
        public Task RetryAsync(CancellationToken cancellationToken = default) => Start(false, cancellationToken);
        // Internal future-refresh seam, with no automatic trigger or public UI.
        internal Task RefreshConfirmedAsync(CancellationToken cancellationToken = default) => Start(true, cancellationToken);
        Task Start(bool refresh, CancellationToken cancellationToken)
        {
            TaskCompletionSource<bool> completion;
            bool retry;
            lock (gate)
            {
                if (operation != null && !operation.IsCompleted) return operation;
                if (disposed || lifetime.IsCancellationRequested) return Task.CompletedTask;
                if (State == PlayerSyncState.SYNCED && !refresh) return operation ?? Task.CompletedTask;
                if (State == PlayerSyncState.FAILED && !CanRetry) return operation ?? Task.CompletedTask;
                retry = State != PlayerSyncState.NOT_SYNCED;
                completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                operation = completion.Task; // Reserve before callbacks can reenter.
            }
            if (context != null && SynchronizationContext.Current != context)
                context.Post(ignored => { _ = RunAsync(completion, retry, cancellationToken); }, null);
            else _ = RunAsync(completion, retry, cancellationToken);
            return completion.Task;
        }
        async Task RunAsync(TaskCompletionSource<bool> completion, bool retry, CancellationToken caller)
        {
            string label = retry ? "retry" : "bootstrap";
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(lifetime, disposalToken, caller);
            deadline.CancelAfter(TimeSpan.FromSeconds(45));
            Error = null;
            ChangeState(PlayerSyncState.SYNCING);
            try
            {
                deadline.Token.ThrowIfCancellationRequested();
                if (!api.IsAvailable) throw new DominoApiException(ApiFailure.Configuration);
                // Only the first attempt may initialize auth; retries require the existing identity.
                var user = retry ? identity.Current : await CancellableTask.Wait(identity.InitializeAsync(), deadline.Token);
                if (user == null || identity.Current?.Uid != user.Uid || (sessionUid != null && sessionUid != user.Uid))
                    throw new DominoApiException(ApiFailure.Authentication);
                sessionUid = user.Uid;
                var code = await CancellableTask.Wait(language(), deadline.Token);
                Log("[PLAYER] " + label + " starting");
                var result = await CancellableTask.Wait(api.BootstrapAsync(code, deadline.Token), deadline.Token);
                var player = PlayerSnapshotMapper.Player(result?.player);
                var wallet = PlayerSnapshotMapper.Wallet(result?.wallet);
                if (player.Uid != user.Uid || identity.Current?.Uid != user.Uid) throw new DominoApiException(ApiFailure.Contract, 200);
                deadline.Token.ThrowIfCancellationRequested();
                Player = player; Wallet = wallet;
                ChangeAvailability(BackendAvailability.AVAILABLE);
                ChangeState(PlayerSyncState.SYNCED);
                Log("[PLAYER] " + label + " succeeded");
            }
            catch (Exception error)
            {
                Error = error as DominoApiException ?? new DominoApiException(error is OperationCanceledException
                    ? (lifetime.IsCancellationRequested || caller.IsCancellationRequested || disposed ? ApiFailure.Cancelled : ApiFailure.Timeout)
                    : ApiFailure.Authentication);
                // Failed attempts never replace or erase confirmed snapshots.
                if (Error.Category == ApiFailure.Transport || Error.Category == ApiFailure.Timeout || Error.HttpStatus >= 500)
                    ChangeAvailability(BackendAvailability.UNAVAILABLE);
                else if (Error.HttpStatus > 0) ChangeAvailability(BackendAvailability.AVAILABLE);
                else if (Error.Category != ApiFailure.Cancelled) ChangeAvailability(BackendAvailability.UNKNOWN);
                ChangeState(PlayerSyncState.FAILED);
                Log(Error.Category == ApiFailure.Configuration ? "[PLAYER] bootstrap unavailable reason=API_CONFIGURATION"
                    : "[PLAYER] " + label + " failed category=" + Error.Category);
            }
            finally { completion.TrySetResult(true); }
        }
        static bool IsRetryable(DominoApiException error) => error != null &&
            (error.Category == ApiFailure.Transport || error.Category == ApiFailure.Timeout || error.Category == ApiFailure.Cancelled ||
             (error.Category == ApiFailure.Server && (error.HttpStatus == 500 || error.HttpStatus == 502 || error.HttpStatus == 503 || error.HttpStatus == 504)));
        void ChangeState(PlayerSyncState state)
        {
            if (State == state) return;
            State = state; Notify(SyncStateChanged, state);
        }
        void ChangeAvailability(BackendAvailability state)
        {
            if (Availability == state) return;
            Availability = state; Notify(BackendAvailabilityChanged, state);
        }
        void Notify<T>(Action<T> subscribers, T value)
        {
            if (disposed || subscribers == null) return;
            foreach (Action<T> subscriber in subscribers.GetInvocationList())
                try { subscriber(value); } catch { Log("[PLAYER] state subscriber failed"); }
        }
        void Log(string message) { try { if (!disposed) log?.Invoke(message); } catch { } }
        public void Dispose()
        {
            lock (gate)
            {
                if (disposed) return;
                disposed = true; SyncStateChanged = null; BackendAvailabilityChanged = null;
                disposal.Cancel();
                disposal.Dispose();
            }
        }
    }
}
