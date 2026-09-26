#if UNITY_EDITOR
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Domino.Identity;
using Domino.Infrastructure;
using Domino.Infrastructure.Api;
using Domino.Infrastructure.Firebase;
using Domino.Realtime;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace Domino.Editor {
    // Explicit bounded TEST validation. No credentials or raw responses are written to evidence.
    public static class Server5Validation {
        const string Key="Domino.Server5.Running";
        static string Output=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../Validation/Generated/SERVER5"));
        static bool running;
        static int errors;
        static double deadline;
        static RealtimeConnectionService measured;
        static Socket current;
        static int pongs,authentications;
        static void Record(string s){Directory.CreateDirectory(Output);File.AppendAllText(Path.Combine(Output,"result.txt"),s+"\n");}
        public static void RunEditMode() {
            bool before=DominoApiSettings.EditorTestSelected;
            try {
                DominoApiSettings.SelectConfigured();
                var asset=Resources.Load<DominoApiSettings>("ApiSettings");
                var local=asset.Configuration;
                if(local.Endpoint.Host!="127.0.0.1")throw new Exception("LOCAL_CONFIG_CHANGED");
                DominoApiSettings.SelectTest();
                if(asset.Environment!="TEST"||asset.Configuration.Endpoint.Host!="domino-api-test.teamfho.com")throw new Exception("TEST_CONFIG");
                if(new RealtimeConfiguration(asset.Configuration).Endpoint.ToString()!="wss://domino-api-test.teamfho.com/ws/v1/realtime")throw new Exception("WSS_CONFIG");
                Record("UNITY_EDITMODE=3_PASS");
            }finally{if(before)DominoApiSettings.SelectTest();else DominoApiSettings.SelectConfigured();}
            EditorApplication.Exit(0);
        }
        public static void RunReal() {
            ValidationNetworkPolicy.AuthorizeReal();
            var cfg=JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"google-services.json")));
            if((string)cfg["project_info"]?["project_id"]!="teamfho-domino")throw new Exception("WRONG_FIREBASE_PROJECT");
            DominoApiSettings.SelectTest();
            SessionState.SetBool(Key,true);Register();
            EditorSceneManager.OpenScene(ClientEditorTools.ScenePath);EditorApplication.isPlaying=true;
        }
        [InitializeOnLoadMethod] static void Register(){
            if(!SessionState.GetBool(Key,false))return;
            deadline=EditorApplication.timeSinceStartup+240;
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
            Application.logMessageReceived+=(m,s,t)=>{if(t==LogType.Error||t==LogType.Exception||t==LogType.Assert)errors++;};
        }
        static void Tick(){
            if(!SessionState.GetBool(Key,false))return;
            if(EditorApplication.timeSinceStartup>deadline){Finish(false,"TIMEOUT");return;}
            if(!running&&Application.isPlaying&&ApplicationServices.Player?.IsFresh==true&&ApplicationServices.Realtime?.State==RealtimeConnectionState.CONNECTED){running=true;_=Check();}
        }
        sealed class Socket:IRealtimeSocket {
            readonly ClientRealtimeSocket inner=new ClientRealtimeSocket();
            public Task ConnectAsync(Uri u,CancellationToken c)=>inner.ConnectAsync(u,c);
            public Task SendAsync(string m,CancellationToken c)=>inner.SendAsync(m,c);
            public async Task<string> ReceiveAsync(CancellationToken c){var s=await inner.ReceiveAsync(c);var type=(string)JObject.Parse(s)["type"];if(type=="PONG")pongs++;if(type=="AUTHENTICATED")authentications++;return s;}
            public void Dispose()=>inner.Dispose();
        }
        static async Task Until(Func<bool> condition,CancellationToken ct){while(!condition())await Task.Delay(100,ct);}
        static async Task Check(){
            using var limit=new CancellationTokenSource(TimeSpan.FromSeconds(180));var ct=limit.Token;
            try {
                if(global::Firebase.FirebaseApp.DefaultInstance.Options.ProjectId!="teamfho-domino")throw new Exception("PROJECT_GUARD");
                var uid=ApplicationServices.Identity.Current.Uid;
                if(ApplicationServices.Player.Player.Uid!=uid)throw new Exception("IDENTITY_MISMATCH");
                Record("AUTHENTICATED_REST=PASS\nUNITY_FIREBASE_PROJECT_ID=teamfho-domino\nUID_PARITY=PASS");
                var sdk=(IFirebaseClient)Activator.CreateInstance(typeof(ApplicationServices).Assembly.GetType("Domino.Infrastructure.Firebase.FirebaseSdkClient",true),new object[]{new Func<PlayerIdentity>(()=>ApplicationServices.Identity.Current)});
                sdk.InitializeApp();var tokens=(IAuthTokenProvider)sdk;
                var config=Resources.Load<DominoApiSettings>("ApiSettings").Configuration;
                var transport=new UnityApiTransport();
                foreach(var path in new[]{"player/profile","player/social-summary","player/friends","player/following","players/me/history","matchmaking/queue"}){
                    var bearer=await tokens.GetIdTokenAsync(false,ct);
                    var response=await transport.SendAsync("GET",new Uri(config.Endpoint,"/api/v1/"+path),null,bearer,15,ct);bearer=null;
                    Record("GET_"+path.Replace('/','_')+"="+response.Status);
                    if(response.Status!=200)throw new Exception("REST_STATUS_"+response.Status);
                }
                var fresh=await tokens.GetIdTokenAsync(true,ct);
                var refreshed=await transport.SendAsync("GET",new Uri(config.Endpoint,"/api/v1/player/profile"),null,fresh,15,ct);fresh=null;
                if(refreshed.Status!=200)throw new Exception("REFRESH_REJECTED");Record("TOKEN_REFRESH_REMOTE=PASS");
                ApplicationServices.Realtime.Dispose();
                measured=new RealtimeConnectionService(new RealtimeConfiguration(config),ApplicationServices.Identity,tokens,()=>{current=new Socket();return current;});
                measured.Start();await Until(()=>measured.State==RealtimeConnectionState.CONNECTED&&measured.Activity!=null,ct);
                await Until(()=>pongs>=3,ct);Record("AUTHENTICATED_WSS=PASS\nHEARTBEAT_CYCLES="+pongs);
                var previous=authentications;current.Dispose();
                await Until(()=>authentications>previous&&measured.State==RealtimeConnectionState.CONNECTED&&measured.Activity!=null,ct);
                Record("WSS_RECONNECT=PASS\nWSS_RESYNC=PASS");
                measured.Dispose();await Until(()=>measured.State==RealtimeConnectionState.DISCONNECTED,ct);
                Record("CONNECTION_DISPOSAL=PASS\nLOGOUT_REMOTE_BEHAVIOR=NOT_RUN_ACCOUNT_PRESERVED");
                Finish(errors==0,"CHECKS_COMPLETE");
            }catch(Exception e){Finish(false,e is OperationCanceledException?"TIMEOUT":e.GetType().Name);}
        }
        static void Finish(bool ok,string reason){
            if(!SessionState.GetBool(Key,false))return;
            SessionState.SetBool(Key,false);measured?.Dispose();
            Record("UNITY_PLAYMODE="+(ok?"PASS":"FAIL")+"\nCONSOLE_ERRORS="+errors+"\nRESULT="+reason);
            ValidationNetworkPolicy.EndValidation();EditorApplication.Exit(ok?0:1);
        }
    }
}
#endif
