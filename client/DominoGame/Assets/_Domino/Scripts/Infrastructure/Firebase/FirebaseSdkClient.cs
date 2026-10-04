using System;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using Domino.Identity;
using global::Firebase;
using global::Firebase.Auth;

namespace Domino.Infrastructure.Firebase
{
    internal sealed class FirebaseSdkClient : IFirebaseClient, IAuthTokenProvider, IFirebaseSessionControl, IFirebaseEmailSessionClient, IFirebaseEmailAccessClient, IFirebaseCredentialLinkClient
    {
        readonly Func<PlayerIdentity> expectedIdentity;
        public FirebaseSdkClient(Func<PlayerIdentity> expectedIdentity = null) { this.expectedIdentity = expectedIdentity; }
        public Task<string> GetIdTokenAsync(bool forceRefresh, CancellationToken cancellationToken) =>
            FirebaseIdTokens.GetAsync(GetCurrentUser, refresh => Auth.CurrentUser.TokenAsync(refresh),
                expectedIdentity, forceRefresh, cancellationToken);
        FirebaseApp app;
        FirebaseAuth auth;
        bool signedOut;
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

                if (signedOut || app == null) throw new InvalidOperationException("Authentication session unavailable.");
                return auth ??= FirebaseAuth.DefaultInstance ?? throw new InvalidOperationException("FirebaseAuth unavailable.");
            }
        }
        public void SignOut() { Auth.SignOut(); signedOut=true; }
        public FirebaseAuthSessionSnapshot GetSession()
        {
            var user=Auth.CurrentUser;if(user==null)return null;
            return new FirebaseAuthSessionSnapshot(user.UserId,user.IsAnonymous,user.IsEmailVerified,
                user.ProviderData.Any(p=>p.ProviderId==EmailAuthProvider.ProviderId),user.Email);
        }
        static EmailAuthException SafeEmailError(Exception error)
        {
            var root=error is AggregateException aggregate?aggregate.Flatten().InnerExceptions.FirstOrDefault():error;
            return root as EmailAuthException ?? new EmailAuthException(root is FirebaseException firebase?EmailAuthRules.FirebaseCode(firebase.ErrorCode):EmailAuthError.Unknown);
        }
        public async Task<FirebaseAuthSessionSnapshot> CreateEmailAsync(string email,string password)
        {
            var existing=Auth.CurrentUser;
            if(existing!=null)throw new EmailAuthException(existing.IsAnonymous?EmailAuthError.GuestUpgradeRequired:EmailAuthError.SessionConflict);
            try {
                var result=await Auth.CreateUserWithEmailAndPasswordAsync(email,password);
                var session=GetSession();
                if(session==null||result?.User==null||result.User.UserId!=session.Uid||session.IsAnonymous||!session.IsPasswordProvider)
                    throw new EmailAuthException(EmailAuthError.SessionConflict);
                return session;
            }catch(Exception error){throw SafeEmailError(error);}
        }
        FirebaseUser RequireEmail(string expectedUid)
        {
            var user=Auth.CurrentUser;var session=GetSession();
            if(user==null||session.Uid!=expectedUid||session.IsAnonymous||!session.IsPasswordProvider)throw new EmailAuthException(EmailAuthError.SessionConflict);
            return user;
        }
        public async Task<FirebaseAuthSessionSnapshot> LinkCurrentUserAsync(string expectedUid,string email,string password)
        {
            var user=Auth.CurrentUser;
            if(user==null||user.UserId!=expectedUid||!user.IsAnonymous)throw new EmailAuthException(EmailAuthError.SessionConflict);
            try {
                using(var credential=EmailAuthProvider.GetCredential(email,password)) {
                    var result=await user.LinkWithCredentialAsync(credential);
                    var current=GetSession();
                    if(result?.User?.UserId!=expectedUid||current?.Uid!=expectedUid||current.IsAnonymous||!current.IsPasswordProvider)
                        throw new EmailAuthException(EmailAuthError.SessionConflict);
                    return current;
                }
            } catch(Exception error) {
                if(ErrorCode(error)==AuthError.CredentialAlreadyInUse||ErrorCode(error)==AuthError.AccountExistsWithDifferentCredentials)
                    throw new EmailAuthException(EmailAuthError.EmailAlreadyInUse);
                throw SafeEmailError(error);
            }
        }
        public async Task<FirebaseAuthSessionSnapshot> ReloadLinkSessionAsync(string expectedUid)
        {
            var user=Auth.CurrentUser;
            if(user==null||user.UserId!=expectedUid)throw new EmailAuthException(EmailAuthError.SessionConflict);
            try{await user.ReloadAsync();var current=GetSession();if(current?.Uid!=expectedUid)throw new EmailAuthException(EmailAuthError.SessionConflict);return current;}
            catch(Exception error){throw SafeEmailError(error);}
        }
        static AuthError? ErrorCode(Exception error)
        {
            var root=error is AggregateException aggregate?aggregate.Flatten().InnerExceptions.FirstOrDefault():error;
            return root is FirebaseException firebase?(AuthError?)firebase.ErrorCode:null;
        }
        public async Task<FirebaseAuthSessionSnapshot> SignInEmailAsync(string email,string password)
        {
            var existing=Auth.CurrentUser;
            if(existing!=null)throw new EmailAuthException(existing.IsAnonymous?EmailAuthError.GuestUpgradeRequired:EmailAuthError.SessionConflict);
            try{
                var result=await Auth.SignInWithEmailAndPasswordAsync(email,password);
                var session=GetSession();
                if(session==null||result?.User==null||result.User.UserId!=session.Uid||session.IsAnonymous||!session.IsPasswordProvider)throw new EmailAuthException(EmailAuthError.SessionConflict);
                return session;
            }catch(Exception error){
                var code=ErrorCode(error);
                if(code==AuthError.InvalidCredential||code==AuthError.WrongPassword||code==AuthError.UserNotFound)throw new EmailAuthException(EmailAuthError.InvalidCredential);
                if(code==AuthError.UserDisabled)throw new EmailAuthException(EmailAuthError.UserDisabled);
                throw SafeEmailError(error);
            }
        }
        public async Task SendPasswordResetAsync(string email)
        {
            if(Auth.CurrentUser!=null)throw new EmailAuthException(EmailAuthError.SessionConflict);
            try{await Auth.SendPasswordResetEmailAsync(email);}
            catch(Exception error){
                // Preserve the same privacy response whether or not Firebase found an account.
                if(ErrorCode(error)==AuthError.UserNotFound)return;
                throw SafeEmailError(error);
            }
        }
        public async Task ReloadEmailAsync(string expectedUid)
        {try{await RequireEmail(expectedUid).ReloadAsync();RequireEmail(expectedUid);}catch(Exception error){throw SafeEmailError(error);}}
        public async Task SendVerificationAsync(string expectedUid)
        {try{await RequireEmail(expectedUid).SendEmailVerificationAsync();RequireEmail(expectedUid);}catch(Exception error){throw SafeEmailError(error);}}
        public void SignOutUnverifiedEmail(string expectedUid)
        {
            var user=RequireEmail(expectedUid);
            if(user.IsEmailVerified)throw new EmailAuthException(EmailAuthError.SessionConflict);
            Auth.SignOut();
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
