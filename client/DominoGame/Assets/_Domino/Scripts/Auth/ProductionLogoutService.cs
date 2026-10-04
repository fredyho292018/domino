using System;
using System.Threading.Tasks;

namespace Domino.Identity
{
    public enum LogoutState { Idle, Confirming, LoggingOut, Success, Error }

    // One provider-independent transaction. Preparation must fail closed before local teardown.
    public sealed class ProductionLogoutService
    {
        readonly Func<PlayerIdentity> current;
        readonly Func<Task> prepare;
        readonly Func<Task> stop;
        readonly Action clear, signOut, welcome;
        Task operation;
        string confirmedUid;
        public LogoutState State { get; private set; }
        public bool GuestWarning { get; private set; }
        public string Message { get; private set; } = "";
        public event Action Changed;
        public ProductionLogoutService(Func<PlayerIdentity> current, Func<Task> prepare, Func<Task> stop,
            Action clear, Action signOut, Action welcome)
        { this.current=current;this.prepare=prepare;this.stop=stop;this.clear=clear;this.signOut=signOut;this.welcome=welcome; }
        public void Request()
        {
            if(State==LogoutState.LoggingOut)return;
            var user=current();
            if(user==null){State=LogoutState.Success;Changed?.Invoke();return;}
            confirmedUid=user.Uid;GuestWarning=user.IsAnonymous;Message="";State=LogoutState.Confirming;Changed?.Invoke();
        }
        public void Cancel()
        { if(State==LogoutState.LoggingOut)return;confirmedUid=null;State=LogoutState.Idle;Message="";Changed?.Invoke(); }
        public Task ConfirmAsync(Func<bool> sessionStillCurrent = null)
        {
            if(State==LogoutState.LoggingOut)return operation;
            if(State!=LogoutState.Confirming)return Task.CompletedTask;
            var done=new TaskCompletionSource<bool>();operation=done.Task;
            State=LogoutState.LoggingOut;Changed?.Invoke();_=Run(done,sessionStillCurrent);return operation;
        }
        async Task Run(TaskCompletionSource<bool> done,Func<bool> sessionStillCurrent)
        {
            try {
                if(current()?.Uid!=confirmedUid || sessionStillCurrent?.Invoke()==false)throw new InvalidOperationException();
                await prepare();
                if(current()?.Uid!=confirmedUid || sessionStillCurrent?.Invoke()==false)throw new InvalidOperationException();
                await stop();
                if(current()?.Uid!=confirmedUid || sessionStillCurrent?.Invoke()==false)throw new InvalidOperationException();
                clear();signOut();welcome();State=LogoutState.Success;Message="";
            } catch { State=LogoutState.Error;Message="Sign out could not finish safely. Close any active game or queue, then try again."; }
            finally { confirmedUid=null;Changed?.Invoke();done.TrySetResult(true); }
        }
    }
}
