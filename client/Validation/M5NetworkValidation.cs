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
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif
namespace Domino.Online.Development
{
    // Isolated validation overlay only; never copied into the product's Assets.
    public static class M5NetworkValidation
    {
        static string role,dir;static int checks,found;static bool done;
        static string FilePath(string name)=>Path.Combine(dir,name);
        static void Mark(string name,string value="PASS")=>File.WriteAllText(FilePath(role+"-"+name+".txt"),value);
        static bool All(string marker)=>new[]{"A","B","C","D"}.All(r=>File.Exists(FilePath(r+"-"+marker+".txt")));
        static void Check(bool b,string key){checks++;if(!b)throw new Exception(key);}
        static async Task Until(Func<bool> f,string key,int seconds=180){var end=DateTime.UtcNow.AddSeconds(seconds);while(!f()&&DateTime.UtcNow<end)await Task.Delay(100);Check(f(),"TIMEOUT_"+key);}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Start(){
            var args=Environment.GetCommandLineArgs();int r=Array.IndexOf(args,"-m5Role"),d=Array.IndexOf(args,"-m5Evidence");
            if(r<0||d<0||!Application.isEditor&&!Debug.isDebugBuild)return;
            role=args[r+1];dir=args[d+1];if(!new[]{"A","B","C","D"}.Contains(role))return;
            ValidationNetworkPolicy.RequireRealOptIn();
            Directory.CreateDirectory(dir);Application.runInBackground=true;Application.logMessageReceived+=Log;_=Run();
        }
        static void Log(string m,string s,LogType t){if(t==LogType.Error||t==LogType.Exception||t==LogType.Assert)Mark("console-error",m);}
        static bool Ready(OnlineMatchController v)=>v&&v.Board&&!(bool)typeof(OnlineMatchController).GetField("rendering",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(v)&&!v.Client.Pending&&!v.Client.NeedsResync;
        static async Task Run(){try{
            await Until(()=>ApplicationServices.Player?.CanEdit==true&&DevelopmentAuthentication.CanReset,"SERVICES");
            int index=role[0]-'A';if(index>0)await Until(()=>File.Exists(FilePath(((char)(role[0]-1))+"-queued.txt")),"PREVIOUS_QUEUED",300);
            await DevelopmentAuthentication.ResetAnonymousIdentityAsync();await Until(()=>ApplicationServices.Player?.CanEdit==true&&DevelopmentAuthentication.CanReset,"NEW_IDENTITY");
            await ApplicationServices.Player.UpdateDisplayNameAsync("FHO-M5-"+role);
            var uid=ApplicationServices.Identity.Current.Uid;Mark("identity",DevelopmentAuthentication.Fingerprint(uid));
            await Until(()=>ApplicationServices.Realtime.State==RealtimeConnectionState.CONNECTED,"REALTIME");
            ((IRealtimeMatchChannel)ApplicationServices.Realtime).MatchMessage+=(type,payload)=>{if(type=="MATCH_FOUND")found++;};
            var cat=ApplicationServices.GameCatalog;
            var api=(Domino.Catalog.IGameCatalogApi)typeof(Domino.Catalog.GameCatalogService).GetField("api",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(cat);
            var raw=JObject.Parse(await api.FetchAsync(CancellationToken.None));await cat.RefreshAsync(true);
            Mark("catalog","API_VERSION="+raw["catalogVersion"]+"\nRAW_KEYS="+string.Join(",",raw["modes"].Select(m=>(string)m["key"]))+"\nVALIDATED_KEYS="+string.Join(",",cat.Current.Modes.Select(m=>m.Key)));
            Check(cat.Current.Modes.Any(m=>m.Key=="PARTNERS_2V2_ONLINE"),"REMOTE_MODE_AVAILABLE");
            var menu=UnityEngine.Object.FindFirstObjectByType<DominoClientController>().Menu;menu.Show(StartScreen.ModeSelector);
            await Until(()=>menu.ButtonFor("PARTNERS_2V2_ONLINE"),"THIRD_CARD");menu.ModeScroll.verticalNormalizedPosition=0;
            DominoLocalization.Select(index%2==0?"es":"en");await Task.Delay(500);ScreenCapture.CaptureScreenshot(FilePath(role+"-selector.png"));await Task.Delay(200);
            Check(menu.VisibleModeKeys.Count==3,"THREE_NORMAL_CARDS");menu.ButtonFor("PARTNERS_2V2_ONLINE").onClick.Invoke();
            var entry=UnityEngine.Object.FindFirstObjectByType<MatchmakingView>();Check(entry,"NORMAL_ENTRY");
            await Until(()=>entry.GetComponentsInChildren<Button>().Any(b=>b.name=="Search"&&b.interactable),"SEARCH_READY");
            Check(entry.Client.State==MatchmakingState.IDLE,"EXPLICIT_SEARCH_REQUIRED");entry.GetComponentsInChildren<Button>().Single(b=>b.name=="Search").onClick.Invoke();
            if(role!="D"){await Until(()=>entry.Client.State==MatchmakingState.SEARCHING,"SEARCHING");await Task.Delay(3000);Check(!entry.Match&&entry.Client.MatchId==null,"LESS_THAN_FOUR_NO_MATCH");}
            Mark("queued");await Until(()=>entry.Match,"FOUR_PLAYER_ASSIGNMENT",300);var view=entry.Match;var client=view.Client;
            Check(client.Snapshot.PlayerCount==4,"FOUR_SEATS");Check((string)client.Snapshot.Public["modeKey"]=="PARTNERS_2V2_ONLINE","MATCH_MODE");
            Mark("match",client.Snapshot.MatchId);Mark("seat",client.Snapshot.Seat.ToString());await Until(()=>All("match"),"ALL_ASSIGNED");
            Check(new[]{"A","B","C","D"}.Select(r=>File.ReadAllText(FilePath(r+"-match.txt"))).Distinct().Count()==1,"SAME_MATCH");
            Check(new[]{"A","B","C","D"}.Select(r=>File.ReadAllText(FilePath(r+"-identity.txt"))).Distinct().Count()==4,"FOUR_DISTINCT_UIDS");
            Check(new[]{"A","B","C","D"}.Select(r=>File.ReadAllText(FilePath(r+"-seat.txt"))).Distinct().Count()==4,"FOUR_DISTINCT_SEATS");
            await Until(()=>Ready(view),"DEAL_COMPLETE");Check(client.Snapshot.Hand.Count==10,"TEN_OWN_TILES");
            for(int p=0;p<4;p++)Check(view.Board.HandViews(p).Count==10&&view.Board.HandViews(p).All(t=>t.IsFaceUp==(p==client.Snapshot.Seat)),"PRIVATE_HAND_"+p);
            Check(view.Board.ReserveViews.Count==15,"RESERVE15");Check(!client.TurnClock.HasDeadline,"UNCHANGED_PARTNERS_NO_TIMER");
            Mark("dealt");await Until(()=>All("dealt"),"ALL_DEALT");var limit=DateTime.UtcNow.AddSeconds(100);
            while(client.Snapshot.Public["board"].Count()<8&&DateTime.UtcNow<limit){
                if(Ready(view)&&(int?)client.Snapshot.Public["currentSeat"]==client.Snapshot.Seat){
                    var tile=view.Board.LocalTiles.FirstOrDefault(t=>view.Board.CanPlace(t.Tile,Domino.Core.ChainEnd.Left)||view.Board.CanPlace(t.Tile,Domino.Core.ChainEnd.Right));
                    if(tile)tile.OnPointerClick(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){eligibleForClick=true});
                    var play=view.Board.GetComponentsInChildren<Button>().Single(b=>b.name=="Play");if(play.interactable)play.onClick.Invoke();
                }await Task.Delay(500);
            }
            Check(client.Snapshot.Public["board"].Count()>=8,"AUTHORITATIVE_GAMEPLAY");await Until(()=>Ready(view),"FINAL_PRESENTATION");
            ScreenCapture.CaptureScreenshot(FilePath(role+"-playing.png"));await Task.Delay(600);Mark("played");await Until(()=>All("played"),"ALL_PLAYED");
            Check(found==1,"SINGLE_MATCH_FOUND");Check(!File.Exists(FilePath(role+"-console-error.txt")),"CONSOLE_ZERO");
            Mark("result","PASS\nCHECKS="+checks+"\nMATCH_FOUND="+found+"\nUID_HASH="+DevelopmentAuthentication.Fingerprint(uid)+"\nCONSOLE_ERRORS=0");Exit(0);
        }catch(Exception e){Mark("result","FAIL "+e.GetType().Name+" "+e.Message);Exit(1);}}
        static void Exit(int code){if(done)return;done=true;var entry=UnityEngine.Object.FindFirstObjectByType<MatchmakingView>(FindObjectsInactive.Include);if(entry)entry.Close();
#if UNITY_EDITOR
            EditorApplication.Exit(code);
#else
            Application.Quit(code);
#endif
        }
    }
}
#endif
