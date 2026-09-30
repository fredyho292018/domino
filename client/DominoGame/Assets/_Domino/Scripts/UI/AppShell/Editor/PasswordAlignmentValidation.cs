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
 public sealed partial class ProductionEmailPreview
 {
  const string PasswordResult="Library/PasswordAlignment.result.txt";
  int passwordConsumer,passwordPhase,passwordChecks;bool passwordBaseline;
  float passwordInitialText,passwordInitialCaret;
  [InitializeOnLoadMethod]static void RegisterPasswordAlignment(){EditorApplication.update+=()=>{
   if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists("Library/PasswordAlignment.request"))return;
   var mode=File.ReadAllText("Library/PasswordAlignment.request").Trim();File.Delete("Library/PasswordAlignment.request");
   var w=GetWindow<ProductionEmailPreview>();w.passwordBaseline=mode=="baseline";w.passwordConsumer=w.passwordPhase=w.passwordChecks=w.size=0;w.testing=false;
   File.WriteAllText(PasswordResult,"RUN="+DateTime.UtcNow.ToString("O")+" MODE="+mode+"\n");w.PasswordMount();
  };}
  void PasswordMount(){state=passwordConsumer==0?13:1;Mount();view.Password.SetValueWithoutNotify("Demo1234");if(view.Confirmation!=null)view.Confirmation.SetValueWithoutNotify("Demo1234");passwordPhase=0;PasswordPhase();}
  ProductionPasswordField PasswordTarget()=>view.Q<ProductionPasswordField>(passwordConsumer==2?"EmailConfirmationRow":"EmailPasswordRow");
  void PasswordPhase(){var p=PasswordTarget();p.Field.isPasswordField=passwordPhase<2;
   if(passwordPhase%2==1)p.Field.Focus();else view.Email.Focus();
   p.Field.SelectRange(8,8);rootVisualElement.schedule.Execute(PasswordMeasure).ExecuteLater(250);
  }
  static float RelativeY(VisualElement e,VisualElement root){float y=0;while(e!=root){y+=e.layout.y;e=e.parent;}return y;}
  void PasswordNeed(bool value,string name){passwordChecks++;if(!value&&!passwordBaseline)throw new Exception(name);}
  void PasswordMeasure(){try{
   var p=PasswordTarget();var t=p.Field.Q<TextElement>(className:"unity-text-element");if(t==null)throw new Exception("TEXT_ELEMENT_MISSING");
   var selection=p.Field.textSelection;
   var hp=typeof(ITextSelection).GetProperty("lineHeightAtCursorPosition",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
   if(hp==null)throw new Exception("CARET_HEIGHT_API_MISSING");
   var cursor=selection.cursorPosition;float height=(float)hp.GetValue(selection);
   float caret=RelativeY(t,p)+cursor.y-height/2;
   float text=WelcomeRuntimeFooterProbe.TextCenter(t,p);
   File.AppendAllText(PasswordResult,"SIZE="+Sizes[size]+" CONSUMER="+passwordConsumer+" PHASE="+passwordPhase+" FIELD="+p.layout.height+" TEXT_CENTER="+text+" CARET_CENTER="+caret+" CURSOR_Y="+cursor.y+" LINE_HEIGHT="+height+" TEXT_Y="+RelativeY(t,p)+" TEXT_H="+t.layout.height+" ALIGN="+t.resolvedStyle.unityTextAlign+"\n");
   PasswordNeed(p.layout.height==48,"FIELD_HEIGHT");PasswordNeed(Mathf.Abs(text-24)<=1,"TEXT_CENTER");PasswordNeed(Mathf.Abs(caret-24)<=1,"CARET_CENTER");
   PasswordNeed(p.Toggle.layout.height>=44&&p.Toggle.layout.width>=44,"EYE_TOUCH");
   if(passwordPhase==0){passwordInitialText=text;passwordInitialCaret=caret;}
   else{PasswordNeed(Mathf.Abs(text-passwordInitialText)<.01f,"TEXT_SHIFT");PasswordNeed(Mathf.Abs(caret-passwordInitialCaret)<.01f,"CARET_SHIFT");}
   if(++passwordPhase<4){PasswordPhase();return;}
   if(++passwordConsumer<3){PasswordMount();return;}passwordConsumer=0;
   if(++size<Sizes.Length){PasswordMount();return;}
   File.AppendAllText(PasswordResult,"CHECKS="+passwordChecks+"\nRESULT="+(passwordBaseline?"DIAGNOSTIC":"PASS")+"\n");size=1;state=1;Mount();view.Password.SetValueWithoutNotify("Demo1234");view.Confirmation.SetValueWithoutNotify("Demo1234");Focus();
  }catch(Exception e){File.AppendAllText(PasswordResult,"FAIL="+e.Message+"\n");}}
 }
}
