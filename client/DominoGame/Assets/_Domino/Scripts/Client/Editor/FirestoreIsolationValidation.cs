#if UNITY_EDITOR
using System;
using System.Threading;
using System.Threading.Tasks;
using Domino.Infrastructure;
using Domino.Infrastructure.Api;
using Domino.Realtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Domino.Editor
{
    public static class FirestoreIsolationValidation
    {
        const string Key="F0.Isolation.Validation";
        static double started;
        static bool checking;
        public static void Run()
        {
            if(!Application.isBatchMode || !Application.dataPath.Replace('\\','/').Contains("/Validation/Generated/"))throw new Exception("ISOLATED_PROJECT_REQUIRED");
            ValidationNetworkPolicy.BeginIsolated();SessionState.SetBool(Key,true);
            EditorSceneManager.OpenScene(ClientEditorTools.ScenePath);EditorApplication.isPlaying=true;
        }
        [InitializeOnLoadMethod] static void Register(){EditorApplication.update+=Tick;Application.logMessageReceived+=Log;started=EditorApplication.timeSinceStartup;}
        static void Log(string message,string trace,LogType type){if(SessionState.GetBool(Key,false)&&(type==LogType.Error||type==LogType.Exception||type==LogType.Assert))Finish(false,message);}
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(EditorApplication.timeSinceStartup-started>90){Finish(false,"TIMEOUT");return;}
            if(EditorApplication.isPlaying && ApplicationServices.Player!=null && !checking){checking=true;_=Check();}
        }
        static async Task Check()
        {
            try {
                await Task.Delay(10000);
                if(!ValidationNetworkPolicy.Isolated || ApplicationServices.Identity.Current!=null || ValidationNetworkPolicy.NetworkEntrypoints!=0)throw new Exception("BOOTSTRAP_NOT_ISOLATED");
                if(!Resources.Load<DominoApiSettings>("ApiSettings").Configuration.IsAvailable)throw new Exception("TEST_REQUIRES_ENABLED_SOURCE_SETTINGS");
                bool blocked=false;
                try{await new UnityApiTransport().SendAsync("GET",new Uri("http://127.0.0.1:1"),null,"unused",1,CancellationToken.None);}catch(InvalidOperationException e){blocked=e.Message=="VALIDATION_NETWORK_DISABLED";}
                if(!blocked)throw new Exception("REST_GUARD_FAILED");
                using(var socket=new ClientRealtimeSocket()) {
                    blocked=false;try{await socket.ConnectAsync(new Uri("ws://127.0.0.1:1"),CancellationToken.None);}catch(InvalidOperationException e){blocked=e.Message=="VALIDATION_NETWORK_DISABLED";}
                    if(!blocked)throw new Exception("SOCKET_GUARD_FAILED");
                }
                if(ValidationNetworkPolicy.NetworkEntrypoints!=0)throw new Exception("NETWORK_ENTRYPOINT_REACHED");
                Finish(true,"VISUAL_TEST_REAL_BACKEND_CALLS=0; FIREBASE_SDK_CALLS=0; REALTIME_CALLS=0; BOOTSTRAP_ISOLATION=PASS");
            }catch(Exception e){Finish(false,e.Message);}
        }
        static void Finish(bool ok,string detail){SessionState.SetBool(Key,false);Debug.Log("F0_UNITY_ISOLATION="+(ok?"PASS":"FAIL")+" "+detail);EditorApplication.Exit(ok?0:1);}
    }
}
#endif
