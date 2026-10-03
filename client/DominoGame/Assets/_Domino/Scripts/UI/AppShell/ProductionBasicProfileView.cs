using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Domino.UI.Theming;
using UnityEngine;
using UnityEngine.UIElements;
namespace Domino.UI.AppShell {
 // Approved UI presentation overrides only; stable catalog/domain keys are unchanged.
 public static class BasicProfileCopy {
  public static string Eyebrow(string locale)=>locale=="es"?"TU PERFIL":"YOUR PROFILE";
  public static string Title(string locale)=>locale=="es"?"Cuéntanos sobre ti":"Tell us about you";
  public static string Field(string locale,string key){bool es=locale=="es";switch(key){
   case "FIRST_NAME":return es?"Nombre":"First name";
   case "LAST_NAME":return es?"Apellidos":"Last name";
   case "DISPLAY_NAME":return es?"Alias de jugador":"Player alias";
   case "COUNTRY":return es?"País":"Country";
   case "PREFERRED_LANGUAGE":return es?"Idioma preferido":"Preferred language";
   default:return null;
  }}
 }
 public sealed class ProductionBasicProfileView:VisualElement {
  readonly OnboardingShellController controller;
  readonly bool es;
  ThemeButton submit;
  AuthStatusMessage aliasFeedback;
  void RefreshSubmit(){if(submit==null)return;submit.SetEnabled(!controller.Busy&&(controller.ProfileRetry||controller.AliasCanContinue));submit.RefreshState();}
  public ProductionBasicProfileView(OnboardingShellController controller){
   this.controller=controller;es=controller.Locale=="es";name="BASIC_PROFILE_STEP";style.flexShrink=0;
   var d=controller.Profile;
   Field("FIRST_NAME",es?"Nombre":"First name",d.FirstName,v=>d.FirstName=v);
   Field("LAST_NAME",es?"Apellidos":"Last name",d.LastName,v=>d.LastName=v);
   Field("DISPLAY_NAME",es?"Alias de jugador":"Player alias",d.DisplayName,v=>{_ =controller.ChangeProfileAliasAsync(v);});
   Label(es?"Este es el nombre que verán los demás jugadores.":"This is the name other players will see.","DisplayNameHelp",TextRole.Secondary);
   aliasFeedback=new AuthStatusMessage{name="AliasAvailabilityFeedback"};Add(aliasFeedback);
   RefreshAlias();controller.AliasChanged+=RefreshAlias;RegisterCallback<DetachFromPanelEvent>(_=>controller.AliasChanged-=RefreshAlias);
   var countries=new List<string>{""};countries.AddRange(BasicProfileRules.Countries);
   Select("COUNTRY",es?"País":"Country",countries,d.Country,CountryName,v=>d.Country=v);
   Select("PREFERRED_LANGUAGE",es?"Idioma preferido":"Preferred language",new List<string>{"","en","es"},d.Language,v=>v=="en"?"English":v=="es"?"Español":es?"Selecciona un idioma":"Select a language",v=>{if(v!="")_ =controller.ChangeProfileLocaleAsync(v);else d.Language="";});
   var feedback=new AuthStatusMessage{name="ProfileFeedback"};Add(feedback);
   var key=controller.ProfileFeedback;
   if(key.Length>0&&key!="DISPLAY_NAME_TAKEN"){var variant=key=="SAVING"||key=="LOCALIZING"?AuthStatusVariant.Loading:key=="CONFLICT"?AuthStatusVariant.Warning:AuthStatusVariant.Error;
    var message=key=="DISPLAY_NAME_TAKEN"?(es?"Ese alias ya está en uso.":"That alias is already taken."):key=="DISPLAY_NAME_RESERVATIONS_NOT_READY"?(es?"El registro de alias aún no está disponible. Tus datos no se han guardado.":"Alias registration is not available yet. Your entries have not been saved."):key=="SAVING"?(es?"Guardando perfil…":"Saving profile…"):key=="LOCALIZING"?(es?"Cargando idioma…":"Loading language…"):key=="CONFLICT"?(es?"El perfil cambió. Revisa tus datos antes de guardar otra vez.":"Your profile changed. Review your entries before saving again."):key=="NETWORK"?(es?"No se pudo confirmar el guardado. Reintenta la misma solicitud.":"Could not confirm the save. Retry the same request."):key=="LOCALE_ERROR"?(es?"No se pudo cargar el idioma. Vuelve a seleccionarlo.":"Could not load the language. Select it again."):(es?"Revisa los campos marcados. El nombre público admite 3–16 letras sin acentos, números, _ o -.":"Review the marked fields. Display name needs 3–16 letters, numbers, _ or -.");feedback.PresentSemantic(variant,message);
   }
   submit=new ThemeButton(controller.ProfileRetry?(es?"Reintentar":"Retry"):(es?"Continuar":"Continue"),()=>{_ =controller.SaveProfileAsync();},true){name="BasicProfileContinue"};RefreshSubmit();Add(submit);
   if(controller.ProfileErrors.Contains("DISPLAY_NAME"))schedule.Execute(()=>this.Q<TextField>("DISPLAY_NAME")?.Focus());
  }
  void RefreshAlias(){
   if(aliasFeedback==null)return;var state=controller.AliasState;
   string message=state==AliasCheckState.Checking?(es?"Comprobando disponibilidad...":"Checking availability..."):
    state==AliasCheckState.Available?(es?"Alias disponible.":"Alias available."):
    state==AliasCheckState.Taken?(es?"Ese alias ya está en uso. Prueba con otro.":"That alias is already taken. Try another one."):
    state==AliasCheckState.CheckFailed?(es?"No se pudo comprobar la disponibilidad. Se verificará al guardar.":"Could not check availability. It will be checked when saving."):null;
   if(message==null)aliasFeedback.Hide();
   if(message!=null)aliasFeedback.PresentSemantic(state==AliasCheckState.Checking?AuthStatusVariant.Loading:state==AliasCheckState.Available?AuthStatusVariant.Success:state==AliasCheckState.Taken?AuthStatusVariant.Error:AuthStatusVariant.Warning,message);
   var field=this.Q<TextField>("DISPLAY_NAME");var input=field?.Q(className:"unity-base-field__input");
   if(input!=null){bool focused=field.Contains(panel?.focusController?.focusedElement as VisualElement);ThemeStyles.Border(input,state==AliasCheckState.Taken||state==AliasCheckState.Invalid?ThemeProvider.Current.Colors.Error:focused?ThemeProvider.Current.Colors.Primary:ThemeProvider.Current.Colors.Surface,2);}
   RefreshSubmit();
  }
  void Label(string value,string key,TextRole role=TextRole.Body){var l=new Label(value){name=key,enableRichText=false};ThemeStyles.Text(l,role);l.style.whiteSpace=WhiteSpace.Normal;l.style.unityTextAlign=TextAnchor.MiddleLeft;l.style.marginBottom=6;Add(l);}
  string Caption(string key,string fallback)=>BasicProfileCopy.Field(controller.Locale,key) ?? controller.CurrentStep?.questions?.FirstOrDefault(x=>x.key==key)?.title??fallback;
  void Decorate(VisualElement field,string key,string label){
   field.name=key;field.tooltip=label;field.style.height=field.style.minHeight=48;field.style.flexShrink=0;field.style.marginLeft=field.style.marginRight=field.style.marginTop=0;field.style.marginBottom=12;field.style.minWidth=0;
   ThemeStyles.Text(field,TextRole.Body);var input=field.Q(className:"unity-base-field__input")??field;input.style.backgroundColor=ThemeProvider.Current.Colors.Surface;input.style.color=ThemeProvider.Current.Colors.TextPrimary;ThemeStyles.Round(input,12);input.style.paddingLeft=input.style.paddingRight=12;input.style.paddingTop=input.style.paddingBottom=0;input.style.unityTextAlign=TextAnchor.MiddleLeft;
   void Border(bool focused)=>ThemeStyles.Border(input,(controller.ProfileErrors.Contains(key)||key=="DISPLAY_NAME"&&(controller.AliasState==AliasCheckState.Taken||controller.AliasState==AliasCheckState.Invalid))?ThemeProvider.Current.Colors.Error:focused?ThemeProvider.Current.Colors.Primary:ThemeProvider.Current.Colors.Surface,2);
   Border(false);field.RegisterCallback<FocusInEvent>(_=>{Border(true);schedule.Execute(()=>GetFirstAncestorOfType<ScrollView>()?.ScrollTo(field));});field.RegisterCallback<FocusOutEvent>(_=>Border(false));field.SetEnabled(!controller.ProfileLocked);Add(field);
  }
  void Field(string key,string caption,string value,Action<string> changed){caption=Caption(key,caption);Label(caption,key+"Label");var f=new TextField{value=value??"",isPasswordField=false};Decorate(f,key,caption);f.RegisterValueChangedCallback(e=>{changed(e.newValue);RefreshSubmit();});}
  void Select(string key,string caption,List<string> choices,string value,Func<string,string> format,Action<string> changed){caption=Caption(key,caption);Label(caption,key+"Label");var field=new PopupField<string>(choices,Math.Max(0,choices.IndexOf(value)),format,format);Decorate(field,key,caption);var text=field.Q<TextElement>();if(text!=null){text.style.overflow=Overflow.Hidden;text.style.textOverflow=TextOverflow.Ellipsis;text.style.minWidth=0;text.style.flexShrink=1;}field.RegisterValueChangedCallback(e=>{changed(e.newValue);RefreshSubmit();});}
  string CountryName(string code){if(code=="")return es?"Selecciona un país":"Select a country";try{return new RegionInfo(code).EnglishName+" ("+code+")";}catch(ArgumentException){return code;}}
 }
}
