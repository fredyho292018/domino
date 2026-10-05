using System;
using System.IO;
using System.Linq;
using Domino.Infrastructure.Api;
using Domino.UI;
using Domino.UI.AppShell;
using Domino.UI.Theming;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Domino.Editor {
 // TEMPORARY_VALIDATION_PREVIEW. Proposed contract, not a backend DTO or entitlement adapter.
 // No production controller, API, session, activation or purchase transport is constructed.
 public sealed class MembershipTrialContractPreview : EditorWindow {
  string state="ACTIVE",locale="es",plan,billing;bool queued,confirmation;
  JObject fixture;MembershipCatalogDto catalog;TrialStateDto trial;
  [MenuItem("Domino/Membership/Trial plan contract (isolated design)")]
  public static void Open(){GetWindow<MembershipTrialContractPreview>().Queue();}
  void CreateGUI(){Queue();}
  void Queue(){if(queued)return;queued=true;EditorApplication.delayCall+=()=>{if(this==null)return;queued=false;Render();};}
  bool Es=>locale=="es";
  string T(string en,string es)=>Es?es:en;
  string FilePath(string name)=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../Validation",name));
  Label Text(VisualElement parent,string value,TextRole role=TextRole.Body){var l=new Label(value);ThemeStyles.Text(l,role);l.style.whiteSpace=WhiteSpace.Normal;l.style.marginBottom=10;l.style.flexShrink=0;parent.Add(l);return l;}
  void Render(){
   titleContent=new GUIContent("TRIAL CONTRACT · DESIGN");minSize=new Vector2(440,650);
   fixture=JObject.Parse(File.ReadAllText(FilePath("MembershipTrialTargetFixture.json")));
   trial=fixture["trial"].ToObject<TrialStateDto>();
   catalog=JObject.Parse(File.ReadAllText(FilePath("MembershipCatalogFixture.json")))[locale].ToObject<MembershipCatalogDto>();
   if(plan==null)plan=(string)fixture["selectedPlan"];if(billing==null)billing=(string)fixture["selectedBillingPeriod"];
   rootVisualElement.Clear();
   var states=new UnityEngine.UIElements.PopupField<string>(new System.Collections.Generic.List<string>{"AVAILABLE","ACTIVE"},state);states.RegisterValueChangedCallback(e=>{state=e.newValue;confirmation=false;Queue();});rootVisualElement.Add(states);
   var languages=new UnityEngine.UIElements.PopupField<string>(new System.Collections.Generic.List<string>{"en","es"},locale);languages.RegisterValueChangedCallback(e=>{locale=e.newValue;Queue();});rootVisualElement.Add(languages);
   Text(rootVisualElement,"TARGET CONTRACT · FIXTURE ONLY · 393x852 · NO NETWORK",TextRole.Secondary);
   var viewport=new ScrollView(ScrollViewMode.Vertical);viewport.style.width=393;viewport.style.height=852;viewport.style.flexShrink=0;viewport.style.backgroundColor=ThemeProvider.Current.Colors.Background;rootVisualElement.Add(viewport);
   var body=new VisualElement();ThemeStyles.Page(body);body.style.flexShrink=0;viewport.Add(body);
   Text(body,confirmation?T("Trial confirmation","Confirmar prueba"):T("Membership","Membresía"),TextRole.PageTitle);
   Text(body,T("Your membership: Free","Tu membresía: Gratis"),TextRole.Secondary);
   if(confirmation){Confirmation(body);return;}
   if(state=="ACTIVE"){
    var status=new AuthStatusMessage();status.PresentSemantic(AuthStatusVariant.Success,T("Free trial active","Prueba gratuita activa"));body.Add(status);
    Text(body,T("Plan: ","Plan: ")+trial.trialPlan);
    Text(body,T("Billing: ","Facturación: ")+Period(trial.trialBillingPeriod));
    Text(body,T("Ends: ","Finaliza: ")+Date("trialEndsAt"));
    Text(body,T("Trial plan benefits","Beneficios del plan en prueba"),TextRole.SectionTitle);
    foreach(var benefit in trial.commercialPlanBenefits){var name=catalog.features.Single(f=>f.key==benefit.key).name;Text(body,name+(benefit.currentlyUsable?"":T(" · Coming soon"," · Próximamente")),TextRole.Secondary);}
   }
   Text(body,T("Explore plans","Explorar planes"),TextRole.SectionTitle);
   var plans=new UnityEngine.UIElements.PopupField<string>(catalog.plans.Where(p=>p.active&&p.key!="FREE").Select(p=>p.key).ToList(),plan);plans.RegisterValueChangedCallback(e=>{plan=e.newValue;Queue();});body.Add(plans);
   var periods=new UnityEngine.UIElements.PopupField<string>(new System.Collections.Generic.List<string>{"YEARLY","MONTHLY"},billing);periods.RegisterValueChangedCallback(e=>{billing=e.newValue;Queue();});body.Add(periods);
   Features(body,plan);
   Text(body,T("Purchases coming soon. Store price unavailable.","Compras próximamente. Precio de tienda no disponible."),TextRole.Secondary);
   if(state=="AVAILABLE"&&plan!="FRIENDS_AND_FAMILY"){
    Text(body,T("Trial duration: ","Duración de prueba: ")+fixture["durationDays"]+T(" days"," días"));
    var start=new ThemeButton(T("Review free trial","Revisar prueba gratis"),()=>{confirmation=true;Queue();},true);body.Add(start);
   }
  }
  string Period(string value)=>value=="YEARLY"?T("Annual","Anual"):T("Monthly","Mensual");
  string Date(string key)=>DateTimeOffset.Parse((string)fixture[key],System.Globalization.CultureInfo.InvariantCulture).ToString("yyyy-MM-dd HH:mm 'UTC'");
  void Features(VisualElement body,string key){var p=catalog.plans.Single(x=>x.key==key);foreach(var f in catalog.features.Where(f=>p.features.Any(r=>r.featureKey==f.key&&r.included)))Text(body,"✓ "+f.name,TextRole.Secondary);}
  void Confirmation(VisualElement body){
   Text(body,T("Selected plan: ","Plan seleccionado: ")+plan);
   Text(body,T("Billing period: ","Período: ")+Period(billing));
   Text(body,T("Trial: ","Prueba: ")+fixture["durationDays"]+T(" days"," días"));
   Text(body,T("Today: ","Hoy: ")+fixture["chargeTodayMinorUnits"]+" "+fixture["currency"]);
   Text(body,T("Example trial end: ","Fin de prueba de ejemplo: ")+Date("trialEndsAt"));
   Text(body,T("Reminder: ","Recordatorio: ")+fixture["reminderBeforeEndDays"]+T(" days before end — "," días antes del final — ")+Date("reminderAt"));
   Text(body,T("Reminder delivery and billing are not implemented. These dates are fixtures, not a live offer.","El envío del recordatorio y la facturación no están implementados. Estas fechas son ejemplos, no una oferta real."),TextRole.Secondary);
   Text(body,T("Post-trial price: unavailable until supplied by the store.","Precio posterior: no disponible hasta que lo proporcione la tienda."));
   Text(body,T("The final store offer must disclose renewal and the cancellation deadline to avoid a charge. Terms are not yet available.","La oferta final de la tienda debe indicar la renovación y el plazo para cancelar y evitar un cobro. Las condiciones aún no están disponibles."),TextRole.Secondary);
   var confirm=new ThemeButton(T("Confirmation unavailable","Confirmación no disponible"),()=>{},true);confirm.SetEnabled(false);body.Add(confirm);
   body.Add(new ThemeButton(T("Back","Volver"),()=>{confirmation=false;Queue();}));
  }
 }
}
