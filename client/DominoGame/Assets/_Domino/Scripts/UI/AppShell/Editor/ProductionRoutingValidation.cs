using System;
using System.IO;
using System.Threading.Tasks;
using Domino.Identity;
using Domino.UI.AppShell;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Domino.Editor
{
    public sealed class ProductionRoutingValidation : EditorWindow
    {
        const string Result="Library/Routing02/Unity.validation.txt";
        RoutingCompositionFixture fixture;GameObject hostObject;int checks;
        static readonly string[] Cases={"NO_SESSION","EMAIL_UNVERIFIED","GUEST_NOT_STARTED","GUEST_IN_PROGRESS_BASIC_PROFILE","GUEST_IN_PROGRESS_EXPERIENCE","GUEST_IN_PROGRESS_COACH","GUEST_IN_PROGRESS_CONTACTS","GUEST_IN_PROGRESS_MEMBERSHIP","GUEST_COMPLETED","VERIFIED_NOT_STARTED","VERIFIED_IN_PROGRESS","VERIFIED_COMPLETED","LEGACY_COMPLETED","V1_IN_PROGRESS","V2_IN_PROGRESS","BOOTSTRAP_ERROR","ONBOARDING_ERROR","CLIENT_UPDATE_REQUIRED","SESSION_RESTORE","SESSION_SWITCH","LOGOUT","VERIFICATION_TO_VERIFIED"};
        static readonly Vector2Int[] Sizes={new Vector2Int(375,667),new Vector2Int(393,852),new Vector2Int(412,915),new Vector2Int(430,932),new Vector2Int(480,1040),new Vector2Int(600,960),new Vector2Int(768,1024),new Vector2Int(834,1194)};
        [InitializeOnLoadMethod]static void Register(){EditorApplication.update+=()=>{
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists("Library/Routing02/request"))return;
            try{File.Delete("Library/Routing02/request");}catch(IOException){return;}
            GetWindow<ProductionRoutingValidation>().Run();
        };}
        void Clean(){if(hostObject)DestroyImmediate(hostObject);hostObject=null;fixture?.Dispose();fixture=null;rootVisualElement.Clear();}
        void OnDisable()=>Clean();
        void Need(bool pass,string key){if(!pass)throw new Exception(key);checks++;}
        async void Run()
        {
            File.WriteAllText(Result,"START="+DateTime.UtcNow.ToString("O")+"\n");checks=0;titleContent=new GUIContent("COMPOSITION · ISOLATED");minSize=new Vector2(440,700);
            try{
                foreach(var c in Cases){
                    Clean();fixture=new RoutingCompositionFixture(c=="V1_IN_PROGRESS"?1:2);
                    bool verified=c.StartsWith("VERIFIED");if(verified||c=="EMAIL_UNVERIFIED"||c=="VERIFICATION_TO_VERIFIED")fixture.Session=new FirebaseAuthSessionSnapshot("routing-fixture",false,verified,true);
                    if(c=="NO_SESSION")fixture.Session=null;
                    if(c.Contains("COMPLETED"))fixture.State("COMPLETED",c=="LEGACY_COMPLETED"?(int?)null:2);
                    else if(c.Contains("IN_PROGRESS")){var step=c.StartsWith("GUEST_IN_PROGRESS_")?c.Substring("GUEST_IN_PROGRESS_".Length)+"_STEP":"COACH_STEP";fixture.State("IN_PROGRESS",c=="V1_IN_PROGRESS"?1:2,step);}
                    if(c=="BOOTSTRAP_ERROR")fixture.ErrorAt="BOOTSTRAP";if(c=="ONBOARDING_ERROR")fixture.ErrorAt="ONBOARDING";if(c=="CLIENT_UPDATE_REQUIRED")fixture.ErrorAt="UPDATE";
                    var frame=new VisualElement();frame.style.width=393;frame.style.height=852;frame.style.flexShrink=0;rootVisualElement.Add(frame);
                    hostObject=new GameObject("Isolated production route host"){hideFlags=HideFlags.HideAndDontSave};hostObject.SetActive(false);var host=hostObject.AddComponent<ProductionAuthHost>();host.BindIsolated(frame,fixture.Forms,fixture.Composition);
                    await fixture.Forms.RestoreAsync();
                    if(c=="LOGOUT"||c=="SESSION_SWITCH"){fixture.Auth.SignOut();await fixture.Forms.RestoreAsync();}
                    if(c=="VERIFICATION_TO_VERIFIED"){fixture.Session=new FirebaseAuthSessionSnapshot("routing-fixture",false,true,true);await fixture.Forms.CheckVerificationAsync();}
                    await Task.Delay(150);
                    var expected=c=="NO_SESSION"||c=="LOGOUT"||c=="SESSION_SWITCH"?ProductionAuthRoute.Welcome:c=="EMAIL_UNVERIFIED"?ProductionAuthRoute.VerificationPending:c=="CLIENT_UPDATE_REQUIRED"?ProductionAuthRoute.UpdateRequired:c.EndsWith("ERROR")?ProductionAuthRoute.Error:c.Contains("COMPLETED")?ProductionAuthRoute.AppShell:ProductionAuthRoute.Onboarding;
                    Need(fixture.Forms.Route==expected,c+"_ROUTE");Need(frame.childCount==1,c+"_SINGLE_TARGET");
                    Need(expected==ProductionAuthRoute.Onboarding?frame[0] is ProductionOnboardingRoot:expected==ProductionAuthRoute.AppShell?frame[0] is ProductionAppShell:expected==ProductionAuthRoute.Welcome?frame[0] is ProductionWelcomeView:expected==ProductionAuthRoute.VerificationPending?frame[0] is ProductionEmailView:frame[0] is ProductionRoutingStatusView,c+"_ACTUAL_VIEW");
                    Need(fixture.AuthWrites==0&&fixture.Server.Applied==0&&fixture.Server.TrialApplied==0,c+"_NO_MUTATIONS");File.AppendAllText(Result,c+"=PASS\n");
                }
                Clean();
                foreach(var size in Sizes)foreach(var route in new[]{ProductionAuthRoute.Loading,ProductionAuthRoute.Error,ProductionAuthRoute.UpdateRequired}){
                    rootVisualElement.Clear();var frame=new VisualElement();frame.style.width=size.x;frame.style.height=size.y;frame.style.flexShrink=0;rootVisualElement.Add(frame);
                    var view=new ProductionRoutingStatusView(route,"Could not load your session. Please try again.",()=>{});frame.Add(view);await Task.Delay(150);
                    Need(Mathf.Abs(view.layout.width-size.x)<1,"STATUS_WIDTH");Need(view.contentContainer.layout.width<=size.x+.1f,"STATUS_OVERFLOW");
                    foreach(var label in view.Query<Label>().ToList())Need(label.worldBound.xMin>=frame.worldBound.xMin-.1f&&label.worldBound.xMax<=frame.worldBound.xMax+.1f,"STATUS_TEXT_BOUNDS");
                    var retry=view.Q<Button>("RoutingRetry");Need(route==ProductionAuthRoute.Error?retry!=null&&retry.layout.height>=44:retry==null,"STATUS_ACTION");
                    if(retry!=null){view.ScrollTo(retry);await Task.Delay(50);Need(retry.worldBound.yMax<=frame.worldBound.yMax+.1f,"STATUS_REACHABLE");}
                }
                File.AppendAllText(Result,"CHECKS="+checks+"_PASS\nSCENARIOS=22_PASS\nRESPONSIVE=8/8_PASS\nFAIL=0\nREAL_OPERATIONS=0\nPLAY_MODE=OFF\n");
                rootVisualElement.Clear();var finalFrame=new VisualElement();finalFrame.style.width=393;finalFrame.style.height=852;finalFrame.style.flexShrink=0;rootVisualElement.Add(finalFrame);
                finalFrame.Add(new ProductionRoutingStatusView(ProductionAuthRoute.UpdateRequired,"Update the app to continue.",()=>{}));
            }catch(Exception e){File.AppendAllText(Result,"FAIL="+e.GetType().Name+":"+e.Message+"\n");}
        }
    }
}
