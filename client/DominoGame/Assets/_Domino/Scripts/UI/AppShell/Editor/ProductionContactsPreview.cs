using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Domino.Infrastructure.Api;
using Domino.UI.AppShell;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
namespace Domino.Editor {
 public sealed partial class ProductionOnboardingPreview {
  bool contactsTesting,contactsPolish;int contactsChecks;Rect contactsReadyAction;
  static void AddContactsStep(OnboardingCatalogDto c,JToken copy){var step=c.steps.First(s=>s.key=="CONTACTS_STEP");step.title=(string)copy[step.key]["title"];step.description=(string)copy[step.key]["description"];step.required=false;step.skippable=true;step.availability="UNAVAILABLE";}
  sealed partial class Fixture {
   sealed class ContactsOperation {public bool back;}
   public object PrepareContactsSkip()=>new ContactsOperation();public object PrepareContactsBack()=>new ContactsOperation{back=true};
   public async Task<OnboardingStateDto> ExecuteContactsAsync(object operation,CancellationToken token){var request=(ContactsOperation)operation;
    if(state=="CONTACTS_LOADING")await Task.Delay(Timeout.Infinite,token);
    if(state=="CONTACTS_NETWORK_ERROR"&&!failed){failed=true;throw new DominoApiException(ApiFailure.Transport);}
    if(state=="CONTACTS_REVISION_CONFLICT"&&!failed){failed=true;current.revision=10;throw new DominoApiException(ApiFailure.Server,409,"REVISION_MISMATCH");}
    current.revision+=3;current.currentStepKey=request.back?"COACH_STEP":"MEMBERSHIP_STEP";if(!request.back)current.skippedStepKeys=new[]{"CONTACTS_STEP"};return current;
   }
  }
  [InitializeOnLoadMethod]static void ContactsRequests(){EditorApplication.update+=()=>{if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists("Library/Onboarding02E.request"))return;try{File.Delete("Library/Onboarding02E.request");}catch(IOException){return;}var w=GetWindow<ProductionOnboardingPreview>();w.contactsPolish=File.Exists("Library/Onboarding02E.polish");if(w.contactsPolish)File.Delete("Library/Onboarding02E.polish");w.contactsTesting=true;w.coachTesting=w.experienceTesting=w.copyTesting=w.basicTesting=w.keyboardTesting=w.validating=false;w.contactsChecks=0;w.version=1;w.selected=38;w.size=0;w.locale="en";if(w.contactsPolish){w.version=2;w.size=1;}File.WriteAllText("Library/Onboarding02E.validation.txt","START="+DateTime.UtcNow.ToString("O")+"\n");w.Mount();};}
  void ContactsNeed(bool value,string key){if(!value)throw new InvalidOperationException(key);contactsChecks++;}
  async void ValidateContacts(){try{
   var form=view.Q<ProductionContactsView>();ContactsNeed(form!=null,"FORM");ContactsNeed(!controller.CurrentStep.required&&controller.CurrentStep.skippable,"OPTIONAL_CONTRACT");ContactsNeed(Mathf.Abs(view.Body.worldBound.center.x-view.worldBound.center.x)<1,"CENTER");ContactsNeed(view.Body.layout.width<=620.1f,"MAX_WIDTH");ContactsNeed(view.Body.resolvedStyle.paddingTop==48&&view.Body.resolvedStyle.paddingBottom==48,"SAFE_AREA");
   foreach(var label in new[]{view.Q<Label>("OnboardingTitle"),form.Q<Label>("ContactsOptional"),form.Q<Label>("ContactsExplanation")}){ContactsNeed(!string.IsNullOrEmpty(label.text),"COPY");ContactsNeed(label.resolvedStyle.whiteSpace==WhiteSpace.Normal,"WRAP");ContactsNeed(label.worldBound.xMin>=view.worldBound.xMin&&label.worldBound.xMax<=view.worldBound.xMax+.1f,"OVERFLOW");ContactsNeed(label.layout.height>=label.MeasureTextSize(label.text,label.layout.width,VisualElement.MeasureMode.Exactly,0,VisualElement.MeasureMode.Undefined).y-1,"TEXT_CLIPPING");}
   ContactsNeed(form.Q<Label>("ContactsOptional").text==(locale=="es"?"OPCIONAL":"OPTIONAL"),"OPTIONAL_COPY");var next=form.Q<Button>("ContactsContinue");ContactsNeed(view.Q<Label>("OnboardingEyebrow").text==(locale=="es"?"TU GENTE":"YOUR PEOPLE"),"EYEBROW");ContactsNeed(form.Q<Image>("ContactsPeopleIcon").vectorImage!=null,"BUNDLED_ICON");ContactsNeed(next.text==(controller.ContactsRetry?(locale=="es"?"Reintentar":"Retry"):(locale=="es"?"Ahora no":"Not now")),"CTA_COPY");ContactsNeed(next.layout.height>=44,"TOUCH");ContactsNeed(view.Q<Button>("ContactsBack").layout.height>=44,"BACK_TOUCH");
   if(selected==38)contactsReadyAction=next.layout;else ContactsNeed(next.layout==contactsReadyAction,"LOADING_GEOMETRY");
   view.scrollOffset=new Vector2(0,view.contentContainer.WorldToLocal(next.worldBound.min).y);await Task.Delay(40);ContactsNeed(next.worldBound.yMin>=view.contentViewport.worldBound.yMin-1&&next.worldBound.yMax<=view.contentViewport.worldBound.yMax+1,"REACHABLE");
   ContactsNeed(controller.State.catalogVersion==version,"PINNED_VERSION");ContactsNeed(controller.State.currentStepKey=="CONTACTS_STEP","CURRENT_STEP");
   if(selected==38)ContactsNeed(next.enabledSelf,"READY");if(selected==39)ContactsNeed(controller.Busy&&!next.enabledSelf,"LOADING");if(selected==40)ContactsNeed(controller.ContactsRetry&&controller.State.revision==7,"NETWORK");if(selected==41)ContactsNeed(controller.ContactsFeedback=="CONFLICT"&&controller.State.revision==10,"CONFLICT");
   File.AppendAllText("Library/Onboarding02E.validation.txt",$"v{version} {locale} {States[selected]} {Sizes[size]} PASS\n");if(contactsPolish){if(locale=="en"){locale="es";Mount();return;}contactsTesting=false;File.AppendAllText("Library/Onboarding02E.validation.txt","POLISH_CHECKS="+contactsChecks+"_PASS\nFAIL=0\n");version=2;size=1;selected=38;locale="en";Mount();return;}selected++;if(selected>=42){selected=38;size++;}if(size>=Sizes.Length){size=0;if(locale=="en")locale="es";else{locale="en";version++;}}if(version>2){contactsTesting=false;File.AppendAllText("Library/Onboarding02E.validation.txt","CHECKS="+contactsChecks+"_PASS\nFAIL=0\n");version=2;size=1;selected=38;locale="en";}Mount();
  }catch(Exception e){contactsTesting=false;File.AppendAllText("Library/Onboarding02E.validation.txt","FAIL="+e.Message+"\n");}}
 }
}
