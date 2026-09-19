#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Domino.Identity;
using Domino.Infrastructure;
using Domino.Infrastructure.Firebase;
using Domino.Realtime;
using Domino.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Domino.Editor
{
    public static class RealtimeValidation
    {
        const string Key="Domino.G3.Real";
        static double deadline, stable;
        static int phase;
        static bool checking;
        static string uid, alias;
        static long coins;
        static string Output=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../G3"));
        sealed class ExistingGuest : IFirebaseClient,IAuthTokenProvider
        {
            readonly IFirebaseClient sdk=(IFirebaseClient)Activator.CreateInstance(typeof(ApplicationServices).Assembly.GetType("Domino.Infrastructure.Firebase.FirebaseSdkClient",true),new object[]{new Func<PlayerIdentity>(()=>ApplicationServices.Identity?.Current)});
            public Task<string> CheckDependenciesAsync()=>sdk.CheckDependenciesAsync();
            public void InitializeApp()=>sdk.InitializeApp();
            public PlayerIdentity GetCurrentUser()=>sdk.GetCurrentUser()??throw new Exception("Existing Guest required");
            public Task<PlayerIdentity> SignInAnonymouslyAsync()=>throw new Exception("New Guest forbidden");
            public Task<string> GetIdTokenAsync(bool refresh,CancellationToken ct)=>((IAuthTokenProvider)sdk).GetIdTokenAsync(refresh,ct);
        }
        public static void RunReal()
        {
            Domino.Infrastructure.ValidationNetworkPolicy.AuthorizeReal();
            if(!Application.isBatchMode)throw new Exception("Isolated batch editor required");
            Directory.CreateDirectory(Output);LocalizationAssets.Import();
            SessionState.SetBool(Key,true);Register();
            EditorSceneManager.OpenScene(ClientEditorTools.ScenePath);EditorApplication.isPlaying=true;
        }
        [InitializeOnLoadMethod] static void Register()
        {
            if(!SessionState.GetBool(Key,false))return;
            deadline=EditorApplication.timeSinceStartup+900;
            ApplicationServices.ValidationFirebaseFactory=()=>new ExistingGuest();
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
            Application.logMessageReceived+=(m,s,t)=>{
                if(t==LogType.Error||t==LogType.Exception||t==LogType.Assert)EditorApplication.delayCall+=()=>Finish(false,"CONSOLE_ERROR");
            };
        }
        static void State(string state)=>File.WriteAllText(Path.Combine(Output,"state.txt"),state);
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(EditorApplication.timeSinceStartup>deadline){Finish(false,"TIMEOUT_PHASE_"+phase);return;}
            var realtime=ApplicationServices.Realtime;var player=ApplicationServices.Player;
            if(realtime==null||player==null||!DominoLocalization.Ready)return;
            bool connected=realtime.State==RealtimeConnectionState.CONNECTED&&realtime.Activity?.OnlinePlayers>=1;
            if(phase==0&&connected&&player.IsFresh){
                uid=player.Player.Uid;alias=player.Player.DisplayName;coins=player.Wallet.Coins;
                stable=EditorApplication.timeSinceStartup;phase=1;State("INITIAL_CONNECTED");
            }
            if(phase==1&&connected&&EditorApplication.timeSinceStartup-stable>24){phase=2;State("STOP_BACKEND");}
            if(phase==2&&realtime.State==RealtimeConnectionState.RECONNECTING){phase=3;State("START_BACKEND");}
            if(phase==3&&connected){phase=4;State("STOP_REDIS");}
            if(phase==4&&realtime.State==RealtimeConnectionState.RECONNECTING&&!checking){checking=true;_ = CheckRest();}
            if(phase==5&&connected){phase=6;stable=EditorApplication.timeSinceStartup;State("REDIS_RESTORED");}
            if(phase==6&&connected&&EditorApplication.timeSinceStartup-stable>24){
                if(player.Player.Uid!=uid||player.Player.DisplayName!=alias||player.Wallet.Coins!=coins){Finish(false,"PLAYER_CHANGED");return;}
                Finish(true,"CONNECTED=PASS\nHEARTBEAT_24S=PASS\nBACKEND_RECONNECT=PASS\nREDIS_RECONNECT=PASS\nREST_WITH_REDIS_OFF=PASS\nSAME_UID_ALIAS_COINS=PASS\nCONSOLE_ERRORS=0");
            }
        }
        static async Task CheckRest()
        {
            try {
                var player=ApplicationServices.Player;
                await (Task)player.GetType().GetMethod("RefreshConfirmedAsync",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(player,new object[]{CancellationToken.None});
                if(!player.IsFresh)throw new Exception();
                // Same-value alias is a backend no-op; verify the second REST operation also remains usable.
                await player.UpdateDisplayNameAsync(alias);
                if(!player.IsFresh||player.Player.DisplayName!=alias||player.Wallet.Coins!=coins)throw new Exception();
                phase=5;State("START_REDIS");
            } catch {Finish(false,"REST_WITH_REDIS_OFF");}
        }
        static void Finish(bool ok,string details)
        {
            if(!SessionState.GetBool(Key,false))return;
            SessionState.SetBool(Key,false);State(ok?"PASS":"FAIL");
            File.WriteAllText(Path.Combine(Output,"result.txt"),(ok?"PASS\n":"FAIL\n")+details);
            EditorApplication.Exit(ok?0:1);
        }
    }
}
#endif
