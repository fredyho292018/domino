using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Domino.Identity
{
    public interface IAccountLinkAuth
    {
        PlayerIdentity Current {get;}
        PlayerIdentity LinkedIdentity {get;}
        FirebaseAuthSessionSnapshot Session {get;}
        Task LinkGuestEmailAsync(string email,string password,string confirmation);
    }
    public enum AccountLinkState { Form, Linking, LinkedUnverified, VerificationSending, VerificationPending, ErrorBeforeLink, ErrorAfterLink }
    // Fingerprints stay in memory. Unavailable fields are explicit, never invented or fetched here.
    public sealed class AccountLinkSnapshot
    {
        readonly Dictionary<string,string> values=new Dictionary<string,string>();
        public IReadOnlyDictionary<string,string> Fingerprints=>values;
        public AccountLinkSnapshot Add(string key,string value){
            using(var sha=SHA256.Create())values[key]=value==null?"UNAVAILABLE":BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-","");return this;
        }
        public bool Same(AccountLinkSnapshot other,string key)=>other!=null&&values.TryGetValue(key,out var a)&&other.values.TryGetValue(key,out var b)&&a==b;
    }
    public sealed class AccountLinkController : IDisposable
    {
        readonly IAccountLinkAuth auth;readonly Func<AccountLinkSnapshot> capture;readonly Func<Task> verification;
        readonly PlayerIdentity owner;bool disposed,linked;Task operation;
        public AccountLinkState State {get;private set;}=AccountLinkState.Form;
        public EmailAuthError Error {get;private set;}
        public AccountLinkSnapshot Before {get;private set;}
        public AccountLinkSnapshot After {get;private set;}
        public bool Busy=>State==AccountLinkState.Linking||State==AccountLinkState.VerificationSending;
        public bool CanCancel {get{try{return !Busy&&!linked&&ReferenceEquals(owner,auth.Current)&&auth.Session?.IsAnonymous==true;}catch{return false;}}}
        public bool CanRetryAfterLink=>linked&&Error!=EmailAuthError.SessionConflict;
        public event Action Changed;
        public AccountLinkController(IAccountLinkAuth auth,Func<AccountLinkSnapshot> capture,Func<Task> verification)
        {this.auth=auth;this.capture=capture;this.verification=verification;owner=auth.Current;}
        public Task SubmitAsync(string email,string password,string confirmation)
        {
            if(disposed)return Task.CompletedTask;if(Busy)return operation??Task.CompletedTask;
            if(linked&&!CanRetryAfterLink)return Task.CompletedTask;
            if(!linked){Error=EmailAuthRules.Validate(email,password,confirmation);if(Error!=EmailAuthError.None){Set(AccountLinkState.ErrorBeforeLink);return Task.CompletedTask;}}
            var done=new TaskCompletionSource<bool>();operation=done.Task;Error=EmailAuthError.None;
            Set(linked?AccountLinkState.VerificationSending:AccountLinkState.Linking);_=Run(email,password,confirmation,done);return operation;
        }
        async Task Run(string email,string password,string confirmation,TaskCompletionSource<bool> done)
        {
            try{
                if(!linked){
                    if(owner==null||!ReferenceEquals(owner,auth.Current)||!owner.IsAnonymous||auth.Session?.Uid!=owner.Uid)throw new EmailAuthException(EmailAuthError.SessionConflict);
                    Before=capture();if(Before==null)throw new EmailAuthException(EmailAuthError.SessionConflict);
                    await auth.LinkGuestEmailAsync(email,password,confirmation);
                    if(disposed)return;
                    if(auth.Session?.Uid!=owner.Uid||auth.Session.IsAnonymous||!auth.Session.IsPasswordProvider||!ReferenceEquals(auth.Current,auth.LinkedIdentity))throw new EmailAuthException(EmailAuthError.SessionConflict);
                    linked=true;After=capture();
                    foreach(var field in Before.Fingerprints.Keys)if(!Before.Same(After,field))throw new EmailAuthException(EmailAuthError.SessionConflict);
                    Set(AccountLinkState.LinkedUnverified);
                }
                if(auth.Session?.Uid!=owner.Uid||auth.Session.IsAnonymous)throw new EmailAuthException(EmailAuthError.SessionConflict);
                Set(AccountLinkState.VerificationSending);await verification();if(disposed)return;Set(AccountLinkState.VerificationPending);
            }catch(Exception e){if(!disposed){Error=e is EmailAuthException safe?safe.Code:EmailAuthError.Unknown;Set(linked?AccountLinkState.ErrorAfterLink:AccountLinkState.ErrorBeforeLink);}}
            finally{done.TrySetResult(true);}
        }
        void Set(AccountLinkState state){State=state;Changed?.Invoke();}
        public void Dispose(){disposed=true;Changed=null;Before=null;After=null;}
    }
}
