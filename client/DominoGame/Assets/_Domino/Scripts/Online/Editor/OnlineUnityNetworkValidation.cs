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
    public static class OnlineUnityNetworkValidation
    {
        const string Key="Domino.I11.NetworkValidation";
        static bool running;static double deadline;static int number,checks,updates,plays,passes;
        static OnlineMatchClient client;static OnlineMatchApi api;static RealtimeConnectionService realtime;
        static OnlineMatchController controller;static long wsSequence;static string rejection;
        static string Dir=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../I11"));
        sealed class NativeTokens:IAuthTokenProvider {
            public Task<string> GetIdTokenAsync(bool refresh,CancellationToken cancel) => global::Firebase.Auth.FirebaseAuth.DefaultInstance.CurrentUser.TokenAsync(refresh);
        }
        public static void Run() {
            Domino.Infrastructure.ValidationNetworkPolicy.AuthorizeReal();
            if(!Application.isBatchMode||!Application.dataPath.Replace('\\','/').Contains("/Validation/Generated/"))throw new Exception("ISOLATED_BATCH_REQUIRED");
            SessionState.SetBool(Key,true);Register();EditorSceneManager.OpenScene(Domino.Editor.ClientEditorTools.ScenePath);EditorApplication.isPlaying=true;
        }
        [InitializeOnLoadMethod] static void Register() {
            if(!SessionState.GetBool(Key,false))return;
            deadline=EditorApplication.timeSinceStartup+650;Application.logMessageReceived+=Log;
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
            await Until(()=>{try {var f=Path.Combine(Dir,"response.json");if(!File.Exists(f))return false;result=JObject.Parse(File.ReadAllText(f));return (int)result["number"]==n;}catch(IOException){return false;}},"JVM_"+operation);
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
                realtime.MatchMessage+=(type,payload)=>{
                    if(type=="MATCH_UPDATE") {
                        var s=payload["snapshot"];Check((int)s["privateState"]["seat"]==0,"WS_OWN_SEAT_ONLY");
                        foreach(var e in (JArray)payload["events"]) {
                            var ev=e["event"];if(ev==null||ev.Type==JTokenType.Null)continue;
                            if((string)e["type"]=="HAND_DEALT")Check((int)ev["payload"]["seat"]==0,"WS_PRIVATE_EVENT");
                        }
                        wsSequence=(long)s["lastSequence"];updates++;
                    }
                };
                realtime.Start();await Until(()=>realtime.State==RealtimeConnectionState.CONNECTED,"REAL_CONNECTED");
                client=new OnlineMatchClient(api,realtime);client.Rejected+=code=>rejection=code;
                await client.CreateAsync();Check(client.Snapshot.Seat==0,"MATCH_CREATED");
                File.WriteAllText(Path.Combine(Dir,"match-id.txt"),client.Snapshot.MatchId);
                controller=new GameObject("I11 real Unity participant").AddComponent<OnlineMatchController>();
                controller.Initialize(client,AssetDatabase.LoadAssetAtPath<DominoTileView>("Assets/_Domino/Prefabs/DominoTile.prefab"),AssetDatabase.LoadAssetAtPath<PlayerView>("Assets/_Domino/Prefabs/Player.prefab"));
                typeof(Domino.Editor.Phase1Validation).GetMethod("ResizeGameView",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{1080,1920});
                await Peer("join");await Compare();
                bool invalid=false,starter=false;int moves=0;
                while(client.Snapshot.Phase!="ROUND_FINISHED"&&client.Snapshot.Phase!="MATCH_FINISHED"&&moves++<100) {
                    var s=client.Snapshot;long before=s.Sequence;
                    if(s.Phase=="STARTER_SELECTION") {
                        var st=s.Starter;bool guess=(string)st["method"]=="EVEN_ODD_GUESS";
                        int seat=guess?(int)st["guessingSeat"]:st["selectedSeats"].Values<int>().Contains(0)?1:0;
                        if(seat==1)await Peer("step");
                        else {int index=guess?0:st["availableCandidates"].Values<int>().First();var button=Button(index==0?"first":"second");Check(button.interactable,"STARTER_UI_ENABLED");button.onClick.Invoke();starter=true;}
                    } else {
                        Check(s.Phase=="PLAYING","REAL_PLAYING");
                        int seat=(int)s.Public["currentSeat"];
                        if(seat==1&&!invalid) {
                            rejection=null;await client.SendAsync("PASS");await Until(()=>!client.Pending&&rejection!=null,"INVALID_REPLY");
                            Check(rejection=="NOT_YOUR_TURN","OUT_OF_TURN_REJECTED");await client.ResyncAsync();await Compare();
                            Check(client.Snapshot.Sequence==before,"INVALID_NO_MUTATION");invalid=true;rejection=null;
                        }
                        if(seat==1)await Peer("step");
                        else {
                            var board=(JArray)s.Public["board"];JToken chosen=null;string end=null;
                            foreach(var t in s.Hand){foreach(var e in new[]{"LEFT","RIGHT"}) {
                                int pip=board.Count==0?-1:(int)(e=="LEFT"?board.First["tile"]["sideA"]:board.Last["tile"]["sideB"]);
                                if(pip<0||(int)t["sideA"]==pip||(int)t["sideB"]==pip){chosen=t;end=e;break;}}
                                if(chosen!=null)break;}
                            if(chosen==null){Check(Button("pass").interactable,"PASS_UI_ENABLED");Button("pass").onClick.Invoke();passes++;}
                            else {
                                var tile=controller.Board.LocalTiles.First(t=>t.Tile.SideA==(int)chosen["sideA"]&&t.Tile.SideB==(int)chosen["sideB"]);
                                tile.Clicked(tile);await Frames();Check(Button(end=="LEFT"?"left":"right").interactable,"PLAY_UI_ENABLED");
                                Button(end=="LEFT"?"left":"right").onClick.Invoke();plays++;
                            }
                        }
                    }
                    await Until(()=>client.Snapshot.Sequence>before&&!client.Pending,"AUTHORITATIVE_EVENT");await Compare();
                    if(client.Snapshot.Phase=="PLAYING"&&((JArray)client.Snapshot.Public["board"]).Count>=2&&!File.Exists(Path.Combine(Dir,"unity-playing.png"))){ScreenCapture.CaptureScreenshot(Path.Combine(Dir,"unity-playing.png"));await Frames();}
                }
                Check(invalid&&plays>0&&updates>0,"REPRESENTATIVE_ACTIONS");
                Check(client.Snapshot.RoundResult!=null,"REAL_ROUND_RESULT");
                var result=client.Snapshot.RoundResult;var remaining=(JArray)result["remainingPips"];int winner=(int)result["winnerSeat"];
                string finish=(string)result["finishType"];int opponent=(int)remaining[1-winner];
                int expected=finish=="BLOCKED"?opponent:finish=="CAPICUA"?opponent*2+10:opponent+10;
                Check((int)result["scoreAwarded"]==expected,"AUTHORITATIVE_SCORE_FORMULA");
                long final=client.Snapshot.Sequence;await client.ResyncAsync();Check(!client.NeedsResync&&client.Snapshot.Sequence==final,"EXPLICIT_SNAPSHOT_RESYNC");
                await Compare();ScreenCapture.CaptureScreenshot(Path.Combine(Dir,"unity-round.png"));await Frames();
                await Peer("finish");await Until(()=>File.Exists(Path.Combine(Dir,"firestore.json")),"FIRESTORE_INSPECTED");
                Finish(true,"REAL_UNITY_PARTICIPANT=YES\nTOPOLOGY=UNITY_EDITOR_NATIVE_FIREBASE_PLUS_INDEPENDENT_JVM\nMATCH_ID="+client.Snapshot.MatchId+"\nUNITY_REAL_WEBSOCKET_EVENT=PASS\nUPDATES="+updates+"\nUNITY_PLAY_TILE="+plays+"\nUNITY_PASS="+passes+"\nUNITY_STARTER_INTENT="+(starter?"PASS":"SERVER_SELECTED_JVM_GUESSER_UNITY_RENDERED_RESULT")+"\nINVALID_COMMAND=NOT_YOUR_TURN\nSEQUENCE_SYNC=PASS\nFINAL_SEQUENCE="+final+"\nFINISH_TYPE="+finish+"\nSCORE="+expected+"\nSNAPSHOT_RESYNC=PASS\nCHECKS="+checks+"\nCONSOLE_ERRORS=0");
            } catch(Exception e) {Finish(false,e.ToString());}
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
