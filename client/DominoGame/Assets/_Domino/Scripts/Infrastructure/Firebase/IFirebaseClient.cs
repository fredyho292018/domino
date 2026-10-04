using System.Threading.Tasks;
using Domino.Identity;

namespace Domino.Infrastructure.Firebase
{
    public interface IFirebaseCredentialLinkClient
    {
        Task<FirebaseAuthSessionSnapshot> LinkCurrentUserAsync(string expectedUid,string email,string password);
        Task<FirebaseAuthSessionSnapshot> ReloadLinkSessionAsync(string expectedUid);
    }
    public interface IFirebaseEmailAccessClient
    {
        Task<FirebaseAuthSessionSnapshot> SignInEmailAsync(string email,string password);
        Task SendPasswordResetAsync(string email);
    }
    public interface IFirebaseEmailSessionClient
    {
        FirebaseAuthSessionSnapshot GetSession();
        Task<FirebaseAuthSessionSnapshot> CreateEmailAsync(string email,string password);
        Task ReloadEmailAsync(string expectedUid);
        Task SendVerificationAsync(string expectedUid);
        void SignOutUnverifiedEmail(string expectedUid);
    }
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
