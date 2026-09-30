using System;
using System.IO;
using System.Reflection;
using Domino.Identity;
using Domino.UI.AppShell;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
namespace Domino.Editor
{
 // Independent runtime panel with fictional text and no authentication callbacks.
 [InitializeOnLoad]public static class PasswordRuntimeAlignmentValidation
 {
  const string Result="Library/PasswordRuntimeAlignment.result.txt";
  static readonly Vector2Int[] Sizes={new Vector2Int(375,667),new Vector2Int(393,852),new Vector2Int(412,915),new Vector2Int(430,932),new Vector2Int(480,1040),new Vector2Int(600,960),new Vector2Int(768,1024),new Vector2Int(834,1194)};
  static GameObject host;static PanelSettings panel;static ProductionEmailView view;static VisualElement root;
  static int size,consumer,phase,checks;static double due;static bool running;static float initialText,initialCaret;
  static PasswordRuntimeAlignmentValidation(){EditorApplication.update+=Poll;}
  static void Need(bool b,string message){checks++;if(!b)throw new Exception(message);}
  static void Poll(){if(!EditorApplication.isPlaying||EditorApplication.isCompiling)return;try{
   if(File.Exists("Library/PasswordRuntimeAlignment.request")){File.Delete("Library/PasswordRuntimeAlignment.request");size=consumer=phase=checks=0;File.WriteAllText(Result,"RUN="+DateTime.UtcNow.ToString("O")+"\n");
    host=new GameObject("Isolated Password Geometry");panel=ScriptableObject.CreateInstance<PanelSettings>();panel.scaleMode=PanelScaleMode.ConstantPixelSize;panel.sortingOrder=1000;
    var doc=host.AddComponent<UIDocument>();doc.panelSettings=panel;root=doc.rootVisualElement;running=true;Mount();}
   if(!running||EditorApplication.timeSinceStartup<due)return;Measure();
  }catch(Exception e){running=false;File.AppendAllText(Result,"FAIL="+e.Message+"\n");Cleanup();}}
  static ProductionPasswordField Target()=>view.Q<ProductionPasswordField>(consumer==2?"EmailConfirmationRow":"EmailPasswordRow");
  static void Mount(){root.Clear();root.style.width=Sizes[size].x;root.style.height=Sizes[size].y;
   view=new ProductionEmailView(consumer==0?ProductionAuthRoute.EmailSignIn:ProductionAuthRoute.Register,_=>{},(a,b,c)=>{},()=>{},()=>{},_=>{},(a,b)=>{},_=>{});root.Add(view);
   view.Password.SetValueWithoutNotify("Demo1234");view.Confirmation?.SetValueWithoutNotify("Demo1234");phase=0;SetPhase();}
  static void SetPhase(){var p=Target();p.Field.isPasswordField=phase<2;if(phase%2==1)p.Field.Focus();else view.Email.Focus();p.Field.SelectRange(8,8);due=EditorApplication.timeSinceStartup+.25;}
  static float Y(VisualElement e,VisualElement stop){float y=0;while(e!=stop){y+=e.layout.y;e=e.parent;}return y;}
  static void Measure(){var p=Target();var t=p.Field.Q<TextElement>(className:"unity-text-element");Need(t!=null,"TEXT_ELEMENT");
   var hp=typeof(ITextSelection).GetProperty("lineHeightAtCursorPosition",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);Need(hp!=null,"CARET_API");
   float h=(float)hp.GetValue(p.Field.textSelection);float caret=Y(t,p)+p.Field.cursorPosition.y-h/2;
   float text=WelcomeRuntimeFooterProbe.TextCenter(t,p);
   File.AppendAllText(Result,"SIZE="+Sizes[size]+" CONSUMER="+consumer+" PHASE="+phase+" FIELD="+p.layout.height+" TEXT_CENTER="+text+" CARET_CENTER="+caret+"\n");
   Need(p.layout.height==48,"HEIGHT");Need(Mathf.Abs(text-24)<=1,"TEXT_CENTER");Need(Mathf.Abs(caret-24)<=1,"CARET_CENTER");
   Need(p.Toggle.layout.height>=44&&p.Toggle.layout.width>=44,"EYE_TOUCH");
   Need(phase%2==0?!p.Contains(root.focusController.focusedElement as VisualElement):p.Contains(root.focusController.focusedElement as VisualElement),"FOCUS_STATE");
   if(phase==0){initialText=text;initialCaret=caret;}else{Need(Mathf.Abs(text-initialText)<.01f,"TEXT_SHIFT");Need(Mathf.Abs(caret-initialCaret)<.01f,"CARET_SHIFT");}
   if(++phase<4){SetPhase();return;}if(++consumer<3){Mount();return;}consumer=0;if(++size<Sizes.Length){Mount();return;}
   File.AppendAllText(Result,"CHECKS="+checks+"\nRESULT=PASS\nREAL_AUTH_CALLS=0\n");running=false;Cleanup();
  }
  static void Cleanup(){if(host)UnityEngine.Object.Destroy(host);if(panel)UnityEngine.Object.Destroy(panel);host=null;panel=null;}
 }
}
