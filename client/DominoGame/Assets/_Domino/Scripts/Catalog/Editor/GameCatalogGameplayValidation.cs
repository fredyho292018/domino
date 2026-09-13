#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Domino.Client;
using Domino.Identity;
using Domino.Infrastructure;
using Domino.Infrastructure.Api;
using Domino.Infrastructure.Firebase;
using Domino.UI;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Domino.Catalog.Editor
{
    // Three separate isolated Editor processes: remote, persisted-cache restart, bundled restart.
    public static class GameCatalogGameplayValidation
    {
        const string Key="Domino.M2.Validation";
        static bool running;
        static double deadline;
        static SwitchApi api;
        static string Output=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../M2Evidence"));
        static string CachePath=>Path.Combine(Output,"catalog-cache.json");
        sealed class ExistingGuest:IFirebaseClient,IAuthTokenProvider
        {
            readonly IFirebaseClient sdk=(IFirebaseClient)Activator.CreateInstance(typeof(ApplicationServices).Assembly.GetType("Domino.Infrastructure.Firebase.FirebaseSdkClient",true),new object[]{new Func<PlayerIdentity>(()=>ApplicationServices.Identity?.Current)});
            public Task<string> CheckDependenciesAsync()=>sdk.CheckDependenciesAsync();
            public void InitializeApp()=>sdk.InitializeApp();
            public PlayerIdentity GetCurrentUser()=>sdk.GetCurrentUser()??throw new Exception("EXISTING_IDENTITY_REQUIRED");
            public Task<PlayerIdentity> SignInAnonymouslyAsync()=>throw new Exception("NEW_GUEST_FORBIDDEN");
            public Task<string> GetIdTokenAsync(bool refresh,CancellationToken token)=>((IAuthTokenProvider)sdk).GetIdTokenAsync(refresh,token);
        }
        sealed class SwitchApi:IGameCatalogApi
        {
            public IGameCatalogApi Real;public string Fixture;
            public Task<string> FetchAsync(CancellationToken t)=>Fixture!=null?Task.FromResult(Fixture):Real.FetchAsync(t);
        }
        public static void RunRemote()=>Run("REMOTE");
        public static void RunCache()=>Run("CACHE");
        public static void RunBundled()=>Run("BUNDLED_FALLBACK");
        static void Run(string phase)
        {
            if(!Application.isBatchMode||!Application.dataPath.Replace('\\','/').Contains("/Validation/Generated/"))throw new Exception("ISOLATED_BATCH_REQUIRED");
            Directory.CreateDirectory(Output);
            // Delete only this harness's cache before the bundled restart.
            if(phase=="BUNDLED_FALLBACK"&&File.Exists(CachePath))File.Delete(CachePath);
            SessionState.SetString(Key,phase);Register();
            Domino.Editor.LocalizationAssets.Import();
            EditorSceneManager.OpenScene(Domino.Editor.ClientEditorTools.ScenePath);
            var controller=UnityEngine.Object.FindFirstObjectByType<DominoClientController>();
            var serialized=new SerializedObject(controller);
            serialized.FindProperty("configurationJson").objectReferenceValue=null;
            serialized.ApplyModifiedPropertiesWithoutUndo(); // unsaved test scene proves legacy asset is not read.
            EditorApplication.isPlaying=true;
        }
        [InitializeOnLoadMethod] static void Register()
        {
            if(string.IsNullOrEmpty(SessionState.GetString(Key,"")))return;
            deadline=EditorApplication.timeSinceStartup+240;
            ApplicationServices.ValidationFirebaseFactory=()=>new ExistingGuest();
            ApplicationServices.ValidationGameCatalogFactory=tokens=>{
                api=new SwitchApi {Real=new GameCatalogApi(new DominoApiConfiguration(true,"http://127.0.0.1:18082",15,"LOCAL",true),tokens,new UnityApiTransport())};
                return new GameCatalogService(api,new FileGameCatalogCache(CachePath),Resources.Load<TextAsset>("GameCatalogFallback").text,log:Debug.Log);
            };
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
            Application.logMessageReceived+=OnLog;
        }
        static void OnLog(string message,string stack,LogType type)
        {
            if(!string.IsNullOrEmpty(SessionState.GetString(Key,""))&&(type==LogType.Error||type==LogType.Exception||type==LogType.Assert))Finish(false,"CONSOLE_ERROR");
        }
        static void Tick()
        {
            if(string.IsNullOrEmpty(SessionState.GetString(Key,"")))return;
            if(EditorApplication.timeSinceStartup>deadline){Finish(false,"TIMEOUT");return;}
            if(!running&&EditorApplication.isPlaying&&ApplicationServices.Identity?.State==IdentityState.Ready&&DominoLocalization.Ready){running=true;_=Validate();}
        }
        static async Task Validate()
        {
            try {
                var controller=UnityEngine.Object.FindFirstObjectByType<DominoClientController>();
                while(controller.Menu==null)await Task.Delay(50);
                var service=ApplicationServices.GameCatalog;
                string phase=SessionState.GetString(Key,"");
                await service.RefreshAsync(true);
                if(service.ResolveMatch().SourceLabel!=phase)throw new Exception("SOURCE");
                controller.StartMatch(GameModeDefinition.TeamMatch);
                var frozen=controller.Session.Rules;var engine=controller.State;
                await Ready(controller);
                if(frozen.SourceLabel!=phase||engine.Configuration.TargetScore!=200||engine.Reserve.Count!=15)throw new Exception("CONFIGURATION");
                if(phase=="REMOTE") {
                    var fixture=JObject.Parse(Resources.Load<TextAsset>("GameCatalogFallback").text);fixture["catalogVersion"]=2;
                    var rules=(JObject)fixture["modes"][0]["ruleSet"];rules["version"]=2;rules["targetScore"]=250;rules["contentHash"]=GameCatalogCodec.Hash(rules);
                    api.Fixture=fixture.ToString();await service.RefreshAsync(true);
                    if(service.Current.CatalogVersion!=2||controller.Session.Rules!=frozen||controller.State!=engine||engine.Configuration.TargetScore!=200)throw new Exception("FREEZE");
                    controller.ExitMatch();controller.StartMatch(GameModeDefinition.TeamMatch);await Ready(controller);
                    if(controller.Session.Rules.CatalogVersion!=2||controller.State.Configuration.TargetScore!=250)throw new Exception("NEXT_MATCH");
                    controller.ExitMatch();api.Fixture=null;await service.RefreshAsync(true);
                    if(service.Current.CatalogVersion!=1)throw new Exception("RESTORE_REAL_CACHE");
                } else controller.ExitMatch();
                Finish(true,"MATCH_CONFIG_SOURCE="+phase+"\nGAMEPLAY=PASS\nLEGACY_ASSET_NULL=PASS\nCONSOLE_ERRORS=0\n"+
                    (phase=="REMOTE"?"REAL_DOWNLOAD=PASS\nACTIVE_FREEZE=PASS\nNEXT_MATCH_REFRESH=PASS\n": "BACKEND_OFF=PASS\nFULL_EDITOR_RESTART=PASS\n"));
            } catch {Finish(false,"M2_VALIDATION_FAILURE");}
        }
        static async Task Ready(DominoClientController controller)
        {
            Time.timeScale=8;
            while(!controller.AcceptingInput)await Task.Delay(50);
            if(controller.View.VisuallyDealt!=40||controller.State.Hand(0).Count!=10||controller.Session.Controls.Count(c=>c==ParticipantControl.BOT)!=3)throw new Exception("DEAL_OR_CONTROLS");
            Time.timeScale=1;
        }
        static void Finish(bool success,string detail)
        {
            string phase=SessionState.GetString(Key,"");if(phase=="")return;
            SessionState.SetString(Key,"");
            File.WriteAllText(Path.Combine(Output,phase+".txt"),(success?"PASS\n":"FAIL\n")+detail);
            EditorApplication.Exit(success?0:1);
        }
    }
}
#endif
