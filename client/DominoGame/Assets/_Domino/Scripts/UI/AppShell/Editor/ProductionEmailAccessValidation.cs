using System;
using System.IO;
using System.Linq;
using Domino.Identity;
using Domino.UI.AppShell;
using Domino.UI.Theming;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
namespace Domino.Editor
{
 public sealed partial class ProductionEmailPreview
 {
  const string AccessResult="Library/Auth02B.result.txt";
  ProductionAuthRoute lastRoute;
  [InitializeOnLoadMethod]static void RegisterAccess(){EditorApplication.update+=()=>{
   if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
   if(File.Exists("Library/Auth02B.request")){File.Delete("Library/Auth02B.request");var w=GetWindow<ProductionEmailPreview>();w.size=w.checks=0;w.state=13;w.testing=true;File.WriteAllText(AccessResult,"RUN="+DateTime.UtcNow.ToString("O")+"\n");w.Mount();}
   if(File.Exists("Library/Auth02BPreview.request")){File.Delete("Library/Auth02BPreview.request");OpenAccess();}
  };}
  [MenuItem("Domino/Production Auth/Email sign in and reset preview (isolated)")]
  public static void OpenAccess(){var w=GetWindow<ProductionEmailPreview>();w.testing=false;w.size=1;w.state=13;w.Mount();w.Focus();}
  void MountAccess()
  {
   var route=state==18?ProductionAuthRoute.VerificationPending:state<19?ProductionAuthRoute.EmailSignIn:ProductionAuthRoute.ForgotPassword;
   lastRoute=route;
   view=new ProductionEmailView(route,r=>{lastRoute=r;callbacks++;},(e,p,c)=>callbacks++,()=>callbacks++,()=>callbacks++,_=>callbacks++,(e,p)=>callbacks++,e=>callbacks++);frame.Add(view);
   if(state==14||state==20){view.Email.value="demo@example.invalid";if(view.Password!=null)view.Password.value="x";}
   bool busy=state==15||state==21;
   var error=state==16?EmailAuthError.InvalidCredential:state==17||state==24?EmailAuthError.NetworkError:state==23?EmailAuthError.InvalidEmail:EmailAuthError.None;
   view.SetState(busy,state==21?EmailOperationState.Resetting:EmailOperationState.SigningIn,state==22?EmailAuthRules.ResetSuccess:state==18?"Verify your email to continue.":"",state==18?"demo@example.invalid":"",true,error);
   if(testing)rootVisualElement.schedule.Execute(CheckAccess).ExecuteLater(350);
  }
  void CheckAccess(){try{
   var body=view.Body;var width=Sizes[size].x;
   Need(body.layout.width<=620.1f&&Mathf.Abs(X(body,frame)+body.layout.width/2-width/2f)<1,"CENTERING");
   Need(body.resolvedStyle.paddingLeft==24&&body.resolvedStyle.paddingRight==24,"MARGINS");
   Need(view.contentViewport.layout.width==view.layout.width&&view.Q("BottomNav")==null,"NO_GUTTER_NAV");
   float end=Mathf.Max(0,view.contentContainer.layout.height-view.contentViewport.layout.height);
   foreach(var e in body.Children().Where(e=>e.resolvedStyle.display!=DisplayStyle.None)){
    Need(e.layout.x>=0&&e.layout.xMax<=body.layout.width+1,"OVERFLOW");Need(body.layout.y+e.layout.yMax<=end+view.contentViewport.layout.height+1,"REACHABILITY");
   }
   foreach(var b in body.Query<Button>().ToList().Where(b=>b.layout.width>0&&b.layout.height>0)){Need(b.layout.width>=44&&b.layout.height>=44,"TOUCH");Need(b.resolvedStyle.unityTextAlign==TextAnchor.MiddleCenter||b.name=="EmailBack","CTA_TEXT_CENTER");}
   Need(view.Q<Label>("EmailPageTitle").resolvedStyle.unityTextAlign==TextAnchor.MiddleCenter,"TITLE_CENTER");
   foreach(var label in body.Query<Label>().ToList())Need(label.text.IndexOfAny(new[]{'\u00c2','\u00c3','\u00e2','\ufffd'})<0,"UTF8");
   if(view.Email!=null){Need(view.Email.layout.height==48,"EMAIL_HEIGHT");Need(view.Q<Label>("EmailAddressLabel").resolvedStyle.unityTextAlign==TextAnchor.MiddleLeft,"LABEL_LEFT");}
   bool busy=state==15||state==21;
   if(view.Password!=null){
    var p=view.Q<ProductionPasswordField>();Need(p!=null&&p.layout.height==48&&p.Toggle.layout.width>=44&&p.Toggle.layout.height>=44,"SHARED_PASSWORD");
    Need(p.Toggle.resolvedStyle.backgroundColor.a==0&&p.Toggle.resolvedStyle.borderLeftWidth==0,"EYE_TRANSPARENT");
    Need(view.Password.isPasswordField,"PASSWORD_MASKED");
    if(!busy){var old=view.Password.value;Click(p.Toggle);Need(!view.Password.isPasswordField&&view.Password.value==old,"EYE_TOGGLE");Click(p.Toggle);}
    var create=view.Q<Button>("EmailCreate");var forgot=view.Q<Button>("EmailForgot");
    Need(create.resolvedStyle.backgroundColor.a==0&&forgot.resolvedStyle.backgroundColor.a==0,"INLINE_LINKS");
    Need(create.resolvedStyle.borderLeftWidth==0&&forgot.resolvedStyle.borderLeftWidth==0,"INLINE_NO_BORDER");
   }
   var feedback=view.Q<AuthStatusMessage>();bool visible=state==15||state==16||state==17||state==18||state>=21;
   Need((feedback.resolvedStyle.display!=DisplayStyle.None)==visible,"STATUS_VISIBLE");
   if(visible){var expected=busy?AuthStatusVariant.Loading:state==18?AuthStatusVariant.Info:state==22?AuthStatusVariant.Success:AuthStatusVariant.Error;Need(feedback.Variant==expected,"STATUS_VARIANT");Need(feedback.Icon.vectorImage!=null,"STATUS_ICON");}
   if(state==16)Need(feedback.Title.text=="Unable to sign in"&&feedback.Message.text==EmailAuthRules.Message(EmailAuthError.InvalidCredential),"PRIVACY_CREDENTIALS");
   if(state==22)Need(feedback.Message.text==EmailAuthRules.ResetSuccess&&view.Q<Button>("EmailSubmit").resolvedStyle.display==DisplayStyle.None,"RESET_PRIVACY_SUCCESS");
   if(busy){Need(!view.Email.enabledSelf&&!view.Q<Button>("EmailSubmit").enabledSelf&&!view.Q<Button>("EmailBack").enabledSelf,"BUSY_CONTROLS");Click(view.Q("EmailSubmit"));Need(callbacks==0,"DOUBLE_SUBMIT_UI");}
   if(state==13||state==19){Click(view.Q("EmailSubmit"));Need(callbacks==0&&feedback.Variant==AuthStatusVariant.Error,"LOCAL_VALIDATION");}
   if(state==14||state==20){Click(view.Q("EmailSubmit"));Need(callbacks==1,"SUBMIT");if(view.Password!=null)Need(view.Password.value=="","CLEAR_SECRET");}
   if(state==13){Click(view.Q("EmailForgot"));Need(lastRoute==ProductionAuthRoute.ForgotPassword,"FORGOT_ROUTE");Click(view.Q("EmailCreate"));Need(lastRoute==ProductionAuthRoute.Register,"REGISTER_ROUTE");}
   if(state==19){Click(view.Q("EmailSignIn"));Need(lastRoute==ProductionAuthRoute.EmailSignIn,"RETURN_ROUTE");}
   view.scrollOffset=new Vector2(0,end);Need(Mathf.Abs(view.scrollOffset.y-end)<1,"SCROLL");
   File.AppendAllText(AccessResult,States[state]+" "+Sizes[size]+" CENTERING=PASS OVERFLOW=PASS REACHABILITY=PASS\n");
   if(++state==States.Length){state=13;size++;}if(size<Sizes.Length){Mount();return;}
   File.AppendAllText(AccessResult,"CHECKS_RUN="+checks+"\nCHECKS_PASS="+checks+"\nCHECKS_FAIL=0\nCHECKS_SKIPPED=0\nREAL_FIREBASE_CALLS=0\n");testing=false;size=1;state=13;Mount();Focus();
  }catch(Exception ex){testing=false;File.AppendAllText(AccessResult,"CHECKS_RUN="+checks+"\nCHECKS_PASS="+(checks-1)+"\nCHECKS_FAIL=1\nFAIL="+ex.Message+"\n");Debug.LogException(ex);}}
 }
}
