using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Domino.Identity;
using Domino.Player;

namespace Domino.Infrastructure.Api
{
    public sealed class DominoApiClient : IDominoApiClient
    {
        static readonly HashSet<string> KnownCodes = new HashSet<string> {
            "AUTH_TOKEN_MISSING", "AUTH_TOKEN_INVALID", "AUTH_TOKEN_EXPIRED", "AUTH_SESSION_INVALID", "ACCESS_DENIED",
            "REQUEST_INVALID", "LANGUAGE_UNSUPPORTED", "PLAYER_STATE_CONFLICT", "WALLET_STATE_INVALID",
            "DEPENDENCY_UNAVAILABLE", "FIRESTORE_CONTENTION_EXHAUSTED", "INTERNAL_ERROR" };
        readonly DominoApiConfiguration settings;
        readonly IAuthTokenProvider tokens;
        readonly IApiTransport transport;
        readonly IApiJsonCodec codec;
        public bool IsAvailable => settings.IsAvailable;
        public DominoApiClient(DominoApiConfiguration settings, IAuthTokenProvider tokens, IApiTransport transport, IApiJsonCodec codec)
        { this.settings = settings; this.tokens = tokens; this.transport = transport; this.codec = codec; }
        public async Task<PlayerBootstrapResponseDto> BootstrapAsync(string language, CancellationToken cancellationToken)
        {
            if (!IsAvailable) throw new DominoApiException(ApiFailure.Configuration);
            if (language != "en" && language != "es") throw new DominoApiException(ApiFailure.Contract);
            var json = codec.Serialize(new PlayerBootstrapRequestDto { language = language });
            for (int attempt = 0; attempt < 2; attempt++)
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds));
                ApiHttpResponse response;
                try
                {
                    string token;
                    try { token = await CancellableTask.Wait(tokens.GetIdTokenAsync(attempt == 1, timeout.Token), timeout.Token); }
                    catch (OperationCanceledException) { throw; }
                    catch { throw new DominoApiException(ApiFailure.Authentication); }
                    if (string.IsNullOrWhiteSpace(token) || token.IndexOfAny(new[] { '\r', '\n' }) >= 0)
                        throw new DominoApiException(ApiFailure.Authentication);
                    response = await CancellableTask.Wait(transport.PostAsync(settings.Endpoint, json, token, settings.TimeoutSeconds, timeout.Token), timeout.Token);
                    timeout.Token.ThrowIfCancellationRequested();
                }
                catch (OperationCanceledException)
                {
                    if (cancellationToken.IsCancellationRequested) throw;
                    throw new DominoApiException(ApiFailure.Timeout);
                }
                catch (DominoApiException) { throw; }
                catch { throw new DominoApiException(ApiFailure.Transport); }
                if (response.Status == 401 && attempt == 0) continue;
                if (response.Status != 200) throw ParseError(response);
                try
                {
                    var dto = codec.ReadSuccess(response.Body);
                    PlayerSnapshotMapper.Player(dto?.player);
                    PlayerSnapshotMapper.Wallet(dto?.wallet);
                    return dto;
                }
                catch { throw new DominoApiException(ApiFailure.Contract, response.Status); }
            }
            throw new DominoApiException(ApiFailure.Authentication, 401);
        }
        DominoApiException ParseError(ApiHttpResponse response)
        {
            string code = null, requestId = null;
            try
            {
                var error = codec.ReadError(response.Body);
                if (error?.code != null && KnownCodes.Contains(error.code)) code = error.code;
                if (Guid.TryParse(error?.requestId, out var id)) requestId = id.ToString();
            }
            catch { /* HTML, proxies and malformed error bodies must remain sanitized. */ }
            return new DominoApiException(response.Status == 401 ? ApiFailure.Authentication : ApiFailure.Server,
                response.Status, code, requestId);
        }
    }
}
