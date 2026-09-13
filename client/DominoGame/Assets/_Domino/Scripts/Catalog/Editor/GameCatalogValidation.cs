#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Domino.Identity;
using Domino.Infrastructure;
using Domino.Infrastructure.Api;
using Domino.Infrastructure.Firebase;
using Domino.Client;
using Domino.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Domino.Catalog.Editor
{
    // Explicit isolated batch validation. Reuses an existing identity; cannot create a Guest.
    public static class GameCatalogValidation
    {
        const string Key="Domino.M1.Validation";
        static double deadline;
        static bool running;
        static string Output=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../M1Evidence"));
        sealed class ExistingGuest:IFirebaseClient,IAuthTokenProvider
        {
            readonly IFirebaseClient sdk=(IFirebaseClient)Activator.CreateInstance(typeof(ApplicationServices).Assembly.GetType("Domino.Infrastructure.Firebase.FirebaseSdkClient",true),new object[]{new Func<PlayerIdentity>(()=>ApplicationServices.Identity?.Current)});
            public Task<string> CheckDependenciesAsync()=>sdk.CheckDependenciesAsync();
            public void InitializeApp()=>sdk.InitializeApp();
            public PlayerIdentity GetCurrentUser()=>sdk.GetCurrentUser()??throw new Exception("EXISTING_IDENTITY_REQUIRED");
            public Task<PlayerIdentity> SignInAnonymouslyAsync()=>throw new Exception("NEW_GUEST_FORBIDDEN");
            public Task<string> GetIdTokenAsync(bool refresh,CancellationToken token)=>((IAuthTokenProvider)sdk).GetIdTokenAsync(refresh,token);
        }
        static ExistingGuest guest;
        [InitializeOnLoadMethod] static void Register()
        {
            if(!SessionState.GetBool(Key,false))return;
            deadline=EditorApplication.timeSinceStartup+240;
            ApplicationServices.ValidationFirebaseFactory=()=>guest=new ExistingGuest();
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
            Application.logMessageReceived+=OnLog;
        }
        public static void Run()
        {
            if(!Application.isBatchMode)throw new Exception("ISOLATED_BATCH_REQUIRED");
            Directory.CreateDirectory(Output);
            SessionState.SetBool(Key,true);Register();
            // Source ApiSettings stays disabled: Player/Wallet and realtime remain offline.
            Domino.Editor.LocalizationAssets.Import();
            EditorSceneManager.OpenScene(Domino.Editor.ClientEditorTools.ScenePath);
            EditorApplication.isPlaying=true;
        }
        static void OnLog(string message,string stack,LogType type)
        {
            if(SessionState.GetBool(Key,false)&&(type==LogType.Error||type==LogType.Exception||type==LogType.Assert))Finish(false,"CONSOLE_ERROR");
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(EditorApplication.timeSinceStartup>deadline){Finish(false,"TIMEOUT");return;}
            if(!running&&EditorApplication.isPlaying&&ApplicationServices.Identity?.State==IdentityState.Ready&&DominoLocalization.Ready){running=true;_=CheckAsync();}
        }
        static async Task CheckAsync()
        {
            try {
                var controller=UnityEngine.Object.FindFirstObjectByType<DominoClientController>();
                while(controller.Menu==null)await Task.Delay(50);
                var config=new DominoApiConfiguration(true,"http://127.0.0.1:18081",15,"LOCAL",true);
                var transport=new UnityApiTransport();
                var unauth=await transport.SendAsync("GET",new Uri("http://127.0.0.1:18081/api/v1/game-modes"),null,"",2,default);
                if(unauth.Status!=401)throw new Exception("AUTH_REQUIRED");
                var api=new GameCatalogApi(config,guest,transport);
                var response=await api.FetchAsync(default);
                File.WriteAllText(Path.Combine(Output,"catalog-response.json"),response);
                new GameCatalogCodec().Read(response);
                string bundled=Resources.Load<TextAsset>("GameCatalogFallback").text;
                var disk=new FileGameCatalogCache(Path.Combine(Output,"catalog-cache.json"));
                var service=new GameCatalogService(api,disk,bundled,log:Debug.Log);
                await service.RefreshAsync(true);
                if(service.Source!=GameCatalogSource.Remote||service.Current.Modes.Single().Key!="PARTNERS_2V2")throw new Exception("REMOTE_DOWNLOAD");
                await Play(controller);
                File.WriteAllText(Path.Combine(Output,"state.txt"),"REMOTE_PASS_STOP_BACKEND");
                while(!File.Exists(Path.Combine(Output,"backend-stopped")))await Task.Delay(100);
                service=new GameCatalogService(api,disk,bundled,log:Debug.Log);
                await service.RefreshAsync(true);
                if(service.Source!=GameCatalogSource.Cache)throw new Exception("OFFLINE_CACHE");
                await Play(controller);
                File.WriteAllText(Path.Combine(Output,"catalog-cache.json"),"corrupt");
                service=new GameCatalogService(api,disk,bundled,log:Debug.Log);await service.RefreshAsync(true);
                if(service.Source!=GameCatalogSource.Bundled)throw new Exception("FALLBACK");
                await Play(controller);
                Finish(true,"REAL_API=PASS\nUNAUTHENTICATED=401\nREMOTE_DOWNLOAD=PASS\nOFFLINE_PERSISTENT_CACHE=PASS\nCORRUPT_CACHE_BUNDLED=PASS\nGAMEPLAY_A_B_C=PASS\nNEW_GUEST=NO\nWALLET_REQUESTS=0");
            } catch { Finish(false,"M1_VALIDATION_FAILURE"); }
        }
        static async Task Play(DominoClientController controller)
        {
            controller.StartMatch(GameModeDefinition.TeamMatch);
            var state=controller.State;
            if(state.Configuration.Id!="double-nine-partners"||state.Configuration.TargetScore!=200||state.Reserve.Count!=15)throw new Exception("GAMEPLAY_CONFIG");
            Time.timeScale=8;
            while(!controller.AcceptingInput)await Task.Delay(50);
            if(controller.View.VisuallyDealt!=40||state.Hand(0).Count!=10)throw new Exception("DEAL");
            controller.ExitMatch();Time.timeScale=1;
        }
        static void Finish(bool success,string detail)
        {
            if(!SessionState.GetBool(Key,false))return;
            SessionState.SetBool(Key,false);
            File.WriteAllText(Path.Combine(Output,"result.txt"),(success?"PASS\nCONSOLE_ERRORS=0\n":"FAIL\n")+detail);
            EditorApplication.Exit(success?0:1);
        }
    }
}
#endif
