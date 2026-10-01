using System;
using System.IO;
using System.Linq;
using Domino.Infrastructure.Api;
using Domino.UI.AppShell;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
namespace Domino.Editor {
 public sealed partial class ProductionOnboardingPreview {
  bool experienceTesting, experiencePolish;int experienceChecks;Rect[] polishGeometry;
  static OnboardingAnswerDto[] ExperienceAnswers(string state){var key=state.StartsWith("EXPERIENCE_SELECTED_")?state.Substring("EXPERIENCE_SELECTED_".Length):new[]{"EXPERIENCE_LOADING","EXPERIENCE_NETWORK_ERROR","EXPERIENCE_REVISION_CONFLICT","EXPERIENCE_VALIDATION_ERROR"}.Contains(state)?"STRATEGY":null;return key==null?Array.Empty<OnboardingAnswerDto>():new[]{new OnboardingAnswerDto{questionKey="DOMINO_EXPERIENCE",type="SINGLE_SELECT",optionKey=key}};}
  // Editor-only fixture loads the canonical catalog copy; no backend or player session is involved.
  static OnboardingCatalogDto CatalogFixture(int version,string locale){
   var seed=JObject.Parse(File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../../../server/domino/src/main/resources/onboarding-catalog-v1.json"))));var copy=seed["translations"][locale];
   var result=new OnboardingCatalogDto{catalogVersion=version,locale=locale,steps=States.Skip(version==1?2:1).Take(version==1?4:5).Select((key,i)=>new OnboardingStepDto{key=key,title=(locale=="es"?new[]{"Perfil básico","Experiencia","Entrenador","Contactos","Membresía"}:new[]{"Basic profile","Experience","Coach","Contacts","Membership"})[Array.IndexOf(States,key)-1],sortOrder=i}).ToArray()};
   var step=result.steps.First(s=>s.key=="EXPERIENCE_STEP");step.title=(string)copy[step.key]["title"];
   var question=seed["questions"].First(q=>(string)q["key"]=="DOMINO_EXPERIENCE");step.questions=new[]{new OnboardingQuestionDto{key=(string)question["key"],type=(string)question["type"],required=(bool)question["required"],title=(string)copy["DOMINO_EXPERIENCE"]["title"],options=question["optionKeys"].Select((k,i)=>new OnboardingOptionDto{key=(string)k,title=(string)copy[(string)k]["title"],description=(string)copy[(string)k]["description"],sortOrder=i}).ToArray()}};AddCoachQuestion(result,seed,copy);AddContactsStep(result,copy);result.membershipCatalogVersion=1;var membership=result.steps.First(s=>s.key=="MEMBERSHIP_STEP");membership.required=false;membership.skippable=true;return result;
  }
  [InitializeOnLoadMethod]static void ExperienceRequests(){EditorApplication.update+=()=>{
   if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||(!File.Exists("Library/Onboarding02C.request")&&!File.Exists("Library/Onboarding02C.polish.request")))return;
   bool polish=File.Exists("Library/Onboarding02C.polish.request");File.Delete(polish?"Library/Onboarding02C.polish.request":"Library/Onboarding02C.request");var w=GetWindow<ProductionOnboardingPreview>();w.experiencePolish=polish;w.polishGeometry=null;w.experienceTesting=true;w.copyTesting=w.basicTesting=w.keyboardTesting=w.validating=false;w.experienceChecks=0;w.version=polish?2:1;w.selected=17;w.size=polish?1:0;w.locale="en";File.WriteAllText((w.experiencePolish?"Library/Onboarding02C.polish.txt":"Library/Onboarding02C.validation.txt"),"START="+DateTime.UtcNow.ToString("O")+"\n");w.Mount();
  };}
  void ExperienceNeed(bool value,string key){if(!value)throw new InvalidOperationException(key);experienceChecks++;}
  void ValidateExperience(){try{
   var form=view.Q<ProductionExperienceView>();ExperienceNeed(form!=null,"FORM");var cards=form.Query<OnboardingOptionCard>().ToList();ExperienceNeed(cards.Count==4,"FOUR_CARDS");
   ExperienceNeed(Mathf.Abs(view.Body.worldBound.center.x-view.worldBound.center.x)<1,"CENTER");ExperienceNeed(view.Body.layout.width<=620.1f,"MAX_WIDTH");ExperienceNeed(view.Body.resolvedStyle.paddingTop==48&&view.Body.resolvedStyle.paddingBottom==48,"SAFE_AREA");
   foreach(var card in cards){ExperienceNeed(card.layout.height>=104&&card.layout.width>=44,"TOUCH_TARGET");ExperienceNeed(card.worldBound.xMin>=view.worldBound.xMin&&card.worldBound.xMax<=view.worldBound.xMax+.1f,"OVERFLOW");foreach(var l in new[]{card.Title,card.Detail}){ExperienceNeed(l.text.Length>0,"COPY");ExperienceNeed(l.worldBound.yMin>=card.worldBound.yMin&&l.worldBound.yMax<=card.worldBound.yMax+.5f,"NO_CLIPPING");ExperienceNeed(l.resolvedStyle.whiteSpace==WhiteSpace.Normal,"WRAP");}ExperienceNeed((card.Check.text=="✓")== (card.name=="ExperienceOption_"+controller.ExperienceSelection),"SELECTION_INDICATOR");}
   var next=form.Q<Button>("ExperienceContinue");ExperienceNeed(next.layout.height>=44,"CTA");view.ScrollTo(next);ExperienceNeed(next.worldBound.yMax<=view.contentViewport.worldBound.yMax+1,"REACHABLE");
   ExperienceNeed((view.Q<Button>("ExperienceBack")!=null)==(version==2),"VERSION_BACK");ExperienceNeed(controller.State.catalogVersion==version,"PINNED_VERSION");
   if(selected==17)ExperienceNeed(!next.enabledSelf&&controller.ExperienceSelection==null,"EMPTY_BLOCKED");
   if(selected==22)ExperienceNeed(controller.Busy&&!next.enabledSelf,"BUSY");
   if(selected==23)ExperienceNeed(controller.ExperienceRetry&&controller.ExperienceSelection=="STRATEGY","NETWORK_PRESERVE");
   if(selected==24)ExperienceNeed(controller.State.revision==8&&controller.ExperienceSelection=="RULES_KNOWN","CONFLICT_RECONCILE");
   if(selected==25)ExperienceNeed(controller.ExperienceFeedback=="VALIDATION"&&controller.ExperienceSelection=="STRATEGY","VALIDATION_PRESERVE");
   File.AppendAllText((experiencePolish?"Library/Onboarding02C.polish.txt":"Library/Onboarding02C.validation.txt"),$"v{version} {locale} {States[selected]} {Sizes[size]} PASS\n");
   if(experiencePolish){
    var expected=Domino.UI.Theming.ThemeProvider.Current.Colors.TextSecondaryEmphasized;
    ExperienceNeed(view.Q<Label>("OnboardingEyebrow").resolvedStyle.color==expected,"EYEBROW_CONTRAST");
    foreach(var card in cards)ExperienceNeed(card.Detail.resolvedStyle.color==expected&&card.Detail.resolvedStyle.color!=card.Title.resolvedStyle.color,"DESCRIPTION_HIERARCHY");
    var geometry=cards.Select(c=>c.layout).Concat(new[]{next.layout}).ToArray();
    if(selected==17&&locale=="en"){polishGeometry=geometry;locale="es";Mount();return;}
    if(selected==17){locale="en";selected=20;Mount();return;}
    ExperienceNeed(controller.ExperienceSelection=="STRATEGY"&&next.enabledSelf,"SELECTED_CONTINUE");
    ExperienceNeed(geometry.SequenceEqual(polishGeometry),"SELECTION_GEOMETRY_SHIFT");
    experienceTesting=false;File.AppendAllText("Library/Onboarding02C.polish.txt","CHECKS="+experienceChecks+"_PASS\nSELECTION_GEOMETRY_SHIFT=0\nFAIL=0\n");return;
   }
   selected++;if(selected>=26){selected=17;size++;}if(size>=Sizes.Length){size=0;if(locale=="en")locale="es";else{locale="en";version++;}}
   if(version>2){experienceTesting=false;File.AppendAllText((experiencePolish?"Library/Onboarding02C.polish.txt":"Library/Onboarding02C.validation.txt"),"CHECKS="+experienceChecks+"_PASS\nFAIL=0\n");version=2;size=1;selected=17;locale="en";}
   Mount();
  }catch(Exception e){experienceTesting=false;File.AppendAllText((experiencePolish?"Library/Onboarding02C.polish.txt":"Library/Onboarding02C.validation.txt"),"FAIL="+e.Message+"\n");}}
 }
}
