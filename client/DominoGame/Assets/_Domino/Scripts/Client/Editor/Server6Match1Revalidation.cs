#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Domino.Client;
using Domino.Infrastructure;
using Domino.Infrastructure.Api;
using Domino.Realtime;
using Domino.Replay;
using Domino.UI;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
namespace Domino.Editor {
 public static class Server6Match1Revalidation {
  const BindingFlags Fields=BindingFlags.Instance|BindingFlags.NonPublic;
  static string Dir=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../Validation/Generated/S606R"));
  static bool running;static string stage;static int errors;
  static void Require(bool b,string c){if(!b)throw new InvalidOperationException(c);}
  static void Record(string s)=>File.AppendAllText(Path.Combine(Dir,"revalidation.txt"),s+"\n");
  static T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,Fields).GetValue(o);
  static void Log(string s,string stack,LogType t){if(t==LogType.Error||t==LogType.Exception||t==LogType.Assert)errors++;}
  [MenuItem("Domino/Validation/SERVER-6/S6-06R Revalidate existing Match 1 only")]
  public static async void Run(){
   if(running||!Application.isPlaying)return;
   Directory.CreateDirectory(Dir);if(File.Exists(Path.Combine(Dir,"revalidation.txt"))){Debug.LogWarning("[S606R] Evidence exists; review required.");return;}
   running=true;errors=0;Application.logMessageReceived+=Log;
   try{
    stage="SCOPE";
    var settings=Resources.Load<DominoApiSettings>("ApiSettings");Require(settings.Environment=="TEST"&&settings.Configuration.Endpoint.Host=="domino-api-test.teamfho.com","TEST_REQUIRED");
    Require(ApplicationServices.Player.IsFresh&&ApplicationServices.Realtime.State==RealtimeConnectionState.CONNECTED,"SESSION_NOT_READY");
    var auth=global::Firebase.Auth.FirebaseAuth.DefaultInstance;string uid=auth.CurrentUser?.UserId;
    Require(!string.IsNullOrWhiteSpace(uid)&&uid==ApplicationServices.Identity.Current.Uid,"CURRENT_IDENTITY");
    string id=File.ReadAllText(Path.Combine(Dir,"../SERVER6-match1/match-1.txt")).Trim();
    var identities=Server6ParticipantValidator.ReadEvidence(JObject.Parse(File.ReadAllText(Path.Combine(Dir,"identity-seats.json"))),id);
    using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(5));var ct=timeout.Token;
    var api=(IDominoApiClient)typeof(Domino.Player.PlayerService).GetField("api",Fields).GetValue(ApplicationServices.Player);
    var bootstrap=await api.BootstrapAsync("es",ct);
    Require(bootstrap.player.uid==uid&&auth.CurrentUser?.UserId==uid,"CURRENT_IDENTITY_CHANGED");
    Require(bootstrap.entitlements.snapshot.trialActive&&bootstrap.entitlements.snapshot.limits.REPLAY_MAX.unlimited,"REPLAY_ENTITLEMENT");
    Record("MATCH_1_REUSED_FOR_REVALIDATION=YES\nCURRENT_USER_PLAN=PREMIUM_TRIAL\nCURRENT_USER_REPLAY_LIMIT=UNLIMITED\nNEW_MATCHES_STARTED=0");
    stage="PARTICIPANTS";
    var authority=await ApplicationServices.OnlineApi.SendAsync("GET","matches/"+id+"/snapshot",null,ct);
    int self=(int)authority["privateState"]["seat"];identities.Add("UNITY_USER",self);
    var replay=new ReplayClient(ApplicationServices.OnlineApi);var page=await replay.History(null,ct);
    var items=(JArray)page["items"];var item=(JObject)items.Single(x=>(string)x["history"]["matchId"]==id);
    var manifest=await replay.Manifest(id,ct);
    Server6ParticipantValidator.Validate(authority,item,manifest,Server6ParticipantValidator.Match1Expected,identities,self);
    Require((bool?)manifest["replayAvailable"]==true,"REPLAY_UNAVAILABLE");
    Record("MATCH_1_PARTICIPANT_VALIDATION=PASS\nMATCH_1_REPLAY_AVAILABLE=YES\nMATCH_1_REPLAY_AUTHORIZATION=PASS");
    stage="HISTORY";var h=item["history"];
    Require((string)authority["publicState"]["status"]=="FINISHED","NOT_FINISHED");
    Require(JToken.DeepEquals(h["score"],manifest["finalScore"])&&JToken.DeepEquals(h["score"],authority["publicState"]["scores"]),"HISTORY_SCORE");
    Require(JToken.DeepEquals(h["score"],new JArray(217,72))&&(string)h["result"]=="WIN","EXPECTED_RESULT");
    Require((string)h["finishReason"]=="TARGET_REACHED"&&(string)manifest["result"]["finishReason"]=="TARGET_REACHED","FINISH_REASON");
    Require(DateTimeOffset.TryParse(h["finishedAt"].ToString(),out _),"FINISHED_AT");
    int team=Array.FindIndex(((JArray)manifest["teams"]).ToArray(),t=>t.Values<int>().Contains(self));
    Require((int?)manifest["result"]["winner"]["index"]==team,"WINNER");
    Require(items.Count==1&&page["nextCursor"]?.Type!=JTokenType.String,"HISTORY_COUNT");
    Record("MATCH_1_HISTORY_CONSISTENCY=PASS\nHISTORY_COUNT_AFTER_MATCH_1=1\nMATCH_1_RESULT=WIN\nMATCH_1_SCORE=217-72\nMATCH_1_COMPLETION_REASON=TARGET_REACHED");
    stage="RECONSTRUCTION";var timeline=await replay.Load(manifest,ct);var terminal=timeline.Seek(timeline.Count).Data;var pub=authority["publicState"];
    Require((string)terminal["phase"]=="MATCH_FINISHED"&&JToken.DeepEquals(terminal["scores"],pub["scores"])&&JToken.DeepEquals(terminal["board"],pub["board"])&&(int)terminal["round"]==(int)pub["currentRound"]&&JToken.DeepEquals(terminal["counts"],pub["tilesRemainingPerSeat"]),"TERMINAL_STATE");
    Require(JToken.DeepEquals(terminal["matchResult"],manifest["result"])&&(long)terminal["sequence"]==(long)pub["lastSequence"],"TERMINAL_RESULT_SEQUENCE");
    var terminalManifest=(JObject)manifest.DeepClone();terminalManifest["participants"]=terminal["participants"].DeepClone();Server6ParticipantValidator.Validate(authority,item,terminalManifest,Server6ParticipantValidator.Match1Expected,identities,self);
    Require(auth.CurrentUser?.UserId==uid,"CURRENT_IDENTITY_CHANGED");
    Record("MATCH_1_REPLAY_RECONSTRUCTION=PASS\nMATCH_1_REPLAY_CONSISTENCY=PASS\nMATCH_1_EVENT_SEQUENCE_REGRESSION=NO\nMATCH_1_REPLAY_SEQUENCE_REGRESSION=NO\nEVENT_COUNT="+timeline.Count);
    // Save detached API evidence without accessSession or raw identity values.
    var safeManifest=(JObject)manifest.DeepClone();safeManifest.Remove("accessSession");
    File.WriteAllText(Path.Combine(Dir,"validated-metadata.json"),new JObject{["manifest"]=safeManifest,["history"]=item,["authority"]=authority,["terminal"]=terminal}.ToString());
    stage="UNITY_HISTORY";
    var local=UnityEngine.Object.FindFirstObjectByType<DominoClientController>();Require(local&&local.Menu,"MENU_REQUIRED");
    var tiles=Field<DominoTileView>(local,"tilePrefab");var players=Field<PlayerView>(local,"playerPrefab");
    local.Menu.Show(StartScreen.Match);var hv=new GameObject("S606R existing Match History",typeof(RectTransform)).AddComponent<HistoryReplayView>();
    hv.Initialize(replay,tiles,players,()=>local.Menu.Show(StartScreen.MainMenu));
    async Task Until(Func<bool> ready){while(!ready()){ct.ThrowIfCancellationRequested();Require(Application.isPlaying&&auth.CurrentUser?.UserId==uid,"SESSION_CHANGED");await Task.Delay(150);}await Task.Delay(500);}
    await Until(()=>!Field<bool>(hv,"loading"));
    Require(Field<System.Collections.Generic.List<JObject>>(hv,"history").Count==1,"HISTORY_UI_COUNT");
    Record("UNITY_HISTORY_ENTRIES_VISIBLE_AFTER_MATCH_1=1");ScreenCapture.CaptureScreenshot(Path.Combine(Dir,"history.png"));await Task.Delay(1000);
    stage="UNITY_REPLAY";hv.GetComponentsInChildren<Button>().Single(x=>x.name=="View match").onClick.Invoke();
    await Until(()=>!Field<bool>(hv,"loading")&&Field<JObject>(hv,"manifest")!=null);
    hv.LoadReplay();await Until(()=>hv.Timeline!=null&&hv.Board);
    Record("UNITY_MATCH_1_REPLAY_OPEN=PASS\nCONSOLE_ERRORS="+errors);ScreenCapture.CaptureScreenshot(Path.Combine(Dir,"replay.png"));
    Require(errors==0,"UNITY_ERRORS");Record("S6_06R_REVALIDATION=PASS");Debug.Log("[S606R] Existing Match 1 revalidation PASS; no matchmaking.");
   }catch(Exception e){Record("S6_06R_REVALIDATION=STOP\nFAILED_STAGE="+stage+"\nERROR_TYPE="+e.GetType().Name+"\nCONSOLE_ERRORS="+errors);Debug.LogWarning("[S606R] Stopped at "+stage);}
   finally{Application.logMessageReceived-=Log;running=false;}
  }
 }
}
#endif
