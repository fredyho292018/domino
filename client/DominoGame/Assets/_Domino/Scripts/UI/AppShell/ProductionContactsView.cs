using Domino.UI.Theming;
using UnityEngine;
using UnityEngine.UIElements;
namespace Domino.UI.AppShell {
 public sealed class ProductionContactsView:VisualElement {
  public ProductionContactsView(OnboardingShellController controller){name="CONTACTS_STEP";style.flexShrink=0;bool es=controller.Locale=="es";
   var anchor=new VisualElement{name="ContactsVisualAnchor"};anchor.style.flexDirection=FlexDirection.Row;anchor.style.alignItems=Align.Center;anchor.style.marginTop=8;anchor.style.marginBottom=20;Add(anchor);var icon=new Image{name="ContactsPeopleIcon",vectorImage=Resources.Load<VectorImage>("AppShellMockIcons/icon_menu_friends")};ThemeStyles.Icon(icon);icon.style.width=icon.style.height=32;icon.style.marginRight=12;anchor.Add(icon);
   var optional=new Label(es?"OPCIONAL":"OPTIONAL"){name="ContactsOptional"};ThemeStyles.Text(optional,TextRole.Caption);optional.style.color=ThemeProvider.Current.Colors.TextSecondaryEmphasized;optional.style.whiteSpace=WhiteSpace.Normal;optional.style.backgroundColor=ThemeProvider.Current.Colors.Surface;optional.style.paddingLeft=optional.style.paddingRight=8;optional.style.paddingTop=optional.style.paddingBottom=4;ThemeStyles.Round(optional,ThemeProvider.Current.Radius.Card);anchor.Add(optional);
   var description=new Label(es?"Conectar tus contactos puede ayudarte a encontrar personas que ya juegan. Esta función estará disponible más adelante. Puedes omitir este paso.":"Connecting your contacts can help you find people who already play. This feature is coming later. You can skip this step."){name="ContactsExplanation"};ThemeStyles.Text(description,TextRole.Body);description.style.whiteSpace=WhiteSpace.Normal;description.style.marginBottom=24;Add(description);
   var next=new ThemeButton(controller.ContactsRetry?(es?"Reintentar":"Retry"):(es?"Ahora no":"Not now"),()=>{_ =controller.ContinueContactsAsync();},true){name="ContactsContinue"};next.SetEnabled(controller.ContactsCanContinue);next.RefreshState();Add(next);
   // Reserve status space so progress feedback never displaces the action.
   var statusArea=new VisualElement{name="ContactsStatusArea"};statusArea.style.minHeight=96;statusArea.style.flexShrink=0;statusArea.style.marginTop=16;Add(statusArea);var status=new AuthStatusMessage{name="ContactsFeedback"};statusArea.Add(status);var code=controller.ContactsFeedback;
   if(code.Length>0)status.PresentSemantic(code=="SAVING"?AuthStatusVariant.Loading:code=="CONFLICT"?AuthStatusVariant.Warning:AuthStatusVariant.Error,
    code=="SAVING"?(es?"Guardando progreso…":"Saving progress…"):code=="CONFLICT"?(es?"Tu progreso cambió. Revisa el estado actualizado.":"Your progress changed. Review the updated state."):code=="NETWORK"?(es?"No se pudo confirmar. Reintenta la misma solicitud.":"Could not confirm. Retry the same request."):code=="LOCALE_ERROR"?(es?"No se pudo cargar el idioma.":"Could not load the language."):(es?"No se pudo continuar. Inténtalo de nuevo.":"Could not continue. Try again."));
  }
 }
}
