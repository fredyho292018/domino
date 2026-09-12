using System;
using System.Threading;
using System.Threading.Tasks;
using Domino.Identity;
using Domino.Infrastructure;
using Domino.Infrastructure.Api;

namespace Domino.Player
{
    public sealed class PlayerService
    {
        readonly IPlayerIdentityService identity;
        readonly IDominoApiClient api;
        readonly Func<Task<string>> language;
        readonly CancellationToken lifetime;
        readonly Action<string> log;
        readonly object gate = new object();
        Task initialization;
        public PlayerSyncState State { get; private set; }
        public PlayerSnapshot Player { get; private set; }
        public WalletSnapshot Wallet { get; private set; }
        public DominoApiException Error { get; private set; }
        public PlayerService(IPlayerIdentityService identity, IDominoApiClient api, Func<Task<string>> language, CancellationToken lifetime,
            Action<string> log = null)
        { this.identity = identity; this.api = api; this.language = language; this.lifetime = lifetime; this.log = log; }
        public Task InitializeAsync()
        {
            lock (gate)
            {
                if (initialization != null && (!initialization.IsCompleted || State == PlayerSyncState.SYNCED)) return initialization;
                return initialization = InitializeCoreAsync();
            }
        }
        async Task InitializeCoreAsync()
        {
            State = PlayerSyncState.SYNCING; Error = null; Player = null; Wallet = null;
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(lifetime);
            deadline.CancelAfter(TimeSpan.FromSeconds(45));
            try
            {
                deadline.Token.ThrowIfCancellationRequested();
                if (!api.IsAvailable) throw new DominoApiException(ApiFailure.Configuration);
                var user = await CancellableTask.Wait(identity.InitializeAsync(), deadline.Token);
                if (user == null || identity.Current?.Uid != user.Uid) throw new DominoApiException(ApiFailure.Authentication);
                var code = await CancellableTask.Wait(language(), deadline.Token);
                Log("[PLAYER] bootstrap starting");
                var result = await CancellableTask.Wait(api.BootstrapAsync(code, deadline.Token), deadline.Token);
                var player = PlayerSnapshotMapper.Player(result?.player);
                var wallet = PlayerSnapshotMapper.Wallet(result?.wallet);
                if (player.Uid != user.Uid || identity.Current?.Uid != user.Uid) throw new DominoApiException(ApiFailure.Contract);
                deadline.Token.ThrowIfCancellationRequested();
                Player = player; Wallet = wallet; State = PlayerSyncState.SYNCED;
                Log("[PLAYER] bootstrap succeeded");
            }
            catch (Exception error)
            {
                Player = null; Wallet = null;
                Error = error as DominoApiException ?? new DominoApiException(error is OperationCanceledException
                    ? (lifetime.IsCancellationRequested ? ApiFailure.Cancelled : ApiFailure.Timeout) : ApiFailure.Authentication);
                State = PlayerSyncState.FAILED;
                Log(Error.Category == ApiFailure.Configuration
                    ? "[PLAYER] bootstrap unavailable reason=API_CONFIGURATION"
                    : "[PLAYER] bootstrap failed category=" + Error.Category);
            }
        }
        void Log(string message)
        {
            try { log?.Invoke(message); }
            catch { /* Diagnostics must not change synchronization or offline behavior. */ }
        }
    }
}
