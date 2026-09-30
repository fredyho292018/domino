using System;
using System.IO;
using Domino.UI.AppShell;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
namespace Domino.Editor {
 public sealed class ProductionLogoutPreview:EditorWindow {
  static readonly Vector2Int[] Sizes={new Vector2Int(375,667),new Vector2Int(393,852),new Vector2Int(412,915),new Vector2Int(430,932),new Vector2Int(480,1040),new Vector2Int(600,960),new Vector2Int(768,1024),new Vector2Int(834,1194)};
  int size=1,mode,checks;bool testing;VisualElement frame;const string Result="Library/AuthLogout.result.txt";
  [InitializeOnLoadMethod]static void Register(){EditorApplication.update+=()=>{if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists("Library/AuthLogout.request"))return;File.Delete("Library/AuthLogout.request");var w=GetWindow<ProductionLogoutPreview>();w.testing=true;w.size=w.mode=w.checks=0;File.WriteAllText(Result,"RUNNING "+DateTime.UtcNow.ToString("O")+"\n");w.Mount();};}
  [MenuItem("Domino/Production Auth/Logout preview (isolated)")]public static void Open(){var w=GetWindow<ProductionLogoutPreview>();w.testing=false;w.size=1;w.mode=0;w.Mount();w.Focus();}
  void Mount(){titleContent=new GUIContent("PRODUCTION LOGOUT · ISOLATED");minSize=new Vector2(440,700);Show();rootVisualElement.Clear();
   var choice=new EnumField(Mode());choice.RegisterValueChangedCallback(e=>{mode=(int)(PreviewMode)e.newValue;testing=false;Mount();});rootVisualElement.Add(choice);
   frame=new VisualElement();frame.style.width=Sizes[size].x;frame.style.height=Sizes[size].y;frame.style.position=Position.Absolute;frame.style.top=28;frame.style.transformOrigin=new TransformOrigin(0,0,0);float scale=Mathf.Min(1,Mathf.Min(position.width/Sizes[size].x,(position.height-28)/Sizes[size].y));frame.style.scale=new Scale(new Vector3(scale,scale,1));rootVisualElement.Add(frame);
   if(mode==0){var shell=new ProductionAppShell(signOut:()=>{mode=1;Mount();});frame.Add(shell);shell.Select(ShellTab.Menu);var page=(ProductionMenuPage)shell.Pages[4];rootVisualElement.schedule.Execute(()=>page.ScrollTo(page.Q("MenuSignOut"))).ExecuteLater(100);}
   else frame.Add(new ProductionLogoutConfirmation(mode==1,()=>{mode=0;Mount();},()=>{},false));
   if(testing)rootVisualElement.schedule.Execute(Check).ExecuteLater(400);
  }
  enum PreviewMode { Menu, GuestConfirmation, RegisteredConfirmation }
  PreviewMode Mode()=>(PreviewMode)mode;
  void Need(bool ok,string key){checks++;if(!ok)throw new Exception(key);}
  void Check(){try{
   Need(frame.childCount==1,"ONE_PAGE");Need(frame.Q<Label>()!=null,"RENDERS");
   if(mode==0){var row=frame.Q<Button>("MenuSignOut");Need(row!=null&&row.resolvedStyle.height>=44,"SIGN_OUT_TOUCH");Need(row.Q<Image>("LogoutIcon").vectorImage!=null,"SIGN_OUT_ICON");}
   else {foreach(var key in new[]{"CancelSignOut","ConfirmSignOut"}){var button=frame.Q<Button>(key);Need(button!=null&&button.resolvedStyle.height>=44,"ACTION_TOUCH");Need(button.tooltip.Length>0,"ACCESSIBLE_ACTION");}Need(frame.Q<Label>("LogoutExplanation").text.Contains("Guest")== (mode==1),"CONFIRMATION_TYPE");
    var page=frame.Q<ProductionLogoutConfirmation>();var cancel=page.Q<Button>("CancelSignOut");var confirm=page.Q<Button>("ConfirmSignOut");var colors=Domino.UI.Theming.ThemeProvider.Current.Colors;
    Need(cancel.layout.yMax<=confirm.layout.y,"CANCEL_FIRST_SIGN_OUT_SECOND");
    Need(cancel.resolvedStyle.backgroundColor==colors.Primary && confirm.resolvedStyle.backgroundColor==colors.Surface,"SAFE_DESTRUCTIVE_DIFFERENTIATION");
    Need(confirm.resolvedStyle.color==colors.Error && confirm.resolvedStyle.borderTopColor==colors.Error,"SEMANTIC_DESTRUCTIVE");
    foreach(var action in new[]{cancel,confirm}){Need(action.layout.x>=0&&action.layout.xMax<=page.Body.layout.width,"ACTION_HORIZONTAL_OVERFLOW");}
    float end=Mathf.Max(0,page.contentContainer.layout.height-page.contentViewport.layout.height);page.ScrollTo(confirm);
    Need(page.Body.layout.y+confirm.layout.yMax-page.scrollOffset.y<=page.contentViewport.layout.height+1,"SIGN_OUT_REACHABLE");
    page.ScrollTo(cancel);Need(page.Body.layout.y+cancel.layout.y-page.scrollOffset.y>=-1,"CANCEL_REACHABLE");
    page.scrollOffset=Vector2.zero;
}
   foreach(var label in frame.Query<Label>().ToList()){if(label.resolvedStyle.display==DisplayStyle.None)continue;Need(label.layout.width<=Sizes[size].x,"LABEL_WIDTH");}
   File.AppendAllText(Result,Sizes[size]+" "+Mode()+" PASS\n");if(++mode==3){mode=0;size++;}if(size<8){Mount();return;}File.AppendAllText(Result,"CHECKS="+checks+"_PASS\nFAIL=0\nREAL_LOGOUT=NO\n");testing=false;size=1;mode=0;Mount();Focus();
  }catch(Exception e){testing=false;File.AppendAllText(Result,"FAIL="+e.Message+"\n");Debug.LogException(e);}}
 }
}

