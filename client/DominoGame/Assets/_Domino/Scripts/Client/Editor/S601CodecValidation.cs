using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Domino.Identity;
using Domino.Infrastructure.Api;
using Domino.Player;
using Newtonsoft.Json.Linq;
namespace Domino.Editor {
 public static class S601CodecValidation {
  static void Check(bool value,string name){if(!value)throw new Exception(name);}
  public static JObject Payload() => JObject.Parse(@"{'player':{'uid':'fixture-user','accountType':'GUEST','displayName':'Fixture','language':'es','status':'ACTIVE'},'wallet':{'coins':123},'entitlements':{'availability':'AVAILABLE','trialGranted':false,'snapshot':{'plan':'FREE','status':'FREE','trialActive':false,'trialConsumed':true,'sources':[],'features':['PUBLIC_DUEL','PUBLIC_PARTNERS'],'limits':{'FRIENDS_MAX':{'unlimited':false,'maximum':5},'HISTORY_MAX':{'unlimited':false,'maximum':10},'REPLAY_MAX':{'unlimited':false,'maximum':3}},'revision':7,'policyVersion':1,'serverTime':'2026-09-26T00:00:00Z'}}}");
  sealed class Session:IPlayerIdentityService,IAuthTokenProvider {
   public IdentityState State=>IdentityState.Ready;public PlayerIdentity Current=>new PlayerIdentity("fixture-user",true);public Exception Error=>null;
   public Task<PlayerIdentity> InitializeAsync()=>Task.FromResult(Current);public Task<string> GetIdTokenAsync(bool refresh,CancellationToken ct)=>Task.FromResult("fixture-only");
  }
  sealed class Transport:IApiTransport {public string Json; public Task<ApiHttpResponse> SendAsync(string method,Uri uri,string body,string token,int timeout,CancellationToken ct)=>Task.FromResult(new ApiHttpResponse(200,Json));}
  public static async Task<int> Checks(){int count=0;var codec=new UnityApiJsonCodec();
   foreach(var scenario in new[]{"FREE","PREMIUM","TRIAL"}) {
    var root=Payload();var e=(JObject)root["entitlements"];var s=(JObject)e["snapshot"];
    if(scenario!="FREE") {s["plan"]="PREMIUM";s["status"]="ACTIVE";s["features"]=new JArray("FULL_HISTORY","FULL_REPLAY");s["limits"]["REPLAY_MAX"]=new JObject{["unlimited"]=true,["maximum"]=null};s["validUntil"]="2026-10-01T00:00:00Z";}
    if(scenario=="TRIAL"){s["trialActive"]=true;s["trialEndsAt"]="2026-10-01T00:00:00Z";s["sources"]=new JArray("PROMOTIONAL_TRIAL");e["trialGranted"]=true;}
    var dto=codec.ReadSuccess(root.ToString());Check(dto.entitlements!=null,scenario+"_ENTITLEMENTS_DROPPED");
    Check(dto.entitlements.snapshot.plan==(string)s["plan"]&&dto.entitlements.snapshot.revision==7&&dto.entitlements.snapshot.limits.REPLAY_MAX.unlimited==(scenario!="FREE"),scenario+"_MAPPING");
    Check(dto.entitlements.snapshot.trialActive==(scenario=="TRIAL")&&dto.entitlements.trialGranted==(scenario=="TRIAL"),scenario+"_TRIAL");
    Check(dto.player.uid=="fixture-user"&&dto.wallet.coins==123,"PLAYER_WALLET");
    var session=new Session();var api=new DominoApiClient(new DominoApiConfiguration(true,"https://fixture.invalid",15),session,new Transport{Json=root.ToString()},codec);
    using(var player=new PlayerService(session,api,()=>Task.FromResult("es"),CancellationToken.None)){await player.InitializeAsync();Check(player.IsFresh&&player.Entitlements?.snapshot?.plan==dto.entitlements.snapshot.plan,scenario+"_STATE");Check(player.Entitlements.snapshot.limits.REPLAY_MAX.maximum==(scenario=="FREE"?3:0),scenario+"_REPLAY_LIMIT");}
    count++;
   }
   foreach(var variant in new[]{"missing","null","empty","future","unavailable"}) {
    var root=Payload();if(variant=="missing")root.Remove("entitlements");if(variant=="null")root["entitlements"]=null;if(variant=="empty")root["entitlements"]=new JObject();if(variant=="future")root["entitlements"]["futureField"]=new JObject{["opaque"]=true};if(variant=="unavailable")root["entitlements"]=new JObject{["availability"]="UNAVAILABLE"};
    var e=codec.ReadSuccess(root.ToString()).entitlements;
    Check(variant=="future"?e?.snapshot?.limits?.REPLAY_MAX?.maximum==3:e?.snapshot==null,variant+"_SAFE");count++;
   }
   var invalid=Payload();invalid["entitlements"]=new JArray(1,2);bool rejected=false;try{codec.ReadSuccess(invalid.ToString());}catch{rejected=true;}Check(rejected,"MALFORMED_ACCEPTED");count++;
   return count;
  }
#if UNITY_EDITOR
  const string Key="S601.Validation";static bool running;static int errors;static double deadline;
  static string Output=>System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath,"../../Validation/Generated/S601"));
  static void Record(string value)=>System.IO.File.AppendAllText(System.IO.Path.Combine(Output,"results.txt"),value+"\n");
  public static async void RunEdit(){try{Record("EDITMODE="+await Checks()+"_PASS");Start(false);}catch(Exception e){Record("EDITMODE=FAIL_"+e.GetType().Name);UnityEditor.EditorApplication.Exit(1);}}
  public static void RunRemote()=>Start(true);
  static void Start(bool remote){if(remote)Domino.Infrastructure.ValidationNetworkPolicy.AuthorizeReal();else Domino.Infrastructure.ValidationNetworkPolicy.BeginIsolated();UnityEditor.SessionState.SetBool(Key,true);UnityEditor.SessionState.SetBool(Key+"Remote",remote);Register();UnityEditor.SceneManagement.EditorSceneManager.OpenScene(Domino.Editor.ClientEditorTools.ScenePath);UnityEditor.EditorApplication.isPlaying=true;}
  [UnityEditor.InitializeOnLoadMethod]static void Register(){if(!UnityEditor.SessionState.GetBool(Key,false))return;deadline=UnityEditor.EditorApplication.timeSinceStartup+180;UnityEngine.Application.logMessageReceived+=(m,s,t)=>{if(t==UnityEngine.LogType.Error||t==UnityEngine.LogType.Exception||t==UnityEngine.LogType.Assert)errors++;};UnityEditor.EditorApplication.update-=Tick;UnityEditor.EditorApplication.update+=Tick;}
  static void Tick(){if(!UnityEditor.SessionState.GetBool(Key,false))return;if(UnityEditor.EditorApplication.timeSinceStartup>deadline){End(false,"TIMEOUT");return;}bool remote=UnityEditor.SessionState.GetBool(Key+"Remote",false);if(!running&&UnityEngine.Application.isPlaying&&(!remote||Domino.Infrastructure.ApplicationServices.Player?.IsFresh==true)){running=true;_=Validate(remote);}}
  static async Task Validate(bool remote){try{
   if(!remote){Record("PLAYMODE_CODEC="+await Checks()+"_PASS");foreach(var mode in new[]{"DUEL_1V1","PARTNERS_2V2_ONLINE"}){var f=JObject.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(Output,"../I4Tests/"+mode+".json")));var timeline=new Domino.Replay.ReplayTimeline((JObject)f["manifest"],((JArray)f["events"]).Cast<JObject>());Check((string)timeline.Seek(timeline.Count).Data["phase"]=="MATCH_FINISHED","REPLAY_TERMINAL");}Record("PLAYMODE_REPLAY=2_PASS");}
   else {
    var app=Domino.Infrastructure.ApplicationServices.Player;var e=app.Entitlements;Check(global::Firebase.FirebaseApp.DefaultInstance.Options.ProjectId=="teamfho-domino","PROJECT");Check(e?.availability=="AVAILABLE"&&e.snapshot?.limits?.REPLAY_MAX!=null,"REMOTE_STATE_MISSING");
    Record("REMOTE_BOOTSTRAP_ENTITLEMENTS=PASS\nPLAN="+e.snapshot.plan+"\nREPLAY_MAX="+e.snapshot.limits.REPLAY_MAX.maximum+"\nREPLAY_UNLIMITED="+e.snapshot.limits.REPLAY_MAX.unlimited);
    var sdk=(Domino.Infrastructure.Firebase.IFirebaseClient)Activator.CreateInstance(typeof(Domino.Infrastructure.ApplicationServices).Assembly.GetType("Domino.Infrastructure.Firebase.FirebaseSdkClient",true),new object[]{new Func<PlayerIdentity>(()=>Domino.Infrastructure.ApplicationServices.Identity.Current)});sdk.InitializeApp();var token=await ((IAuthTokenProvider)sdk).GetIdTokenAsync(false,CancellationToken.None);
    var response=await new UnityApiTransport().SendAsync("POST",new Uri("https://domino-api-test.teamfho.com/api/v1/player/bootstrap"),"{\"language\":\"es\"}",token,15,CancellationToken.None);token=null;Check(response.Status==200,"WIRE_STATUS");var wire=JObject.Parse(response.Body);Check(wire["entitlements"] is JObject,"WIRE_ENTITLEMENTS");Check((string)wire["entitlements"]["snapshot"]["plan"]==e.snapshot.plan,"WIRE_STATE_PARITY");Record("WIRE_ENTITLEMENTS_PRESENT=YES\nWIRE_STATE_PARITY=PASS");
    System.IO.File.WriteAllText(System.IO.Path.Combine(Output,"sanitized-entitlements.json"),wire["entitlements"].ToString());
    var history=await new Domino.Replay.ReplayClient(Domino.Infrastructure.ApplicationServices.OnlineApi).History(null,CancellationToken.None);Record("HISTORY_COUNT="+((JArray)history["items"]).Count);
    Record("UNITY_FREE_REPLAY_ENTITLEMENT_STATE="+(e.snapshot.plan=="FREE"&&!e.snapshot.limits.REPLAY_MAX.unlimited&&e.snapshot.limits.REPLAY_MAX.maximum==3?"PASS":"NOT_FREE_CURRENT_USER"));
   }End(errors==0,"COMPLETE");
  }catch(Exception ex){End(false,ex.GetType().Name);}}
  static void End(bool ok,string reason){if(!UnityEditor.SessionState.GetBool(Key,false))return;UnityEditor.SessionState.SetBool(Key,false);Record("RESULT="+reason+"\nSUCCESS="+ok+"\nCONSOLE_ERRORS="+errors);Domino.Infrastructure.ValidationNetworkPolicy.EndValidation();UnityEditor.EditorApplication.Exit(ok?0:1);}
#endif
 }
}
