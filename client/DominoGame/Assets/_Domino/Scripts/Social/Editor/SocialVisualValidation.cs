#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Domino.Identity;
using Domino.Infrastructure;
using Domino.Online;
using Domino.Social;
using Domino.UI;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Domino.Editor
{
    public static class SocialVisualValidation
    {
        const string Key="Domino.S11.Validation";static bool running,finished;static int checks;static double deadline;
        static string Output=>Environment.GetEnvironmentVariable("DOMINO_S11_OUTPUT");
        sealed class Identity:IPlayerIdentityService {
            public IdentityState State=>IdentityState.Ready;public PlayerIdentity Current=>new PlayerIdentity("s11-local-viewer",true);
            public Exception Error=>null;public Task<PlayerIdentity> InitializeAsync()=>Task.FromResult(Current);
        }
        sealed class Api:IOnlineMatchApi {
            readonly string bearer; public Api(string bearer="s11-loopback"){this.bearer=bearer;}
            public async Task<JObject> SendAsync(string method,string path,JObject body,CancellationToken token) {
                if(path.Contains(":")||path.StartsWith("/")||path.Contains(".."))throw new Exception("INVALID_TEST_PATH");
                using var http=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false});http.Timeout=TimeSpan.FromSeconds(15);
                http.DefaultRequestHeaders.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",bearer);
                using var request=new HttpRequestMessage(new HttpMethod(method),"http://127.0.0.1:18089/api/v1/"+path);
                if(body!=null)request.Content=new StringContent(body.ToString(),System.Text.Encoding.UTF8,"application/json");
                using var result=await http.SendAsync(request,token);var json=JObject.Parse(await result.Content.ReadAsStringAsync());
                if(!result.IsSuccessStatusCode)throw new SocialException((string)json["code"]??"SOCIAL_SERVICE_UNAVAILABLE");return json;
            }
        }
        sealed class DeferredApi:IOnlineMatchApi {
            public readonly TaskCompletionSource<JObject> Pending=new TaskCompletionSource<JObject>();
            public Task<JObject> SendAsync(string m,string p,JObject b,CancellationToken t)=>Pending.Task;
        }
        public static void ValidateEditMode() {
            if(!Application.dataPath.Replace('\\','/').Contains("/Validation/Generated/"))throw new Exception("ISOLATED_PROJECT_REQUIRED");
            ValidationNetworkPolicy.BeginIsolated();LocalizationAssets.ImportTablesOnly();int count=0;
            var source=JsonUtility.FromJson<LocalizationAssets.Source>(File.ReadAllText("Assets/_Domino/Editor/Localization/Translations.json"));
            foreach(var language in new[]{"en","es"}) {
                var table=AssetDatabase.LoadAssetAtPath<UnityEngine.Localization.Tables.StringTable>("Assets/_Domino/Localization/Tables/Domino UI_"+language+".asset");
                foreach(var row in source.entries.Where(e=>e.key.StartsWith("social.",StringComparison.Ordinal))) {
                    var entry=table.GetEntry(row.key);if(entry==null||entry.Value!=(language=="en"?row.en:row.es))throw new Exception("TRANSLATION_"+row.key);count++;
                }
            }
            File.WriteAllText(Path.Combine(Output,"editmode-result.txt"),"UNITY_EDITMODE_CHECKS="+count+" PASS\nREAL_FIRESTORE_CALLS=0");
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
        static async Task Frames(int n=5){int frame=Time.frameCount+n;while(Time.frameCount<frame)await Task.Delay(30);}
        static async Task Ready(SocialView view){double until=EditorApplication.timeSinceStartup+20;while(view&&view.Busy&&EditorApplication.timeSinceStartup<until)await Frames();Check(view&&!view.Busy,"REQUEST_FINISHED");await Frames();}
        static Button Find(SocialView v,string name)=>v.GetComponentsInChildren<Button>().FirstOrDefault(b=>b.name==name)??throw new Exception("MISSING_BUTTON="+name+" AVAILABLE="+string.Join(",",v.GetComponentsInChildren<Button>().Select(b=>b.name))+" TEXT="+string.Join("|",v.GetComponentsInChildren<Text>().Select(t=>t.text)));
        static void Resize(int w,int h)=>typeof(Phase1Validation).GetMethod("ResizeGameView",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,new object[]{w,h});
        static async Task Validate() {
            try {
                var deferred=new DeferredApi();string uid="a";var scoped=new SocialClient(deferred,()=>uid);var pending=scoped.Summary(CancellationToken.None);uid="b";deferred.Pending.SetResult(new JObject());
                try{await pending;throw new Exception("ACCOUNT_RESULT_LEAK");}catch(OperationCanceledException){Check(true,"ACCOUNT_SWITCH");}
                foreach(string invalid in new[]{"a","ab","abc@","abcdefghijklmnopq"}){try{await scoped.Search(invalid,null,CancellationToken.None);throw new Exception("QUERY_ACCEPTED");}catch(SocialException){Check(true,"QUERY_VALIDATION");}}
                var api=new Api();typeof(ApplicationServices).GetProperty("SocialApi").SetValue(null,api);typeof(ApplicationServices).GetProperty("Identity").SetValue(null,new Identity());
                var menu=UnityEngine.Object.FindFirstObjectByType<Domino.Client.DominoClientController>().Menu;
                await FriendsFlow();
                foreach(string lang in new[]{"en","es"})foreach(var size in new[]{new Vector2Int(1080,1920),new Vector2Int(1080,2400),new Vector2Int(1536,2048)}) {
                    DominoLocalization.Select(lang);Resize(size.x,size.y);menu.GetComponentsInChildren<Button>().Single(b=>b.name=="Social").onClick.Invoke();await Frames();
                    var view=UnityEngine.Object.FindFirstObjectByType<SocialView>();await Ready(view);
                    Check(view.GetComponent<SocialAccessibility>().NodeCount>0,"ACCESSIBILITY_LABELS");
                    Check(view.GetComponentsInChildren<Text>().Single(t=>t.name=="Own code").text.Contains("FHO-"),"OWN_CODE");
                    Find(view,"Copy").onClick.Invoke();Check(GUIUtility.systemCopyBuffer.StartsWith("FHO-"),"COPY");
                    view.SearchInput.text="Alice";Find(view,"Search submit").onClick.Invoke();await Ready(view);Check(view.ResultCount==20,"SEARCH_PAGE");
                    Find(view,"More").onClick.Invoke();await Ready(view);Check(view.ResultCount==25,"SEARCH_MORE");
                    ScreenCapture.CaptureScreenshot(Path.Combine(Output,"search-"+lang+"-"+size.x+"x"+size.y+".png"));await Frames();
                    Find(view,"View profile").onClick.Invoke();await Ready(view);Check(Find(view,"Block player"),"PUBLIC_PROFILE");
                    Find(view,"Block player").onClick.Invoke();Check(Find(view,"Confirm block"),"BLOCK_CONFIRM");Find(view,"Confirm block").onClick.Invoke();await Ready(view);Check(view.ResultCount==0,"BLOCK_CLEAR");
                    view.SearchInput.text="FHO-000000000001";Find(view,"Search submit").onClick.Invoke();await Ready(view);Check(view.ResultCount==0,"BLOCK_HIDDEN");
                    Find(view,"Blocked tab").onClick.Invoke();await Ready(view);Check(view.ResultCount==1,"BLOCK_LIST");Find(view,"Unblock").onClick.Invoke();await Ready(view);Check(view.ResultCount==0,"UNBLOCK");
                    Find(view,"Search tab").onClick.Invoke();view.SearchInput.text="FHO-000000000001";Find(view,"Search submit").onClick.Invoke();await Ready(view);Check(view.ResultCount==1,"CODE_SEARCH");
                    view.SearchInput.text="Zzz";Find(view,"Search submit").onClick.Invoke();await Ready(view);Check(view.ResultCount==0,"EMPTY");
                    view.SearchInput.text="ab";Find(view,"Search submit").onClick.Invoke();await Ready(view);Check(Find(view,"Retry"),"ERROR_RETRY");
                    Find(view,"Privacy tab").onClick.Invoke();await Ready(view);string before=Find(view,"Discoverable toggle").GetComponentInChildren<Text>().text;
                    Find(view,"Discoverable toggle").onClick.Invoke();await Ready(view);Check(before!=Find(view,"Discoverable toggle").GetComponentInChildren<Text>().text,"PRIVACY_TOGGLE");
                    ScreenCapture.CaptureScreenshot(Path.Combine(Output,"privacy-"+lang+"-"+size.x+"x"+size.y+".png"));await Frames();view.Close();await Frames();
                }
                Check(ValidationNetworkPolicy.NetworkEntrypoints==0,"NO_REAL_SERVICES");Finish(true,"REAL_FIRESTORE_CALLS=0");
            }catch(Exception e){Finish(false,e.ToString());}
        }
        static async Task FriendsFlow() {
            Resize(1080,1920);await Frames();
            var apiA=new Api();var apiB=new Api("s12-loopback-b");var a=new SocialClient(apiA,()=>"s11-local-viewer");var b=new SocialClient(apiB,()=>"local-social-1");
            var ownA=await a.Summary(CancellationToken.None);var ownB=await b.Summary(CancellationToken.None);
            Check((string)ownA["profile"]["publicPlayerId"]!=(string)ownB["profile"]["publicPlayerId"],"TWO_DISTINCT_IDENTITIES");
            async Task<SocialView> Open(SocialClient client){var v=new GameObject("S12 controlled social").AddComponent<SocialView>();v.Initialize(client,()=>{});await Ready(v);return v;}
            var view=await Open(a);view.SearchInput.text=(string)ownB["profile"]["friendCode"];Find(view,"Search submit").onClick.Invoke();await Ready(view);Find(view,"View profile").onClick.Invoke();await Ready(view);
            Check(Find(view,"Add friend").interactable,"ADD_ENABLED");Find(view,"Add friend").onClick.Invoke();Find(view,"Add friend").onClick.Invoke();await Ready(view);Check(Find(view,"Cancel request"),"OUTGOING_STATE");view.Close();await Frames();
            view=await Open(b);Find(view,"Requests tab").onClick.Invoke();await Ready(view);Check(view.ResultCount==1,"B_INCOMING");Find(view,"Accept request").onClick.Invoke();await Ready(view);Find(view,"Friends tab").onClick.Invoke();await Ready(view);Check(view.ResultCount==1,"B_FRIEND");view.Close();await Frames();
            view=await Open(a);Find(view,"Friends tab").onClick.Invoke();await Ready(view);Check(view.ResultCount==1,"A_FRIEND");
            ScreenCapture.CaptureScreenshot(Path.Combine(Output,"friends-en.png"));await Frames();
            Find(view,"Remove friend").onClick.Invoke();Find(view,"Confirm remove").onClick.Invoke();await Ready(view);Check(view.ResultCount==0,"UNFRIEND");
            await b.AddFriend((string)ownA["profile"]["publicPlayerId"],CancellationToken.None);
            Find(view,"Requests tab").onClick.Invoke();await Ready(view);Find(view,"Accept request").onClick.Invoke();await Ready(view);Find(view,"Friends tab").onClick.Invoke();await Ready(view);Find(view,"View profile").onClick.Invoke();await Ready(view);
            Find(view,"Block player").onClick.Invoke();Find(view,"Confirm block").onClick.Invoke();await Ready(view);
            Check(((JArray)(await b.Friends(null,CancellationToken.None))["items"]).Count==0,"BLOCK_REMOVES_B_FRIEND");
            await a.Block((string)ownB["profile"]["publicPlayerId"],false,CancellationToken.None);
            await b.AddFriend((string)ownA["profile"]["publicPlayerId"],CancellationToken.None);
            await a.Block((string)ownB["profile"]["publicPlayerId"],true,CancellationToken.None);
            Check(((JArray)(await b.Requests(false,null,CancellationToken.None))["items"]).Count==0,"BLOCK_CANCELS_PENDING");
            await a.Block((string)ownB["profile"]["publicPlayerId"],false,CancellationToken.None);
            Find(view,"Search tab").onClick.Invoke();view.SearchInput.text=(string)ownB["profile"]["friendCode"];Find(view,"Search submit").onClick.Invoke();await Ready(view);Find(view,"View profile").onClick.Invoke();await Ready(view);Find(view,"Add friend").onClick.Invoke();await Ready(view);Find(view,"Cancel request").onClick.Invoke();await Ready(view);Check(Find(view,"Add friend"),"CANCELED_STATE");view.Close();await Frames();
            var sent=await a.AddFriend((string)ownB["profile"]["publicPlayerId"],CancellationToken.None);var cross=await b.AddFriend((string)ownA["profile"]["publicPlayerId"],CancellationToken.None);Check((string)sent["outgoingRequestId"]==(string)cross["incomingRequestId"],"CROSS_NO_AUTO_ACCEPT");
            view=await Open(b);Find(view,"Requests tab").onClick.Invoke();await Ready(view);Find(view,"Decline request").onClick.Invoke();await Ready(view);Check(view.ResultCount==0,"DECLINE");view.Close();await Frames();
            foreach(var lang in new[]{"en","es"}) {
                DominoLocalization.Select(lang);var limited=new SocialClient(new Api("s12-loopback-limit"),()=>"local-social-2");view=await Open(limited);
                Find(view,"Friends tab").onClick.Invoke();await Ready(view);Check(view.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("20 / 5")),"DOWNGRADE_LIMIT_VISIBLE");
                ScreenCapture.CaptureScreenshot(Path.Combine(Output,"limit-"+lang+".png"));await Frames();
                Find(view,"Search tab").onClick.Invoke();view.SearchInput.text=(string)ownB["profile"]["friendCode"];Find(view,"Search submit").onClick.Invoke();await Ready(view);Find(view,"View profile").onClick.Invoke();await Ready(view);Check(!Find(view,"Add friend").interactable,"LIMIT_DISABLED_ADD");view.Close();await Frames();
            }
        }
        static void Finish(bool pass,string reason){if(finished)return;finished=true;SessionState.EraseBool(Key);EditorApplication.update-=Tick;Application.logMessageReceived-=Log;
            File.WriteAllText(Path.Combine(Output,"unity-result.txt"),"PASS="+pass+"\nCHECKS="+checks+"\n"+reason);EditorApplication.Exit(pass?0:1);}
    }
}
#endif
