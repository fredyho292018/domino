using System;
using System.IO;
using System.Linq;
using Domino.Identity;
using Domino.UI.AppShell;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
namespace Domino.Editor {
 public sealed partial class ProductionEmailPreview:EditorWindow {
  static readonly Vector2Int[] Sizes={new Vector2Int(375,667),new Vector2Int(393,852),new Vector2Int(412,915),new Vector2Int(430,932),new Vector2Int(480,1040),new Vector2Int(600,960),new Vector2Int(768,1024),new Vector2Int(834,1194)};
  static readonly string[] States={"EMAIL_ENTRY","REGISTER_EMPTY","REGISTER_VALID","REGISTER_VALIDATION_ERROR","REGISTER_SUBMITTING","REGISTER_FIREBASE_ERROR","VERIFICATION_PENDING","VERIFICATION_CHECKING","VERIFICATION_NOT_YET_VERIFIED","VERIFICATION_RESENDING","VERIFICATION_ERROR","VERIFICATION_INFO","VERIFICATION_SUCCESS","SIGNIN_EMPTY","SIGNIN_VALID","SIGNIN_SUBMITTING","SIGNIN_INVALID_CREDENTIALS","SIGNIN_NETWORK_ERROR","SIGNIN_UNVERIFIED","RESET_EMPTY","RESET_VALID","RESET_SUBMITTING","RESET_SUCCESS","RESET_INVALID_EMAIL","RESET_NETWORK_ERROR"};
  const string Result="Library/Auth02A.result.txt";int size=1,state=1,checks,callbacks;bool testing;VisualElement frame;ProductionEmailView view;
  [InitializeOnLoadMethod]static void Register(){EditorApplication.update+=()=>{
   if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
   if(File.Exists("Library/Auth02A.request")){File.Delete("Library/Auth02A.request");var w=GetWindow<ProductionEmailPreview>();w.size=w.state=w.checks=0;w.testing=true;File.WriteAllText(Result,"RUNNING "+DateTime.UtcNow.ToString("O")+"\n");w.Mount();}
  };}
  [MenuItem("Domino/Production Auth/Email register preview (isolated)")]
  public static void Open(){var w=GetWindow<ProductionEmailPreview>();w.testing=false;w.size=w.state=1;w.Mount();w.Focus();}
  void Mount(){titleContent=new GUIContent("PRODUCTION EMAIL · ISOLATED");minSize=new Vector2(440,700);Show();rootVisualElement.Clear();
   var selector=new PopupField<string>(States.ToList(),state);selector.RegisterValueChangedCallback(_=>{state=selector.index;testing=false;Mount();});rootVisualElement.Add(selector);
   var sizes=new PopupField<string>(Sizes.Select(s=>s.x+"x"+s.y).ToList(),size);sizes.RegisterValueChangedCallback(_=>{size=sizes.index;testing=false;Mount();});rootVisualElement.Add(sizes);
   frame=new VisualElement();frame.style.position=Position.Absolute;frame.style.top=52;frame.style.width=Sizes[size].x;frame.style.height=Sizes[size].y;frame.style.paddingTop=frame.style.paddingBottom=24;
   float scale=Mathf.Min(1,Mathf.Min(position.width/Sizes[size].x,(position.height-52)/Sizes[size].y));frame.style.transformOrigin=new TransformOrigin(0,0,0);frame.style.scale=new Scale(new Vector3(scale,scale,1));rootVisualElement.Add(frame);
   callbacks=0;if(state>=13){MountAccess();return;}var route=state==0?ProductionAuthRoute.EmailEntry:state<6?ProductionAuthRoute.Register:ProductionAuthRoute.VerificationPending;
   view=new ProductionEmailView(route,_=>callbacks++,(e,p,c)=>callbacks++,()=>callbacks++,()=>callbacks++,_=>callbacks++);frame.Add(view);
   if(state==2){view.Email.value="demo@example.invalid";view.Password.value=view.Confirmation.value=new string('x',256);}
   bool busy=state==4||state==7||state==9;var operation=state==9?EmailOperationState.Resending:state==7?EmailOperationState.Checking:EmailOperationState.Submitting;
   string message=state==3?EmailAuthRules.Message(EmailAuthError.PasswordMismatch):state==5?EmailAuthRules.Message(EmailAuthError.EmailAlreadyInUse):state==8?"Your email is not verified yet. Check your inbox and try again.":state==10?EmailAuthRules.Message(EmailAuthError.NetworkError):state==11?"Verify your email to continue.":state==12?"Verification email sent.":"";
   view.SetState(busy,operation,message,route==ProductionAuthRoute.VerificationPending?"demo@example.invalid":"",true,state==5?EmailAuthError.EmailAlreadyInUse:EmailAuthError.None);
   if(testing)rootVisualElement.schedule.Execute(CheckGeometry).ExecuteLater(350);
  }
  void Need(bool value,string key){checks++;if(!value)throw new Exception(States[state]+" "+Sizes[size]+" "+key);}
  static float X(VisualElement e,VisualElement root){float x=0;while(e!=root){x+=e.layout.x;e=e.parent;}return x;}
  static void Click(VisualElement e){using(var ev=NavigationSubmitEvent.GetPooled()){ev.target=e;e.SendEvent(ev);}}
  void CheckGeometry(){try{
   var body=view.Body;Need(body.layout.width>0&&body.layout.width<=620.1f,"MAX_WIDTH");Need(Mathf.Abs(X(body,frame)+body.layout.width/2-Sizes[size].x/2f)<1,"CENTERING");
   Need(view.contentViewport.layout.width==view.layout.width,"NO_GUTTER");Need(view.Q("BottomNav")==null,"NO_BOTTOM_NAV");
   float end=Mathf.Max(0,view.contentContainer.layout.height-view.contentViewport.layout.height);
   foreach(var child in body.Children().Where(e=>e.resolvedStyle.display!=DisplayStyle.None)){Need(child.layout.x>=0&&child.layout.xMax<=body.layout.width+.1f,"OVERFLOW");Need(body.layout.y+child.layout.yMax<=end+view.contentViewport.layout.height+1,"REACHABILITY");}
   foreach(var button in body.Query<Button>().ToList().Where(e=>e.resolvedStyle.display!=DisplayStyle.None&&e.layout.width>0)){Need(button.layout.height>=44&&button.layout.width>=44,"TOUCH_TARGET");}
   bool busy=state==4||state==7||state==9;
   if(view.Password!=null){Need(view.Password.isPasswordField&&view.Confirmation.isPasswordField,"MASKED");Need(view.Email.keyboardType==TouchScreenKeyboardType.EmailAddress,"EMAIL_KEYBOARD");Need(view.Email.enabledSelf==!busy,"FIELD_BUSY");}
   if(view.Password!=null&&!busy){
    foreach(var password in body.Query<ProductionPasswordField>().ToList()){
     var field=password.Field;var input=field.Q(className:"unity-text-field__input");var toggle=password.Toggle;
     Need(Mathf.Abs(password.layout.height-view.Email.layout.height)<.1f,"CONSISTENT_FIELD_HEIGHT");
     Need(input.resolvedStyle.backgroundColor.a==0&&input.resolvedStyle.borderLeftWidth==0&&input.resolvedStyle.borderRightWidth==0,"TRANSPARENT_INPUT");
     Need(toggle.resolvedStyle.backgroundColor.a==0&&toggle.resolvedStyle.borderLeftWidth==0&&toggle.resolvedStyle.borderRightWidth==0,"TRANSPARENT_EYE");
     Need(field.layout.xMax<=toggle.layout.x+.1f&&toggle.layout.xMax<=password.layout.width,"NO_TEXT_ICON_COLLISION");
     Need(toggle.layout.width>=44&&toggle.layout.height>=44,"EYE_HIT_TARGET");
     Need(password.Icon.vectorImage==Resources.Load<VectorImage>("AuthIcons/icon_eye_off"),"HIDDEN_ICON");
     var value=field.value;Click(toggle);Need(field.value==value&&password.Icon.vectorImage==Resources.Load<VectorImage>("AuthIcons/icon_eye"),"VISIBLE_ICON_VALUE_PRESERVED");Click(toggle);
     field.Focus();Need(password.style.borderLeftColor.value==Domino.UI.Theming.ThemeProvider.Current.Colors.Primary,"WHOLE_FIELD_FOCUS");
     password.SetError(true);Need(password.style.borderLeftColor.value==Domino.UI.Theming.ThemeProvider.Current.Colors.Error&&toggle.enabledSelf,"WHOLE_FIELD_ERROR");password.SetError(false);
    }
    var eye=view.Q<Button>("ToggleEmailPassword");var confirmEye=view.Q<Button>("ToggleEmailConfirmation");
    Need(eye.Q<Image>().vectorImage!=null&&confirmEye.Q<Image>().vectorImage!=null,"EYE_ASSETS");
    Click(eye);Need(!view.Password.isPasswordField&&view.Confirmation.isPasswordField,"PASSWORD_ONLY_VISIBLE");
    Click(eye);Need(view.Password.isPasswordField&&view.Confirmation.isPasswordField,"PASSWORD_MASK_RESTORED");
    Click(confirmEye);Need(view.Password.isPasswordField&&!view.Confirmation.isPasswordField,"CONFIRM_ONLY_VISIBLE");
    Click(confirmEye);Need(view.Password.isPasswordField&&view.Confirmation.isPasswordField,"CONFIRM_MASK_RESTORED");
   }
   Need(view.Q<Label>("EmailPageTitle").resolvedStyle.unityTextAlign==TextAnchor.MiddleCenter,"AUTH_TITLE_CENTERED");
   var subtitle=view.Q<Label>("EmailSubtitle");if(subtitle!=null)Need(subtitle.resolvedStyle.unityTextAlign==TextAnchor.MiddleCenter,"AUTH_SUBTITLE_CENTERED");
   foreach(var key in new[]{"EmailCreate","EmailSignIn","EmailSubmit","EmailVerified","EmailResend"}){var action=view.Q<Button>(key);if(action!=null)Need(action.resolvedStyle.unityTextAlign==TextAnchor.MiddleCenter,"AUTH_CTA_CENTERED");}
   if(view.Email!=null){
    Need(Mathf.Abs(view.Email.layout.width-view.Q("EmailPasswordRow").layout.width)<1,"EQUAL_FIELD_WIDTH");
    Need(view.Email.layout.height>=48,"EMAIL_HEIGHT");
    foreach(var key in new[]{"EmailAddressLabel","EmailPasswordLabel","EmailConfirmationLabel"})Need(view.Q<Label>(key).resolvedStyle.unityTextAlign==TextAnchor.MiddleLeft,"FORM_LABEL_LEFT");
    var link=view.Q<Button>("EmailSignIn");var row=view.Q("RegisterSignInRow");
    Need(link.resolvedStyle.backgroundColor.a==0&&link.resolvedStyle.borderTopWidth==0&&link.layout.height>=44,"INLINE_SIGNIN_TRANSPARENT_TOUCH");
    Need(row.resolvedStyle.justifyContent==Justify.Center&&link.resolvedStyle.color==Domino.UI.Theming.ThemeProvider.Current.Colors.Primary,"INLINE_SIGNIN_CENTERED_GREEN");
   }
   foreach(var label in body.Query<Label>().ToList())Need(label.text.IndexOfAny(new[]{'\u00c2','\u00c3','\u00e2','\ufffd'})<0,"UTF8");
   var feedback=view.Q<AuthStatusMessage>();bool hasFeedback=state==3||state==4||state==5||state>=7;
   Need((feedback.resolvedStyle.display!=DisplayStyle.None)==hasFeedback,"STATUS_VISIBILITY");
   if(hasFeedback){
    var expected=busy?AuthStatusVariant.Loading:state==8?AuthStatusVariant.Warning:state==11?AuthStatusVariant.Info:state==12?AuthStatusVariant.Success:AuthStatusVariant.Error;
    Need(feedback.Variant==expected,"STATUS_SEMANTICS");Need(feedback.Icon.vectorImage!=null,"STATUS_VECTOR_ICON");Need(feedback.tooltip.Length>0,"STATUS_MEANINGFUL_TEXT");
    foreach(var label in feedback.Query<Label>().ToList().Where(l=>l.resolvedStyle.display!=DisplayStyle.None)){Need(X(label,body)>=0&&X(label,body)+label.layout.width<=body.layout.width+.1f,"STATUS_TEXT_OVERFLOW");}
   }
   if(busy){Need(!view.Q<Button>("EmailBack").enabledSelf,"BUSY_BACK");Click(view.Q<Button>(state==4?"EmailSubmit":"EmailVerified"));Need(callbacks==0,"BUSY_NO_CALLBACK");}
   if(state==0){Click(view.Q<Button>("EmailCreate"));Need(callbacks==1,"ENTRY_ROUTE");}
   if(state==1){Click(view.Q<Button>("EmailSubmit"));Need(callbacks==0&&view.Q<Label>("EmailStatus").text.Length>0,"LOCAL_VALIDATION");}
   if(state==2){Click(view.Q<Button>("EmailSubmit"));Need(callbacks==1&&view.Password.value==""&&view.Confirmation.value=="","SUBMIT_CLEARS_SECRETS");}
   if(state==6){Click(view.Q<Button>("EmailAnother"));Need(callbacks==0&&view.Q("EmailCancelConfirmation").style.display.value==DisplayStyle.Flex,"CANCEL_REQUIRES_CONFIRMATION");Click(view.Q<Button>("EmailKeepVerifying"));Need(callbacks==0,"KEEP_SESSION");}
   view.scrollOffset=new Vector2(0,end);Need(Mathf.Abs(view.scrollOffset.y-end)<1,"SCROLL");
   File.AppendAllText(Result,States[state]+" "+Sizes[size]+" CENTERING=PASS OVERFLOW=PASS REACHABILITY=PASS\n");
   if(++state==13){state=0;size++;}if(size<Sizes.Length){Mount();return;}
   File.AppendAllText(Result,"CHECKS="+checks+"_PASS\nFAIL=0\nSKIPPED=0\nREAL_FIREBASE_CALLS=0\n");testing=false;state=3;size=1;Mount();Focus();
  }catch(Exception e){testing=false;File.AppendAllText(Result,"FAIL="+e.Message+"\n");Debug.LogException(e);}}
 }
}
