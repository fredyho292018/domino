using System;
using System.Threading;
using System.Threading.Tasks;
using Domino.Infrastructure.Api;
using Domino.Editor;
using Newtonsoft.Json.Linq;

static partial class PlayerFoundationClientTests
{
    static async Task Server6CorrelationChecks()
    {
        var config = new DominoApiConfiguration(true,"https://domino-api-test.teamfho.com",15,"TEST",true);
        int historyCalls=0;
        Task<JObject> History(CancellationToken ct) { historyCalls++;return Task.FromResult(new JObject { ["items"]=new JArray(),["nextCursor"]=null }); }
        var transport=new Transport();
        var api=new DominoApiClient(config,new Tokens(),transport,new UnityApiJsonCodec());
        var result=await Server6PlayerCorrelation.Verify(api,()=>"u1",History,CancellationToken.None);
        Check(result.HistoryCount==0&&historyCalls==1,"S603C authenticated correlation and history without Firestore");
        // Existing Transport asserts exact bootstrap path, transient auth token, and language-only body.
        try { await Server6PlayerCorrelation.Verify(api,()=>"another-user",History,CancellationToken.None);throw new Exception("mismatch accepted"); }
        catch(InvalidOperationException) { Check(historyCalls==1,"S603C mismatched player blocks history"); }
        var denied=new Transport();denied.Responses.Enqueue(new ApiHttpResponse(403,"{}"));
        try { await Server6PlayerCorrelation.Verify(new DominoApiClient(config,new Tokens(),denied,new UnityApiJsonCodec()),()=>"u1",History,CancellationToken.None);throw new Exception("denied accepted"); }
        catch(DominoApiException) { Check(historyCalls==1,"S603C unauthorized correlation blocks history"); }
        int identityReads=0;
        try { await Server6PlayerCorrelation.Verify(api,()=>++identityReads==1?"u1":"changed",History,CancellationToken.None);throw new Exception("changed identity accepted"); }
        catch(InvalidOperationException) { Check(historyCalls==1,"S603C session change blocks history"); }
        try { await Server6PlayerCorrelation.Verify(api,()=>"u1",ct=>Task.FromException<JObject>(new DominoApiException(ApiFailure.Authentication)),CancellationToken.None);throw new Exception("history auth failure accepted"); }
        catch(DominoApiException) { Check(true,"S603C unauthorized history blocks completion"); }
    }
}
