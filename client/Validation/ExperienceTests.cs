using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Domino.Infrastructure.Api;
using Domino.UI.AppShell;
static class ExperienceTests {
 static int n; static void Check(bool b){if(!b)throw new Exception("Experience check "+n);n++;}
 sealed class Source:IOnboardingShellSource,IExperienceSource {
 public OnboardingStateDto state=new OnboardingStateDto{status="IN_PROGRESS",catalogVersion=2,revision=7,currentStepKey="EXPERIENCE_STEP"};
 public int prepares,calls,loads;public object first,last;public string error,answer,question;public bool back;public TaskCompletionSource<bool> hold;
 public Task<OnboardingStateDto> LoadAsync(CancellationToken t){loads++;return Task.FromResult(state);}
 public Task<OnboardingCatalogDto> CatalogAsync(string l,CancellationToken t)=>Task.FromResult(new OnboardingCatalogDto{catalogVersion=state.catalogVersion.Value,locale=l,steps=new[]{new OnboardingStepDto{key="EXPERIENCE_STEP",questions=new[]{new OnboardingQuestionDto{key="DOMINO_EXPERIENCE",type="SINGLE_SELECT",title=l,options=new[]{"BEGINNER","RULES_KNOWN","STRATEGY","COMPETITIVE"}.Select(k=>new OnboardingOptionDto{key=k,title=l+" localized "+k}).ToArray()}}},new OnboardingStepDto{key="COACH_STEP"},new OnboardingStepDto{key="BASIC_PROFILE_STEP"}}.Where(s=>state.catalogVersion!=1||s.key!="BASIC_PROFILE_STEP").ToArray()});
 public object PrepareExperience(string q,string a){prepares++;question=q;answer=a;return new object();}
 public object PrepareExperienceBack(){prepares++;back=true;return new object();}
 public async Task<OnboardingStateDto> ExecuteExperienceAsync(object o,CancellationToken t){calls++;last=o;if(first==null)first=o;if(hold!=null)await hold.Task;if(error=="NETWORK")throw new DominoApiException(ApiFailure.Transport);if(error=="VALIDATION")throw new DominoApiException(ApiFailure.Server,400,"ONBOARDING_INVALID_ANSWER");if(error=="CONFLICT"){state.revision=9;state.answers=new[]{OnboardingApiClient.Experience("DOMINO_EXPERIENCE","RULES_KNOWN")};throw new DominoApiException(ApiFailure.Server,409,"REVISION_MISMATCH");}state.revision++;state.currentStepKey=back?"BASIC_PROFILE_STEP":"COACH_STEP";state.answers=new[]{OnboardingApiClient.Experience("DOMINO_EXPERIENCE",answer??"STRATEGY")};return state;}
 }
 static async Task Main(){
 var f=new Source();using(var c=new OnboardingShellController(f)){await c.LoadAsync("en");Check(!c.ExperienceCanSave);await c.SaveExperienceAsync();Check(f.calls==0);c.SelectExperience("invalid");Check(c.ExperienceSelection==null);foreach(var k in new[]{"BEGINNER","RULES_KNOWN","STRATEGY","COMPETITIVE"}){c.SelectExperience(k);Check(c.ExperienceSelection==k&&c.ExperienceCanSave);}c.SelectExperience("STRATEGY");await c.ChangeExperienceLocaleAsync("es");Check(c.Locale=="es"&&c.ExperienceQuestion.title=="es");Check(c.ExperienceSelection=="STRATEGY"&&c.State.revision==7&&c.State.catalogVersion==2);f.error="NETWORK";await c.SaveExperienceAsync();Check(c.ExperienceRetry&&c.ExperienceLocked);Check(c.ExperienceSelection=="STRATEGY"&&c.State.revision==7);c.SelectExperience("BEGINNER");Check(c.ExperienceSelection=="STRATEGY");Check(f.question=="DOMINO_EXPERIENCE"&&f.answer=="STRATEGY");f.error=null;await c.SaveExperienceAsync();Check(ReferenceEquals(f.first,f.last)&&f.prepares==1);Check(c.State.revision==8&&c.State.currentStepKey=="COACH_STEP");Check(c.ExperienceFeedback=="");f.state.currentStepKey="EXPERIENCE_STEP";await c.LoadAsync("en");Check(c.ExperienceSelection=="STRATEGY");await c.BackExperienceAsync();Check(f.back&&c.State.currentStepKey=="BASIC_PROFILE_STEP"&&c.State.revision==9);}
 f=new Source{error="CONFLICT"};using(var c=new OnboardingShellController(f)){await c.LoadAsync("en");c.SelectExperience("BEGINNER");await c.SaveExperienceAsync();Check(c.State.revision==9&&c.ExperienceSelection=="RULES_KNOWN");Check(f.loads==2&&f.calls==1&&c.ExperienceFeedback=="CONFLICT");}
 f=new Source{error="VALIDATION"};using(var c=new OnboardingShellController(f)){await c.LoadAsync("en");c.SelectExperience("STRATEGY");await c.SaveExperienceAsync();Check(c.ExperienceFeedback=="VALIDATION"&&!c.ExperienceRetry);Check(c.ExperienceSelection=="STRATEGY"&&c.State.revision==7);}
 f=new Source{hold=new TaskCompletionSource<bool>()};using(var c=new OnboardingShellController(f)){await c.LoadAsync("en");c.SelectExperience("STRATEGY");var task=c.SaveExperienceAsync();await c.SaveExperienceAsync();Check(f.calls==1&&c.Busy);f.hold.SetResult(true);await task;Check(c.State.revision==8);}
 f=new Source();f.state.catalogVersion=1;using(var c=new OnboardingShellController(f)){await c.LoadAsync("en");Check(c.Phase==OnboardingShellPhase.InProgress&&c.CurrentStep.key=="EXPERIENCE_STEP");Check(!c.ExperienceCanBack);await c.BackExperienceAsync();Check(f.calls==0);}
 Console.WriteLine("EXPERIENCE_CHECKS="+n+"_PASS");
 }
}
