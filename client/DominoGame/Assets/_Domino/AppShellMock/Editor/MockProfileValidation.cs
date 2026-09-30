using System;
using System.IO;
using System.Linq;
using Domino.AppShellMock;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
namespace Domino.Editor
{
 public static class MockProfileValidation
 {
  const string Request="Library/AppShellProfile.request",Result="Library/AppShellProfile.result.txt";
  static MockShellPreview window;static int device,checks;
  [InitializeOnLoadMethod]static void Register(){EditorApplication.update+=Poll;}
  static void Poll(){if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;try{File.Delete(Request);}catch(IOException){return;}Run();}
  [MenuItem("Domino/App Shell Mock/Validate Profile")]
  public static void Run(){checks=0;device=0;File.WriteAllText(Result,"RUNNING\n");MockShellPreview.OpenMenu();window=EditorWindow.GetWindow<MockShellPreview>();Next();}
  static void Check(bool v,string m){if(!v)throw new Exception(m);checks++;}
  static void Tap(Button b){using(var e=NavigationSubmitEvent.GetPooled()){e.target=b;b.SendEvent(e);}}
  static void Next(){window.SetPreviewDevice(device);var v=window.rootVisualElement.Q<MockShellView>();v.State.Tab("Menu");v.Rebuild();Tap(v.Q<Button>("MenuProfile"));v.State.Profile.displayName="Alex Alejandro Fernandez · Demo player";v.Rebuild();window.rootVisualElement.schedule.Execute(Validate).ExecuteLater(700);}
  static void Validate()
  {
   try
   {
    var v=window.rootVisualElement.Q<MockShellView>();Check(v.State.Page==MockPage.Profile,"Menu header opens Profile");
    Check(v.Q<Button>("ProfilePrimaryAction").Q<Label>().text=="Edit Profile","Own action");Check(!v.Query<Label>().ToList().Any(l=>l.text=="Add Friend"),"No self friend");
    Check(v.Q<Button>("ShareProfile")!=null,"Share visible");Check(v.Q<Label>("ProfileCountry").text=="Cuba","Country");Check(v.Q<Label>("ProfileJoined").text=="Joined January 21, 2022","Joined date");Check(v.Q<Image>("ProfileFlag").vectorImage!=null,"Flag vector");
    var card=v.Q("ProfilePlayerCard");var name=v.Q<Label>("ProfilePlayerName");var avatar=card.Q("PlayerAvatar");Check(name.worldBound.xMin>=avatar.worldBound.xMax&&name.worldBound.xMax<=card.worldBound.xMax,"Long name bounds");Check(name.resolvedStyle.whiteSpace==WhiteSpace.Normal,"Long name wraps");
    var rows=v.Query<Button>("ProfileGameRow").ToList();Check(rows.Count==5,"Five games");float scoreRight=rows[0].Q<Label>("GameScore").worldBound.xMax;
    for(int i=0;i<rows.Count;i++){var row=rows[i];var score=row.Q<Label>("GameScore");var desc=row.Q<Label>("GameMatchup");Check(score.text==v.State.ProfilePreviewGames[i].Score,"Score order");Check(score.style.unityTextAlign.value==TextAnchor.MiddleRight&&Mathf.Abs(score.worldBound.xMax-scoreRight)<1,"Score alignment");Check(desc.worldBound.xMax<=score.worldBound.xMin,"No score overlap");Check(row.Q<Label>("GameResult").text==v.State.ProfilePreviewGames[i].Result,"Result text");Check(row.Q<Image>().image!=null,"Avatar");}
    Check(v.Q<Button>("NavMenu").Q<Image>().tintColor==MockShellTheme.Text,"Menu active");Check(v.Q("MockBottomToolbar").Children().Count()==5,"Five bottom tabs");
    var all=v.Query<Button>().ToList().First(b=>b.text=="View All Games");var scroll=v.Q<ScrollView>();scroll.ScrollTo(all);Check(scroll.worldBound.yMax<=v.Q("MockBottomToolbar").worldBound.yMin+1,"Content viewport above toolbar");Check(scroll.verticalScroller.highValue>0&&scroll.scrollOffset.y>0,"Last action scroll reachable");
    Tap(all);Check(v.State.Page==MockPage.ProfileHistory&&v.Query<Button>("ProfileGameRow").ToList().Count==8,"All games");Tap(v.Q<Button>("BackAction"));Check(v.State.Page==MockPage.Profile,"History Back");
    Tap(v.Query<Button>("ProfileGameRow").First());Check(v.State.Page==MockPage.Placeholder,"Replay placeholder");v.State.Back();v.Rebuild();
    foreach(var mode in new[]{MockProfileMode.OTHER_NOT_FRIEND,MockProfileMode.OTHER_REQUEST_SENT,MockProfileMode.OTHER_FRIEND}){v.State.SetProfileMode(mode);v.Rebuild();Check(v.Q<Button>("ProfilePrimaryAction").Q<Label>().text==v.State.ProfileAction,"Friend mode");if(mode==MockProfileMode.OTHER_NOT_FRIEND){Tap(v.Q<Button>("ProfilePrimaryAction"));Check(v.State.Profile.friendState==MockFriendState.REQUEST_SENT,"Local request");}}
    Tap(v.Q<Button>("ShareProfile"));Check(v.State.Notice.Contains("Nothing was shared"),"Mock share");Tap(v.Q<Button>("BackAction"));Check(v.State.Page==MockPage.Menu,"Profile Back Menu");
    File.AppendAllText(Result,"DEVICE_"+device+"=PASS\n");if(++device<3){Next();return;}File.AppendAllText(Result,"PROFILE_CHECKS="+checks+"_PASS\n");MockShellPreview.OpenProfile();
   }catch(Exception e){File.AppendAllText(Result,"FAIL="+e.Message);Debug.LogException(e);}
  }
 }
}
