using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Domino.UI.Theming;
using UnityEngine.UIElements;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
namespace Domino.UI.AppShell {
 public sealed class ProductionProfileEditView:ScrollView,IDisposable {
  readonly ProfileEditController controller;bool es;readonly Action back;readonly VisualElement body,fields;
  readonly AuthStatusMessage feedback,aliasFeedback;readonly ThemeButton retryLoad;ThemeButton save;bool mounted,disposed;
  readonly Dictionary<string,AuthStatusMessage> fieldErrors=new Dictionary<string,AuthStatusMessage>();
  public ProductionProfileEditView(ProfileEditController controller,string locale,Action back):base(ScrollViewMode.Vertical){
   this.controller=controller;this.back=back;es=locale=="es";name="ProductionProfileEdit";style.flexGrow=1;style.minHeight=0;horizontalScrollerVisibility=ScrollerVisibility.Hidden;
   body=new VisualElement();ThemeStyles.Page(body);Add(body);
   var arrow=new Button(back){name="EditProfileBack",tooltip=es?"Volver sin guardar":"Back without saving"};var icon=new Image();ThemeStyles.Back(arrow,icon);arrow.Add(icon);body.Add(arrow);
   Text(es?"Editar perfil":"Edit Profile","EditProfileTitle",TextRole.PageTitle);
   feedback=new AuthStatusMessage{name="EditProfileFeedback"};body.Add(feedback);fields=new VisualElement();body.Add(fields);aliasFeedback=new AuthStatusMessage{name="EditAliasFeedback"};
   retryLoad=new ThemeButton(es?"Reintentar":"Retry",()=>{_=controller.Load();}){name="EditProfileRetryLoad"};body.Insert(body.IndexOf(fields),retryLoad);
   controller.Changed+=Refresh;controller.Saved+=back;RegisterCallback<AttachToPanelEvent>(_=>LocalizationSettings.SelectedLocaleChanged+=LocaleChanged);RegisterCallback<DetachFromPanelEvent>(_=>Dispose());Refresh();_=controller.Load();
  }
  void Text(string text,string key,TextRole role=TextRole.Body){var label=new Label(text){name=key};ThemeStyles.Text(label,role);label.style.whiteSpace=WhiteSpace.Normal;body.Add(label);}
  void Field(string key,string value,Action<string> change){var caption=BasicProfileCopy.Field(es?"es":"en",key);var label=new Label(caption){name=key+"Label"};ThemeStyles.Text(label,TextRole.Secondary);fields.Add(label);var input=new TextField{value=value,name=key,tooltip=caption};StyleField(input);input.style.height=48;input.style.minHeight=48;input.style.marginBottom=12;fields.Add(input);input.RegisterValueChangedCallback(e=>{change(e.newValue);controller.Edited();});input.RegisterCallback<FocusInEvent>(_=>schedule.Execute(()=>ScrollTo(input)).ExecuteLater(30));}
  void Choice(string key,List<string> choices,string value,Func<string,string> format,Action<string> change){var caption=BasicProfileCopy.Field(es?"es":"en",key);var label=new Label(caption){name=key+"Label"};ThemeStyles.Text(label,TextRole.Secondary);fields.Add(label);var input=new PopupField<string>(choices,Math.Max(0,choices.IndexOf(value)),format,format){name=key,tooltip=caption};StyleField(input);input.style.height=48;input.style.marginBottom=12;fields.Add(input);input.RegisterValueChangedCallback(e=>{change(e.newValue);controller.Edited();});input.RegisterCallback<FocusInEvent>(_=>schedule.Execute(()=>ScrollTo(input)).ExecuteLater(30));}
  void StyleField(VisualElement field){field.style.flexShrink=0;field.style.minWidth=0;ThemeStyles.Text(field,TextRole.Body);var input=field.Q(className:"unity-base-field__input")??field;input.style.backgroundColor=ThemeProvider.Current.Colors.Surface;input.style.color=ThemeProvider.Current.Colors.TextPrimary;ThemeStyles.Round(input,12);ThemeStyles.Border(input,ThemeProvider.Current.Colors.Surface,2);input.style.paddingLeft=input.style.paddingRight=12;input.style.paddingTop=input.style.paddingBottom=0;input.style.unityTextAlign=UnityEngine.TextAnchor.MiddleLeft;}
  void Refresh(){if(disposed)return;
   if(controller.Draft!=null&&!mounted){mounted=true;var d=controller.Draft;
    Field("FIRST_NAME",d.FirstName,v=>d.FirstName=v);Field("LAST_NAME",d.LastName,v=>d.LastName=v);Field("DISPLAY_NAME",d.DisplayName,v=>{_=controller.Alias(v);});
    var help=new Label(es?"Este es el nombre que verán los demás jugadores.":"This is the name other players will see."){name="EditAliasHelp"};ThemeStyles.Text(help,TextRole.Secondary);help.style.whiteSpace=WhiteSpace.Normal;fields.Add(help);fields.Add(aliasFeedback);
    Choice("COUNTRY",new[]{""}.Concat(BasicProfileRules.Countries).ToList(),d.Country,v=>{try{return v==""?(es?"Selecciona un país":"Select a country"):new RegionInfo(v).EnglishName+" ("+v+")";}catch{return v;}},v=>d.Country=v);
    Choice("PREFERRED_LANGUAGE",new List<string>{"","en","es"},d.Language,v=>v=="es"?"Español":v=="en"?"English":"—",v=>d.Language=v);
    foreach(var key in new[]{"FIRST_NAME","LAST_NAME","DISPLAY_NAME","COUNTRY","PREFERRED_LANGUAGE"}){var field=fields.Q(key);var error=new AuthStatusMessage{name=key+"Error"};fields.Insert(fields.IndexOf(field)+1,error);fieldErrors[key]=error;}
    save=new ThemeButton(es?"Guardar cambios":"Save changes",()=>{_=controller.Save();},true){name="EditProfileSave"};fields.Add(save);
    fields.Add(new ThemeButton(es?"Cancelar":"Cancel",back){name="EditProfileCancel"});
   }
   fields.SetEnabled(!controller.Busy);save?.SetEnabled(controller.CanSave);
   var invalid=controller.Error=="FIRST_NAME_INVALID"?"FIRST_NAME":controller.Error=="LAST_NAME_INVALID"?"LAST_NAME":controller.Error=="COUNTRY_INVALID"?"COUNTRY":controller.Error=="LANGUAGE_UNSUPPORTED"?"PREFERRED_LANGUAGE":controller.Error=="DISPLAY_NAME_INVALID"||controller.Error=="DISPLAY_NAME_RESERVED"?"DISPLAY_NAME":null;
   foreach(var entry in fieldErrors){if(entry.Key==invalid)entry.Value.PresentSemantic(AuthStatusVariant.Error,es?"Revisa este campo.":"Check this field.");else entry.Value.Hide();}
   var loadFailed=controller.Error=="LOAD_FAILED";retryLoad.style.display=loadFailed&&!controller.Busy?DisplayStyle.Flex:DisplayStyle.None;
   if(controller.Busy)feedback.PresentSemantic(AuthStatusVariant.Loading,es?"Procesando…":"Working…");
   else if(loadFailed)feedback.PresentSemantic(AuthStatusVariant.Error,es?"No se pudo cargar tu perfil. Inténtalo de nuevo.":"Could not load your profile. Please try again.");
   else if(controller.Error.Length>0&&controller.Error!="DISPLAY_NAME_TAKEN")feedback.PresentSemantic(AuthStatusVariant.Error,es?"No se pudo guardar. Tus datos siguen en el formulario. Revisa los campos e inténtalo de nuevo.":"Could not save. Your entries are preserved. Review the fields and try again.");else feedback.Hide();
   var state=controller.AliasState;var message=state==AliasCheckState.Taken?(es?"Ese alias ya está en uso. Prueba con otro.":"That alias is already taken. Try another one."):state==AliasCheckState.Available?(es?"Alias disponible.":"Alias available."):state==AliasCheckState.Checking?(es?"Comprobando disponibilidad…":"Checking availability…"):state==AliasCheckState.CheckFailed?(es?"No se pudo comprobar la disponibilidad. Se verificará al guardar.":"Could not check availability. It will be checked when saving."):null;
   if(message==null)aliasFeedback.Hide();else aliasFeedback.PresentSemantic(state==AliasCheckState.Taken?AuthStatusVariant.Error:state==AliasCheckState.Available?AuthStatusVariant.Success:AuthStatusVariant.Warning,message);
  }
  void LocaleChanged(Locale locale){if(locale!=null)ApplyLocale(locale.Identifier.Code);}
  public void ApplyLocale(string locale){
   if(disposed || (locale!="en" && locale!="es"))return;es=locale=="es";
   this.Q<Label>("EditProfileTitle").text=es?"Editar perfil":"Edit Profile";
   this.Q<Button>("EditProfileBack").tooltip=es?"Volver sin guardar":"Back without saving";
   retryLoad.text=es?"Reintentar":"Retry";
   foreach(var key in new[]{"FIRST_NAME","LAST_NAME","DISPLAY_NAME","COUNTRY","PREFERRED_LANGUAGE"}){
    var label=this.Q<Label>(key+"Label");if(label!=null)label.text=BasicProfileCopy.Field(locale,key);
    var field=this.Q(key);if(field!=null)field.tooltip=BasicProfileCopy.Field(locale,key);
   }
   var help=this.Q<Label>("EditAliasHelp");if(help!=null)help.text=es?"Este es el nombre que verán los demás jugadores.":"This is the name other players will see.";
   if(save!=null)save.text=es?"Guardar cambios":"Save changes";
   var cancel=this.Q<Button>("EditProfileCancel");if(cancel!=null)cancel.text=es?"Cancelar":"Cancel";
   Refresh();
  }
  public void Dispose(){LocalizationSettings.SelectedLocaleChanged-=LocaleChanged;if(disposed)return;disposed=true;controller.Changed-=Refresh;controller.Saved-=back;controller.Dispose();}
 }
}
