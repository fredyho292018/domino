using System;
using System.IO;
using System.Linq;
using Domino.AppShellMock;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
namespace Domino.Editor
{
 public static class MockWelcomeValidation
 {
  const string Request="Library/AppShellWelcome.request",Result="Library/AppShellWelcome.result.txt";
  static MockShellPreview window;static int device,checks;
  [InitializeOnLoadMethod]static void Register(){EditorApplication.update+=Poll;}
  static void Poll(){if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;try{File.Delete(Request);}catch(IOException){return;}Run();}
  [MenuItem("Domino/App Shell Mock/Validate Welcome responsive")]
  public static void Run(){checks=device=0;File.WriteAllText(Result,"RUNNING\n");MockShellPreview.Open();window=EditorWindow.GetWindow<MockShellPreview>();Next();}
  static void Check(bool ok,string m){if(!ok)throw new Exception(m);checks++;}
  static void Tap(Button b){using(var e=NavigationSubmitEvent.GetPooled()){e.target=b;b.SendEvent(e);}}
  static void Next(){window.SetPreviewDevice(device);window.rootVisualElement.schedule.Execute(Validate).ExecuteLater(600);}
  static void Validate()
  {
   try
   {
    var v=window.rootVisualElement.Q<MockShellView>();var content=v.Q("WelcomeContent");var scroll=v.Q<ScrollView>();
    Check(v.State.Page==MockPage.Welcome,"Welcome preserved");Check(content.layout.width<=620.1f,"Maximum width");Check(Mathf.Abs(content.worldBound.center.x-scroll.contentViewport.worldBound.center.x)<1,"Centered horizontally");
    if(content.worldBound.height<scroll.contentViewport.worldBound.height)Check(Mathf.Abs(content.worldBound.center.y-scroll.contentViewport.worldBound.center.y)<2,"Balanced vertically");
    var title=v.Q<Label>("WelcomeTitle");Check(title.text=="CUBAN DOMINO CLUB"&&!title.text.Contains("\n"),"Natural title wrap");
    var buttons=new[]{"Continue with Google","Continue with Facebook","Continue with Email","Continue with Phone","Continue as Guest"}.Select(n=>v.Query<Button>().ToList().First(b=>b.name==n)).ToArray();
    foreach(var b in buttons){Check(Mathf.Abs(b.worldBound.width-buttons[0].worldBound.width)<1,"Uniform width");Check(b.worldBound.xMin>=v.worldBound.xMin&&b.worldBound.xMax<=v.worldBound.xMax+1,"Button bounds");Check(b.style.backgroundColor.value==(b==buttons[0]?MockShellTheme.Primary:MockShellTheme.Surface),"Button color");Check(b.Q<Label>("AuthPrimaryLabel").style.unityFont.value.name=="SourceSans3-Bold","Label weight");Check(b.Q<Image>("AuthProviderIcon").vectorImage!=null,"Provider icon");Check(b.Q<Label>("AuthPrimaryLabel").text==b.name,"Accessible label");Check(Mathf.Abs(b.Q<Label>("AuthPrimaryLabel").worldBound.xMin-buttons[0].Q<Label>("AuthPrimaryLabel").worldBound.xMin)<1,"Text columns aligned");}
    foreach(var b in buttons){
     Check(Mathf.Abs(b.layout.height-56)<.1f,"Compact 56px height");
     var area=b.Q("AuthIconArea");Check(Mathf.Abs(area.layout.width-34)<.1f&&Mathf.Abs(area.layout.height-34)<.1f,"34px icon container");
     Check(area.style.borderTopLeftRadius.value.value==17,"Circular container");
     Check(Mathf.Abs(area.worldBound.xMin-buttons[0].Q("AuthIconArea").worldBound.xMin)<1,"Icon columns");
     Check(Mathf.Abs(b.Q("AuthDivider").worldBound.xMin-buttons[0].Q("AuthDivider").worldBound.xMin)<1,"Divider columns");
     Check(Mathf.Abs(b.Q("AuthChevron").worldBound.xMin-buttons[0].Q("AuthChevron").worldBound.xMin)<1,"Chevron columns");
     Check(b.Q<Label>("AuthSecondaryLabel").worldBound.yMax<=b.worldBound.yMax,"Secondary fits row");
    }
    Check(buttons.Length==5,"Five choices");
    string[] descriptions={"Fast, secure and easy","Play with your friends","Use your email and password","Sign in with your phone number","Play now, create an account later"};
    for(int i=0;i<5;i++){Check(buttons[i].Q<Label>("AuthSecondaryLabel").text==descriptions[i],"Description");Check(buttons[i].Q<Image>("AuthProviderIcon").pickingMode==PickingMode.Ignore,"Whole row target");}
    Check(buttons[2].Q<Image>().vectorImage!=buttons[3].Q<Image>().vectorImage,"Email phone distinct");
    var sign=v.Q("WelcomeSignInRow");Check(sign.childCount==2,"Inline sign in");Check(sign.Q<Button>().style.backgroundColor.value==Color.clear,"Text action");Check(sign.Q<Button>().style.unityFont.value.name=="SourceSans3-Bold","Sign in bold");
    Check(sign.Children().First().worldBound.xMax<=sign.Q<Button>().worldBound.xMin+1,"Sign in no overlap");
    Check(!v.Query<Label>().ToList().Any(l=>l.text.Contains("No real account")),"Technical intro removed");
    foreach(var b in buttons){string text=b.name;Tap(v.Query<Button>().ToList().First(x=>x.name==text));Check(v.State.Page!=MockPage.Welcome,"Action preserved");v.State.Start(MockEntry.NO_SESSION);v.Rebuild();}
    Tap(v.Q("WelcomeSignInRow").Q<Button>());Check(v.State.Page==MockPage.SignIn&&v.State.SignInFlow,"Sign in route");v.State.Start(MockEntry.NO_SESSION);v.Rebuild();
    File.AppendAllText(Result,"PRESET_"+device+"=PASS\n");if(++device<8){Next();return;}File.AppendAllText(Result,"WELCOME_CHECKS="+checks+"_PASS\n");MockShellPreview.Open();window.SetPreviewDevice(0);
   }catch(Exception e){File.AppendAllText(Result,"FAIL="+e.Message);Debug.LogException(e);}
  }
 }
}
