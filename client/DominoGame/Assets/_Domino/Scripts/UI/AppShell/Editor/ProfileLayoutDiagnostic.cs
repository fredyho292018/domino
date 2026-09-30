using System;
using System.IO;
using System.Linq;
using Domino.UI.AppShell;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
namespace Domino.Editor {
 public sealed partial class ProductionShellPreview {
  int layoutPreset;
  [InitializeOnLoadMethod]static void RegisterProfileLayout(){EditorApplication.update+=()=>{
   if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists("Library/ProfileLayout.request"))return;
   File.Delete("Library/ProfileLayout.request");var w=GetWindow<ProductionShellPreview>();w.layoutPreset=0;File.WriteAllText("Library/ProfileLayout.result.txt","CURRENT_DIAGNOSTIC "+DateTime.UtcNow.ToString("O")+"\n");w.LayoutMount();
  };}
  void LayoutMount(){index=layoutPreset;ProfileMount(new DemoProfileDataSource());rootVisualElement.schedule.Execute(LayoutCapture).ExecuteLater(400);}
  void LayoutCapture(){try {
   var page=shell.Q<ProductionProfilePage>();var actions=page.Q("ProfileActions");var history=page.Q("HistoryHeading").parent;
   foreach(var element in new[]{page.Body,page.Q("SubpageTitle").parent,page.Q("ProfilePlayerCard"),actions,page.Q("ProfilePrimaryAction"),page.Q("ShareProfile"),history,page.Q("ProfileGameRow")})
    File.AppendAllText("Library/ProfileLayout.result.txt",Sizes[layoutPreset]+" name="+element.name+" rect="+element.layout+" shrink="+element.resolvedStyle.flexShrink+" minHeight="+element.resolvedStyle.minHeight+" bottomMargin="+element.resolvedStyle.marginBottom+"\n");
   File.AppendAllText("Library/ProfileLayout.result.txt","ACTION_BOTTOM="+actions.layout.yMax+" HISTORY_TOP="+history.layout.y+" CHILD_BOTTOM="+(actions.layout.y+page.Q("ProfilePrimaryAction").layout.yMax)+"\n");
   if(++layoutPreset<8){LayoutMount();return;}File.AppendAllText("Library/ProfileLayout.result.txt","COMPLETE\n");index=1;ProfileMount(new DemoProfileDataSource());Focus();
  }catch(Exception e){File.AppendAllText("Library/ProfileLayout.result.txt","FAIL="+e.GetType().Name+"\n");}}
 }
}
