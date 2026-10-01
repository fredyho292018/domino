using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Domino.Infrastructure.Api;
namespace Domino.UI.AppShell {
 public interface IExperienceSource {
  object PrepareExperience(string questionKey,string optionKey);
  object PrepareExperienceBack();
  Task<OnboardingStateDto> ExecuteExperienceAsync(object operation,CancellationToken token);
 }
 public sealed partial class OnboardingShellController {
  string experienceSelection;object pendingExperience;
  public string ExperienceSelection=>experienceSelection;
  public string ExperienceFeedback {get;private set;}="";
  public bool ExperienceLocked=>busy||pendingExperience!=null||Phase==OnboardingShellPhase.UpdateRequired;
  public bool ExperienceRetry=>pendingExperience!=null&&!busy;
  public OnboardingQuestionDto ExperienceQuestion=>Copy(catalog?.steps?.FirstOrDefault(s=>s.key=="EXPERIENCE_STEP")?.questions?.FirstOrDefault(q=>q.key=="DOMINO_EXPERIENCE"&&q.type=="SINGLE_SELECT"));
  public bool ExperienceCanBack=>state?.catalogVersion==2&&state.currentStepKey=="EXPERIENCE_STEP"&&!ExperienceLocked;
  public bool ExperienceCanSave=>!busy&&Phase==OnboardingShellPhase.InProgress&&state?.currentStepKey=="EXPERIENCE_STEP"&&(pendingExperience!=null||ValidExperience(experienceSelection));
  bool ValidExperience(string key)=>key!=null&&new[]{"BEGINNER","RULES_KNOWN","STRATEGY","COMPETITIVE"}.Contains(key)&&ExperienceQuestion?.options?.Count(o=>o.key==key)==1;
  void SynchronizeExperience(){var q=ExperienceQuestion;var saved=state?.answers?.FirstOrDefault(a=>a.questionKey==q?.key&&a.type=="SINGLE_SELECT")?.optionKey;experienceSelection=ValidExperience(saved)?saved:null;}
  public void SelectExperience(string key){if(disposed||ExperienceLocked||state?.currentStepKey!="EXPERIENCE_STEP"||!ValidExperience(key))return;experienceSelection=key;ExperienceFeedback="";Changed?.Invoke();}
  public async Task ChangeExperienceLocaleAsync(string locale){
   if(disposed||ExperienceLocked||(locale!="es"&&locale!="en"))return;busy=true;ExperienceFeedback="LOCALIZING";Changed?.Invoke();
   try{var c=await source.CatalogAsync(locale,lifetime.Token);lifetime.Token.ThrowIfCancellationRequested();Validate(state,c);catalog=Copy(c);Locale=locale;if(!ValidExperience(experienceSelection))experienceSelection=null;ExperienceFeedback="";}
   catch(OperationCanceledException){}catch(Exception){ExperienceFeedback="LOCALE_ERROR";}
   finally{busy=false;if(!disposed)Changed?.Invoke();}
  }
  public Task SaveExperienceAsync()=>RunExperience(false);
  public Task BackExperienceAsync()=>ExperienceCanBack?RunExperience(true):Task.CompletedTask;
  async Task RunExperience(bool back){
   if(disposed||busy||lifetime.IsCancellationRequested||!(source is IExperienceSource writer)||(!back&&!ExperienceCanSave)||state?.currentStepKey!="EXPERIENCE_STEP")return;
   busy=true;ExperienceFeedback="SAVING";Changed?.Invoke();
   try{
    if(pendingExperience==null)pendingExperience=back?writer.PrepareExperienceBack():writer.PrepareExperience(ExperienceQuestion.key,experienceSelection);
    var next=await writer.ExecuteExperienceAsync(pendingExperience,lifetime.Token);lifetime.Token.ThrowIfCancellationRequested();Validate(next,catalog);
    if(next.revision<state.revision)throw new DominoApiException(ApiFailure.Contract);
    state=Copy(next);pendingExperience=null;ExperienceFeedback="";InitializeProfile();SynchronizeExperience();await RefreshCoachesAsync();
    Phase=next.status=="COMPLETED"?OnboardingShellPhase.Completed:OnboardingShellPhase.InProgress;
   }catch(OperationCanceledException){}
   catch(DominoApiException e){
    if(e.ServerErrorCode=="CLIENT_UPDATE_REQUIRED"){pendingExperience=null;Phase=OnboardingShellPhase.UpdateRequired;}
    else if(new[]{"REVISION_MISMATCH","ONBOARDING_REVISION_MISMATCH","DOMAIN_REVISION_MISMATCH","ONBOARDING_CATALOG_VERSION_MISMATCH"}.Contains(e.ServerErrorCode)){
     pendingExperience=null;ExperienceFeedback="CONFLICT";
     try{var next=await source.LoadAsync(lifetime.Token);var c=await source.CatalogAsync(Locale,lifetime.Token);lifetime.Token.ThrowIfCancellationRequested();Validate(next,c);if(next.revision<state.revision)throw new InvalidOperationException();state=Copy(next);catalog=Copy(c);SynchronizeExperience();InitializeProfile();Phase=next.status=="COMPLETED"?OnboardingShellPhase.Completed:next.status=="NOT_STARTED"?OnboardingShellPhase.NotStarted:OnboardingShellPhase.InProgress;}
     catch(OperationCanceledException){}catch(Exception){Phase=OnboardingShellPhase.Error;}
    }else if(e.HttpStatus==400||e.HttpStatus==403){pendingExperience=null;ExperienceFeedback="VALIDATION";}
    else {ExperienceFeedback="NETWORK";if(pendingExperience==null)Phase=OnboardingShellPhase.Error;}
   }catch(Exception){ExperienceFeedback="NETWORK";if(pendingExperience==null)Phase=OnboardingShellPhase.Error;}
   finally{busy=false;if(!disposed)Changed?.Invoke();}
  }
 }
}
