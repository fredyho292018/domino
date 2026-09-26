using System;
using System.Threading;
using System.Threading.Tasks;
using Domino.Infrastructure.Api;
using Newtonsoft.Json.Linq;
namespace Domino.Editor {
 // Validation only. No Firestore dependency; identity is compared, never sent as a selector.
 public static class Server6PlayerCorrelation {
  public sealed class Result { public PlayerBootstrapResponseDto Bootstrap; public int HistoryCount; }
  public static async Task<Result> Verify(IDominoApiClient api, Func<string> currentUid,
      Func<CancellationToken,Task<JObject>> history, CancellationToken ct) {
   string expected=currentUid();
   if(string.IsNullOrWhiteSpace(expected))throw new InvalidOperationException("IDENTITY_REQUIRED");
   var response=await api.BootstrapAsync("es",ct);
   if(response?.player?.uid!=expected || currentUid()!=expected)throw new InvalidOperationException("PLAYER_CORRELATION_FAILED");
   var page=await history(ct);
   if(currentUid()!=expected)throw new InvalidOperationException("IDENTITY_CHANGED");
   if(!(page?["items"] is JArray items)||page["nextCursor"]?.Type==JTokenType.String)throw new InvalidOperationException("HISTORY_BASELINE_INCOMPLETE");
   return new Result{Bootstrap=response,HistoryCount=items.Count};
  }
 }
}
