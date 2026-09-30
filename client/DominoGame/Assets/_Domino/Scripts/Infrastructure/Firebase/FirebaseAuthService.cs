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
        readonly bool requireExplicitGuest;
        readonly object gate = new object();
        Task<PlayerIdentity> initialization;
        public IdentityState State { get; private set; }
        public PlayerIdentity Current { get; private set; }
        public Exception Error { get; private set; }
        public bool ReusedExistingUser { get; private set; }
        public FirebaseAuthService(FirebaseBootstrap bootstrap, IFirebaseClient client, Action<string> log, CancellationToken lifetime = default, bool requireExplicitGuest = false)
        {
            this.bootstrap = bootstrap ?? throw new ArgumentNullException(nameof(bootstrap));
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            this.log = log; this.lifetime = lifetime; this.requireExplicitGuest = requireExplicitGuest;
        }
        public Task<PlayerIdentity> InitializeAsync() => Begin(!requireExplicitGuest, false);
        public Task<PlayerIdentity> RestoreAsync() => Begin(false);
        public Task<PlayerIdentity> ContinueAsGuestAsync() => Begin(true);
        Task<PlayerIdentity> Begin(bool createGuest, bool explicitRetry = true)
        {
            lock (gate) {
                if (initialization != null && (!initialization.IsCompleted || (!explicitRetry && State == IdentityState.Failed))) return initialization;
                if (Current != null && State == IdentityState.Ready) return initialization ?? Task.FromResult(Current);
                Error = null;
                return initialization = InitializeCoreAsync(createGuest);
            }
        }
        public void SignOut()
        {
            lock (gate) {
                if (initialization != null && !initialization.IsCompleted) throw new InvalidOperationException("Authentication is busy.");
                if (!(client is IFirebaseSessionControl control)) throw new InvalidOperationException("Sign out unavailable.");
                control.SignOut(); Current = null; initialization = null; Error = null; State = IdentityState.NotStarted;
            }
        }
        async Task<PlayerIdentity> InitializeCoreAsync(bool createGuest)
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
                    if (!createGuest) { Current = null; State = IdentityState.NotStarted; return null; }
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
