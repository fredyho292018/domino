using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Domino.Infrastructure.Api;
using Domino.Player;
namespace Domino.UI.AppShell {
 [Serializable] public sealed class ProfileEditDto {
  public string firstName,lastName,displayName,country,preferredLanguage;
  public long profileRevision,preferencesRevision;
 }
 public interface IProfileEditSource:IDisposable {
  Task<ProfileEditDto> Load(CancellationToken token);
  Task<string> Availability(string alias,CancellationToken token);
  Task<ProfileEditDto> Save(ProfileEditDto current,BasicProfileDraft draft,CancellationToken token);
 }
 public sealed class ProfileEditApiSource:IProfileEditSource {
  readonly OnboardingApiSession session;readonly PlayerService player;
  public ProfileEditApiSource(OnboardingApiSession session,PlayerService player){this.session=session;this.player=player;}
  public Task<ProfileEditDto> Load(CancellationToken token)=>session.Send<ProfileEditDto>("GET","player/profile/editable",null,token);
  public Task<string> Availability(string alias,CancellationToken token)=>new OnboardingApiClient(session).AliasAvailabilityAsync(alias,token);
  public async Task<ProfileEditDto> Save(ProfileEditDto current,BasicProfileDraft draft,CancellationToken token){
   var expected=player.Player;session.EnsureCurrent();
   var result=await session.Send<ProfileEditDto>("PUT","player/profile",OnboardingApiSession.Serialize(new {firstName=BasicProfileRules.Normalize(draft.FirstName),lastName=BasicProfileRules.Normalize(draft.LastName),displayName=BasicProfileRules.Normalize(draft.DisplayName),country=draft.Country,preferredLanguage=draft.Language,expectedProfileRevision=current.profileRevision,expectedPreferencesRevision=current.preferencesRevision}),token);
   session.EnsureCurrent();token.ThrowIfCancellationRequested();
   if(result.firstName!=BasicProfileRules.Normalize(draft.FirstName)||result.lastName!=BasicProfileRules.Normalize(draft.LastName)||result.displayName!=BasicProfileRules.Normalize(draft.DisplayName)||result.country!=draft.Country||result.preferredLanguage!=draft.Language||result.profileRevision<current.profileRevision||result.preferencesRevision<current.preferencesRevision)throw new DominoApiException(ApiFailure.Contract);
   if(!player.ReceiveConfirmedProfile(expected,new BasicProfileDto{firstName=result.firstName,lastName=result.lastName,displayName=result.displayName,countryCode=result.country,preferredLocale=result.preferredLanguage},new OnboardingDomainRevisionsDto{profile=result.profileRevision,preferences=result.preferencesRevision}))throw new OperationCanceledException();
   return result;
  }
  public void Dispose()=>session.Dispose();
 }
 public sealed class ProfileEditController:IDisposable {
  readonly IProfileEditSource source;readonly CancellationTokenSource lifetime=new CancellationTokenSource();CancellationTokenSource check;long generation;bool disposed;
  ProfileEditDto original;public BasicProfileDraft Draft{get;private set;}
  public bool Busy{get;private set;} public string Error{get;private set;}=""; public AliasCheckState AliasState{get;private set;}
  public event Action Changed;public event Action Saved;
  static string Key(string s)=>BasicProfileRules.Normalize(s).ToLowerInvariant();
  public bool Dirty=>Draft!=null&&original!=null&&(BasicProfileRules.Normalize(Draft.FirstName)!=original.firstName||BasicProfileRules.Normalize(Draft.LastName)!=original.lastName||BasicProfileRules.Normalize(Draft.DisplayName)!=original.displayName||Draft.Country!=original.country||Draft.Language!=original.preferredLanguage);
  public bool CanSave=>!disposed&&!Busy&&Dirty&&BasicProfileRules.Errors(Draft).Length==0&&AliasState!=AliasCheckState.Checking&&AliasState!=AliasCheckState.Taken&&AliasState!=AliasCheckState.Invalid;
  public ProfileEditController(IProfileEditSource source){this.source=source;}
  public async Task Load(){if(disposed||Busy)return;Busy=true;Changed?.Invoke();try{original=await source.Load(lifetime.Token);lifetime.Token.ThrowIfCancellationRequested();if(original==null||original.displayName==null||original.profileRevision<0||original.preferencesRevision<0)throw new DominoApiException(ApiFailure.Contract);Draft=new BasicProfileDraft{FirstName=original.firstName??"",LastName=original.lastName??"",DisplayName=original.displayName,Country=original.country??"",Language=original.preferredLanguage??""};Error="";}catch(OperationCanceledException){}catch{Error="LOAD_FAILED";}finally{Busy=false;if(!disposed)Changed?.Invoke();}}
  public void Edited(){if(!Busy){Error="";Changed?.Invoke();}}
  public async Task Alias(string value){if(disposed||Busy||Draft==null)return;Draft.DisplayName=value;check?.Cancel();check?.Dispose();var epoch=++generation;Error="";
   if(BasicProfileRules.Errors(Draft).Contains("DISPLAY_NAME")){AliasState=AliasCheckState.Invalid;Changed?.Invoke();return;}
   if(Key(value)==Key(original.displayName)){AliasState=AliasCheckState.UnchangedOrIdle;Changed?.Invoke();return;}
   check=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);var token=check.Token;AliasState=AliasCheckState.Checking;Changed?.Invoke();
   try{await Task.Delay(450,token);var result=await source.Availability(BasicProfileRules.Normalize(value),token);token.ThrowIfCancellationRequested();if(disposed||epoch!=generation)return;if(result!="AVAILABLE"&&result!="TAKEN")throw new DominoApiException(ApiFailure.Contract);AliasState=result=="AVAILABLE"?AliasCheckState.Available:AliasCheckState.Taken;}catch(OperationCanceledException){return;}catch{if(disposed||epoch!=generation)return;AliasState=AliasCheckState.CheckFailed;}Changed?.Invoke();
  }
  public async Task Save(){if(!CanSave)return;Busy=true;Error="";Changed?.Invoke();try{var result=await source.Save(original,Draft,lifetime.Token);lifetime.Token.ThrowIfCancellationRequested();original=result;Saved?.Invoke();}catch(OperationCanceledException){}catch(DominoApiException e){Error=e.ServerErrorCode??"SAVE_FAILED";if(Error=="DISPLAY_NAME_TAKEN")AliasState=AliasCheckState.Taken;}catch{Error="SAVE_FAILED";}finally{Busy=false;if(!disposed)Changed?.Invoke();}}
  public void Dispose(){if(disposed)return;disposed=true;generation++;check?.Cancel();check?.Dispose();lifetime.Cancel();source.Dispose();lifetime.Dispose();Changed=null;Saved=null;}
 }
}
