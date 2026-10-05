using System;
using System.Linq;
using Domino.Infrastructure.Api;
using Domino.UI.Theming;
using UnityEngine;
using UnityEngine.UIElements;
namespace Domino.UI.AppShell {
 public class SharedMembershipExperience:VisualElement {
  readonly IMembershipPresentation controller;
  bool Es=>controller.Locale=="es";
  // Presentation-only mapping from approved MockShellView PremiumFeatures; inclusion stays catalog-owned.
  public static Color FeatureIconColor(string key){var colors=ThemeProvider.Current.Colors;switch(key){
   case "GAME_REVIEW":case "COACH_GAMES":return colors.Primary;
   case "MOVE_EXPLANATIONS":return UiKit.Hex("56BDB5");
   case "ADVANCED_STATS":return UiKit.Hex("64A5DB");
   case "PUZZLES":return UiKit.Hex("D99B5B");
   case "LESSONS":return UiKit.Hex("63B4CF");
   case "BOTS":return UiKit.Hex("A6B9CB");
   case "NO_ADS":return UiKit.Hex("CA8080");
   default:return colors.IconInactive;
  }}
  protected Label Text(VisualElement parent,string value,string key,TextRole role=TextRole.Body){var label=new Label(value){name=key};ThemeStyles.Text(label,role);label.style.whiteSpace=WhiteSpace.Normal;label.style.flexShrink=0;label.style.marginBottom=8;parent.Add(label);return label;}
  protected ThemeButton Action(VisualElement parent,string copy,string key,Action action,bool enabled=true,bool primary=false){var button=new ThemeButton(copy,action,primary){name=key};button.style.whiteSpace=WhiteSpace.Normal;if(primary)button.style.marginLeft=button.style.marginRight=0;button.SetEnabled(enabled);button.RefreshState();parent.Add(button);button.RegisterCallback<FocusInEvent>(_=>{var scroll=GetFirstAncestorOfType<ScrollView>();scroll?.ScrollTo(button);});return button;}
  public SharedMembershipExperience(IMembershipPresentation controller){this.controller=controller;name="SharedMembershipExperience";style.flexShrink=0;RenderCore();}
  public virtual void Refresh()=>RenderCore();
  void RenderCore(){Clear();var catalog=controller.Membership;
   if(catalog==null){Text(this,Es?"No se pudo cargar el catálogo.":"The catalog could not be loaded.","MembershipUnavailable");return;}
   if(controller is IAppMembershipPresentation appState)AppAuthority(appState);
   var plans=catalog.plans.Where(p=>p.active).OrderBy(p=>p.sortOrder).ThenBy(p=>p.key,StringComparer.Ordinal).ToArray();
   var colors=ThemeProvider.Current.Colors;
   var tabs=new VisualElement{name="CommercialPlans"};tabs.style.flexDirection=FlexDirection.Row;tabs.style.flexWrap=Wrap.Wrap;tabs.style.marginBottom=ThemeProvider.Current.Spacing.XL;Add(tabs);
   var commercial=plans.Where(p=>p.key!="FREE").ToArray();
   foreach(var plan in commercial){
    bool selected=plan.key==controller.SelectedMembershipPlan;
    var tab=VisualAction(tabs,"","Plan_"+plan.key,()=>controller.SelectMembershipPlan(plan.key),!controller.Busy&&!controller.InteractionBlocked);
    tab.userData=selected;tab.tooltip=plan.name;tab.style.width=Length.Percent(100f/Math.Min(4,commercial.Length));tab.style.height=94;tab.style.paddingLeft=tab.style.paddingRight=2;tab.style.flexDirection=FlexDirection.Column;tab.style.justifyContent=Justify.FlexStart;tab.style.alignItems=Align.Center;
    tab.style.borderBottomWidth=3;tab.style.borderBottomColor=selected?colors.Primary:Color.clear;
    var icon=Icon(plan.iconKey,28);icon.tintColor=plan.key=="DIAMOND"?colors.DiamondAccent:plan.key=="PLATINUM"?colors.PlatinumAccent:plan.key=="GOLD"?colors.GoldAccent:colors.FamilyAccent;icon.style.marginBottom=8;tab.Add(icon);
    var name=Text(tab,plan.name.ToUpperInvariant(),"PlanLabel",TextRole.NavigationLabel);name.style.fontSize=11;name.style.marginBottom=0;name.style.width=Length.Percent(100);name.style.unityTextAlign=TextAnchor.MiddleCenter;name.style.color=selected?colors.TextPrimary:colors.TextSecondary;name.pickingMode=PickingMode.Ignore;var restingLabelColor=name.style.color;tab.RegisterCallback<FocusInEvent>(_=>name.style.color=colors.Primary);tab.RegisterCallback<FocusOutEvent>(_=>name.style.color=restingLabelColor);
   }
   var current=plans.FirstOrDefault(p=>p.key==controller.SelectedMembershipPlan);
   if(current!=null){
    if(current.productKind=="MULTI_PLAYER"&&catalog.familyPresentation!=null){var family=catalog.familyPresentation;var meta=Text(this,$"{family.minPlayers}–{family.maxPlayers} "+(Es?"jugadores":"Players")+(family.ownerIncluded?(Es?" · Titular incluido":" · Owner included"):""),"MembershipFamilyMeta",TextRole.Secondary);meta.style.unityTextAlign=TextAnchor.MiddleCenter;Text(this,Es?"Producto independiente. Disponible más adelante.":"Separate product. Coming later.","MembershipFamilyAvailability",TextRole.Secondary).style.unityTextAlign=TextAnchor.MiddleCenter;}
    if(current.key=="FREE"){Text(this,current.name,"MembershipPlanName",TextRole.SectionTitle);Text(this,Es?"El juego básico gratuito sigue disponible. Estos son beneficios comerciales adicionales.":"Free core gameplay remains available. These are additional commercial benefits.","MembershipFreeCore",TextRole.Secondary);}
    var features=new VisualElement{name="MembershipFeatures",tooltip=current.description};ThemeStyles.Card(features);ThemeStyles.Pad(features,14);features.style.marginBottom=14;Add(features);
    foreach(var feature in catalog.features.Where(f=>current.features.Any(relation=>relation.featureKey==f.key&&relation.included)).OrderBy(f=>f.sortOrder).ThenBy(f=>f.key,StringComparer.Ordinal)){
     bool included=current.features.Any(f=>f.featureKey==feature.key&&f.included);var row=new VisualElement{name="Feature_"+feature.key,tooltip=feature.description};row.style.flexDirection=FlexDirection.Row;row.style.alignItems=Align.Center;row.style.minHeight=40;row.style.flexShrink=0;features.Add(row);
     var icon=Icon(feature.iconKey,22);icon.tintColor=FeatureIconColor(feature.key);icon.style.marginRight=12;row.Add(icon);
     var unavailable=!MembershipStatePresentation.CapabilityUsable(feature.implementationStatus,feature.currentlyUsable);
     var label=Text(row,feature.name+(unavailable?(Es?" · Próximamente":" · Coming soon"):""),"FeatureName",TextRole.ButtonSecondary);label.style.flexGrow=1;label.style.flexShrink=1;label.style.minWidth=0;label.style.marginBottom=0;
     var state=Text(row,unavailable?(Es?"Incluido":"Included"):"✓","FeatureInclusion",TextRole.Secondary);state.tooltip=Es?"Incluido en el plan":"Included in the plan";state.style.width=unavailable?60:22;state.style.minHeight=22;state.style.flexShrink=0;state.style.fontSize=11;state.style.marginBottom=0;state.style.marginLeft=8;state.style.unityTextAlign=TextAnchor.MiddleCenter;state.style.backgroundColor=unavailable?Color.clear:colors.Primary;state.style.color=unavailable?colors.TextSecondary:colors.TextPrimary;ThemeStyles.Round(state,11);
    }
    var periods=(current.billingProducts??Array.Empty<BillingProductDto>()).Select(p=>p.billingPeriod).Where(p=>p=="MONTHLY"||p=="YEARLY").Distinct().OrderBy(p=>p=="YEARLY"?0:1).ToArray();
    if(periods.Length>0){var billing=new VisualElement{name="MembershipBilling"};ThemeStyles.Segmented(billing);billing.style.height=52;billing.style.marginTop=6;billing.style.marginBottom=20;Add(billing);foreach(var period in periods){bool selected=period==controller.MembershipBillingPeriod;var b=VisualAction(billing,period=="YEARLY"?(Es?"Anual":"Annual"):(Es?"Mensual":"Monthly"),"Billing_"+period,()=>controller.SelectMembershipPeriod(period),!controller.Busy);b.style.flexGrow=1;b.style.flexBasis=0;b.style.height=44;ThemeStyles.Round(b,8);ThemeStyles.Border(b,selected?colors.Primary:Color.clear,1);b.style.backgroundColor=selected?colors.Background:Color.clear;ThemeStyles.Text(b,selected?TextRole.ButtonPrimary:TextRole.ButtonSecondary);b.style.color=selected?colors.TextPrimary:colors.TextSecondary;}
     var offer=MembershipPricePresentation.Resolve(catalog,current.key,controller.MembershipBillingPeriod);
     Text(this,MembershipPricePresentation.Price(offer,controller.Locale),"MembershipPrice",TextRole.SectionTitle).style.unityTextAlign=TextAnchor.MiddleCenter;
     var equivalent=MembershipPricePresentation.MonthlyEquivalent(offer,controller.Locale);
     if(equivalent!=null)Text(this,equivalent,"MembershipMonthlyEquivalent",TextRole.Secondary).style.unityTextAlign=TextAnchor.MiddleCenter;
     var savings=offer?.billingPeriod=="YEARLY"?MembershipPricePresentation.Savings(MembershipPricePresentation.Resolve(catalog,current.key,"MONTHLY"),offer,controller.Locale):null;
     if(savings!=null)Text(this,savings,"MembershipSavings",TextRole.Secondary).style.unityTextAlign=TextAnchor.MiddleCenter;
     Text(this,Es?"Próximamente":"Coming soon","MembershipStoreUnavailable",TextRole.SectionTitle).style.unityTextAlign=TextAnchor.MiddleCenter;
     Text(this,Es?"Las compras todavía no están disponibles.":"Purchases are not available yet.","MembershipStoreDetail",TextRole.Secondary).style.unityTextAlign=TextAnchor.MiddleCenter;
    }
   }
   if(controller is IAppMembershipPresentation app){
    if(app.State=="FREE_TRIAL_AVAILABLE"&&!app.MembershipIsFamily&&current!=null&&(current.key=="DIAMOND"||current.key=="PLATINUM"||current.key=="GOLD")){
     var trial=new VisualElement{name="MembershipTrial"};trial.style.marginTop=12;trial.style.marginBottom=16;Add(trial);
     Text(trial,(Es?"Prueba gratuita de ":"Free trial of ")+current.name,"TrialTitle",TextRole.ButtonSecondary);
     if(app.MembershipTrialEligibility?.periodDays>0)Text(trial,app.MembershipTrialEligibility.periodDays+(Es?" días de acceso a los beneficios disponibles de ":" days of access to the available benefits of ")+current.name+".","TrialPolicyDuration",TextRole.Secondary);
     var offer=MembershipPricePresentation.Resolve(catalog,current.key,controller.MembershipBillingPeriod);
     if(offer!=null){
      Text(trial,(Es?"Hoy: ":"Today: ")+MembershipPricePresentation.Money(0,offer.currencyCode,controller.Locale),"TrialToday",TextRole.ButtonSecondary);
      Text(trial,(Es?"Después de la prueba: ":"After the trial: ")+MembershipPricePresentation.Price(offer,controller.Locale),"TrialPostPrice",TextRole.Secondary);
     }
     if(app.CanActivateTrial)Action(this,Es?$"Iniciar prueba gratis de {app.MembershipTrialEligibility.periodDays} días":$"Start {app.MembershipTrialEligibility.periodDays}-Day Free Trial","MembershipTrialAction",()=>{_=app.ActivateMembershipTrialAsync();},true,true);
     Feedback(this,app.TrialFeedback,true);
    }
    return;
   }
   // Onboarding action presentation is preserved, including its existing disabled/loading CTA.
   // Generic legacy promotional trial is deliberately separate from plan comparison.
   if(!controller.MembershipIsFamily){
    var eligibility=controller.MembershipTrialEligibility;
    if(eligibility?.eligible==true&&eligibility.periodDays>0||controller.TrialRetry||controller.TrialFeedback.Length>0||controller.MembershipEntitlements?.snapshot?.trialActive==true){
     var trial=new VisualElement{name="MembershipTrial"};trial.style.marginTop=12;trial.style.marginBottom=16;Add(trial);
     Text(trial,Es?"Prueba promocional Premium":"Premium promotional trial","TrialTitle",TextRole.ButtonSecondary);
     if(eligibility?.periodDays>0)Text(trial,eligibility.periodDays+(Es?" días de beneficios Premium. No es una prueba de un plan comercial.":" days of Premium benefits. This is not a commercial-plan trial."),"TrialPolicyDuration",TextRole.Secondary);
     Feedback(trial,controller.TrialFeedback,true);
     var snapshot=controller.MembershipEntitlements?.snapshot;if(snapshot?.trialActive==true&&DateTimeOffset.TryParse(snapshot.trialEndsAt,System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.RoundtripKind,out var date))Text(trial,(Es?"Finaliza: ":"Ends: ")+date.ToString("d",System.Globalization.CultureInfo.GetCultureInfo(Es?"es":"en")),"TrialEndsAt",TextRole.Secondary);
    }
   }
   var active=controller.MembershipEntitlements?.snapshot?.trialActive==true;
   var canStart=controller.CanActivateTrial&&!active;
   var days=controller.MembershipTrialEligibility?.periodDays;
   var ctaCopy=controller.MembershipIsFamily?(Es?"Próximamente":"Coming soon"):
    active?(Es?"Prueba activa":"Trial active"):
    controller.TrialFeedback=="LOADING"?(Es?"Activando prueba…":"Activating trial…"):
    canStart?(controller.TrialRetry?(Es?"Reintentar activación":"Retry activation"):(Es?$"Iniciar prueba gratis de {days} días":$"Start {days}-Day Free Trial")):
    (Es?"No disponible":"Unavailable");
   Action(this,ctaCopy,"MembershipTrialAction",()=>{_ =controller.ActivateMembershipTrialAsync();},canStart,true);


  }
  void AppAuthority(IAppMembershipPresentation app){
   var state=app.State;
   var bound=app.MembershipEntitlements?.snapshot?.trial;
   if(state!="UNAVAILABLE"&&bound!=null&&!bound.legacy&&bound.trialPlan!=null&&(state=="TRIAL_ACTIVE"||state=="TRIAL_EXPIRED")){
    var card=new VisualElement{name="ActiveTrialCard"};ThemeStyles.Card(card);ThemeStyles.Pad(card,14);card.style.marginBottom=16;card.style.flexShrink=0;Add(card);
    var status=new AuthStatusMessage{name="MembershipTrialStatus"};card.Add(status);
    status.PresentSemantic(bound.trialStatus=="ACTIVE"?AuthStatusVariant.Success:AuthStatusVariant.Info,bound.trialStatus=="ACTIVE"?(Es?"Prueba gratuita activa":"Free trial active"):(Es?"Prueba finalizada":"Trial ended"));
    var trialPlan=app.Membership.plans.FirstOrDefault(p=>p.key==bound.trialPlan);
    Text(card,(Es?"Plan de prueba: ":"Trial plan: ")+(trialPlan?.name??bound.trialPlan),"TrialPlan");
    Text(card,(Es?"Periodo: ":"Period: ")+MembershipStatePresentation.BillingPeriod(bound.trialBillingPeriod,app.Locale),"TrialBillingPeriod");
    Text(card,(state=="TRIAL_ACTIVE"?(Es?"Finaliza: ":"Ends: "):(Es?"Finalizó: ":"Ended: "))+MembershipStatePresentation.TimelineDate(bound.trialEndsAt,app.Locale),"TrialEndsAt",TextRole.Secondary);
    // reminderAt remains server-owned; expose no reminder promise before delivery exists.
    Text(card,Es?"Beneficios comerciales del plan":"Commercial plan benefits","TrialCommercialBenefits",TextRole.ButtonSecondary);
    foreach(var benefit in bound.commercialPlanBenefits??Array.Empty<CommercialBenefitDto>()){
     var name=app.Membership.features.FirstOrDefault(f=>f.key==benefit.key)?.name??benefit.key;
     Text(card,name+(MembershipStatePresentation.CapabilityUsable(benefit.implementationStatus,benefit.currentlyUsable)?"":(Es?" · Próximamente":" · Coming soon")),"TrialCommercialBenefit",TextRole.Secondary);
    }
    Text(this,Es?"Explorar planes":"Explore plans","MembershipCatalogHeading",TextRole.ButtonSecondary);return;
   }
   if(state=="TRIAL_ACTIVE"||state=="TRIAL_EXPIRED"){
    var status=new AuthStatusMessage{name="MembershipTrialStatus"};Add(status);
    status.PresentSemantic(state=="TRIAL_ACTIVE"?AuthStatusVariant.Success:AuthStatusVariant.Info,
     state=="TRIAL_ACTIVE"?(Es?"Tus beneficios promocionales Premium están activos.":"Your promotional Premium benefits are active."):(Es?"Tu prueba promocional ha finalizado.":"Your promotional trial has ended."));
    if(DateTimeOffset.TryParse(app.MembershipEntitlements?.snapshot?.trialEndsAt,System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.RoundtripKind,out var end))
     Text(this,(state=="TRIAL_ACTIVE"?(Es?"Finaliza: ":"Ends: "):(Es?"Finalizó: ":"Ended: "))+end.ToString("d",System.Globalization.CultureInfo.GetCultureInfo(Es?"es":"en")),"TrialEndsAt",TextRole.Secondary);
   }
   if(state=="PREMIUM_LEGACY"||state=="TRIAL_ACTIVE"){
    var benefits=new VisualElement{name="CurrentMembershipBenefits"};ThemeStyles.Card(benefits);benefits.style.marginBottom=ThemeProvider.Current.Spacing.MD;Add(benefits);
    Text(benefits,Es?"Tus beneficios activos":"Your active benefits","CurrentBenefitsTitle",TextRole.ButtonSecondary);
    var features=app.MembershipEntitlements?.snapshot?.features;
    if(features==null)Text(benefits,Es?"No se pudo confirmar la lista de beneficios.":"The benefit list could not be confirmed.","EffectiveBenefitsUnavailable",TextRole.Secondary);
    else if(features.Length==0)Text(benefits,Es?"Sin beneficios adicionales indicados.":"No additional benefits listed.","EffectiveBenefitsEmpty",TextRole.Secondary);
    else foreach(var key in features.Where(k=>!string.IsNullOrWhiteSpace(k)).Distinct())Text(benefits,MembershipStatePresentation.FeatureName(key,app.Locale),"EffectiveFeature",TextRole.Secondary).userData=key;
   }
   Text(this,Es?"Explorar planes":"Explore plans","MembershipCatalogHeading",TextRole.ButtonSecondary);
  }
  Image Icon(string key,float size){var image=new Image{vectorImage=Resources.Load<VectorImage>("AppShellMockIcons/"+key)};ThemeStyles.Icon(image);image.style.width=image.style.height=size;return image;}
  Button VisualAction(VisualElement parent,string text,string key,Action action,bool enabled){var button=new Button(action){name=key,text=text};ThemeStyles.Text(button,TextRole.ButtonSecondary);ThemeStyles.Pad(button,0);ThemeStyles.Border(button,Color.clear,0);ThemeStyles.Round(button,0);button.style.backgroundColor=Color.clear;button.style.marginLeft=button.style.marginRight=button.style.marginTop=button.style.marginBottom=0;button.style.minWidth=0;button.style.minHeight=44;button.style.flexShrink=0;button.style.whiteSpace=WhiteSpace.Normal;button.SetEnabled(enabled);button.style.opacity=enabled?1:.55f;StyleColor restingBackground=button.style.backgroundColor;button.RegisterCallback<FocusInEvent>(_=>{restingBackground=button.style.backgroundColor;if(!key.StartsWith("Plan_",StringComparison.Ordinal))button.style.backgroundColor=Color.Lerp(ThemeProvider.Current.Colors.Surface,ThemeProvider.Current.Colors.TextPrimary,.08f);GetFirstAncestorOfType<ScrollView>()?.ScrollTo(button);});button.RegisterCallback<FocusOutEvent>(_=>button.style.backgroundColor=restingBackground);parent.Add(button);return button;}
  protected void Feedback(VisualElement parent,string code,bool trial){if(string.IsNullOrEmpty(code))return;var status=new AuthStatusMessage{name=trial?"TrialFeedback":"MembershipFeedback"};parent.Add(status);
   bool loading=code=="LOADING"||code=="SAVING",success=code=="ACTIVATED"||code=="ALREADY_ACTIVE"||code=="SAVED";
   string en,es;switch(code){
    case "START_TRIAL_REQUESTED":en="Trial activation is not available here yet.";es="La activación de la prueba aún no está disponible aquí.";break;
    case "LOADING":en="Activating trial…";es="Activando prueba…";break;
    case "ACTIVATED":en="Your Premium trial is active.";es="Tu prueba Premium está activa.";break;
    case "ALREADY_ACTIVE":en="Your trial is already active.";es="Tu prueba ya está activa.";break;
    case "TRIAL_NOT_ELIGIBLE":en="This account is not eligible for a trial.";es="Esta cuenta no puede activar una prueba.";break;
    case "TRIAL_ALREADY_CONSUMED":en="This account has already used its trial.";es="Esta cuenta ya utilizó su prueba.";break;
    case "TRIAL_POLICY_VERSION_MISMATCH":en="The trial offer changed. Reload before activating.";es="La oferta cambió. Recarga antes de activar.";break;
    case "TRIAL_DISABLED":en="The trial is currently unavailable.";es="La prueba no está disponible actualmente.";break;
    case "IDEMPOTENCY_CONFLICT":en="The activation could not be confirmed. Reload to review your access.";es="No se pudo confirmar la activación. Recarga para revisar tu acceso.";break;
    case "CLIENT_UPDATE_REQUIRED":en="Update the app before activating a trial.";es="Actualiza la aplicación antes de activar una prueba.";break;
    case "SAVING":en="Saving progress…";es="Guardando progreso…";break;
    case "SAVED":en="Your choice has been saved.";es="Tu elección se ha guardado.";break;
    case "CONFLICT":en="Your progress changed. Review the updated state.";es="Tu progreso cambió. Revisa el estado actualizado.";break;
    case "LOCALE_ERROR":en="The language could not be loaded.";es="No se pudo cargar el idioma.";break;
    default:en="Could not confirm. Please retry.";es="No se pudo confirmar. Inténtalo de nuevo.";break;
   }status.PresentSemantic(loading?AuthStatusVariant.Loading:success?AuthStatusVariant.Success:AuthStatusVariant.Warning,Es?es:en);
  }
 }
}
