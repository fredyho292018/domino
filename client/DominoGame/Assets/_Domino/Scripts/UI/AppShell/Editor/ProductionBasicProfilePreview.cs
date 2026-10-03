using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Domino.Infrastructure.Api;
using Domino.UI.AppShell;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
namespace Domino.Editor {
 public sealed partial class ProductionOnboardingPreview {
  bool copyTesting;
  [InitializeOnLoadMethod]static void CopyRequests(){EditorApplication.update+=()=>{
   if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists("Library/Onboarding02B.copy.request"))return;
   File.Delete("Library/Onboarding02B.copy.request");var w=GetWindow<ProductionOnboardingPreview>();w.copyTesting=true;w.keyboardTesting=w.basicTesting=w.validating=false;w.version=2;w.selected=10;w.size=1;w.locale="en";File.WriteAllText("Library/Onboarding02B.copy.txt","START="+DateTime.UtcNow.ToString("O")+"\n");w.Mount();
  };}
  void ValidateCopy(){try{
   bool es=locale=="es";
   BasicNeed(view.Q<Label>("OnboardingEyebrow").text==(es?"TU PERFIL":"YOUR PROFILE"),"EYEBROW_COPY");
   BasicNeed(view.Q<Label>("OnboardingTitle").text==(es?"Cuéntanos sobre ti":"Tell us about you"),"TITLE_COPY");
   var expected=es?new[]{"Nombre","Apellidos","Alias de jugador","País","Idioma preferido"}:new[]{"First name","Last name","Player alias","Country","Preferred language"};
   var keys=new[]{"FIRST_NAME","LAST_NAME","DISPLAY_NAME","COUNTRY","PREFERRED_LANGUAGE"};
   for(int i=0;i<keys.Length;i++)BasicNeed(view.Q<Label>(keys[i]+"Label").text==expected[i],"LABEL_COPY");
   BasicNeed(view.Q<Label>("DisplayNameHelp").text==(es?"Este es el nombre que verán los demás jugadores.":"This is the name other players will see."),"HELP_COPY");
   BasicNeed(view.Q<Button>("BasicProfileContinue").text==(es?"Continuar":"Continue"),"CTA_COPY");
   foreach(var label in view.Query<Label>().ToList())if(label.resolvedStyle.display!=DisplayStyle.None&&label.layout.width>0){
    BasicNeed(label.worldBound.xMin>=view.worldBound.xMin-.5f&&label.worldBound.xMax<=view.worldBound.xMax+.5f,"HORIZONTAL_OVERFLOW");
    BasicNeed(label.resolvedStyle.whiteSpace==WhiteSpace.Normal||label.text.Length==0,"TEXT_WRAP");
   }
   File.AppendAllText("Library/Onboarding02B.copy.txt",locale+" COPY=PASS TEXT_WRAP_393x852=PASS HORIZONTAL_OVERFLOW=0\n");
   if(locale=="en"){locale="es";Mount();}else{copyTesting=false;locale="en";File.AppendAllText("Library/Onboarding02B.copy.txt","FAIL=0\nFINAL=V2_BASIC_PROFILE_EMPTY_393x852_EN\n");Mount();}
  }catch(Exception e){copyTesting=false;File.AppendAllText("Library/Onboarding02B.copy.txt","FAIL="+e.Message+"\n");}}
  bool keyboardTesting;
  [InitializeOnLoadMethod]static void KeyboardRequests(){EditorApplication.update+=()=>{
   if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists("Library/Onboarding02B.keyboard.request"))return;
   File.Delete("Library/Onboarding02B.keyboard.request");var w=GetWindow<ProductionOnboardingPreview>();w.keyboardTesting=true;w.basicTesting=w.validating=false;w.size=0;w.version=2;w.locale="en";w.selected=10;File.WriteAllText("Library/Onboarding02B.keyboard.txt","START="+DateTime.UtcNow.ToString("O")+"\n");w.Mount();
  };}
  void ValidateKeyboard(){
   var root=view;var input=root.Q<TextField>("DISPLAY_NAME");
   root.parent.style.height=Mathf.Max(300,Sizes[size].y-300);input.Focus();
   root.schedule.Execute(()=>{
    try{
     foreach(var label in root.Query<Label>().ToList())if(label.resolvedStyle.display!=DisplayStyle.None&&label.layout.width>0)BasicNeed(label.worldBound.xMin>=root.worldBound.xMin-.5f&&label.worldBound.xMax<=root.worldBound.xMax+.5f,"TEXT_WRAP");
     BasicNeed(input.worldBound.yMin>=root.contentViewport.worldBound.yMin-1&&input.worldBound.yMax<=root.contentViewport.worldBound.yMax+1,"FOCUSED_FIELD_REACHABLE");
     var action=root.Q<Button>("BasicProfileContinue");root.ScrollTo(action);
     root.schedule.Execute(()=>{try{
      BasicNeed(action.worldBound.yMax<=root.contentViewport.worldBound.yMax+1,"KEYBOARD_CTA_REACHABLE");
      File.AppendAllText("Library/Onboarding02B.keyboard.txt",$"{locale} {Sizes[size]} FOCUS_SCROLL_WRAP=PASS\n");
      size++;if(size==Sizes.Length){size=0;if(locale=="en")locale="es";else{keyboardTesting=false;File.AppendAllText("Library/Onboarding02B.keyboard.txt","FAIL=0\nKEYBOARD_LAYOUT=16_PASS\n");size=1;locale="en";}}
      Mount();
     }catch(Exception e){keyboardTesting=false;File.AppendAllText("Library/Onboarding02B.keyboard.txt","FAIL="+e.Message+"\n");}}).StartingIn(150);
    }catch(Exception e){keyboardTesting=false;File.AppendAllText("Library/Onboarding02B.keyboard.txt","FAIL="+e.Message+"\n");}
   }).StartingIn(200);
  }
  bool basicTesting;int basicChecks;
  static BasicProfileDto ProfileFixture(string state){if(state=="BASIC_PROFILE_EMPTY"||!state.StartsWith("BASIC_PROFILE_"))return new BasicProfileDto{displayName="Guest-ABCDEFGH"};if(state=="BASIC_PROFILE_PARTIAL")return new BasicProfileDto{displayName="FixturePlayer"};return new BasicProfileDto{firstName="Prueba",lastName="Validación",displayName="FixturePlayer",countryCode="ES",preferredLocale="en",timeZone="UTC"};}
  async Task LoadFixture(){await controller.LoadAsync(locale);if(selected>=13&&selected<=16)await controller.SaveProfileAsync();if(selected>=22&&selected<=25)await controller.SaveExperienceAsync();if(selected==31||selected==32||selected==33||selected==36)await controller.SaveCoachAsync();if(selected>=39&&selected<=41)await controller.ContinueContactsAsync();await LoadMembershipFixture();}
  [InitializeOnLoadMethod]static void BasicRequests(){EditorApplication.update+=()=>{
   if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
   if(!File.Exists("Library/Onboarding02B.request"))return;File.Delete("Library/Onboarding02B.request");var w=GetWindow<ProductionOnboardingPreview>();w.basicTesting=true;w.validating=false;w.basicChecks=0;w.version=2;w.selected=10;w.size=0;w.locale="en";File.WriteAllText("Library/Onboarding02B.validation.txt","START="+DateTime.UtcNow.ToString("O")+"\n");w.Mount();
  };}
  void BasicNeed(bool ok,string key){if(!ok)throw new InvalidOperationException(key);basicChecks++;}
  void ValidateBasic(){try{
   var form=view.Q<ProductionBasicProfileView>();BasicNeed(form!=null,"FORM");BasicNeed(Mathf.Abs(view.layout.width-Sizes[size].x)<1,"ROOT_WIDTH");
   BasicNeed(view.Body.resolvedStyle.paddingTop>=48&&view.Body.resolvedStyle.paddingBottom>=48,"SAFE_AREA");BasicNeed(view.Body.layout.width<=620.1f,"MAX_WIDTH");BasicNeed(Mathf.Abs(view.Body.worldBound.center.x-view.worldBound.center.x)<1,"CENTER");
   foreach(var key in new[]{"FIRST_NAME","LAST_NAME","DISPLAY_NAME","COUNTRY","PREFERRED_LANGUAGE"}){var f=form.Q(key);BasicNeed(f!=null&&Mathf.Abs(f.layout.height-48)<.1f,"FIELD_HEIGHT");BasicNeed(f.worldBound.xMin>=view.worldBound.xMin&&f.worldBound.xMax<=view.worldBound.xMax+.1f,"OVERFLOW");BasicNeed(form.Q<Label>(key+"Label")?.text.Length>0,"LABEL");}
   var action=form.Q<Button>("BasicProfileContinue");BasicNeed(action.layout.height>=44,"CTA_TOUCH");BasicNeed(view.verticalScroller.highValue>=0,"SCROLL");view.ScrollTo(action);
   BasicNeed(action.worldBound.yMax<=view.contentViewport.worldBound.yMax+1,"CTA_REACHABLE");
   if(selected==10)BasicNeed(form.Q<TextField>("FIRST_NAME").value==""&&form.Q<TextField>("DISPLAY_NAME").value=="Guest-ABCDEFGH","EMPTY_WITH_GENERATED_ALIAS");
   if(selected==10){var alias=form.Q<TextField>("DISPLAY_NAME");alias.value="x";BasicNeed(!action.enabledSelf,"INVALID_ALIAS_DISABLED");alias.value="Guest-ABCDEFGH";BasicNeed(action.enabledSelf,"VALID_GUEST_CAN_CONTINUE");BasicNeed(form.Q<Label>("DISPLAY_NAMELabel").text==(locale=="es"?"Alias de jugador":"Player alias"),"ALIAS_LOCALIZATION");}
   if(selected==11)BasicNeed(form.Q<TextField>("DISPLAY_NAME").value=="FixturePlayer","PREFILL");
   if(selected==14)BasicNeed(controller.Busy&&!action.enabledSelf,"LOADING");
   if(selected==15)BasicNeed(controller.ProfileRetry,"UNCERTAIN_RETRY");
   if(selected==16)BasicNeed(controller.State.revision==8&&controller.Profile.FirstName=="Prueba","CONFLICT_RELOAD_PRESERVE");
   File.AppendAllText("Library/Onboarding02B.validation.txt",$"{locale} {States[selected]} {Sizes[size]} PASS\n");
   selected++;if(selected>=17){selected=10;size++;}if(size>=Sizes.Length){size=0;if(locale=="en")locale="es";else {basicTesting=false;File.AppendAllText("Library/Onboarding02B.validation.txt","CHECKS="+basicChecks+"_PASS\nFAIL=0\n");selected=10;size=1;locale="en";}}
   Mount();
  }catch(Exception e){basicTesting=false;File.AppendAllText("Library/Onboarding02B.validation.txt","FAIL="+e.Message+"\n");}}
 }
}
