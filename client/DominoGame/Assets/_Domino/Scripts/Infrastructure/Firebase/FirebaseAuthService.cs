using System;
using System.Threading;
using System.Threading.Tasks;
using Domino.Identity;

namespace Domino.Infrastructure.Firebase
{
    public sealed class FirebaseAuthService : IPlayerIdentityService
    {
        readonly FirebaseBootstrap bootstrap;
        readonly IFirebaseClient client;
        readonly Action<string> log;
        readonly CancellationToken lifetime;
        readonly object gate = new object();
        Task<PlayerIdentity> initialization;
        public IdentityState State { get; private set; }
        public PlayerIdentity Current { get; private set; }
        public Exception Error { get; private set; }
        public bool ReusedExistingUser { get; private set; }
        public FirebaseAuthService(FirebaseBootstrap bootstrap, IFirebaseClient client, Action<string> log, CancellationToken lifetime = default)
        {
            this.bootstrap = bootstrap ?? throw new ArgumentNullException(nameof(bootstrap));
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            this.log = log; this.lifetime = lifetime;
        }
        public Task<PlayerIdentity> InitializeAsync()
        {
            lock (gate) return initialization ??= InitializeCoreAsync();
        }
        async Task<PlayerIdentity> InitializeCoreAsync()
        {
            State = IdentityState.Initializing;
            try
            {
                await bootstrap.InitializeAsync();
                lifetime.ThrowIfCancellationRequested();
                State = IdentityState.Authenticating;
                var identity = client.GetCurrentUser();
                ReusedExistingUser = identity != null;
                if (ReusedExistingUser) log?.Invoke("[AUTH] Existing user reused");
                else
                {
                    log?.Invoke("[AUTH] No existing user");
                    try { identity = await client.SignInAnonymouslyAsync(); }
                    catch (Exception error) { throw new InvalidOperationException("Anonymous sign-in failed.", error); }
                    lifetime.ThrowIfCancellationRequested();
                    if (identity == null) throw new InvalidOperationException("Firebase user returned null after anonymous sign-in.");
                    log?.Invoke("[AUTH] Anonymous sign-in successful");
                }
                Current = identity; State = IdentityState.Ready;
                log?.Invoke("[AUTH] Identity ready");
                log?.Invoke("[AUTH] IsAnonymous=" + identity.IsAnonymous.ToString().ToLowerInvariant());
                return identity;
            }
            catch (Exception error) { Error = error; State = IdentityState.Failed; throw; }
        }
    }
}
