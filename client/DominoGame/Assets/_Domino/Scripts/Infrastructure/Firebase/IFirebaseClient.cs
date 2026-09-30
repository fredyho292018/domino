using System.Threading.Tasks;
using Domino.Identity;

namespace Domino.Infrastructure.Firebase
{
    public interface IFirebaseSessionControl { void SignOut(); }
    // Minimal SDK boundary: consumers and tests do not reference Firebase types.
    public interface IFirebaseClient
    {
        Task<string> CheckDependenciesAsync();
        void InitializeApp();
        PlayerIdentity GetCurrentUser();
        Task<PlayerIdentity> SignInAnonymouslyAsync();
    }
}
