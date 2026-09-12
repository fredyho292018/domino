using System;
using System.Threading;
using System.Threading.Tasks;
using Domino.Infrastructure.Api;
using Domino.Player;

static partial class PlayerFoundationClientTests
{
    sealed class AliasTransport : IApiTransport
    {
        public int Calls, Puts;
        public long Status = 200;
        public string Body;
        public Exception Failure;
        public TaskCompletionSource<ApiHttpResponse> Pending;
        public Task<ApiHttpResponse> SendAsync(string method, Uri uri, string json, string token, int seconds, CancellationToken ct)
        {
            Calls++;
            if (method == "PUT") {
                Puts++; Check(uri.AbsolutePath == "/api/v1/player/display-name", "Alias endpoint");
                Check(json == "{\"displayName\":\"Fredy92\"}", "Only displayName sent");
            } else Check(method == "POST", "Bootstrap POST preserved");
            if (Failure != null) return Task.FromException<ApiHttpResponse>(Failure);
            return Pending?.Task ?? Task.FromResult(new ApiHttpResponse(Status, Body ?? (method == "PUT" ? Success.Replace("Guest-ABCDEFGH", "Fredy92") : Success)));
        }
    }
    static async Task AliasTests()
    {
        foreach (string value in new[] { "abc", "ABCDEFGHIJKLMNOP", "Fredy92", "Fredy_HO", "player-99" }) Check(DisplayNameRules.IsValid(value), "Valid alias");
        foreach (string value in new[] { null, "", "ab", "ABCDEFGHIJKLMNOPQ", " Fredy", "Fredy ", "Fredy HO", "Fredy\n", "Fredy!", "🔥Fredy", "ñame", "https://test" }) Check(!DisplayNameRules.IsValid(value), "Invalid alias");
        Check(DisplayNameRules.IsGenerated("Guest-6DH9C7CA") && !DisplayNameRules.IsGenerated("Fredy92"), "Generated name prompt");
        var http = new AliasTransport(); var tokens = new Tokens(); var identity = new Identity();
        var api = new DominoApiClient(new DominoApiConfiguration(true, "https://example.invalid"), tokens, http, new UnityApiJsonCodec());
        var service = new PlayerService(identity, api, () => Task.FromResult("es"), default);
        await service.UpdateDisplayNameAsync("Fredy92"); Check(http.Calls == 0 && service.Player == null, "No rename before bootstrap");
        await service.InitializeAsync(); var wallet = service.Wallet; int changes = 0;
        service.SnapshotChanged += _ => throw new Exception("Subscriber isolation"); service.SnapshotChanged += _ => changes++;
        http.Pending = new TaskCompletionSource<ApiHttpResponse>();
        var first = service.UpdateDisplayNameAsync("Fredy92");
        Check(service.IsSaving && !service.CanEdit && ReferenceEquals(first, service.UpdateDisplayNameAsync("Fredy92")) && ReferenceEquals(first, service.RetryAsync()), "Single flight across rename and retry");
        http.Pending.SetResult(new ApiHttpResponse(200, Success.Replace("Guest-ABCDEFGH", "Fredy92"))); await first; http.Pending = null;
        Check(service.Player.DisplayName == "Fredy92" && ReferenceEquals(wallet, service.Wallet) && changes == 1 && service.CanEdit, "Confirmed alias and immutable wallet preserved");
        Check(identity.Calls == 1, "No second auth initialization");
        int count = http.Calls;
        await service.UpdateDisplayNameAsync("bad value"); Check(http.Calls == count && service.Error.ServerErrorCode == "DISPLAY_NAME_INVALID" && service.IsFresh, "Invalid local input never sent");
        http.Status = 400; http.Body = "{\"code\":\"DISPLAY_NAME_RESERVED\",\"requestId\":\"00000000-0000-0000-0000-000000000001\"}";
        await service.UpdateDisplayNameAsync("Fredy92"); Check(service.Error.ServerErrorCode == "DISPLAY_NAME_RESERVED" && service.Player.DisplayName == "Fredy92" && service.CanEdit, "Reserved error editable without losing snapshots");
        http.Status = 200; http.Body = null; http.Failure = new Exception();
        await service.UpdateDisplayNameAsync("Fredy92"); Check(service.CanRetry && !service.CanEdit && service.HasConfirmedSnapshots && ReferenceEquals(wallet, service.Wallet), "Offline stale cached alias and wallet");
        http.Failure = null; http.Body = Success.Replace("Guest-ABCDEFGH", "Fredy92"); await service.RetryAsync();
        Check(service.IsFresh && service.Player.DisplayName == "Fredy92", "Retry recovers alias");
        http.Body = http.Body.Replace("u1", "other"); var player = service.Player;
        await service.UpdateDisplayNameAsync("Fredy92"); Check(service.Error.Category == ApiFailure.Contract && ReferenceEquals(player, service.Player), "Foreign UID rejected");
        http = new AliasTransport(); tokens = new Tokens(); api = new DominoApiClient(new DominoApiConfiguration(true, "https://example.invalid"), tokens, http, new UnityApiJsonCodec());
        http.Status = 401; await Fails(() => api.UpdateDisplayNameAsync("Fredy92", default), ApiFailure.Authentication);
        Check(http.Puts == 2 && tokens.Refresh.Count == 2 && !tokens.Refresh[0] && tokens.Refresh[1], "PUT refreshes token once after401");
        http.Status = 200; http.Pending = new TaskCompletionSource<ApiHttpResponse>();
        service = new PlayerService(new Identity(), api, () => Task.FromResult("es"), default);
        http.Pending.SetResult(new ApiHttpResponse(200, Success)); await service.InitializeAsync();
        http.Pending = new TaskCompletionSource<ApiHttpResponse>(); using var cancel = new CancellationTokenSource();
        first = service.UpdateDisplayNameAsync("Fredy92", cancel.Token); cancel.Cancel(); await first;
        http.Pending.SetResult(new ApiHttpResponse(200, Success.Replace("Guest-ABCDEFGH", "Fredy92")));
        Check(service.Error.Category == ApiFailure.Cancelled && service.Player.DisplayName == "Guest-ABCDEFGH" && !service.IsSaving, "Cancellation suppresses late rename");
        Console.WriteLine("PLAYER_ALIAS_VALIDATION_API_SERVICE=PASS");
    }
}
