#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Domino.Identity;
using Domino.Ads;
using Domino.Infrastructure;
using Domino.Infrastructure.Api;
using Domino.Realtime;
using Domino.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Domino.Online.Editor
{
    // Explicit isolated Editor test driver, not product autoplay. Uses the native Firebase SDK,
    // production API/socket/protocol/client/controller, and an independent authenticated JVM peer.
    public static class OnlineTurnNetworkValidation
    {
        const string Key="Domino.I2.NetworkValidation";
        static bool running;static double deadline;static int number,checks,updates,plays,passes;
        static OnlineMatchClient client;static OnlineMatchApi api;static RealtimeConnectionService realtime;
        static OnlineMatchController controller;static long wsSequence;static string rejection;
        static string Dir=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../I2"));
        sealed class NativeTokens:IAuthTokenProvider {
            public Task<string> GetIdTokenAsync(bool refresh,CancellationToken cancel) => global::Firebase.Auth.FirebaseAuth.DefaultInstance.CurrentUser.TokenAsync(refresh);
        }
        public static void Run() {
            if(!Application.isBatchMode||!Application.dataPath.Replace('\\','/').Contains("/Validation/Generated/"))throw new Exception("ISOLATED_BATCH_REQUIRED");
            SessionState.SetBool(Key,true);Register();EditorSceneManager.OpenScene(Domino.Editor.ClientEditorTools.ScenePath);EditorApplication.isPlaying=true;
        }
        [InitializeOnLoadMethod] static void Register() {
            if(!SessionState.GetBool(Key,false))return;
            deadline=EditorApplication.timeSinceStartup+900;Application.logMessageReceived+=Log;
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
        }
        static void Tick() {
            if(!SessionState.GetBool(Key,false))return;
            if(EditorApplication.timeSinceStartup>deadline){Finish(false,"TIMEOUT");return;}
            if(!running&&EditorApplication.isPlaying&&DominoLocalization.Ready&&ApplicationServices.Identity?.Current!=null){running=true;_=Validate();}
        }
        static void Log(string message,string stack,LogType type) {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)Finish(false,"UNITY_CONSOLE_ERROR: "+message);}
        static void Check(bool value,string label) {checks++;if(!value)throw new Exception(label);}
        static async Task Until(Func<bool> predicate,string label,int seconds=35) {
            var end=EditorApplication.timeSinceStartup+seconds;
            while(!predicate()&&EditorApplication.timeSinceStartup<end)await Task.Delay(60);
            Check(predicate(),label);
        }
        static async Task Frames() {int target=Time.frameCount+5;await Until(()=>Time.frameCount>=target,"RENDERED_FRAMES");}
        static async Task<JObject> Peer(string operation) {
            int n=++number;
            var request=new JObject {["number"]=n,["operation"]=operation,["matchId"]=client.Snapshot.MatchId};
            File.WriteAllText(Path.Combine(Dir,"request.tmp"),request.ToString(Formatting.None));
            string path=Path.Combine(Dir,"request.json");
            if(File.Exists(path))File.Replace(Path.Combine(Dir,"request.tmp"),path,null);else File.Move(Path.Combine(Dir,"request.tmp"),path);
            JObject result=null;
            await Until(()=>{try {var f=Path.Combine(Dir,"response.json");if(!File.Exists(f))return false;result=JObject.Parse(File.ReadAllText(f));return (int)result["number"]==n;}catch(IOException){return false;}},"JVM_"+operation,110);
            return result;
        }
        static Button Button(string name)=>(Button)typeof(OnlineMatchController).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(controller);
        static async Task ViewReady() {
            await Until(()=>controller.Board&&!controller.Board.IsPreparingRound&&!(bool)typeof(OnlineMatchController).GetField("rendering",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(controller),"VIEW_READY");
            await Frames();
        }
        static async Task Compare() {
            var peer=await Peer("inspect");long sequence=(long)peer["sequence"];
            await Until(()=>client.Snapshot.Sequence==sequence&&!client.Pending,"UNITY_SEQUENCE");
            var server=await api.SendAsync("GET","matches/"+client.Snapshot.MatchId+"/snapshot",null,CancellationToken.None);
            Check((long)server["lastSequence"]==sequence&&wsSequence==sequence&&(long)peer["wsSequence"]==sequence,"THREE_WAY_SEQUENCE");
            Check(JToken.DeepEquals(client.Snapshot.Public,server["publicState"])&&JToken.DeepEquals(server["publicState"],peer["publicState"]),"IDENTICAL_PUBLIC_BOARD_SCORE");
            var peerResult=peer["roundResult"]?.Type==JTokenType.Null?null:peer["roundResult"];
            if(client.Snapshot.RoundResult!=null)File.WriteAllText(Path.Combine(Dir,"public-round-comparison.json"),new JObject {["unity"]=client.Snapshot.RoundResult,["server"]=server["roundResult"].DeepClone(),["jvm"]=peerResult?.DeepClone()}.ToString());
            Check(JToken.DeepEquals(client.Snapshot.RoundResult,peerResult),"IDENTICAL_RESULT");
            Check(server["publicState"]["hands"]==null&&server["privateState"]["hands"]==null&&(int)server["privateState"]["seat"]==0,"PRIVATE_DTO");
            await ViewReady();
            if(client.Snapshot.Phase=="PLAYING") {
                Check(controller.Board.HandViews(0).All(t=>t.IsFaceUp),"OWN_HAND_VISIBLE");
                Check(controller.Board.HandViews(1).All(t=>!t.IsFaceUp),"OPPONENT_HIDDEN");
                Check(controller.Board.PlayedCount==((JArray)client.Snapshot.Public["board"]).Count,"RENDERED_BOARD");
            }
            File.AppendAllText(Path.Combine(Dir,"sequence-evidence.txt"),"SERVER=UNITY=JVM="+sequence+" WS=PASS PHASE="+client.Snapshot.Phase+"\n");
        }
        static int timeoutEvents,automaticEvents,disconnectEvents,reconnectEvents;
        static async Task Verify() {
            await Until(()=>ConnectedAndReady(),"CONNECTED_RESYNC",70);
            var peer=await Peer("inspect");
            await Until(()=>client.Snapshot.Sequence>=(long)peer["sequence"],"SEQUENCE_RESTORED");
            var server=await api.SendAsync("GET","matches/"+client.Snapshot.MatchId+"/snapshot",null,CancellationToken.None);
            if(client.Snapshot.Sequence!=(long)server["lastSequence"]){await client.ResyncAsync();server=await api.SendAsync("GET","matches/"+client.Snapshot.MatchId+"/snapshot",null,CancellationToken.None);}
            Check(client.Snapshot.Sequence==(long)server["lastSequence"],"SERVER_UNITY_SEQUENCE");
            Check(JToken.DeepEquals(client.Snapshot.Public["board"],server["publicState"]["board"]),"BOARD_CURRENT");
            Check(JToken.DeepEquals(client.Snapshot.Hand,server["privateState"]["hand"]),"OWN_HAND_RESYNC");
            await ViewReady();Check(controller.Board.HandViews(1).All(t=>!t.IsFaceUp),"OPPONENT_HIDDEN");
            Check(client.TurnClock.HasDeadline,"COUNTDOWN_PRESENT");
        }
        static bool ConnectedAndReady()=>realtime.State==RealtimeConnectionState.CONNECTED&&!client.NeedsResync;
        static async Task Validate() {
            try {
                Application.runInBackground=true;Time.timeScale=2;
                Check(!Resources.Load<DominoAdsSettings>("AdsSettings").Configuration.Enabled,"ISOLATED_ADS_DISABLED");
                ApplicationServices.Realtime.Dispose();
                var config=new DominoApiConfiguration(true,"http://127.0.0.1:18083",25,"LOCAL",true);
                var tokens=new NativeTokens();
                realtime=new RealtimeConnectionService(new RealtimeConfiguration(config),ApplicationServices.Identity,tokens);
                typeof(ApplicationServices).GetProperty("Realtime").SetValue(null,realtime);
                api=new OnlineMatchApi(config,tokens,new UnityApiTransport());
                realtime.MatchMessage+=(type,payload)=>{if(type=="MATCH_UPDATE") {
                    wsSequence=(long)payload["snapshot"]["lastSequence"];updates++;
                    Check((int)payload["snapshot"]["privateState"]["seat"]==0,"WS_PRIVATE_SEAT");
                }};
                realtime.Start();await Until(()=>realtime.State==RealtimeConnectionState.CONNECTED,"REAL_CONNECTED");
                client=new OnlineMatchClient(api,realtime);
                client.EventApplied+=type=>{if(type=="TURN_TIMEOUT")timeoutEvents++;if(type=="AUTO_PLAYED")automaticEvents++;if(type=="PLAYER_DISCONNECTED")disconnectEvents++;if(type=="PLAYER_RECONNECTED")reconnectEvents++;};
                await client.CreateAsync();File.WriteAllText(Path.Combine(Dir,"match-id.txt"),client.Snapshot.MatchId);
                controller=new GameObject("I2 real Unity participant").AddComponent<OnlineMatchController>();
                controller.Initialize(client,AssetDatabase.LoadAssetAtPath<DominoTileView>("Assets/_Domino/Prefabs/DominoTile.prefab"),AssetDatabase.LoadAssetAtPath<PlayerView>("Assets/_Domino/Prefabs/Player.prefab"));
                typeof(Domino.Editor.Phase1Validation).GetMethod("ResizeGameView",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{1080,1920});
                await Peer("join");await Until(()=>client.Snapshot.Phase=="STARTER_SELECTION","JOINED");
                while(client.Snapshot.Phase=="STARTER_SELECTION") {
                    await ViewReady();var st=client.Snapshot.Starter;long seq=client.Snapshot.Sequence;
                    bool guess=(string)st["method"]=="EVEN_ODD_GUESS";int seat=guess?(int)st["guessingSeat"]:st["selectedSeats"].Values<int>().Contains(0)?1:0;
                    if(seat==1)await Peer("step");else {int index=guess?0:st["availableCandidates"].Values<int>().First();Button(index==0?"first":"second").onClick.Invoke();}
                    await Until(()=>client.Snapshot.Sequence>seq&&!client.Pending,"STARTER_ADVANCED");
                }
                await Verify();Check(client.TurnClock.RemainingSeconds>40&&client.TurnClock.RemainingSeconds<=60,"CANONICAL_60S_COUNTDOWN");
                ScreenCapture.CaptureScreenshot(Path.Combine(Dir,"countdown.png"));await Frames();
                File.WriteAllText(Path.Combine(Dir,"stage.txt"),"WAITING_CONNECTED_IDLE_TIMEOUT");
                await Until(()=>timeoutEvents>=1&&automaticEvents>=1,"REAL_CONNECTED_IDLE_TIMEOUT",80);await Verify();
                File.WriteAllText(Path.Combine(Dir,"stage.txt"),"DISCONNECTING_CURRENT_SEAT");
                int disconnectedSeat=(int)client.Snapshot.Public["currentSeat"];
                string oldDeadline=(string)client.Snapshot.Public["turnDeadline"];
                if(disconnectedSeat==0)realtime.SetBackground(true);else await Peer("disconnect");
                await Until(()=>realtime.State!=RealtimeConnectionState.CONNECTED||disconnectedSeat==1,"DISCONNECTED_TRANSPORT");
                await Task.Delay(6500);
                var disconnected=await api.SendAsync("GET","matches/"+client.Snapshot.MatchId+"/snapshot",null,CancellationToken.None);
                Check((string)disconnected["publicState"]["participants"][disconnectedSeat]["connectionState"]=="DISCONNECTED","PERSISTED_DISCONNECTED");
                Check(DateTimeOffset.Parse((string)disconnected["turnDeadlineAt"])==DateTimeOffset.Parse(oldDeadline),"DISCONNECT_DEADLINE_UNCHANGED");
                // The companion inspects REST while disconnected; no gameplay polling is introduced.
                await Until(()=>DateTimeOffset.UtcNow>=DateTimeOffset.Parse(oldDeadline).AddSeconds(7),"REAL_DISCONNECTED_TIMEOUT_WAIT",80);
                var after=await api.SendAsync("GET","matches/"+client.Snapshot.MatchId+"/snapshot",null,CancellationToken.None);
                Check((int)after["publicState"]["currentTurn"]>(int)disconnected["publicState"]["currentTurn"],"DISCONNECTED_AUTOPLAY_ADVANCED");
                if(disconnectedSeat==0)realtime.SetBackground(false);else await Peer("reconnect");
                await Until(()=>realtime.State==RealtimeConnectionState.CONNECTED,"SAME_UID_RECONNECTED",70);
                await Task.Delay(6500);await Verify();
                Check((string)client.Snapshot.Public["participants"][disconnectedSeat]["connectionState"]=="CONNECTED","RECONNECTED_STATE");
                File.WriteAllText(Path.Combine(Dir,"stage.txt"),"RESTART_OVERDUE_65_SECONDS");
                int priorTurn=(int)client.Snapshot.Public["currentTurn"];
                await Peer("restart");await Until(()=>ConnectedAndReady(),"BACKEND_RESTART_RESYNC",80);
                await Task.Delay(6500);await Verify();
                Check((int)client.Snapshot.Public["currentTurn"]>priorTurn,"OVERDUE_RECOVERY");
                File.WriteAllText(Path.Combine(Dir,"stage.txt"),"REDIS_OFF_REQUESTED");
                await Until(()=>File.Exists(Path.Combine(Dir,"redis-off.txt")),"REDIS_OFF_ORCHESTRATED",90);
                var redisOff=await api.SendAsync("GET","matches/"+client.Snapshot.MatchId+"/snapshot",null,CancellationToken.None);
                Check(redisOff["publicState"]["matchId"].Value<string>()==client.Snapshot.MatchId,"REST_WHILE_REDIS_OFF");
                File.WriteAllText(Path.Combine(Dir,"stage.txt"),"REDIS_RESTORE_REQUESTED");
                await Until(()=>File.Exists(Path.Combine(Dir,"redis-restored.txt")),"REDIS_RESTORED",90);
                await Peer("reconnect");await Until(()=>ConnectedAndReady(),"REDIS_RECOVERY",80);await Task.Delay(6500);await Verify();
                foreach(string language in new[]{"en","es"}) {DominoLocalization.Select(language);await Frames();Check(DominoLocalization.Get("online.turn_seconds",15).Contains("15"),"COUNTDOWN_LOCALIZED");ScreenCapture.CaptureScreenshot(Path.Combine(Dir,"status-"+language+".png"));await Frames();}
                await Peer("finish");await Until(()=>File.Exists(Path.Combine(Dir,"firestore.json")),"FIRESTORE_I2");
                Finish(true,"REAL_UNITY=YES\nREAL_TIMEOUT=PASS\nREAL_DISCONNECT=PASS\nREAL_RECONNECT=PASS\nRESTART_OVERDUE_RECOVERY=PASS\nREDIS_OFF_REST=PASS\nREDIS_RESTORE=PASS\nCANONICAL_TURN_SECONDS=60\nMATCH_ID="+client.Snapshot.MatchId+"\nCHECKS="+checks+"\nCONSOLE_ERRORS=0");
            } catch(Exception e){Finish(false,e.ToString());}
        }
        static void Finish(bool success,string detail) {
            if(!SessionState.GetBool(Key,false))return;SessionState.SetBool(Key,false);
            client?.Dispose();realtime?.Dispose();Time.timeScale=1;
            File.WriteAllText(Path.Combine(Dir,"unity-result.txt"),(success?"PASS\n":"FAIL\n")+detail);
            EditorApplication.Exit(success?0:1);
        }
    }
}
#endif
