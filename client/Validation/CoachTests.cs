using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Domino.Infrastructure.Api;
using Domino.UI.AppShell;
static class CoachTests {
 static OnboardingAnswerDto Answer(string k)=>new OnboardingAnswerDto{questionKey="COACH_SELECTION",type="COACH_SELECT",optionKey=k};
 static int n; static void Check(bool b){if(!b)throw new Exception("Coach check "+n);n++;}
 sealed class Source:IOnboardingShellSource,ICoachSource {
 public OnboardingStateDto state=new OnboardingStateDto{status="IN_PROGRESS",catalogVersion=2,revision=7,currentStepKey="COACH_STEP"};
 public string[] keys={"LUCIA","AMARA","MATEO","OMAR","NEW_COACH"};public int prepares,calls,loads;public object first,last;public string error,answer,question;public bool back;public TaskCompletionSource<bool> hold;
 public Task<OnboardingStateDto> LoadAsync(CancellationToken t){loads++;return Task.FromResult(state);}
 public Task<OnboardingCatalogDto> CatalogAsync(string l,CancellationToken t)=>Task.FromResult(new OnboardingCatalogDto{catalogVersion=state.catalogVersion.Value,locale=l,steps=new[]{new OnboardingStepDto{key="COACH_STEP",questions=new[]{new OnboardingQuestionDto{key="COACH_SELECTION",type="COACH_SELECT",title=l,options=new[]{"LUCIA","AMARA","MATEO","OMAR"}.Select(k=>new OnboardingOptionDto{key=k,title=l+" localized "+k}).ToArray()}}},new OnboardingStepDto{key="CONTACTS_STEP"},new OnboardingStepDto{key="EXPERIENCE_STEP"}}.Where(s=>state.catalogVersion!=1||s.key!="BASIC_PROFILE_STEP").ToArray()});
 public Task<CoachCatalogDto> LoadCoachesAsync(string l,int? v,CancellationToken t)=>Task.FromResult(new CoachCatalogDto{catalogVersion=1,resolvedLocale=l,items=keys.Select(k=>new CoachDto{key=k,name=l+k,selectable=true}).ToArray()});
 public object PrepareCoach(string q,string a){prepares++;question=q;answer=a;return new object();}
 public object PrepareCoachBack(){prepares++;back=true;return new object();}
 public async Task<OnboardingStateDto> ExecuteCoachAsync(object o,CancellationToken t){calls++;last=o;if(first==null)first=o;if(hold!=null)await hold.Task;if(error=="NETWORK")throw new DominoApiException(ApiFailure.Transport);if(error=="VALIDATION")throw new DominoApiException(ApiFailure.Server,400,"ONBOARDING_INVALID_ANSWER");if(error=="CONFLICT"){state.revision=9;state.answers=new[]{Answer("AMARA")};throw new DominoApiException(ApiFailure.Server,409,"REVISION_MISMATCH");}state.revision++;state.currentStepKey=back?"EXPERIENCE_STEP":"CONTACTS_STEP";state.answers=new[]{Answer(answer??"MATEO")};return state;}
 }
 static async Task Main(){
 var f=new Source();using(var c=new OnboardingShellController(f)){await c.LoadAsync("en");Check(!c.CoachCanSave);await c.SaveCoachAsync();Check(f.calls==0);c.SelectCoach("invalid");Check(c.CoachSelection==null);foreach(var k in new[]{"LUCIA","AMARA","MATEO","OMAR"}){c.SelectCoach(k);Check(c.CoachSelection==k&&c.CoachCanSave);}c.SelectCoach("MATEO");await c.ChangeCoachLocaleAsync("es");Check(c.Locale=="es"&&c.CoachQuestion.title=="es");Check(c.Coaches.resolvedLocale=="es"&&c.Coaches.items.All(x=>x.name.StartsWith("es")));Check(c.CoachSelection=="MATEO"&&c.State.revision==7&&c.State.catalogVersion==2);f.error="NETWORK";await c.SaveCoachAsync();Check(c.CoachRetry&&c.CoachLocked);Check(c.CoachSelection=="MATEO"&&c.State.revision==7);c.SelectCoach("LUCIA");Check(c.CoachSelection=="MATEO");Check(f.question=="COACH_SELECTION"&&f.answer=="MATEO");f.error=null;await c.SaveCoachAsync();Check(ReferenceEquals(f.first,f.last)&&f.prepares==1);Check(c.State.revision==8&&c.State.currentStepKey=="CONTACTS_STEP");Check(c.CoachFeedback=="");f.state.currentStepKey="COACH_STEP";await c.LoadAsync("en");Check(c.CoachSelection=="MATEO");await c.BackCoachAsync();Check(f.back&&c.State.currentStepKey=="EXPERIENCE_STEP"&&c.State.revision==9);}
 f=new Source{error="CONFLICT"};using(var c=new OnboardingShellController(f)){await c.LoadAsync("en");c.SelectCoach("LUCIA");await c.SaveCoachAsync();Check(c.State.revision==9&&c.CoachSelection=="AMARA");Check(f.loads==2&&f.calls==1&&c.CoachFeedback=="CONFLICT");}
 f=new Source{error="VALIDATION"};using(var c=new OnboardingShellController(f)){await c.LoadAsync("en");c.SelectCoach("MATEO");await c.SaveCoachAsync();Check(c.CoachFeedback=="VALIDATION"&&!c.CoachRetry);Check(c.CoachSelection=="MATEO"&&c.State.revision==7);}
 f=new Source{hold=new TaskCompletionSource<bool>()};using(var c=new OnboardingShellController(f)){await c.LoadAsync("en");c.SelectCoach("MATEO");var task=c.SaveCoachAsync();await c.SaveCoachAsync();Check(f.calls==1&&c.Busy);f.hold.SetResult(true);await task;Check(c.State.revision==8);}
 f=new Source();f.state.catalogVersion=1;using(var c=new OnboardingShellController(f)){await c.LoadAsync("en");Check(c.Phase==OnboardingShellPhase.InProgress&&c.CurrentStep.key=="COACH_STEP");Check(c.CoachCanBack);await c.BackCoachAsync();Check(f.calls==1&&c.State.currentStepKey=="EXPERIENCE_STEP");}
 f=new Source{keys=new[]{"NEW_COACH","MATEO"}};using(var c=new OnboardingShellController(f)){await c.LoadAsync("en");Check(c.Coaches.items.Select(x=>x.key).SequenceEqual(f.keys));c.SelectCoach("NEW_COACH");Check(c.CoachCanSave);await c.SaveCoachAsync();Check(f.answer=="NEW_COACH");}
 f=new Source{keys=new[]{"MATEO"}};f.state.answers=new[]{Answer("HISTORICAL")};using(var c=new OnboardingShellController(f)){await c.LoadAsync("en");Check(c.CoachSelection=="HISTORICAL"&&c.MissingSavedCoach&&!c.CoachCanSave);c.SelectCoach("MATEO");Check(c.CoachCanSave&&!c.MissingSavedCoach);}
 Console.WriteLine("COACH_CHECKS="+n+"_PASS");
 }
}
