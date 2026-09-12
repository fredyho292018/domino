using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Domino.Realtime
{
    public interface IRealtimeSocket : IDisposable
    {
        Task ConnectAsync(Uri endpoint, CancellationToken cancellation);
        Task SendAsync(string message, CancellationToken cancellation);
        Task<string> ReceiveAsync(CancellationToken cancellation);
    }
    // One receive loop and serialized sends. No token retained in fields or diagnostics.
    public sealed class ClientRealtimeSocket : IRealtimeSocket
    {
        readonly ClientWebSocket socket = new ClientWebSocket();
        readonly SemaphoreSlim sending = new SemaphoreSlim(1, 1);
        public ClientRealtimeSocket() { socket.Options.KeepAliveInterval = TimeSpan.Zero; }
        public Task ConnectAsync(Uri endpoint, CancellationToken cancellation) => socket.ConnectAsync(endpoint, cancellation);
        public async Task SendAsync(string message, CancellationToken cancellation)
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            await sending.WaitAsync(cancellation);
            try { await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellation); }
            finally { Array.Clear(bytes, 0, bytes.Length); sending.Release(); }
        }
        public async Task<string> ReceiveAsync(CancellationToken cancellation)
        {
            var buffer = new byte[4096];
            using var message = new MemoryStream();
            while (true) {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellation);
                if (result.MessageType == WebSocketMessageType.Close) throw new RealtimeFailure("CLOSED", result.CloseStatus != WebSocketCloseStatus.PolicyViolation && result.CloseStatus != WebSocketCloseStatus.ProtocolError);
                if (result.MessageType != WebSocketMessageType.Text || message.Length + result.Count > RealtimeProtocol.MaxBytes) throw new RealtimeFailure("PROTOCOL");
                message.Write(buffer, 0, result.Count);
                if (result.EndOfMessage) return new UTF8Encoding(false, true).GetString(message.ToArray());
            }
        }
        public void Dispose() { socket.Abort(); socket.Dispose(); /* outstanding send owns semaphore until completion */ }
    }
}
