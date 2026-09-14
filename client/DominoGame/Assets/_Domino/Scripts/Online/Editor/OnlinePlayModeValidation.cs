#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Domino.Infrastructure;
using Domino.Realtime;
using Domino.UI;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Domino.Online.Editor
{
    public static class OnlinePlayModeValidation
    {
        const string Key="Domino.I1.Validation";static bool running;static double deadline;static int checks;
        sealed class Channel:IRealtimeMatchChannel {public event Action<string,JObject> MatchMessage;public Task SendMatchCommandAsync(JObject c)=>Task.CompletedTask;}
        sealed class Api:IOnlineMatchApi {public Task<JObject> SendAsync(string m,string p,JObject b,CancellationToken c)=>throw new InvalidOperationException();}
        sealed class ConnectedRealtime:IRealtimeConnectionService {
            public RealtimeConnectionState State=>RealtimeConnectionState.CONNECTED;
            public GlobalActivitySnapshot Activity=>null;public event Action Changed;
            public void Start(){}public void SetBackground(bool b){}public void Dispose(){}
        }
        public static void Run() {
            if(!Application.isBatchMode||!Application.dataPath.Replace('\\','/').Contains("/Validation/Generated/"))throw new Exception("ISOLATED_BATCH_REQUIRED");
            SessionState.SetBool(Key,true);Register();EditorSceneManager.OpenScene(Domino.Editor.ClientEditorTools.ScenePath);EditorApplication.isPlaying=true;
        }
        [InitializeOnLoadMethod] static void Register() {
            if(!SessionState.GetBool(Key,false))return;
            deadline=EditorApplication.timeSinceStartup+300;Application.logMessageReceived+=Log;
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
        }
        static void Tick() {
            if(!SessionState.GetBool(Key,false))return;
            if(EditorApplication.timeSinceStartup>deadline){Finish(false,"TIMEOUT");return;}
            if(!running&&EditorApplication.isPlaying&&DominoLocalization.Ready&&ApplicationServices.Realtime!=null){running=true;_=Validate();}
        }
        static void Log(string message,string stack,LogType type) {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)Finish(false,message);}
        static void Check(bool value,string label) {checks++;if(!value)throw new Exception(label);}
        static async Task Validate() {
            try {
                Time.timeScale=20;
                Application.runInBackground=true;
                ApplicationServices.Realtime.Dispose();
                typeof(ApplicationServices).GetProperty("Realtime").SetValue(null,new ConnectedRealtime());
                var sizes=new[]{new Vector2Int(1080,1920),new Vector2Int(1170,2532),new Vector2Int(1179,2556),new Vector2Int(1290,2796),new Vector2Int(1206,2622),new Vector2Int(1320,2868),new Vector2Int(1080,2400),new Vector2Int(1440,3120),new Vector2Int(1536,2048)};
                for(int seat=0;seat<2;seat++) {
                    DominoLocalization.Select(seat==0?"en":"es");await Frames();
                    var json=File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../../i1-snapshot-"+seat+".json")));
                    var client=new OnlineMatchClient(new Api(),new Channel());client.ApplySnapshot(JObject.Parse(json));
                    var controller=new GameObject("I1 validation").AddComponent<OnlineMatchController>();
                    controller.Initialize(client,AssetDatabase.LoadAssetAtPath<DominoTileView>("Assets/_Domino/Prefabs/DominoTile.prefab"),AssetDatabase.LoadAssetAtPath<PlayerView>("Assets/_Domino/Prefabs/Player.prefab"));
                    foreach(var size in sizes) {
                        typeof(Domino.Editor.Phase1Validation).GetMethod("ResizeGameView",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,new object[]{size.x,size.y});await Frames();
                        var until=EditorApplication.timeSinceStartup+30;
                        while((!controller.Board||controller.Board.IsPreparingRound||controller.Board.PlayedCount==0)&&EditorApplication.timeSinceStartup<until)await Task.Delay(100);
                        var view=controller.Board;Check(view!=null&&!view.IsPreparingRound,"VIEW_READY");
                        var timer=controller.GetComponentsInChildren<UnityEngine.UI.Text>().Single(t=>t.name=="Turn countdown");
                        Check(timer.gameObject.activeInHierarchy&&client.TurnClock.HasDeadline,"I2_COUNTDOWN_VISIBLE");
                        Check(timer.text.StartsWith(seat==0?"Turn:":"Turno:"),"I2_COUNTDOWN_EN_ES");
                        var timerCorners=new Vector3[4];timer.rectTransform.GetWorldCorners(timerCorners);
                        Check(timerCorners.All(v=>Screen.safeArea.Contains(v)),"I2_COUNTDOWN_SAFE_AREA");
                        Check(view.LocalPlayerSeat==seat,"LOCAL_PERSPECTIVE");
                        Check(view.HandViews(seat).All(t=>t.IsFaceUp),"OWN_VISIBLE");Check(view.HandViews(1-seat).All(t=>!t.IsFaceUp),"OPPONENT_HIDDEN");
                        Check(view.PlayedCount==((JArray)client.Snapshot.Public["board"]).Count,"BOARD_CONFIRMED");
                        Check(view.ReserveViews.Count==35,"RESERVE_35");
                        if((int?)client.Snapshot.Public["currentSeat"]==seat) {
                            var tile=view.LocalTiles.First();Check(tile.Selectable,"OWN_TURN_INPUT");tile.Clicked(tile);Check(tile.Selected,"SELECTION_HIGHLIGHT");
                        } else Check(view.LocalTiles.All(t=>!t.Selectable),"OPPONENT_TURN_INPUT_BLOCKED");
                        foreach(var tile in view.LocalTiles) {var corners=new Vector3[4];tile.Rect.GetWorldCorners(corners);Check(corners.All(v=>Screen.safeArea.Contains(v)),"HAND_SAFE_AREA");}
                        if(size==sizes[0]){
                            view.SetSelected(null);await Frames();Canvas.ForceUpdateCanvases();
                            // Queue capture for the rendered Game view, never read the Editor GUI's active target.
                            ScreenCapture.CaptureScreenshot(Path.GetFullPath(Path.Combine(Application.dataPath,"../../i1-seat-"+seat+".png")));
                            await Frames();
                        }
                    }
                    UnityEngine.Object.Destroy(controller.gameObject);await Task.Delay(150);
                }
                Finish(true,"ONLINE_PORTRAIT=PASS SIZES=9 SEATS=2 CHECKS="+checks+" CONSOLE_ERRORS=0");
            } catch(Exception e){Finish(false,e.ToString());}
        }
        static async Task Frames() {
            int target=Time.frameCount+5;double until=EditorApplication.timeSinceStartup+10;
            while(Time.frameCount<target&&EditorApplication.timeSinceStartup<until)await Task.Delay(60);
            Check(Time.frameCount>=target,"RENDER_FRAMES_ADVANCED");
        }
        static void Finish(bool success,string detail) {
            if(!SessionState.GetBool(Key,false))return;SessionState.SetBool(Key,false);Time.timeScale=1;
            File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../../i1-unity-result.txt")),(success?"PASS\n":"FAIL\n")+detail);
            EditorApplication.Exit(success?0:1);
        }
    }
}
#endif
