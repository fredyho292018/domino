using System;
using System.IO;
using System.Linq;
using Domino.Identity;
using Domino.Infrastructure;
using Domino.UI.AppShell;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
namespace Domino.Editor
{
 [InitializeOnLoad]public static class ProductionEmailRuntimeValidation
 {
  const string Result="Library/Auth02BRuntime.result.txt";static int step=-1,checks;static double due;
  static readonly Vector2Int[] Sizes={new Vector2Int(393,852),new Vector2Int(480,800)};
  static ProductionEmailRuntimeValidation(){EditorApplication.update+=Poll;}
  static void Need(bool value,string key){checks++;if(!value)throw new Exception(key);}
  static void Poll(){if(!EditorApplication.isPlaying||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
   try{
    if(File.Exists("Library/Auth02BRuntime.request")){File.Delete("Library/Auth02BRuntime.request");File.WriteAllText(Result,"RUN="+DateTime.UtcNow.ToString("O")+"\n");checks=0;step=0;Need(ApplicationServices.AuthRouter.SessionKind==AuthSessionKind.NoSession,"NO_SESSION_REQUIRED");ApplicationServices.AuthRouter.NavigateEmail(ProductionAuthRoute.EmailSignIn);WelcomeRuntimeFooterProbe.Resize(Sizes[0]);due=EditorApplication.timeSinceStartup+2;}
    if(step<0||EditorApplication.timeSinceStartup<due)return;
    var host=UnityEngine.Object.FindObjectsByType<ProductionAuthHost>(FindObjectsSortMode.None).Single();var root=host.GetComponent<UIDocument>().rootVisualElement;var view=root.Q<ProductionEmailView>();
    Need(view!=null&&view.Route==ProductionAuthRoute.EmailSignIn,"REAL_SIGNIN");Need(Screen.width==Sizes[step].x&&Screen.height==Sizes[step].y,"SCREEN");
    Need(root.layout.width==Screen.width,"ROOT_WIDTH");Need(view.Password.isPasswordField,"MASKED");Need(view.Q("BottomNav")==null,"NO_BOTTOM_NAV");
    var body=view.Body;Need(Mathf.Abs(body.layout.center.x-Screen.width/2f)<1,"CENTER");Need(body.resolvedStyle.paddingLeft==24&&body.resolvedStyle.paddingRight==24,"MARGINS");
    float end=Mathf.Max(0,view.contentContainer.layout.height-view.contentViewport.layout.height);
    foreach(var e in body.Children().Where(e=>e.resolvedStyle.display!=DisplayStyle.None)){Need(e.layout.x>=0&&e.layout.xMax<=body.layout.width+1,"OVERFLOW");Need(body.layout.y+e.layout.yMax<=end+view.contentViewport.layout.height+1,"REACHABILITY");}
    foreach(var name in new[]{"EmailBack","EmailSubmit","EmailForgot","EmailCreate","ToggleEmailPassword"}){var b=view.Q<Button>(name);Need(b!=null&&b.layout.width>=44&&b.layout.height>=44,"TOUCH_"+name);}
    Need(view.Q<ProductionPasswordField>().layout.height==48,"PASSWORD_HEIGHT");Need(view.Q<AuthStatusMessage>().resolvedStyle.display==DisplayStyle.None,"INITIAL_FEEDBACK_HIDDEN");
    File.AppendAllText(Result,"SCREEN="+Screen.width+"x"+Screen.height+" SIGNIN_LAYOUT=PASS OVERFLOW=0 ACTIONS_REACHABLE=YES\n");
    if(++step<Sizes.Length){WelcomeRuntimeFooterProbe.Resize(Sizes[step]);due=EditorApplication.timeSinceStartup+2;}else{step=-1;File.AppendAllText(Result,"CHECKS_RUN="+checks+"\nCHECKS_PASS="+checks+"\nCHECKS_FAIL=0\nCHECKS_SKIPPED=0\nREAL_SIGNINS=0\nREAL_RESETS=0\n");}
   }catch(Exception ex){step=-1;File.AppendAllText(Result,"FAIL="+ex.Message+"\nCHECKS_FAIL=1\n");}
  }
 }
}
