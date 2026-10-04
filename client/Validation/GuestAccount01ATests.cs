using System;
using System.Threading.Tasks;
using Domino.Identity;
static class GuestAccount01ATests
{
    static int checks;
    static void Need(bool value,string key){checks++;if(!value)throw new Exception(key);}
    sealed class Fixture
    {
        public FirebaseAuthSessionSnapshot Snapshot; public PlayerIdentity Identity;
        public int Prepare,Stop,Clear,SignOut,Welcome;public TaskCompletionSource<bool> Hold;
        public readonly ProductionLogoutService Logout;public readonly AccountProtectionController Gate;
        public Fixture(bool anonymous=true,bool password=false,string uid="FIXTURE_A")
        {
            Snapshot=new FirebaseAuthSessionSnapshot(uid,anonymous,false,password);Identity=new PlayerIdentity(uid,anonymous);
            Logout=new ProductionLogoutService(()=>Identity,async()=>{Prepare++;if(Hold!=null)await Hold.Task;},()=>{Stop++;return Task.CompletedTask;},()=>Clear++,()=>{SignOut++;Identity=null;Snapshot=null;},()=>Welcome++);
            Gate=new AccountProtectionController(()=>Snapshot,()=>Identity,Logout);
        }
        public bool Untouched=>Prepare+Stop+Clear+SignOut+Welcome==0;
    }
    static async Task Main()
    {
        Need(AuthRecoverabilityClassifier.Classify(null)==AuthRecoverability.NoSession,"NO_SESSION");
        Need(AuthRecoverabilityClassifier.Classify(new FirebaseAuthSessionSnapshot("fixture",true,false,false))==AuthRecoverability.AnonymousUnlinked,"ANONYMOUS");
        foreach(bool verified in new[]{false,true})Need(AuthRecoverabilityClassifier.Classify(new FirebaseAuthSessionSnapshot("fixture",false,verified,true))==AuthRecoverability.Recoverable,"PASSWORD_VERIFICATION_INDEPENDENT");
        Need(AuthRecoverabilityClassifier.Classify(new FirebaseAuthSessionSnapshot("fixture",true,false,true))==AuthRecoverability.Unknown,"CONTRADICTORY_FAIL_CLOSED");
        Need(AuthRecoverabilityClassifier.Classify(new FirebaseAuthSessionSnapshot("fixture",false,true,false))==AuthRecoverability.Unknown,"UNSUPPORTED_PROVIDER");
        Need(AuthRecoverabilityClassifier.Classify(new FirebaseAuthSessionSnapshot("",false,true,true))==AuthRecoverability.Unknown,"EMPTY_UID");
        // Public alias is deliberately not an input to classification or the logout gate.
        foreach(var alias in new[]{"Guest-XXXXXXXX","DominoKing"}){
            var email=new Fixture(false,true);await email.Gate.RequestAsync();Need(email.SignOut==1&&email.Welcome==1&&email.Gate.State==AccountProtectionState.Closed,"EMAIL_NORMAL_NO_DIALOG_"+alias);
            var guest=new Fixture();await guest.Gate.RequestAsync();Need(guest.Gate.State==AccountProtectionState.Warning&&guest.Untouched,"GUEST_INTERCEPT_"+alias);
        }
        {
            var f=new Fixture();int opened=0;f.Gate.Changed+=()=>opened++;
            await f.Gate.RequestAsync();await f.Gate.RequestAsync();Need(opened==1&&f.Untouched,"DOUBLE_REQUEST");
            var identity=f.Identity;f.Gate.Cancel();Need(f.Gate.State==AccountProtectionState.Closed&&ReferenceEquals(identity,f.Identity)&&f.Untouched,"CANCEL_NO_TEARDOWN");
            await f.Gate.RequestAsync();f.Gate.Protect();int count=opened;f.Gate.Protect();await f.Gate.RequestAsync();
            Need(f.Gate.State==AccountProtectionState.ProtectEntry&&opened==count&&f.Untouched&&ReferenceEquals(identity,f.Identity),"PROTECT_SINGLE_PRESERVES");
            await f.Gate.ForceLogoutAsync();Need(f.Untouched,"ENTRY_CANNOT_FORCE_WITH_OLD_BUTTON");f.Gate.Cancel();Need(f.Gate.State==AccountProtectionState.Closed,"ENTRY_CANCEL");
        }
        {var f=new Fixture();await f.Gate.RequestAsync();await f.Gate.ForceLogoutAsync();Need(f.Prepare==1&&f.Stop==1&&f.Clear==1&&f.SignOut==1&&f.Welcome==1,"EXISTING_LOGOUT_PATH");await f.Gate.RequestAsync();Need(f.SignOut==1,"NO_SESSION_NO_DIALOG");}
        foreach(bool sameUid in new[]{false,true})foreach(bool protect in new[]{false,true}){
            var f=new Fixture();await f.Gate.RequestAsync();f.Identity=new PlayerIdentity(sameUid?"FIXTURE_A":"FIXTURE_B",true);f.Snapshot=new FirebaseAuthSessionSnapshot(f.Identity.Uid,true,false,false);
            if(protect)f.Gate.Protect();else await f.Gate.ForceLogoutAsync();Need(f.Untouched&&f.Gate.State==AccountProtectionState.Closed,"STALE_DIALOG_EPOCH");
        }
        {var f=new Fixture();await f.Gate.RequestAsync();f.Snapshot=new FirebaseAuthSessionSnapshot("FIXTURE_A",false,true,true);f.Gate.Protect();Need(f.Untouched&&f.Gate.State==AccountProtectionState.Closed,"PROVIDER_CHANGED");}
        {var f=new Fixture();f.Snapshot=new FirebaseAuthSessionSnapshot("FIXTURE_B",true,false,false);await f.Gate.RequestAsync();Need(f.Untouched&&f.Gate.State==AccountProtectionState.Unavailable,"SDK_WRAPPER_MISMATCH");}
        {var f=new Fixture();f.Hold=new TaskCompletionSource<bool>();await f.Gate.RequestAsync();var run=f.Gate.ForceLogoutAsync();await f.Gate.RequestAsync();Need(f.Prepare==1,"SINGLE_FLIGHT");f.Identity=new PlayerIdentity("FIXTURE_A",true);f.Hold.SetResult(true);await run;Need(f.SignOut==0&&f.Clear==0&&f.Logout.State==LogoutState.Error,"SAME_UID_NEW_EPOCH_DURING_PREPARE");}
        {var f=new Fixture(false,false);await f.Gate.RequestAsync();Need(f.Gate.State==AccountProtectionState.Unavailable&&f.Untouched,"UNKNOWN_NO_LOGOUT");f.Gate.Cancel();Need(f.Untouched,"UNKNOWN_CANCEL");}
        {var f=new Fixture();await f.Gate.RequestAsync();f.Gate.Dispose();f.Gate.Protect();await f.Gate.ForceLogoutAsync();Need(f.Untouched,"DISPOSED_DIALOG");}
        Console.WriteLine("GUEST_ACCOUNT_01A_CHECKS="+checks+"_PASS");
    }
}
