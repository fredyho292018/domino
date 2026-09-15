using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Domino.Infrastructure.Api
{
    // Called on Unity's main thread; Task.Yield resumes on its synchronization context.
    public sealed class UnityApiTransport : IApiTransport
    {
        public async Task<ApiHttpResponse> SendAsync(string method, Uri url, string json, string token, int timeoutSeconds, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (method != "POST" && method != "PUT" && method != "GET" && method != "DELETE") throw new ArgumentException(nameof(method));
            using var request = new UnityWebRequest(url, method);
            if (method != "GET" && json != null) request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + token);
            request.timeout = timeoutSeconds;
            request.redirectLimit = 0;
            var operation = request.SendWebRequest();
            var context = SynchronizationContext.Current;
            bool finished = false;
            // Shutdown is raised on Unity's main thread: abort immediately, even if no next frame runs.
            // Timer cancellation is marshalled back to that thread before touching Unity native state.
            using var cancellation = cancellationToken.Register(() =>
            {
                if (SynchronizationContext.Current == context) { if (!finished) request.Abort(); }
                else context?.Post(_ => { if (!finished) request.Abort(); }, null);
            });
            try
            {
                while (!operation.isDone)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Yield();
                }
                cancellationToken.ThrowIfCancellationRequested();
                if (request.responseCode == 0)
                    throw new DominoApiException(request.error?.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0
                        || request.error?.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0 ? ApiFailure.Timeout : ApiFailure.Transport);
                return new ApiHttpResponse(request.responseCode, request.downloadHandler.text);
            }
            finally { finished = true; if (!operation.isDone) request.Abort(); }
        }
    }
}
