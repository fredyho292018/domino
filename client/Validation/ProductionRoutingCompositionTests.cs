using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Domino.Editor;
using Domino.Identity;
using Domino.UI.AppShell;

static class ProductionRoutingCompositionTests
{
    static int checks;
    static void Need(bool pass,string key){if(!pass)throw new Exception(key);checks++;}
    static async Task Main(){try{await Run();}catch(Exception e){Console.WriteLine("FAIL="+e.Message);Environment.ExitCode=1;}}
    static async Task Run()
    {
        using(var f=new RoutingCompositionFixture()){f.Session=null;await f.Forms.RestoreAsync();Need(f.Forms.Route==ProductionAuthRoute.Welcome&&f.BootstrapCalls==0&&f.StateCalls==0,"no_session");f.Forms.NavigateEmail(ProductionAuthRoute.Register);Need(f.Forms.Route==ProductionAuthRoute.Register,"form_navigation");}
        using(var f=new RoutingCompositionFixture()){f.Session=null;await f.Forms.RestoreAsync();await f.Forms.ContinueAsGuestAsync();Need(f.Forms.Route==ProductionAuthRoute.Error&&!string.IsNullOrEmpty(f.Forms.Message)&&f.BootstrapCalls==0,"guest_auth_failure_visible");}
        using(var f=new RoutingCompositionFixture()){
            f.Session=new FirebaseAuthSessionSnapshot("routing-fixture",false,false,true);await f.Forms.RestoreAsync();Need(f.Forms.Route==ProductionAuthRoute.VerificationPending&&f.BootstrapCalls==0,"unverified");
            f.Session=new FirebaseAuthSessionSnapshot("routing-fixture",false,true,true);await f.Forms.CheckVerificationAsync();Need(f.Forms.Route==ProductionAuthRoute.Onboarding&&f.BootstrapCalls==1,"verified_transition");
        }
        foreach(bool guest in new[]{true,false})foreach(int v in new[]{1,2})foreach(string status in new[]{"NOT_STARTED","IN_PROGRESS","COMPLETED"}){
            using var f=new RoutingCompositionFixture(v);f.Session=new FirebaseAuthSessionSnapshot("routing-fixture",guest,!guest,!guest);f.State(status,status=="NOT_STARTED"?(int?)null:v,status=="IN_PROGRESS"?"COACH_STEP":null);
            int applications=0;f.Composition.Changed+=()=>{if(f.Forms.Route==ProductionAuthRoute.AppShell||f.Forms.Route==ProductionAuthRoute.Onboarding)applications++;};
            await f.Forms.RestoreAsync();Need(f.Forms.Route==(status=="COMPLETED"?ProductionAuthRoute.AppShell:ProductionAuthRoute.Onboarding),"matrix");Need(applications==1,"one_final_application");Need(f.BootstrapCalls==1&&f.StateCalls==1,"one_bootstrap_read");
            if(status!="COMPLETED"){await f.Composition.LoadOnboardingAsync();Need(f.Composition.Onboarding.State.currentStepKey==f.Server.State.currentStepKey,"controller_cursor");Need(f.Composition.Onboarding.Catalog.catalogVersion==v,"pinned_catalog");}
            Need(f.Server.Applied==0&&f.Server.TrialApplied==0&&f.AuthWrites==0,"read_only_resolution");
        }
        foreach(var step in new[]{"BASIC_PROFILE_STEP","EXPERIENCE_STEP","COACH_STEP","CONTACTS_STEP","MEMBERSHIP_STEP"}){
            using var f=new RoutingCompositionFixture();f.State("IN_PROGRESS",2,step);await f.Forms.RestoreAsync();await f.Composition.LoadOnboardingAsync();Need(f.Composition.Onboarding.Phase==OnboardingShellPhase.InProgress&&f.Composition.Onboarding.State.currentStepKey==step,"all_steps");
        }
        using(var f=new RoutingCompositionFixture()){f.State("COMPLETED");await f.Forms.RestoreAsync();Need(f.Forms.Route==ProductionAuthRoute.AppShell&&f.Composition.Onboarding.Phase==OnboardingShellPhase.Idle,"legacy_no_reopen");}
        foreach(var error in new[]{"BOOTSTRAP","ONBOARDING","UPDATE"}){
            using var f=new RoutingCompositionFixture();f.ErrorAt=error;await f.Forms.RestoreAsync();Need(f.Forms.Route==(error=="UPDATE"?ProductionAuthRoute.UpdateRequired:ProductionAuthRoute.Error),"fail_closed");
            int bootstraps=f.BootstrapCalls;f.ErrorAt=null;await f.Forms.RetryRoutingAsync();Need(f.Forms.Route==(error=="UPDATE"?ProductionAuthRoute.UpdateRequired:ProductionAuthRoute.Onboarding),"retry_or_terminal");if(error=="ONBOARDING")Need(f.BootstrapCalls==bootstraps,"stage_only_retry");
        }
        using(var f=new RoutingCompositionFixture()){
            f.Hold=new TaskCompletionSource<bool>();var seen=new List<ProductionAuthRoute>();f.Forms.Changed+=()=>seen.Add(f.Forms.Route);var pending=f.Forms.RestoreAsync();Need(f.Forms.Route==ProductionAuthRoute.Loading,"restore_loading");f.Hold.SetResult(true);await pending;Need(!seen.Contains(ProductionAuthRoute.Welcome),"no_welcome_flash");
        }
        using(var a=new RoutingCompositionFixture()){
            a.Hold=new TaskCompletionSource<bool>();var pending=a.Forms.RestoreAsync();a.Forms.Dispose();using var b=new RoutingCompositionFixture();b.Session=new FirebaseAuthSessionSnapshot("routing-fixture-b",true,false,false);b.State("COMPLETED",2);await b.Forms.RestoreAsync();a.Hold.SetResult(true);await pending;Need(b.Forms.Route==ProductionAuthRoute.AppShell&&a.Composition.Router.Onboarding==null,"session_replacement");
        }
        using(var f=new RoutingCompositionFixture()){await f.Forms.RestoreAsync();f.Auth.SignOut();await f.Forms.RestoreAsync();Need(f.Forms.Route==ProductionAuthRoute.Welcome&&f.Composition.Onboarding==null,"logout_fixture");}
        using(var f=new RoutingCompositionFixture()){
            f.State("IN_PROGRESS",2,"MEMBERSHIP_STEP");f.Server.State.completedStepKeys=new[]{"BASIC_PROFILE_STEP","EXPERIENCE_STEP","COACH_STEP"};f.Server.State.skippedStepKeys=new[]{"CONTACTS_STEP","MEMBERSHIP_STEP"};
            await f.Forms.RestoreAsync();await f.Composition.LoadOnboardingAsync();int reads=f.StateCalls;await f.Composition.Onboarding.CompleteAsync();
            for(int i=0;i<100&&f.Forms.Route!=ProductionAuthRoute.AppShell;i++)await Task.Yield();
            Need(f.Server.State.status=="COMPLETED"&&f.Forms.Route==ProductionAuthRoute.AppShell,"completion_home");Need(f.StateCalls>reads,"completion_reread");Need(f.Server.TrialApplied==0,"no_trial");
        }
        Console.WriteLine("PRODUCTION_COMPOSITION_CHECKS="+checks+"_PASS REAL_OPERATIONS=0");
    }
}
