#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Domino.Infrastructure;
using Domino.Infrastructure.Api;
using Domino.Realtime;
using Domino.Replay;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
namespace Domino.Editor {
 public static class Server6CurrentSession {
  static bool running;
  static string Dir=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../Validation/Generated/SERVER6-current"));
  static void Record(string s)=>File.AppendAllText(Path.Combine(Dir,"player-correlation.txt"),s+"\n");
  static void Require(bool b){if(!b)throw new InvalidOperationException();}
  [MenuItem("Domino/Validation/SERVER-6/S6-03C authorized player correlation")]
  public static async void CheckCurrentSession(){
   if(running||!Application.isPlaying)return;
   Directory.CreateDirectory(Dir);
   if(File.Exists(Path.Combine(Dir,"player-correlation.txt"))){Debug.Log("[SERVER6] Existing coordination evidence: review required.");return;}
   running=true;string stage="CONFIGURATION";
   try{
    var config=Resources.Load<DominoApiSettings>("ApiSettings");
    Require(config!=null&&config.Environment=="TEST"&&config.Configuration.Endpoint.Host=="domino-api-test.teamfho.com");
    Require(ApplicationServices.Player?.IsFresh==true&&ApplicationServices.Realtime?.State==RealtimeConnectionState.CONNECTED);
    stage="CURRENT_IDENTITY";
    var auth=global::Firebase.Auth.FirebaseAuth.DefaultInstance;
    string uid=auth.CurrentUser?.UserId;
    string project=global::Firebase.FirebaseApp.DefaultInstance.Options.ProjectId;
    Require(!string.IsNullOrWhiteSpace(uid)&&uid==ApplicationServices.Identity?.Current?.Uid&&project=="teamfho-domino");
    Record("UNITY_PROJECT_MATCHES_TEST=YES\nUNITY_AUTH_READY=YES\nUNITY_SWARM_FILE_READS=0\nUNITY_OPPONENT_CREDENTIAL_FILES_OPENED=0");
    stage="AUTHENTICATED_PLAYER_CORRELATION_AND_HISTORY";
    var api=(IDominoApiClient)typeof(Domino.Player.PlayerService).GetField("api",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(ApplicationServices.Player);
    Require(api is DominoApiClient);
    var actual=(DominoApiConfiguration)typeof(DominoApiClient).GetField("settings",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(api);
    Require(actual.Endpoint.Scheme=="https"&&actual.Endpoint.Host=="domino-api-test.teamfho.com");
    using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(60));
    var replay=new ReplayClient(ApplicationServices.OnlineApi);
    var result=await Server6PlayerCorrelation.Verify(api,()=>auth.CurrentUser?.UserId,ct=>replay.History(null,ct),deadline.Token);
    Record("BOOTSTRAP_AUTHENTICATED_UID_USED=YES\nBOOTSTRAP_PLAYER_LOOKUP_BY_AUTH_UID=YES\nCLIENT_CAN_SELECT_ARBITRARY_PLAYER_UID=NO");
    Record("CURRENT_PLAYER_DOCUMENT_EXISTS=YES\nUNITY_AUTH_IDENTITY_RESOLVES_CURRENT_PLAYER=YES\nUNITY_UID_EQUALS_PLAYER_DOCUMENT_ID=YES_BY_BACKEND_CONTRACT\nAUTHENTICATED_PLAYER_CORRELATION=PASS");
    Record("HISTORY_REQUEST_AUTHORIZED=YES\nHISTORY_COUNT_BEFORE="+result.HistoryCount);
    Require(result.HistoryCount==0);
    Record("REPLAY_COUNT_BEFORE=0\nADMIN_TOOLING_USED_FOR_CORRELATION=NO\nUNITY_DIRECT_FIRESTORE_PLAYER_READ=PROHIBITED");
    stage="ENTITLEMENTS";
    var e=result.Bootstrap.entitlements;
    Require(e?.snapshot?.plan=="PREMIUM"&&e.snapshot.trialActive&&e.snapshot.limits?.REPLAY_MAX?.unlimited==true);
    Record("CURRENT_USER_PLAN=PREMIUM_TRIAL\nCURRENT_USER_REPLAY_LIMIT=UNLIMITED\nPREFLIGHT=PASS\nMATCHES_STARTED=0\nMATCHES_COMPLETED=0");
    Debug.Log("[SERVER6] S6-03C preflight PASS. No matchmaking.");
   }catch(Exception e){Record("PREFLIGHT=FAIL\nFAILED_STAGE="+stage+"\nERROR_TYPE="+e.GetType().Name+"\nMATCHES_STARTED=0");Debug.LogWarning("[SERVER6] S6-03C stopped at "+stage);}
   finally{running=false;}
  }
 }
}
#endif
