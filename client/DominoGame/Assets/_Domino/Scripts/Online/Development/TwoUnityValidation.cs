#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Domino.Client;
using Domino.Identity;
using Domino.Infrastructure;
using Domino.Infrastructure.Api;
using Domino.Realtime;
using Domino.UI;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif
namespace Domino.Online.Development
{
    // Explicit isolated two-process integration driver. No normal startup, reward or gameplay authority.
    public static class TwoUnityValidation
    {
        static string role,dir;static OnlineEntryView entry;static OnlineMatchController view;static OnlineMatchClient client;
        static RealtimeConnectionService realtime;static OnlineMatchApi api;static Firebase.Auth.FirebaseAuth auth;static int updates,timeouts,autoplays,disconnects,reconnects,plays,checks;static bool finished;
        const BindingFlags Fields=BindingFlags.Instance|BindingFlags.NonPublic;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Start(){
#if UNITY_EDITOR
            if(!SessionState.GetBool("I21.TwoUnity",false))return;SessionState.SetBool("I21.TwoUnity",false);role="A";
#else
            var args=Environment.GetCommandLineArgs();if(!Debug.isDebugBuild||(!args.Contains("--i21-client-b")&&!args.Contains("--i21-visual")))return;role=args.Contains("--i21-visual")?"V":"B";
#endif
            if(role!="V")ValidationNetworkPolicy.RequireRealOptIn();
            dir=Path.GetFullPath(Path.Combine(Application.dataPath,
#if UNITY_EDITOR
            "../../I21"
#else
            "../.."
#endif
            ));Directory.CreateDirectory(dir);Application.runInBackground=true;Application.logMessageReceived+=Log;_=role=="V"?Visual():Validate();
        }
        static async Task Visual(){try{
            await Until(()=>UnityEngine.Object.FindFirstObjectByType<DominoClientController>()?.Menu!=null,"MENU_READY");
            var local=UnityEngine.Object.FindFirstObjectByType<DominoClientController>();local.Menu.Show(StartScreen.ModeSelector);
            await Task.Delay(2000);Canvas.ForceUpdateCanvases();ScreenCapture.CaptureScreenshot(Path.Combine(dir,"windows-selector.png"));await Task.Delay(1000);
            local.OpenDevelopmentOnlineEntry();await Task.Delay(2000);Canvas.ForceUpdateCanvases();ScreenCapture.CaptureScreenshot(Path.Combine(dir,"windows-entry.png"));await Task.Delay(1000);
            Finish(true,"WINDOWS_VISUAL_PROBE_COMPLETED");
        }catch(Exception e){Finish(false,e.ToString());}}
        sealed class Identity:IPlayerIdentityService,IAuthTokenProvider {
            readonly Firebase.Auth.FirebaseAuth sdk;public Identity(Firebase.Auth.FirebaseAuth sdk){this.sdk=sdk;Current=new PlayerIdentity(sdk.CurrentUser.UserId,sdk.CurrentUser.IsAnonymous);}
            public IdentityState State=>IdentityState.Ready;public PlayerIdentity Current{get;}public Exception Error=>null;
            public Task<PlayerIdentity> InitializeAsync()=>Task.FromResult(Current);
            public Task<string> GetIdTokenAsync(bool refresh,CancellationToken t)=>sdk.CurrentUser.TokenAsync(refresh);
        }
        static void Log(string m,string s,LogType t){if(t==LogType.Error||t==LogType.Exception||t==LogType.Assert)Finish(false,"CONSOLE_ERROR="+m);}
        static void Check(bool value,string label){checks++;if(!value)throw new Exception(label);}
        static async Task Until(Func<bool> f,string label,int seconds=60){var end=DateTime.UtcNow.AddSeconds(seconds);while(!f()&&DateTime.UtcNow<end&&!finished)await Task.Delay(100);Check(f(),label);}
        static Button Button(string name)=>(Button)typeof(OnlineMatchController).GetField(name,Fields).GetValue(view);
        static bool Ready()=>view&&view.Board&&!(bool)typeof(OnlineMatchController).GetField("rendering",Fields).GetValue(view)&&!client.Pending&&!client.NeedsResync;
        static void State(){if(client?.Snapshot==null)return;var s=client.Snapshot;File.WriteAllText(Path.Combine(dir,role+"-state.json"),new JObject{["sequence"]=s.Sequence,["public"]=s.Public,["phase"]=s.Phase,["seat"]=s.Seat,["ownHandCount"]=s.Hand.Count}.ToString());}
        static async Task Validate(){try {
            await Until(()=>ApplicationServices.Identity?.Current!=null&&UnityEngine.Object.FindFirstObjectByType<DominoClientController>()?.Menu!=null,"BOOT");
            Time.timeScale=4;
            Check(!Resources.Load<Domino.Ads.DominoAdsSettings>("AdsSettings").Configuration.Enabled,"ADS_DISABLED");
            ApplicationServices.Realtime.Dispose();
            // Named Firebase app isolates the SDK's persisted session from the user's default app.
            var named=Firebase.FirebaseApp.Create(Firebase.FirebaseApp.DefaultInstance.Options,"Domino-I21-"+role);
            auth=Firebase.Auth.FirebaseAuth.GetAuth(named);if(auth.CurrentUser==null)await auth.SignInAnonymouslyAsync();
            var identity=new Identity(auth);var config=new DominoApiConfiguration(true,"http://127.0.0.1:8080",25,"LOCAL",true);
            realtime=new RealtimeConnectionService(new RealtimeConfiguration(config),identity,identity);api=new OnlineMatchApi(config,identity,new UnityApiTransport());
            typeof(ApplicationServices).GetProperty("Realtime").SetValue(null,realtime);typeof(ApplicationServices).GetProperty("OnlineApi").SetValue(null,api);
            var hash=System.Security.Cryptography.SHA256.Create().ComputeHash(System.Text.Encoding.UTF8.GetBytes(identity.Current.Uid));File.WriteAllText(Path.Combine(dir,role+"-identity-hash.txt"),BitConverter.ToString(hash));
            realtime.Start();await Until(()=>realtime.State==RealtimeConnectionState.CONNECTED,"CONNECTED");
            var catalog=new Domino.Catalog.GameCatalogService(new Domino.Catalog.GameCatalogApi(config,identity,new UnityApiTransport()),new Domino.Catalog.FileGameCatalogCache(Path.Combine(dir,role+"-catalog.json")),Resources.Load<TextAsset>("GameCatalogFallback").text);await catalog.RefreshAsync(true);
            typeof(ApplicationServices).GetProperty("GameCatalog").SetValue(null,catalog);Check(catalog.Source==Domino.Catalog.GameCatalogSource.Remote,"REMOTE_CATALOG");
            var local=UnityEngine.Object.FindFirstObjectByType<DominoClientController>();local.Menu.Show(StartScreen.ModeSelector);await Until(()=>local.Menu.DuelPlay&&local.Menu.DuelPlay.gameObject.activeInHierarchy,"NORMAL_DUEL_VISIBLE");
            local.OpenDevelopmentOnlineEntry();entry=UnityEngine.Object.FindFirstObjectByType<OnlineEntryView>();Check(entry,"NORMAL_ONLINE_ENTRY");
            if(role=="A") {entry.CreateButton.onClick.Invoke();await Until(()=>entry.Match,"CREATE");File.WriteAllText(Path.Combine(dir,"match-id.txt"),entry.Match.Client.Snapshot.MatchId);
                try{await api.SendAsync("POST","matches/"+entry.Match.Client.Snapshot.MatchId+"/join",new JObject{["commandId"]=Guid.NewGuid().ToString()},CancellationToken.None);throw new Exception("SAME_UID_ACCEPTED");}catch(OnlineEntryException e){Check(e.LocalizationKey=="online.same_player","SAME_UID_REJECTED");}
            }else {await Until(()=>File.Exists(Path.Combine(dir,"match-id.txt")),"MATCH_ID",180);entry.MatchId.text=File.ReadAllText(Path.Combine(dir,"match-id.txt"));entry.JoinButton.onClick.Invoke();await Until(()=>entry.Match,"JOIN");}
            view=entry.Match;client=view.Client;client.Changed+=State;realtime.MatchMessage+=(t,p)=>{if(t=="MATCH_UPDATE"){updates++;Check((int)p["snapshot"]["privateState"]["seat"]==client.Snapshot.Seat,"PRIVATE_WS_SEAT");}};
            client.EventApplied+=t=>{if(t=="TURN_TIMEOUT")timeouts++;if(t=="AUTO_PLAYED")autoplays++;if(t=="PLAYER_DISCONNECTED")disconnects++;if(t=="PLAYER_RECONNECTED")reconnects++;};
            await Until(()=>File.Exists(Path.Combine(dir,(role=="A"?"B":"A")+"-identity-hash.txt")),"OTHER_IDENTITY");Check(File.ReadAllText(Path.Combine(dir,"A-identity-hash.txt"))!=File.ReadAllText(Path.Combine(dir,"B-identity-hash.txt")),"DIFFERENT_UIDS");
            await Until(()=>client.Snapshot.Phase!="WAITING_FOR_PLAYER","JOINED",180);
            while(client.Snapshot.Phase=="STARTER_SELECTION") {await Until(Ready,"STARTER_READY");var st=client.Snapshot.Starter;if(st==null)break;
                if((string)st["method"]=="HIGH_TILE_SELECTION") {Check(view.Board.StarterView&&view.Board.StarterView.Tiles.All(t=>!t.IsFaceUp),"HIGH_TILE_TABLE_HIDDEN");var b=view.Board.StarterView.Choices.FirstOrDefault(x=>x.interactable);if(b)b.onClick.Invoke();}
                else {var b=Button("first");if(b.interactable)b.onClick.Invoke();}await Task.Delay(400);
            }
            await Until(Ready,"DEAL_READY");Check(client.Snapshot.Hand.Count==10,"TEN_OWN_TILES");Check(view.Board.HandViews(client.Snapshot.Seat).All(t=>t.IsFaceUp)&&view.Board.HandViews(1-client.Snapshot.Seat).All(t=>!t.IsFaceUp),"PRIVATE_UI");
            if((int)client.Snapshot.Public["currentSeat"]!=client.Snapshot.Seat){string rejected=null;client.Rejected+=c=>rejected=c;long before=client.Snapshot.Sequence;await client.SendAsync("PASS");await Until(()=>rejected!=null&&!client.Pending,"INVALID_ACTION_REJECTED");Check(rejected=="NOT_YOUR_TURN"&&client.Snapshot.Sequence==before,"INVALID_ACTION_NO_TRANSITION");File.WriteAllText(Path.Combine(dir,"invalid-action.txt"),"PASS");}
            Check(client.TurnClock.HasDeadline&&client.TurnClock.RemainingSeconds<=60,"SERVER_60S_TIMER");State();ScreenCapture.CaptureScreenshot(Path.Combine(dir,role+"-dealt.png"));
            if(role=="A") {long seq=client.Snapshot.Sequence;string uid=auth.CurrentUser.UserId;realtime.SetBackground(true);await Task.Delay(70000);realtime.SetBackground(false);await Until(()=>realtime.State==RealtimeConnectionState.CONNECTED&&!client.NeedsResync&&client.Snapshot.Sequence>seq,"RECONNECT_CURRENT_STATE",65);Check(auth.CurrentUser.UserId==uid,"SAME_UID_RECONNECT");File.WriteAllText(Path.Combine(dir,"resume.txt"),"ready");}
            else {await Until(()=>File.Exists(Path.Combine(dir,"resume.txt")),"TIMEOUT_AND_RECONNECT",150);Check(timeouts>0&&autoplays>0&&disconnects>0,"REAL_TIMEOUT_AUTOPLAY_DISCONNECT");}
            int rounds=0;var end=DateTime.UtcNow.AddMinutes(10);
            while(client.Snapshot.Phase!="MATCH_FINISHED"&&DateTime.UtcNow<end){if(view.Board.RoundRewardPanel){view.Board.RoundRewardPanel.ContinueButton.onClick.Invoke();await Task.Delay(600);continue;}await Until(()=>Ready()||view.Board.RoundRewardPanel,"TURN_VIEW");if(view.Board.RoundRewardPanel)continue;var s=client.Snapshot;
                if(s.Phase=="ROUND_FINISHED"){rounds++;if(view.Board.RoundRewardPanel)view.Board.RoundRewardPanel.ContinueButton.onClick.Invoke();await Task.Delay(600);continue;}
                if(s.Phase=="PLAYING"&&(int)s.Public["currentSeat"]==s.Seat){var tile=view.Board.LocalTiles.FirstOrDefault(t=>view.Board.CanPlace(t.Tile,Domino.Core.ChainEnd.Left)||view.Board.CanPlace(t.Tile,Domino.Core.ChainEnd.Right));
                    if(tile){tile.OnPointerClick(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){eligibleForClick=true});var b=view.Board.GetComponentsInChildren<UnityEngine.UI.Button>().First(x=>x.name=="Play");b.onClick.Invoke();b.onClick.Invoke();plays++;}else {var action=view.Board.GetComponentsInChildren<UnityEngine.UI.Button>().First(x=>x.name=="Play");if(action.interactable)action.onClick.Invoke();}
                }await Task.Delay(450);
            }
            Check(client.Snapshot.Phase=="MATCH_FINISHED","FULL_MATCH");await Task.Delay(1200);await client.ResyncAsync();State();
            var server=await api.SendAsync("GET","matches/"+client.Snapshot.MatchId+"/snapshot",null,CancellationToken.None);Check(JToken.DeepEquals(server["publicState"],client.Snapshot.Public),"SERVER_STATE_EQUAL");
            var gap=(JObject)server.DeepClone();long cursor=client.Snapshot.Sequence;gap["lastSequence"]=cursor+2;gap["publicState"]["lastSequence"]=cursor+2;gap["privateState"]["lastSequence"]=cursor+2;
            Check(!client.ApplyUpdate(new JObject{["matchId"]=client.Snapshot.MatchId,["firstSequence"]=cursor+2,["snapshot"]=gap,["events"]=new JArray()})&&client.NeedsResync,"GAP_DETECTED");await client.ResyncAsync();Check(!client.NeedsResync&&client.Snapshot.Sequence==cursor,"GAP_RESYNC_NO_REWIND");
            Check(client.Snapshot.Public["hands"]==null&&server["privateState"]["hands"]==null,"NO_HAND_LEAK");
            ScreenCapture.CaptureScreenshot(Path.Combine(dir,role+"-finished.png"));await Task.Delay(1000);
            Finish(true,"MATCH_ID="+client.Snapshot.MatchId+"\nSEQUENCE="+client.Snapshot.Sequence+"\nUPDATES="+updates+"\nPLAYS="+plays+"\nTIMEOUTS="+timeouts+"\nAUTOPLAYS="+autoplays+"\nDISCONNECTS="+disconnects+"\nRECONNECTS="+reconnects+"\nFULL_MATCH=PASS\nCHECKS="+checks);
        }catch(Exception e){Finish(false,e.ToString());}}
        static void Finish(bool success,string detail){if(finished)return;finished=true;File.WriteAllText(Path.Combine(dir,role+"-result.txt"),(success?"PASS\n":"FAIL\n")+detail);client?.Dispose();realtime?.Dispose();
#if UNITY_EDITOR
            EditorApplication.Exit(success?0:1);
#else
            Application.Quit(success?0:1);
#endif
        }
    }
}
#endif
