using System;
using System.Threading;
using System.Threading.Tasks;
using Domino.Infrastructure.Firebase;
using Domino.Player;

namespace Domino.Identity
{
    public enum ProductionAuthRoute { Loading, Welcome, AppShell, Error }
    public interface IPostAuthenticationPolicy { ProductionAuthRoute Destination(PlayerSnapshot player); }
    // Temporary migration policy. No onboarding status is fabricated or stored.
    public sealed class TemporaryAppShellPolicy : IPostAuthenticationPolicy
    { public ProductionAuthRoute Destination(PlayerSnapshot player) => ProductionAuthRoute.AppShell; }

    public sealed class ProductionAuthRouter : IDisposable
    {
        readonly FirebaseAuthService identity;
        readonly PlayerService player;
        readonly IPostAuthenticationPolicy policy;
        readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        Task operation;
        bool disposed;
        public ProductionAuthRoute Route { get; private set; } = ProductionAuthRoute.Loading;
        public bool Busy { get; private set; }
        public string Message { get; private set; } = "";
        public event Action Changed;
        public ProductionAuthRouter(FirebaseAuthService identity, PlayerService player, IPostAuthenticationPolicy policy = null)
        { this.identity=identity; this.player=player; this.policy=policy??new TemporaryAppShellPolicy(); }
        public Task RestoreAsync() => Begin(false);
        public Task ContinueAsGuestAsync() => Begin(true);
        Task Begin(bool guest)
        {
            if(disposed || Route==ProductionAuthRoute.AppShell)return Task.CompletedTask;
            if(Busy)return operation??Task.CompletedTask;
            Busy=true;Route=ProductionAuthRoute.Loading;Message="Connecting...";
            // Reserve Busy before notifying the view; a second tap never starts another operation.
            var completion=new TaskCompletionSource<bool>();operation=completion.Task;
            Notify();_ = Run(guest,completion);return operation;
        }
        async Task Run(bool guest,TaskCompletionSource<bool> completion)
        {
            try {
                var user=await (guest?identity.ContinueAsGuestAsync():identity.RestoreAsync());
                if(disposed)return;
                if(user==null){Route=ProductionAuthRoute.Welcome;Message="";return;}
                await (player.State==PlayerSyncState.FAILED?player.RetryBootstrapFromAuthAsync(lifetime.Token):player.InitializeAsync(lifetime.Token));
                if(disposed)return;
                if(player.State!=PlayerSyncState.SYNCED || !player.HasConfirmedSnapshots ||
                    player.Player.Uid!=user.Uid || identity.Current?.Uid!=user.Uid)
                    throw new InvalidOperationException("Player unavailable.");
                Route=policy.Destination(player.Player);Message="";
            } catch { if(!disposed){Route=ProductionAuthRoute.Error;Message="We could not connect. Check your connection and try again.";} }
            finally { if(!disposed){Busy=false;Notify();} completion.TrySetResult(true); }
        }
        void Notify(){Changed?.Invoke();}
        public async Task StopAsync(){Dispose();if(operation!=null)await operation;}
        public void Dispose(){if(disposed)return;disposed=true;lifetime.Cancel();Changed=null;}
    }
}
