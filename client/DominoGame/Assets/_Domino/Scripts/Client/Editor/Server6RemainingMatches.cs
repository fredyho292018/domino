#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Domino.Client;
using Domino.Infrastructure;
using Domino.Online;
using Domino.Realtime;
using Domino.Replay;
using Domino.UI;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
namespace Domino.Editor {
 public static class Server6RemainingMatches {
  const BindingFlags Fields=BindingFlags.Instance|BindingFlags.NonPublic;
  static int number;
  static string Dir=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../Validation/Generated/SERVER6-match"+number));
  static string failure="OPERATION_EXCEPTION";static bool running;static int errors,started,completed;static string uid;static OnlineMatchController view;
  static void Record(string text)=>File.AppendAllText(Path.Combine(Dir,"execution.txt"),text.Replace("MATCH_2","MATCH_"+number)+"\n");
  static void Check(bool ok,string code){if(!ok){failure=code;throw new InvalidOperationException();}}
  static void Guard(){Check(Application.isPlaying,"PLAY_STOPPED");Check(global::Firebase.Auth.FirebaseAuth.DefaultInstance.CurrentUser?.UserId==uid,"IDENTITY_CHANGED");Check(ApplicationServices.Realtime.State==RealtimeConnectionState.CONNECTED,"AUTH_OR_CONNECTION_LOST");Check(errors==0,"UNITY_ERROR");Check(!File.Exists(Path.Combine(Dir,"stop")),"EXTERNAL_STOP");}
  static async Task Until(Func<bool> condition,string code,int seconds=180){var end=DateTime.UtcNow.AddSeconds(seconds);while(!condition()){Guard();Check(DateTime.UtcNow<end,code);await Task.Delay(150);}Guard();}
  static void Log(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
  static T Field<T>(object obj,string name)=>(T)obj.GetType().GetField(name,Fields).GetValue(obj);
  [MenuItem("Domino/Validation/SERVER-6/Execute MATCH 3 only")] public static void Match3()=>Run(3);
  [MenuItem("Domino/Validation/SERVER-6/Execute MATCH 4 only")] public static void Match4()=>Run(4);
  [MenuItem("Domino/Validation/SERVER-6/Execute MATCH 5 only")] public static void Match5()=>Run(5);
  public static async void Run(int requested){
   if(running||!Application.isPlaying||requested<3||requested>5)return;number=requested;
   Directory.CreateDirectory(Dir);if(File.Exists(Path.Combine(Dir,"execution.txt"))){Debug.LogWarning("[SERVER6-FIVE] Existing execution; do not restart.");return;}
   running=true;errors=0;started=completed=number-1;Application.logMessageReceived+=Log;string stage="START";
   try{
    uid=global::Firebase.Auth.FirebaseAuth.DefaultInstance.CurrentUser?.UserId;Check(!string.IsNullOrWhiteSpace(uid),"NO_CURRENT_USER");Guard();
    var local=UnityEngine.Object.FindFirstObjectByType<DominoClientController>();Check(local&&local.Menu,"MENU_REQUIRED");
    var apiConfig=Field<Domino.Infrastructure.Api.DominoApiConfiguration>(ApplicationServices.OnlineApi,"config");Check(apiConfig.Endpoint.Scheme=="https"&&apiConfig.Endpoint.Host=="domino-api-test.teamfho.com","WRONG_ENDPOINT"); foreach(var oldHistory in UnityEngine.Object.FindObjectsByType<HistoryReplayView>(FindObjectsSortMode.None))UnityEngine.Object.Destroy(oldHistory.gameObject); var tiles=Field<DominoTileView>(local,"tilePrefab");var players=Field<PlayerView>(local,"playerPrefab");
    Check(ApplicationServices.Player.IsFresh,"PLAYER_NOT_SYNCED");Record("UNITY_ENVIRONMENT=TEST\nUNITY_PLAYER_SYNC=SYNCED\nUNITY_CONNECTION=CONNECTED");var before=await new ReplayClient(ApplicationServices.OnlineApi).History(null,CancellationToken.None);
    string firstId=File.ReadAllText(Path.Combine(Dir,"../SERVER6-match1/match-1.txt")).Trim();
    Check(((JArray)before["items"]).Count==number-1,"INITIAL_HISTORY_COUNT");
    for(int prior=1;prior<number;prior++){
     string priorId=File.ReadAllText(Path.Combine(Dir,"../SERVER6-match"+prior+"/match-"+prior+".txt")).Trim();
     Check(((JArray)before["items"]).Any(x=>(string)x["history"]["matchId"]==priorId),"PRIOR_HISTORY_MISSING");
    }
    string priorGate=File.ReadAllText(Path.Combine(Dir,"../SERVER6-match"+(number-1)+"/execution.txt"));
    Check(priorGate.Contains("MATCH_"+(number-1)+"_GATE=PASS"),"PRIOR_GATE_REQUIRED");
    Record("MATCHES_REQUESTED=1\nMATCH_CONCURRENCY=1\nHISTORY_COUNT_BEFORE="+(number-1)+"");
    for(int n=number;n<=number;n++){
     stage="MATCH_"+n;File.WriteAllText(Path.Combine(Dir,"request-"+n),"START_THREE_TEST_OPPONENTS");
     await Until(()=>File.Exists(Path.Combine(Dir,"opponents-ready-"+n)),"OPPONENT_START_TIMEOUT",600);
     local.Menu.Show(StartScreen.Match);
     var entry=new GameObject("SERVER6 normal matchmaking",typeof(RectTransform)).AddComponent<MatchmakingView>();
     entry.Initialize(tiles,players,()=>local.Menu.Show(StartScreen.MainMenu),"PARTNERS_2V2_ONLINE");
     await Until(()=>Field<Button>(entry,"search").interactable,"SEARCH_NOT_READY");
     Check(entry.Client.State==MatchmakingState.IDLE,"UNEXPECTED_EXISTING_ASSIGNMENT");
     Field<Button>(entry,"search").onClick.Invoke();
     await Until(()=>entry.Match!=null,"MATCH_FOUND_TIMEOUT");view=entry.Match;started++;
     var client=view.Client;string id=client.Snapshot.MatchId;
     File.WriteAllText(Path.Combine(Dir,"match-"+n+".txt"),id);
     Record("MATCH_"+n+"_FOUND=YES\nMATCHMAKING_STARTED=YES\nMATCH_2_ID_RESOLVED=YES\nMATCHES_STARTED="+started);
     // External coordinator verifies all three authenticated opponents received exactly this match.
     await Until(()=>File.Exists(Path.Combine(Dir,"participants-confirmed-"+n)),"PARTICIPANT_CORRELATION_TIMEOUT",240);
     Check(File.ReadAllText(Path.Combine(Dir,"participants-confirmed-"+n)).Trim()==id,"PARTICIPANT_CORRELATION_FAILED");
     var expected=Server6ParticipantValidator.ReadEvidence(JObject.Parse(File.ReadAllText(Path.Combine(Dir,"identity-seats-initial.json"))),id);expected.Add("UNITY_USER",client.Snapshot.Seat);
     Check(expected.Values.Distinct().Count()==4,"INITIAL_SEAT_IDENTITIES");
     Record("MATCH_2_MATCHMAKING_STARTED=YES");
     Record("DISTINCT_MATCH_PLAYERS=4\nCURRENT_UNITY_USER_IN_MATCH_2=YES");string rejected=null;client.Rejected+=c=>rejected=c;long last=client.Snapshot.Sequence;
     var deadline=DateTime.UtcNow.AddMinutes(40);
     while(client.Snapshot.Phase!="MATCH_FINISHED"){
      Guard();Check(DateTime.UtcNow<deadline,"MATCH_TIMEOUT");Check(rejected==null,"COMMAND_OR_RESYNC_REJECTED");
      var s=client.Snapshot;Check(s.Sequence>=last,"SEQUENCE_REGRESSION");last=s.Sequence;
      Check(s.PlayerCount==4&&((JArray)s.Public["participants"]).Count==4,"PARTICIPANT_COUNT");
      if(view.Board&&view.Board.RoundRewardPanel){
       if(s.Phase!="ROUND_FINISHED"||s.Seat==0)view.Board.RoundRewardPanel.ContinueButton.onClick.Invoke();
       await Task.Delay(300);continue;
      }
      bool ready=view.Board&&!view.Board.IsPreparingRound&&!Field<bool>(view,"rendering")&&!client.Pending&&!client.NeedsResync;
      if(ready&&s.Phase=="STARTER_SELECTION"){
       var starter=view.Board.StarterView;
       var choice=starter?starter.Choices.FirstOrDefault(x=>x.interactable):null;
       if(choice)choice.onClick.Invoke();else {var first=Field<Button>(view,"first");if(first&&first.interactable)first.onClick.Invoke();}
      } else if(ready&&s.Phase=="PLAYING"&&(int?)s.Public["currentSeat"]==s.Seat){
       var tile=view.Board.LocalTiles.FirstOrDefault(t=>view.Board.CanPlace(t.Tile,Domino.Core.ChainEnd.Left)||view.Board.CanPlace(t.Tile,Domino.Core.ChainEnd.Right));
       if(tile){tile.OnPointerClick(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){eligibleForClick=true});typeof(OnlineMatchController).GetMethod("Play",Fields).Invoke(view,new object[]{"AUTO"});}
       else await client.SendAsync("PASS");
      }
      await Task.Delay(400);
     }
     Guard();completed++;Record("MATCH_"+n+"_COMPLETED=YES\nMATCHES_COMPLETED="+completed);
     var authority=await ApplicationServices.OnlineApi.SendAsync("GET","matches/"+id+"/snapshot",null,CancellationToken.None);
     Check((string)authority["publicState"]["status"]=="FINISHED","AUTHORITY_NOT_COMPLETED");
     var replay=new ReplayClient(ApplicationServices.OnlineApi);var history=await replay.History(null,CancellationToken.None);
     Check(((JArray)history["items"]).Any(x=>(string)x["history"]?["matchId"]==id),"HISTORY_MISSING");Record("MATCH_"+n+"_IN_HISTORY=YES");
     var manifest=await replay.Manifest(id,CancellationToken.None);Check((bool?)manifest["replayAvailable"]==true,"REPLAY_UNAVAILABLE");
     var h=((JArray)history["items"]).Single(x=>(string)x["history"]?["matchId"]==id)["history"];
     Check((string)manifest["result"]?["finishReason"]=="TARGET_REACHED","NON_GAMEPLAY_COMPLETION");
     Check(JToken.DeepEquals(h["score"],manifest["finalScore"])&&(string)h["finishReason"]==(string)manifest["result"]["finishReason"]&&h["finishedAt"]!=null,"HISTORY_RESULT_MISMATCH");
     Check(DateTimeOffset.TryParse(h["finishedAt"].ToString(),out var finishedAt),"HISTORY_FINISHED_AT");
     int owner=Array.FindIndex(((JArray)manifest["teams"]).ToArray(),t=>t.Values<int>().Contains(client.Snapshot.Seat));
     Check((string)h["result"]==((int?)manifest["result"]["winner"]?["index"]==owner?"WIN":"LOSS"),"HISTORY_WINNER_MISMATCH");
     await Until(()=>File.Exists(Path.Combine(Dir,"identity-seats-final.json")),"FINAL_IDENTITY_TIMEOUT",240);
     var proof=JObject.Parse(File.ReadAllText(Path.Combine(Dir,"identity-seats-final.json")));
     var identities=Server6ParticipantValidator.ReadEvidence(proof,id);identities.Add("UNITY_USER",client.Snapshot.Seat);
     var historyItem=(JObject)((JArray)history["items"]).Single(x=>(string)x["history"]?["matchId"]==id);
     Server6ParticipantValidator.Validate(authority,historyItem,manifest,expected,identities,client.Snapshot.Seat);
     Record("MATCH_2_HISTORY_CONSISTENCY=PASS\nMATCH_2_RESULT="+h["result"].ToString()+"\nMATCH_2_SCORE="+manifest["finalScore"].ToString(Newtonsoft.Json.Formatting.None)+"\nMATCH_2_COMPLETION_REASON=TARGET_REACHED");
     var timeline=await replay.Load(manifest,CancellationToken.None);var terminal=timeline.Seek(timeline.Count).Data;
     Check((string)terminal["phase"]=="MATCH_FINISHED"&&JToken.DeepEquals(terminal["scores"],authority["publicState"]["scores"])&&JToken.DeepEquals(terminal["board"],authority["publicState"]["board"])&&(int)terminal["round"]==(int)authority["publicState"]["currentRound"]&&JToken.DeepEquals(terminal["counts"],authority["publicState"]["tilesRemainingPerSeat"]),"REPLAY_AUTHORITY_MISMATCH");
     Check(JToken.DeepEquals(terminal["matchResult"],manifest["result"])&&(long)terminal["sequence"]==(long)authority["publicState"]["lastSequence"],"REPLAY_TERMINAL_SEQUENCE");
     var terminalManifest=(JObject)manifest.DeepClone();terminalManifest["participants"]=terminal["participants"].DeepClone();Server6ParticipantValidator.Validate(authority,historyItem,terminalManifest,expected,identities,client.Snapshot.Seat);
     Record("MATCH_2_REPLAY_EVENTS="+timeline.Count);
     Record("MATCH_"+n+"_REPLAY_AVAILABLE=YES\nMATCH_"+n+"_REPLAY_RECONSTRUCTION=PASS\nMATCH_"+n+"_REPLAY_CONSISTENCY=PASS");
     File.WriteAllText(Path.Combine(Dir,"terminal-"+n+".json"),authority["publicState"].ToString());
     await Until(()=>File.Exists(Path.Combine(Dir,"opponents-done-"+n)),"OPPONENT_COMPLETION_TIMEOUT",120);
     UnityEngine.Object.Destroy(entry.gameObject);UnityEngine.Object.Destroy(view.gameObject);await Task.Delay(600);view=null;
     File.WriteAllText(Path.Combine(Dir,"gate-"+n),"PASS");
    }
    var final=await new ReplayClient(ApplicationServices.OnlineApi).History(null,CancellationToken.None);
    Check(((JArray)final["items"]).Count==number&&final["nextCursor"]?.Type!=JTokenType.String,"FINAL_HISTORY_COUNT");
    Check((string)final["items"][number-1]["history"]["matchId"]==firstId,"MATCH_1_MISSING_OR_ORDER");
    string secondId=File.ReadAllText(Path.Combine(Dir,"match-"+number+".txt")).Trim();
    Check((string)final["items"][0]["history"]["matchId"]==secondId,"MATCH_2_ORDER");
    for(int prior=1;prior<number;prior++){
     string priorId=File.ReadAllText(Path.Combine(Dir,"../SERVER6-match"+prior+"/match-"+prior+".txt")).Trim();
     Check((string)final["items"][number-prior]["history"]["matchId"]==priorId,"HISTORY_ORDER");
     var priorManifest=await new ReplayClient(ApplicationServices.OnlineApi).Manifest(priorId,CancellationToken.None);
     Check((bool?)priorManifest["replayAvailable"]==true,"PRIOR_REPLAY_UNAVAILABLE");
    }
    Record("MATCH_1_STILL_IN_HISTORY=YES\nMATCH_1_REPLAY_STILL_AVAILABLE=YES\nHISTORY_ORDERING_AFTER_MATCH_2=FINISHED_AT_DESC_ALL_EXPECTED_IDS");
    Record("MATCH_2_EVENT_SEQUENCE_REGRESSION=NO\nMATCH_2_REPLAY_SEQUENCE_REGRESSION=NO");Record("HISTORY_COUNT_AFTER_MATCH_2="+number+"\nEXTRA_MATCHES_STARTED=0\nMATCH_2_API_GATE=PASS\nCONSOLE_ERRORS="+errors);
    local.Menu.Show(StartScreen.Match);var hv=new GameObject("SERVER6 History",typeof(RectTransform)).AddComponent<HistoryReplayView>();
    hv.Initialize(new ReplayClient(ApplicationServices.OnlineApi),tiles,players,()=>local.Menu.Show(StartScreen.MainMenu));
    await Until(()=>!Field<bool>(hv,"loading"),"HISTORY_UI_TIMEOUT",60);await Task.Delay(1000);
    Check(Field<System.Collections.Generic.List<JObject>>(hv,"history").Count==number,"HISTORY_UI_COUNT");
    Record("UNITY_HISTORY_ENTRIES_VISIBLE_AFTER_MATCH_2="+number+"");ScreenCapture.CaptureScreenshot(Path.Combine(Dir,"history.png"));await Task.Delay(1000);
    var open=hv.GetComponentsInChildren<Button>().First(x=>x.name=="View match"&&x.transform.parent.name=="Match "+secondId);open.onClick.Invoke();
    await Until(()=>!Field<bool>(hv,"loading")&&Field<JObject>(hv,"manifest")!=null,"REPLAY_DETAIL_UI_TIMEOUT",60);
    Check((string)Field<JObject>(hv,"manifest")["matchId"]==secondId,"UI_WRONG_MATCH");
    hv.LoadReplay();await Until(()=>hv.Timeline!=null&&hv.Board,"REPLAY_UI_TIMEOUT",60);await Task.Delay(1000);
    Record("UNITY_MATCH_2_REPLAY_OPEN=PASS");ScreenCapture.CaptureScreenshot(Path.Combine(Dir,"replay.png"));
    Guard();Record("MATCH_2_GATE=PASS\nNEXT_MATCH_STARTED=NO");File.WriteAllText(Path.Combine(Dir,"full-gate"),"PASS");Debug.Log("[SERVER6] Match "+number+" PASS; no next matchmaking.");
   }catch(Exception e){Record("EXECUTION=STOP\nFAILED_STAGE="+stage+"\nERROR_TYPE="+e.GetType().Name+"\nFAILURE_CODE="+failure+"\nMATCHES_STARTED="+started+"\nMATCHES_COMPLETED="+completed+"\nCONSOLE_ERRORS="+errors);File.WriteAllText(Path.Combine(Dir,"stop"),"STOP");Debug.LogWarning("[SERVER6-FIVE] Stopped at "+stage);}
   finally{Application.logMessageReceived-=Log;running=false;uid=null;}
  }
 }
}
#endif
