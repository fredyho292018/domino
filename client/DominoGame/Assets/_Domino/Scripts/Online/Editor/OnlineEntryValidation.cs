#if UNITY_EDITOR
using System;using System.IO;using System.Linq;using System.Threading;using System.Threading.Tasks;using System.Reflection;
using Domino.Client;using Domino.Infrastructure;using Domino.Realtime;using Domino.UI;using Newtonsoft.Json.Linq;using UnityEngine;using UnityEngine.UI;using UnityEditor;using UnityEditor.SceneManagement;
namespace Domino.Online.Editor {
 public static class OnlineEntryValidation {
 const string Key="Domino.I21.EntryValidation";static bool running;static int checks;static double end;
 sealed class Channel:IRealtimeMatchChannel{public event Action<string,JObject> MatchMessage;public JObject Last;public Task SendMatchCommandAsync(JObject value){Last=value;return Task.CompletedTask;}}
 sealed class Api:IOnlineMatchApi{public Task<JObject> SendAsync(string m,string p,JObject b,CancellationToken t)=>throw new Exception("NO_NETWORK_FIXTURE");}
 sealed class Connected:IRealtimeConnectionService{public RealtimeConnectionState State=>RealtimeConnectionState.CONNECTED;public GlobalActivitySnapshot Activity=>null;public event Action Changed;public void Start(){}public void Dispose(){}public void SetBackground(bool b){}}
 public static void Run(){
            Domino.Infrastructure.ValidationNetworkPolicy.BeginIsolated();if(!Application.isBatchMode||!Application.dataPath.Replace('\\','/').Contains("/Validation/Generated/"))throw new Exception("ISOLATED_REQUIRED");SessionState.SetBool(Key,true);Register();Domino.Editor.LocalizationAssets.Import();EditorSceneManager.OpenScene(Domino.Editor.ClientEditorTools.ScenePath);EditorApplication.isPlaying=true;}
 [InitializeOnLoadMethod] static void Register(){if(!SessionState.GetBool(Key,false))return;end=EditorApplication.timeSinceStartup+240;EditorApplication.update+=Tick;Application.logMessageReceived+=(m,s,t)=>{if(t==LogType.Error||t==LogType.Exception||t==LogType.Assert)Finish(false,m);};}
 static void Tick(){if(!SessionState.GetBool(Key,false))return;if(EditorApplication.timeSinceStartup>end){Finish(false,"TIMEOUT");return;}var local=UnityEngine.Object.FindFirstObjectByType<DominoClientController>();if(!running&&EditorApplication.isPlaying&&local?.Menu&&DominoLocalization.Ready){running=true;_=Validate(local);}}
 static void Check(bool b,string label){checks++;if(!b)throw new Exception(label);}
 static async Task Frames(){await Task.Delay(300);Canvas.ForceUpdateCanvases();}
 static void Resize(int w,int h)=>typeof(Domino.Editor.Phase1Validation).GetMethod("ResizeGameView",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{w,h});
 static string Dir=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../I21"));
 static async Task Validate(DominoClientController local){try{
  var sizes=new[]{new Vector2Int(1080,1920),new Vector2Int(1170,2532),new Vector2Int(1080,2400),new Vector2Int(1536,2048),new Vector2Int(1920,1080)};
  foreach(string lang in new[]{"en","es"}){DominoLocalization.Select(lang);foreach(var size in sizes){Resize(size.x,size.y);local.Menu.Show(StartScreen.ModeSelector);await Frames();Check(local.Menu.DuelPlay&&local.Menu.DuelPlay.gameObject.activeInHierarchy,"NORMAL_DUEL_VISIBLE");local.OpenDevelopmentOnlineEntry();await Frames();var entry=UnityEngine.Object.FindFirstObjectByType<OnlineEntryView>();Check(entry&&local.Session==null,"ONLINE_ENTRY_NO_LOCAL_ENGINE");
   foreach(var rect in new[]{entry.CreateButton.GetComponent<RectTransform>(),entry.JoinButton.GetComponent<RectTransform>(),entry.BackButton.GetComponent<RectTransform>(),entry.MatchId.GetComponent<RectTransform>()}){var corners=new Vector3[4];rect.GetWorldCorners(corners);Check(corners.All(p=>Screen.safeArea.Contains(p)),"ENTRY_SAFE_AREA");}
   Check(entry.CreateButton.GetComponentInChildren<Text>().text==(lang=="es"?"Crear partida":"Create match"),"LOCALIZED_ENTRY");entry.CreateButton.onClick.Invoke();Check(entry.GetComponentsInChildren<Text>().Single(t=>t.name=="Feedback").text==DominoLocalization.Get("online.network_unavailable"),"SAFE_OFFLINE_FEEDBACK");
   if(size==sizes[0]){ScreenCapture.CaptureScreenshot(Path.Combine(Dir,"entry-"+lang+".png"));await Frames();}entry.BackButton.onClick.Invoke();await Frames();Check(local.Menu.Screen==StartScreen.ModeSelector&&local.Session==null,"BACK_PRESERVES_MENU");
  }}
  ApplicationServices.Realtime.Dispose();typeof(ApplicationServices).GetProperty("Realtime").SetValue(null,new Connected());local.Menu.Show(StartScreen.Match);Resize(1080,1920);
  var wire=JObject.Parse(File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../../i1-snapshot-0.json"))));
  var starter=(JObject)wire.DeepClone();starter["phase"]="STARTER_SELECTION";starter["lastSequence"]=0;starter["publicState"]["lastSequence"]=0;starter["privateState"]["lastSequence"]=0;starter["publicState"]["currentRound"]=0;starter["publicState"]["board"]=new JArray();starter["publicState"]["tilesRemainingPerSeat"]=new JArray(0,0);starter["privateState"]["hand"]=new JArray();starter["privateState"]["setup"]=null;starter["turnDeadlineAt"]=null;starter["publicState"]["turnDeadline"]=null;
  starter["starter"]=new JObject{["method"]="HIGH_TILE_SELECTION",["guessingSeat"]=0,["attempt"]=1,["selectedSeats"]=new JArray(),["availableCandidates"]=new JArray(0,1)};
  var channel=new Channel();var client=new OnlineMatchClient(new Api(),channel);client.ApplySnapshot(starter);var controller=new GameObject("I21 high tile fixture").AddComponent<OnlineMatchController>();controller.Initialize(client,AssetDatabase.LoadAssetAtPath<DominoTileView>("Assets/_Domino/Prefabs/DominoTile.prefab"),AssetDatabase.LoadAssetAtPath<PlayerView>("Assets/_Domino/Prefabs/Player.prefab"));await Frames();
  var presentation=controller.Board.StarterView;Check(presentation&&presentation.transform.parent==controller.Board.BoardSurface,"SAME_ONLINE_TABLE");Check(presentation.Tiles.All(t=>!t.IsFaceUp),"HIDDEN_SERVER_CANDIDATES");presentation.Choices[1].onClick.Invoke();Check((string)channel.Last["type"]=="SELECT_STARTER_TILE"&&(int)channel.Last["candidate"]==1,"DIRECT_TILE_SENDS_INTENT");Check(client.Snapshot.Phase=="STARTER_SELECTION","NO_LOCAL_STARTER_AUTHORITY");ScreenCapture.CaptureScreenshot(Path.Combine(Dir,"online-high-tile.png"));await Frames();
  wire["privateState"]["setup"]=new JObject{["starterMethod"]="HIGH_TILE_SELECTION",["starterSeat"]=1,["chosenTiles"]=new JArray(new JObject{["sideA"]=6,["sideB"]=6},new JObject{["sideA"]=9,["sideB"]=9})};client.ApplySnapshot(wire);await Task.Delay(650);Check(presentation.Tiles.All(t=>t.IsFaceUp),"SERVER_REVEAL_ONLY");ScreenCapture.CaptureScreenshot(Path.Combine(Dir,"online-high-revealed.png"));await Task.Delay(1200);Check(!presentation.gameObject.activeSelf,"BOARD_RESTORED_AFTER_REVEAL");UnityEngine.Object.Destroy(controller.gameObject);await Frames();Finish(true,"ENTRY_EN_ES=PASS SAFE_AREA_5_SIZES=PASS ONLINE_HIGH_TILE=PASS CHECKS="+checks+" CONSOLE_ERRORS=0");
 }catch(Exception e){Finish(false,e.ToString());}}
 static void Finish(bool ok,string detail){if(!SessionState.GetBool(Key,false))return;SessionState.SetBool(Key,false);File.WriteAllText(Path.Combine(Dir,"entry-result.txt"),(ok?"PASS\n":"FAIL\n")+detail);EditorApplication.Exit(ok?0:1);}
 }
}
#endif
