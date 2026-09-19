using System;
using System.Threading.Tasks;
using System.Threading;
using Domino.Identity;
using global::Firebase;
using global::Firebase.Auth;

namespace Domino.Infrastructure.Firebase
{
    internal sealed class FirebaseSdkClient : IFirebaseClient, IAuthTokenProvider
    {
        readonly Func<PlayerIdentity> expectedIdentity;
        public FirebaseSdkClient(Func<PlayerIdentity> expectedIdentity = null) { this.expectedIdentity = expectedIdentity; }
        public Task<string> GetIdTokenAsync(bool forceRefresh, CancellationToken cancellationToken) =>
            FirebaseIdTokens.GetAsync(GetCurrentUser, refresh => Auth.CurrentUser.TokenAsync(refresh),
                expectedIdentity, forceRefresh, cancellationToken);
        FirebaseApp app;
        FirebaseAuth auth;
        public async Task<string> CheckDependenciesAsync()
        {
            if (Domino.Infrastructure.ValidationNetworkPolicy.Isolated) return "Unavailable";
            Domino.Infrastructure.ValidationNetworkPolicy.RequireNetwork();

            return (await FirebaseApp.CheckAndFixDependenciesAsync()).ToString();
        }
        public void InitializeApp()
        {
            Domino.Infrastructure.ValidationNetworkPolicy.RequireNetwork();

            app = FirebaseApp.DefaultInstance;
            if (app == null) throw new InvalidOperationException("Firebase initialization failed: DefaultInstance is null.");
        }
        FirebaseAuth Auth
        {
            get
            {
                Domino.Infrastructure.ValidationNetworkPolicy.RequireNetwork();

                if (app == null) throw new InvalidOperationException("Firebase must initialize before authentication.");
                return auth ??= FirebaseAuth.DefaultInstance ?? throw new InvalidOperationException("FirebaseAuth unavailable.");
            }
        }
        public PlayerIdentity GetCurrentUser() => Snapshot(Auth.CurrentUser);
        public async Task<PlayerIdentity> SignInAnonymouslyAsync()
        {
            var result = await Auth.SignInAnonymouslyAsync();
            if (result?.User == null || Auth.CurrentUser == null) return null;
            return Snapshot(Auth.CurrentUser);
        }
        static PlayerIdentity Snapshot(FirebaseUser user) => user == null ? null : new PlayerIdentity(user.UserId, user.IsAnonymous);
    }
}
