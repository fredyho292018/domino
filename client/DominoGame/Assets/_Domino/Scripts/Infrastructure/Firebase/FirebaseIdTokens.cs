using System;
using System.Threading;
using System.Threading.Tasks;
using Domino.Identity;

namespace Domino.Infrastructure.Firebase
{
    // Testable SDK boundary. Tokens live only for the duration of the awaiting request.
    internal static class FirebaseIdTokens
    {
        internal static async Task<string> GetAsync(Func<PlayerIdentity> currentUser,
            Func<bool, Task<string>> tokenAsync, Func<PlayerIdentity> expectedUser,
            bool forceRefresh, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var user = currentUser();
                if (user == null || (expectedUser?.Invoke() is PlayerIdentity expected && expected.Uid != user.Uid))
                    throw new InvalidOperationException();
                var token = await CancellableTask.Wait(tokenAsync(forceRefresh), cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(token) || currentUser()?.Uid != user.Uid ||
                    (expectedUser?.Invoke() is PlayerIdentity latest && latest.Uid != user.Uid))
                    throw new InvalidOperationException();
                return token;
            }
            catch (OperationCanceledException) { throw; }
            catch { throw new InvalidOperationException("Firebase ID token is unavailable."); }
        }
    }
}
