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
        public event Action<PlayerSnapshot> SnapshotChanged;
        public bool IsSaving { get; private set; }
        public bool CanEdit => !disposed && !lifetime.IsCancellationRequested && HasConfirmedSnapshots &&
            State == PlayerSyncState.SYNCED && identity.Current?.Uid == Player.Uid;
        public Task UpdateDisplayNameAsync(string value, CancellationToken cancellationToken = default) => Start(false, cancellationToken, value ?? string.Empty);
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
        Task Start(bool refresh, CancellationToken cancellationToken, string displayName = null)
        {
            TaskCompletionSource<bool> completion;
            bool retry;
            lock (gate)
            {
                if (operation != null && !operation.IsCompleted) return operation;
                if (disposed || lifetime.IsCancellationRequested) return Task.CompletedTask;
                if (displayName == null && State == PlayerSyncState.SYNCED && !refresh) return operation ?? Task.CompletedTask;
                if (displayName != null && !CanEdit) return Task.CompletedTask;
                if (State == PlayerSyncState.FAILED && !CanRetry) return operation ?? Task.CompletedTask;
                retry = State != PlayerSyncState.NOT_SYNCED;
                completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                operation = completion.Task; // Reserve before callbacks can reenter.
            }
            if (context != null && SynchronizationContext.Current != context)
                context.Post(ignored => { _ = RunAsync(completion, retry, cancellationToken, displayName); }, null);
            else _ = RunAsync(completion, retry, cancellationToken, displayName);
            return completion.Task;
        }
        async Task RunAsync(TaskCompletionSource<bool> completion, bool retry, CancellationToken caller, string displayName)
        {
            string label = displayName != null ? "alias" : retry ? "retry" : "bootstrap";
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(lifetime, disposalToken, caller);
            deadline.CancelAfter(TimeSpan.FromSeconds(45));
            Error = null;
            IsSaving = displayName != null;
            ChangeState(PlayerSyncState.SYNCING);
            try
            {
                deadline.Token.ThrowIfCancellationRequested();
                if (!api.IsAvailable) throw new DominoApiException(ApiFailure.Configuration);
                if (IsSaving && !DisplayNameRules.IsValid(displayName)) throw new DominoApiException(ApiFailure.Server, 400, "DISPLAY_NAME_INVALID");
                // Only the first attempt may initialize auth; retries require the existing identity.
                var user = retry ? identity.Current : await CancellableTask.Wait(identity.InitializeAsync(), deadline.Token);
                if (user == null || identity.Current?.Uid != user.Uid || (sessionUid != null && sessionUid != user.Uid))
                    throw new DominoApiException(ApiFailure.Authentication);
                sessionUid = user.Uid;
                Log("[PLAYER] " + label + " starting");
                var result = IsSaving
                    ? await CancellableTask.Wait(api.UpdateDisplayNameAsync(displayName, deadline.Token), deadline.Token)
                    : await CancellableTask.Wait(api.BootstrapAsync(await CancellableTask.Wait(language(), deadline.Token), deadline.Token), deadline.Token);
                var player = PlayerSnapshotMapper.Player(result?.player);
                var wallet = PlayerSnapshotMapper.Wallet(result?.wallet);
                if (player.Uid != user.Uid || identity.Current?.Uid != user.Uid) throw new DominoApiException(ApiFailure.Contract, 200);
                deadline.Token.ThrowIfCancellationRequested();
                if (IsSaving && (player.DisplayName != displayName || wallet.Coins != Wallet.Coins ||
                    player.AccountType != Player.AccountType || player.Language != Player.Language || player.Status != Player.Status))
                    throw new DominoApiException(ApiFailure.Contract, 200);
                Player = player;
                if (!IsSaving) Wallet = wallet;
                IsSaving = false;
                Notify(SnapshotChanged, Player);
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
                bool rejectedAlias = IsSaving && Error.HttpStatus == 400;
                IsSaving = false;
                ChangeState(rejectedAlias ? PlayerSyncState.SYNCED : PlayerSyncState.FAILED);
                Log(Error.Category == ApiFailure.Configuration ? "[PLAYER] bootstrap unavailable reason=API_CONFIGURATION"
                    : "[PLAYER] " + label + " failed category=" + Error.Category);
            }
            finally { completion.TrySetResult(true); }
        }
        // Share the existing operation gate with bootstrap/alias: a late response cannot
        // overwrite a newer reward balance. The callback must return the backend balance.
        internal Task<bool> ApplyConfirmedRewardWalletAsync(Func<CancellationToken, Task<long>> loadWallet, CancellationToken caller)
        {
            TaskCompletionSource<bool> completion;
            lock (gate)
            {
                if (disposed || lifetime.IsCancellationRequested || !HasConfirmedSnapshots ||
                    identity.Current?.Uid != Player.Uid || (operation != null && !operation.IsCompleted))
                    return Task.FromResult(false);
                completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                operation = completion.Task;
            }
            if (context != null && SynchronizationContext.Current != context)
                context.Post(_ => { _ = ReceiveRewardWallet(completion, loadWallet, caller); }, null);
            else _ = ReceiveRewardWallet(completion, loadWallet, caller);
            return completion.Task;
        }
        async Task ReceiveRewardWallet(TaskCompletionSource<bool> completion, Func<CancellationToken, Task<long>> loadWallet,
            CancellationToken caller)
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(lifetime, disposalToken, caller);
            deadline.CancelAfter(TimeSpan.FromSeconds(45));
            var uid = identity.Current?.Uid;
            Error = null; ChangeState(PlayerSyncState.SYNCING);
            try
            {
                deadline.Token.ThrowIfCancellationRequested();
                var coins = await CancellableTask.Wait(loadWallet(deadline.Token), deadline.Token);
                deadline.Token.ThrowIfCancellationRequested();
                if (uid == null || uid != identity.Current?.Uid || Player?.Uid != uid)
                    throw new DominoApiException(ApiFailure.Authentication);
                Wallet = new WalletSnapshot(coins);
                Notify(SnapshotChanged, Player);
                ChangeAvailability(BackendAvailability.AVAILABLE);
                ChangeState(PlayerSyncState.SYNCED);
                completion.TrySetResult(true);
            }
            catch (Exception error)
            {
                Error = error as DominoApiException ?? new DominoApiException(error is OperationCanceledException
                    ? ApiFailure.Cancelled : ApiFailure.Contract);
                if (Error.Category == ApiFailure.Transport || Error.Category == ApiFailure.Timeout || Error.HttpStatus >= 500)
                    ChangeAvailability(BackendAvailability.UNAVAILABLE);
                ChangeState(PlayerSyncState.FAILED);
                Log("[ECONOMY] reward consume failed category=" + Error.Category);
                completion.TrySetResult(false);
            }
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
                disposed = true; SyncStateChanged = null; BackendAvailabilityChanged = null; SnapshotChanged = null;
                disposal.Cancel();
                disposal.Dispose();
            }
        }
    }
}
