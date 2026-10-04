using System;
using System.Threading.Tasks;

namespace Domino.Identity
{
    public enum AuthRecoverability { NoSession, AnonymousUnlinked, Recoverable, Unknown }
    public enum AccountProtectionState { Closed, Warning, ProtectEntry, Unavailable }

    public static class AuthRecoverabilityClassifier
    {
        public static AuthRecoverability Classify(FirebaseAuthSessionSnapshot session)
        {
            if(session==null)return AuthRecoverability.NoSession;
            if(string.IsNullOrWhiteSpace(session.Uid))return AuthRecoverability.Unknown;
            if(session.IsAnonymous)return session.IsPasswordProvider?AuthRecoverability.Unknown:AuthRecoverability.AnonymousUnlinked;
            // Only password recovery is implemented by the production application today.
            return session.IsPasswordProvider?AuthRecoverability.Recoverable:AuthRecoverability.Unknown;
        }
    }

    // Decision/navigation only. All teardown delegates to the existing logout transaction.
    // No Player, alias, credential or backend operations belong to this controller.
    public sealed class AccountProtectionController : IDisposable
    {
        readonly Func<FirebaseAuthSessionSnapshot> session;
        readonly Func<object> epoch;
        readonly ProductionLogoutService logout;
        object owner; string uid; bool disposed;
        public AccountProtectionState State { get; private set; }
        public event Action Changed;
        public AccountProtectionController(Func<FirebaseAuthSessionSnapshot> session,Func<object> epoch,ProductionLogoutService logout)
        {this.session=session;this.epoch=epoch;this.logout=logout;}
        public Task RequestAsync()
        {
            if(disposed||logout.State==LogoutState.LoggingOut)return Task.CompletedTask;
            if(State!=AccountProtectionState.Closed){ValidateOwner();return Task.CompletedTask;}
            FirebaseAuthSessionSnapshot current;
            try{current=session();}catch{Set(AccountProtectionState.Unavailable);return Task.CompletedTask;}
            var classification=AuthRecoverabilityClassifier.Classify(current);
            if(classification==AuthRecoverability.NoSession)return Task.CompletedTask;
            owner=epoch();uid=current.Uid;
            if(owner==null||classification==AuthRecoverability.Unknown||owner is PlayerIdentity identity&&(identity.Uid!=uid||identity.IsAnonymous!=current.IsAnonymous)){Set(AccountProtectionState.Unavailable);return Task.CompletedTask;}
            if(classification==AuthRecoverability.AnonymousUnlinked){Set(AccountProtectionState.Warning);return Task.CompletedTask;}
            return NormalLogoutAsync();
        }
        bool ValidateOwner()
        {
            bool valid=false;
            try{valid=!disposed&&owner!=null&&ReferenceEquals(owner,epoch())&&session()?.Uid==uid;}catch{}
            if(!valid)Close();return valid;
        }
        bool AnonymousOwner()
        {
            if(!ValidateOwner())return false;
            try{if(AuthRecoverabilityClassifier.Classify(session())==AuthRecoverability.AnonymousUnlinked)return true;}catch{}
            Close();return false;
        }
        public void Protect()
        {if(State==AccountProtectionState.Warning&&AnonymousOwner())Set(AccountProtectionState.ProtectEntry);}
        public Task ForceLogoutAsync()
        {
            if(State!=AccountProtectionState.Warning||!AnonymousOwner())return Task.CompletedTask;
            return NormalLogoutAsync();
        }
        Task NormalLogoutAsync()
        {
            if(!ValidateOwner())return Task.CompletedTask;
            var expectedOwner=owner;var expectedUid=uid;
            Close();logout.Request();return logout.ConfirmAsync(()=>!disposed&&ReferenceEquals(expectedOwner,epoch())&&session()?.Uid==expectedUid);
        }
        public void Cancel(){if(!disposed)Close();}
        void Close(){owner=null;uid=null;Set(AccountProtectionState.Closed);}
        void Set(AccountProtectionState value){if(State==value)return;State=value;Changed?.Invoke();}
        public void Dispose(){disposed=true;owner=null;uid=null;State=AccountProtectionState.Closed;Changed=null;}
    }
}
