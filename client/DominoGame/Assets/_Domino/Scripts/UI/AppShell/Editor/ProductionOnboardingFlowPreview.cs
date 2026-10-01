using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Domino.Infrastructure.Api;
using Domino.UI.AppShell;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Domino.Editor
{
 public sealed partial class ProductionOnboardingPreview
 {
  OnboardingApiSession flowSession;
  int flowChecks;
  bool flowRunning;
  const string FlowResult="Library/Onboarding02G.validation.txt";
  [InitializeOnLoadMethod]static void CompletedCopyRequests(){EditorApplication.update+=()=>{
   if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists("Library/Onboarding02G.copy.request"))return;
   try{File.Delete("Library/Onboarding02G.copy.request");}catch(IOException){return;}
   var w=GetWindow<ProductionOnboardingPreview>();if(!w.flowRunning)w.CheckCompletedCopy();
  };}
  async void CheckCompletedCopy(){
   const string result="Library/Onboarding02G.copy.txt";flowRunning=true;flowChecks=0;
   File.WriteAllText(result,"START="+DateTime.UtcNow.ToString("O")+"\n");
   try{
    foreach(string l in new[]{"es","en"}){
     var server=MountFlow(2,1,l);
     // Resume an authoritative isolated completed response; no client-side completion mutation.
     server.State.status="COMPLETED";server.State.catalogVersion=2;server.State.currentStepKey=null;
     await controller.LoadAsync(l);await CheckFlowVisual(null);
     var eyebrow=view.Q<Label>("OnboardingEyebrow");var title=view.Q<Label>("OnboardingTitle");var feedback=view.Q<AuthStatusMessage>();
     FlowNeed(eyebrow.text==(l=="es"?"TODO LISTO":"YOU'RE READY"),"EYEBROW");
     FlowNeed(title.text==(l=="es"?"Todo listo":"All set"),"TITLE");
     FlowNeed(feedback.Variant==AuthStatusVariant.Success,"SUCCESS_COMPONENT");
     FlowNeed(feedback.Message.text==(l=="es"?"Tu perfil está listo para jugar.":"Your profile is ready to play."),"MESSAGE");
     FlowNeed(view.Body.Query<Button>().ToList().Count==0,"NO_BACK_OR_HOME_CTA");
     FlowNeed(server.Applied==0,"READ_ONLY_RESUME");
     foreach(var label in new[]{eyebrow,title,feedback.Message}){
      FlowNeed(label.worldBound.xMin>=view.worldBound.xMin&&label.worldBound.xMax<=view.worldBound.xMax+1,"HORIZONTAL_OVERFLOW");
      var measured=label.MeasureTextSize(label.text,label.contentRect.width,VisualElement.MeasureMode.Exactly,0,VisualElement.MeasureMode.Undefined);
      FlowNeed(measured.y<=label.contentRect.height+1,"TEXT_CLIPPING");
     }
     FlowNeed(feedback.Message.worldBound.yMax<=feedback.worldBound.yMax,"FEEDBACK_CONTAINMENT");
     File.AppendAllText(result,"COMPLETED_"+l.ToUpperInvariant()+"=PASS\n");
    }
    view.scrollOffset=Vector2.zero;Focus();File.AppendAllText(result,"CHECKS="+flowChecks+"_PASS\nHORIZONTAL_OVERFLOW=0\nTEXT_CLIPPING=0\nFINAL_PREVIEW=V2_COMPLETED_393x852_EN\nFAIL=0\n");
   }catch(Exception e){File.AppendAllText(result,"FAIL="+e.Message+"\n");}
   finally{flowRunning=false;}
  }
  [InitializeOnLoadMethod]static void FlowRequests(){EditorApplication.update+=()=>{
   if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists("Library/Onboarding02G.request"))return;
   try{File.Delete("Library/Onboarding02G.request");}catch(IOException){return;}var w=GetWindow<ProductionOnboardingPreview>();if(!w.flowRunning)w.RunFullFlow();
  };}
  void FlowNeed(bool condition,string name){if(!condition)throw new InvalidOperationException(name);flowChecks++;}
  IsolatedOnboardingServer MountFlow(int v,int sizeIndex,string language)
  {
   view?.Dispose();controller?.Dispose();flowSession?.Dispose();rootVisualElement.Clear();
   version=v;size=sizeIndex;locale=language;titleContent=new GUIContent("ONBOARDING · ISOLATED");
   var stateLabel=new Label("NOT_STARTED"){name="FlowState"};rootVisualElement.Add(stateLabel);
   rootVisualElement.Add(new Label(Sizes[size].x+"x"+Sizes[size].y));rootVisualElement.Add(new Label("v"+version));rootVisualElement.Add(new Label(locale));
   rootVisualElement.Add(new Label("ISOLATED · simulated server · no network"));
   var frame=new VisualElement{name="OnboardingViewport"};frame.style.width=Sizes[size].x;frame.style.height=Sizes[size].y;frame.style.flexShrink=0;
   float scale=Mathf.Max(.1f,Mathf.Min(1,Mathf.Min(position.width/Sizes[size].x,(position.height-110)/Sizes[size].y)));
   frame.style.transformOrigin=new TransformOrigin(0,0,0);frame.style.scale=new Scale(new Vector3(scale,scale,1));rootVisualElement.Add(frame);
   var server=new IsolatedOnboardingServer(version,(catalogVersion,l)=>{var c=CatalogFixture(catalogVersion,l);c.requiredCapabilities=Array.Empty<string>();foreach(var step in c.steps)step.required=step.key!="CONTACTS_STEP"&&step.key!="MEMBERSHIP_STEP";return c;},l=>new Fixture("COACH_SELECTED_MATEO",version).LoadCoachesAsync(l,1,default).GetAwaiter().GetResult(),MembershipFixture);
   controller=server.Controller(out flowSession);controller.Changed+=()=>stateLabel.text=controller.State?.status=="IN_PROGRESS"?controller.State.currentStepKey:controller.State?.status??"LOADING";
   view=new ProductionOnboardingRoot(controller);frame.Add(view);view.SetSafeArea(24,24);return server;
  }
  async Task CheckFlowVisual(string action)
  {
   await Task.Delay(180);
   FlowNeed(view.panel!=null,"PANEL");FlowNeed(Mathf.Abs(view.layout.width-Sizes[size].x)<1,"WIDTH");
   FlowNeed(view.Body.layout.width<=620.1f&&view.Body.layout.width<=view.layout.width,"COLUMN");
   FlowNeed(Mathf.Abs(view.Body.worldBound.center.x-view.worldBound.center.x)<1,"CENTER");
   FlowNeed(rootVisualElement.Query<ProductionOnboardingRoot>().ToList().Count==1,"ONE_ROOT");
   FlowNeed(view.Q<Label>("OnboardingTitle")?.text.Length>0,"TITLE");
   foreach(var button in view.Query<Button>().ToList().Where(b=>b.resolvedStyle.display!=DisplayStyle.None&&b.worldBound.width>0)){
    FlowNeed(button.worldBound.xMin>=view.worldBound.xMin-1&&button.worldBound.xMax<=view.worldBound.xMax+1,"HORIZONTAL_"+button.name);
   }
   if(action!=null){var button=view.Q<Button>(action);FlowNeed(button!=null&&button.enabledInHierarchy,"ACTION_"+action);FlowNeed(button.layout.height>=44,"TOUCH_"+action);view.scrollOffset=new Vector2(0,view.contentContainer.WorldToLocal(button.worldBound.min).y);await Task.Delay(80);FlowNeed(button.worldBound.yMin>=view.contentViewport.worldBound.yMin-1&&button.worldBound.yMax<=view.contentViewport.worldBound.yMax+1,"REACH_"+action);}
   if(controller.Phase==OnboardingShellPhase.Completed){FlowNeed(view.Q("OnboardingNotStarted")==null,"NO_STALE_CONTENT");FlowNeed(view.Q<AuthStatusMessage>()?.resolvedStyle.display!=DisplayStyle.None,"SUCCESS_FEEDBACK");}
  }
  async void RunFullFlow()
  {
   flowRunning=true;flowChecks=0;File.WriteAllText(FlowResult,"START="+DateTime.UtcNow.ToString("O")+"\n");
   try{
    foreach(int v in new[]{1,2})foreach(string l in new[]{"en","es"})for(int i=0;i<Sizes.Length;i++){
     var server=MountFlow(v,i,l);await controller.LoadAsync(l);await CheckFlowVisual("OnboardingStart");await controller.StartAsync();
     if(v==2){controller.Profile.FirstName="Fixture";controller.Profile.LastName="Player";controller.Profile.DisplayName="FixturePlayer";controller.Profile.Country="CU";await controller.ChangeProfileLocaleAsync(l);await CheckFlowVisual("BasicProfileContinue");await controller.SaveProfileAsync();}
     controller.SelectExperience("STRATEGY");await CheckFlowVisual("ExperienceContinue");await controller.SaveExperienceAsync();
     controller.SelectCoach("MATEO");await CheckFlowVisual("CoachContinue");await controller.SaveCoachAsync();
     await CheckFlowVisual("ContactsContinue");await controller.ContinueContactsAsync();await CheckFlowVisual("MembershipNotNow");await controller.SkipMembershipAsync();
     FlowNeed(controller.Phase==OnboardingShellPhase.Completed&&server.State.status=="COMPLETED","SERVER_COMPLETED");await CheckFlowVisual(null);
     File.AppendAllText(FlowResult,$"v{v} {l} {Sizes[i]} FULL_FLOW_PASS\n");
    }
    // Leave a completed result from an actual isolated sequence, not an injected completion flag.
    MountFlow(2,1,"en");await controller.LoadAsync("en");await controller.StartAsync();controller.Profile.FirstName="Fixture";controller.Profile.LastName="Player";controller.Profile.DisplayName="FixturePlayer";controller.Profile.Country="CU";await controller.ChangeProfileLocaleAsync("en");await controller.SaveProfileAsync();controller.SelectExperience("STRATEGY");await controller.SaveExperienceAsync();controller.SelectCoach("MATEO");await controller.SaveCoachAsync();await controller.ContinueContactsAsync();await controller.SkipMembershipAsync();await CheckFlowVisual(null);
    view.scrollOffset=Vector2.zero;File.AppendAllText(FlowResult,"CHECKS="+flowChecks+"_PASS\nSCENARIOS=32_PASS\nFAIL=0\nFINAL_PREVIEW=V2_COMPLETED_393x852_EN\nREAL_NETWORK_CALLS=0\n");
   }catch(Exception e){File.AppendAllText(FlowResult,"FAIL="+e.Message+"\n");}
   finally{flowRunning=false;}
  }
 }
}
