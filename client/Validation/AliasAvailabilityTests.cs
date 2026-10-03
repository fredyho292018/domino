using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Domino.Infrastructure.Api;
using Domino.UI.AppShell;
static class AliasAvailabilityTests {
 static int checks;static void Need(bool value){if(!value)throw new Exception("CHECK_"+checks);checks++;}
 sealed class Source:IOnboardingShellSource,IBasicProfileSource,IAliasAvailabilitySource {
  public int checks;public string availability="AVAILABLE";public TaskCompletionSource<string> checkHold;
  public async Task<string> CheckAliasAsync(string alias,CancellationToken token){checks++;if(checkHold!=null)return await checkHold.Task;if(availability=="FAIL")throw new Exception();return availability;}
  public OnboardingStateDto state=new OnboardingStateDto{status="IN_PROGRESS",catalogVersion=2,revision=7,currentStepKey="BASIC_PROFILE_STEP",basicProfile=new BasicProfileDto{displayName="FixturePlayer",timeZone="UTC"}};
  public int prepares,saves,loads;public object last,first;public string zone;public OnboardingAnswerDto[] answers;public string error;
  public Task<OnboardingStateDto> LoadAsync(CancellationToken t){loads++;return Task.FromResult(state);}
  public Task<OnboardingCatalogDto> CatalogAsync(string l,CancellationToken t)=>Task.FromResult(new OnboardingCatalogDto{catalogVersion=2,locale=l,steps=new[]{new OnboardingStepDto{key="BASIC_PROFILE_STEP"},new OnboardingStepDto{key="EXPERIENCE_STEP"}}});
  public object PrepareProfile(OnboardingAnswerDto[] a,string z){prepares++;answers=a;zone=z;return new object();}
  public async Task<OnboardingStateDto> SaveProfileAsync(object op,CancellationToken t){saves++;last=op;if(first==null)first=op;await Task.CompletedTask;if(error=="NETWORK")throw new DominoApiException(ApiFailure.Transport);if(error=="CONFLICT"){state.revision=9;throw new DominoApiException(ApiFailure.Server,409,"REVISION_MISMATCH");}if(error=="TAKEN")throw new DominoApiException(ApiFailure.Server,409,"DISPLAY_NAME_TAKEN");if(error=="ROLLOUT")throw new DominoApiException(ApiFailure.Server,409,"DISPLAY_NAME_RESERVATIONS_NOT_READY");if(error=="VALIDATION")throw new DominoApiException(ApiFailure.Server,400,"DISPLAY_NAME_RESERVED");state.revision++;state.currentStepKey="EXPERIENCE_STEP";return state;}
 }

 static async Task Main(){
 var f=new Source();using(var c=new OnboardingShellController(f)){
 await c.LoadAsync("en");c.Profile.FirstName="Private";c.Profile.LastName="Fixture";c.Profile.Country="ES";c.Profile.Language="en";
 Need(c.AliasCanContinue);
 foreach(var invalid in new[]{"ab","bad name","ADMIN","support",new string('x',17)}){await c.ChangeProfileAliasAsync(invalid);Need(c.AliasState==AliasCheckState.Invalid&&!c.AliasCanContinue);}
 Need(f.checks==0);
 var a=c.ChangeProfileAliasAsync("CandidateA");var b=c.ChangeProfileAliasAsync("CandidateB");Need(c.AliasState==AliasCheckState.Checking&&!c.AliasCanContinue);Need(f.checks==0);await Task.WhenAll(a,b);Need(f.checks==1&&c.AliasState==AliasCheckState.Available&&c.AliasCanContinue);
 f.availability="TAKEN";await c.ChangeProfileAliasAsync("CandidateC");Need(c.AliasState==AliasCheckState.Taken&&!c.AliasCanContinue);await c.SaveProfileAsync();Need(f.saves==0);
 f.availability="FAIL";await c.ChangeProfileAliasAsync("CandidateD");Need(c.AliasState==AliasCheckState.CheckFailed&&c.AliasCanContinue);
 f.availability="AVAILABLE";f.checkHold=new TaskCompletionSource<string>();var stale=c.ChangeProfileAliasAsync("CandidateOld");await Task.Delay(500);var hold=f.checkHold;f.checkHold=null;await c.ChangeProfileAliasAsync("CandidateNew");hold.SetResult("TAKEN");await stale;Need(c.AliasState==AliasCheckState.Available&&c.Profile.DisplayName=="CandidateNew");
 f.error="TAKEN";await c.SaveProfileAsync();Need(c.AliasState==AliasCheckState.Taken&&!c.AliasCanContinue);Need(c.ProfileFeedback=="DISPLAY_NAME_TAKEN"&&!c.ProfileRetry);Need(c.ProfileErrors.SequenceEqual(new[]{"DISPLAY_NAME"}));Need(c.State.currentStepKey=="BASIC_PROFILE_STEP"&&c.State.revision==7);Need(c.Profile.FirstName=="Private"&&c.Profile.LastName=="Fixture"&&c.Profile.Country=="ES"&&c.Profile.Language=="en"&&c.Profile.DisplayName=="CandidateNew");
 await c.ChangeProfileAliasAsync("FixturePlayer");Need(c.AliasCanContinue&&c.AliasState==AliasCheckState.UnchangedOrIdle);
 var late=c.ChangeProfileAliasAsync("DisposeCheck");c.Dispose();await late;
 }
 Console.WriteLine("ALIAS_AVAILABILITY_CLIENT_CHECKS="+checks+"_PASS");
 }
}
