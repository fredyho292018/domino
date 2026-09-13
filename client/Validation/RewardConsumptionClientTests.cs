using System;
using System.Threading;
using System.Threading.Tasks;
using Domino.Ads;
using Domino.Identity;
using Domino.Infrastructure;
using Domino.Infrastructure.Api;
using Domino.Player;

static class RewardConsumptionClientTests
{
    static int checks;
    const string Id = "12345678-1234-4234-8234-123456789012";
    static void Check(bool b, string why) { checks++; if (!b) throw new Exception(why); }
    static RewardIntentReceipt Receipt(string state) => new RewardIntentReceipt(Id, state, DateTimeOffset.UtcNow.AddMinutes(10));
    sealed class Identity : IPlayerIdentityService
    {
        public IdentityState State => IdentityState.Ready;
        public PlayerIdentity Current { get; set; } = new PlayerIdentity("u1", true);
        public Exception Error => null;
        public Task<PlayerIdentity> InitializeAsync() => Task.FromResult(Current);
    }
    sealed class Foundation : IDominoApiClient
    {
        public bool IsAvailable => true;
        public int Calls;
        public Task<PlayerBootstrapResponseDto> BootstrapAsync(string language, CancellationToken token)
        { Calls++; return Task.FromResult(new PlayerBootstrapResponseDto {
            player = new PlayerResponseDto { uid="u1", accountType="GUEST", displayName="Guest-ABCDEFGH", language="en", status="ACTIVE" },
            wallet = new WalletResponseDto { coins=0 } }); }
        public Task<PlayerBootstrapResponseDto> UpdateDisplayNameAsync(string name, CancellationToken token) => throw new Exception("Unexpected alias write");
    }
    sealed class Api : IRewardIntentApi, IRewardConsumptionApi
    {
        public int Consumes; public bool Fail, CommitThenFail, Applied, Foreign;
        public TaskCompletionSource<bool> Gate;
        public TaskCompletionSource<RewardIntentReceipt> CreateGate;
        public Task<RewardIntentReceipt> CreateAsync(CancellationToken ct) => CreateGate?.Task ?? Task.FromResult(Receipt("ISSUED"));
        public Task<RewardIntentReceipt> StatusAsync(string id, CancellationToken ct) => Task.FromResult(Receipt(Applied ? "CONSUMED" : "VERIFIED"));
        public Task<RewardIntentReceipt> PendingAsync(CancellationToken ct) => Task.FromResult(Applied ? null : Receipt("VERIFIED"));
        public async Task<RewardConsumeReceipt> ConsumeRewardAsync(string id, CancellationToken ct)
        {
            Check(id == Id, "only intent identifier requested"); Consumes++;
            if (Gate != null) await Gate.Task;
            if (CommitThenFail) { Applied = true; CommitThenFail = false; throw new DominoApiException(ApiFailure.Timeout); }
            if (Fail) throw new DominoApiException(ApiFailure.Transport);
            Applied = true;
            return new RewardConsumeReceipt(Foreign ? "00000000-0000-4000-8000-000000000000" : Id, 10, 10);
        }
    }
    sealed class Rig
    {
        public readonly Identity Identity = new Identity();
        public readonly Foundation Foundation = new Foundation();
        public readonly Api Api = new Api();
        public readonly PlayerService Player;
        public readonly RewardVerificationService Flow;
        public Rig()
        {
            Player = new PlayerService(Identity, Foundation, () => Task.FromResult("en"), CancellationToken.None);
            Flow = new RewardVerificationService(Api, CancellationToken.None, new PlayerRewardWalletReceiver(Player));
        }
        public async Task Ready()
        { await Player.InitializeAsync(); await Flow.CreateAsync(default); await Flow.RefreshAsync(); }
    }
    sealed class Tokens : IAuthTokenProvider
    { public Task<string> GetIdTokenAsync(bool refresh, CancellationToken ct) => Task.FromResult("fake"); }
    sealed class Transport : IApiTransport
    {
        public string Method, Body; public Uri Url;
        public Task<ApiHttpResponse> SendAsync(string method, Uri url, string json, string token, int seconds, CancellationToken ct)
        {
            Method=method; Body=json; Url=url;
            return Task.FromResult(new ApiHttpResponse(200, method == "GET" ? "{\"intent\":null}" :
                "{\"reward\":{\"type\":\"COINS\",\"amount\":10},\"wallet\":{\"coins\":10},\"intent\":{\"intentId\":\"" + Id + "\",\"status\":\"CONSUMED\"}}"));
        }
    }
    static async Task Main()
    {
        var r = new Rig(); await r.Ready();
        var original = r.Player.Wallet;
        Check(r.Flow.State == RewardVerificationState.VERIFIED, "verified before consume");
        r.Api.Gate = new TaskCompletionSource<bool>();
        var first = r.Flow.ConsumeRewardAsync(); var repeat = r.Flow.ConsumeRewardAsync();
        Check(ReferenceEquals(first, repeat) && r.Api.Consumes == 1, "single flight consume");
        Check(ReferenceEquals(original, r.Player.Wallet) && r.Flow.State == RewardVerificationState.CONSUMING, "no optimistic mutation");
        var bootstrapDuring = r.Player.RetryAsync();
        Check(r.Foundation.Calls == 1, "shared player operation gate prevents stale bootstrap");
        r.Api.Gate.SetResult(true); Check(await first, "backend consume success"); await bootstrapDuring;
        Check(r.Player.Wallet.Coins == 10 && !ReferenceEquals(original, r.Player.Wallet), "replace snapshot from response");
        Check(r.Flow.State == RewardVerificationState.CONSUMED, "consumed state");
        Check(await r.Flow.ConsumeRewardAsync() && r.Player.Wallet.Coins == 10, "retry absolute balance no local addition");
        r.Player.Dispose();

        r = new Rig(); await r.Ready();
        r.Api.CreateGate = new TaskCompletionSource<RewardIntentReceipt>();
        var creating = r.Flow.CreateAsync(default);
        Check(!await r.Flow.ConsumeRewardAsync() && r.Api.Consumes == 0, "consume cannot race a pending ad intent request");
        r.Api.CreateGate.SetResult(Receipt("ISSUED")); await creating; r.Player.Dispose();

        foreach (var scenario in new[] { "offline", "timeout_after_commit", "foreign", "identity" })
        {
            r = new Rig(); await r.Ready(); original = r.Player.Wallet;
            r.Api.Fail = scenario == "offline"; r.Api.CommitThenFail = scenario == "timeout_after_commit"; r.Api.Foreign = scenario == "foreign";
            if (scenario == "identity") r.Api.Gate = new TaskCompletionSource<bool>();
            var pending = r.Flow.ConsumeRewardAsync();
            if (scenario == "identity") { r.Identity.Current = new PlayerIdentity("other", true); r.Api.Gate.SetResult(true); }
            Check(!await pending && ReferenceEquals(original, r.Player.Wallet), "failure keeps wallet " + scenario);
            Check(r.Flow.Current.Status == "VERIFIED", "failure keeps retryable intent");
            if (scenario == "timeout_after_commit")
                Check(await r.Flow.ConsumeRewardAsync() && r.Player.Wallet.Coins == 10, "unknown outcome retry applies one backend balance");
            r.Player.Dispose();
        }
        r = new Rig(); await r.Player.InitializeAsync(); await r.Flow.CreateAsync(default); r.Flow.ClientEarned(Id);
        Check(!await r.Flow.ConsumeRewardAsync() && r.Api.Consumes == 0, "client earned alone cannot consume");
        await r.Flow.RecoverPendingAsync();
        Check(r.Flow.State == RewardVerificationState.VERIFIED && await r.Flow.ConsumeRewardAsync(), "pending discovery and consume");
        r.Player.Dispose();

        var transport = new Transport(); var client = new RewardIntentApiClient(new DominoApiConfiguration(true, "https://example.invalid"),
            new Tokens(), transport, new UnityRewardIntentCodec());
        Check((await client.ConsumeRewardAsync(Id, default)).Coins == 10, "consume parsed");
        Check(transport.Method == "POST" && transport.Body == "{}" && transport.Url.AbsolutePath.EndsWith("/" + Id + "/consume"), "no client amount uid or ledger id");
        Check(await client.PendingAsync(default) == null && transport.Method == "GET" && transport.Body == null, "pending contract");
        foreach (var invalid in new[] { "{}", "{\"wallet\":{\"coins\":-1}}", "{\"intent\":{\"status\":\"VERIFIED\"}}", "[]",
            "{\"intent\":{},\"intent\":{}}" })
        {
            try { new UnityRewardIntentCodec().ReadConsume(invalid); throw new Exception("accepted"); }
            catch (Exception e) { Check(e.Message != "accepted", "invalid consume response rejected"); }
        }
        Console.WriteLine("UNITY_H4_TESTS=PASS CHECKS=" + checks + "; REAL_NETWORK=0; CLIENT_LOCAL_CREDIT=0");
    }
}
