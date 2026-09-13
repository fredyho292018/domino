using System;
using System.Threading;
using System.Threading.Tasks;
using Domino.Identity;
using Domino.Infrastructure.Api;
using Domino.Infrastructure;

namespace Domino.Ads
{
    public sealed class RewardIntentApiClient : IRewardIntentApi, IRewardConsumptionApi
    {
        readonly DominoApiConfiguration settings;
        readonly IAuthTokenProvider tokens;
        readonly IApiTransport transport;
        readonly IRewardIntentCodec codec;
        public RewardIntentApiClient(DominoApiConfiguration settings, IAuthTokenProvider tokens, IApiTransport transport, IRewardIntentCodec codec)
        { this.settings = settings; this.tokens = tokens; this.transport = transport; this.codec = codec; }
        public Task<RewardIntentReceipt> CreateAsync(CancellationToken token) => Send("POST", "intents", token, codec.Read);
        public Task<T> GetResourceAsync<T>(string resource,CancellationToken token,Func<string,T> read)=>Send("GET",resource,token,read);
        public Task<T> PostResourceAsync<T>(string resource,CancellationToken token,Func<string,T> read)=>Send("POST",resource,token,read);
        public Task<RewardConsumeReceipt> ConsumeRewardAsync(string id, CancellationToken token)
        {
            if (!Guid.TryParseExact(id, "D", out var guid) || guid == Guid.Empty) throw new FormatException("INTENT_CONTRACT");
            return Send("POST", "intents/" + guid.ToString("D") + "/consume", token, ((IRewardConsumptionCodec)codec).ReadConsume);
        }
        public Task<RewardIntentReceipt> PendingAsync(CancellationToken token) =>
            Send("GET", "pending", token, ((IRewardConsumptionCodec)codec).ReadPending);
        public Task<RewardIntentReceipt> StatusAsync(string id, CancellationToken token)
        {
            if (!Guid.TryParseExact(id, "D", out var guid) || guid == Guid.Empty) throw new FormatException("INTENT_CONTRACT");
            return Send("GET", "intents/" + guid.ToString("D"), token, codec.Read);
        }
        async Task<T> Send<T>(string method, string resource, CancellationToken cancellationToken, Func<string, T> read)
        {
            if (!settings.IsAvailable) throw new DominoApiException(ApiFailure.Configuration);
            // Preserve any configured base path, using the existing validated REST endpoint.
            var endpoint = new Uri(settings.Endpoint, "../economy/ad-rewards/" + resource);
            for (int attempt = 0; attempt < 2; attempt++)
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds));
                try
                {
                    var bearer = await CancellableTask.Wait(tokens.GetIdTokenAsync(attempt == 1, timeout.Token), timeout.Token);
                    if (string.IsNullOrWhiteSpace(bearer) || bearer.IndexOfAny(new[] { '\r', '\n' }) >= 0)
                        throw new DominoApiException(ApiFailure.Authentication);
                    var response = await CancellableTask.Wait(transport.SendAsync(method, endpoint, method == "POST" ? "{}" : null,
                        bearer, settings.TimeoutSeconds, timeout.Token), timeout.Token);
                    timeout.Token.ThrowIfCancellationRequested();
                    if (response.Status == 401 && attempt == 0) continue;
                    if (response.Status != 200) throw new DominoApiException(response.Status == 401 ? ApiFailure.Authentication : ApiFailure.Server, response.Status);
                    try { return read(response.Body); } catch { throw new DominoApiException(ApiFailure.Contract); }
                }
                catch (OperationCanceledException)
                {
                    if (cancellationToken.IsCancellationRequested) throw;
                    throw new DominoApiException(ApiFailure.Timeout);
                }
                catch (DominoApiException) { throw; }
                catch { throw new DominoApiException(ApiFailure.Transport); }
            }
            throw new DominoApiException(ApiFailure.Authentication);
        }
    }
}
