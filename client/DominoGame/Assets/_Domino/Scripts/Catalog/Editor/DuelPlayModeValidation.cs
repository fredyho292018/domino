#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Domino.Client;
using Domino.Infrastructure;
using Domino.Infrastructure.Api;
using Domino.Infrastructure.Firebase;
using Domino.Identity;
using Domino.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Domino.Catalog.Editor
{
    public static class DuelPlayModeValidation
    {
        const string Key="Domino.M3.Validation";
        static bool running;
        static int checks;
        static double deadline;
        static string Phase=>SessionState.GetString(Key+".Source","");
        static string CachePath=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../m3-catalog-cache.json"));
        sealed class ExistingGuest:IFirebaseClient,IAuthTokenProvider
        {
            readonly IFirebaseClient sdk=(IFirebaseClient)Activator.CreateInstance(typeof(ApplicationServices).Assembly.GetType("Domino.Infrastructure.Firebase.FirebaseSdkClient",true),new object[]{new Func<PlayerIdentity>(()=>ApplicationServices.Identity?.Current)});
            public Task<string> CheckDependenciesAsync()=>sdk.CheckDependenciesAsync();
            public void InitializeApp()=>sdk.InitializeApp();
            public PlayerIdentity GetCurrentUser()=>sdk.GetCurrentUser()??throw new Exception("EXISTING_IDENTITY_REQUIRED");
            public Task<PlayerIdentity> SignInAnonymouslyAsync()=>throw new Exception("NEW_GUEST_FORBIDDEN");
            public Task<string> GetIdTokenAsync(bool refresh,CancellationToken token)=>((IAuthTokenProvider)sdk).GetIdTokenAsync(refresh,token);
        }
        public static void RunRemote(){SessionState.SetString(Key+".Source","REMOTE");Run();}
        public static void RunCache(){SessionState.SetString(Key+".Source","CACHE");Run();}
        public static void RunBundled(){SessionState.SetString(Key+".Source","BUNDLED_FALLBACK");Run();}
        sealed class Offline:IGameCatalogApi {public Task<string> FetchAsync(CancellationToken t)=>throw new IOException();}
        sealed class EmptyCache:IGameCatalogCache {public CatalogCacheEntry Read()=>null;public void Write(CatalogCacheEntry e){} }
        public static void Run()
        {
            if(!Application.isBatchMode||!Application.dataPath.Replace('\\','/').Contains("/Validation/Generated/"))throw new Exception("ISOLATED_BATCH_REQUIRED");
            SessionState.SetBool(Key,true);Register();
            Domino.Editor.LocalizationAssets.Import();
            EditorSceneManager.OpenScene(Domino.Editor.ClientEditorTools.ScenePath);
            EditorApplication.isPlaying=true;
        }
        [InitializeOnLoadMethod] static void Register()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(Phase!="")ApplicationServices.ValidationFirebaseFactory=()=>new ExistingGuest();
            ApplicationServices.ValidationGameCatalogFactory=tokens=>new GameCatalogService(
                Phase==""?(IGameCatalogApi)new Offline():new GameCatalogApi(new DominoApiConfiguration(true,"http://127.0.0.1:18082",3,"LOCAL",true),tokens,new UnityApiTransport()),
                Phase==""||Phase=="BUNDLED_FALLBACK"?(IGameCatalogCache)new EmptyCache():new FileGameCatalogCache(CachePath),
                Resources.Load<TextAsset>("GameCatalogFallback").text);
            deadline=EditorApplication.timeSinceStartup+480;
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
            Application.logMessageReceived+=OnLog;
        }
        static void OnLog(string m,string s,LogType t) {if(SessionState.GetBool(Key,false)&&(t==LogType.Error||t==LogType.Exception||t==LogType.Assert))Finish(false,m);}
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(EditorApplication.timeSinceStartup>deadline){Finish(false,"TIMEOUT");return;}
            var c=UnityEngine.Object.FindFirstObjectByType<DominoClientController>();
            if(!running&&EditorApplication.isPlaying&&c&&c.Menu&&DominoLocalization.Ready){running=true;_=Validate(c);}
        }
        static void Check(bool b,string label) {checks++;if(!b)throw new Exception(label);}
        static void ClickPrompt(DominoClientController c)
        {
            var p=c.SharedPrompt;if(!p||!p.gameObject.activeInHierarchy||!p.AwaitingInput)return;
            Check(!c.AcceptingInput,"HANDOFF_INPUT_BLOCKED");
            if(c.View.LocalTiles.Count>0)Check(Enumerable.Range(0,2).All(s=>c.View.HandViews(s).All(t=>!t.IsFaceUp)),"HANDOFF_PRIVACY");
            if(p.ContinueButton&&p.ContinueButton.gameObject.activeInHierarchy)p.ContinueButton.onClick.Invoke();
            else if(p.FirstChoice&&p.FirstChoice.interactable)p.FirstChoice.onClick.Invoke();
            else if(p.SecondChoice&&p.SecondChoice.interactable)p.SecondChoice.onClick.Invoke();
        }
        static async Task Ready(DominoClientController c)
        {
            var until=EditorApplication.timeSinceStartup+35;
            while(!c.AcceptingInput&&!c.State.Finished&&EditorApplication.timeSinceStartup<until) {ClickPrompt(c);await Task.Delay(80);}
            Check(c.AcceptingInput||c.State.Finished,"HUMAN_TURN_READY_OR_ROUND_COMPLETE");
        }
        static async Task Validate(DominoClientController c)
        {
            try {
                if(Phase!="") {
                    while(ApplicationServices.Identity?.State!=IdentityState.Ready)await Task.Delay(50);
                    await ApplicationServices.GameCatalog.RefreshAsync(true);
                    Check(ApplicationServices.GameCatalog.ResolveMatch("DUEL_1V1").SourceLabel==Phase,"CATALOG_SOURCE_"+Phase);
                    Check(ApplicationServices.GameCatalog.Current.CatalogVersion==2,"CATALOG_V2");
                }
                Time.timeScale=8;
                var sizes=new[]{(1080,1920),(1080,2160),(1080,2340),(1080,2400),(1080,2520),(1170,2532),(1284,2778),(1600,2560),(1536,2048)};
                foreach(var size in sizes) {
                    typeof(Domino.Editor.Phase1Validation).GetMethod("ResizeGameView",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{size.Item1,size.Item2});
                    await Task.Delay(300);DominoLocalization.Select(checks%2==0?"es":"en");
                    c.Menu.Show(StartScreen.ModeSelector);await Task.Delay(150);
                    Check(c.Menu.DuelPlay&&c.Menu.DuelPlay.gameObject.activeInHierarchy,"DUEL_CARD");
                    c.Menu.DuelPlay.onClick.Invoke();await Ready(c);
                    Check(c.Session.SharedDevice&&c.Session.Mode.BotCount==0,"NO_BOTS");
                    Check(c.State.Reserve.Count==35&&c.View.ReserveViews.Count==35&&c.View.VisuallyDealt==20,"20_DEALT_35_RESERVE");
                    Check(c.View.Perspective.BottomPlayer==0&&c.View.Perspective.TopPlayer==1,"FIXED_DUEL_TOPOLOGY");
                    Check(c.View.Perspective.LeftPlayer==-1&&c.View.Perspective.RightPlayer==-1,"ONLY_TOP_BOTTOM");
                    for(int turn=0;turn<2&&!c.State.Finished;turn++) {
                        int active=c.State.CurrentPlayer;
                        Check(c.View.HandViews(active).All(t=>t.IsFaceUp)&&c.View.HandViews(1-active).All(t=>!t.IsFaceUp),"ONLY_ACTIVE_REVEALED");
                        var tile=c.View.LocalTiles.First(t=>c.State.CanPlay(t.Tile));
                        var corners=new Vector3[4];tile.Rect.GetWorldCorners(corners);Check(corners.All(v=>Screen.safeArea.Contains(v)),"LOCAL_HAND_SAFE");
                        c.Select(tile);c.PlaySelected();await Ready(c);
                    }
                    if(size==sizes[0]) {ScreenCapture.CaptureScreenshot(Path.GetFullPath(Path.Combine(Application.dataPath,"../../m3-duel-portrait.png")));await Task.Delay(300);}
                    c.ExitMatch();await Task.Delay(150);
                }
                // Exercise a complete shared-device round and result privacy/CTA handling.
                c.StartMatch(new GameModeDefinition(ApplicationServices.GameCatalog.ResolveMatch("DUEL_1V1").Mode));await Ready(c);
                int actions=0;
                while(!c.State.Finished&&actions++<70) {
                    if(c.AcceptingInput){var t=c.View.LocalTiles.First(t=>c.State.CanPlay(t.Tile));c.Select(t);c.PlaySelected();}
                    if(!c.State.Finished)await Ready(c);
                }
                Check(c.State.Finished,"COMPLETE_DUEL_ROUND");
                var end=EditorApplication.timeSinceStartup+35;
                while(!c.View.RoundPresentationFinished&&EditorApplication.timeSinceStartup<end)await Task.Delay(100);
                Check(c.View.RoundPresentationFinished,"ROUND_PRESENTATION");
                Check(!c.View.RoundRewardPanel.WatchButton.gameObject.activeInHierarchy,"DUEL_REWARD_DISABLED");
                Finish(true,"M3_DUEL_UI=PASS CHECKS="+checks+" PORTRAIT_SIZES=9 SHARED_DEVICE_PRIVACY=PASS CONSOLE_ERRORS=0");
            }catch(Exception e){Finish(false,e.ToString());}
        }
        static void Finish(bool ok,string detail)
        {
            if(!SessionState.GetBool(Key,false))return;
            SessionState.SetBool(Key,false);Time.timeScale=1;
            File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../../m3-unity"+(Phase==""?"":"-"+Phase)+"-result.txt")),(ok?"PASS\n":"FAIL\n")+"CATALOG_SOURCE="+Phase+"\n"+detail);
            SessionState.SetString(Key+".Source","");
            EditorApplication.Exit(ok?0:1);
        }
    }
}
#endif
