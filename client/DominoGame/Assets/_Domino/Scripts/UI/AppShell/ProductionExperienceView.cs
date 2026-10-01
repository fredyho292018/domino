using System;
using System.Linq;
using Domino.UI.Theming;
using UnityEngine;
using UnityEngine.UIElements;
namespace Domino.UI.AppShell {
 public sealed class OnboardingOptionCard:Button {
  public Label Title {get;} public Label Detail {get;} public Label Check {get;}
  readonly bool selected;bool hover,focused;
  public OnboardingOptionCard(string key,string title,string description,bool selected,bool disabled,Action action):base(action){
   this.selected=selected;name="ExperienceOption_"+key;tooltip=title;style.minHeight=104;style.minWidth=44;style.flexShrink=0;style.marginBottom=12;ThemeStyles.Pad(this,16);ThemeStyles.Round(this,16);style.flexDirection=FlexDirection.Row;style.alignItems=Align.Center;
   var content=new VisualElement{pickingMode=PickingMode.Ignore};content.style.flexGrow=1;content.style.flexShrink=1;content.style.minWidth=0;Add(content);
   Title=new Label(title){pickingMode=PickingMode.Ignore};ThemeStyles.Text(Title,TextRole.ButtonSecondary);Title.style.fontSize=17;Title.style.whiteSpace=WhiteSpace.Normal;Title.style.unityTextAlign=TextAnchor.MiddleLeft;content.Add(Title);
   Detail=new Label(description??""){pickingMode=PickingMode.Ignore};ThemeStyles.Text(Detail,TextRole.Secondary);Detail.style.color=ThemeProvider.Current.Colors.TextSecondaryEmphasized;Detail.style.fontSize=13;Detail.style.marginTop=6;Detail.style.whiteSpace=WhiteSpace.Normal;Detail.style.unityTextAlign=TextAnchor.MiddleLeft;content.Add(Detail);
   Check=new Label(selected?"✓":""){name="ExperienceCheck",pickingMode=PickingMode.Ignore};Check.style.width=24;Check.style.flexShrink=0;Check.style.marginLeft=12;Check.style.fontSize=22;Check.style.color=ThemeProvider.Current.Colors.Primary;Add(Check);
   SetEnabled(!disabled);RegisterCallback<PointerEnterEvent>(_=>{hover=true;Paint();});RegisterCallback<PointerLeaveEvent>(_=>{hover=false;Paint();});RegisterCallback<FocusInEvent>(_=>{focused=true;Paint();schedule.Execute(()=>GetFirstAncestorOfType<ScrollView>()?.ScrollTo(this));});RegisterCallback<FocusOutEvent>(_=>{focused=false;Paint();});Paint();
  }
  void Paint(){var c=ThemeProvider.Current.Colors;style.backgroundColor=c.Surface;ThemeStyles.Border(this,selected||focused||hover?c.Primary:c.Surface,2);style.opacity=enabledSelf?1:.65f;}
 }
 public sealed class ProductionExperienceView:VisualElement {
  public ProductionExperienceView(OnboardingShellController controller){
   name="EXPERIENCE_STEP";style.flexShrink=0;bool es=controller.Locale=="es";var q=controller.ExperienceQuestion;
   if(q?.options!=null)foreach(var option in q.options.OrderBy(o=>o.sortOrder).ThenBy(o=>o.key,StringComparer.Ordinal)){
    string key=option.key;Add(new OnboardingOptionCard(key,option.title,option.description,controller.ExperienceSelection==key,controller.ExperienceLocked,()=>controller.SelectExperience(key)));
   }
   var message=new AuthStatusMessage{name="ExperienceFeedback"};Add(message);
   var feedback=controller.ExperienceFeedback;
   if(feedback.Length>0)message.PresentSemantic(feedback=="SAVING"||feedback=="LOCALIZING"?AuthStatusVariant.Loading:feedback=="CONFLICT"?AuthStatusVariant.Warning:AuthStatusVariant.Error,
    feedback=="SAVING"?(es?"Guardando…":"Saving…"):feedback=="LOCALIZING"?(es?"Cargando idioma…":"Loading language…"):feedback=="CONFLICT"?(es?"Tu progreso cambió. Revisa la selección actualizada antes de continuar.":"Your progress changed. Review the updated selection before continuing."):feedback=="NETWORK"?(es?"No se pudo confirmar el guardado. Reintenta la misma solicitud.":"Could not confirm the save. Retry the same request."):feedback=="LOCALE_ERROR"?(es?"No se pudo cargar el idioma.":"Could not load the language."):(es?"No se pudo guardar esta selección. Revísala e inténtalo de nuevo.":"Could not save this selection. Review it and try again."));
   var next=new ThemeButton(controller.ExperienceRetry?(es?"Reintentar":"Retry"):(es?"Continuar":"Continue"),()=>{_ =controller.SaveExperienceAsync();},true){name="ExperienceContinue"};next.style.marginTop=16;next.SetEnabled(controller.ExperienceCanSave);next.RefreshState();Add(next);
  }
 }
}
