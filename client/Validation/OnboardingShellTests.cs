using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Domino.Infrastructure.Api;
using Domino.UI.AppShell;
static class OnboardingShellTests
{
 static int checks;static void Need(bool ok){if(!ok)throw new Exception("Shell contract check "+checks);checks++;}
 sealed class Source:IOnboardingShellSource{
  public OnboardingStateDto state=new OnboardingStateDto{status="IN_PROGRESS",catalogVersion=2,revision=19,currentStepKey="BASIC_PROFILE_STEP"};
  public int version=2,calls;public Exception failure;public TaskCompletionSource<bool> hold;
  public async Task<OnboardingStateDto> LoadAsync(CancellationToken t){calls++;if(hold!=null)await hold.Task;if(failure!=null)throw failure;return state;}
  public Task<OnboardingCatalogDto> CatalogAsync(string l,CancellationToken t)=>Task.FromResult(new OnboardingCatalogDto{catalogVersion=version,locale=l,steps=(version==1?new[]{"EXPERIENCE_STEP","COACH_STEP","CONTACTS_STEP","MEMBERSHIP_STEP"}:new[]{"BASIC_PROFILE_STEP","EXPERIENCE_STEP","COACH_STEP","CONTACTS_STEP","MEMBERSHIP_STEP"}).Select(x=>new OnboardingStepDto{key=x,title=x}).ToArray()});
 }
 static async Task Main(){
  foreach(int v in new[]{1,2})foreach(string locale in new[]{"en","es"}){
   var f=new Source{version=v};f.state.catalogVersion=v;f.state.currentStepKey=v==1?"EXPERIENCE_STEP":"BASIC_PROFILE_STEP";
   using(var c=new OnboardingShellController(f)){
    await c.LoadAsync(locale);Need(c.Phase==OnboardingShellPhase.InProgress);Need(c.State.revision==19);Need(c.Catalog.catalogVersion==v);Need(c.Locale==locale);
    foreach(var step in c.Catalog.steps){f.state.currentStepKey=step.key;await c.LoadAsync(locale);Need(c.CurrentStep.key==step.key);Need(c.State.revision==19);}
    c.State.revision=999;Need(c.State.revision==19);c.Catalog.steps[0].key="MUTATED";Need(c.Catalog.steps[0].key!="MUTATED");
    f.state.status="COMPLETED";f.state.currentStepKey=null;await c.LoadAsync(locale);Need(c.Phase==OnboardingShellPhase.Completed);Need(c.CurrentStep==null);
    f.state.status="NOT_STARTED";f.state.catalogVersion=null;await c.LoadAsync(locale);Need(c.Phase==OnboardingShellPhase.NotStarted);Need(c.State.catalogVersion==null);
   }
  }
  var source=new Source{hold=new TaskCompletionSource<bool>()};var controller=new OnboardingShellController(source);var load=controller.LoadAsync("en");Need(controller.Busy&&controller.Phase==OnboardingShellPhase.Loading);await controller.LoadAsync("es");Need(source.calls==1);source.hold.SetResult(true);await load;Need(!controller.Busy);
  source.failure=new DominoApiException(ApiFailure.Transport);await controller.LoadAsync("en");Need(controller.CanRetry);Need(controller.State.revision==19);source.failure=null;await controller.RetryAsync();Need(controller.Phase==OnboardingShellPhase.InProgress);
  source.failure=new DominoApiException(ApiFailure.Server,409,"CLIENT_UPDATE_REQUIRED");await controller.LoadAsync("en");Need(controller.Phase==OnboardingShellPhase.UpdateRequired);Need(!controller.CanRetry);int calls=source.calls;await controller.RetryAsync();Need(source.calls==calls);
  source.failure=null;source.state.catalogVersion=1;await controller.LoadAsync("en");Need(controller.Phase==OnboardingShellPhase.Error);Need(controller.State.catalogVersion==2);
  source.state.catalogVersion=2;source.state.revision=18;await controller.LoadAsync("en");Need(controller.Phase==OnboardingShellPhase.Error);Need(controller.State.revision==19);
  controller.Dispose();Need(controller.State==null);
  source=new Source{hold=new TaskCompletionSource<bool>()};controller=new OnboardingShellController(source);load=controller.LoadAsync("en");controller.Dispose();source.hold.SetResult(true);await load;Need(controller.State==null);
  source=new Source();source.state.currentStepKey="UNKNOWN";using(controller=new OnboardingShellController(source)){await controller.LoadAsync("en");Need(controller.Phase==OnboardingShellPhase.Error);Need(controller.State==null);}
  Console.WriteLine("ONBOARDING_SHELL_CHECKS="+checks+"_PASS");
 }
}
