using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Domino.Identity;
using Domino.Infrastructure.Api;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Domino.Editor
{
    // Diagnostic route target harness, not an alternative production screen/router.
    public sealed class AuthenticatedRoutingPreview : EditorWindow
    {
        static readonly string[] Scenarios={"NO_SESSION","GUEST_NOT_STARTED","GUEST_IN_PROGRESS","GUEST_COMPLETED","EMAIL_UNVERIFIED","EMAIL_VERIFIED_NOT_STARTED","EMAIL_VERIFIED_IN_PROGRESS","EMAIL_VERIFIED_COMPLETED","BOOTSTRAP_ERROR","ONBOARDING_STATE_ERROR","CLIENT_UPDATE_REQUIRED","SESSION_SWITCH"};
        int selected;
        AuthenticatedRoutingOrchestrator router;
        Fixture fixture;
        Label target,message,step;
        Button retry;
        const string Result="Library/OnboardingRouting01.validation.txt";
        sealed class Fixture:IRoutingSessionSource,IRoutingPlayerBinding
        {
            public string Scenario;public int Writes=>0; // This source exposes no mutation operation.
            public Task<FirebaseAuthSessionSnapshot> ResolveAsync(CancellationToken token)=>Task.FromResult(Scenario=="NO_SESSION"?null:new FirebaseAuthSessionSnapshot("isolated-routing-owner",!Scenario.StartsWith("EMAIL"),Scenario!="EMAIL_UNVERIFIED",Scenario.StartsWith("EMAIL")));
            public IRoutingPlayerBinding Bind(FirebaseAuthSessionSnapshot identity)=>this;
            public Task<string> BootstrapAsync(CancellationToken token){if(Scenario=="BOOTSTRAP_ERROR")throw new DominoApiException(ApiFailure.Transport);if(Scenario=="CLIENT_UPDATE_REQUIRED")throw new DominoApiException(ApiFailure.Server,409,"CLIENT_UPDATE_REQUIRED");return Task.FromResult("isolated-routing-owner");}
            public Task<OnboardingStateDto> ReadOnboardingAsync(CancellationToken token){if(Scenario=="ONBOARDING_STATE_ERROR")throw new DominoApiException(ApiFailure.Transport);var status=Scenario.EndsWith("NOT_STARTED")?"NOT_STARTED":Scenario.EndsWith("COMPLETED")?"COMPLETED":"IN_PROGRESS";return Task.FromResult(new OnboardingStateDto{status=status,catalogVersion=status=="NOT_STARTED"?(int?)null:2,currentStepKey=status=="IN_PROGRESS"?"COACH_STEP":null,revision=7});}
            public void Dispose(){}
        }
        [MenuItem("Domino/Production Onboarding/Routing preview (isolated)")]
        public static void Open()=>GetWindow<AuthenticatedRoutingPreview>().Mount();
        void CreateGUI()=>Mount();
        void OnDisable()=>router?.Dispose();
        void Mount()
        {
            router?.Dispose();rootVisualElement.Clear();titleContent=new GUIContent("ROUTING · ISOLATED");minSize=new Vector2(400,300);
            var picker=new PopupField<string>(Scenarios.ToList(),selected);picker.RegisterValueChangedCallback(_=>{selected=picker.index;Mount();});rootVisualElement.Add(picker);
            rootVisualElement.Add(new Label("ISOLATED · synthetic session · no network · startup unchanged"));
            target=new Label();message=new Label();step=new Label();retry=new Button(()=>{fixture.Scenario="GUEST_NOT_STARTED";_=router.RetryAsync();}){text="Retry"};
            foreach(var element in new VisualElement[]{target,message,step,retry})rootVisualElement.Add(element);
            foreach(var label in new[]{target,message,step}){label.style.whiteSpace=WhiteSpace.Normal;label.style.marginTop=12;}
            retry.style.minHeight=44;
            fixture=new Fixture{Scenario=Scenarios[selected]};router=new AuthenticatedRoutingOrchestrator(fixture);router.Changed+=Render;Render();_=Resolve();
        }
        async Task Resolve(){await router.ResolveAsync();if(fixture.Scenario=="SESSION_SWITCH"){fixture.Scenario="NO_SESSION";await router.ResolveAsync();}}
        void Render(){target.text="Target: "+router.Route;message.text=router.Message;step.text="Server step: "+(router.Onboarding?.currentStepKey??"none");retry.style.display=router.CanRetry?DisplayStyle.Flex:DisplayStyle.None;}
        [InitializeOnLoadMethod]static void Register(){EditorApplication.update+=()=>{
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists("Library/OnboardingRouting01.request"))return;
            try{File.Delete("Library/OnboardingRouting01.request");}catch(IOException){return;}
            GetWindow<AuthenticatedRoutingPreview>().ValidateMatrix();
        };}
        async void ValidateMatrix()
        {
            File.WriteAllText(Result,"START="+DateTime.UtcNow.ToString("O")+"\n");int checks=0;
            try{
                for(int i=0;i<Scenarios.Length;i++){
                    selected=i;Mount();await Task.Delay(100);
                    var s=Scenarios[i];var expected=s=="NO_SESSION"||s=="SESSION_SWITCH"?AuthenticatedRoute.Welcome:s=="EMAIL_UNVERIFIED"?AuthenticatedRoute.VerificationPending:s=="CLIENT_UPDATE_REQUIRED"?AuthenticatedRoute.UpdateRequired:s.EndsWith("ERROR")?AuthenticatedRoute.Error:s.EndsWith("COMPLETED")?AuthenticatedRoute.Home:AuthenticatedRoute.Onboarding;
                    if(router.Route!=expected||fixture.Writes!=0||target.text!="Target: "+expected)throw new Exception("ROUTE_MATRIX");checks++;
                    if(router.Route==AuthenticatedRoute.Error){if(!router.CanRetry||retry.resolvedStyle.display==DisplayStyle.None||retry.layout.height<44)throw new Exception("RETRY");checks++;}
                    File.AppendAllText(Result,s+"="+expected+" PASS\n");
                }
                selected=1;Mount();File.AppendAllText(Result,"CHECKS="+checks+"_PASS\nFAIL=0\nREAL_OPERATIONS=0\nPLAY_MODE=OFF\n");
            }catch(Exception e){File.AppendAllText(Result,"FAIL="+e.Message+"\n");}
        }
    }
}
