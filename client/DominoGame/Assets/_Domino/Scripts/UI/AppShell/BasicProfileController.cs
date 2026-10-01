using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Domino.Infrastructure.Api;
namespace Domino.UI.AppShell
{
 public interface IBasicProfileSource {
  object PrepareProfile(OnboardingAnswerDto[] answers,string detectedTimeZone);
  Task<OnboardingStateDto> SaveProfileAsync(object operation,CancellationToken token);
 }
 public sealed class BasicProfileDraft {
  public string FirstName="",LastName="",DisplayName="",Country="",Language="";
 }
 public static class BasicProfileRules {
  // ISO 3166-1 alpha-2 presentation data; never infer a player's selection.
  public static readonly string[] Countries=("AD AE AF AG AI AL AM AO AQ AR AS AT AU AW AX AZ BA BB BD BE BF BG BH BI BJ BL BM BN BO BQ BR BS BT BV BW BY BZ CA CC CD CF CG CH CI CK CL CM CN CO CR CU CV CW CX CY CZ DE DJ DK DM DO DZ EC EE EG EH ER ES ET FI FJ FK FM FO FR GA GB GD GE GF GG GH GI GL GM GN GP GQ GR GS GT GU GW GY HK HM HN HR HT HU ID IE IL IM IN IO IQ IR IS IT JE JM JO JP KE KG KH KI KM KN KP KR KW KY KZ LA LB LC LI LK LR LS LT LU LV LY MA MC MD ME MF MG MH MK ML MM MN MO MP MQ MR MS MT MU MV MW MX MY MZ NA NC NE NF NG NI NL NO NP NR NU NZ OM PA PE PF PG PH PK PL PM PN PR PS PT PW PY QA RE RO RS RU RW SA SB SC SD SE SG SH SI SJ SK SL SM SN SO SR SS ST SV SX SY SZ TC TD TF TG TH TJ TK TL TM TN TO TR TT TV TW TZ UA UG UM US UY UZ VA VC VE VG VI VN VU WF WS YE YT ZA ZM ZW").Split(' ');
  public static string Normalize(string s)=>(s??"").Trim().Normalize(NormalizationForm.FormC);
  public static bool Name(string s){var n=Normalize(s);return new StringInfo(n).LengthInTextElements>=1&&new StringInfo(n).LengthInTextElements<=80&&!n.Any(c=>char.IsControl(c)||(c>='\u202a'&&c<='\u202e')||(c>='\u2066'&&c<='\u2069')||c=='\u200e'||c=='\u200f'||c=='\u061c');}
  public static string[] Errors(BasicProfileDraft d){var r=new List<string>();if(!Name(d.FirstName))r.Add("FIRST_NAME");if(!Name(d.LastName))r.Add("LAST_NAME");if(!Regex.IsMatch(d.DisplayName??"",@"^[A-Za-z0-9_-]{3,16}$")||new[]{"admin","administrator","moderator","support","teamfho","system"}.Contains((d.DisplayName??"").ToLowerInvariant()))r.Add("DISPLAY_NAME");if(!Countries.Contains(d.Country))r.Add("COUNTRY");if(d.Language!="en"&&d.Language!="es")r.Add("PREFERRED_LANGUAGE");return r.ToArray();}
  public static OnboardingAnswerDto[] Answers(BasicProfileDraft d)=>new[]{
   new OnboardingAnswerDto{questionKey="FIRST_NAME",type="TEXT",textValue=Normalize(d.FirstName)},new OnboardingAnswerDto{questionKey="LAST_NAME",type="TEXT",textValue=Normalize(d.LastName)},new OnboardingAnswerDto{questionKey="DISPLAY_NAME",type="TEXT",textValue=d.DisplayName},new OnboardingAnswerDto{questionKey="COUNTRY",type="COUNTRY_SELECT",optionKey=d.Country},new OnboardingAnswerDto{questionKey="PREFERRED_LANGUAGE",type="LOCALE_SELECT",optionKey=d.Language}};
  public static string DetectedZone(){var z=TimeZoneInfo.Local.Id;return z=="UTC"||z.Contains("/")?z:null;}
 }
 public sealed partial class OnboardingShellController {
  BasicProfileDraft draft;object pendingProfile;
  public BasicProfileDraft Profile=>draft;
  public string ProfileFeedback {get;private set;}="";
  public string[] ProfileErrors {get;private set;}=Array.Empty<string>();
  public bool ProfileLocked=>busy||pendingProfile!=null||Phase==OnboardingShellPhase.UpdateRequired;
  public bool ProfileRetry=>pendingProfile!=null&&!busy;
  void InitializeProfile(){if(state?.currentStepKey!="BASIC_PROFILE_STEP")return;var p=state.basicProfile;draft=new BasicProfileDraft{FirstName=p?.firstName??"",LastName=p?.lastName??"",DisplayName=p?.displayName??"",Country=p?.countryCode??"",Language=p?.preferredLocale??""};}
  public async Task ChangeProfileLocaleAsync(string value){
   if(ProfileLocked||disposed||draft==null||(value!="en"&&value!="es"))return;
   draft.Language=value;busy=true;ProfileFeedback="LOCALIZING";Changed?.Invoke();
   try{var c=await source.CatalogAsync(value,lifetime.Token);lifetime.Token.ThrowIfCancellationRequested();Validate(state,c);catalog=Copy(c);Locale=value;ProfileFeedback="";}
   catch(OperationCanceledException){}catch(Exception){ProfileFeedback="LOCALE_ERROR";}
   finally{busy=false;if(!disposed)Changed?.Invoke();}
  }
  public async Task SaveProfileAsync(){
   if(disposed||busy||lifetime.IsCancellationRequested||Phase!=OnboardingShellPhase.InProgress||state?.currentStepKey!="BASIC_PROFILE_STEP"||!(source is IBasicProfileSource writer))return;
   if(pendingProfile==null){ProfileErrors=BasicProfileRules.Errors(draft);if(ProfileErrors.Length>0){ProfileFeedback="VALIDATION";Changed?.Invoke();return;}}
   busy=true;ProfileFeedback="SAVING";ProfileErrors=Array.Empty<string>();Changed?.Invoke();
   try{
    if(pendingProfile==null)pendingProfile=writer.PrepareProfile(BasicProfileRules.Answers(draft),string.IsNullOrEmpty(state.basicProfile?.timeZone)?BasicProfileRules.DetectedZone():null);
    var next=await writer.SaveProfileAsync(pendingProfile,lifetime.Token);lifetime.Token.ThrowIfCancellationRequested();Validate(next,catalog);
    if(next.revision<state.revision)throw new DominoApiException(ApiFailure.Contract);
    state=Copy(next);SynchronizeExperience();pendingProfile=null;ProfileFeedback="";Phase=next.status=="COMPLETED"?OnboardingShellPhase.Completed:OnboardingShellPhase.InProgress;
   }catch(OperationCanceledException){}
   catch(DominoApiException e){
    if(e.ServerErrorCode=="CLIENT_UPDATE_REQUIRED"){pendingProfile=null;Phase=OnboardingShellPhase.UpdateRequired;}
    else if(new[]{"REVISION_MISMATCH","ONBOARDING_REVISION_MISMATCH","DOMAIN_REVISION_MISMATCH","ONBOARDING_CATALOG_VERSION_MISMATCH"}.Contains(e.ServerErrorCode)){
     pendingProfile=null;ProfileFeedback="CONFLICT";
     try{var next=await source.LoadAsync(lifetime.Token);var c=await source.CatalogAsync(Locale,lifetime.Token);lifetime.Token.ThrowIfCancellationRequested();Validate(next,c);if(next.revision<state.revision)throw new InvalidOperationException();state=Copy(next);catalog=Copy(c);Phase=next.status=="COMPLETED"?OnboardingShellPhase.Completed:next.status=="NOT_STARTED"?OnboardingShellPhase.NotStarted:OnboardingShellPhase.InProgress;}
     catch(OperationCanceledException){}catch(Exception){Phase=OnboardingShellPhase.Error;}
    }else if(e.HttpStatus==400){pendingProfile=null;ProfileFeedback="VALIDATION";ProfileErrors=e.ServerErrorCode=="DISPLAY_NAME_INVALID"||e.ServerErrorCode=="DISPLAY_NAME_RESERVED"?new[]{"DISPLAY_NAME"}:e.ServerErrorCode=="LANGUAGE_UNSUPPORTED"?new[]{"PREFERRED_LANGUAGE"}:new[]{"FIRST_NAME","LAST_NAME","DISPLAY_NAME","COUNTRY","PREFERRED_LANGUAGE"};}
    else ProfileFeedback="NETWORK";
   }catch(Exception){ProfileFeedback="NETWORK";}
   finally{busy=false;if(!disposed)Changed?.Invoke();}
  }
 }
}
