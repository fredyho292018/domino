using System;
using Domino.UI.Theming;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;

namespace Domino.UI.AppShell {
 public sealed class AppMembershipPage:ScrollView,IDisposable {
  readonly AppMembershipController controller;readonly Action back;
  readonly VisualElement body=new VisualElement{name="MembershipBody"};bool disposed;
  public AppMembershipController Controller=>controller;
  public AppMembershipPage(AppMembershipController controller,Action back,string locale=null):base(ScrollViewMode.Vertical){
   this.controller=controller;this.back=back;name="AppMembershipPage";
   style.flexGrow=1;style.minHeight=0;style.backgroundColor=ThemeProvider.Current.Colors.Background;
   horizontalScrollerVisibility=verticalScrollerVisibility=ScrollerVisibility.Hidden;
   contentContainer.style.flexGrow=1;ThemeStyles.Page(body);body.style.flexShrink=0;Add(body);
   controller.Changed+=Render;
   RegisterCallback<AttachToPanelEvent>(_=>LocalizationSettings.SelectedLocaleChanged+=LocaleChanged);
   RegisterCallback<DetachFromPanelEvent>(_=>Dispose());
   // Clears obsolete account data even before a host detach if the session is invalidated.
   schedule.Execute(()=>{if(!disposed&&controller.State=="UNAVAILABLE"&&body.Q("MembershipCurrentState")?.userData as string!="UNAVAILABLE")Render();}).Every(250);
   _=controller.LoadAsync(locale??DominoLocalization.Language);
  }
  void LocaleChanged(Locale locale){if(locale!=null&&!disposed)_=controller.LoadAsync(locale.Identifier.Code);}
  void Render(){
   if(disposed)return;var scroll=scrollOffset;var focus=(panel?.focusController?.focusedElement as VisualElement)?.name;body.Clear();bool es=controller.Locale=="es";
   var arrow=new Image();var button=new Button(back){name="MembershipBack",tooltip=es?"Volver":"Back"};ThemeStyles.Back(button,arrow);button.Add(arrow);button.style.alignSelf=Align.FlexStart;body.Add(button);
   var title=new Label(es?"Membresía":"Membership"){name="MembershipTitle"};ThemeStyles.Text(title,TextRole.PageTitle);title.style.fontSize=27;title.style.unityTextAlign=TextAnchor.MiddleCenter;title.style.marginBottom=22;body.Add(title);
   var state=new Label(controller.State!="UNAVAILABLE"&&controller.MembershipEntitlements?.snapshot?.membershipPlan!=null?MembershipStatePresentation.MembershipIdentity(controller.MembershipEntitlements,controller.Locale):MembershipStatePresentation.Copy(controller.State,controller.Locale)){name="MembershipCurrentState",userData=controller.State};ThemeStyles.Text(state,TextRole.Secondary);state.style.whiteSpace=WhiteSpace.Normal;state.style.marginBottom=ThemeProvider.Current.Spacing.MD;body.Add(state);
   if(controller.Membership!=null)body.Add(new SharedMembershipExperience(controller));
   else {
    var feedback=new AuthStatusMessage{name="MembershipLoadFeedback"};feedback.PresentSemantic(controller.Busy?AuthStatusVariant.Loading:AuthStatusVariant.Warning,controller.Busy?(es?"Cargando membresía…":"Loading membership…"):(es?"No se pudo cargar la membresía.":"Membership could not be loaded."));body.Add(feedback);
    var retry=new ThemeButton(es?"Reintentar":"Retry",()=>{_=controller.LoadAsync(controller.Locale);}){name="MembershipRetry"};retry.SetEnabled(!controller.Busy);body.Add(retry);
   }
   schedule.Execute(()=>{if(disposed)return;scrollOffset=scroll;if(!string.IsNullOrEmpty(focus)){var target=body.Q<Button>(focus);if(target?.enabledInHierarchy==true)target.Focus();}});
  }
  public void Dispose(){if(disposed)return;disposed=true;LocalizationSettings.SelectedLocaleChanged-=LocaleChanged;controller.Changed-=Render;controller.Dispose();}
 }
}
