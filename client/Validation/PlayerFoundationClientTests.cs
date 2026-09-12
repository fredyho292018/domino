using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Domino.Identity;
using Domino.Infrastructure.Api;
using Domino.Infrastructure.Firebase;
using Domino.Player;

static partial class PlayerFoundationClientTests
{
    static int checks;
    static void Check(bool ok, string name) { checks++; if (!ok) throw new Exception(name); }
    const string Success = "{\"player\":{\"uid\":\"u1\",\"accountType\":\"GUEST\",\"displayName\":\"Guest-ABCDEFGH\",\"language\":\"es\",\"status\":\"ACTIVE\"},\"wallet\":{\"coins\":3000000000}}";
    sealed class Tokens : IAuthTokenProvider
    {
        public readonly List<bool> Refresh = new List<bool>();
        public string Value = "fake-token";
        public Task<string> GetIdTokenAsync(bool refresh, CancellationToken ct) { ct.ThrowIfCancellationRequested(); Refresh.Add(refresh); return Task.FromResult(Value); }
    }
    sealed class Transport : IApiTransport
    {
        public readonly Queue<ApiHttpResponse> Responses = new Queue<ApiHttpResponse>();
        public int Calls;
        public TaskCompletionSource<ApiHttpResponse> Pending;
        public Exception Failure;
        public string ExpectedScheme = "https";
        public Task<ApiHttpResponse> SendAsync(string method, Uri url, string json, string token, int seconds, CancellationToken ct)
        {
            Calls++; Check(url.Scheme == ExpectedScheme && url.AbsolutePath == "/api/v1/player/bootstrap", "Exact secure endpoint");
            Check(token == "fake-token" && seconds > 0, "Token passed transiently");
            Check(json == "{\"language\":\"es\"}", "Only language in body");
            if (Failure != null) return Task.FromException<ApiHttpResponse>(Failure);
            return Pending?.Task ?? Task.FromResult(Responses.Count > 0 ? Responses.Dequeue() : new ApiHttpResponse(200, Success));
        }
    }
    sealed class Identity : IPlayerIdentityService
    {
        public IdentityState State => IdentityState.Ready;
        public PlayerIdentity Current { get; set; } = new PlayerIdentity("u1", true);
        public Exception Error => null;
        public TaskCompletionSource<PlayerIdentity> Pending;
        public int Calls;
        public Task<PlayerIdentity> InitializeAsync() { Calls++; return Pending?.Task ?? Task.FromResult(Current); }
    }
    sealed class GuestSdk : IFirebaseClient
    {
        public int Signs;
        public Task<string> CheckDependenciesAsync() => Task.FromResult("Available");
        public void InitializeApp() { }
        public PlayerIdentity GetCurrentUser() => null;
        public Task<PlayerIdentity> SignInAnonymouslyAsync() { Signs++; return Task.FromResult(new PlayerIdentity("u1", true)); }
    }
    static DominoApiClient Api(Tokens t, Transport h, int seconds = 15) => new DominoApiClient(new DominoApiConfiguration(true, "https://example.invalid", seconds), t, h, new UnityApiJsonCodec());
    static async Task<DominoApiException> Fails(Func<Task> action, ApiFailure category)
    {
        try { await action(); throw new Exception("Expected API failure"); }
        catch (DominoApiException e) { Check(e.Category == category, "Failure category"); Check(!e.ToString().Contains("sensitive-body"), "Sanitized failure"); return e; }
    }
    static async Task TokenTests()
    {
        var user = new PlayerIdentity("u1", true);
        foreach (bool refresh in new[] { false, true })
        {
            bool? actual = null;
            var token = await FirebaseIdTokens.GetAsync(() => user, r => { actual = r; return Task.FromResult("fake"); }, () => user, refresh, default);
            Check(token == "fake" && actual == refresh, "SDK refresh forwarded");
        }
        foreach (string invalid in new[] { null, "", " " })
        {
            try { await FirebaseIdTokens.GetAsync(() => user, r => Task.FromResult(invalid), () => user, false, default); throw new Exception("Empty accepted"); }
            catch (InvalidOperationException) { Check(true, "Empty token rejected"); }
        }
        foreach (bool mismatch in new[] { false, true })
        {
            int calls = 0;
            try { await FirebaseIdTokens.GetAsync(() => mismatch ? user : null, r => { calls++; return Task.FromResult("fake"); }, () => new PlayerIdentity("other", true), false, default); throw new Exception("Identity accepted"); }
            catch (InvalidOperationException) { Check(calls == 0, "No user or mismatch before SDK"); }
        }
        using var ct = new CancellationTokenSource();
        var pending = new TaskCompletionSource<string>();
        var task = FirebaseIdTokens.GetAsync(() => user, r => pending.Task, () => user, false, ct.Token);
        ct.Cancel();
        try { await task; throw new Exception("Cancellation ignored"); } catch (OperationCanceledException) { Check(true, "Token cancellation"); }
        pending.SetResult("late-token");
        pending = new TaskCompletionSource<string>();
        task = FirebaseIdTokens.GetAsync(() => user, r => pending.Task, null, false, default);
        user = new PlayerIdentity("changed", true); pending.SetResult("fake");
        try { await task; throw new Exception("Late identity accepted"); } catch (InvalidOperationException) { Check(true, "Changed SDK user rejected"); }
    }
    public static async Task Main()
    {
        await TokenTests();
        await LocalHttpTests();
        await RetryTests();
        await AliasTests();
        ConnectionTests();
        foreach (string url in new[] { "", "http://localhost:8080", "https://user:pass@example.invalid", "https://example.invalid?q=1", "bad" })
            Check(!new DominoApiConfiguration(true, url).IsAvailable, "Unsafe config rejected");
        var tokens = new Tokens(); var http = new Transport(); var api = Api(tokens, http);
        var response = await api.BootstrapAsync("es", default);
        Check(response.wallet.coins == 3000000000L && tokens.Refresh.Count == 1 && !tokens.Refresh[0], "Long and first token");
        foreach (bool twice in new[] { false, true })
        {
            tokens = new Tokens(); http = new Transport(); api = Api(tokens, http);
            http.Responses.Enqueue(new ApiHttpResponse(401, "{}"));
            if (twice) http.Responses.Enqueue(new ApiHttpResponse(401, "{}"));
            if (twice) await Fails(() => api.BootstrapAsync("es", default), ApiFailure.Authentication);
            else await api.BootstrapAsync("es", default);
            Check(http.Calls == 2 && tokens.Refresh.Count == 2 && !tokens.Refresh[0] && tokens.Refresh[1], "Exactly one refresh/retry");
        }
        foreach (var pair in new[] { (400,"REQUEST_INVALID"), (400,"LANGUAGE_UNSUPPORTED"), (403,"ACCESS_DENIED"), (409,"PLAYER_STATE_CONFLICT"), (409,"WALLET_STATE_INVALID"), (503,"DEPENDENCY_UNAVAILABLE"), (503,"FIRESTORE_CONTENTION_EXHAUSTED"), (500,"INTERNAL_ERROR") })
        {
            tokens = new Tokens(); http = new Transport(); api = Api(tokens, http);
            string id = Guid.NewGuid().ToString();
            http.Responses.Enqueue(new ApiHttpResponse(pair.Item1, "{\"code\":\"" + pair.Item2 + "\",\"message\":\"sensitive-body\",\"requestId\":\"" + id + "\"}"));
            var e = await Fails(() => api.BootstrapAsync("es", default), ApiFailure.Server);
            Check(e.HttpStatus == pair.Item1 && e.ServerErrorCode == pair.Item2 && e.RequestId == id && http.Calls == 1, "Error parsed without retry");
        }
        foreach (string body in new[] { "broken", "{}", Success.Replace("GUEST", "UNKNOWN"), Success.Replace("ACTIVE", "DELETED"), Success.Replace("3000000000", "-1"), Success.Replace("\"coins\":3000000000", ""), Success.Replace("3000000000", "1.5") })
        {
            http = new Transport(); http.Responses.Enqueue(new ApiHttpResponse(200, body)); api = Api(new Tokens(), http);
            await Fails(() => api.BootstrapAsync("es", default), ApiFailure.Contract);
        }
        http = new Transport { Pending = new TaskCompletionSource<ApiHttpResponse>() }; api = Api(new Tokens(), http, 1);
        await Fails(() => api.BootstrapAsync("es", default), ApiFailure.Timeout);
        http.Pending.SetResult(new ApiHttpResponse(200, Success));
        http = new Transport { Failure = new Exception("sensitive-body") }; api = Api(new Tokens(), http);
        await Fails(() => api.BootstrapAsync("es", default), ApiFailure.Transport);
        http = new Transport(); api = Api(new Tokens { Value = "" }, http);
        await Fails(() => api.BootstrapAsync("es", default), ApiFailure.Authentication); Check(http.Calls == 0, "No HTTP with empty token");

        var identity = new Identity();
        http = new Transport { Pending = new TaskCompletionSource<ApiHttpResponse>() }; api = Api(new Tokens(), http);
        var service = new PlayerService(identity, api, () => Task.FromResult("es"), default);
        var first = service.InitializeAsync(); var second = service.InitializeAsync();
        Check(ReferenceEquals(first, second) && service.State == PlayerSyncState.SYNCING && http.Calls == 1, "Shared task");
        http.Pending.SetResult(new ApiHttpResponse(200, Success)); await first;
        Check(service.State == PlayerSyncState.SYNCED && service.Wallet.Coins == 3000000000L && service.Player.Uid == "u1", "Published snapshots");
        await service.InitializeAsync(); Check(http.Calls == 1, "Synced does not bootstrap again");

        foreach (bool changeIdentity in new[] { false, true })
        {
            identity = new Identity(); http = new Transport { Pending = new TaskCompletionSource<ApiHttpResponse>() }; api = Api(new Tokens(), http);
            service = new PlayerService(identity, api, () => Task.FromResult("es"), default);
            first = service.InitializeAsync();
            if (changeIdentity) identity.Current = new PlayerIdentity("other", true);
            http.Pending.SetResult(new ApiHttpResponse(200, changeIdentity ? Success : Success.Replace("u1", "other")));
            await first; Check(service.State == PlayerSyncState.FAILED && service.Wallet == null && service.Player == null, "UID mismatch rejects snapshots");
        }
        identity = new Identity(); http = new Transport(); http.Responses.Enqueue(new ApiHttpResponse(503, "{}")); api = Api(new Tokens(), http);
        service = new PlayerService(identity, api, () => Task.FromResult("es"), default);
        await service.InitializeAsync(); Check(service.State == PlayerSyncState.FAILED && service.Wallet == null, "Failure has no fake wallet");
        await service.InitializeAsync(); Check(service.State == PlayerSyncState.SYNCED && http.Calls == 2, "Explicit retry succeeds");
        http = new Transport { Pending = new TaskCompletionSource<ApiHttpResponse>() }; api = Api(new Tokens(), http);
        using var lifetime = new CancellationTokenSource();
        service = new PlayerService(new Identity(), api, () => Task.FromResult("es"), lifetime.Token);
        first = service.InitializeAsync(); lifetime.Cancel(); await first;
        http.Pending.SetResult(new ApiHttpResponse(200, Success)); await Task.Delay(20);
        Check(service.State == PlayerSyncState.FAILED && service.Wallet == null && service.Error.Category == ApiFailure.Cancelled, "No late publication after shutdown");
        identity = new Identity(); api = new DominoApiClient(new DominoApiConfiguration(false, ""), new Tokens(), new Transport(), new UnityApiJsonCodec());
        service = new PlayerService(identity, api, () => Task.FromResult("es"), default); await service.InitializeAsync();
        Check(service.State == PlayerSyncState.FAILED && identity.Calls == 0, "Disabled API exits independently of Firebase");
        identity = new Identity { Pending = new TaskCompletionSource<PlayerIdentity>() }; http = new Transport(); api = Api(new Tokens(), http);
        service = new PlayerService(identity, api, () => Task.FromResult("es"), default);
        first = service.InitializeAsync(); Check(http.Calls == 0 && !first.IsCompleted, "Waits for identity");
        identity.Pending.SetResult(identity.Current); await first; Check(service.State == PlayerSyncState.SYNCED, "Identity ready resumes bootstrap");
        var sdk = new GuestSdk(); var auth = new FirebaseAuthService(new FirebaseBootstrap(sdk, null), sdk, null);
        http = new Transport(); http.Responses.Enqueue(new ApiHttpResponse(401, "{}")); http.Responses.Enqueue(new ApiHttpResponse(401, "{}"));
        service = new PlayerService(auth, Api(new Tokens(), http), () => Task.FromResult("es"), default);
        await service.InitializeAsync(); Check(service.State == PlayerSyncState.FAILED && sdk.Signs == 1, "Persistent 401 never creates another guest");
        await service.RetryAsync(); Check(service.State == PlayerSyncState.FAILED && !service.CanRetry && sdk.Signs == 1, "Persistent 401 requires auth repair, never another guest");
        Console.WriteLine("PLAYER_FOUNDATION_CLIENT_CHECKS=" + checks + " PASS; REAL_NETWORK_CALLS=0");
    }
    static async Task LocalHttpTests()
    {
        foreach (bool development in new[] { false, true })
        foreach (string environment in new[] { "LOCAL", "PROD", "local", "", null })
        {
            Check(new DominoApiConfiguration(true, "https://api.example.com", 15, environment, development).IsAvailable, "HTTPS_REMOTE_ALLOWED");
            foreach (string host in new[] { "localhost", "127.0.0.1" })
                Check(new DominoApiConfiguration(true, "http://" + host + ":8080", 15, environment, development).IsAvailable ==
                    (development && environment == "LOCAL"), "Local HTTP requires both LOCAL and development");
        }
        foreach (string host in new[] { "example.com", "192.168.1.100", "evil.localhost.example.com", "remotehost", "127.0.0.2", "[::1]", "127.1", "2130706433", "localhost." })
            Check(!new DominoApiConfiguration(true, "http://" + host + ":8080", 15, "LOCAL", true).IsAvailable, "Exact HTTP allowlist rejects " + host);
        foreach (string url in new[] { "http://localhost@evil.example", "http://user@localhost", "http://127.0.0.1?x=1", "http://localhost#x", "not a uri" })
            Check(!new DominoApiConfiguration(true, url, 15, "LOCAL", true).IsAvailable, "Unsafe URI rejected");
        Check(!new DominoApiConfiguration(false, "http://localhost", 15, "LOCAL", true).IsAvailable, "Disabled still unavailable");
        var logs = new List<string>();
        var tokens = new Tokens(); var http = new Transport { ExpectedScheme = "http" };
        var api = new DominoApiClient(new DominoApiConfiguration(true, "http://127.0.0.1:8080", 15, "LOCAL", true), tokens, http, new UnityApiJsonCodec());
        var service = new PlayerService(new Identity(), api, () => Task.FromResult("es"), default, logs.Add);
        await service.InitializeAsync();
        Check(service.State == PlayerSyncState.SYNCED && http.Calls == 1 && tokens.Refresh.Count == 1, "Local bootstrap attempted");
        Check(logs.Count == 2 && logs[0] == "[PLAYER] bootstrap starting" && logs[1] == "[PLAYER] bootstrap succeeded", "Start and success logs");
        logs.Clear(); tokens = new Tokens(); http = new Transport();
        api = new DominoApiClient(new DominoApiConfiguration(true, "http://127.0.0.1", 15, "PROD", true), tokens, http, new UnityApiJsonCodec());
        service = new PlayerService(new Identity(), api, () => Task.FromResult("es"), default, logs.Add);
        await service.InitializeAsync();
        Check(service.State == PlayerSyncState.FAILED && service.Wallet == null && http.Calls == 0 && tokens.Refresh.Count == 0, "Invalid config no token or HTTP");
        Check(logs.Count == 1 && logs[0] == "[PLAYER] bootstrap unavailable reason=API_CONFIGURATION", "Configuration log");
        logs.Clear(); http = new Transport { Failure = new Exception("sensitive-body") };
        service = new PlayerService(new Identity(), Api(new Tokens(), http), () => Task.FromResult("es"), default, logs.Add);
        await service.InitializeAsync();
        Check(service.State == PlayerSyncState.FAILED && logs.Count == 2 && logs[1] == "[PLAYER] bootstrap failed category=Transport", "Safe failure log");
        Check(!string.Join(" ", logs).Contains("sensitive-body") && !string.Join(" ", logs).Contains("fake-token"), "No sensitive logging");
        Console.WriteLine("LOCAL_HTTP_POLICY_AND_LOGGING=PASS (including non-development context)");
    }
}
