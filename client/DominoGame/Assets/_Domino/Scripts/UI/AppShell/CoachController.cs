using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Domino.Infrastructure.Api;
namespace Domino.UI.AppShell {
 public interface ICoachSource {
  Task<CoachCatalogDto> LoadCoachesAsync(string locale,int? version,CancellationToken token);
  object PrepareCoach(string questionKey,string optionKey);
  object PrepareCoachBack();
  Task<OnboardingStateDto> ExecuteCoachAsync(object operation,CancellationToken token);
 }
 public sealed partial class OnboardingShellController {
  string coachSelection;object pendingCoach;CoachCatalogDto coaches;
  public CoachCatalogDto Coaches=>Copy(coaches);
  public bool MissingSavedCoach=>coachSelection!=null&&!ValidCoach(coachSelection);
  async Task RefreshCoachesAsync(){if(state?.currentStepKey=="COACH_STEP"&&source is ICoachSource loader){var next=await loader.LoadCoachesAsync(Locale,catalog.coachCatalogVersion,lifetime.Token);lifetime.Token.ThrowIfCancellationRequested();if(next==null||next.catalogVersion<=0||next.items==null||next.items.Any(x=>x==null||string.IsNullOrEmpty(x.key))||next.items.Select(x=>x.key).Distinct().Count()!=next.items.Length||(catalog.coachCatalogVersion.HasValue&&catalog.coachCatalogVersion!=next.catalogVersion))throw new InvalidOperationException();coaches=Copy(next);}SynchronizeCoach();}
  public string CoachSelection=>coachSelection;
  public string CoachFeedback {get;private set;}="";
  public bool CoachLocked=>busy||pendingCoach!=null||Phase==OnboardingShellPhase.UpdateRequired;
  public bool CoachRetry=>pendingCoach!=null&&!busy;
  public OnboardingQuestionDto CoachQuestion=>Copy(catalog?.steps?.FirstOrDefault(s=>s.key=="COACH_STEP")?.questions?.FirstOrDefault(q=>q.key=="COACH_SELECTION"&&q.type=="COACH_SELECT"));
  public bool CoachCanBack=>state?.catalogVersion>=1&&state.currentStepKey=="COACH_STEP"&&!CoachLocked;
  public bool CoachCanSave=>!busy&&Phase==OnboardingShellPhase.InProgress&&state?.currentStepKey=="COACH_STEP"&&(pendingCoach!=null||ValidCoach(coachSelection));
  bool ValidCoach(string key)=>key!=null&&coaches?.items?.Count(o=>o.key==key&&o.selectable)==1;
  void SynchronizeCoach(){var q=CoachQuestion;var saved=state?.answers?.FirstOrDefault(a=>a.questionKey==q?.key&&a.type=="COACH_SELECT")?.optionKey;coachSelection=saved;}
  public void SelectCoach(string key){if(disposed||CoachLocked||state?.currentStepKey!="COACH_STEP"||!ValidCoach(key))return;coachSelection=key;CoachFeedback="";Changed?.Invoke();}
  public async Task ChangeCoachLocaleAsync(string locale){
   if(disposed||CoachLocked||(locale!="es"&&locale!="en"))return;busy=true;CoachFeedback="LOCALIZING";Changed?.Invoke();
   try{var c=await source.CatalogAsync(locale,lifetime.Token);lifetime.Token.ThrowIfCancellationRequested();Validate(state,c);var previous=coachSelection;catalog=Copy(c);Locale=locale;await RefreshCoachesAsync();coachSelection=previous;CoachFeedback="";}
   catch(OperationCanceledException){}catch(Exception){CoachFeedback="LOCALE_ERROR";}
   finally{busy=false;if(!disposed)Changed?.Invoke();}
  }
  public Task SaveCoachAsync()=>RunCoach(false);
  public Task BackCoachAsync()=>CoachCanBack?RunCoach(true):Task.CompletedTask;
  async Task RunCoach(bool back){
   if(disposed||busy||lifetime.IsCancellationRequested||!(source is ICoachSource writer)||(!back&&!CoachCanSave)||state?.currentStepKey!="COACH_STEP")return;
   busy=true;CoachFeedback="SAVING";Changed?.Invoke();
   try{
    if(pendingCoach==null)pendingCoach=back?writer.PrepareCoachBack():writer.PrepareCoach(CoachQuestion.key,coachSelection);
    var next=await writer.ExecuteCoachAsync(pendingCoach,lifetime.Token);lifetime.Token.ThrowIfCancellationRequested();Validate(next,catalog);
    if(next.revision<state.revision)throw new DominoApiException(ApiFailure.Contract);
    state=Copy(next);pendingCoach=null;CoachFeedback="";InitializeProfile();await RefreshCoachesAsync();SynchronizeExperience();
    Phase=next.status=="COMPLETED"?OnboardingShellPhase.Completed:OnboardingShellPhase.InProgress;
   }catch(OperationCanceledException){}
   catch(DominoApiException e){
    if(e.ServerErrorCode=="CLIENT_UPDATE_REQUIRED"){pendingCoach=null;Phase=OnboardingShellPhase.UpdateRequired;}
    else if(new[]{"REVISION_MISMATCH","ONBOARDING_REVISION_MISMATCH","DOMAIN_REVISION_MISMATCH","ONBOARDING_CATALOG_VERSION_MISMATCH"}.Contains(e.ServerErrorCode)){
     pendingCoach=null;CoachFeedback="CONFLICT";
     try{var next=await source.LoadAsync(lifetime.Token);var c=await source.CatalogAsync(Locale,lifetime.Token);lifetime.Token.ThrowIfCancellationRequested();Validate(next,c);if(next.revision<state.revision)throw new InvalidOperationException();state=Copy(next);catalog=Copy(c);await RefreshCoachesAsync();SynchronizeExperience();InitializeProfile();Phase=next.status=="COMPLETED"?OnboardingShellPhase.Completed:next.status=="NOT_STARTED"?OnboardingShellPhase.NotStarted:OnboardingShellPhase.InProgress;}
     catch(OperationCanceledException){}catch(Exception){Phase=OnboardingShellPhase.Error;}
    }else if(e.HttpStatus==400||e.HttpStatus==403){pendingCoach=null;CoachFeedback="VALIDATION";}
    else CoachFeedback="NETWORK";
   }catch(Exception){CoachFeedback="NETWORK";}
   finally{busy=false;if(!disposed)Changed?.Invoke();}
  }
 }
}
