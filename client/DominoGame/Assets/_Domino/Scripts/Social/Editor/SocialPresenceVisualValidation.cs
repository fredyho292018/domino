#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Domino.Infrastructure;
using Domino.Online;
using Domino.Realtime;
using Domino.Social;
using Domino.UI;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Domino.Editor
{
    // Isolated presentation fixture. Never authenticates or contacts any backend.
    public static class SocialPresenceVisualValidation
    {
        const string Key="Domino.S14B.Visual";
        const string Id="AAAAAAAAAAAAAAAAAAAAAA";
        static bool running,finished;static int checks;static double deadline;
        static string Output=>Environment.GetEnvironmentVariable("DOMINO_S14B_OUTPUT");
        sealed class Api:IOnlineMatchApi {
            public Task<JObject> SendAsync(string method,string path,JObject body,CancellationToken token) {
                if(path=="player/social-summary")return Task.FromResult(new JObject {
                    ["profile"]=new JObject{["friendCode"]="FHO-LOCAL",["publicPlayerId"]="BBBBBBBBBBBBBBBBBBBBBB"},
                    ["friends"]=new JObject{["availability"]="AVAILABLE",["friendCount"]=1,["effectiveFriendLimit"]=5,["canAddFriend"]=true},
                    ["privacy"]=new JObject{["revision"]=1,["discoverableByName"]=true,["friendRequests"]="EVERYONE",["follow"]="EVERYONE",["presenceVisibility"]="FRIENDS",["matchActivityVisibility"]="FRIENDS"}});
                return Task.FromResult(new JObject{["items"]=new JArray(new JObject{["profile"]=new JObject{["publicPlayerId"]=Id,["displayName"]="María García",["friendCode"]="FHO-LOCAL"},["friendsSince"]="2026-09-01T00:00:00Z"})});
            }
        }
        sealed class Channel:IRealtimeConnectionService,IRealtimePresenceChannel {
            public RealtimeConnectionState State{get;private set;}=RealtimeConnectionState.CONNECTED;
            public GlobalActivitySnapshot Activity=>null;
            public event Action Changed;public event Action<string,JObject> PresenceMessage;
            public JObject Desired;public int Requests;
            public Task SubscribePresenceAsync(JObject desired){Desired=desired;Requests++;return Task.CompletedTask;}
            public void Emit(string type,SocialPresenceState state,long revision=1) {
                PresenceMessage?.Invoke(type,new JObject{["generation"]=(long)Desired["generation"],["revision"]=revision,["publicPlayerId"]=Id,["state"]=state.ToString()});
            }
            public void Set(RealtimeConnectionState s){State=s;Changed?.Invoke();}
            public void Start(){}public void SetBackground(bool background){}public void Dispose(){}
        }
        public static void Run() {
            if(!Application.dataPath.Replace('\\','/').Contains("/Validation/Generated/")||string.IsNullOrEmpty(Output))throw new Exception("ISOLATED_PROJECT_REQUIRED");
            Directory.CreateDirectory(Output);ValidationNetworkPolicy.BeginIsolated();LocalizationAssets.ImportTablesOnly();
            SessionState.SetBool(Key,true);Register();EditorSceneManager.OpenScene(ClientEditorTools.ScenePath);EditorApplication.isPlaying=true;
        }
        [InitializeOnLoadMethod]static void Register(){if(!SessionState.GetBool(Key,false))return;deadline=EditorApplication.timeSinceStartup+180;Application.logMessageReceived+=Log;EditorApplication.update+=Tick;}
        static void Log(string m,string s,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)Finish(false,m);}
        static void Tick(){if(EditorApplication.timeSinceStartup>deadline){Finish(false,"TIMEOUT");return;}if(!running&&EditorApplication.isPlaying&&DominoLocalization.Ready){running=true;_=Validate();}}
        static void Check(bool value,string name){checks++;if(!value)throw new Exception(name);}
        static Button Find(SocialView v,string name)=>v.GetComponentsInChildren<Button>().Single(b=>b.name==name);
        static async Task Validate() {
            try {
                foreach(var lang in new[]{"en","es"})foreach(var size in new[]{new Vector2Int(1080,1920),new Vector2Int(1080,2400),new Vector2Int(1536,2048)}) {
                    typeof(Phase1Validation).GetMethod("ResizeGameView",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).Invoke(null,new object[]{size.x,size.y});
                    DominoLocalization.Select(lang);await Task.Delay(300);
                    var channel=new Channel();var client=new SocialClient(new Api(),()=>"local-presentation",channel);
                    var view=new GameObject("S14B isolated presence").AddComponent<SocialView>();view.Initialize(client,()=>{});
                    await Task.Delay(100);Find(view,"Friends tab").onClick.Invoke();await Task.Delay(600);
                    Check(channel.Desired!=null&&((JArray)channel.Desired["publicPlayerIds"]).Count==1,"VISIBLE_SUBSCRIPTION");
                    channel.Emit("SOCIAL_PRESENCE_INVALIDATED",SocialPresenceState.UNKNOWN);
                    foreach(SocialPresenceState state in Enum.GetValues(typeof(SocialPresenceState))) {
                        channel.Emit("SOCIAL_PRESENCE_UPDATED",state);await Task.Delay(100);
                        Check(view.GetComponentsInChildren<Text>().Any(t=>t.text.Contains(DominoLocalization.Get(SocialPresenceStore.LocalizationKey(state)))),"VISIBLE_"+state);
                        ScreenCapture.CaptureScreenshot(Path.Combine(Output,lang+"-"+size.x+"x"+size.y+"-"+state+".png"));await Task.Delay(100);
                    }
                    channel.Emit("SOCIAL_PRESENCE_INVALIDATED",SocialPresenceState.UNKNOWN,2);
                    channel.Emit("SOCIAL_PRESENCE_UPDATED",SocialPresenceState.ONLINE,1);
                    Check(client.Presence.Get(Id)==SocialPresenceState.UNKNOWN,"STALE_REVISION");
                    channel.Set(RealtimeConnectionState.RECONNECTING);Check(client.Presence.Get(Id)==SocialPresenceState.UNKNOWN,"DISCONNECT_CLEAR");
                    channel.Set(RealtimeConnectionState.CONNECTED);await Task.Delay(400);Check(channel.Requests>=2,"RESUBSCRIBE");
                    Find(view,"Privacy tab").onClick.Invoke();await Task.Delay(100);
                    Check(Find(view,"Presence privacy")&&Find(view,"Activity privacy"),"PRIVACY_CONTROLS");
                    ScreenCapture.CaptureScreenshot(Path.Combine(Output,lang+"-"+size.x+"x"+size.y+"-privacy.png"));await Task.Delay(100);
                    view.Close();await Task.Delay(100);Check(((JArray)channel.Desired["publicPlayerIds"]).Count==0,"CLOSE_UNSUBSCRIBE");
                }
                Finish(true,"REAL_BACKEND_CALLS=0\nCONSOLE_ERRORS=0");
            }catch(Exception e){Finish(false,e.ToString());}
        }
        static void Finish(bool pass,string reason){if(finished)return;finished=true;SessionState.EraseBool(Key);EditorApplication.update-=Tick;Application.logMessageReceived-=Log;
            File.WriteAllText(Path.Combine(Output,"unity-presence-result.txt"),"PASS="+pass+"\nCHECKS="+checks+"\n"+reason);EditorApplication.Exit(pass?0:1);}
    }
}
#endif
