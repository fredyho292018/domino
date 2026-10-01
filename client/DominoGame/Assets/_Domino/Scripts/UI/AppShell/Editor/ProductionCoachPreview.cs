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
  bool coachTesting,coachPolish;int coachChecks;
  static void AddCoachQuestion(OnboardingCatalogDto c,JObject seed,JToken copy){c.coachCatalogVersion=1;var step=c.steps.First(s=>s.key=="COACH_STEP");step.title=(string)copy["COACH_STEP"]["title"];step.questions=new[]{new OnboardingQuestionDto{key="COACH_SELECTION",type="COACH_SELECT",required=true,title=(string)copy["COACH_SELECTION"]["title"]}};}
  static OnboardingAnswerDto[] CoachAnswers(string state){var k=state.StartsWith("COACH_SELECTED_")?state.Substring(15):state=="COACH_MISSING_SAVED"?"HISTORICAL":new[]{"COACH_LOADING","COACH_NETWORK_ERROR","COACH_REVISION_CONFLICT","COACH_VALIDATION_ERROR"}.Contains(state)?"MATEO":null;return k==null?Array.Empty<OnboardingAnswerDto>():new[]{new OnboardingAnswerDto{questionKey="COACH_SELECTION",type="COACH_SELECT",optionKey=k}};}
  sealed partial class Fixture {
   public Task<CoachCatalogDto> LoadCoachesAsync(string locale,int? version,CancellationToken token){var json=JObject.Parse(File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../../../server/domino/src/main/resources/coach-catalog-v1.json"))));var copy=json["translations"][locale];var items=json["coaches"].Where(c=>(bool)c["active"]).OrderBy(c=>(int)c["sortOrder"]).Select(c=>new CoachDto{key=(string)c["key"],name=(string)copy[(string)c["key"]]["name"],selectable=true,avatar=new CoachAvatarDto{key=(string)c["avatarKey"],assetVersion=1}}).ToList();
    if(state=="COACH_UNKNOWN_AVATAR")items[0].avatar.key="UNKNOWN_FIXTURE";
    if(state=="COACH_DYNAMIC")for(int i=0;i<5;i++)items.Add(new CoachDto{key="FIXTURE_"+i,name="Fixture Coach "+i,selectable=true});
    return Task.FromResult(new CoachCatalogDto{catalogVersion=1,resolvedLocale=locale,items=items.ToArray()});}
   sealed class CoachSave {public string key;public bool back;}
   public object PrepareCoach(string question,string key)=>new CoachSave{key=key};public object PrepareCoachBack()=>new CoachSave{back=true};
   public async Task<OnboardingStateDto> ExecuteCoachAsync(object operation,CancellationToken token){var save=(CoachSave)operation;
    if(state=="COACH_LOADING")await Task.Delay(Timeout.Infinite,token);
    if(state=="COACH_NETWORK_ERROR"&&!failed){failed=true;throw new DominoApiException(ApiFailure.Transport);}
    if(state=="COACH_VALIDATION_ERROR")throw new DominoApiException(ApiFailure.Server,400,"COACH_NOT_SELECTABLE");
    if(state=="COACH_REVISION_CONFLICT"&&!failed){failed=true;current.revision=8;current.answers=CoachAnswers("COACH_SELECTED_AMARA");throw new DominoApiException(ApiFailure.Server,409,"REVISION_MISMATCH");}
    current.revision++;current.currentStepKey=save.back?"EXPERIENCE_STEP":"CONTACTS_STEP";if(!save.back)current.answers=new[]{new OnboardingAnswerDto{questionKey="COACH_SELECTION",type="COACH_SELECT",optionKey=save.key}};return current;
   }
  }
  [InitializeOnLoadMethod]static void CoachRequests(){EditorApplication.update+=()=>{if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||(!File.Exists("Library/Onboarding02D.request")&&!File.Exists("Library/Onboarding02D.polish.request")))return;bool polish=File.Exists("Library/Onboarding02D.polish.request");try{File.Delete(polish?"Library/Onboarding02D.polish.request":"Library/Onboarding02D.request");}catch(IOException){return;}var w=GetWindow<ProductionOnboardingPreview>();w.coachTesting=true;w.coachPolish=polish;w.experienceTesting=w.copyTesting=w.basicTesting=w.keyboardTesting=w.validating=false;w.coachChecks=0;w.version=polish?2:1;w.selected=26;w.size=polish?1:0;w.locale="en";File.WriteAllText((w.coachPolish?"Library/Onboarding02D.polish.txt":"Library/Onboarding02D.validation.txt"),"START="+DateTime.UtcNow.ToString("O")+"\n");w.Mount();};}
  void CoachNeed(bool value,string key){if(!value)throw new InvalidOperationException(key);coachChecks++;}
  async void ValidateCoach(){try{
   var form=view.Q<ProductionCoachView>();CoachNeed(form!=null,"FORM");var cards=form.Query<CoachOptionCard>().ToList();CoachNeed(cards.Count==(selected==35?15:10),"COUNT");CoachNeed(Mathf.Abs(view.Body.worldBound.center.x-view.worldBound.center.x)<1,"CENTER");CoachNeed(view.Body.resolvedStyle.paddingTop==48&&view.Body.resolvedStyle.paddingBottom==48,"SAFE_AREA");
   var keys=controller.Coaches.items.Where(c=>c.selectable).Select(c=>"Coach_"+c.key);CoachNeed(cards.Select(c=>c.name).SequenceEqual(keys),"SERVER_ORDER");
   foreach(var card in cards){CoachNeed(card.layout.width>=44&&card.layout.height>=44,"TOUCH");CoachNeed(card.worldBound.xMin>=view.worldBound.xMin&&card.worldBound.xMax<=view.worldBound.xMax+.1f,"OVERFLOW");CoachNeed(card.NameLabel.worldBound.yMax<=card.worldBound.yMax&&card.NameLabel.worldBound.xMax<=card.worldBound.xMax,"CLIPPING");CoachNeed(card.NameLabel.resolvedStyle.whiteSpace==WhiteSpace.Normal,"WRAP");CoachNeed(card.Portrait.scaleMode==ScaleMode.ScaleToFit,"ASPECT");if(selected!=34&&selected!=35)CoachNeed(card.Portrait.image!=null,"AVATAR");view.scrollOffset=new Vector2(0,view.contentContainer.WorldToLocal(card.worldBound.min).y);await Task.Delay(40);CoachNeed(card.worldBound.yMax<=view.contentViewport.worldBound.yMax+1,"CARD_REACHABLE_"+card.name+"_"+card.worldBound+"_"+view.contentViewport.worldBound);}
   foreach(var row in form.Query<VisualElement>("CoachRow").ToList()){var pair=row.Query<CoachOptionCard>().ToList();if(pair.Count==2)CoachNeed(Mathf.Abs(pair[0].layout.width-pair[1].layout.width)<=1&&Mathf.Abs(pair[0].layout.height-pair[1].layout.height)<1,"ROW_GEOMETRY_"+pair[0].layout+"_"+pair[1].layout);}
   var next=form.Q<Button>("CoachContinue");view.scrollOffset=new Vector2(0,view.contentContainer.WorldToLocal(next.worldBound.min).y);await Task.Delay(40);CoachNeed(next.worldBound.yMax<=view.contentViewport.worldBound.yMax+1,"CONTINUE_REACHABLE");CoachNeed(next.worldBound.yMin>=cards.Last().worldBound.yMax,"CTA_OVERLAP");
   if(selected==31)CoachNeed(controller.Busy&&!next.enabledSelf&&controller.CoachSelection=="MATEO","LOADING");if(selected==32)CoachNeed(controller.CoachRetry&&controller.CoachSelection=="MATEO","NETWORK");if(selected==33)CoachNeed(controller.State.revision==8&&controller.CoachSelection=="AMARA"&&controller.CoachFeedback=="CONFLICT","CONFLICT");if(selected==36)CoachNeed(controller.CoachFeedback=="VALIDATION"&&controller.CoachSelection=="MATEO","REJECTION");if(selected==26)CoachNeed(!next.enabledSelf,"EMPTY_BLOCKED");if(selected==34){CoachNeed(cards[0].Q("NeutralCoachPortrait")!=null,"FALLBACK");controller.SelectCoach(cards[0].name.Substring(6));CoachNeed(cards[0].Check.text=="✓"&&next.enabledSelf,"FALLBACK_SELECTABLE");}if(selected==37)CoachNeed(controller.MissingSavedCoach&&!next.enabledSelf,"HISTORICAL_SAFE");
   if(!controller.CoachLocked&&selected!=37){var before=view.scrollOffset;var rects=cards.Select(c=>c.layout).ToArray();controller.SelectCoach(cards.Last().name.Substring(6));CoachNeed(view.scrollOffset==before,"SCROLL_PRESERVED");CoachNeed(cards.Select(c=>c.layout).SequenceEqual(rects),"SELECTION_GEOMETRY");CoachNeed(cards.Last().Check.text=="✓"&&next.enabledSelf,"SELECTED");}
   File.AppendAllText((coachPolish?"Library/Onboarding02D.polish.txt":"Library/Onboarding02D.validation.txt"),$"v{version} {locale} {States[selected]} {Sizes[size]} PASS\n");if(coachPolish){
    var eyebrow=view.Q<Label>("OnboardingEyebrow");CoachNeed(eyebrow.text==(locale=="es"?"TU ENTRENADOR":"YOUR COACH"),"HEADER_COPY");CoachNeed(eyebrow.worldBound.xMin>=view.worldBound.xMin&&eyebrow.worldBound.xMax<=view.worldBound.xMax,"HEADER_OVERFLOW");
    if(selected==26&&locale=="en"){locale="es";Mount();return;}
    if(selected==26){locale="en";selected=29;Mount();return;}
    controller.SelectCoach("MATEO");var mateo=form.Q<CoachOptionCard>("Coach_MATEO");view.scrollOffset=new Vector2(0,view.contentContainer.WorldToLocal(mateo.worldBound.min).y);await Task.Delay(60);
    CoachNeed(controller.CoachSelection=="MATEO"&&mateo.Check.text=="✓","FINAL_SELECTION");CoachNeed(mateo.resolvedStyle.borderTopColor==Domino.UI.Theming.ThemeProvider.Current.Colors.Primary&&mateo.resolvedStyle.borderTopWidth==2,"SELECTED_BORDER");CoachNeed(mateo.worldBound.yMin>=view.contentViewport.worldBound.yMin-1&&mateo.worldBound.yMax<=view.contentViewport.worldBound.yMax+1,"MATEO_VISIBLE");
    coachTesting=false;File.AppendAllText("Library/Onboarding02D.polish.txt","CHECKS="+coachChecks+"_PASS\nFINAL=V2_COACH_SELECTED_MATEO_393x852_EN\nFAIL=0\n");return;
   }
   selected++;if(selected>=38){selected=26;size++;}if(size>=Sizes.Length){size=0;if(locale=="en")locale="es";else{locale="en";version++;}}if(version>2){coachTesting=false;File.AppendAllText((coachPolish?"Library/Onboarding02D.polish.txt":"Library/Onboarding02D.validation.txt"),"CHECKS="+coachChecks+"_PASS\nFAIL=0\n");version=2;size=1;selected=26;locale="en";}Mount();
  }catch(Exception e){coachTesting=false;File.AppendAllText((coachPolish?"Library/Onboarding02D.polish.txt":"Library/Onboarding02D.validation.txt"),"FAIL="+e.Message+"\n");}}
 }
}
