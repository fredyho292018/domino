using System;
using System.Threading;
using System.Threading.Tasks;
using Domino.Rewards;
using Domino.Ads;

static class MonetizationTests
{
    static int checks;
    static void Check(bool ok,string name){checks++;if(!ok)throw new Exception(name);}
    sealed class Api : IMonetizationPolicyApi
    {
        public int Configs,Queries,Opportunities; public long Coins=15; public bool Ads=true,Rewarded=true,Fail;
        public string Reason; public DateTimeOffset Server=DateTimeOffset.Parse("2026-09-13T12:00:00Z");
        public Task<MonetizationPolicySnapshot> ConfigAsync(CancellationToken t){Configs++;if(Fail)throw new Exception();return Task.FromResult(new MonetizationPolicySnapshot(2,Ads,Rewarded,Coins,1,120,5,20,200));}
        public Task<RewardEligibilitySnapshot> EligibilityAsync(string id,CancellationToken t){Queries++;return Task.FromResult(new RewardEligibilitySnapshot(Reason==null,Coins,Reason,Reason=="COOLDOWN"?Server.AddSeconds(120):(DateTimeOffset?)null,Server,5,20,200));}
        public Task<string> OpportunityAsync(CancellationToken t){Opportunities++;return Task.FromResult(Guid.NewGuid().ToString());}
    }
    sealed class Ad : IRewardedAdsService
    {
        public RewardedState State{get;set;}=RewardedState.READY;
        public bool IsAvailable=>true;public int Loads,Shows;
        public event Action<RewardedState> RewardedStateChanged;
        public event Action<bool> RewardedAvailabilityChanged;
        public event Action<RewardedCompletionResult> RewardEarned;
        public Task InitializeAsync()=>LoadRewardedAsync();
        public Task LoadRewardedAsync(){Loads++;return Task.CompletedTask;}
        public void PreloadIfNeeded(){}
        public Task<RewardedShowResult> ShowRewardedAsync(){Shows++;return Task.FromResult(RewardedShowResult.Closed);}
        public void Dispose(){}
    }
    static async Task Main()
    {
        double time=0;var api=new Api();var policy=new MonetizationPolicyService(api,default,()=>time);
        Check(!policy.AllowAds&&!policy.Eligible&&policy.RewardCoins==0,"unknown fails closed");
        await policy.RefreshAsync();Check(policy.AllowAds&&policy.RewardCoins==15,"remote fifteen");
        await policy.RefreshAsync();Check(api.Configs==1,"cache no duplicate fetch");
        await policy.RefreshEligibilityAsync(true);Check(policy.Eligible&&api.Opportunities==1,"server opportunity");
        var ad=new Ad();var gated=new PolicyRewardedAds(ad,policy);
        await gated.ShowRewardedAsync();Check(ad.Shows==1&&api.Configs==2,"fresh policy before show");
        api.Ads=false;await gated.ShowRewardedAsync();Check(ad.Shows==1&&!policy.AllowAds,"global kill blocks show");
        await gated.LoadRewardedAsync();Check(ad.Loads==0,"global kill blocks load");
        api.Ads=true;api.Rewarded=false;await gated.ShowRewardedAsync();Check(ad.Shows==1&&!policy.AllowAds,"reward kill");
        api.Rewarded=true;api.Reason="COOLDOWN";await policy.RefreshAsync(true);await policy.RefreshEligibilityAsync();
        Check(!policy.Eligible&&policy.RemainingSeconds==120,"server cooldown");
        var requests=api.Queries;time=119;Check(policy.RemainingSeconds==1,"monotonic countdown");time=120;
        Check(policy.RemainingSeconds==0&&api.Queries==requests,"countdown no HTTP");
        foreach(var reason in new[]{"ROUND_LIMIT","HOURLY_LIMIT","DAILY_LIMIT","DAILY_COIN_LIMIT","ACCOUNT_NOT_ELIGIBLE","DEPENDENCY_UNAVAILABLE"}){
            api.Reason=reason;await gated.ShowRewardedAsync();Check(ad.Shows==1&&!policy.Eligible,"blocked "+reason);
        }
        api.Reason=null;await policy.RefreshEligibilityAsync();time+=600;Check(!policy.AllowAds&&!policy.Eligible,"expired cache fails closed");
        api.Fail=true;await gated.ShowRewardedAsync();Check(policy.Snapshot==null&&ad.Shows==1,"failed refresh discards stale grants");
        const string json="{\"version\":2,\"adsEnabled\":true,\"rewarded\":{\"enabled\":true,\"rewardCoins\":15,\"limits\":{\"perRound\":1,\"cooldownSeconds\":120,\"perHour\":5,\"perDay\":20,\"maxCoinsPerDay\":200}}}";
        Check(MonetizationPolicyApi.ReadPolicy(json).RewardCoins==15,"wire contract");
        foreach(var invalid in new[]{json.Replace("15","0"),json.Replace("15","\"15\""),json.Replace("\"version\":2","\"version\":2,\"version\":3"),json+"{}","{}"}){
            bool rejected=false;try{MonetizationPolicyApi.ReadPolicy(invalid);}catch{rejected=true;}Check(rejected,"reject malformed policy");
        }
        Check(AdsConfiguration.AndroidDemoRewarded=="ca-app-pub-3940256099942544/5224354917","local SDK test ID preserved");
        Console.WriteLine($"H6_UNITY_TESTS=PASS CHECKS={checks}; REAL_AD_REQUESTS=0; LIVE_WALLET_MUTATIONS=0");
    }
}
