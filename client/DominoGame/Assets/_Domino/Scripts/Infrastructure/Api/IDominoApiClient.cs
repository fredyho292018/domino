using System;
using System.Threading;
using System.Threading.Tasks;

namespace Domino.Infrastructure.Api
{
    public sealed class DominoApiConfiguration
    {
        public Uri Endpoint { get; }
        public int TimeoutSeconds { get; }
        public bool IsAvailable => Endpoint != null;
        public DominoApiConfiguration(bool enabled, string baseUrl, int timeoutSeconds = 15,
            string environment = null, bool isDevelopmentContext = false)
        {
            TimeoutSeconds = Math.Max(1, Math.Min(60, timeoutSeconds));
            if (enabled && Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttps || IsLocalHttp(uri, environment, isDevelopmentContext)) && string.IsNullOrEmpty(uri.UserInfo) &&
                string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment))
                Endpoint = new Uri(uri.AbsoluteUri.TrimEnd('/') + "/api/v1/player/bootstrap");
        }
        static bool IsLocalHttp(Uri uri, string environment, bool isDevelopmentContext)
        {
            if (uri.Scheme != Uri.UriSchemeHttp || environment != "LOCAL" || !isDevelopmentContext) return false;
            if (uri.Host != "localhost" && uri.Host != "127.0.0.1") return false;
            // Uri canonicalizes numeric aliases such as 127.1. Keep the explicit host allowlist literal too.
            var authority = uri.OriginalString.Substring(uri.Scheme.Length + 3).Split('/', '?', '#')[0];
            var host = authority.Split(':')[0];
            return string.Equals(host, uri.Host, StringComparison.OrdinalIgnoreCase);
        }
    }
    public interface IDominoApiClient
    {
        bool IsAvailable { get; }
        Task<PlayerBootstrapResponseDto> BootstrapAsync(string language, CancellationToken cancellationToken);
        Task<PlayerBootstrapResponseDto> UpdateDisplayNameAsync(string displayName, CancellationToken cancellationToken);
    }
    public interface IApiJsonCodec
    {
        string Serialize(PlayerBootstrapRequestDto request);
        string SerializeDisplayName(string displayName);
        PlayerBootstrapResponseDto ReadSuccess(string json);
        ApiErrorDto ReadError(string json);
    }
    public sealed class ApiHttpResponse
    {
        public long Status { get; }
        public string Body { get; }
        public ApiHttpResponse(long status, string body) { Status = status; Body = body; }
    }
    public interface IApiTransport
    {
        Task<ApiHttpResponse> SendAsync(string method, Uri url, string json, string token, int timeoutSeconds, CancellationToken cancellationToken);
    }
}
