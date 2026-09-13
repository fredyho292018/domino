using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Domino.Rewards
{
    public sealed class MonetizationPolicySnapshot
    {
        public long Version { get; }
        public bool AdsEnabled { get; }
        public bool RewardedEnabled { get; }
        public long RewardCoins { get; }
        public int MaxPerRound { get; }
        public long CooldownSeconds { get; }
        public int MaxPerHour { get; }
        public int MaxPerDay { get; }
        public long MaxCoinsPerDay { get; }
        public DateTimeOffset ReceivedAt { get; }
        public MonetizationPolicySnapshot(long version,bool ads,bool rewarded,long coins,int round,long cooldown,int hour,int day,long maxCoins)
        {
            if(version<1||coins<1||coins>9007199254740991L||round<1||cooldown<0||cooldown>86400||hour<1||day<1||maxCoins<coins||maxCoins>9007199254740991L)throw new FormatException("POLICY_CONTRACT");
            Version=version;AdsEnabled=ads;RewardedEnabled=rewarded;RewardCoins=coins;MaxPerRound=round;
            CooldownSeconds=cooldown;MaxPerHour=hour;MaxPerDay=day;MaxCoinsPerDay=maxCoins;ReceivedAt=DateTimeOffset.UtcNow;
        }
    }
    public sealed class RewardEligibilitySnapshot
    {
        public bool Eligible { get; }
        public long RewardCoins { get; }
        public string Reason { get; }
        public DateTimeOffset? NextEligibleAt { get; }
        public DateTimeOffset ServerTime { get; }
        public int RemainingHour { get; }
        public int RemainingDay { get; }
        public long RemainingCoinsToday { get; }
        public RewardEligibilitySnapshot(bool eligible,long coins,string reason,DateTimeOffset? next,DateTimeOffset server,int hour,int day,long remainingCoins)
        {
            if(coins<1||coins>9007199254740991L||hour<0||day<0||remainingCoins<0||remainingCoins>9007199254740991L||
                (eligible && reason!=null)||(!eligible && !ValidReason(reason)))throw new FormatException("ELIGIBILITY_CONTRACT");
            Eligible=eligible;RewardCoins=coins;Reason=reason;NextEligibleAt=next;ServerTime=server;
            RemainingHour=hour;RemainingDay=day;RemainingCoinsToday=remainingCoins;
        }
        public static bool ValidReason(string value)=>value=="ADS_DISABLED"||value=="REWARDED_DISABLED"||value=="ROUND_LIMIT"||value=="COOLDOWN"||value=="HOURLY_LIMIT"||value=="DAILY_LIMIT"||value=="DAILY_COIN_LIMIT"||value=="ACCOUNT_NOT_ELIGIBLE"||value=="DEPENDENCY_UNAVAILABLE";
    }
    public interface IMonetizationPolicyApi
    {
        Task<MonetizationPolicySnapshot> ConfigAsync(CancellationToken token);
        Task<RewardEligibilitySnapshot> EligibilityAsync(string opportunityId,CancellationToken token);
        Task<string> OpportunityAsync(CancellationToken token);
    }
    public interface IMonetizationPolicyService
    {
        MonetizationPolicySnapshot Snapshot { get; }
        RewardEligibilitySnapshot Eligibility { get; }
        bool Fresh { get; }
        bool AllowAds { get; }
        bool Eligible { get; }
        long RewardCoins { get; }
        int RemainingSeconds { get; }
        event Action Changed;
        Task RefreshAsync(bool force=false);
        Task RefreshEligibilityAsync(bool newRound=false);
        Task<bool> BeforeShowAsync();
    }
    public sealed class MonetizationPolicyService : IMonetizationPolicyService
    {
        public const int CacheTtlSeconds=600;
        readonly IMonetizationPolicyApi api;
        readonly CancellationToken lifetime;
        readonly Func<double> elapsed;
        Task refresh,eligibilityRefresh;
        double fetchedAt,eligibleAt;
        string opportunity;
        public MonetizationPolicySnapshot Snapshot {get;private set;}
        public RewardEligibilitySnapshot Eligibility {get;private set;}
        public bool Fresh=>Snapshot!=null && elapsed()-fetchedAt<CacheTtlSeconds;
        public bool AllowAds=>Fresh && Snapshot.AdsEnabled && Snapshot.RewardedEnabled;
        public bool Eligible=>AllowAds && Eligibility?.Eligible==true && elapsed()-eligibleAt<CacheTtlSeconds;
        public long RewardCoins=>Eligibility?.Eligible==true && Fresh ? Eligibility.RewardCoins : Snapshot?.RewardCoins ?? 0;
        public int RemainingSeconds=>Eligibility?.NextEligibleAt==null?0:(int)Math.Max(0,Math.Ceiling((Eligibility.NextEligibleAt.Value-Eligibility.ServerTime).TotalSeconds-(elapsed()-eligibleAt)));
        public event Action Changed;
        public MonetizationPolicyService(IMonetizationPolicyApi api,CancellationToken lifetime,Func<double> elapsed=null)
        {this.api=api;this.lifetime=lifetime;this.elapsed=elapsed??(()=>Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency);}
        public Task RefreshAsync(bool force=false)
        {
            if(refresh!=null)return refresh;
            if(!force&&Fresh)return Task.CompletedTask;
            var done=new TaskCompletionSource<bool>();refresh=done.Task;_=RefreshCore(done);return done.Task;
        }
        async Task RefreshCore(TaskCompletionSource<bool> done)
        {
            try { Snapshot=await api.ConfigAsync(lifetime);fetchedAt=elapsed();Eligibility=null; }
            catch { Snapshot=null;Eligibility=null; }
            finally {refresh=null;Notify();done.TrySetResult(true);}
        }
        public Task RefreshEligibilityAsync(bool newRound=false)
        {
            if(eligibilityRefresh!=null)return eligibilityRefresh;
            var done=new TaskCompletionSource<bool>();eligibilityRefresh=done.Task;_=EligibilityCore(done,newRound);return done.Task;
        }
        async Task EligibilityCore(TaskCompletionSource<bool> done,bool newRound)
        {
            try
            {
                await RefreshAsync();if(!AllowAds){Eligibility=null;return;}
                if(newRound)opportunity=null;
                Eligibility=await api.EligibilityAsync(opportunity,lifetime);eligibleAt=elapsed();
                if(Eligibility.Eligible && opportunity==null)opportunity=await api.OpportunityAsync(lifetime);
            }
            catch {Eligibility=null;}
            finally {eligibilityRefresh=null;Notify();done.TrySetResult(true);}
        }
        public async Task<bool> BeforeShowAsync()
        {
            // Reconcile current backend policy immediately before H3 intent creation; no SDK config comes from it.
            await RefreshAsync(true);await RefreshEligibilityAsync();return Eligible;
        }
        void Notify(){if(lifetime.IsCancellationRequested)return;if(Changed!=null)foreach(Action action in Changed.GetInvocationList())try{action();}catch{}}
    }
}
