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
namespace Domino.Editor {
 public static class Server6FinalValidation {
  const BindingFlags Fields=BindingFlags.Instance|BindingFlags.NonPublic;
  static string Root=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../Validation/Generated"));
  static bool running;static int errors;
  static void Need(bool value,string code){if(!value)throw new InvalidOperationException(code);}
  static void Log(string message,string stack,LogType kind){if(kind==LogType.Error||kind==LogType.Exception||kind==LogType.Assert)errors++;}
  [MenuItem("Domino/Validation/SERVER-6/Final verify BEFORE API restart")]
  public static void Before()=>Run(false);
  [MenuItem("Domino/Validation/SERVER-6/Final verify AFTER API restart")]
  public static void After()=>Run(true);
  static async void Run(bool after){
   if(running||!Application.isPlaying)return;
   var dir=Path.Combine(Root,"SERVER6-final");Directory.CreateDirectory(dir);
   var evidence=Path.Combine(dir,after?"after-restart.txt":"before-restart.txt");
   if(File.Exists(evidence)){Debug.LogWarning("[SERVER6-FINAL] Evidence exists; no retry.");return;}
   running=true;errors=0;Application.logMessageReceived+=Log;string stage="SCOPE";
   void Record(string value)=>File.AppendAllText(evidence,value+"\n");
   try{
    Need(ApplicationServices.Player.IsFresh&&ApplicationServices.Realtime.State==RealtimeConnectionState.CONNECTED,"SESSION_REQUIRED");
    var settings=Resources.Load<DominoApiSettings>("ApiSettings");Need(settings.Environment=="TEST"&&settings.Configuration.Endpoint.Host=="domino-api-test.teamfho.com","TEST_REQUIRED");
    var uid=global::Firebase.Auth.FirebaseAuth.DefaultInstance.CurrentUser?.UserId;
    Need(!string.IsNullOrWhiteSpace(uid)&&ApplicationServices.Identity.Current.Uid==uid,"IDENTITY_REQUIRED");
    if(after)Need(File.Exists(Path.Combine(dir,"api-restart-healthy.txt")),"RESTART_EVIDENCE_REQUIRED");
    Need(File.ReadAllText(Path.Combine(Root,"S606R/revalidation.txt")).Contains("S6_06R_REVALIDATION=PASS"),"MATCH_1_GATE");
    for(int n=2;n<=5;n++)Need(File.ReadAllText(Path.Combine(Root,"SERVER6-match"+n+"/execution.txt")).Contains("MATCH_"+n+"_GATE=PASS"),"PRIOR_GATE");
    using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(8));var ct=timeout.Token;
    var api=(IDominoApiClient)typeof(Domino.Player.PlayerService).GetField("api",Fields).GetValue(ApplicationServices.Player);
    var bootstrap=await api.BootstrapAsync("es",ct);Need(bootstrap.player.uid==uid&&bootstrap.entitlements.snapshot.trialActive&&bootstrap.entitlements.snapshot.limits.REPLAY_MAX.unlimited,"ENTITLEMENTS");
    Record("CURRENT_USER_PLAN=PREMIUM_TRIAL\nCURRENT_USER_REPLAY_LIMIT=UNLIMITED");
    var replay=new ReplayClient(ApplicationServices.OnlineApi);stage="HISTORY";
    var page=await replay.History(null,ct);var items=(JArray)page["items"];
    Need(items.Count==5&&page["nextCursor"]?.Type!=JTokenType.String,"HISTORY_COUNT");
    DateTimeOffset previous=DateTimeOffset.MaxValue;
    for(int n=5;n>=1;n--){
     stage="MATCH_"+n;
     string id=File.ReadAllText(Path.Combine(Root,"SERVER6-match"+n+"/match-"+n+".txt")).Trim();
     var row=(JObject)items[5-n];Need((string)row["history"]["matchId"]==id,"HISTORY_ORDER");
     var stamp=row["history"]["finishedAt"];var date=stamp is JValue v&&v.Value is DateTime dt?new DateTimeOffset(dt):DateTimeOffset.Parse((string)stamp,System.Globalization.CultureInfo.InvariantCulture);
     Need(date<=previous,"TIMESTAMP_ORDER");previous=date;
     var authority=await ApplicationServices.OnlineApi.SendAsync("GET","matches/"+id+"/snapshot",null,ct);
     var manifest=await replay.Manifest(id,ct);Need((bool?)manifest["replayAvailable"]==true,"REPLAY_UNAVAILABLE");
     string proofPath=n==1?Path.Combine(Root,"S606R/identity-seats.json"):Path.Combine(Root,"SERVER6-match"+n+"/identity-seats-final.json");
     var proof=JObject.Parse(File.ReadAllText(proofPath));Need((string)proof["matchId"]==id&&(string)proof["source"]=="AUTHENTICATED_SELF_SEATS","ARCHIVED_PROOF_SCOPE");
     // Reuse already validated, immutable seat assignments; no identity migration/preflight.
     var expected=((JArray)proof["slots"]).ToDictionary(x=>(string)x["label"],x=>(int)x["selfSeat"]);
     expected.Add("UNITY_USER",Enumerable.Range(0,4).Except(expected.Values).Single());
     int self=(int)authority["privateState"]["seat"];
     Server6ParticipantValidator.Validate(authority,row,manifest,expected,expected,self);
     var pub=authority["publicState"];var h=row["history"];
     Need((string)pub["status"]=="FINISHED"&&JToken.DeepEquals(h["score"],pub["scores"])&&JToken.DeepEquals(h["score"],manifest["finalScore"])&&(string)h["finishReason"]==(string)manifest["result"]["finishReason"],"HISTORY_AUTHORITY");
     int team=Array.FindIndex(((JArray)manifest["teams"]).ToArray(),t=>t.Values<int>().Contains(self));
     Need((string)h["result"]==((int?)manifest["result"]["winner"]["index"]==team?"WIN":"LOSS"),"HISTORY_RESULT");
     var timeline=await replay.Load(manifest,ct);var terminal=timeline.Seek(timeline.Count).Data;
     Need((string)terminal["phase"]=="MATCH_FINISHED"&&JToken.DeepEquals(terminal["scores"],pub["scores"])&&JToken.DeepEquals(terminal["board"],pub["board"])&&(int)terminal["round"]==(int)pub["currentRound"]&&JToken.DeepEquals(terminal["counts"],pub["tilesRemainingPerSeat"])&&JToken.DeepEquals(terminal["matchResult"],manifest["result"])&&(long)terminal["sequence"]==(long)pub["lastSequence"],"REPLAY_AUTHORITY");
     var endManifest=(JObject)manifest.DeepClone();endManifest["participants"]=terminal["participants"].DeepClone();Server6ParticipantValidator.Validate(authority,row,endManifest,expected,expected,self);
     Record("MATCH_"+n+"_HISTORY_CONSISTENCY=PASS\nMATCH_"+n+"_REPLAY_RECONSTRUCTION=PASS\nMATCH_"+n+"_REPLAY_EVENTS="+timeline.Count);
    }
    Need(global::Firebase.Auth.FirebaseAuth.DefaultInstance.CurrentUser?.UserId==uid&&errors==0,"SESSION_ERRORS");
    Record("HISTORY_COUNT=5\nHISTORY_ORDERING=FINISHED_AT_DESC_5_4_3_2_1\nHISTORY_CONSISTENCY_ALL_5=PASS\nREPLAY_AVAILABLE_COUNT=5\nREPLAY_RECONSTRUCTION_COUNT=5\nREPLAY_TERMINAL_STATE_MATCHES_AUTHORITY=PASS\nREPLAY_SEQUENCE_REGRESSIONS=0\nCONSOLE_ERRORS=0");
    stage="HISTORY_UI";
    foreach(var old in UnityEngine.Object.FindObjectsByType<HistoryReplayView>(FindObjectsSortMode.None))UnityEngine.Object.Destroy(old.gameObject);
    var local=UnityEngine.Object.FindFirstObjectByType<DominoClientController>();Need(local&&local.Menu,"MENU_REQUIRED");
    local.Menu.Show(StartScreen.Match);var hv=new GameObject("SERVER6 final History",typeof(RectTransform)).AddComponent<HistoryReplayView>();
    var tiles=(DominoTileView)typeof(DominoClientController).GetField("tilePrefab",Fields).GetValue(local);
    var players=(PlayerView)typeof(DominoClientController).GetField("playerPrefab",Fields).GetValue(local);
    hv.Initialize(replay,tiles,players,()=>local.Menu.Show(StartScreen.MainMenu));
    while((bool)typeof(HistoryReplayView).GetField("loading",Fields).GetValue(hv)){ct.ThrowIfCancellationRequested();await Task.Delay(150);}
    Need(((System.Collections.Generic.List<JObject>)typeof(HistoryReplayView).GetField("history",Fields).GetValue(hv)).Count==5,"UI_COUNT");
    await Task.Delay(700);ScreenCapture.CaptureScreenshot(Path.Combine(dir,after?"history-after-restart.png":"history-final.png"));
    Need(errors==0,"UI_ERRORS");Record("UNITY_HISTORY_ENTRIES=5\nFINAL_VALIDATION=PASS");Debug.Log("[SERVER6-FINAL] Five matches PASS; afterRestart="+after);
   }catch(Exception e){Record("FINAL_VALIDATION=STOP\nSTAGE="+stage+"\nERROR_TYPE="+e.GetType().Name+"\nCONSOLE_ERRORS="+errors);Debug.LogWarning("[SERVER6-FINAL] Stopped at "+stage);}
   finally{Application.logMessageReceived-=Log;running=false;}
  }
 }
}
#endif
