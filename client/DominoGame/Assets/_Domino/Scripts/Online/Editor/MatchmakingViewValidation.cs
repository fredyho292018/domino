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
using UnityEngine.UI;
namespace Domino.Online.Editor
{
    public static class MatchmakingViewValidation
    {
        const string Key="I3.UI.Validation";static bool running;static int checks;static double deadline;
        sealed class Realtime:IRealtimeConnectionService,IRealtimeMatchChannel {
            public RealtimeConnectionState State{get;private set;}=RealtimeConnectionState.CONNECTED;
            public GlobalActivitySnapshot Activity=>null;public event Action Changed;public event Action<string,JObject> MatchMessage;
            public void Start(){}public void Dispose(){}public Task SendMatchCommandAsync(JObject c)=>Task.CompletedTask;
            public void SetBackground(bool b){State=b?RealtimeConnectionState.DISCONNECTED:RealtimeConnectionState.CONNECTED;Changed?.Invoke();}
            public void ActivityUpdate()=>Changed?.Invoke();
        }
        sealed class Api:IOnlineMatchApi {
            public bool Queued;public int Reads,Joins;
            public Task<JObject> SendAsync(string method,string path,JObject body,CancellationToken cancel){
                if(method=="POST"){Queued=true;Joins++;}else if(method=="DELETE")Queued=false;else Reads++;
                return Task.FromResult(new JObject{["state"]=Queued?"QUEUED":"NOT_QUEUED"});
            }
        }
        public static void Run(){
            if(!Application.isBatchMode||!Application.dataPath.Replace('\\','/').Contains("/Validation/Generated/"))throw new InvalidOperationException("Isolated validation only");
            Domino.Editor.LocalizationAssets.Import();SessionState.SetBool(Key,true);Register();EditorSceneManager.OpenScene(Domino.Editor.ClientEditorTools.ScenePath);EditorApplication.isPlaying=true;
        }
        [InitializeOnLoadMethod] static void Register(){if(!SessionState.GetBool(Key,false))return;deadline=EditorApplication.timeSinceStartup+180;EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived+=Log;}
        static void Tick(){if(!SessionState.GetBool(Key,false))return;if(EditorApplication.timeSinceStartup>deadline){Finish(false,"TIMEOUT");return;}if(!running&&EditorApplication.isPlaying&&DominoLocalization.Ready&&ApplicationServices.Realtime!=null){running=true;_=Validate();}}
        static void Log(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)Finish(false,message);}
        static void Check(bool value,string label){checks++;if(!value)throw new Exception(label);}
        static async Task Frames(){int until=Time.frameCount+5;while(Time.frameCount<until)await Task.Delay(60);}
        static async Task Validate(){try{
            ApplicationServices.Realtime.Dispose();var realtime=new Realtime();var api=new Api();
            typeof(ApplicationServices).GetProperty("Realtime").SetValue(null,realtime);typeof(ApplicationServices).GetProperty("OnlineApi").SetValue(null,api);
            var sizes=new[]{new Vector2Int(1080,1920),new Vector2Int(1170,2532),new Vector2Int(1179,2556),new Vector2Int(1290,2796),new Vector2Int(1206,2622),new Vector2Int(1320,2868),new Vector2Int(1080,2400),new Vector2Int(1440,3120),new Vector2Int(1536,2048)};
            foreach(var language in new[]{"en","es"})foreach(var size in sizes){
                DominoLocalization.Select(language);typeof(Domino.Editor.Phase1Validation).GetMethod("ResizeGameView",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).Invoke(null,new object[]{size.x,size.y});await Frames();
                var view=new GameObject("Matchmaking visual test",typeof(RectTransform)).AddComponent<MatchmakingView>();
                view.Initialize(AssetDatabase.LoadAssetAtPath<DominoTileView>("Assets/_Domino/Prefabs/DominoTile.prefab"),AssetDatabase.LoadAssetAtPath<PlayerView>("Assets/_Domino/Prefabs/Player.prefab"),()=>{});await Frames();
                Check(view.Client.State==MatchmakingState.IDLE,"EXPLICIT_SEARCH_REQUIRED");
                var search=view.GetComponentsInChildren<Button>().Single(b=>b.name=="Search");search.onClick.Invoke();search.onClick.Invoke();await Frames();
                Check(view.Client.State==MatchmakingState.SEARCHING,"SEARCH_VISIBLE");
                Check(view.GetComponentsInChildren<Text>().Any(t=>t.text==DominoLocalization.Get("matchmaking.searching")),"LOCALIZED_SEARCH");
                foreach(var text in view.GetComponentsInChildren<Text>()){
                    var corners=new Vector3[4];text.rectTransform.GetWorldCorners(corners);Check(corners.All(p=>Screen.safeArea.Contains(p)),"TEXT_SAFE_AREA");
                    Check(!text.text.StartsWith("matchmaking."),"LOCALIZATION_RESOLVED");
                }
                int reads=api.Reads;realtime.ActivityUpdate();realtime.ActivityUpdate();await Frames();Check(api.Reads==reads,"ACTIVITY_NOT_QUEUE_POLLING");
                int joins=api.Joins;realtime.SetBackground(true);Check(view.Client.State==MatchmakingState.FAILED,"DISCONNECTED_NOT_SEARCHING");api.Queued=false;realtime.SetBackground(false);await Frames();
                Check(view.Client.State==MatchmakingState.IDLE&&api.Joins==joins,"EXPLICIT_SEARCH_AFTER_RECOVERY");
                await view.Client.JoinAsync();await view.Client.CancelAsync();Check(view.Client.State==MatchmakingState.IDLE,"CANCELLED");view.Close();await Frames();
            }
            Finish(true,"I3_UI=PASS SIZES=9 LANGUAGES=en,es CHECKS="+checks+" CONSOLE_ERRORS=0");
        }catch(Exception e){Finish(false,e.ToString());}}
        static void Finish(bool ok,string detail){if(!SessionState.GetBool(Key,false))return;SessionState.SetBool(Key,false);File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../../i3-ui-result.txt")),(ok?"PASS\n":"FAIL\n")+detail);EditorApplication.Exit(ok?0:1);}
    }
}
#endif
