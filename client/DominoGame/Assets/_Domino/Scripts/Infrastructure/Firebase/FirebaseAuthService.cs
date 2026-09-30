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
        Task emailOperation;
        string createdEmailUid;
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
                if(emailOperation!=null&&!emailOperation.IsCompleted)throw new EmailAuthException(EmailAuthError.SessionConflict);
                if (initialization != null && (!initialization.IsCompleted || (!explicitRetry && State == IdentityState.Failed))) return initialization;
                if (Current != null && State == IdentityState.Ready) return initialization ?? Task.FromResult(Current);
                Error = null;
                return initialization = InitializeCoreAsync(createGuest);
            }
        }
        public void SignOut()
        {
            lock (gate) {
                if(emailOperation!=null&&!emailOperation.IsCompleted)throw new EmailAuthException(EmailAuthError.SessionConflict);
                if (initialization != null && !initialization.IsCompleted) throw new InvalidOperationException("Authentication is busy.");
                if (!(client is IFirebaseSessionControl control)) throw new InvalidOperationException("Sign out unavailable.");
                control.SignOut(); createdEmailUid=null; Current = null; initialization = null; Error = null; State = IdentityState.NotStarted;
            }
        }
        public FirebaseAuthSessionSnapshot Session => (client as IFirebaseEmailSessionClient)?.GetSession();
        public bool SupportsEmail => client is IFirebaseEmailSessionClient;
        public bool CanCancelCreatedEmail => Session is FirebaseAuthSessionSnapshot session && session.Uid==createdEmailUid && !session.IsAnonymous && session.IsPasswordProvider && !session.IsEmailVerified;
        IFirebaseEmailSessionClient EmailClient => client as IFirebaseEmailSessionClient ?? throw new EmailAuthException(EmailAuthError.Unknown);
        void Adopt(FirebaseAuthSessionSnapshot session)
        {
            lifetime.ThrowIfCancellationRequested();
            if(session==null)throw new EmailAuthException(EmailAuthError.SessionConflict);
            Current=new PlayerIdentity(session.Uid,session.IsAnonymous);State=IdentityState.Ready;Error=null;initialization=Task.FromResult(Current);
        }
        public Task RegisterEmailAsync(string email,string password,string confirmation)
        {
            return RunEmail(async()=>{
                await bootstrap.InitializeAsync();lifetime.ThrowIfCancellationRequested();
                var current=client.GetCurrentUser();
                if(current!=null)throw new EmailAuthException(current.IsAnonymous?EmailAuthError.GuestUpgradeRequired:EmailAuthError.SessionConflict);
                var error=EmailAuthRules.Validate(email,password,confirmation);if(error!=EmailAuthError.None)throw new EmailAuthException(error);
                var session=await EmailClient.CreateEmailAsync(EmailAuthRules.Normalize(email),password);
                lifetime.ThrowIfCancellationRequested();
                var latest=EmailClient.GetSession();
                if(latest==null||latest.Uid!=session.Uid||latest.IsAnonymous||!latest.IsPasswordProvider)throw new EmailAuthException(EmailAuthError.SessionConflict);
                createdEmailUid=session.Uid;Adopt(latest);
            });
        }
        public Task SignInEmailAsync(string email,string password)=>RunEmail(async()=>{
            var error=EmailAuthRules.ValidateSignIn(email,password);if(error!=EmailAuthError.None)throw new EmailAuthException(error);
            await bootstrap.InitializeAsync();lifetime.ThrowIfCancellationRequested();RequireNoExistingSession();
            if(!(client is IFirebaseEmailAccessClient access))throw new EmailAuthException(EmailAuthError.Unknown);
            var session=await access.SignInEmailAsync(EmailAuthRules.Normalize(email),password);lifetime.ThrowIfCancellationRequested();
            var latest=EmailClient.GetSession();
            if(session==null||latest==null||session.Uid!=latest.Uid||latest.IsAnonymous||!latest.IsPasswordProvider)throw new EmailAuthException(EmailAuthError.SessionConflict);
            createdEmailUid=null;Adopt(latest);
        });
        public Task SendPasswordResetAsync(string email)=>RunEmail(async()=>{
            var error=EmailAuthRules.ValidateEmail(email);if(error!=EmailAuthError.None)throw new EmailAuthException(error);
            await bootstrap.InitializeAsync();lifetime.ThrowIfCancellationRequested();RequireNoExistingSession();
            if(!(client is IFirebaseEmailAccessClient access))throw new EmailAuthException(EmailAuthError.Unknown);
            await access.SendPasswordResetAsync(EmailAuthRules.Normalize(email));lifetime.ThrowIfCancellationRequested();RequireNoExistingSession();
        });
        void RequireNoExistingSession()
        {
            var current=client.GetCurrentUser();
            if(current!=null)throw new EmailAuthException(current.IsAnonymous?EmailAuthError.GuestUpgradeRequired:EmailAuthError.SessionConflict);
        }
        public Task ReloadEmailAsync()=>RunEmail(async()=>{
            var before=RequireEmailSession();await EmailClient.ReloadEmailAsync(before.Uid);lifetime.ThrowIfCancellationRequested();
            var after=RequireEmailSession();if(after.Uid!=before.Uid)throw new EmailAuthException(EmailAuthError.SessionConflict);Adopt(after);
        });
        public Task SendVerificationAsync()=>RunEmail(async()=>{
            var before=RequireEmailSession();await EmailClient.SendVerificationAsync(before.Uid);lifetime.ThrowIfCancellationRequested();
            if(RequireEmailSession().Uid!=before.Uid)throw new EmailAuthException(EmailAuthError.SessionConflict);
        });
        public Task RefreshVerifiedTokenAsync()=>RunEmail(async()=>{
            var before=RequireEmailSession();if(!before.IsEmailVerified)throw new EmailAuthException(EmailAuthError.SessionConflict);
            if(!(client is IAuthTokenProvider tokens))throw new EmailAuthException(EmailAuthError.Unknown);
            var token=await tokens.GetIdTokenAsync(true,lifetime);lifetime.ThrowIfCancellationRequested();
            if(string.IsNullOrWhiteSpace(token)||RequireEmailSession().Uid!=before.Uid||!RequireEmailSession().IsEmailVerified)throw new EmailAuthException(EmailAuthError.SessionConflict);
        });
        FirebaseAuthSessionSnapshot RequireEmailSession()
        {
            var session=EmailClient.GetSession();
            if(session==null||session.IsAnonymous||!session.IsPasswordProvider||Current?.Uid!=session.Uid)throw new EmailAuthException(EmailAuthError.SessionConflict);
            return session;
        }
        public Task CancelCreatedEmailAsync(bool confirmed)=>RunEmail(()=>{
            if(!confirmed||!CanCancelCreatedEmail)throw new EmailAuthException(EmailAuthError.SessionConflict);
            var session=RequireEmailSession();EmailClient.SignOutUnverifiedEmail(session.Uid);
            Current=null;State=IdentityState.NotStarted;Error=null;initialization=null;createdEmailUid=null;return Task.CompletedTask;
        });
        Task RunEmail(Func<Task> action)
        {
            lock(gate){
                if(emailOperation!=null&&!emailOperation.IsCompleted)return emailOperation;
                if(initialization!=null&&!initialization.IsCompleted)throw new EmailAuthException(EmailAuthError.SessionConflict);
                var done=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);emailOperation=done.Task;
                _=CompleteEmail(action,done);return done.Task;
            }
        }
        async Task CompleteEmail(Func<Task> action,TaskCompletionSource<bool> done)
        {try{lifetime.ThrowIfCancellationRequested();await action();done.TrySetResult(true);}catch(Exception error){done.TrySetException(error is EmailAuthException||error is OperationCanceledException?error:new EmailAuthException(EmailAuthError.Unknown));}}
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
