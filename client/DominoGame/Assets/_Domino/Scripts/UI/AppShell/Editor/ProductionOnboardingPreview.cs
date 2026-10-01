using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Domino.Infrastructure.Api;
using Domino.UI.AppShell;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
namespace Domino.Editor
{
    public sealed partial class ProductionOnboardingPreview:EditorWindow
    {
        static readonly string[] States={"NOT_STARTED","BASIC_PROFILE_STEP","EXPERIENCE_STEP","COACH_STEP","CONTACTS_STEP","MEMBERSHIP_STEP","COMPLETED","LOADING","ERROR","UPDATE_REQUIRED","BASIC_PROFILE_EMPTY","BASIC_PROFILE_PARTIAL","BASIC_PROFILE_VALID","BASIC_PROFILE_VALIDATION_ERROR","BASIC_PROFILE_LOADING","BASIC_PROFILE_NETWORK_ERROR","BASIC_PROFILE_REVISION_CONFLICT","EXPERIENCE_EMPTY","EXPERIENCE_SELECTED_BEGINNER","EXPERIENCE_SELECTED_RULES_KNOWN","EXPERIENCE_SELECTED_STRATEGY","EXPERIENCE_SELECTED_COMPETITIVE","EXPERIENCE_LOADING","EXPERIENCE_NETWORK_ERROR","EXPERIENCE_REVISION_CONFLICT","EXPERIENCE_VALIDATION_ERROR","COACH_EMPTY","COACH_SELECTED_LUCIA","COACH_SELECTED_AMARA","COACH_SELECTED_MATEO","COACH_SELECTED_OMAR","COACH_LOADING","COACH_NETWORK_ERROR","COACH_REVISION_CONFLICT","COACH_UNKNOWN_AVATAR","COACH_DYNAMIC","COACH_VALIDATION_ERROR","COACH_MISSING_SAVED","CONTACTS_READY","CONTACTS_LOADING","CONTACTS_NETWORK_ERROR","CONTACTS_REVISION_CONFLICT","MEMBERSHIP_FREE","MEMBERSHIP_GOLD","MEMBERSHIP_PLATINUM","MEMBERSHIP_DIAMOND","MEMBERSHIP_FAMILY","MEMBERSHIP_TRIAL_ELIGIBLE","MEMBERSHIP_TRIAL_LOADING","MEMBERSHIP_TRIAL_SUCCESS","MEMBERSHIP_TRIAL_ALREADY_ACTIVE","MEMBERSHIP_TRIAL_NOT_ELIGIBLE","MEMBERSHIP_TRIAL_CONSUMED","MEMBERSHIP_TRIAL_POLICY_CHANGED","MEMBERSHIP_TRIAL_NETWORK_ERROR","MEMBERSHIP_CLIENT_UPDATE_REQUIRED","MEMBERSHIP_TRIAL_DISABLED","MEMBERSHIP_TRIAL_IDEMPOTENCY_CONFLICT","MEMBERSHIP_TRIAL_DEPENDENCY_UNAVAILABLE"};
        static readonly Vector2Int[] Sizes={new Vector2Int(375,667),new Vector2Int(393,852),new Vector2Int(412,915),new Vector2Int(430,932),new Vector2Int(480,1040),new Vector2Int(600,960),new Vector2Int(768,1024),new Vector2Int(834,1194)};
        bool validating;int checks;int selected, size=1,version=2;string locale="en";OnboardingShellController controller;ProductionOnboardingRoot view;
        [MenuItem("Domino/Production Onboarding/Shell preview (isolated)")]
        public static void Open(){GetWindow<ProductionOnboardingPreview>().Mount();}
        [InitializeOnLoadMethod]static void Register(){EditorApplication.update+=()=>{
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(File.Exists("Library/Onboarding02A.request")){File.Delete("Library/Onboarding02A.request");var w=GetWindow<ProductionOnboardingPreview>();w.validating=true;w.checks=0;w.selected=0;w.size=0;w.version=1;w.locale="en";File.WriteAllText("Library/Onboarding02A.validation.txt","START="+DateTime.UtcNow.ToString("O")+"\n");w.Mount();}
        };}
        void OnDisable(){view?.Dispose();controller?.Dispose();flowSession?.Dispose();}
        void CreateGUI(){Mount();}
        void Mount()
        {
            view?.Dispose();controller?.Dispose();flowSession?.Dispose();rootVisualElement.Clear();titleContent=new GUIContent("ONBOARDING · ISOLATED");minSize=new Vector2(440,700);
            var state=new PopupField<string>(States.ToList(),selected);state.RegisterValueChangedCallback(_=>{selected=state.index;Mount();});rootVisualElement.Add(state);
            var sizes=new PopupField<string>(Sizes.Select(x=>x.x+"x"+x.y).ToList(),size);sizes.RegisterValueChangedCallback(_=>{size=sizes.index;Mount();});rootVisualElement.Add(sizes);
            var versions=new PopupField<string>(new System.Collections.Generic.List<string>{"v1","v2"},version-1);versions.RegisterValueChangedCallback(_=>{version=versions.index+1;if(version==1&&selected==1)selected=2;Mount();});rootVisualElement.Add(versions);
            var languages=new PopupField<string>(new System.Collections.Generic.List<string>{"en","es"},locale=="es"?1:0);languages.RegisterValueChangedCallback(evt=>{locale=languages.value;if(controller?.State?.currentStepKey=="EXPERIENCE_STEP")_ =controller.ChangeExperienceLocaleAsync(locale);else if(controller?.State?.currentStepKey=="COACH_STEP")_ =controller.ChangeCoachLocaleAsync(locale);else if(controller?.State?.currentStepKey=="MEMBERSHIP_STEP")_ =controller.ChangeMembershipLocaleAsync(locale);else if(controller?.State?.currentStepKey=="CONTACTS_STEP")_ =controller.ChangeContactsLocaleAsync(locale);else Mount();});rootVisualElement.Add(languages);
            rootVisualElement.Add(new Label("ISOLATED · fixture data · no network · read-only shell"));
            var frame=new VisualElement{name="OnboardingViewport"};frame.style.width=Sizes[size].x;frame.style.height=Sizes[size].y;frame.style.flexShrink=0;
            float scale=Mathf.Min(1,Mathf.Min(position.width/Sizes[size].x,(position.height-110)/Sizes[size].y));frame.style.transformOrigin=new TransformOrigin(0,0,0);frame.style.scale=new Scale(new Vector3(Mathf.Max(.1f,scale),Mathf.Max(.1f,scale),1));rootVisualElement.Add(frame);
            controller=new OnboardingShellController(new Fixture(States[selected],version));view=new ProductionOnboardingRoot(controller);frame.Add(view);if(selected>=10)view.SetSafeArea(24,24);_ =LoadFixture();
            view.schedule.Execute(()=>{
                if(view.panel==null)return;
                File.WriteAllText("Library/Onboarding02A.preview.txt","UTC="+DateTime.UtcNow.ToString("O")+"\nPHASE="+controller.Phase+"\nVIEWPORT="+Sizes[size]+"\nROOT_WIDTH="+view.layout.width+"\nROOT_HEIGHT="+view.layout.height+"\nBODY_WIDTH="+view.Body.layout.width+"\nREAL_NETWORK=NO\n");if(membershipTesting)ValidateMembershipVisual();else if(contactsTesting)ValidateContacts();else if(coachTesting)ValidateCoach();else if(experienceTesting)ValidateExperience();else if(copyTesting)ValidateCopy();else if(keyboardTesting)ValidateKeyboard();else if(basicTesting)ValidateBasic();else if(validating)ValidateCurrent();
            }).StartingIn(500);
        }
        void Need(bool condition,string code){if(!condition)throw new InvalidOperationException(code);checks++;}
        void ValidateCurrent()
        {
            try {
                var phase=controller.Phase;
                Need(view.panel!=null,"MOUNTED");Need(Mathf.Abs(view.layout.width-Sizes[size].x)<1,"ROOT_WIDTH");
                Need(view.Body.layout.width<=620.1f,"MAX_WIDTH");Need(view.Body.layout.width<=view.layout.width+.1f,"NO_OVERFLOW");
                Need(view.Q<Label>("OnboardingTitle")!=null,"TITLE");
                if(selected==0)Need(phase==OnboardingShellPhase.NotStarted,"NOT_STARTED");
                else if(selected<=5){Need(phase==OnboardingShellPhase.InProgress,"IN_PROGRESS");Need(view.Q(States[selected])!=null,"DISPATCH");Need(controller.State.revision==7,"REVISION");Need(controller.Catalog.catalogVersion==version,"VERSION");}
                else Need(phase==(selected==6?OnboardingShellPhase.Completed:selected==7?OnboardingShellPhase.Loading:selected==8?OnboardingShellPhase.Error:OnboardingShellPhase.UpdateRequired),"PHASE");
                if(selected==8){var retry=view.Q<Button>("OnboardingRetry");Need(retry!=null&&retry.enabledSelf,"RETRY");Need(retry.layout.height>=44,"RETRY_TOUCH");}
                if(selected>=6){var feedback=view.Q<AuthStatusMessage>();Need(feedback.resolvedStyle.display!=DisplayStyle.None,"FEEDBACK");}
                File.AppendAllText("Library/Onboarding02A.validation.txt",$"v{version} {locale} {States[selected]} {Sizes[size]} PASS\n");
                selected++;if(version==1&&selected==1)selected=2;
                if(selected>=10){selected=0;size++;}
                if(size>=Sizes.Length){size=0;if(locale=="en")locale="es";else{locale="en";version++;}}
                if(version>2){validating=false;File.AppendAllText("Library/Onboarding02A.validation.txt","CHECKS="+checks+"_PASS\nFAIL=0\n");version=2;size=1;selected=0;locale="en";}
                Mount();
            }catch(Exception e){validating=false;File.AppendAllText("Library/Onboarding02A.validation.txt","FAIL="+e.Message+"\n");}
        }
        sealed partial class Fixture:IOnboardingShellSource, IBasicProfileSource, IExperienceSource, ICoachSource, IContactsSource, IMembershipSource
        {
            readonly string state;readonly int version;bool failed;OnboardingStateDto current;
            public Fixture(string state,int version){this.state=state;this.version=version;}
            public async Task<OnboardingStateDto> LoadAsync(CancellationToken token){
                if(state=="LOADING")await Task.Delay(Timeout.Infinite,token);
                if(state=="UPDATE_REQUIRED")throw new DominoApiException(ApiFailure.Server,409,"CLIENT_UPDATE_REQUIRED");
                if(state=="ERROR"&&!failed){failed=true;throw new DominoApiException(ApiFailure.Transport);}
                if(current!=null)return current;return current=new OnboardingStateDto{basicProfile=ProfileFixture(state),status=state=="NOT_STARTED"?state:state=="COMPLETED"?state:"IN_PROGRESS",currentStepKey=state.StartsWith("MEMBERSHIP_")?"MEMBERSHIP_STEP":state.StartsWith("CONTACTS_")?"CONTACTS_STEP":state.StartsWith("COACH_")?"COACH_STEP":state.StartsWith("EXPERIENCE_")?"EXPERIENCE_STEP":States.Skip(1).Take(5).Contains(state)?state:version==2?"BASIC_PROFILE_STEP":"EXPERIENCE_STEP",catalogVersion=state=="NOT_STARTED"?(int?)null:version,revision=7,domainRevisions=new OnboardingDomainRevisionsDto(),answers=state.StartsWith("CONTACTS_")?CoachAnswers("COACH_SELECTED_MATEO"):state.StartsWith("COACH_")?CoachAnswers(state):ExperienceAnswers(state)};
            }
            sealed class Save {public OnboardingAnswerDto[] answers;public string id=Guid.NewGuid().ToString("D");}
            public object PrepareProfile(OnboardingAnswerDto[] answers,string zone)=>new Save{answers=answers};
            public async Task<OnboardingStateDto> SaveProfileAsync(object operation,CancellationToken token){
                if(state=="BASIC_PROFILE_LOADING")await Task.Delay(Timeout.Infinite,token);
                if(state=="BASIC_PROFILE_VALIDATION_ERROR")throw new DominoApiException(ApiFailure.Server,400,"DISPLAY_NAME_RESERVED");
                if(state=="BASIC_PROFILE_NETWORK_ERROR"&&!failed){failed=true;throw new DominoApiException(ApiFailure.Transport);}
                if(state=="BASIC_PROFILE_REVISION_CONFLICT"&&!failed){failed=true;current.revision=8;throw new DominoApiException(ApiFailure.Server,409,"REVISION_MISMATCH");}
                current.revision++;current.currentStepKey="EXPERIENCE_STEP";return current;
            }
            sealed class ExperienceSave {public string answer;public bool back;}
            public object PrepareExperience(string questionKey,string optionKey)=>new ExperienceSave{answer=optionKey};
            public object PrepareExperienceBack()=>new ExperienceSave{back=true};
            public async Task<OnboardingStateDto> ExecuteExperienceAsync(object operation,CancellationToken token){
                var save=(ExperienceSave)operation;
                if(state=="EXPERIENCE_LOADING")await Task.Delay(Timeout.Infinite,token);
                if(state=="EXPERIENCE_VALIDATION_ERROR")throw new DominoApiException(ApiFailure.Server,400,"ONBOARDING_INVALID_ANSWER");
                if(state=="EXPERIENCE_NETWORK_ERROR"&&!failed){failed=true;throw new DominoApiException(ApiFailure.Transport);}
                if(state=="EXPERIENCE_REVISION_CONFLICT"&&!failed){failed=true;current.revision=8;current.answers=ExperienceAnswers("EXPERIENCE_SELECTED_RULES_KNOWN");throw new DominoApiException(ApiFailure.Server,409,"REVISION_MISMATCH");}
                current.revision++;current.currentStepKey=save.back?"BASIC_PROFILE_STEP":"COACH_STEP";if(!save.back)current.answers=new[]{new OnboardingAnswerDto{questionKey="DOMINO_EXPERIENCE",type="SINGLE_SELECT",optionKey=save.answer}};return current;
            }
            public Task<OnboardingCatalogDto> CatalogAsync(string locale,CancellationToken token)=>Task.FromResult(CatalogFixture(version,locale));
        }
    }
}
