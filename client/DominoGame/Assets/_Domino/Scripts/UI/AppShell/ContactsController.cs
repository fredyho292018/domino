using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Domino.Infrastructure.Api;
namespace Domino.UI.AppShell {
 public interface IContactsSource {
  object PrepareContactsSkip();object PrepareContactsBack();
  Task<OnboardingStateDto> ExecuteContactsAsync(object operation,CancellationToken token);
 }
 public sealed partial class OnboardingShellController {
  object pendingContacts;
  public string ContactsFeedback {get;private set;}="";
  public bool ContactsRetry=>pendingContacts!=null&&!busy;
  public bool ContactsCanContinue=>!busy&&Phase==OnboardingShellPhase.InProgress&&state?.currentStepKey=="CONTACTS_STEP"&&(pendingContacts!=null||CurrentStep?.skippable==true&&CurrentStep.required==false);
  public bool ContactsCanBack=>!busy&&pendingContacts==null&&Phase==OnboardingShellPhase.InProgress&&state?.currentStepKey=="CONTACTS_STEP";
  public async Task ChangeContactsLocaleAsync(string locale){
   if(disposed||busy||pendingContacts!=null||(locale!="es"&&locale!="en"))return;busy=true;Changed?.Invoke();
   try{var next=await source.CatalogAsync(locale,lifetime.Token);lifetime.Token.ThrowIfCancellationRequested();Validate(state,next);catalog=Copy(next);Locale=locale;ContactsFeedback="";}
   catch(OperationCanceledException){}catch(Exception){ContactsFeedback="LOCALE_ERROR";}
   finally{busy=false;if(!disposed)Changed?.Invoke();}
  }
  public Task ContinueContactsAsync()=>RunContacts(false);
  public Task BackContactsAsync()=>ContactsCanBack?RunContacts(true):Task.CompletedTask;
  async Task RunContacts(bool back){
   if(disposed||busy||lifetime.IsCancellationRequested||!(source is IContactsSource writer)||state?.currentStepKey!="CONTACTS_STEP"||(!back&&!ContactsCanContinue))return;
   busy=true;ContactsFeedback="SAVING";Changed?.Invoke();
   try{
    if(pendingContacts==null)pendingContacts=back?writer.PrepareContactsBack():writer.PrepareContactsSkip();
    var next=await writer.ExecuteContactsAsync(pendingContacts,lifetime.Token);lifetime.Token.ThrowIfCancellationRequested();Validate(next,catalog);if(next.revision<state.revision)throw new DominoApiException(ApiFailure.Contract);
    state=Copy(next);pendingContacts=null;ContactsFeedback="";InitializeProfile();SynchronizeExperience();await RefreshCoachesAsync();await RefreshMembershipAsync();Phase=state.status=="COMPLETED"?OnboardingShellPhase.Completed:OnboardingShellPhase.InProgress;
   }catch(OperationCanceledException){}
   catch(DominoApiException e){
    if(e.ServerErrorCode=="CLIENT_UPDATE_REQUIRED"){pendingContacts=null;Phase=OnboardingShellPhase.UpdateRequired;}
    else if(new[]{"REVISION_MISMATCH","ONBOARDING_REVISION_MISMATCH","DOMAIN_REVISION_MISMATCH","ONBOARDING_CATALOG_VERSION_MISMATCH"}.Contains(e.ServerErrorCode)){
     pendingContacts=null;ContactsFeedback="CONFLICT";
     try{var next=await source.LoadAsync(lifetime.Token);var c=await source.CatalogAsync(Locale,lifetime.Token);lifetime.Token.ThrowIfCancellationRequested();Validate(next,c);if(next.revision<state.revision)throw new InvalidOperationException();state=Copy(next);catalog=Copy(c);InitializeProfile();SynchronizeExperience();await RefreshCoachesAsync();await RefreshMembershipAsync();Phase=state.status=="COMPLETED"?OnboardingShellPhase.Completed:state.status=="NOT_STARTED"?OnboardingShellPhase.NotStarted:OnboardingShellPhase.InProgress;}
     catch(OperationCanceledException){}catch(Exception){Phase=OnboardingShellPhase.Error;}
    }else if(e.HttpStatus==400||e.HttpStatus==403){pendingContacts=null;ContactsFeedback="VALIDATION";}
    else{ContactsFeedback="NETWORK";if(pendingContacts==null)Phase=OnboardingShellPhase.Error;}
   }catch(Exception){ContactsFeedback="NETWORK";if(pendingContacts==null)Phase=OnboardingShellPhase.Error;}
   finally{busy=false;if(!disposed)Changed?.Invoke();}
  }
 }
}
