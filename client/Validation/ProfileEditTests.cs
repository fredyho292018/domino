using System;
using System.Threading;
using System.Threading.Tasks;
using Domino.UI.AppShell;
using Domino.Infrastructure.Api;
using Domino.Editor;
using Newtonsoft.Json;
static class ProfileEditTests {
 static int count;static void Need(bool value,string key){count++;if(!value)throw new Exception(key);}
 sealed class Fake:IProfileEditSource {
  public int Saves,Checks,Loads;public bool LoadFails;public string Result="AVAILABLE",Failure;public TaskCompletionSource<bool> Hold;
  public Task<ProfileEditDto> Load(CancellationToken t){Loads++;if(LoadFails)throw new Exception("fixture load failure");return Task.FromResult(new ProfileEditDto{firstName="Ana",lastName="Rivera",displayName="Fixture",country="CU",preferredLanguage="en",profileRevision=1,preferencesRevision=1});}
  public async Task<string> Availability(string a,CancellationToken t){Checks++;if(Hold!=null)await Hold.Task;if(Failure=="CHECK")throw new Exception();return Result;}
  public async Task<ProfileEditDto> Save(ProfileEditDto c,BasicProfileDraft d,CancellationToken t){Saves++;if(Hold!=null)await Hold.Task;t.ThrowIfCancellationRequested();if(Failure!=null)throw new DominoApiException(ApiFailure.Server,409,Failure);return new ProfileEditDto{firstName=d.FirstName,lastName=d.LastName,displayName=d.DisplayName,country=d.Country,preferredLanguage=d.Language,profileRevision=2,preferencesRevision=2};}
  public void Dispose(){}
 }
 sealed class Transport:IApiTransport {
  public TaskCompletionSource<bool> Hold;public int Writes;public int Revision=1;public string Error;
  public async Task<ApiHttpResponse> SendAsync(string method,Uri uri,string body,string bearer,int timeout,CancellationToken token){
   if(method=="PUT"){Writes++;if(Hold!=null)await Hold.Task;if(Error!=null)return new ApiHttpResponse(409,"{\"code\":\""+Error+"\"}");var p=JsonConvert.DeserializeObject<ProfileEditDto>(body);p.profileRevision=Revision;p.preferencesRevision=Revision;return new ApiHttpResponse(200,JsonConvert.SerializeObject(p));}
   return new ApiHttpResponse(200,JsonConvert.SerializeObject(new ProfileEditDto{firstName="Ana",lastName="Rivera",displayName="Fixture",country="CU",preferredLanguage="en"}));
  }
 }
 static async Task Main(){
  var f=new Fake();using(var c=new ProfileEditController(f)){await c.Load();Need(c.Draft.FirstName=="Ana"&&c.Draft.Country=="CU","PREFILL");Need(!c.Dirty&&!c.CanSave,"CLEAN");await c.Save();Need(f.Saves==0,"NOOP");c.Draft.LastName="Updated";c.Edited();Need(c.CanSave,"OWN_ALIAS_VALID");int saved=0;c.Saved+=()=>saved++;await c.Save();Need(saved==1&&f.Saves==1&&!c.Dirty,"SAVE");}
  f=new Fake();using(var c=new ProfileEditController(f)){await c.Load();var first=c.Alias("Other");Need(c.AliasState==AliasCheckState.Checking&&!c.CanSave&&f.Checks==0,"DEBOUNCE");var second=c.Alias("Final");await Task.WhenAll(first,second);Need(f.Checks==1&&c.AliasState==AliasCheckState.Available&&c.CanSave,"AVAILABLE");f.Result="TAKEN";await c.Alias("TakenAlias");Need(c.AliasState==AliasCheckState.Taken&&!c.CanSave,"TAKEN");f.Failure="CHECK";await c.Alias("CheckAlias");Need(c.AliasState==AliasCheckState.CheckFailed&&c.CanSave,"CHECK_FAILED");f.Failure="DISPLAY_NAME_TAKEN";await c.Save();Need(c.AliasState==AliasCheckState.Taken&&c.Draft.DisplayName=="CheckAlias","RACE_FORM_PRESERVED");}
  f=new Fake{Hold=new TaskCompletionSource<bool>()};var pending=new ProfileEditController(f);await pending.Load();pending.Draft.FirstName="Changed";int late=0;pending.Saved+=()=>late++;var save=pending.Save();await pending.Save();Need(f.Saves==1,"DOUBLE_SUBMIT");pending.Dispose();f.Hold.SetResult(true);await save;Need(late==0,"SESSION_DISPOSE_LATE_RESPONSE");
  f=new Fake();using(var c=new ProfileEditController(f)){await c.Load();c.Draft.FirstName="";Need(!c.CanSave,"INVALID");}Need(f.Saves==0,"CANCEL_NO_WRITES");
  using(var fixture=new RoutingCompositionFixture()){
   fixture.CreatedAt="2026-01-01T00:00:00Z";fixture.State("COMPLETED",2);await fixture.Forms.RestoreAsync();
   using var presentation=new PlayerPresentationSource(fixture.Player,fixture.Composition.Router);
   var created=fixture.Player.Player.CreatedAt;var wallet=fixture.Player.Wallet;var entitlement=fixture.Player.Entitlements;
   var transport=new Transport();using var source=new ProfileEditApiSource(new OnboardingApiSession(new DominoApiConfiguration(true,"https://example.test"),fixture,transport,()=>fixture.Session?.Uid,default),fixture.Player);
   var original=await source.Load(default);var d=new BasicProfileDraft{FirstName="Private",LastName="Name",DisplayName="UpdatedAlias",Country="US",Language="es"};
   int notifications=0;presentation.Changed+=()=>notifications++;await source.Save(original,d,default);
   Need(fixture.Player.Player.DisplayName=="UpdatedAlias"&&fixture.Player.Player.CountryCode=="US","PLAYER_REFRESH");
   Need(presentation.Current.DisplayName=="UpdatedAlias","PRESENTATION_REFRESH");Need(notifications>0,"PRESENTATION_NOTIFIED");Need(fixture.Player.Player.CreatedAt==created&&ReferenceEquals(wallet,fixture.Player.Wallet)&&ReferenceEquals(entitlement,fixture.Player.Entitlements),"UNRELATED_SNAPSHOTS_PRESERVED");
   transport.Error="DISPLAY_NAME_TAKEN";try{await source.Save(original,d,default);throw new Exception("MISSING_409");}catch(DominoApiException e){Need(e.ServerErrorCode=="DISPLAY_NAME_TAKEN","HTTP_409_MAPPING");}
   transport.Error=null;transport.Hold=new TaskCompletionSource<bool>();var pendingSave=source.Save(original,d,default);fixture.Session=new Domino.Identity.FirebaseAuthSessionSnapshot("other-fixture",false,true,true);transport.Hold.SetResult(true);try{await pendingSave;throw new Exception("STALE_ACCEPTED");}catch(OperationCanceledException){Need(fixture.Player.Player.DisplayName=="UpdatedAlias","STALE_SAVE_BLOCKED");}
  }
  f=new Fake{LoadFails=true};using(var c=new ProfileEditController(f)){await c.Load();Need(c.Error=="LOAD_FAILED"&&c.Draft==null&&!c.Busy&&!c.CanSave,"LOAD_FAILURE");Need(f.Loads==1&&f.Saves==0,"NO_AUTOMATIC_RETRY");f.LoadFails=false;await c.Load();Need(c.Error==""&&c.Draft!=null&&f.Loads==2,"EXPLICIT_LOAD_RECOVERY");c.Draft.FirstName="Edited";c.Draft.LastName="Preserved";c.Draft.Country="US";c.Draft.Language="es";await c.Alias("PreservedAlias");var draft=c.Draft;f.Failure="SAVE_FAILED";await c.Save();Need(c.Error=="SAVE_FAILED"&&!c.Busy&&ReferenceEquals(draft,c.Draft),"SAVE_FAILURE_RETAINS_DRAFT");Need(c.Draft.FirstName=="Edited"&&c.Draft.LastName=="Preserved"&&c.Draft.DisplayName=="PreservedAlias"&&c.Draft.Country=="US"&&c.Draft.Language=="es","ALL_FIVE_VALUES_PRESERVED");Need(c.CanSave&&f.Loads==2,"SAVE_ERROR_NO_RELOAD");}
  using(var fixture=new RoutingCompositionFixture()){
   fixture.State("COMPLETED",2);await fixture.Forms.RestoreAsync();
   string locale="es";int calls=0;bool ready=false;
   using var binding=new PlayerLocaleBinding(fixture.Player,()=>ready,v=>{locale=v;calls++;});
   Need(calls==0,"LOCALE_WAIT_READY");ready=true;binding.Refresh();Need(locale==fixture.Player.PreferredLocale&&calls==1,"LOCALE_RESTORE");binding.Refresh();Need(calls==1,"LOCALE_NOOP");
   var transport=new Transport();using var source=new ProfileEditApiSource(new OnboardingApiSession(new DominoApiConfiguration(true,"https://example.test"),fixture,transport,()=>fixture.Session?.Uid,default),fixture.Player);
   var original=await source.Load(default);
   var draft=new BasicProfileDraft{FirstName="Ana",LastName="Rivera",DisplayName="Fixture",Country="CU",Language="es"};
   transport.Error="REVISION_MISMATCH";var previous=locale;try{await source.Save(original,draft,default);}catch(DominoApiException){}
   Need(locale==previous,"LOCALE_FAILED_SAVE_UNCHANGED");Need(draft.Language=="es","LOCALE_FAILURE_SELECTION_RETAINED");
   transport.Error=null;await source.Save(original,draft,default);Need(locale=="es","LOCALE_SUCCESS_ES");
   transport.Revision=2;draft.Language="en";transport.Hold=new TaskCompletionSource<bool>();var pendingLocale=source.Save(original,draft,default);Need(locale=="es","LOCALE_NO_OPTIMISTIC_CHANGE");transport.Hold.SetResult(true);await pendingLocale;Need(locale=="en","LOCALE_SUCCESS_EN");
   transport.Revision=3;transport.Hold=new TaskCompletionSource<bool>();draft.Language="es";var stale=source.Save(original,draft,default);fixture.Session=new Domino.Identity.FirebaseAuthSessionSnapshot("other-fixture",false,true,true);transport.Hold.SetResult(true);try{await stale;}catch(OperationCanceledException){}Need(locale=="en","LOCALE_STALE_SESSION_REJECTED");
   binding.Dispose();binding.Refresh();Need(locale=="en","LOCALE_DISPOSED");
  }
  using(var fixture=new RoutingCompositionFixture()){
   string runtime="es";int changes=0;using var binding=new PlayerLocaleBinding(fixture.Player,()=>true,v=>{runtime=v;changes++;});Need(changes==0,"NO_PLAYER_FALLBACK_UNCHANGED");
   fixture.State("COMPLETED",2);await fixture.Forms.RestoreAsync();Need(runtime=="en","LOGIN_EXPLICIT_OVERRIDES_DEVICE");
   // Deliberate absent/unsupported preference fixture, never a production mutation.
   var preference=typeof(Domino.Player.PlayerService).GetProperty("PreferredLocale");preference.SetValue(fixture.Player,null);var prior=changes;binding.Refresh();Need(changes==prior,"MISSING_PREFERENCE_PRESERVES_FALLBACK");
   preference.SetValue(fixture.Player,"xx");binding.Refresh();Need(changes==prior,"UNSUPPORTED_PREFERENCE_PRESERVES_FALLBACK");
  }
  f=new Fake();using(var c=new ProfileEditController(f)){await c.Load();await c.Save();Need(f.Saves==0,"LANGUAGE_SAME_NO_WRITE");c.Draft.Language="es";c.Edited();Need(c.Dirty&&c.CanSave,"LANGUAGE_ONLY_DIRTY_SAVE");}
  Console.WriteLine("PROFILE_EDIT_CLIENT_CHECKS="+count+"_PASS");
 }
}
