#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Domino.Client;
using Domino.Development;
using Domino.Infrastructure;
using Domino.Realtime;
using Domino.UI;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif
namespace Domino.Online.Development
{
    // Explicit development validation only. Files coordinate test timing, never choose opponents or UIDs.
    public static class MatchmakingNetworkValidation
    {
        static string role,dir;static int checks,found;static bool done;
        static void Check(bool v,string key){checks++;if(!v)throw new Exception(key);}
        static string FilePath(string file)=>Path.Combine(dir,file);
        static void Mark(string file,string value="PASS")=>File.WriteAllText(FilePath(file),value);
        static async Task Until(Func<bool> condition,string label,int seconds=120){
            var end=DateTime.UtcNow.AddSeconds(seconds);
            while(!condition()&&DateTime.UtcNow<end)await Task.Delay(100);
            Check(condition(),"TIMEOUT_"+label);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Start(){
            bool restart=false;
#if UNITY_EDITOR
            restart=SessionState.GetBool("I3.Restart",false);SessionState.SetBool("I3.Restart",false);
            if(!restart&&!SessionState.GetBool("I3.Validate",false))return;SessionState.SetBool("I3.Validate",false);role=restart?"R":"A";
#else
            if(!Debug.isDebugBuild||!Environment.GetCommandLineArgs().Contains("--i3-client-b"))return;role="B";
#endif
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-i3Evidence");if(i<0)return;
            dir=args[i+1];Directory.CreateDirectory(dir);Application.runInBackground=true;Application.logMessageReceived+=Log;
            if(restart)_=RestartProbe();else _=Run();
        }
        static async Task RestartProbe(){
            try {
                await Until(()=>ApplicationServices.Player?.CanEdit==true,"BOOTSTRAP");
                var entry=await Open();await Until(()=>entry.Match,"EXISTING_ASSIGNMENT");
                var client=entry.Match.Client;var id=client.Snapshot.MatchId;var rules=client.Snapshot.Rules;long sequence=client.Snapshot.Sequence;
                Mark("R-restart-ready.txt",id);
                await Until(()=>File.Exists(FilePath("redis-restarted.txt")),"REDIS_RESTART",180);
                await Until(()=>ApplicationServices.Realtime.State==RealtimeConnectionState.CONNECTED&&!client.NeedsResync,"RECONNECTED",120);
                await client.ResyncAsync();
                Check(client.Snapshot.MatchId==id&&client.Snapshot.Sequence>=sequence,"MATCH_SURVIVED_REDIS");
                Check(Newtonsoft.Json.Linq.JToken.DeepEquals(rules,client.Snapshot.Rules),"IMMUTABLE_RULE_SNAPSHOT");
                Check(client.Snapshot.Public["hands"]==null&&client.Snapshot.Hand.Count<=10,"PRIVATE_HANDS");
                entry.Close();await Task.Delay(300);Check(!File.Exists(FilePath("R-console-error.txt")),"CONSOLE_ZERO");
                Mark("R-result.txt","PASS\nREDIS_RESTART_MATCH_INTACT=PASS\nMATCH_ID="+id+"\nCHECKS="+checks+"\nCONSOLE_ERRORS=0");Exit(0);
            }catch(Exception e){Mark("R-result.txt","FAIL "+e.GetType().Name+" "+e.Message);Exit(1);}
        }
        static void Log(string m,string s,LogType t){if(t==LogType.Error||t==LogType.Exception||t==LogType.Assert)Mark(role+"-console-error.txt",m);}
        static async Task<MatchmakingView> Open(){
            var c=UnityEngine.Object.FindFirstObjectByType<DominoClientController>();c.Menu.Show(StartScreen.ModeSelector);
            await Until(()=>c.Menu.DuelPlay&&c.Menu.DuelPlay.interactable,"DUEL_ENABLED");c.Menu.DuelPlay.onClick.Invoke();
            var v=UnityEngine.Object.FindFirstObjectByType<MatchmakingView>();Check(v,"NORMAL_MATCHMAKING_VIEW");
            await Until(()=>v.Client.MatchId!=null||v.GetComponentsInChildren<Button>().Any(b=>b.name=="Search"&&b.interactable),"RECOVERY_COMPLETE");
            if(v.Client.MatchId==null){Check(v.Client.State==MatchmakingState.IDLE,"EXPLICIT_SEARCH_REQUIRED");v.GetComponentsInChildren<Button>().Single(b=>b.name=="Search").onClick.Invoke();}
            return v;
        }
        static async Task Cancel(MatchmakingView view){
            view.GetComponentsInChildren<Button>().Single(b=>b.name=="Cancel").onClick.Invoke();
            await Until(()=>!view,"CANCEL_CONFIRMED");
        }
        static bool Ready(OnlineMatchController v)=>v&&v.Board&&!(bool)typeof(OnlineMatchController).GetField("rendering",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(v)&&!v.Client.Pending&&!v.Client.NeedsResync;
        static async Task Run(){
            try {
                await Until(()=>ApplicationServices.Player?.CanEdit==true&&DevelopmentAuthentication.CanReset,"SERVICES");
                if(role=="B")await Until(()=>File.Exists(FilePath("A-ready.txt")),"A_READY");
                var active=await ApplicationServices.OnlineApi.SendAsync("GET","matches/active",null,CancellationToken.None);
                // Both opted-in test clients need fresh identities: another open Editor may keep
                // the persisted UID online, which correctly prevents last-connection cleanup.
                if(role=="A"||role=="B") {
                    await DevelopmentAuthentication.ResetAnonymousIdentityAsync();
                    await Until(()=>ApplicationServices.Player?.CanEdit==true&&DevelopmentAuthentication.CanReset,"NEW_SESSION");
                }
                await ApplicationServices.Player.UpdateDisplayNameAsync("FHO-"+role);
                var uid=ApplicationServices.Identity.Current.Uid;
                Mark(role+"-identity.txt",DevelopmentAuthentication.Fingerprint(uid));
                ((IRealtimeMatchChannel)ApplicationServices.Realtime).MatchMessage+=(type,payload)=>{if(type=="MATCH_FOUND")found++;};
                Mark(role+"-ready.txt");
                MatchmakingView entry;
                if(role=="A") {
                    entry=await Open();await Until(()=>entry.Client.State==MatchmakingState.SEARCHING,"SOLO_SEARCH");
                    await Task.Delay(1500);ScreenCapture.CaptureScreenshot(FilePath("A-search.png"));await Task.Delay(300);
                    await Cancel(entry);Mark("A-cancelled.txt");
                    await Until(()=>File.Exists(FilePath("B-cancelled.txt")),"B_CANCEL");
                    entry=await Open();await Until(()=>entry.Client.State==MatchmakingState.SEARCHING,"SEARCH_BEFORE_DISCONNECT");
                    ApplicationServices.Realtime.SetBackground(true);
                    await Until(()=>ApplicationServices.Realtime.State!=RealtimeConnectionState.CONNECTED,"DISCONNECTED");
                    await Task.Delay(2000);Mark("A-disconnected.txt");
                    await Until(()=>File.Exists(FilePath("B-disconnect-check.txt")),"STALE_QUEUE_REMOVED");
                    ApplicationServices.Realtime.SetBackground(false);
                    await Until(()=>ApplicationServices.Realtime.State==RealtimeConnectionState.CONNECTED&&entry.Client.State==MatchmakingState.IDLE,"RECOVER_IDLE");
                    Check(ApplicationServices.Identity.Current.Uid==uid,"SAME_UID_RECONNECT");
                    await entry.Client.JoinAsync();Mark("A-pair-ready.txt");
                    await Until(()=>File.Exists(FilePath("B-race-join.txt")),"CANCEL_RACE_START");
                    var raceClient=entry.Client;
                    entry.GetComponentsInChildren<Button>(true).Single(b=>b.name=="Cancel").onClick.Invoke();
                    await Until(()=>raceClient.State==MatchmakingState.IDLE||raceClient.MatchId!=null,"CANCEL_RACE_RESOLVED");
                    Check(!(raceClient.State==MatchmakingState.IDLE&&raceClient.MatchId!=null),"CANCEL_RACE_SINGLE_OUTCOME");
                    if(raceClient.MatchId==null){Mark("cancel-race.txt","CANCELLED");await Until(()=>!entry,"RACE_CANCEL_CLOSED");entry=await Open();}
                    else Mark("cancel-race.txt","MATCH_FOUND");
                } else {
                    await Until(()=>File.Exists(FilePath("A-cancelled.txt")),"A_CANCEL");
                    entry=await Open();await Until(()=>entry.Client.State==MatchmakingState.SEARCHING,"B_ALONE");await Task.Delay(3000);
                    Check(entry.Client.State==MatchmakingState.SEARCHING,"CANCELLED_A_NOT_PAIRED");await Cancel(entry);Mark("B-cancelled.txt");
                    await Until(()=>File.Exists(FilePath("A-disconnected.txt")),"A_DISCONNECT");
                    entry=await Open();await Until(()=>entry.Client.State==MatchmakingState.SEARCHING,"B_AFTER_DISCONNECT");await Task.Delay(3000);
                    Check(entry.Client.State==MatchmakingState.SEARCHING,"DISCONNECTED_A_NOT_PAIRED");await Cancel(entry);Mark("B-disconnect-check.txt");
                    await Until(()=>File.Exists(FilePath("A-pair-ready.txt")),"A_PAIR_READY");entry=await Open();Mark("B-race-join.txt");
                }
                await Until(()=>entry.Match,"AUTO_MATCH_FOUND");var view=entry.Match;var client=view.Client;
                Check(found>0,"MATCH_FOUND_WS_DELIVERED");Mark(role+"-match.txt",client.Snapshot.MatchId);Mark(role+"-seat.txt",client.Snapshot.Seat.ToString());
                await Until(()=>File.Exists(FilePath((role=="A"?"B":"A")+"-match.txt")),"BOTH_ASSIGNED");
                Check(File.ReadAllText(FilePath("A-match.txt"))==File.ReadAllText(FilePath("B-match.txt")),"SAME_MATCH");
                Check(File.ReadAllText(FilePath("A-seat.txt"))!=File.ReadAllText(FilePath("B-seat.txt")),"DISTINCT_SEATS");
                Check(File.ReadAllText(FilePath("A-identity.txt"))!=File.ReadAllText(FilePath("B-identity.txt")),"DISTINCT_UIDS");
                var recovery=await ApplicationServices.OnlineApi.SendAsync("GET","matches/active",null,CancellationToken.None);
                Check((string)recovery["match"]["matchId"]==client.Snapshot.MatchId,"ACTIVE_ASSIGNMENT_RECOVERY");
                while(client.Snapshot.Phase=="STARTER_SELECTION") {
                    await Until(()=>Ready(view),"STARTER_READY");
                    if((string)client.Snapshot.Starter?["method"]=="HIGH_TILE_SELECTION") {
                        var choice=view.Board.StarterView?.Choices.FirstOrDefault(b=>b.interactable);if(choice)choice.onClick.Invoke();
                    } else {var button=(Button)typeof(OnlineMatchController).GetField("first",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(view);if(button&&button.interactable)button.onClick.Invoke();}
                    await Task.Delay(400);
                }
                await Until(()=>Ready(view),"DEAL_COMPLETE");
                Check(client.Snapshot.Hand.Count==10,"TEN_OWN_TILES");
                Check(view.Board.HandViews(client.Snapshot.Seat).All(t=>t.IsFaceUp)&&view.Board.HandViews(1-client.Snapshot.Seat).All(t=>!t.IsFaceUp),"PRIVATE_HANDS");
                long before=client.Snapshot.Sequence;var limit=DateTime.UtcNow.AddSeconds(70);
                while(client.Snapshot.Sequence<before+4&&DateTime.UtcNow<limit) {
                    if(Ready(view)&&(int?)client.Snapshot.Public["currentSeat"]==client.Snapshot.Seat) {
                        var tile=view.Board.LocalTiles.FirstOrDefault(t=>view.Board.CanPlace(t.Tile,Domino.Core.ChainEnd.Left)||view.Board.CanPlace(t.Tile,Domino.Core.ChainEnd.Right));
                        if(tile){tile.OnPointerClick(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){eligibleForClick=true});}
                        var play=view.Board.GetComponentsInChildren<Button>().Single(b=>b.name=="Play");if(play.interactable)play.onClick.Invoke();
                    }await Task.Delay(500);
                }
                Check(client.Snapshot.Sequence>=before+4,"AUTHORITATIVE_GAMEPLAY");
                Check(client.TurnClock.HasDeadline,"I2_TIMER_REUSED");ScreenCapture.CaptureScreenshot(FilePath(role+"-playing.png"));await Task.Delay(800);
                Mark(role+"-played.txt");await Until(()=>File.Exists(FilePath((role=="A"?"B":"A")+"-played.txt")),"BOTH_PLAYED");
                entry.Close();await Task.Delay(300);
                Check(!File.Exists(FilePath(role+"-console-error.txt")),"CONSOLE_ZERO");
                Mark(role+"-result.txt","PASS\nCHECKS="+checks+"\nMATCH_FOUND="+found+"\nUID_HASH="+DevelopmentAuthentication.Fingerprint(uid)+"\nCONSOLE_ERRORS=0");Exit(0);
            }catch(Exception e){Mark(role+"-result.txt","FAIL "+e.GetType().Name+" "+e.Message);Exit(1);}
        }
        static void Exit(int code){if(done)return;done=true;
            var entry=UnityEngine.Object.FindFirstObjectByType<MatchmakingView>(FindObjectsInactive.Include);if(entry)entry.Close();
#if UNITY_EDITOR
            EditorApplication.Exit(code);
#else
            Application.Quit(code);
#endif
        }
    }
}
#endif
