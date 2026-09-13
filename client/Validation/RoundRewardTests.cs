using System;
using System.Threading;
using System.Threading.Tasks;
using Domino.Ads;
using Domino.Rewards;

static class RoundRewardTests
{
    static int checks;
    static void Check(bool value, string label) { checks++; if (!value) throw new Exception(label); }
    sealed class Api : IRewardIntentApi, IRewardConsumptionApi, IRewardWalletReceiver
    {
        public string Id = Guid.NewGuid().ToString(), Status = "ISSUED";
        public bool Offline, Reject, NeverVerify, TimeoutAfterCommit;
        public int Creates, Consumes, Credits;
        public long ServerCoins, Wallet;
        public TaskCompletionSource<bool> VerificationGate;
        public RewardIntentReceipt Receipt() => new RewardIntentReceipt(Id,Status,DateTimeOffset.UtcNow.AddMinutes(10));
        public Task<RewardIntentReceipt> CreateAsync(CancellationToken token)
        { Creates++; if (Offline) throw new Exception(); Id=Guid.NewGuid().ToString(); Status="ISSUED"; return Task.FromResult(Receipt()); }
        public async Task<RewardIntentReceipt> StatusAsync(string id,CancellationToken token)
        { if (VerificationGate != null) await VerificationGate.Task; if (!NeverVerify) Status=Reject?"REJECTED":"VERIFIED"; return Receipt(); }
        public Task<RewardIntentReceipt> PendingAsync(CancellationToken token) => Task.FromResult(Status=="VERIFIED"?Receipt():null);
        public Task<RewardConsumeReceipt> ConsumeRewardAsync(string id,CancellationToken token)
        {
            Consumes++; if (Status!="CONSUMED") { ServerCoins+=10; Credits++; Status="CONSUMED"; }
            if (TimeoutAfterCommit) { TimeoutAfterCommit=false; throw new TimeoutException(); }
            return Task.FromResult(new RewardConsumeReceipt(id,10,ServerCoins));
        }
        public async Task<bool> ApplyAsync(Func<CancellationToken,Task<long>> request,CancellationToken token)
        { try { Wallet=await request(token); return true; } catch { return false; } }
    }
    sealed class Ad : IRewardedAdsService
    {
        readonly RewardVerificationService verification;
        public Ad(RewardVerificationService v) { verification=v; }
        public RewardedState State {get;set;}=RewardedState.READY;
        public bool IsAvailable=>State==RewardedState.READY;
        public int Shows;
        public RewardedShowResult Result=RewardedShowResult.Earned;
        public TaskCompletionSource<bool> Gate;
        public event Action<RewardedState> RewardedStateChanged;
        public event Action<bool> RewardedAvailabilityChanged;
        public event Action<RewardedCompletionResult> RewardEarned;
        public Task InitializeAsync()=>Task.CompletedTask;
        public Task LoadRewardedAsync()=>Task.CompletedTask;
        public void PreloadIfNeeded(){}
        public async Task<RewardedShowResult> ShowRewardedAsync()
        {
            try { var intent=await verification.CreateAsync(default); Shows++; State=RewardedState.SHOWING; RewardedStateChanged?.Invoke(State);
                if(Gate!=null) await Gate.Task;
                if(Result==RewardedShowResult.Earned){verification.ClientEarned(intent.IntentId); verification.ClientEarned(intent.IntentId);}
                State=RewardedState.READY; RewardedStateChanged?.Invoke(State); return Result;
            } catch {return RewardedShowResult.Unavailable;}
        }
        public void Dispose(){}
    }
    static (Api api, Ad ads, RoundRewardFlow flow) Setup()
    {
        var api=new Api(); var verification=new RewardVerificationService(api,default,api); var ads=new Ad(verification);
        var flow=new RoundRewardFlow(ads,verification,()=>true,default,(ms,ct)=>Task.CompletedTask);
        flow.PresentRound(new object()); return(api,ads,flow);
    }
    static async Task Main()
    {
        var (api,ads,flow)=Setup(); int notifications=0; flow.Confirmed+=amount=>{Check(amount==10,"confirmed backend amount");notifications++;};
        Check(flow.State==RoundRewardState.AVAILABLE && flow.CanWatch,"available"); Check(flow.PreviewCoins==10,"preview");
        ads.Gate=new TaskCompletionSource<bool>(); var first=flow.WatchAsync(); await flow.WatchAsync();
        Check(ads.Shows==1 && api.Creates==1,"duplicate taps single flight"); Check(api.Wallet==0,"no early wallet credit");
        Check(flow.State==RoundRewardState.WATCHING && !flow.CanWatch,"watch lock");
        ads.Gate.SetResult(true); await first;
        Check(api.Wallet==10 && api.Credits==1 && api.Consumes==1,"confirmed wallet");
        Check(flow.State==RoundRewardState.REWARDED && notifications==1,"success once");
        await flow.WatchAsync(); await flow.RetryAsync(); Check(ads.Shows==1 && api.Credits==1,"one per round");
        flow.LeaveRound(); Check(flow.State==RoundRewardState.HIDDEN,"leave panel");
        flow.PresentRound(new object()); Check(flow.CanWatch,"new round opportunity");
        await flow.WatchAsync(); Check(api.Wallet==20 && api.Credits==2,"new round grant");

        var offline=Setup(); offline.api.Offline=true; await offline.flow.WatchAsync();
        Check(offline.ads.Shows==0 && offline.api.Wallet==0,"offline no show credit");
        var cancel=Setup(); cancel.ads.Result=RewardedShowResult.Closed; await cancel.flow.WatchAsync();
        Check(cancel.api.Wallet==0 && cancel.api.Consumes==0,"close no earn"); Check(cancel.flow.MessageKey=="reward.incomplete","cancel copy");
        var failed=Setup(); failed.ads.Result=RewardedShowResult.Failed; await failed.flow.WatchAsync(); Check(failed.api.Credits==0,"failed ad no reward");
        var disabled=Setup(); disabled.ads.State=RewardedState.DISABLED; await disabled.flow.WatchAsync(); await disabled.flow.RecoverAsync();
        Check(disabled.flow.State==RoundRewardState.HIDDEN && disabled.api.Creates==0 && disabled.ads.Shows==0,"disabled no calls");
        var timeout=Setup(); timeout.api.TimeoutAfterCommit=true; await timeout.flow.WatchAsync();
        Check(timeout.api.Consumes==2 && timeout.api.Credits==1 && timeout.api.Wallet==10,"timeout committed replay");
        var rejected=Setup(); rejected.api.Reject=true; await rejected.flow.WatchAsync(); Check(rejected.api.Consumes==0,"rejected no consume");
        var pending=Setup(); pending.api.NeverVerify=true; await pending.flow.WatchAsync();
        Check(pending.flow.CanRetry && pending.api.Wallet==0,"bounded verification pending");
        pending.api.NeverVerify=false; await pending.flow.RetryAsync(); Check(pending.api.Credits==1 && pending.api.Creates==1,"same intent retry");
        var background=Setup(); background.api.VerificationGate=new TaskCompletionSource<bool>(); var work=background.flow.WatchAsync();
        background.flow.LeaveRound(); background.api.VerificationGate.SetResult(true); await work;
        Check(background.api.Wallet==10 && background.flow.State==RoundRewardState.HIDDEN,"background completion");
        var restart=Setup(); restart.api.Status="VERIFIED"; await restart.flow.RecoverAsync(); await restart.flow.RecoverAsync();
        Check(restart.api.Wallet==10 && restart.api.Credits==1 && restart.ads.Shows==0,"restart pending once");
        Check(restart.flow.CanWatch,"recovery does not consume new round eligibility");
        var recoveringApi=new Api { NeverVerify=true };
        var recoveringVerification=new RewardVerificationService(recoveringApi,default,recoveringApi);
        var recoveringAds=new Ad(recoveringVerification); bool synced=true;
        var recoveringFlow=new RoundRewardFlow(recoveringAds,recoveringVerification,()=>synced,default,(ms,ct)=>Task.CompletedTask,()=>true);
        recoveringFlow.PresentRound(new object()); await recoveringFlow.WatchAsync(); synced=false;
        Check(recoveringFlow.CanRetry,"pending retry with confirmed snapshot after offline failure");
        recoveringApi.NeverVerify=false; await recoveringFlow.RetryAsync(); Check(recoveringApi.Wallet==10,"offline pending retry resolved");
        Console.WriteLine($"H5_FLOW_TESTS=PASS CHECKS={checks}; REAL_NETWORK=0; LIVE_WALLET_CREDIT=0");
    }
}
