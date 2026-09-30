using System;
using System.IO;
using System.Linq;
using Domino.AppShellMock;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
namespace Domino.Editor
{
 public static class MockResponsiveValidation
 {
  const string Request="Library/AppShellResponsive.request",Result="Library/AppShellResponsive.result.txt";
  static MockShellPreview window;static int preset,page,checks;static MockShellView view;
  static readonly MockPage[] Pages={MockPage.Profile,MockPage.Membership,MockPage.Experience,MockPage.CoachSelection,MockPage.ContactResults,MockPage.Trial,MockPage.Welcome,MockPage.Contacts,MockPage.Home,MockPage.Puzzles,MockPage.Learn,MockPage.Watch,MockPage.Menu};
  [InitializeOnLoadMethod]static void Register(){EditorApplication.update+=Poll;}
  static void Poll(){if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;try{File.Delete(Request);}catch(IOException){return;}Run();}
  [MenuItem("Domino/App Shell Mock/Validate responsive presets")]
  public static void Run(){preset=page=checks=0;File.WriteAllText(Result,"RUNNING\n");MockShellPreview.OpenProfile();window=EditorWindow.GetWindow<MockShellPreview>();view=window.rootVisualElement.Q<MockShellView>();Next();}
  static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
  static void Next()
  {
   view.State.Start((int)Pages[page]>=(int)MockPage.Home?MockEntry.EXISTING_REGISTERED:MockEntry.NEW_GUEST);view.State.Go(Pages[page]);view.State.ExperienceLevel=2;view.State.SelectedCoachId="amara";view.State.selectedMembershipPlan=MockMembershipPlan.DIAMOND;view.State.MockFriendSelections.Add("Ana Rivera");view.Rebuild();
   window.SetPreviewDevice(preset);window.rootVisualElement.schedule.Execute(Validate).ExecuteLater(500);
  }
  static void Validate()
  {
   try
   {
    int[] widths={375,393,412,430,480,600,768,834},heights={667,852,915,932,1040,960,1024,1194};
    Check(view.panel!=null&&view.worldBound.width>0&&view.worldBound.height>0&&view.Query<Label>().ToList().Any(l=>!string.IsNullOrEmpty(l.text)),"Mounted nonblank page");
    Check(MockShellPreview.PreviewSizes.Length==8,"Eight presets");var frame=window.rootVisualElement.Q("MockPhone");
    Check(Mathf.Abs(frame.layout.width-widths[preset])<1&&Mathf.Abs(frame.layout.height-heights[preset])<1,"Logical dimensions");
    Check(ReferenceEquals(view,window.rootVisualElement.Q<MockShellView>())&&view.State.Page==Pages[page],"View and route preserved");
    Check(view.State.ExperienceLevel==2&&view.State.SelectedCoachId=="amara"&&view.State.MockFriendSelections.Contains("Ana Rivera"),"State preserved");
    Check(view.layout.width<=620.1f&&view.layout.width<=widths[preset],"Content maximum");
    Check(window.PreviewScale>0&&window.PreviewScale<=1,"Fit scale");
    Check(Mathf.Abs(frame.worldBound.width-frame.layout.width*window.PreviewScale)<1,"Scale preserves logical size");
    var scroll=view.Q<ScrollView>();Check(scroll!=null,"Scroll available");
    var header=view.Q("MockSubpageHeader");if(header!=null)Check(header.worldBound.xMin>=frame.worldBound.xMin-1&&header.worldBound.xMax<=frame.worldBound.xMax+1,"Header bounds");
    var nav=view.Q("MockBottomToolbar");if(nav!=null){Check(nav.worldBound.xMin>=frame.worldBound.xMin-1&&nav.worldBound.xMax<=frame.worldBound.xMax+1,"Nav bounds");Check(nav.worldBound.yMax<=frame.worldBound.yMax-frame.resolvedStyle.paddingBottom*window.PreviewScale+1,"Safe bottom");Check(scroll.worldBound.yMax<=nav.worldBound.yMin+1,"Content above nav");}
    foreach(var child in scroll.contentContainer.Children())Check(child.worldBound.xMin>=view.worldBound.xMin-1&&child.worldBound.xMax<=view.worldBound.xMax+1,"No horizontal overflow: "+child.name);
    var last=scroll.contentContainer.Children().LastOrDefault(c=>c is Button);if(last!=null){scroll.ScrollTo(last);Check(scroll.contentContainer.Contains(last),"CTA in scroll content");}
    if(Pages[page]==MockPage.Profile){Check(view.Query<Button>("ProfileGameRow").ToList().Count==5,"Five history rows");foreach(var row in view.Query<Button>("ProfileGameRow").ToList())Check(row.Q<Label>("GameMatchup").worldBound.xMax<=row.Q<Label>("GameScore").worldBound.xMin+1,"Score separation");}
    if(Pages[page]==MockPage.Membership||Pages[page]==MockPage.Trial){Check(view.Q("PremiumTabs").childCount==4,"Four plan tabs");Check(view.Q("PremiumFeatures").childCount==8,"Eight features");}
    File.AppendAllText(Result,"PRESET_"+preset+"_"+Pages[page]+"=PASS\n");
    if(++page<Pages.Length){Next();return;}page=0;if(++preset<8){Next();return;}
    File.AppendAllText(Result,"RESPONSIVE_CHECKS="+checks+"_PASS\n");MockShellPreview.OpenProfile();window.SetPreviewDevice(6);
   }catch(Exception e){File.AppendAllText(Result,"FAIL="+e.Message);Debug.LogException(e);}
  }
 }
}
