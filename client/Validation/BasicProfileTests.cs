using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Domino.Infrastructure.Api;
using Domino.UI.AppShell;
static class BasicProfileTests {
 static int checks;static void Need(bool ok){if(!ok)throw new Exception("Basic Profile check "+checks);checks++;}
 sealed class Source:IOnboardingShellSource,IBasicProfileSource {
  public OnboardingStateDto state=new OnboardingStateDto{status="IN_PROGRESS",catalogVersion=2,revision=7,currentStepKey="BASIC_PROFILE_STEP",basicProfile=new BasicProfileDto{displayName="FixturePlayer",timeZone="UTC"}};
  public int prepares,saves,loads;public object last,first;public string zone;public OnboardingAnswerDto[] answers;public string error;public TaskCompletionSource<bool> hold;
  public Task<OnboardingStateDto> LoadAsync(CancellationToken t){loads++;return Task.FromResult(state);}
  public Task<OnboardingCatalogDto> CatalogAsync(string l,CancellationToken t)=>Task.FromResult(new OnboardingCatalogDto{catalogVersion=2,locale=l,steps=new[]{new OnboardingStepDto{key="BASIC_PROFILE_STEP"},new OnboardingStepDto{key="EXPERIENCE_STEP"}}});
  public object PrepareProfile(OnboardingAnswerDto[] a,string z){prepares++;answers=a;zone=z;return new object();}
  public async Task<OnboardingStateDto> SaveProfileAsync(object op,CancellationToken t){saves++;last=op;if(first==null)first=op;if(hold!=null)await hold.Task;if(error=="NETWORK")throw new DominoApiException(ApiFailure.Transport);if(error=="CONFLICT"){state.revision=9;throw new DominoApiException(ApiFailure.Server,409,"REVISION_MISMATCH");}if(error=="TAKEN")throw new DominoApiException(ApiFailure.Server,409,"DISPLAY_NAME_TAKEN");if(error=="ROLLOUT")throw new DominoApiException(ApiFailure.Server,409,"DISPLAY_NAME_RESERVATIONS_NOT_READY");if(error=="VALIDATION")throw new DominoApiException(ApiFailure.Server,400,"DISPLAY_NAME_RESERVED");state.revision++;state.currentStepKey="EXPERIENCE_STEP";return state;}
 }
 static void Fill(OnboardingShellController c){c.Profile.FirstName="Prueba";c.Profile.LastName="Validación";c.Profile.Country="ES";c.Profile.Language="en";}
 static async Task Main(){
  Need(BasicProfileRules.Countries.Length==249);Need(BasicProfileRules.Name("  Épreuve  "));Need(!BasicProfileRules.Name("\u202e"));Need(!BasicProfileRules.Name(new string('a',81)));Need(!BasicProfileRules.Name(""));
  var f=new Source();using(var c=new OnboardingShellController(f)){await c.LoadAsync("en");Need(c.Profile.DisplayName=="FixturePlayer");Need(c.Profile.FirstName=="");Need(c.Profile.Country=="");await c.SaveProfileAsync();Need(f.saves==0);Need(c.ProfileErrors.Contains("COUNTRY"));Fill(c);
   await c.ChangeProfileLocaleAsync("es");Need(c.Locale=="es");Need(c.Profile.FirstName=="Prueba"&&c.Profile.LastName=="Validación"&&c.Profile.DisplayName=="FixturePlayer"&&c.Profile.Country=="ES");Need(c.State.revision==7&&c.State.catalogVersion==2&&c.State.currentStepKey=="BASIC_PROFILE_STEP");
   f.error="NETWORK";await c.SaveProfileAsync();Need(c.ProfileRetry&&c.ProfileLocked);Need(c.State.revision==7);Need(f.zone==null);Need(f.answers.Length==5);Need(f.answers[0].type=="TEXT"&&f.answers[3].optionKey=="ES"&&f.answers[4].optionKey=="es");
   f.error=null;await c.SaveProfileAsync();Need(ReferenceEquals(f.first,f.last)&&f.prepares==1);Need(c.State.revision==8&&c.State.currentStepKey=="EXPERIENCE_STEP");Need(c.ProfileFeedback=="");
  }
  f=new Source{error="CONFLICT"};using(var c=new OnboardingShellController(f)){await c.LoadAsync("en");Fill(c);await c.SaveProfileAsync();Need(c.ProfileFeedback=="CONFLICT");Need(c.State.revision==9&&f.loads==2);Need(c.Profile.FirstName=="Prueba"&&c.Profile.Country=="ES");Need(!c.ProfileLocked);f.error=null;await c.SaveProfileAsync();Need(f.prepares==2);Need(c.State.revision==10);}
  f=new Source{error="VALIDATION"};using(var c=new OnboardingShellController(f)){await c.LoadAsync("en");Fill(c);await c.SaveProfileAsync();Need(c.ProfileErrors.SequenceEqual(new[]{"DISPLAY_NAME"}));Need(c.State.revision==7);Need(c.ProfileFeedback=="VALIDATION");}
  f=new Source{hold=new TaskCompletionSource<bool>()};using(var c=new OnboardingShellController(f)){await c.LoadAsync("en");Fill(c);var save=c.SaveProfileAsync();Need(c.Busy&&c.State.revision==7);await c.SaveProfileAsync();Need(f.saves==1);f.hold.SetResult(true);await save;Need(c.State.revision==8);}
  f=new Source();f.state.basicProfile.displayName="Guest-ABCDEFGH";using(var c=new OnboardingShellController(f)){await c.LoadAsync("en");Need(c.Profile.DisplayName=="Guest-ABCDEFGH");Fill(c);await c.SaveProfileAsync();Need(f.answers.Single(a=>a.questionKey=="DISPLAY_NAME").textValue=="Guest-ABCDEFGH");}
  foreach(var conflict in new[]{"TAKEN","ROLLOUT"}){f=new Source{error=conflict};using(var c=new OnboardingShellController(f)){await c.LoadAsync("en");Fill(c);c.Profile.DisplayName="  CustomAlias  ";await c.SaveProfileAsync();Need(c.Phase==OnboardingShellPhase.InProgress);Need(c.Profile.DisplayName=="  CustomAlias  "&&c.Profile.FirstName=="Prueba");Need(c.ProfileErrors.SequenceEqual(new[]{"DISPLAY_NAME"}));Need(!c.ProfileLocked&&!c.ProfileRetry);Need(f.answers.Single(a=>a.questionKey=="DISPLAY_NAME").textValue=="CustomAlias");Need(c.State.revision==7);f.error=null;await c.SaveProfileAsync();Need(f.prepares==2&&c.State.revision==8);}}
  Console.WriteLine("BASIC_PROFILE_CHECKS="+checks+"_PASS");
 }
}
