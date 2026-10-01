using System;
using System.Threading;
using System.Threading.Tasks;
using Domino.Infrastructure.Api;
using Domino.Identity;
static class OnboardingCheckpointTests
{
    static int checks;
    static void Check(bool ok){if(!ok)throw new Exception("BE07 bootstrap contract mismatch");checks++;}
    sealed class Tokens:IAuthTokenProvider {public Task<string> GetIdTokenAsync(bool forceRefresh,CancellationToken token)=>Task.FromResult("fixture-only");}
    sealed class Transport:IApiTransport {
        public string Body;
        public Task<ApiHttpResponse> SendAsync(string method,Uri url,string json,string token,int timeout,CancellationToken cancellation) {
            Check(method=="POST"&&url.AbsolutePath=="/api/v1/player/bootstrap");
            Check(TrialContractHeader.Required(method,url));
            return Task.FromResult(new ApiHttpResponse(200,Body));
        }
    }
    static async Task Main() {
        var wire=new Transport();
        var api=new DominoApiClient(new DominoApiConfiguration(true,"https://example.test"),new Tokens(),wire,new UnityApiJsonCodec());
        foreach(var eligibility in new[]{"NOT_STARTED","ACTIVE","EXPIRED","INELIGIBLE","UNKNOWN"}) {
            wire.Body="{\"player\":{\"uid\":\"fixture-player\",\"accountType\":\"GUEST\",\"displayName\":\"Guest-ABCDEFGH\",\"language\":\"en\",\"status\":\"ACTIVE\"},\"wallet\":{\"coins\":0},\"entitlements\":{\"availability\":\"AVAILABLE\",\"trialGranted\":false,\"snapshot\":{\"plan\":\"FREE\",\"limits\":{\"HISTORY_MAX\":{\"unlimited\":true,\"maximum\":null}}}},\"capabilities\":{\"trialActivationMode\":\"EXPLICIT\",\"trialActivationContractVersion\":\"1\"},\"trialEligibility\":{\"state\":\""+eligibility+"\",\"eligible\":false,\"policyVersion\":null,\"periodDays\":null,\"activationMode\":\"EXPLICIT\"}}";
            var dto=await api.BootstrapAsync("en",default);
            Check(dto.player.uid=="fixture-player"&&dto.wallet.coins==0);
            Check(!dto.entitlements.trialGranted&&dto.entitlements.snapshot.limits.HISTORY_MAX.unlimited);
            Check(dto.capabilities["trialActivationMode"]=="EXPLICIT"&&dto.capabilities["trialActivationContractVersion"]=="1");
            Check(dto.trialEligibility.state==eligibility&&dto.trialEligibility.policyVersion==null&&dto.trialEligibility.periodDays==null);
        }
        Check(TrialContractHeader.Name=="X-Trial-Activation-Contract"&&TrialContractHeader.Value=="1");
        Console.WriteLine("BE07_BOOTSTRAP_CLIENT_CONTRACT=PASS CHECKS="+checks+" REAL_NETWORK_CALLS=0");
    }
}
