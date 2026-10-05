using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Domino.Infrastructure.Api;
using Domino.UI;
using Domino.UI.AppShell;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;

namespace Domino.Editor {
 // Validation-only controls. Uses fake commercial data; never constructs a production API source.
 public sealed class SharedMembershipPreview:EditorWindow {
  const string Request="Library/Membership01.request",Result="Library/Membership01D.result.txt";
  static readonly string[] States={"FREE_TRIAL_AVAILABLE","FREE_NOT_ELIGIBLE","TRIAL_ACTIVE","TRIAL_EXPIRED","PREMIUM_LEGACY","UNAVAILABLE"};
  static readonly Vector2Int[] Sizes={new Vector2Int(375,667),new Vector2Int(393,852),new Vector2Int(412,915),new Vector2Int(430,932),new Vector2Int(480,1040),new Vector2Int(600,960),new Vector2Int(768,1024),new Vector2Int(834,1194)};
  int state=2,size=1,checks,scenarios;string locale="es";bool onboarding,testing,captured,sized;Locale initialLocale;
  string membershipOverride,trialOverride,planOverride,periodOverride,endOverride,reminderOverride,viewedOverride;
  VisualElement frame;ProductionAppShell shell;AppMembershipPage page;Fixture source;OnboardingShellController onboard;ProductionOnboardingRoot onboardView;
  static MembershipCatalogDto Catalog(string language)=>JObject.Parse(File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../../Validation/MembershipCatalogFixture.json"))))[language].ToObject<MembershipCatalogDto>();
  public sealed class Fixture:IAppMembershipSource {
   public bool IsCurrent{get;set;}=true;public int Loads;public int Writes=>0;
   public EntitlementSummaryDto Entitlements{get;set;}
   public TrialEligibilityDto TrialEligibility{get;set;}
   public event Action Changed;public void Notify()=>Changed?.Invoke();
   public Fixture(string state){SetState(state);}
   public void Apply(string membership,string status,string plan,string period,string end,string reminder){
    var snapshot=Entitlements.snapshot;var t=snapshot.trial;
    if(membership!=null)snapshot.membershipPlan=membership;
    if(status!=null){t.trialStatus=status;TrialEligibility.state=status;TrialEligibility.eligible=status=="NOT_STARTED";snapshot.trialActive=status=="ACTIVE";snapshot.trialConsumed=status=="ACTIVE"||status=="EXPIRED";snapshot.plan=snapshot.trialActive||snapshot.membershipPlan=="PREMIUM"?"PREMIUM":"FREE";snapshot.status=snapshot.trialActive?"ACTIVE":status=="EXPIRED"?"EXPIRED":snapshot.membershipPlan=="PREMIUM"?"ACTIVE":"FREE";}
    if(plan!=null)t.trialPlan=plan=="NONE"?null:plan;
    if(period!=null)t.trialBillingPeriod=period=="NONE"?null:period;
    if(end!=null)t.trialEndsAt=end.Length==0?null:end;
    if(reminder!=null)t.reminderAt=reminder.Length==0?null:reminder;
    // Mirror the server fixture contract for the independently selected trial plan.
    var catalog=Catalog("en");var selected=catalog.plans.FirstOrDefault(p=>p.key==t.trialPlan);
    var benefits=JObject.Parse(File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../../Validation/MembershipTrialTargetFixture.json"))))["trial"]["commercialPlanBenefits"].ToObject<CommercialBenefitDto[]>();
    t.commercialPlanBenefits=benefits.Where(b=>selected?.features.Any(f=>f.featureKey==b.key&&f.included)==true).ToArray();
    TrialEligibility.trial=t;snapshot.trialEndsAt=t.trialEndsAt;
   }
   public void SetState(string state){
    Entitlements=new EntitlementSummaryDto{availability=state=="UNAVAILABLE"?"UNAVAILABLE":"AVAILABLE",snapshot=new EffectiveEntitlementsDto{plan=state=="TRIAL_ACTIVE"||state=="PREMIUM_LEGACY"?"PREMIUM":"FREE",status=state=="TRIAL_EXPIRED"?"EXPIRED":state=="TRIAL_ACTIVE"||state=="PREMIUM_LEGACY"?"ACTIVE":"FREE",trialActive=state=="TRIAL_ACTIVE",trialConsumed=state=="TRIAL_ACTIVE"||state=="TRIAL_EXPIRED",features=state=="TRIAL_ACTIVE"||state=="PREMIUM_LEGACY"?new[]{"FULL_HISTORY","FULL_REPLAY","ADVANCED_STATS","PREMIUM_THEMES"}:new[]{"PUBLIC_DUEL","FRIENDS"},trialEndsAt=state=="TRIAL_ACTIVE"||state=="TRIAL_EXPIRED"?"2030-01-08T00:00:00Z":null}};
    TrialEligibility=new TrialEligibilityDto{state=state=="FREE_TRIAL_AVAILABLE"?"NOT_STARTED":state=="TRIAL_ACTIVE"?"ACTIVE":state=="TRIAL_EXPIRED"?"EXPIRED":state=="PREMIUM_LEGACY"?"CONVERTED":state=="UNAVAILABLE"?"UNKNOWN":"INELIGIBLE",eligible=state=="FREE_TRIAL_AVAILABLE",activationMode="EXPLICIT",policyVersion=1,periodDays=7};
    Entitlements.snapshot.membershipPlan=state=="PREMIUM_LEGACY"?"PREMIUM":"FREE";
    Entitlements.snapshot.trial=new TrialStateDto{trialStatus=TrialEligibility.state,legacy=state=="PREMIUM_LEGACY"};
    TrialEligibility.trial=Entitlements.snapshot.trial;
    var configuration=JObject.Parse(File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../../Validation/MembershipTrialTargetFixture.json"))));
    TrialEligibility.periodDays=(int)configuration["durationDays"];
    if(state=="TRIAL_ACTIVE"||state=="TRIAL_EXPIRED"){
     var trial=JObject.Parse(File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../../Validation/MembershipTrialTargetFixture.json"))))["trial"].ToObject<TrialStateDto>();
     trial.trialStatus=state=="TRIAL_ACTIVE"?"ACTIVE":"EXPIRED";
     Entitlements.snapshot.trial=trial;Entitlements.snapshot.membershipPlan="FREE";Entitlements.snapshot.trialEndsAt=trial.trialEndsAt;
     TrialEligibility.trial=trial;
     Entitlements.snapshot.features=state=="TRIAL_ACTIVE"?new[]{"FULL_HISTORY","FULL_REPLAY","PREMIUM_THEMES"}:new[]{"PUBLIC_DUEL","FRIENDS"};
    }
   }
   public Task<MembershipCatalogDto> LoadAsync(string language,CancellationToken token){Loads++;return Task.FromResult(Catalog(language));}
   public void Dispose(){}
  }
  [MenuItem("Domino/Membership/Shared experience (isolated)")]
  public static void Open(){GetWindow<SharedMembershipPreview>().QueueMount();}
  [InitializeOnLoadMethod]static void Requests(){EditorApplication.update+=()=>{
   if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(Request))return;
   var mode=File.ReadAllText(Request).Trim();File.Delete(Request);var w=GetWindow<SharedMembershipPreview>();if(mode=="pricing")w.RunPricing();else if(mode=="validate")w.Run();else if(mode=="polish")w.RunPolish();else if(mode=="available")w.RunAvailable();else if(mode=="offer")w.OpenAvailableOffer();else w.QueueMount();
  };}
  void OnDisable(){Clean();if(captured){LocalizationSettings.SelectedLocale=initialLocale;captured=false;}}
  void Clean(){page?.Dispose();page=null;onboardView?.Dispose();onboard?.Dispose();onboard=null;onboardView=null;rootVisualElement.Clear();}
  bool mountQueued;
  void CreateGUI(){QueueMount();}
  void QueueMount(){
   if(testing||mountQueued)return;mountQueued=true;
   EditorApplication.delayCall+=()=>{if(this==null)return;mountQueued=false;if(!testing)Mount();};
  }
  async void Mount(){try{await MountAsync();}catch(Exception e){File.WriteAllText(Result,"PREVIEW_FAIL="+e.GetType().Name+"\n");}}
  async Task MountAsync(){
   Clean();titleContent=new GUIContent("MEMBERSHIP · ISOLATED");minSize=new Vector2(440,650);
   if(!captured){initialLocale=LocalizationSettings.SelectedLocale;captured=true;}
   LocalizationSettings.SelectedLocale=AssetDatabase.LoadAssetAtPath<Locale>("Assets/_Domino/Localization/"+locale+".asset");
   if(!sized){position=new Rect(120,80,440,1030);sized=true;}
   var states=new PopupField<string>(States.ToList(),state);states.SetEnabled(!testing);states.RegisterValueChangedCallback(_=>{state=states.index;ResetOverrides();Mount();});rootVisualElement.Add(states);
   var locales=new PopupField<string>(new List<string>{"en","es"},locale=="es"?1:0);locales.SetEnabled(!testing);locales.RegisterValueChangedCallback(_=>{locale=locales.value;Mount();});rootVisualElement.Add(locales);
   var sizes=new PopupField<string>(Sizes.Select(s=>s.x+"x"+s.y).ToList(),size);sizes.SetEnabled(!testing);sizes.RegisterValueChangedCallback(_=>{size=sizes.index;Mount();});rootVisualElement.Add(sizes);
   var host=new PopupField<string>(new List<string>{"App Membership","Onboarding Membership"},onboarding?1:0);host.SetEnabled(!testing);host.RegisterValueChangedCallback(_=>{onboarding=host.index==1;Mount();});rootVisualElement.Add(host);
   var dimensions=new Foldout{text="Independent fixture dimensions",value=false};rootVisualElement.Add(dimensions);
   Choice(dimensions,"Membership",new[]{"FREE","PREMIUM"},membershipOverride??(state==4?"PREMIUM":"FREE"),v=>membershipOverride=v);
   Choice(dimensions,"Trial status",new[]{"NOT_STARTED","INELIGIBLE","ACTIVE","EXPIRED","CONVERTED","UNKNOWN"},trialOverride??(state==0?"NOT_STARTED":state==1?"INELIGIBLE":state==2?"ACTIVE":state==3?"EXPIRED":state==4?"CONVERTED":"UNKNOWN"),v=>trialOverride=v);
   Choice(dimensions,"Trial plan",new[]{"NONE","DIAMOND","PLATINUM","GOLD"},planOverride??(state==2||state==3?"DIAMOND":"NONE"),v=>planOverride=v);
   Choice(dimensions,"Trial period",new[]{"NONE","MONTHLY","YEARLY"},periodOverride??(state==2||state==3?"YEARLY":"NONE"),v=>periodOverride=v);
   Instant(dimensions,"Trial end",endOverride??(state==2||state==3?"2026-10-11T12:00:00Z":""),v=>endOverride=v);
   Instant(dimensions,"Reminder",reminderOverride??(state==2||state==3?"2026-10-09T12:00:00Z":""),v=>reminderOverride=v);
   Choice(dimensions,"Viewed plan",new[]{"DIAMOND","PLATINUM","GOLD","FRIENDS_AND_FAMILY"},viewedOverride??"DIAMOND",v=>viewedOverride=v);
   rootVisualElement.Add(new Label("ISOLATED · fixture data · no network · no real writes"));
   frame=new VisualElement{name="MembershipViewport"};frame.style.width=Sizes[size].x;frame.style.height=Sizes[size].y;frame.style.flexShrink=0;rootVisualElement.Add(frame);
   if(!testing){float scale=Mathf.Min(1,Mathf.Min((position.width-4)/Sizes[size].x,(position.height-140)/Sizes[size].y));frame.style.transformOrigin=new TransformOrigin(0,0,0);frame.style.scale=new Scale(new Vector3(Mathf.Max(.1f,scale),Mathf.Max(.1f,scale),1));}
   if(onboarding){
    // Existing approved fixture, reused read-only. No SDK/service implementation in this path.
    var type=typeof(ProductionOnboardingPreview).GetNestedType("Fixture",BindingFlags.NonPublic);
    var fixture=(IOnboardingShellSource)Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,new object[]{"MEMBERSHIP_TRIAL_ELIGIBLE",2},null);
    onboard=new OnboardingShellController(fixture);onboardView=new ProductionOnboardingRoot(onboard);frame.Add(onboardView);onboardView.SetSafeArea(24,24);await onboard.LoadAsync(locale);onboard.SelectMembershipPlan("DIAMOND");
   }else{
    source=new Fixture(States[state]);source.Apply(membershipOverride,trialOverride,planOverride,periodOverride,endOverride,reminderOverride);shell=new ProductionAppShell(membership:()=>new AppMembershipController(source));frame.Add(shell);shell.Select(ShellTab.Menu);shell.OpenMenuDestination(MenuDestination.Membership);page=frame.Q<AppMembershipPage>();await page.Controller.LoadAsync(locale);if(viewedOverride!=null)page.Controller.SelectMembershipPlan(viewedOverride);
   }
   await Task.Delay(140);
  }
  void ResetOverrides(){membershipOverride=trialOverride=planOverride=periodOverride=endOverride=reminderOverride=viewedOverride=null;}
  void Choice(VisualElement parent,string label,string[] values,string value,Action<string> set){var field=new PopupField<string>(label,values.ToList(),value);field.SetEnabled(!testing);field.RegisterValueChangedCallback(e=>{set(e.newValue);QueueMount();});parent.Add(field);}
  void Instant(VisualElement parent,string label,string value,Action<string> set){var field=new TextField(label){value=value,isDelayed=true};field.SetEnabled(!testing);field.RegisterValueChangedCallback(e=>{set(e.newValue);QueueMount();});parent.Add(field);}
  void Need(bool value,string name){if(!value)throw new InvalidOperationException(name);checks++;}
  void Geometry(ScrollView scroll,VisualElement content){
   foreach(var label in content.Query<Label>().ToList()){
    if(label.layout.width<=0||label.resolvedStyle.display==DisplayStyle.None)continue;
    var r=scroll.WorldToLocal(label.worldBound);Need(r.xMin>=-.5f&&r.xMax<=scroll.layout.width+.5f,"HORIZONTAL_OVERFLOW_"+label.name);
    var needed=label.MeasureTextSize(label.text,label.layout.width,VisualElement.MeasureMode.Exactly,0,VisualElement.MeasureMode.Undefined).y;
    Need(label.layout.height>=needed-1,"TEXT_CLIPPING_"+label.name);
   }
  }
  async Task Reachable(ScrollView scroll,VisualElement content){
   foreach(var button in content.Query<Button>().ToList()){
    Need(button.layout.height>=44,"TOUCH_"+button.name);Need(button.focusable,"FOCUS_"+button.name);
    scroll.ScrollTo(button);await Task.Delay(15);
    var r=scroll.WorldToLocal(button.worldBound);var viewport=scroll.WorldToLocal(scroll.contentViewport.worldBound);
    Need(r.yMin>=viewport.yMin-1&&r.yMax<=viewport.yMax+1,"REACHABILITY_"+button.name);
   }
  }
  async void OpenAvailableOffer(){
   if(testing)return;
   ResetOverrides();state=0;size=1;locale="es";onboarding=false;
   planOverride="DIAMOND";periodOverride="YEARLY";viewedOverride="DIAMOND";
   await MountAsync();page.Controller.SelectMembershipPeriod("YEARLY");await Task.Delay(140);
   page.ScrollTo(page.Q("MembershipTrialAction"));
   var c=page.Controller;
   var valid=c.SelectedMembershipPlan=="DIAMOND"&&c.MembershipBillingPeriod=="YEARLY"&&c.State=="FREE_TRIAL_AVAILABLE"&&page.Q<Label>("TrialTitle").text=="Prueba gratuita de Diamond";
   File.WriteAllText("Library/Membership01D.offer.txt","DECLARED_VISIBLE_MATCH="+valid+"\nVIEWED_PLAN="+c.SelectedMembershipPlan+"\nBILLING="+c.MembershipBillingPeriod+"\nSTATE="+c.State+"\nHOST=ISOLATED_APP_MEMBERSHIP\nSIZE=393x852\nLOCALE=es\nREAL_REQUESTS=0\n");
  }
  async void RunPricing(){
   if(testing)return;ResetOverrides();testing=true;checks=scenarios=0;
   const string result="Library/Membership01E.pricing.txt";
   File.WriteAllText(result,"START="+DateTime.UtcNow.ToString("O")+"\n");
   bool passed=false;
   try{
    state=0;onboarding=false;
    foreach(var language in new[]{"en","es"})for(size=0;size<Sizes.Length;size++){
     locale=language;await MountAsync();var c=page.Controller;var authority=OnboardingApiSession.Serialize(source.Entitlements);
     foreach(var plan in new[]{"DIAMOND","PLATINUM","GOLD","FRIENDS_AND_FAMILY"})foreach(var period in new[]{"MONTHLY","YEARLY"}){
      c.SelectMembershipPlan(plan);c.SelectMembershipPeriod(period);await Task.Delay(35);
      var offer=MembershipPricePresentation.Resolve(c.Membership,plan,period);
      Need(offer!=null,"OFFER_RESOLVED");
      Need(page.Q<Label>("MembershipPrice").text==MembershipPricePresentation.Price(offer,locale),"PRICE_MATCHES_BACKEND_DTO");
      Need(page.Q("MembershipStoreUnavailable")!=null&&!c.Membership.pricing.purchasesAvailable,"PRICE_WITHOUT_PURCHASE");
      Need((page.Q("MembershipMonthlyEquivalent")!=null)==(period=="YEARLY"),"EQUIVALENT_DERIVED");
      Need((page.Q("MembershipSavings")!=null)==(period=="YEARLY"),"SAVINGS_DERIVED");
      Need((page.Q("MembershipTrialAction")!=null)==(plan!="FRIENDS_AND_FAMILY"),"FAMILY_NO_TRIAL");
      if(plan!="FRIENDS_AND_FAMILY"){
       Need(page.Q<Label>("TrialPostPrice").text.EndsWith(MembershipPricePresentation.Price(offer,locale)),"POST_TRIAL_SELECTED_OFFER");
       Need(page.Q<Label>("TrialToday").text.EndsWith("$0.00"),"ZERO_TODAY");
       Need(page.Q<Label>("TrialPolicyDuration").text.StartsWith(source.TrialEligibility.periodDays+" "),"CONFIGURED_DURATION");
      }
      Need(page.Q("TrialFirstChargeAt")==null&&page.Q("TrialReminderAt")==null,"NO_BILLING_OR_REMINDER_PROMISE");
      Geometry(page,page);await Reachable(page,page);scenarios++;
      Need(authority==OnboardingApiSession.Serialize(source.Entitlements)&&source.Writes==0,"NO_DOMAIN_WRITES");
     }
     c.Membership.pricing=null;c.SelectMembershipPlan("GOLD");await Task.Delay(35);
     Need(page.Q<Label>("MembershipPrice").text==(locale=="es"?"Precio no disponible":"Price unavailable"),"MISSING_NOT_ZERO");
     Need(page.Q("TrialToday")==null&&page.Q("TrialPostPrice")==null,"NO_MISSING_PRICE_CLAIMS");
     Geometry(page,page);
    }
    passed=true;File.AppendAllText(result,"CHECKS="+checks+"_PASS\nSCENARIOS="+scenarios+"_PASS\nFAIL=0\nREAL_REQUESTS=0\nREAL_WRITES=0\n");
   }catch(Exception e){File.AppendAllText(result,"FAIL="+e.Message+"\n");}
   finally{testing=false;if(passed){OpenAvailableOffer();File.AppendAllText(result,"FINAL=APP_MEMBERSHIP_FREE_AVAILABLE_DIAMOND_YEARLY_393x852_ES\n");}ConsoleCounts();}
  }
  async void RunAvailable(){
   if(testing)return;ResetOverrides();testing=true;checks=scenarios=0;
   const string result="Library/Membership01D.available.txt";
   File.WriteAllText(result,"START="+DateTime.UtcNow.ToString("O")+"\n");
   try{
    state=0;onboarding=false;
    foreach(var language in new[]{"en","es"})for(size=0;size<Sizes.Length;size++){
     locale=language;await MountAsync();var c=page.Controller;
     // Reproduce old eligibility payloads as well: no legacy copy fallback is allowed.
     source.TrialEligibility.trial=null;source.TrialEligibility.periodDays=9;source.Notify();
     foreach(var plan in new[]{"DIAMOND","PLATINUM","GOLD"}){
      c.SelectMembershipPlan(plan);c.SelectMembershipPeriod("YEARLY");await Task.Delay(50);
      var name=c.Membership.plans.Single(p=>p.key==plan).name;
      Need(page.Q<Label>("TrialTitle").text==(locale=="es"?"Prueba gratuita de ":"Free trial of ")+name,"SELECTED_PLAN_OFFER");
      Need(page.Q<Label>("TrialPolicyDuration").text.StartsWith("9 ")&&page.Q<Label>("TrialPolicyDuration").text.EndsWith(name+"."),"CONFIGURED_DURATION_AND_PLAN");
      Need(page.Q<Button>("MembershipTrialAction").text.Contains("9"),"CTA_CONFIGURED_DURATION");
      Need(!page.Query<Label>().ToList().Any(l=>l.text.Contains("Premium")),"NO_GENERIC_PREMIUM");
      foreach(var row in page.Q("MembershipFeatures").Children()){
       Need(row.Q<Label>("FeatureName").text.Contains(locale=="es"?"Próximamente":"Coming soon"),"AVAILABILITY_EXPLICIT");
       var inclusion=row.Q<Label>("FeatureInclusion");
       Need(inclusion.text==(locale=="es"?"Incluido":"Included")&&inclusion.resolvedStyle.backgroundColor.a==0,"INCLUSION_NOT_ACTIVE_GREEN_CHECK");
      }
      Geometry(page,page);await Reachable(page,page);scenarios++;
     }
     c.SelectMembershipPlan("FRIENDS_AND_FAMILY");await Task.Delay(30);Need(page.Q("MembershipTrialAction")==null,"FAMILY_NO_TRIAL");Need(source.Writes==0,"NO_WRITES");
    }
    File.AppendAllText(result,"CHECKS="+checks+"_PASS\nSCENARIOS="+scenarios+"_PASS\nFAIL=0\nREAL_REQUESTS=0\nREAL_WRITES=0\n");
   }catch(Exception e){File.AppendAllText(result,"FAIL="+e.Message+"\n");}
   finally{testing=false;ResetOverrides();state=0;size=1;locale="es";onboarding=false;await MountAsync();page.Controller.SelectMembershipPeriod("YEARLY");await Task.Delay(140);page.ScrollTo(page.Q("MembershipTrialAction"));File.AppendAllText(result,"FINAL=APP_MEMBERSHIP_FREE_AVAILABLE_DIAMOND_YEARLY_393x852_ES\n");ConsoleCounts();}
  }
  async void RunPolish(){
   if(testing)return;ResetOverrides();testing=true;checks=scenarios=0;
   const string result="Library/Membership01D.polish.txt";
   File.WriteAllText(result,"START="+DateTime.UtcNow.ToString("O")+"\n");
   try{
    size=1;onboarding=false;
    foreach(var language in new[]{"en","es"})foreach(var trialState in new[]{2,3}){
     locale=language;state=trialState;await MountAsync();
     var before=OnboardingApiSession.Serialize(source.Entitlements);
     var date=page.Q<Label>("TrialEndsAt");
     Need(date!=null&&date.text.EndsWith(locale=="es"?"11 de octubre de 2026":"October 11, 2026"),"LOCALIZED_END");
     Need(!page.Query<Label>().ToList().Any(l=>l.text.Contains("UTC")||l.text.Contains("Recordatorio")||l.text.Contains("Reminder")||l.text.Contains("recordatorio")),"NO_RAW_UTC_OR_REMINDER_COPY");
     Need(page.Q("TrialReminderAt")==null&&page.Q("TrialReminderAvailability")==null,"NO_REMINDER_PROMISE");
     Need(source.Entitlements.snapshot.trial.reminderAt=="2026-10-09T12:00:00Z","DOMAIN_REMINDER_PRESERVED");
     Need(page.Q<Label>("MembershipCurrentState").text==MembershipStatePresentation.MembershipIdentity(source.Entitlements,locale),"IDENTITY_PRESERVED");
     Geometry(page,page);await Reachable(page,page);
     Need(before==OnboardingApiSession.Serialize(source.Entitlements)&&source.Writes==0,"NO_DOMAIN_MUTATION");scenarios++;
    }
    File.AppendAllText(result,"CHECKS="+checks+"_PASS\nSCENARIOS="+scenarios+"_PASS\nFAIL=0\nREAL_REQUESTS=0\nREAL_WRITES=0\n");
   }catch(Exception e){File.AppendAllText(result,"FAIL="+e.Message+"\n");}
   finally{testing=false;ResetOverrides();state=2;size=1;locale="es";onboarding=false;await MountAsync();File.AppendAllText(result,"FINAL=APP_MEMBERSHIP_FREE_ACTIVE_DIAMOND_YEARLY_393x852_ES\n");ConsoleCounts();}
  }
  async void Run(){
   if(testing)return;ResetOverrides();testing=true;checks=scenarios=0;File.WriteAllText(Result,"START="+DateTime.UtcNow.ToString("O")+"\n");
   var previous=LocalizationSettings.SelectedLocale;
   try{
    foreach(var language in new[]{"en","es"})for(size=0;size<Sizes.Length;size++){
     locale=language;onboarding=false;
     for(state=0;state<States.Length;state++){
      await MountAsync();var c=page.Controller;var authority=OnboardingApiSession.Serialize(source.Entitlements);var view=page.Q<SharedMembershipExperience>();
      Need(page.layout.width==Sizes[size].x,"ACTUAL_WIDTH");Need(c.State==States[state],"STATE");Need(page.Q("MembershipNotNow")==null,"NO_APP_SKIP");Need(page.Q("SubpageMessage")==null,"NO_PLACEHOLDER");
      Need(view!=null,"SHARED_APP_VIEW");Need((view.Q<Button>("MembershipTrialAction")!=null)==(state==0),"TRIAL_CTA_VISIBILITY");
      if(state==4){Need(page.Query<Label>("EffectiveFeature").ToList().Select(l=>(string)l.userData).SequenceEqual(source.Entitlements.snapshot.features),"EFFECTIVE_FEATURE_AUTHORITY");}
      if(state==2){Need(page.Q("TrialEndsAt")!=null&&page.Q("MembershipTrialStatus")!=null,"ACTIVE_STATUS_AND_DATE");Need(page.Query<Label>("TrialCommercialBenefit").ToList().Count==source.Entitlements.snapshot.trial.commercialPlanBenefits.Length,"PLAN_BOUND_COMMERCIAL_BENEFITS");Need(page.Q<Label>("MembershipCurrentState").text==MembershipStatePresentation.MembershipIdentity(source.Entitlements,locale),"MEMBERSHIP_TRIAL_SEPARATE");}
      if(state==5){Need(page.Q("ActiveTrialCard")==null,"UNAVAILABLE_NO_TRIAL");}if(state==5)Need(!page.Q<Label>("MembershipCurrentState").text.Contains("Gratis")&&!page.Q<Label>("MembershipCurrentState").text.Contains("Free"),"UNAVAILABLE_NOT_FREE");
      if(state==2||state==3){
       Need(page.Q("ActiveTrialCard")!=null,"DISTINCT_CARD");
       Need(page.Q<Label>("TrialPlan").text.EndsWith(c.Membership.plans.Single(p=>p.key=="DIAMOND").name),"LOCALIZED_TRIAL_PLAN");
       Need(page.Q<Label>("TrialBillingPeriod").text.EndsWith(MembershipStatePresentation.BillingPeriod("YEARLY",locale)),"ANNUAL_LOCALIZATION");
       Need(page.Q("TrialReminderAt")==null&&page.Q("TrialReminderAvailability")==null,"NO_REMINDER_PROMISE");
       Need(page.Query<Label>("TrialCommercialBenefit").ToList().All(l=>l.text.Contains(locale=="es"?"Próximamente":"Coming soon")),"NO_UNIMPLEMENTED_ACCESS");
      }
      if(state==2||state==3||state==4){
       foreach(var plan in new[]{"PLATINUM","GOLD","FRIENDS_AND_FAMILY","DIAMOND"}){
        c.SelectMembershipPlan(plan);await Task.Delay(25);
        Need(c.State==States[state]&&page.Q("MembershipTrialAction")==null,"EFFECTIVE_STATE_INDEPENDENT_OF_TAB");
        Need(OnboardingApiSession.Serialize(source.Entitlements)==authority,"EFFECTIVE_ENTITLEMENTS_UNCHANGED");
        if(state==4)Need(page.Query<Label>("EffectiveFeature").ToList().Select(l=>(string)l.userData).SequenceEqual(source.Entitlements.snapshot.features),"ACTIVE_BENEFITS_NOT_CATALOG");
       }
      }
      Need(view.Query<Button>().ToList().Count(b=>b.name.StartsWith("Plan_"))==4,"FOUR_TABS");
      Geometry(page,page);await Reachable(page,page);Need(source.Writes==0,"HARNESS_ZERO_WRITES");scenarios++;
      if(state==0){
       foreach(var plan in new[]{"GOLD","PLATINUM","FRIENDS_AND_FAMILY","DIAMOND"}){c.SelectMembershipPlan(plan);await Task.Delay(40);Geometry(page,page);Need((page.Q("MembershipTrialAction")!=null)==(plan!="FRIENDS_AND_FAMILY"),"FAMILY_NO_TRIAL");if(plan=="FRIENDS_AND_FAMILY"){await Reachable(page,page);scenarios++;}Need(c.SelectedMembershipPlan==plan,"SELECT_PLAN");Need(OnboardingApiSession.Serialize(source.Entitlements)==authority,"TABS_DONT_CHANGE_AUTHORITY");}
       c.SelectMembershipPeriod("YEARLY");await Task.Delay(40);Need(c.MembershipBillingPeriod=="YEARLY"&&source.Writes==0,"BILLING_PRESENTATION_ONLY");
       Need(page.Q<Button>("MembershipTrialAction").text.Contains(source.TrialEligibility.periodDays.ToString()),"CONFIGURED_DURATION");
       Need(page.Q<Button>("Billing_YEARLY").text==(locale=="es"?"Anual":"Annual"),"YEARLY_UI");Geometry(page,page);await Reachable(page,page);scenarios++;
       var appFeatures=page.Q("MembershipFeatures").Children().Select(r=>r.Q<Label>("FeatureName").text).ToArray();
       onboarding=true;await MountAsync();var onboardContent=onboardView.Q<ProductionMembershipView>();
       Need(onboardContent is SharedMembershipExperience,"SHARED_ONBOARDING_VIEW");Need(onboardContent.Q("MembershipNotNow")!=null,"ONBOARDING_SKIP");
       Need(appFeatures.SequenceEqual(onboardContent.Q("MembershipFeatures").Children().Select(r=>r.Q<Label>("FeatureName").text)),"FEATURE_PARITY");
       Geometry(onboardView,onboardView);await Reachable(onboardView,onboardView);scenarios++;onboarding=false;
      }
     }
    }
    // Independent fixture edits: no dimensions are inferred from the viewed tab.
    state=2;size=1;locale="en";onboarding=false;membershipOverride="PREMIUM";planOverride="GOLD";periodOverride="MONTHLY";endOverride="2026-10-20T12:00:00Z";reminderOverride="";viewedOverride="DIAMOND";await MountAsync();
    Need(source.Entitlements.snapshot.membershipPlan=="PREMIUM"&&source.Entitlements.snapshot.trial.trialPlan=="GOLD"&&page.Controller.SelectedMembershipPlan=="DIAMOND","INDEPENDENT_PLAN_DIMENSIONS");
    Need(page.Q("TrialReminderAt")==null&&page.Q<Label>("TrialEndsAt").text.Contains("October 20, 2026"),"INDEPENDENT_TIMELINE");
    Need(page.Q<Label>("TrialBillingPeriod").text.EndsWith("Monthly"),"INDEPENDENT_TRIAL_BILLING");
    Need(source.Entitlements.snapshot.trial.commercialPlanBenefits.Length==Catalog("en").plans.Single(p=>p.key=="GOLD").features.Count(f=>f.included),"TRIAL_PLAN_BENEFITS_NOT_VIEWED_PLAN");
    ResetOverrides();state=0;size=1;locale="en";onboarding=false;await MountAsync();
    int intents=0;page.Controller.StartTrialRequested+=()=>intents++;await page.Controller.ActivateMembershipTrialAsync();
    Need(intents==1&&source.Writes==0&&page.Controller.State=="FREE_TRIAL_AVAILABLE","INTERCEPTED_INTENT_NO_ACTIVATION");
    foreach(var value in new[]{"FREE_NOT_ELIGIBLE","FREE_TRIAL_AVAILABLE","TRIAL_ACTIVE","TRIAL_EXPIRED","PREMIUM_LEGACY","UNAVAILABLE"}){
     source.SetState(value);source.Notify();await Task.Delay(60);Need(page.Controller.State==value,"PRESENTATION_TRANSITION");Need(source.Writes==0,"TRANSITION_NO_WRITE");
    }
    source.SetState("FREE_TRIAL_AVAILABLE");source.Notify();

    LocalizationSettings.SelectedLocale=AssetDatabase.LoadAssetAtPath<Locale>("Assets/_Domino/Localization/es.asset");await Task.Delay(180);
    Need(page.Controller.Locale=="es"&&page.Q<Label>("MembershipTitle").text=="Membresía","RUNTIME_LOCALE_EVENT");
    source.IsCurrent=false;source.Notify();await Task.Delay(40);Need(page.Controller.Membership==null&&page.Q<SharedMembershipExperience>()==null,"SESSION_INVALIDATION_CLEARS_VIEW");
    shell.Back();Need(!shell.HasSubpage&&shell.ActiveTab==ShellTab.Menu,"BACK_MENU");
    File.AppendAllText(Result,"CHECKS="+checks+"_PASS\nSCENARIOS="+scenarios+"_PASS\nFAIL=0\nREAL_REQUESTS=0\nREAL_WRITES=0\n");
   }catch(Exception e){File.AppendAllText(Result,"FAIL="+e.Message+"\n"+e.StackTrace+"\n");}
   finally{Clean();LocalizationSettings.SelectedLocale=previous;testing=false;ResetOverrides();state=2;size=1;locale="es";onboarding=false;await MountAsync();File.AppendAllText(Result,"FINAL=APP_MEMBERSHIP_TRIAL_ACTIVE_393x852_ES\n");ConsoleCounts();}
  }
  static void ConsoleCounts(){
   var logs=typeof(EditorApplication).Assembly.GetType("UnityEditor.LogEntries");var method=logs?.GetMethod("GetCountsByType",BindingFlags.Public|BindingFlags.Static);var args=new object[]{0,0,0};method?.Invoke(null,args);
   File.AppendAllText(Result,"CONSOLE_ERRORS="+args[0]+"\nCONSOLE_WARNINGS="+args[1]+"\nCONSOLE_LOGS="+args[2]+"\n");
  }
 }
}
