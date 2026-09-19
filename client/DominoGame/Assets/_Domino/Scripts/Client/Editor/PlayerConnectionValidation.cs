#if UNITY_EDITOR
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Domino.Identity;
using Domino.Infrastructure;
using Domino.Infrastructure.Firebase;
using Domino.Player;
using Domino.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Domino.Editor
{
    // Explicit isolated validation only. No polling/heartbeat is added to production.
    public static class PlayerConnectionValidation
    {
        const string ModeKey = "Domino.G21.Idle";
        static double deadline, started;
        static string uid, alias;
        static long coins;
        static string Output => Path.GetFullPath(Path.Combine(Application.dataPath,"../../G21"));
        sealed class ExistingGuest : IFirebaseClient, IAuthTokenProvider
        {
            readonly IFirebaseClient sdk = (IFirebaseClient)Activator.CreateInstance(typeof(ApplicationServices).Assembly.GetType("Domino.Infrastructure.Firebase.FirebaseSdkClient",true),
                new object[]{new Func<PlayerIdentity>(()=>ApplicationServices.Identity?.Current)});
            public Task<string> CheckDependenciesAsync()=>sdk.CheckDependenciesAsync();
            public void InitializeApp()=>sdk.InitializeApp();
            public PlayerIdentity GetCurrentUser()=>sdk.GetCurrentUser() ?? throw new Exception("Existing identity required");
            public Task<PlayerIdentity> SignInAnonymouslyAsync()=>throw new Exception("New Guest forbidden in validation");
            public Task<string> GetIdTokenAsync(bool refresh,CancellationToken ct)=>((IAuthTokenProvider)sdk).GetIdTokenAsync(refresh,ct);
        }
        public static void RunEditorIdle()=>Run(1);
        public static void RunDisabledIdle()=>Run(2);
        public static void RunEnabledIdle()=>Run(3);
        static void Run(int mode)
        {
            if(mode==1)Domino.Infrastructure.ValidationNetworkPolicy.BeginIsolated();
            else Domino.Infrastructure.ValidationNetworkPolicy.AuthorizeReal();
            if(!Application.isBatchMode) throw new Exception("Isolated batch editor required");
            Directory.CreateDirectory(Output); LocalizationAssets.Import();
            SessionState.SetInt(ModeKey,mode); Register();
            EditorSceneManager.OpenScene(ClientEditorTools.ScenePath);
            if(mode!=1) EditorApplication.isPlaying=true;
        }
        [InitializeOnLoadMethod] static void Register()
        {
            if(SessionState.GetInt(ModeKey,0)==0)return;
            deadline=EditorApplication.timeSinceStartup+180;
            ApplicationServices.ValidationFirebaseFactory=()=>new ExistingGuest();
            EditorApplication.update-=Tick; EditorApplication.update+=Tick;
            Application.logMessageReceived+=(message,stack,type)=>{
                if(SessionState.GetInt(ModeKey,0)!=0 && (type==LogType.Error||type==LogType.Exception||type==LogType.Assert)) Finish(false,"CONSOLE_ERROR");
            };
        }
        static void Tick()
        {
            int mode=SessionState.GetInt(ModeKey,0); if(mode==0)return;
            if(EditorApplication.timeSinceStartup>deadline){Finish(false,"TIMEOUT");return;}
            var service=ApplicationServices.Player;
            if(mode!=1){
                if(!EditorApplication.isPlaying || !DominoLocalization.Ready || service==null)return;
                if(service.State==PlayerSyncState.NOT_SYNCED || service.State==PlayerSyncState.SYNCING)return;
                if(mode==2 && (service.State!=PlayerSyncState.FAILED || service.Error?.Category!=Infrastructure.Api.ApiFailure.Configuration)){Finish(false,"DISABLED_API_STATE");return;}
                if(mode==3 && !service.IsFresh){Finish(false,"BOOTSTRAP_"+service.Error?.Category);return;}
            }
            if(started==0){
                started=EditorApplication.timeSinceStartup;
                if(mode==3){uid=service.Player.Uid;alias=service.Player.DisplayName;coins=service.Wallet.Coins;}
                File.WriteAllText(Path.Combine(Output,"idle-"+mode+"-start.txt"),DateTime.UtcNow.ToString("o"));
            }
            if(EditorApplication.timeSinceStartup-started<31)return;
            if(mode==3){
                var profile=UnityEngine.Object.FindFirstObjectByType<PlayerProfileView>();
                if(service.Player.Uid!=uid || service.Player.DisplayName!=alias || service.Wallet.Coins!=coins || !profile || profile.DisplayedStatus!=DominoLocalization.Get("profile.synced")){Finish(false,"IDLE_STATE_CHANGED");return;}
            }
            Finish(true,"IDLE_SECONDS="+(EditorApplication.timeSinceStartup-started).ToString("F1",System.Globalization.CultureInfo.InvariantCulture)+"\nCONSOLE_ERRORS=0");
        }
        static void Finish(bool success,string detail)
        {
            int mode=SessionState.GetInt(ModeKey,0);SessionState.SetInt(ModeKey,0);
            File.WriteAllText(Path.Combine(Output,"idle-"+mode+"-result.txt"),(success?"PASS\n":"FAIL\n")+detail+"\nEND_UTC="+DateTime.UtcNow.ToString("o"));
            EditorApplication.Exit(success?0:1);
        }
    }
}
#endif
