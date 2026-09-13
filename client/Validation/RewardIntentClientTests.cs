using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Domino.Ads;
using Domino.Identity;
using Domino.Infrastructure.Api;

static class RewardIntentClientTests
{
    static int checks;
    const string Id = "12345678-1234-4234-8234-123456789012";
    static RewardIntentReceipt Receipt(string state = "ISSUED") => new RewardIntentReceipt(Id, state, DateTimeOffset.UtcNow.AddMinutes(10));
    static void Check(bool b, string why) { checks++; if (!b) throw new Exception(why); }
    sealed class Api : IRewardIntentApi
    {
        public int Creates, Reads;
        public string Status = "ISSUED";
        public bool Fail;
        public TaskCompletionSource<RewardIntentReceipt> Pending;
        public Task<RewardIntentReceipt> CreateAsync(CancellationToken token)
        { Creates++; if (Fail) throw new Exception("private"); return Pending?.Task ?? Task.FromResult(Receipt()); }
        public Task<RewardIntentReceipt> StatusAsync(string id, CancellationToken token)
        { Reads++; return Task.FromResult(Receipt(Status)); }
    }
    sealed class Gate : IAdsConsentGate { public bool CanInitializeAds => true; }
    sealed class Init : IAdsService
    { public AdsState State => AdsState.READY; public string UnavailableReason => null; public Task InitializeAsync() => Task.CompletedTask; public void Dispose() {} }
    sealed class Ad : IRewardedAd
    {
        public int Shows, Disposals; public string CustomData;
        public bool CanShow => Disposals == 0;
        public event Action Opened, Closed, Failed;
        Action<RewardedCompletionResult> callback;
        public void SetVerificationIntent(string id) { Check(Shows == 0, "SSV before Show"); CustomData = id; }
        public void Show(Action<RewardedCompletionResult> cb)
        { Check(CustomData == Id, "opaque custom_data attached"); Shows++; callback = cb; Opened?.Invoke(); }
        public void Earn() => callback(new RewardedCompletionResult("anything", 99999));
        public void Close() => Closed?.Invoke();
        public void Dispose() { Disposals++; }
    }
    sealed class Loader : IRewardedAdLoader
    {
        public int Loads; public Ad Last;
        public Task<IRewardedAd> LoadAsync(string id) { Loads++; Last = new Ad(); return Task.FromResult<IRewardedAd>(Last); }
    }
    sealed class Token : IAuthTokenProvider
    {
        public readonly List<bool> Refresh = new List<bool>();
        public Task<string> GetIdTokenAsync(bool forceRefresh, CancellationToken cancellationToken)
        { Refresh.Add(forceRefresh); return Task.FromResult("test-bearer"); }
    }
    sealed class Transport : IApiTransport
    {
        public int Calls; public bool UnauthorizedOnce; public string Method, Body; public Uri Url;
        public Task<ApiHttpResponse> SendAsync(string method, Uri uri, string body, string token, int seconds, CancellationToken ct)
        {
            Check(token == "test-bearer", "bearer sent through authenticated transport");
            Calls++; Method = method; Body = body; Url = uri;
            return Task.FromResult(new ApiHttpResponse(UnauthorizedOnce && Calls == 1 ? 401 : 200,
                "{\"intentId\":\"" + Id + "\",\"status\":\"ISSUED\",\"expiresAt\":\"2099-01-01T00:00:00Z\",\"reward\":{\"type\":\"COINS\",\"previewAmount\":10}}"));
        }
    }
    static async Task Main()
    {
        var api = new Api();
        var verification = new RewardVerificationService(api, CancellationToken.None);
        var loader = new Loader();
        var config = new AdsConfiguration(true, AdsEnvironment.DEVELOPMENT, AdsPlatform.Android, true);
        var service = new GoogleRewardedAdsService(config, new Init(), new Gate(), loader, intents: verification);
        await service.LoadRewardedAsync();
        Check(api.Creates == 0 && loader.Loads == 1, "preload independent");
        api.Pending = new TaskCompletionSource<RewardIntentReceipt>();
        var first = service.ShowRewardedAsync();
        Check(api.Creates == 1 && loader.Last.Shows == 0, "intent before Show");
        Check(await service.ShowRewardedAsync() == RewardedShowResult.Unavailable && api.Creates == 1, "double Show during intent blocked");
        api.Pending.SetResult(Receipt());
        Check(loader.Last.Shows == 1, "Show after intent");
        var ad = loader.Last;
        ad.Earn();
        Check(verification.State == RewardVerificationState.CLIENT_EARNED, "client earned is not VERIFIED");
        Check(api.Reads == 0, "no automatic polling");
        await verification.RefreshAsync();
        Check(verification.State == RewardVerificationState.CLIENT_EARNED && api.Reads == 1, "manual pending status");
        api.Status = "VERIFIED";
        await verification.RefreshAsync();
        Check(verification.State == RewardVerificationState.VERIFIED, "server verified before close");
        ad.Earn();
        Check(verification.State == RewardVerificationState.VERIFIED, "late earned cannot downgrade verification");
        ad.Close();
        Check(await first == RewardedShowResult.Earned && loader.Loads == 2 && ad.Disposals == 1, "H2 reload preserved");
        Check(api.Creates == 1 && loader.Last.CustomData == null, "reload does not request/reuse SSV implicitly");
        service.Dispose();

        api = new Api { Fail = true };
        loader = new Loader();
        var blocked = new GoogleRewardedAdsService(config, new Init(), new Gate(), loader,
            intents: new RewardVerificationService(api, CancellationToken.None));
        await blocked.InitializeAsync();
        Check(await blocked.ShowRewardedAsync() == RewardedShowResult.Unavailable && loader.Last.Shows == 0, "offline never Show");
        blocked.Dispose();
        var missing = new GoogleRewardedAdsService(config, new Init(), new Gate(), new Loader());
        await missing.InitializeAsync();
        Check(await missing.ShowRewardedAsync() == RewardedShowResult.Unavailable, "missing authorization fails closed");
        missing.Dispose();
        api = new Api(); verification = new RewardVerificationService(api, CancellationToken.None);
        await verification.CreateAsync(CancellationToken.None); api.Status = "EXPIRED"; await verification.RefreshAsync();
        Check(verification.State == RewardVerificationState.EXPIRED, "expired mapped");
        verification.ClientEarned(Id);
        Check(verification.State == RewardVerificationState.EXPIRED, "late client callback cannot revive expired intent");
        var tokens = new Token(); var transport = new Transport { UnauthorizedOnce = true };
        var client = new RewardIntentApiClient(new DominoApiConfiguration(true, "http://127.0.0.1:8080", 15, "LOCAL", true),
            tokens, transport, new UnityRewardIntentCodec());
        Check((await client.CreateAsync(CancellationToken.None)).IntentId == Id, "response parsed");
        Check(transport.Body == "{}" && transport.Method == "POST", "no UID or amount in request");
        Check(transport.Url.AbsolutePath == "/api/v1/economy/ad-rewards/intents", "create URL");
        Check(tokens.Refresh.Count == 2 && !tokens.Refresh[0] && tokens.Refresh[1], "401 one forced refresh");
        await client.StatusAsync(Id, CancellationToken.None);
        Check(transport.Method == "GET" && transport.Body == null && transport.Url.AbsolutePath.EndsWith("/" + Id), "status URL");
        foreach (var bad in new[] { "{}", "{\"intentId\":\"foreign\"}", "[]", "{\"intentId\":\"a\",\"intentId\":\"b\"}" })
        {
            try { new UnityRewardIntentCodec().Read(bad); throw new Exception("accepted"); }
            catch (Exception e) { Check(e.Message != "accepted", "invalid contract rejected"); }
        }
        var unavailable = new RewardIntentApiClient(new DominoApiConfiguration(false, ""), tokens, transport, new UnityRewardIntentCodec());
        int count = transport.Calls;
        try { await unavailable.CreateAsync(CancellationToken.None); throw new Exception("accepted"); }
        catch (DominoApiException) { Check(transport.Calls == count, "disabled API no request"); }
        Console.WriteLine("UNITY_H3_TESTS=PASS CHECKS=" + checks + "; WALLET_MUTATIONS=0; COINS_CREDITED=0; REAL_AD_REQUESTS=0");
    }
}
