using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Domino.Infrastructure.Api;
using Domino.UI.AppShell;
using Newtonsoft.Json.Linq;

static class SharedMembershipTests {
 static int checks;static void Need(bool v,string key){if(!v)throw new Exception(key);checks++;}
 sealed class Wire:IApiTransport {
  public readonly System.Collections.Generic.List<string> Requests=new System.Collections.Generic.List<string>();
  public Task<ApiHttpResponse> SendAsync(string method,Uri uri,string body,string bearer,int timeout,CancellationToken token){
   Requests.Add(method+" "+uri.AbsolutePath);
   if(method!="GET")throw new Exception("UNEXPECTED_WRITE");
   if(uri.AbsolutePath.EndsWith("/membership/catalog"))return Task.FromResult(new ApiHttpResponse(200,JObject.Parse(File.ReadAllText("client/Validation/MembershipCatalogFixture.json"))["en"].ToString()));
   if(uri.AbsolutePath.EndsWith("/player/entitlements"))return Task.FromResult(new ApiHttpResponse(200,"{\"availability\":\"AVAILABLE\",\"snapshot\":{\"plan\":\"PREMIUM\",\"status\":\"ACTIVE\",\"trialActive\":false,\"trialConsumed\":true}}"));
   throw new Exception("UNEXPECTED_READ");
  }
 }
 sealed class Source:IAppMembershipSource {
  public bool IsCurrent{get;set;}=true;public bool Fail;public int Loads;public int Writes=>0;public TaskCompletionSource<bool> Hold;
  public EntitlementSummaryDto Entitlements{get;set;}=new EntitlementSummaryDto{availability="AVAILABLE",snapshot=new EffectiveEntitlementsDto{plan="FREE",status="FREE"}};
  public TrialEligibilityDto TrialEligibility{get;set;}=new TrialEligibilityDto{eligible=true,state="NOT_STARTED",activationMode="EXPLICIT",policyVersion=1,periodDays=7};
  public event Action Changed;public void Notify()=>Changed?.Invoke();
  public async Task<MembershipCatalogDto> LoadAsync(string locale,CancellationToken token){Loads++;if(Hold!=null)await Hold.Task;if(Fail)throw new Exception();return JObject.Parse(File.ReadAllText("client/Validation/MembershipCatalogFixture.json"))[locale].ToObject<MembershipCatalogDto>();}
  public void Dispose(){}
 }
 static async Task Main(){
  await MembershipPricingTests.Run();
  var target=JObject.Parse(File.ReadAllText("client/Validation/MembershipTrialTargetFixture.json"))["trial"].ToObject<TrialStateDto>();
  Need(target.trialPlan=="DIAMOND"&&target.trialBillingPeriod=="YEARLY"&&!target.legacy,"PLAN_BOUND_DTO");
  Need(target.trialStartedAt!=null&&target.reminderAt!=null&&target.trialEndsAt!=null,"TIMELINE_DTO");
  foreach(var benefit in target.commercialPlanBenefits)Need(!benefit.currentlyUsable&&benefit.implementationStatus!="IMPLEMENTED","COMMERCIAL_NOT_FUNCTIONAL");
  var legacy=JObject.Parse("{\"availability\":\"AVAILABLE\",\"snapshot\":{\"plan\":\"PREMIUM\"}}").ToObject<EntitlementSummaryDto>();
  Need(legacy.snapshot.trial==null&&legacy.snapshot.membershipPlan==null,"LEGACY_NULL_SAFE");
  var bound=new EntitlementSummaryDto{availability="AVAILABLE",snapshot=new EffectiveEntitlementsDto{plan="PREMIUM",membershipPlan="FREE",status="ACTIVE",trialActive=true,trialConsumed=true,trial=target}};
  Need(MembershipStatePresentation.Classify(bound,null)=="TRIAL_ACTIVE","BOUND_ACTIVE");
  Need(MembershipStatePresentation.MembershipIdentity(bound,"en")=="Your membership: Free","IDENTITY_SEPARATE");
  Need(MembershipStatePresentation.TimelineDate(target.trialEndsAt,"es")=="11 de octubre de 2026","ES_LOCALIZED_TIMELINE");
  Need(MembershipStatePresentation.TimelineDate(target.trialEndsAt,"en")=="October 11, 2026","EN_LOCALIZED_TIMELINE");
  bound.snapshot.trialActive=false;bound.snapshot.plan="FREE";bound.snapshot.status="EXPIRED";target.trialStatus="EXPIRED";
  Need(MembershipStatePresentation.Classify(bound,null)=="TRIAL_EXPIRED"&&target.trialPlan=="DIAMOND","BOUND_EXPIRED_RETAINS_PLAN");
  foreach(var language in new[]{"en","es"}){
   Need(MembershipStatePresentation.BillingPeriod("YEARLY",language)==(language=="es"?"Anual":"Annual"),"ANNUAL_DISPLAY");
   Need(MembershipStatePresentation.BillingPeriod("MONTHLY",language)==(language=="es"?"Mensual":"Monthly"),"MONTHLY_DISPLAY");
   Need(MembershipStatePresentation.BillingPeriod("INVALID",language)=="—","UNKNOWN_PERIOD_NOT_MONTHLY");
   Need(MembershipStatePresentation.TimelineDate(null,language)=="—"&&MembershipStatePresentation.TimelineDate("invalid",language)=="—","NO_FABRICATED_TIMELINE");
   Need(!MembershipStatePresentation.Copy("TRIAL_EXPIRED",language).Contains(language=="es"?"finalizada":"ended"),"EXPIRED_NOT_IDENTITY");
  }
  foreach(var status in new[]{"IMPLEMENTED","PARTIALLY_IMPLEMENTED","NOT_IMPLEMENTED","FUTURE",null})foreach(var usable in new[]{true,false})
   Need(MembershipStatePresentation.CapabilityUsable(status,usable)==(status=="IMPLEMENTED"&&usable),"IMPLEMENTATION_AND_AUTHORITY_REQUIRED");
  foreach(var key in new[]{"DIAMOND","PLATINUM","GOLD"})foreach(var status in new[]{"ACTIVE","EXPIRED"}){
   target.trialPlan=key;target.trialStatus=status;bound.snapshot.plan="FREE";bound.snapshot.trialActive=false;
   Need(MembershipStatePresentation.Classify(bound,null)=="TRIAL_"+status,"NEW_TRIAL_AUTHORITY_INDEPENDENT_OF_LEGACY_FLAGS");
   Need(MembershipStatePresentation.MembershipIdentity(bound,"en")=="Your membership: Free","TRIAL_NOT_OWNERSHIP");
  }
  target.trialPlan="FRIENDS_AND_FAMILY";Need(MembershipStatePresentation.Classify(bound,null)=="UNAVAILABLE","UNSUPPORTED_FAMILY_TRIAL");
  target.trialPlan="DIAMOND";target.trialBillingPeriod="INVALID";Need(MembershipStatePresentation.Classify(bound,null)=="UNAVAILABLE","MALFORMED_BINDING_FAILS_CLOSED");
  target.trialBillingPeriod="YEARLY";bound.availability="UNAVAILABLE";Need(MembershipStatePresentation.Classify(bound,null)=="UNAVAILABLE","UNAVAILABLE_NO_STALE_TRIAL");
  bound.availability="AVAILABLE";
  var f=new Source();using(var c=new AppMembershipController(f)){
   await c.LoadAsync("en");Need(c.State=="FREE_TRIAL_AVAILABLE"&&c.CanActivateTrial,"FREE_AVAILABLE");Need(f.Writes==0,"OPEN_NO_WRITE");
   c.SelectMembershipPlan("GOLD");c.SelectMembershipPeriod("YEARLY");await c.LoadAsync("es");Need(c.SelectedMembershipPlan=="GOLD"&&c.MembershipBillingPeriod=="YEARLY"&&c.Locale=="es","LOCALE_SELECTION");Need(c.Membership.features[0].name=="Análisis de partidas","SERVER_LOCALE");Need(f.Writes==0,"SELECTION_NO_WRITE");
   c.SelectMembershipPlan("FRIENDS_AND_FAMILY");await c.ActivateMembershipTrialAsync();Need(f.Writes==0&&!c.CanActivateTrial,"FAMILY_BLOCKED");c.SelectMembershipPlan("DIAMOND");
   f.TrialEligibility.eligible=false;f.TrialEligibility.state="INELIGIBLE";Need(c.State=="FREE_NOT_ELIGIBLE"&&!c.CanActivateTrial,"FREE_INELIGIBLE");await c.ActivateMembershipTrialAsync();Need(f.Writes==0,"INELIGIBLE_NO_WRITE");
   f.TrialEligibility.eligible=true;f.Entitlements.snapshot.trialConsumed=true;f.Entitlements.snapshot.status="EXPIRED";Need(c.State=="TRIAL_EXPIRED"&&!c.CanActivateTrial,"CONSUMED_BEATS_STALE_ELIGIBILITY");
   f.Entitlements.snapshot.plan="PREMIUM";f.Entitlements.snapshot.status="ACTIVE";Need(c.State=="PREMIUM_LEGACY"&&!c.CanActivateTrial,"NO_COMMERCIAL_REMAP");f.Entitlements.snapshot.trialActive=true;Need(c.State=="TRIAL_ACTIVE"&&!c.CanActivateTrial,"TRIAL_ACTIVE");
   f.Entitlements.snapshot.trialActive=false;f.Entitlements.snapshot.plan="DIAMOND";Need(c.State=="UNAVAILABLE"&&!c.CanActivateTrial,"UNSUPPORTED_EFFECTIVE_PLAN");
   f.Entitlements.availability="UNAVAILABLE";Need(c.State=="UNAVAILABLE"&&!c.CanActivateTrial,"NO_FREE_FALLBACK");
   f.IsCurrent=false;f.Notify();Need(c.Membership==null&&c.MembershipEntitlements==null&&!c.CanActivateTrial,"SESSION_CLEARS");Need(f.Writes==0,"READ_ONLY_MATRIX");
  }
  f=new Source{Hold=new TaskCompletionSource<bool>()};using(var c=new AppMembershipController(f)){var load=c.LoadAsync("en");f.IsCurrent=false;f.Hold.SetResult(true);await load;Need(c.Membership==null&&c.Error=="SESSION_UNAVAILABLE","STALE_LOAD_REJECTED");}
  f=new Source{Hold=new TaskCompletionSource<bool>()};using(var c=new AppMembershipController(f)){var en=c.LoadAsync("en");var es=c.LoadAsync("es");f.Hold.SetResult(true);await Task.WhenAll(en,es);Need(c.Locale=="es"&&c.Membership.resolvedLocale=="es","LATEST_LOCALE_WINS");}
  f=new Source{Fail=true};using(var c=new AppMembershipController(f)){await c.LoadAsync("en");Need(c.Membership==null&&c.Error=="LOAD_FAILED"&&f.Loads==1,"NO_AUTO_RETRY");f.Fail=false;await c.LoadAsync("en");Need(c.Membership!=null&&f.Writes==0,"EXPLICIT_LOAD_RETRY");}
  f=new Source();using(var c=new AppMembershipController(f)){await c.LoadAsync("en");int intents=0;c.StartTrialRequested+=()=>intents++;var before=OnboardingApiSession.Serialize(f.Entitlements);await c.ActivateMembershipTrialAsync();Need(intents==1&&f.Writes==0,"INTENT_INTERCEPTED_NO_ACTIVATION");Need(c.State=="FREE_TRIAL_AVAILABLE"&&before==OnboardingApiSession.Serialize(f.Entitlements),"INTENT_NOT_ENTITLEMENT");}
  f=new Source{Hold=new TaskCompletionSource<bool>()};using(var c=new AppMembershipController(f)){var load=c.LoadAsync("en");Need(c.State=="UNAVAILABLE"&&!c.CanActivateTrial,"LOADING_NOT_FREE");f.Hold.SetResult(true);await load;f.TrialEligibility=null;Need(c.State=="UNAVAILABLE","MISSING_ELIGIBILITY_NOT_FREE");f.TrialEligibility=new TrialEligibilityDto{state="UNKNOWN"};Need(c.State=="UNAVAILABLE","UNKNOWN_ELIGIBILITY_NOT_FREE");f.Entitlements.snapshot=null;Need(c.State=="UNAVAILABLE","MISSING_ENTITLEMENTS_NOT_FREE");}
  f=new Source{Fail=true};using(var c=new AppMembershipController(f)){await c.LoadAsync("en");Need(c.State=="UNAVAILABLE","FAILED_LOAD_NOT_FREE");}
  foreach(var state in new[]{"FREE_NOT_ELIGIBLE","FREE_TRIAL_AVAILABLE","TRIAL_ACTIVE","TRIAL_EXPIRED","PREMIUM_LEGACY","UNAVAILABLE"}){
   f=new Source();var e=f.Entitlements.snapshot;
   if(state=="FREE_NOT_ELIGIBLE"){f.TrialEligibility.eligible=false;f.TrialEligibility.state="INELIGIBLE";}
   if(state=="TRIAL_ACTIVE"||state=="PREMIUM_LEGACY"){e.plan="PREMIUM";e.status="ACTIVE";e.trialActive=state=="TRIAL_ACTIVE";e.features=new[]{"FULL_HISTORY","PREMIUM_THEMES"};}
   if(state=="TRIAL_EXPIRED"){e.status="EXPIRED";e.trialConsumed=true;}
   if(state=="UNAVAILABLE")f.Entitlements.availability="UNAVAILABLE";
   using var c=new AppMembershipController(f);await c.LoadAsync("en");var authority=OnboardingApiSession.Serialize(new{f.Entitlements,f.TrialEligibility});
   foreach(var plan in new[]{"DIAMOND","PLATINUM","GOLD","FRIENDS_AND_FAMILY"}){c.SelectMembershipPlan(plan);c.SelectMembershipPeriod("YEARLY");Need(c.State==state,"PLAN_STATE_INDEPENDENCE");Need(authority==OnboardingApiSession.Serialize(new{f.Entitlements,f.TrialEligibility}),"PLAN_AND_BILLING_NO_MUTATION");}
  }
  using(var c=new AppMembershipController(null)){await c.LoadAsync("en");Need(c.Membership==null&&c.State=="UNAVAILABLE","NO_DEMO_SOURCE");}
  using(var fixture=new Domino.Editor.RoutingCompositionFixture()){
   fixture.State("COMPLETED",2);await fixture.Forms.RestoreAsync();var wire=new Wire();
   using var c=new AppMembershipController(new AppMembershipApiSource(new OnboardingApiSession(new DominoApiConfiguration(true,"https://example.test"),fixture,wire,()=>fixture.Session?.Uid,default),fixture.Player));
   var bootstrap=fixture.BootstrapCalls;await c.LoadAsync("en");Need(wire.Requests.Count==2&&wire.Requests[0]=="GET /api/v1/membership/catalog"&&wire.Requests[1]=="GET /api/v1/player/entitlements","EXISTING_READ_CONTRACTS_ONLY");
   Need(c.State=="PREMIUM_LEGACY"&&ReferenceEquals(c.MembershipEntitlements,fixture.Player.Entitlements),"SINGLE_PLAYER_AUTHORITY");Need(fixture.BootstrapCalls==bootstrap,"NO_REBOOTSTRAP");
   fixture.Session=null;Need(c.Membership==null&&c.MembershipEntitlements==null,"REAL_SOURCE_SESSION_ISOLATION");
  }
  foreach(var locale in new[]{"en","es"})foreach(var state in new[]{"FREE_NOT_ELIGIBLE","FREE_TRIAL_AVAILABLE","TRIAL_ACTIVE","TRIAL_EXPIRED","PREMIUM_LEGACY","UNAVAILABLE"})Need(!string.IsNullOrEmpty(MembershipStatePresentation.Copy(state,locale)),"STATE_COPY");
  Console.WriteLine("SHARED_MEMBERSHIP_CHECKS="+checks+"_PASS");
 }
}
