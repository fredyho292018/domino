using System;
using Domino.Infrastructure.Api;
using Domino.UI.Theming;
using UnityEngine;
using UnityEngine.UIElements;
namespace Domino.UI.AppShell {
 public sealed class CoachOptionCard:Button {
  public readonly Label NameLabel,Check;public readonly Image Portrait;
  readonly string key;readonly OnboardingShellController controller;bool hover,focused;
  public CoachOptionCard(CoachDto coach,OnboardingShellController controller):base(()=>controller.SelectCoach(coach.key)){
   this.controller=controller;key=coach.key;name="Coach_"+key;tooltip=coach.name;style.alignSelf=Align.Stretch;style.flexGrow=1;style.flexBasis=0;style.minWidth=0;style.minHeight=176;style.flexShrink=0;style.marginLeft=style.marginRight=4;ThemeStyles.Pad(this,12);ThemeStyles.Round(this,16);style.alignItems=Align.Center;
   Portrait=new Image{name="CoachPortrait",image=CoachAvatarResources.Resolve<Texture2D>(coach.avatar?.key,p=>Resources.Load<Texture2D>(p),null),scaleMode=ScaleMode.ScaleToFit,pickingMode=PickingMode.Ignore};Portrait.style.width=Portrait.style.height=112;Portrait.style.maxWidth=Length.Percent(100);Portrait.style.flexShrink=0;ThemeStyles.Round(Portrait,56);Portrait.style.overflow=Overflow.Hidden;Add(Portrait);
   if(Portrait.image==null){Portrait.style.display=DisplayStyle.None;var fallback=new Label("○"){name="NeutralCoachPortrait",pickingMode=PickingMode.Ignore};ThemeStyles.Text(fallback,TextRole.Secondary);fallback.style.fontSize=64;fallback.style.height=112;fallback.style.unityTextAlign=TextAnchor.MiddleCenter;Add(fallback);}
   NameLabel=new Label(coach.name){pickingMode=PickingMode.Ignore};ThemeStyles.Text(NameLabel,TextRole.ButtonSecondary);NameLabel.style.whiteSpace=WhiteSpace.Normal;NameLabel.style.unityTextAlign=TextAnchor.MiddleCenter;NameLabel.style.marginTop=8;Add(NameLabel);
   Check=new Label(){name="CoachSelectedCheck",pickingMode=PickingMode.Ignore};Check.style.position=Position.Absolute;Check.style.right=8;Check.style.top=6;Check.style.color=ThemeProvider.Current.Colors.Primary;Check.style.fontSize=22;Add(Check);
   RegisterCallback<PointerEnterEvent>(_=>{hover=true;Paint();});RegisterCallback<PointerLeaveEvent>(_=>{hover=false;Paint();});RegisterCallback<FocusInEvent>(_=>{focused=true;Paint();});RegisterCallback<FocusOutEvent>(_=>{focused=false;Paint();});Refresh();
  }
  public void Refresh(){SetEnabled(!controller.CoachLocked);Check.text=controller.CoachSelection==key?"✓":"";Paint();}
  void Paint(){var c=ThemeProvider.Current.Colors;style.backgroundColor=c.Surface;ThemeStyles.Border(this,controller.CoachSelection==key||hover||focused?c.Primary:c.Surface,2);style.opacity=enabledSelf?1:.65f;}
 }
 public sealed class ProductionCoachView:VisualElement {
  readonly OnboardingShellController controller;readonly AuthStatusMessage feedback;readonly ThemeButton next;
  public ProductionCoachView(OnboardingShellController controller){this.controller=controller;name="COACH_STEP";style.flexShrink=0;
   var items=controller.Coaches?.items??Array.Empty<CoachDto>();VisualElement row=null;int count=0;
   foreach(var coach in items){if(!coach.selectable)continue;if(count++%2==0){row=new VisualElement{name="CoachRow"};row.style.flexDirection=FlexDirection.Row;row.style.alignItems=Align.Stretch;row.style.marginBottom=12;row.style.flexShrink=0;Add(row);}row.Add(new CoachOptionCard(coach,controller));}
   if(count%2!=0){var spacer=new VisualElement();spacer.style.flexGrow=1;spacer.style.flexBasis=0;spacer.style.marginLeft=spacer.style.marginRight=4;row.Add(spacer);}
   feedback=new AuthStatusMessage{name="CoachFeedback"};Add(feedback);next=new ThemeButton("",()=>{_ =controller.SaveCoachAsync();},true){name="CoachContinue"};next.style.marginTop=16;Add(next);Refresh();
  }
  public void Refresh(){bool es=controller.Locale=="es";foreach(var c in this.Query<CoachOptionCard>().ToList())c.Refresh();var code=controller.CoachFeedback;
   if(code.Length==0&&!controller.MissingSavedCoach)feedback.style.display=DisplayStyle.None;
   else feedback.PresentSemantic(code=="SAVING"||code=="LOCALIZING"?AuthStatusVariant.Loading:code=="CONFLICT"||controller.MissingSavedCoach?AuthStatusVariant.Warning:AuthStatusVariant.Error,
    code=="SAVING"?(es?"Guardando entrenador…":"Saving coach…"):code=="LOCALIZING"?(es?"Cargando idioma…":"Loading language…"):code=="CONFLICT"?(es?"Tu progreso cambió. Revisa la selección actualizada.":"Your progress changed. Review the updated selection."):code=="NETWORK"?(es?"No se pudo confirmar el guardado. Reintenta la misma solicitud.":"Could not confirm the save. Retry the same request."):controller.MissingSavedCoach?(es?"Tu entrenador guardado no está disponible en este catálogo. Elige uno para continuar.":"Your saved coach is not available in this catalog. Choose one to continue."):(es?"No se pudo guardar la selección. Revísala e inténtalo de nuevo.":"Could not save the selection. Review it and try again."));
   next.text=controller.CoachRetry?(es?"Reintentar":"Retry"):(es?"Continuar":"Continue");next.SetEnabled(controller.CoachCanSave);next.RefreshState();
  }
 }
}
