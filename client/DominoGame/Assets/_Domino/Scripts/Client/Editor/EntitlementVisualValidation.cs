#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Domino.Identity;
using Domino.Infrastructure;
using Domino.Infrastructure.Api;
using Domino.Online;
using Domino.Player;
using Domino.Replay;
using Domino.UI;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Domino.Editor
{
    public static class EntitlementVisualValidation
    {
        const string Key="Domino.P01.Validation";
        static bool running;static int checks;static double deadline;
        static string Output=>Environment.GetEnvironmentVariable("DOMINO_P01_OUTPUT");
        sealed class Identity:IPlayerIdentityService {
            public IdentityState State=>IdentityState.Ready;
            public PlayerIdentity Current{get;}=new PlayerIdentity("p0",true);
            public Exception Error=>null;
            public Task<PlayerIdentity> InitializeAsync()=>Task.FromResult(Current);
        }
        sealed class Api:IDominoApiClient,IOnlineMatchApi {
            public bool IsAvailable=>true;
            public async Task<JObject> SendAsync(string method,string path,JObject body,CancellationToken token) {
                if(path.Contains(":")||path.StartsWith("/")||path.Contains(".."))throw new Exception("INVALID_TEST_PATH");
                using var http=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false});
                http.Timeout=TimeSpan.FromSeconds(15);
                http.DefaultRequestHeaders.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer","p01-loopback");
                using var request=new HttpRequestMessage(new HttpMethod(method),"http://127.0.0.1:18088/api/v1/"+path);
                if(body!=null)request.Content=new StringContent(body.ToString(),System.Text.Encoding.UTF8,"application/json");
                using var result=await http.SendAsync(request,token);result.EnsureSuccessStatusCode();
                return JObject.Parse(await result.Content.ReadAsStringAsync());
            }
            public async Task<PlayerBootstrapResponseDto> BootstrapAsync(string lang,CancellationToken token)=>
                JsonUtility.FromJson<PlayerBootstrapResponseDto>((await SendAsync("POST","player/bootstrap",new JObject{["language"]=lang},token)).ToString());
            public Task<PlayerBootstrapResponseDto> UpdateDisplayNameAsync(string n,CancellationToken t)=>throw new Exception("NOT_PART_OF_VALIDATION");
        }
        public static void Run() {
            if(!Application.dataPath.Replace('\\','/').Contains("/Validation/Generated/"))throw new Exception("ISOLATED_PROJECT_REQUIRED");
            if(string.IsNullOrEmpty(Output))throw new Exception("OUTPUT_REQUIRED");
            Directory.CreateDirectory(Output);ValidationNetworkPolicy.BeginIsolated();LocalizationAssets.ImportTablesOnly();
            SessionState.SetBool(Key,true);Register();EditorSceneManager.OpenScene(ClientEditorTools.ScenePath);EditorApplication.isPlaying=true;
        }
        [InitializeOnLoadMethod]static void Register(){if(!SessionState.GetBool(Key,false))return;deadline=EditorApplication.timeSinceStartup+240;Application.logMessageReceived+=Log;EditorApplication.update-=Tick;EditorApplication.update+=Tick;}
        static void Tick(){if(!SessionState.GetBool(Key,false))return;if(EditorApplication.timeSinceStartup>deadline){Finish(false,"TIMEOUT");return;}if(!running&&EditorApplication.isPlaying&&DominoLocalization.Ready&&UnityEngine.Object.FindFirstObjectByType<Domino.Client.DominoClientController>()?.Menu){running=true;_=Validate();}}
        static void Log(string m,string s,LogType t){if(t==LogType.Error||t==LogType.Exception||t==LogType.Assert)Finish(false,m+"\n"+s);}
        static void Check(bool value,string name){checks++;if(!value)throw new Exception(name);}
        static async Task Frames(int n=8){int target=Time.frameCount+n;while(Time.frameCount<target)await Task.Delay(30);}
        static void Resize(int w,int h)=>typeof(Phase1Validation).GetMethod("ResizeGameView",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,new object[]{w,h});
        static async Task Validate() {
            try {
                var api=new Api();typeof(ApplicationServices).GetProperty("OnlineApi").SetValue(null,api);
                var menu=UnityEngine.Object.FindFirstObjectByType<Domino.Client.DominoClientController>().Menu;
                foreach(string state in new[]{"FREE","TRIAL","PREMIUM","EXPIRED"}) {
                    await api.SendAsync("POST","validation/scenario/"+state,null,CancellationToken.None);
                    using var player=new PlayerService(new Identity(),api,()=>Task.FromResult("en"),CancellationToken.None);
                    await player.InitializeAsync();Check(player.IsFresh,"BOOTSTRAP_"+state);
                    Check(player.Entitlements?.availability=="AVAILABLE","ENTITLEMENTS_"+state);
                    menu.Profile.Bind(player);
                    foreach(string lang in new[]{"en","es"})foreach(var size in new[]{new Vector2Int(1080,1920),new Vector2Int(1536,2048)}) {
                        DominoLocalization.Select(lang);Resize(size.x,size.y);menu.Profile.Open();await Frames();
                        var label=menu.Profile.GetComponent<EntitlementProfilePresentation>().DisplayedText;
                        Check(!string.IsNullOrEmpty(label),"PROFILE_TEXT");
                        Check(label.Contains(state=="FREE"?(lang=="es"?"Gratis":"Free"):state=="EXPIRED"?(lang=="es"?"terminó":"ended"):"Premium"),"PROFILE_"+state);
                        if(state=="TRIAL")Check(label.Contains(lang=="es"?"Sin pago":"No payment"),"NO_PAYMENT_PROMISE");
                        ScreenCapture.CaptureScreenshot(Path.Combine(Output,"profile-"+state+"-"+lang+"-"+size.x+".png"));await Frames();menu.Profile.Close();
                    }
                    menu.MainPlay.onClick.Invoke();await Frames();
                    Check(menu.VisibleModeKeys.Contains("DUEL_1V1"),"PUBLIC_DUEL_VISIBLE");
                    Check(menu.VisibleModeKeys.Contains("PARTNERS_2V2_ONLINE"),"PUBLIC_PARTNERS_VISIBLE");menu.Back.onClick.Invoke();await Frames();
                    menu.GetComponentsInChildren<Button>().Single(b=>b.name=="History").onClick.Invoke();await Frames(20);
                    var view=UnityEngine.Object.FindFirstObjectByType<HistoryReplayView>();Check(view,"HISTORY_VIEW");
                    double historyDeadline=EditorApplication.timeSinceStartup+20;
                    while(!view.GetComponentsInChildren<Button>().Any(b=>b.name=="View match")&&EditorApplication.timeSinceStartup<historyDeadline)await Frames();
                    var buttons=view.GetComponentsInChildren<Button>().Where(b=>b.name=="View match").ToArray();
                    bool free=state=="FREE"||state=="EXPIRED";
                    Check(buttons.Length==(free?10:20),"HISTORY_LIMIT_"+state+" count="+buttons.Length+" message="+view.GetComponentsInChildren<Text>().Single(t=>t.name=="Message").text);
                    Check(buttons.Count(b=>b.GetComponentInChildren<Text>().text==DominoLocalization.Get("premium.locked"))==(free?7:0),"REPLAY_LOCK_"+state);
                    if(free){buttons.Last().onClick.Invoke();await Frames();Check(view.GetComponentsInChildren<Text>().Single(t=>t.name=="Message").text==DominoLocalization.Get("premium.replay_limited"),"LOCK_EXPLANATION");}
                    ScreenCapture.CaptureScreenshot(Path.Combine(Output,"history-"+state+".png"));await Frames();
                    view.GetComponentsInChildren<Button>().Single(b=>b.name=="Back").onClick.Invoke();await Frames();
                }
                Check(ValidationNetworkPolicy.NetworkEntrypoints==0,"NO_REAL_SERVICES");
                Finish(true,"CHECKS="+checks+" STATES=FREE,TRIAL,PREMIUM,EXPIRED LANGUAGES=en,es PORTRAIT=PASS LOOPBACK_HTTP=YES CONSOLE_ERRORS=0 REAL_FIRESTORE_CALLS=0");
            }catch(Exception e){Finish(false,e.ToString());}
        }
        static void Finish(bool pass,string detail){if(!SessionState.GetBool(Key,false))return;SessionState.SetBool(Key,false);File.WriteAllText(Path.Combine(Output,"unity-result.txt"),(pass?"PASS":"FAIL")+"\n"+detail);EditorApplication.Exit(pass?0:1);}
    }
}
#endif
