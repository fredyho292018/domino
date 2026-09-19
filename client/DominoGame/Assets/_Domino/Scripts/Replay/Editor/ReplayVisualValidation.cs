#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Domino.Client;
using Domino.Infrastructure;
using Domino.Online;
using Domino.UI;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Domino.Replay.Editor
{
    public static class ReplayVisualValidation
    {
        const string Key="Domino.I4.Validation";static bool running;static double deadline;static int checks;
        static string Output=>Environment.GetEnvironmentVariable("DOMINO_I4_OUTPUT");
        sealed class Api:IOnlineMatchApi
        {
            public readonly JObject[] Fixtures=new[]{"DUEL_1V1","PARTNERS_2V2_ONLINE"}.Select(k=>JObject.Parse(File.ReadAllText(Path.Combine(Output,k+".json")))).ToArray();
            public int Requests;
            public string Scenario;
            public async Task<JObject> SendAsync(string method,string path,JObject body,CancellationToken token)
            {
                Check(method=="GET","READ_ONLY_METHOD");Requests++;
                if(Scenario=="empty")return new JObject{["items"]=new JArray(),["nextCursor"]=null};
                if(Scenario=="error")throw new InvalidOperationException("Simulated unavailable transport");
                if(Environment.GetEnvironmentVariable("DOMINO_I4_LOOPBACK")=="true") {
                    Check(path.StartsWith("players/me/history?")||path.StartsWith("matches/"),"READ_PATH");
                    using var http=new System.Net.Http.HttpClient();
                    http.DefaultRequestHeaders.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer","i4-loopback-participant");
                    using var response=await http.GetAsync("http://127.0.0.1:18087/api/v1/"+path,token);
                    response.EnsureSuccessStatusCode();return JObject.Parse(await response.Content.ReadAsStringAsync());
                }
                if(path.StartsWith("players/me/history?"))return new JObject {["items"]=new JArray(Fixtures.Select(f=>(object)new JObject {
                    ["history"]=new JObject {["matchId"]=f["manifest"]["matchId"].DeepClone(),["modeKey"]=f["manifest"]["modeKey"].DeepClone(),["score"]=f["manifest"]["finalScore"].DeepClone(),["result"]="WIN",["finishedAt"]="2026-09-19T20:00:00Z"},
                    ["participants"]=f["manifest"]["participants"].DeepClone(),["teams"]=f["manifest"]["teams"].DeepClone(),["replayAvailable"]=true})),["nextCursor"]=null};
                var fixture=Fixtures.Single(f=>path.Contains((string)f["manifest"]["matchId"]));
                if(path.EndsWith("/replay"))return (JObject)fixture["manifest"].DeepClone();
                int after=int.Parse(path.Split(new[]{"after="},StringSplitOptions.None)[1].Split('&')[0]);
                var events=(JArray)fixture["events"];return new JObject {["items"]=new JArray(events.Skip(after).Take(250)),["lastSequence"]=events.Count};
            }
        }
        public static void Run()
        {
            if(!Application.dataPath.Replace('\\','/').Contains("/Validation/Generated/"))throw new Exception("ISOLATED_PROJECT_REQUIRED");
            ValidationNetworkPolicy.BeginIsolated();Domino.Editor.LocalizationAssets.ImportTablesOnly();
            SessionState.SetBool(Key,true);Register();EditorSceneManager.OpenScene(Domino.Editor.ClientEditorTools.ScenePath);EditorApplication.isPlaying=true;
        }
        [InitializeOnLoadMethod]static void Register(){if(!SessionState.GetBool(Key,false))return;deadline=EditorApplication.timeSinceStartup+480;Application.logMessageReceived+=Log;EditorApplication.update-=Tick;EditorApplication.update+=Tick;}
        static void Tick(){if(!SessionState.GetBool(Key,false))return;if(EditorApplication.timeSinceStartup>deadline){Finish(false,"TIMEOUT");return;}if(!running&&EditorApplication.isPlaying&&DominoLocalization.Ready&&UnityEngine.Object.FindFirstObjectByType<DominoClientController>()?.Menu){running=true;_=Validate();}}
        static void Log(string m,string s,LogType t){if(t==LogType.Error||t==LogType.Exception||t==LogType.Assert)Finish(false,m+"\n"+s);}
        static void Check(bool value,string name){checks++;if(!value)throw new Exception(name);}
        static async Task Frames(int count=6){int end=Time.frameCount+count;while(Time.frameCount<end)await Task.Delay(35);}
        static void Resize(int w,int h)=>typeof(Domino.Editor.Phase1Validation).GetMethod("ResizeGameView",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,new object[]{w,h});
        static async Task Validate()
        {
            try {
                var api=new Api();typeof(ApplicationServices).GetProperty("OnlineApi").SetValue(null,api);
                var menu=UnityEngine.Object.FindFirstObjectByType<DominoClientController>().Menu;
                Resize(1080,1920);await Frames();menu.GetComponentsInChildren<Button>().Single(b=>b.name=="History").onClick.Invoke();await Frames();
                var view=UnityEngine.Object.FindFirstObjectByType<HistoryReplayView>();Check(view,"NORMAL_HISTORY_ENTRY");
                ScreenCapture.CaptureScreenshot(Path.Combine(Output,"history.png"));await Frames();
                foreach(var fixture in api.Fixtures) {
                    view.Open((string)fixture["manifest"]["matchId"]);
                    double detailDeadline=EditorApplication.timeSinceStartup+25;
                    while(!view.GetComponentsInChildren<Button>().Any(b=>b.name=="Replay")&&EditorApplication.timeSinceStartup<detailDeadline)await Frames();
                    Check(view.GetComponentsInChildren<Button>().Any(b=>b.name=="Replay"),"DETAIL_LOADED");await Frames();
                    ScreenCapture.CaptureScreenshot(Path.Combine(Output,"detail-"+(string)fixture["manifest"]["modeKey"]+".png"));await Frames();
                    view.LoadReplay();double end=EditorApplication.timeSinceStartup+25;
                    while(!view.Board&&EditorApplication.timeSinceStartup<end)await Frames();Check(view.Board,"REPLAY_LOADED");await Frames();
                    int requests=api.Requests;
                    foreach(string lang in new[]{"en","es"})foreach(var size in new[]{new Vector2Int(1080,1920),new Vector2Int(1170,2532),new Vector2Int(1179,2556),new Vector2Int(1290,2796),new Vector2Int(1206,2622),new Vector2Int(1320,2868),new Vector2Int(1080,2400),new Vector2Int(1440,3120),new Vector2Int(1536,2048)}) {
                        DominoLocalization.Select(lang);Resize(size.x,size.y);await Frames();
                        view.Seek(Math.Min(25,view.Timeline.Count));await Frames();
                        var tableCorners=new Vector3[4];view.Board.BoardSurface.GetWorldCorners(tableCorners);
                        Check((tableCorners[2].x-tableCorners[0].x)*(tableCorners[2].y-tableCorners[0].y)>Screen.width*Screen.height*.15f,"TABLE_VISUALLY_LARGE");
                        for(int seat=-1;seat<((JArray)fixture["manifest"]["participants"]).Count;seat++) {
                            view.SelectPerspective(seat);await Frames();
                            Check(view.Board.LocalPlayerSeat==Math.Max(0,seat),"SELECTED_BOTTOM");
                            for(int p=0;p<((JArray)fixture["manifest"]["participants"]).Count;p++)Check(view.Board.HandViews(p).All(t=>t.IsFaceUp==(seat==p)&&!t.Selectable),"HAND_PRIVACY_AND_NO_INPUT");
                            foreach(var tile in view.Board.LocalTiles){var corners=new Vector3[4];tile.Rect.GetWorldCorners(corners);Check(corners.All(v=>Screen.safeArea.Contains(v)),"HAND_IN_SAFE_AREA");}
                        }
                        foreach(var button in view.GetComponentsInChildren<Button>().Where(b=>b.transform.IsChildOf(view.transform.Find("Safe area/Replay controls")))) {
                            var corners=new Vector3[4];button.GetComponent<RectTransform>().GetWorldCorners(corners);Check(corners.All(v=>Screen.safeArea.Contains(v)),"CONTROL_SAFE "+button.name);
                            var label=button.GetComponentInChildren<Text>();Check(!string.IsNullOrEmpty(label.text)&&label.cachedTextGenerator.vertexCount>0,"CONTROL_LABEL_VISIBLE "+button.name);
                        }
                        if(size.x==1080&&size.y==1920){ScreenCapture.CaptureScreenshot(Path.Combine(Output,"replay-"+(string)fixture["manifest"]["modeKey"]+"-"+lang+".png"));await Frames();}
                    }
                    foreach(int n in new[]{0,200,50,400,125,view.Timeline.Count}){view.Seek(Math.Min(n,view.Timeline.Count));await Frames();Check(view.Sequence==Math.Min(n,view.Timeline.Count),"SEEK");}
                    view.Seek(10);await Frames();view.SetPlaying(true);await Task.Delay(1900);Check(view.Sequence>10,"PLAY");view.SetPlaying(false);int paused=view.Sequence;await Task.Delay(850);Check(view.Sequence==paused,"PAUSE");
                    var speed=view.GetComponentsInChildren<Button>().Single(b=>b.name=="Speed");
                    foreach(float expectedSpeed in new[]{2f,.5f,1f}) {
                        speed.onClick.Invoke();Check(view.PlaybackSpeed==expectedSpeed,"PLAYBACK_SPEED");
                        view.Seek(10);await Frames();view.SetPlaying(true);await Task.Delay((int)(850/expectedSpeed));
                        Check(view.Sequence>10,"PLAY_AT_SPEED");view.SetPlaying(false);
                    }
                    view.Seek(view.Timeline.Count);await Frames();
                    Check((string)view.Timeline.Seek(view.Sequence).Data["phase"]=="MATCH_FINISHED","FINAL_MATCH");
                    ScreenCapture.CaptureScreenshot(Path.Combine(Output,"result-"+(string)fixture["manifest"]["modeKey"]+".png"));await Frames();
                    Check(api.Requests==requests,"NO_PLAYBACK_REQUESTS");
                    view.GetComponentsInChildren<Button>().Single(b=>b.name=="Back").onClick.Invoke();await Frames();
                    view.GetComponentsInChildren<Button>().Single(b=>b.name=="Back").onClick.Invoke();await Frames();
                }
                view.GetComponentsInChildren<Button>().Single(b=>b.name=="Back").onClick.Invoke();await Frames();
                foreach(string lang in new[]{"en","es"})foreach(string scenario in new[]{"empty","error"}) {
                    DominoLocalization.Select(lang);api.Scenario=scenario;
                    menu.GetComponentsInChildren<Button>().Single(b=>b.name=="History").onClick.Invoke();await Frames();
                    view=UnityEngine.Object.FindFirstObjectByType<HistoryReplayView>();
                    Check(view.GetComponentsInChildren<Text>().Single(t=>t.name=="Message").text==DominoLocalization.Get(scenario=="empty"?"history.empty":"history.error"),"HISTORY_"+scenario);
                    view.GetComponentsInChildren<Button>().Single(b=>b.name=="Back").onClick.Invoke();await Frames();
                }
                Check(ValidationNetworkPolicy.NetworkEntrypoints==0,"NO_REAL_NETWORK");Finish(true,"CHECKS="+checks+" SIZES=9 LANGUAGES=en,es MODES=2 CONSOLE_ERRORS=0 REAL_BACKEND_CALLS=0 LOOPBACK_HTTP="+(Environment.GetEnvironmentVariable("DOMINO_I4_LOOPBACK")=="true")+" READ_ONLY_REQUESTS="+api.Requests);
            }catch(Exception e){Finish(false,e.ToString());}
        }
        static void Finish(bool pass,string detail){if(!SessionState.GetBool(Key,false))return;SessionState.SetBool(Key,false);File.WriteAllText(Path.Combine(Output,"unity-result.txt"),(pass?"PASS":"FAIL")+"\n"+detail);EditorApplication.Exit(pass?0:1);}
    }
}
#endif
